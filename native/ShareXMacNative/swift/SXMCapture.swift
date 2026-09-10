// Still capture via ScreenCaptureKit.
//
// Capture targets are resolved before any ShareX overlay is shown, ShareX's own
// windows are removed through the content filter rather than by racing
// hide/show calls, and mixed-scale desktop composition uses an explicit,
// reported composition scale (PROJECT-SPEC.md section 5).

import Foundation
import AppKit
import CoreGraphics
import ImageIO
import ScreenCaptureKit
import UniformTypeIdentifiers

struct SXMCaptureOutput {
    let image: CGImage
    /// Rectangle actually captured, in CoreGraphics global points.
    let sourceRectGlobalPoints: CGRect
    /// Pixels per point applied to the whole composition.
    let compositionScale: CGFloat
    /// Displays that contributed, in contribution order.
    let contributingDisplays: [CGDirectDisplayID]
    let cursorIncluded: Bool
}

enum SXMCapture {

    /// Bound: refuse absurd allocations rather than dying with an OOM.
    static let maxPixels = 400_000_000   // ~1.6 GB at 4 bytes/pixel

    /// Gate for every capture and recording entry point.
    ///
    /// Uses `ensureScreenRecording()` rather than a bare preflight so that a
    /// first-time user actually sees the macOS prompt instead of an unexplained
    /// failure. Only a stored denial reaches the throw below.
    static func requirePermission() throws {
        if SXMPermissions.ensureScreenRecording() == .granted {
            return
        }

        throw SXMFailure(.permissionRequired,
                         "Screen Recording permission is required. macOS has stored a "
                             + "decision for this app, so it must be changed in System Settings.",
                         detail: ["permission": "screenRecording",
                                  "settingsUrl": "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture"])
    }

    static func shareableContent() async throws -> SCShareableContent {
        do {
            return try await SCShareableContent.excludingDesktopWindows(false,
                                                                       onScreenWindowsOnly: false)
        } catch {
            // SCShareableContent fails with a TCC error when permission is absent.
            if SXMPermissions.screenRecording() != .granted {
                throw SXMFailure(.permissionRequired,
                                 "Screen Recording permission is required.",
                                 detail: ["permission": "screenRecording"])
            }
            throw SXMFailure(.internalFailure, "Shareable content unavailable: \(error.localizedDescription)")
        }
    }

    /// Applications whose windows must never appear in a capture: our own
    /// overlays, magnifier and recording controls.
    static func selfApplications(in content: SCShareableContent) -> [SCRunningApplication] {
        let pid = ProcessInfo.processInfo.processIdentifier
        return content.applications.filter { $0.processID == pid }
    }

    // MARK: - Rect capture with explicit composition

    static func captureGlobalRect(_ requested: CGRect,
                                  compositionScale requestedScale: CGFloat?,
                                  showsCursor: Bool,
                                  excludeSelf: Bool) async throws -> SXMCaptureOutput {
        try requirePermission()
        let content = try await shareableContent()

        let displays = SXMDisplayInfo.all().filter { $0.boundsGlobalPoints.intersects(requested) }
        guard !displays.isEmpty else {
            throw SXMFailure(.invalidConfiguration,
                             "The requested rectangle does not intersect any active display.",
                             detail: ["requested": requested.asJson,
                                      "space": SXMSpace.cgGlobalPoints.rawValue])
        }

        // Explicit composition scale: there is no single physical pixel scale for
        // a virtual desktop spanning mixed-scale screens, so choose the highest
        // contributing scale and report it with the artifact.
        let scale = requestedScale ?? (displays.map(\.scale).max() ?? 1.0)
        let aligned = SXMGeometry.pixelAligned(requested, scale: scale)

        let outWidth = Int((aligned.width * scale).rounded())
        let outHeight = Int((aligned.height * scale).rounded())
        guard outWidth > 0, outHeight > 0 else {
            throw SXMFailure(.invalidConfiguration, "Computed capture size is empty.")
        }
        guard outWidth * outHeight <= maxPixels else {
            throw SXMFailure(.invalidConfiguration,
                             "Requested capture exceeds the supported pixel bound.",
                             detail: ["pixels": outWidth * outHeight, "maxPixels": maxPixels])
        }

        // Single-display fast path avoids a redundant composite.
        if displays.count == 1, let only = displays.first,
           let scDisplay = content.displays.first(where: { $0.displayID == only.displayID }) {
            let local = SXMGeometry.cgGlobalToDisplayPoints(aligned, displayBounds: only.boundsGlobalPoints)
            let image = try await captureDisplayRegion(scDisplay,
                                                       content: content,
                                                       sourceRectDisplayPoints: local,
                                                       outputPixelSize: CGSize(width: outWidth, height: outHeight),
                                                       showsCursor: showsCursor,
                                                       excludeSelf: excludeSelf)
            return SXMCaptureOutput(image: image,
                                    sourceRectGlobalPoints: aligned,
                                    compositionScale: scale,
                                    contributingDisplays: [only.displayID],
                                    cursorIncluded: showsCursor)
        }

        guard let context = CGContext(data: nil,
                                      width: outWidth,
                                      height: outHeight,
                                      bitsPerComponent: 8,
                                      bytesPerRow: 0,
                                      space: CGColorSpace(name: CGColorSpace.sRGB)!,
                                      bitmapInfo: CGImageAlphaInfo.premultipliedFirst.rawValue
                                        | CGBitmapInfo.byteOrder32Little.rawValue) else {
            throw SXMFailure(.internalFailure, "Could not allocate the composition bitmap.")
        }
        context.interpolationQuality = .high

        var contributed: [CGDirectDisplayID] = []
        for display in displays {
            guard let scDisplay = content.displays.first(where: { $0.displayID == display.displayID }) else {
                continue
            }
            let piece = aligned.intersection(display.boundsGlobalPoints)
            if piece.isEmpty { continue }

            let local = SXMGeometry.cgGlobalToDisplayPoints(piece, displayBounds: display.boundsGlobalPoints)
            let pieceW = Int((piece.width * scale).rounded())
            let pieceH = Int((piece.height * scale).rounded())
            if pieceW <= 0 || pieceH <= 0 { continue }

            let image = try await captureDisplayRegion(scDisplay,
                                                       content: content,
                                                       sourceRectDisplayPoints: local,
                                                       outputPixelSize: CGSize(width: pieceW, height: pieceH),
                                                       showsCursor: showsCursor,
                                                       excludeSelf: excludeSelf)

            // CGContext is bottom-left origin; the requested space is top-left.
            let dx = (piece.minX - aligned.minX) * scale
            let dyTop = (piece.minY - aligned.minY) * scale
            let dy = CGFloat(outHeight) - dyTop - CGFloat(pieceH)
            context.draw(image, in: CGRect(x: dx, y: dy, width: CGFloat(pieceW), height: CGFloat(pieceH)))
            contributed.append(display.displayID)
        }

        guard let composed = context.makeImage() else {
            throw SXMFailure(.internalFailure, "Composition produced no image.")
        }
        return SXMCaptureOutput(image: composed,
                                sourceRectGlobalPoints: aligned,
                                compositionScale: scale,
                                contributingDisplays: contributed,
                                cursorIncluded: showsCursor)
    }

    private static func captureDisplayRegion(_ display: SCDisplay,
                                             content: SCShareableContent,
                                             sourceRectDisplayPoints: CGRect,
                                             outputPixelSize: CGSize,
                                             showsCursor: Bool,
                                             excludeSelf: Bool) async throws -> CGImage {
        let excluded = excludeSelf ? selfApplications(in: content) : []
        let filter = SCContentFilter(display: display,
                                     excludingApplications: excluded,
                                     exceptingWindows: [])

        let config = SCStreamConfiguration()
        config.sourceRect = sourceRectDisplayPoints
        config.width = Int(outputPixelSize.width)
        config.height = Int(outputPixelSize.height)
        config.showsCursor = showsCursor
        config.captureResolution = .best
        config.scalesToFit = false
        config.colorSpaceName = CGColorSpace.sRGB
        config.ignoreShadowsSingleWindow = false

        do {
            return try await SCScreenshotManager.captureImage(contentFilter: filter,
                                                              configuration: config)
        } catch {
            throw SXMFailure(.internalFailure,
                             "Screenshot failed: \(error.localizedDescription)",
                             detail: ["sourceRect": sourceRectDisplayPoints.asJson,
                                      "space": SXMSpace.displayPoints.rawValue])
        }
    }

    // MARK: - Window capture

    static func captureWindow(_ windowId: CGWindowID,
                              showsCursor: Bool,
                              includeShadow: Bool,
                              scale requestedScale: CGFloat?) async throws -> SXMCaptureOutput {
        try requirePermission()
        let content = try await shareableContent()

        guard let window = content.windows.first(where: { $0.windowID == windowId }) else {
            throw SXMFailure(.targetDisappeared,
                             "The requested window no longer exists.",
                             detail: ["windowId": Int(windowId)])
        }

        let frame = window.frame                          // CG global points
        let display = SXMDisplayInfo.all().first { $0.boundsGlobalPoints.intersects(frame) }
        let scale = requestedScale ?? display?.scale ?? 1.0

        let width = Int((frame.width * scale).rounded())
        let height = Int((frame.height * scale).rounded())
        guard width > 0, height > 0 else {
            throw SXMFailure(.targetDisappeared, "The requested window has an empty frame.")
        }
        guard width * height <= maxPixels else {
            throw SXMFailure(.invalidConfiguration, "Window capture exceeds the supported pixel bound.")
        }

        let filter = SCContentFilter(desktopIndependentWindow: window)
        let config = SCStreamConfiguration()
        config.width = width
        config.height = height
        config.showsCursor = showsCursor
        config.captureResolution = .best
        config.scalesToFit = false
        config.colorSpaceName = CGColorSpace.sRGB
        // ScreenCaptureKit's only public shadow control for single-window capture.
        // It is not equivalent to the Windows DWM frame options; the difference is
        // recorded rather than claimed as parity.
        config.ignoreShadowsSingleWindow = !includeShadow

        do {
            let image = try await SCScreenshotManager.captureImage(contentFilter: filter,
                                                                   configuration: config)
            return SXMCaptureOutput(image: image,
                                    sourceRectGlobalPoints: frame,
                                    compositionScale: scale,
                                    contributingDisplays: display.map { [$0.displayID] } ?? [],
                                    cursorIncluded: showsCursor)
        } catch {
            throw SXMFailure(.targetDisappeared,
                             "Window capture failed: \(error.localizedDescription)",
                             detail: ["windowId": Int(windowId)])
        }
    }

    // MARK: - Encoding

    static func encodePng(_ image: CGImage) throws -> Data {
        let data = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(data as CFMutableData,
                                                                 UTType.png.identifier as CFString,
                                                                 1, nil) else {
            throw SXMFailure(.encoderFailure, "Could not create a PNG encoder.")
        }
        CGImageDestinationAddImage(destination, image, nil)
        guard CGImageDestinationFinalize(destination) else {
            throw SXMFailure(.encoderFailure, "PNG encoding failed.")
        }
        return data as Data
    }

    static func metadata(_ output: SXMCaptureOutput) -> [String: Any] {
        [
            "pixelWidth": output.image.width,
            "pixelHeight": output.image.height,
            "compositionScale": output.compositionScale,
            "sourceRect": output.sourceRectGlobalPoints.asJson,
            "sourceSpace": SXMSpace.cgGlobalPoints.rawValue,
            "displays": output.contributingDisplays.map { Int($0) },
            "cursorIncluded": output.cursorIncluded,
            "colorSpace": "sRGB",
            "alpha": "premultiplied-first",
            "format": "png",
            "capturedAtUnixMs": Int(Date().timeIntervalSince1970 * 1000)
        ]
    }
}
