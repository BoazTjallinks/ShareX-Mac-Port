// Operation routing table. Operation names are part of the ABI; unknown names
// fail deterministically with SXM_UNKNOWN_OPERATION.

import Foundation
import AppKit
import CoreGraphics

enum SXMRoutes {

    static func build() -> [String: SXMHandler] {
        var routes: [String: SXMHandler] = [:]

        routes["abi.version"] = { ctx in
            ctx.succeed(["abiVersion": Int(SXM_ABI)])
        }

        routes["capabilities.get"] = { ctx in
            ctx.succeed(SXMCapabilities.snapshot())
        }

        routes["permissions.get"] = { ctx in
            ctx.succeed(SXMPermissions.snapshot())
        }

        routes["permissions.request"] = { ctx in
            let which = try ctx.string("permission")
            switch which {
            case "screenRecording":
                // Raises the standard macOS prompt. Never alters TCC directly.
                let granted = SXMPermissions.requestScreenRecording()
                ctx.succeed(["permission": which,
                             "state": granted ? SXMPermissionState.granted.rawValue
                                              : SXMPermissionState.required.rawValue,
                             "note": "If macOS shows no prompt the decision is already stored; the user must change it in System Settings."])
            case "accessibility":
                let prompt = ctx.bool("prompt", default: true)
                let state = SXMPermissions.requestAccessibility(prompt: prompt)
                ctx.succeed(["permission": which, "state": state.rawValue])
            case "microphone":
                SXMPermissions.requestMicrophone { state in
                    ctx.succeed(["permission": which, "state": state.rawValue])
                }
            default:
                throw SXMFailure(.invalidInput, "Unknown permission '\(which)'.")
            }
        }

        routes["displays.list"] = { ctx in
            let displays = SXMDisplayInfo.all()
            let active = SXMDisplayInfo.activeDisplay()
            ctx.succeed([
                "displays": displays.map { $0.asJson },
                "activeDisplayId": active.map { Int($0.displayID) } as Any,
                "mainDisplayId": Int(CGMainDisplayID()),
                "virtualDesktop": SXMGeometry.virtualDesktopBounds().asJson,
                "space": SXMSpace.cgGlobalPoints.rawValue
            ])
        }

        routes["windows.list"] = { ctx in
            let onScreenOnly = ctx.bool("onScreenOnly", default: true)
            let windows = SXMWindowInfo.all(onScreenOnly: onScreenOnly)
            let active = SXMWindowInfo.activeWindow()
            // Window titles are redacted by macOS without Screen Recording
            // permission. Report that instead of presenting blanks as real titles.
            let titlesAvailable = SXMPermissions.screenRecording() == .granted
            ctx.succeed([
                "windows": windows.map { $0.asJson },
                "activeWindowId": active.map { Int($0.windowNumber) } as Any,
                "titlesAvailable": titlesAvailable,
                "space": SXMSpace.cgGlobalPoints.rawValue
            ])
        }

        routes["capture.screenshot"] = { ctx in
            let target = ctx.dictionary("target") ?? ["kind": "allDisplays"]
            let kind = (target["kind"] as? String) ?? "allDisplays"
            let showsCursor = ctx.bool("showsCursor", default: false)
            let includeShadow = ctx.bool("includeShadow", default: true)
            let excludeSelf = ctx.bool("excludeSelf", default: true)
            let requestedScale = (ctx.args["compositionScale"] as? NSNumber).map { CGFloat($0.doubleValue) }
            let outputPath = ctx.args["outputPath"] as? String

            let work = Task {
                do {
                    let output: SXMCaptureOutput
                    switch kind {
                    case "allDisplays":
                        output = try await SXMCapture.captureGlobalRect(SXMGeometry.virtualDesktopBounds(),
                                                                        compositionScale: requestedScale,
                                                                        showsCursor: showsCursor,
                                                                        excludeSelf: excludeSelf)
                    case "display", "activeDisplay":
                        let display: SXMDisplayInfo?
                        if kind == "activeDisplay" {
                            display = SXMDisplayInfo.activeDisplay()
                        } else {
                            guard let id = (target["displayId"] as? NSNumber)?.uint32Value else {
                                throw SXMFailure(.invalidInput, "target.displayId is required for kind 'display'.")
                            }
                            display = SXMDisplayInfo.find(CGDirectDisplayID(id))
                        }
                        guard let display else {
                            throw SXMFailure(.targetDisappeared, "The requested display is not active.")
                        }
                        output = try await SXMCapture.captureGlobalRect(display.boundsGlobalPoints,
                                                                        compositionScale: requestedScale,
                                                                        showsCursor: showsCursor,
                                                                        excludeSelf: excludeSelf)
                    case "region":
                        guard let raw = target["rect"] as? [String: Any],
                              let x = (raw["x"] as? NSNumber)?.doubleValue,
                              let y = (raw["y"] as? NSNumber)?.doubleValue,
                              let w = (raw["width"] as? NSNumber)?.doubleValue,
                              let h = (raw["height"] as? NSNumber)?.doubleValue, w > 0, h > 0 else {
                            throw SXMFailure(.invalidInput, "target.rect is required for kind 'region'.")
                        }
                        let space = (target["space"] as? String) ?? SXMSpace.cgGlobalPoints.rawValue
                        var rect = CGRect(x: x, y: y, width: w, height: h)
                        if space == SXMSpace.appKitPoints.rawValue {
                            rect = SXMGeometry.appKitToCgGlobal(rect)
                        } else if space != SXMSpace.cgGlobalPoints.rawValue {
                            throw SXMFailure(.invalidInput, "Unsupported target.space '\(space)'.")
                        }
                        output = try await SXMCapture.captureGlobalRect(rect,
                                                                        compositionScale: requestedScale,
                                                                        showsCursor: showsCursor,
                                                                        excludeSelf: excludeSelf)
                    case "window", "activeWindow":
                        let windowId: CGWindowID
                        if kind == "activeWindow" {
                            guard let active = SXMWindowInfo.activeWindow() else {
                                throw SXMFailure(.targetDisappeared, "No foreground window could be resolved.")
                            }
                            windowId = active.windowNumber
                        } else {
                            guard let id = (target["windowId"] as? NSNumber)?.uint32Value else {
                                throw SXMFailure(.invalidInput, "target.windowId is required for kind 'window'.")
                            }
                            windowId = CGWindowID(id)
                        }
                        output = try await SXMCapture.captureWindow(windowId,
                                                                    showsCursor: showsCursor,
                                                                    includeShadow: includeShadow,
                                                                    scale: requestedScale)
                    default:
                        throw SXMFailure(.invalidInput, "Unknown capture target kind '\(kind)'.")
                    }

                    if ctx.isCancelled {
                        ctx.cancelled()
                        return
                    }

                    let png = try SXMCapture.encodePng(output.image)
                    var meta = SXMCapture.metadata(output)
                    meta["targetKind"] = kind

                    if let outputPath {
                        do {
                            try png.write(to: URL(fileURLWithPath: outputPath), options: .atomic)
                            meta["outputPath"] = outputPath
                            meta["delivery"] = "file"
                            ctx.succeed(meta)
                        } catch {
                            throw SXMFailure(.localIOFailure,
                                             "Could not write the capture to '\(outputPath)': \(error.localizedDescription)")
                        }
                    } else {
                        meta["delivery"] = "blob"
                        ctx.succeed(meta, blob: png)
                    }
                } catch let failure as SXMFailure {
                    ctx.fail(failure)
                } catch {
                    ctx.fail(.internalFailure, "Capture failed: \(error.localizedDescription)")
                }
            }
            ctx.onCancel { work.cancel() }
        }

        // MARK: - Clipboard and Finder (upstream ClipboardHelpers / FileHelpers)

        // Separate operations rather than one generic "copy", because upstream
        // treats image / file / file-path / folder-path as distinct, mutually
        // exclusive after-capture tasks (PROJECT-SPEC.md section 8).
        routes["clipboard.copyImageFile"] = { ctx in
            try SXMClipboard.copyImage(fromFile: try ctx.string("path"))
            ctx.succeed(["copied": "image"])
        }

        routes["clipboard.copyImageData"] = { ctx in
            guard let base64 = ctx.args["pngBase64"] as? String,
                  let data = Data(base64Encoded: base64) else {
                throw SXMFailure(.invalidInput, "pngBase64 must be base64-encoded PNG bytes.")
            }
            try SXMClipboard.copyImage(pngData: data)
            ctx.succeed(["copied": "image", "bytes": data.count])
        }

        routes["clipboard.copyText"] = { ctx in
            try SXMClipboard.copyText(try ctx.string("text"))
            ctx.succeed(["copied": "text"])
        }

        routes["clipboard.copyFile"] = { ctx in
            try SXMClipboard.copyFile(try ctx.string("path"))
            ctx.succeed(["copied": "file"])
        }

        routes["clipboard.get"] = { ctx in
            ctx.succeed(SXMClipboard.snapshot())
        }

        routes["clipboard.clear"] = { ctx in
            SXMClipboard.clear()
            ctx.succeed([:])
        }

        routes["finder.reveal"] = { ctx in
            try SXMFinder.reveal(try ctx.string("path"))
            ctx.succeed([:])
        }

        routes["finder.open"] = { ctx in
            try SXMFinder.open(try ctx.string("path"))
            ctx.succeed([:])
        }

        routes["url.open"] = { ctx in
            try SXMFinder.openUrl(try ctx.string("url"))
            ctx.succeed([:])
        }

        // MARK: - Recording (PROJECT-SPEC.md section 6)

        // Completes as soon as the session is RUNNING; it does not block until the
        // recording ends. Progress and state arrive on the event channel keyed by
        // sessionId, never through this one-shot completion.
        routes["recording.start"] = { ctx in
            let work = SXMRecordingSessionFactory.start(
                args: ctx.args,
                isCancelled: { ctx.isCancelled },
                completion: { result in
                    switch result {
                    case .success(let payload): ctx.succeed(payload)
                    case .failure(let failure): ctx.fail(failure)
                    }
                })
            ctx.onCancel { work.cancel() }
        }

        // Each control route resolves the session by id. An unknown id is
        // targetDisappeared, not a silent success.
        func session(_ ctx: SXMContext) throws -> SXMRecordingSession {
            guard let raw = ctx.args["sessionId"] as? NSNumber else {
                throw SXMFailure(.invalidInput, "sessionId is required.")
            }
            let id = raw.uint64Value
            guard let found = SXMRecordingRegistry.shared.find(id) else {
                throw SXMFailure(.targetDisappeared,
                                 "No live recording session with id \(id).",
                                 detail: ["sessionId": Int(id)])
            }
            return found
        }

        routes["recording.pause"]  = { ctx in ctx.succeed(try session(ctx).pause()) }
        routes["recording.resume"] = { ctx in ctx.succeed(try session(ctx).resume()) }
        routes["recording.abort"]  = { ctx in ctx.succeed(try session(ctx).abort()) }
        routes["recording.status"] = { ctx in ctx.succeed(try session(ctx).status()) }

        // Unlike the others, stop completes only after finalization, so the caller
        // can rely on the returned path being a complete, playable file.
        routes["recording.stop"] = { ctx in
            let target = try session(ctx)
            target.stop { result in
                switch result {
                case .success(let payload): ctx.succeed(payload)
                case .failure(let failure): ctx.fail(failure)
                }
            }
        }

        // Discover what this machine can actually encode. The spec forbids
        // assuming a codec is available (section 6): an unavailable codec stays
        // unavailable rather than being silently substituted.
        routes["media.encoders"] = { ctx in
            ctx.succeed(SXMEncoderProbe.snapshot())
        }

        return routes
    }
}
