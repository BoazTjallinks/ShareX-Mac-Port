// Coordinate subsystem.
//
// PROJECT-SPEC.md section 5 makes this a named subsystem: AppKit points,
// CoreGraphics global points, capture pixel rectangles and Avalonia
// device-independent pixels must never be mixed implicitly. Every rectangle
// that crosses the ABI is tagged with the space it belongs to.

import Foundation
import AppKit
import CoreGraphics

/// Named coordinate spaces. The string values are part of the ABI payloads.
enum SXMSpace: String {
    /// CoreGraphics global display space: points, origin at the top-left of the
    /// main display, +y downward. This is what CGDisplayBounds returns.
    case cgGlobalPoints = "cg-global-points"
    /// AppKit screen space: points, origin at the bottom-left of the main
    /// screen, +y upward. This is what NSScreen.frame returns.
    case appKitPoints = "appkit-points"
    /// Pixels inside a single display, origin at that display's top-left.
    case displayPixels = "display-pixels"
    /// Points inside a single display, origin at that display's top-left.
    /// SCStreamConfiguration.sourceRect uses this space.
    case displayPoints = "display-points"
}

enum SXMGeometry {

    /// Height of the AppKit coordinate system, used to flip between the
    /// bottom-left AppKit origin and the top-left CoreGraphics origin.
    static var appKitFlipHeight: CGFloat {
        // NSScreen.screens[0] is the screen containing the menu bar and defines
        // the AppKit origin. CGDisplayBounds(CGMainDisplayID()) is the same
        // rectangle expressed top-left-origin.
        NSScreen.screens.first?.frame.maxY ?? CGDisplayBounds(CGMainDisplayID()).height
    }

    static func appKitToCgGlobal(_ rect: CGRect) -> CGRect {
        let flip = appKitFlipHeight
        return CGRect(x: rect.origin.x,
                      y: flip - rect.origin.y - rect.height,
                      width: rect.width,
                      height: rect.height)
    }

    static func cgGlobalToAppKit(_ rect: CGRect) -> CGRect {
        let flip = appKitFlipHeight
        return CGRect(x: rect.origin.x,
                      y: flip - rect.origin.y - rect.height,
                      width: rect.width,
                      height: rect.height)
    }

    /// Converts a rectangle in CoreGraphics global points into the display-local
    /// point space used by SCStreamConfiguration.sourceRect.
    static func cgGlobalToDisplayPoints(_ rect: CGRect, displayBounds: CGRect) -> CGRect {
        rect.offsetBy(dx: -displayBounds.origin.x, dy: -displayBounds.origin.y)
    }

    /// Rounds a point rectangle out to whole device pixels for a given scale.
    /// Rounding outward keeps requested content inside the result; the caller
    /// records the adjustment so a one-pixel fixture can assert on it.
    static func pixelAligned(_ rect: CGRect, scale: CGFloat) -> CGRect {
        guard scale > 0 else { return rect.integral }
        let minX = (rect.minX * scale).rounded(.down) / scale
        let minY = (rect.minY * scale).rounded(.down) / scale
        let maxX = (rect.maxX * scale).rounded(.up) / scale
        let maxY = (rect.maxY * scale).rounded(.up) / scale
        return CGRect(x: minX, y: minY, width: maxX - minX, height: maxY - minY)
    }

    /// The union of all active displays in CoreGraphics global points.
    static func virtualDesktopBounds() -> CGRect {
        var union = CGRect.null
        for display in SXMDisplayInfo.all() {
            union = union.union(display.boundsGlobalPoints)
        }
        return union.isNull ? CGDisplayBounds(CGMainDisplayID()) : union
    }
}
