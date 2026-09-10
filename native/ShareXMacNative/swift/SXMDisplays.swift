// Display and window enumeration.
//
// Display identity is stable (CGDirectDisplayID plus vendor/model/serial);
// window numbers are not and may expire between enumeration and capture
// (PROJECT-SPEC.md section 5).

import Foundation
import AppKit
import CoreGraphics

struct SXMDisplayInfo {
    let displayID: CGDirectDisplayID
    let boundsGlobalPoints: CGRect
    let pixelWidth: Int
    let pixelHeight: Int
    let scale: CGFloat
    let isMain: Bool
    let name: String
    let rotationDegrees: Double

    static func all() -> [SXMDisplayInfo] {
        var count: UInt32 = 0
        guard CGGetActiveDisplayList(0, nil, &count) == .success, count > 0 else { return [] }
        var ids = [CGDirectDisplayID](repeating: 0, count: Int(count))
        guard CGGetActiveDisplayList(count, &ids, &count) == .success else { return [] }

        let screensByNumber: [CGDirectDisplayID: NSScreen] = NSScreen.screens.reduce(into: [:]) { map, screen in
            if let number = screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber {
                map[CGDirectDisplayID(number.uint32Value)] = screen
            }
        }

        let mainID = CGMainDisplayID()
        return ids.prefix(Int(count)).map { id in
            let bounds = CGDisplayBounds(id)
            let screen = screensByNumber[id]
            let scale = screen?.backingScaleFactor ?? 1.0
            let mode = CGDisplayCopyDisplayMode(id)
            // CGDisplayModeGetPixelWidth reports true backing pixels; the plain
            // width is in points for scaled (HiDPI) modes.
            let pixelWidth = mode.map { $0.pixelWidth } ?? Int(bounds.width * scale)
            let pixelHeight = mode.map { $0.pixelHeight } ?? Int(bounds.height * scale)
            return SXMDisplayInfo(displayID: id,
                                  boundsGlobalPoints: bounds,
                                  pixelWidth: pixelWidth,
                                  pixelHeight: pixelHeight,
                                  scale: scale,
                                  isMain: id == mainID,
                                  name: screen?.localizedName ?? "Display \(id)",
                                  rotationDegrees: CGDisplayRotation(id))
        }
    }

    static func find(_ id: CGDirectDisplayID) -> SXMDisplayInfo? {
        all().first { $0.displayID == id }
    }

    /// The display holding the mouse cursor. Matches the upstream notion of
    /// "active monitor" more closely than key-window ownership does.
    static func activeDisplay() -> SXMDisplayInfo? {
        let mouse = NSEvent.mouseLocation                       // AppKit points
        let global = CGPoint(x: mouse.x, y: SXMGeometry.appKitFlipHeight - mouse.y)
        let displays = all()
        return displays.first { $0.boundsGlobalPoints.contains(global) }
            ?? displays.first { $0.isMain }
            ?? displays.first
    }

    var asJson: [String: Any] {
        [
            "displayId": Int(displayID),
            "name": name,
            "isMain": isMain,
            "scale": scale,
            "rotationDegrees": rotationDegrees,
            "space": SXMSpace.cgGlobalPoints.rawValue,
            "bounds": boundsGlobalPoints.asJson,
            "pixelWidth": pixelWidth,
            "pixelHeight": pixelHeight
        ]
    }
}

struct SXMWindowInfo {
    let windowNumber: CGWindowID
    let title: String
    let ownerName: String
    let ownerPid: pid_t
    let boundsGlobalPoints: CGRect
    let layer: Int
    let isOnScreen: Bool

    /// Enumerates on-screen windows via CGWindowListCopyWindowInfo. This list is
    /// available without Screen Recording permission but window *titles* are
    /// redacted when that permission is absent; the caller must report that
    /// rather than presenting empty titles as real ones.
    static func all(onScreenOnly: Bool = true, excludeDesktop: Bool = true) -> [SXMWindowInfo] {
        var options: CGWindowListOption = [.excludeDesktopElements]
        if !excludeDesktop { options = [] }
        if onScreenOnly { options.insert(.optionOnScreenOnly) }

        guard let raw = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]] else {
            return []
        }
        let selfPid = ProcessInfo.processInfo.processIdentifier
        return raw.compactMap { entry -> SXMWindowInfo? in
            guard let number = entry[kCGWindowNumber as String] as? NSNumber,
                  let boundsDict = entry[kCGWindowBounds as String] as? [String: Any],
                  let bounds = CGRect(dictionaryRepresentation: boundsDict as CFDictionary) else {
                return nil
            }
            let pid = (entry[kCGWindowOwnerPID as String] as? NSNumber)?.int32Value ?? 0
            // Never offer our own overlays as capture targets.
            if pid == selfPid { return nil }
            let layer = (entry[kCGWindowLayer as String] as? NSNumber)?.intValue ?? 0
            return SXMWindowInfo(
                windowNumber: CGWindowID(number.uint32Value),
                title: (entry[kCGWindowName as String] as? String) ?? "",
                ownerName: (entry[kCGWindowOwnerName as String] as? String) ?? "",
                ownerPid: pid,
                boundsGlobalPoints: bounds,
                layer: layer,
                isOnScreen: (entry[kCGWindowIsOnscreen as String] as? NSNumber)?.boolValue ?? false)
        }
    }

    /// The frontmost ordinary window of the frontmost application.
    /// Resolved BEFORE ShareX activates any overlay (PROJECT-SPEC.md section 5).
    static func activeWindow() -> SXMWindowInfo? {
        let frontPid = NSWorkspace.shared.frontmostApplication?.processIdentifier
        let windows = all(onScreenOnly: true)
        if let frontPid {
            if let match = windows.first(where: { $0.ownerPid == frontPid && $0.layer == 0 }) {
                return match
            }
        }
        return windows.first { $0.layer == 0 }
    }

    var asJson: [String: Any] {
        [
            "windowId": Int(windowNumber),
            "title": title,
            "ownerName": ownerName,
            "ownerPid": Int(ownerPid),
            "layer": layer,
            "isOnScreen": isOnScreen,
            "space": SXMSpace.cgGlobalPoints.rawValue,
            "bounds": boundsGlobalPoints.asJson
        ]
    }
}
