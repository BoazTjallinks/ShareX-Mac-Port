// Recording target resolution.
//
// Reuses the same target vocabulary as SXMRoutes' capture.screenshot
// ({"kind":"display"|"activeDisplay"|"window"|"activeWindow"|"region", ...})
// and the same geometry helpers (SXMGeometry/SXMDisplayInfo/SXMWindowInfo), per
// the recording ABI surface. Unlike capture.screenshot there is no "allDisplays"
// kind here: a live SCStream attaches to exactly one SCDisplay or SCWindow, so a
// region request must resolve to a single physical display rather than the
// manually-composited virtual desktop screenshots can produce.

import Foundation
import AppKit
import CoreGraphics
import ScreenCaptureKit

struct SXMRecordingTargetResolution {
    let contentFilter: SCContentFilter
    let pixelWidth: Int
    let pixelHeight: Int
    /// Sub-rectangle of the display to stream, in display-local points
    /// (SXMSpace.displayPoints). Nil means "the whole display/window".
    let sourceRectDisplayPoints: CGRect?
    let displayId: CGDirectDisplayID?
    let windowId: CGWindowID?
}

enum SXMRecordingTarget {

    static func resolve(_ target: [String: Any],
                        content: SCShareableContent,
                        excludeSelf: Bool) throws -> SXMRecordingTargetResolution {
        let kind = (target["kind"] as? String) ?? "activeDisplay"
        switch kind {
        case "display", "activeDisplay":
            return try resolveDisplay(target, kind: kind, content: content, excludeSelf: excludeSelf)
        case "region":
            return try resolveRegion(target, content: content, excludeSelf: excludeSelf)
        case "window", "activeWindow":
            return try resolveWindow(target, kind: kind, content: content)
        default:
            throw SXMFailure(.invalidInput, "Unknown recording target kind '\(kind)'.")
        }
    }

    private static func resolveDisplay(_ target: [String: Any],
                                       kind: String,
                                       content: SCShareableContent,
                                       excludeSelf: Bool) throws -> SXMRecordingTargetResolution {
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
        guard let scDisplay = content.displays.first(where: { $0.displayID == display.displayID }) else {
            throw SXMFailure(.targetDisappeared, "The requested display is not shareable.",
                             detail: ["displayId": Int(display.displayID)])
        }
        guard display.pixelWidth * display.pixelHeight <= SXMCapture.maxPixels else {
            throw SXMFailure(.invalidConfiguration, "The requested display exceeds the supported pixel bound.")
        }
        let excluded = excludeSelf ? SXMCapture.selfApplications(in: content) : []
        let filter = SCContentFilter(display: scDisplay, excludingApplications: excluded, exceptingWindows: [])
        return SXMRecordingTargetResolution(contentFilter: filter,
                                            pixelWidth: display.pixelWidth,
                                            pixelHeight: display.pixelHeight,
                                            sourceRectDisplayPoints: nil,
                                            displayId: display.displayID,
                                            windowId: nil)
    }

    private static func resolveRegion(_ target: [String: Any],
                                      content: SCShareableContent,
                                      excludeSelf: Bool) throws -> SXMRecordingTargetResolution {
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

        let displays = SXMDisplayInfo.all().filter { $0.boundsGlobalPoints.intersects(rect) }
        // A live SCStream captures one SCDisplay; unlike capture.screenshot there is
        // no per-frame real-time compositing across displays here, so a region that
        // straddles more than one display is a configuration error, not a platform
        // gap (PROJECT-SPEC.md section 6 gives no "allDisplays" recording target).
        guard displays.count == 1, let display = displays.first else {
            throw SXMFailure(.invalidConfiguration,
                             "Recording target.rect must intersect exactly one display; it intersected \(displays.count).",
                             detail: ["requested": rect.asJson, "space": SXMSpace.cgGlobalPoints.rawValue])
        }
        guard let scDisplay = content.displays.first(where: { $0.displayID == display.displayID }) else {
            throw SXMFailure(.targetDisappeared, "The requested display is not shareable.",
                             detail: ["displayId": Int(display.displayID)])
        }

        let scale = display.scale
        let aligned = SXMGeometry.pixelAligned(rect, scale: scale)
        let local = SXMGeometry.cgGlobalToDisplayPoints(aligned, displayBounds: display.boundsGlobalPoints)
        let outWidth = Int((aligned.width * scale).rounded())
        let outHeight = Int((aligned.height * scale).rounded())
        guard outWidth > 0, outHeight > 0 else {
            throw SXMFailure(.invalidConfiguration, "Computed recording size is empty.")
        }
        guard outWidth * outHeight <= SXMCapture.maxPixels else {
            throw SXMFailure(.invalidConfiguration, "Requested recording region exceeds the supported pixel bound.")
        }

        let excluded = excludeSelf ? SXMCapture.selfApplications(in: content) : []
        let filter = SCContentFilter(display: scDisplay, excludingApplications: excluded, exceptingWindows: [])
        return SXMRecordingTargetResolution(contentFilter: filter,
                                            pixelWidth: outWidth,
                                            pixelHeight: outHeight,
                                            sourceRectDisplayPoints: local,
                                            displayId: display.displayID,
                                            windowId: nil)
    }

    private static func resolveWindow(_ target: [String: Any],
                                      kind: String,
                                      content: SCShareableContent) throws -> SXMRecordingTargetResolution {
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
        guard let window = content.windows.first(where: { $0.windowID == windowId }) else {
            throw SXMFailure(.targetDisappeared, "The requested window no longer exists.",
                             detail: ["windowId": Int(windowId)])
        }
        let frame = window.frame                      // CG global points
        let display = SXMDisplayInfo.all().first { $0.boundsGlobalPoints.intersects(frame) }
        let scale = display?.scale ?? 1.0
        let width = Int((frame.width * scale).rounded())
        let height = Int((frame.height * scale).rounded())
        guard width > 0, height > 0 else {
            throw SXMFailure(.targetDisappeared, "The requested window has an empty frame.")
        }
        guard width * height <= SXMCapture.maxPixels else {
            throw SXMFailure(.invalidConfiguration, "Window recording exceeds the supported pixel bound.")
        }
        let filter = SCContentFilter(desktopIndependentWindow: window)
        return SXMRecordingTargetResolution(contentFilter: filter,
                                            pixelWidth: width,
                                            pixelHeight: height,
                                            sourceRectDisplayPoints: nil,
                                            displayId: display?.displayID,
                                            windowId: windowId)
    }
}
