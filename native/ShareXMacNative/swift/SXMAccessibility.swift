// Window/element inspection via the Accessibility API.
//
// Replaces upstream's Win32 child-HWND enumeration (PROJECT-SPEC.md section
// 5: "Accessibility can supply exposed element geometry; custom-rendered
// controls may not expose anything useful. Show permission or support state
// instead of fabricating selection targets."). This file only ever reads:
// it never posts synthetic events or mutates another app's UI.

import Foundation
import ApplicationServices
import CoreGraphics

enum SXMAccessibility {

    // AX attribute names as literal strings rather than the kAX*Attribute
    // constants: SXMPermissions.swift already avoids those constants for the
    // same reason ("Literal key avoids the Unmanaged/CFString import
    // difference between SDKs"). The literal values are stable public API.
    private static let attrRole = "AXRole"
    private static let attrSubrole = "AXSubrole"
    private static let attrTitle = "AXTitle"
    private static let attrValue = "AXValue"
    private static let attrIdentifier = "AXIdentifier"
    private static let attrPosition = "AXPosition"
    private static let attrSize = "AXSize"
    private static let attrWindows = "AXWindows"
    private static let attrChildren = "AXChildren"

    private static let maxValueLength = 2000
    private static let hardMaxNodes = 2000
    private static let defaultDepth = 4
    private static let hardMaxDepth = 8

    static func inspectAt(_ ctx: SXMContext) throws {
        try ensureAccessibility()

        let x = try ctx.number("x")
        let y = try ctx.number("y")
        let space = try ctx.string("space")
        let point = try globalPoint(x: x, y: y, space: space)

        let systemWide = AXUIElementCreateSystemWide()
        var element: AXUIElement?
        let result = AXUIElementCopyElementAtPosition(systemWide, Float(point.x), Float(point.y), &element)

        guard result == .success, let found = element else {
            ctx.succeed([
                "treeAvailable": false,
                "reason": "No accessible element was found at the requested point (AXError \(result.rawValue)).",
                "space": SXMSpace.cgGlobalPoints.rawValue,
                "point": ["x": x, "y": y]
            ])
            return
        }

        var node = readAttributes(of: found)
        let opaque = isOpaque(node)
        node["treeAvailable"] = !opaque
        if opaque {
            // Honesty requirement: an empty-looking success here would read as
            // a real inspection. Many apps (custom-rendered controls, some
            // games/canvases) expose nothing useful through AX at all.
            node["reason"] = "The element exposed no role, title, value, identifier or frame; " +
                "the app likely renders its own controls without a usable Accessibility tree."
        }
        node["space"] = SXMSpace.cgGlobalPoints.rawValue
        node["point"] = ["x": x, "y": y]
        ctx.succeed(node)
    }

    static func inspectWindow(_ ctx: SXMContext) throws {
        try ensureAccessibility()

        let windowIdArg = (ctx.args["windowId"] as? NSNumber)?.uint32Value
        let pidArg = (ctx.args["pid"] as? NSNumber)?.int32Value

        var targetPid: pid_t?
        var matchInfo: SXMWindowInfo?

        if let windowIdArg {
            guard let info = SXMWindowInfo.all(onScreenOnly: false)
                .first(where: { $0.windowNumber == CGWindowID(windowIdArg) }) else {
                throw SXMFailure(.targetDisappeared, "No window with id \(windowIdArg) was found.",
                                 detail: ["windowId": Int(windowIdArg)])
            }
            matchInfo = info
            targetPid = info.ownerPid
        } else if let pidArg {
            targetPid = pidArg
        } else {
            throw SXMFailure(.invalidInput, "accessibility.inspectWindow requires 'windowId' or 'pid'.")
        }

        guard let pid = targetPid else {
            throw SXMFailure(.invalidInput, "Could not resolve a target process.")
        }

        let appElement = AXUIElementCreateApplication(pid)
        guard let windows = copyAttribute(appElement, attrWindows) as? [AXUIElement], !windows.isEmpty else {
            ctx.succeed([
                "treeAvailable": false,
                "reason": "The target application exposed no AXWindows: it may not be running, may have " +
                    "no open windows, or does not participate in Accessibility.",
                "pid": Int(pid)
            ])
            return
        }

        var window = windows.first
        if let matchInfo {
            // AX does not expose a CGWindowID directly; match by frame (with a
            // small tolerance for rounding) or, failing that, by title. This is
            // a heuristic, not an identity guarantee, which is why it falls
            // back to "first window" rather than failing outright.
            window = windows.first { candidate in
                if let frame = frameAttribute(candidate), approximatelyEqual(frame, matchInfo.boundsGlobalPoints) {
                    return true
                }
                if !matchInfo.title.isEmpty, stringAttribute(candidate, attrTitle) == matchInfo.title {
                    return true
                }
                return false
            } ?? window
        }

        guard let resolvedWindow = window else {
            throw SXMFailure(.targetDisappeared, "Could not resolve an AXWindow for pid \(pid).",
                             detail: ["pid": Int(pid)])
        }

        let depth = min(max(ctx.integer("depth", default: defaultDepth), 1), hardMaxDepth)
        var nodeCount = 0
        let tree = buildTree(resolvedWindow, depth: 0, maxDepth: depth, nodeCount: &nodeCount)

        let children = tree["children"] as? [[String: Any]]
        let treeAvailable = (children?.isEmpty == false)

        var payload: [String: Any] = [
            "root": tree,
            "treeAvailable": treeAvailable,
            "nodeCount": nodeCount,
            "depth": depth,
            "pid": Int(pid),
            "space": SXMSpace.cgGlobalPoints.rawValue
        ]
        if !treeAvailable {
            payload["reason"] = "The window exposed no child elements through Accessibility; " +
                "the app likely renders custom controls with no usable AX tree."
        }
        ctx.succeed(payload)
    }

    // MARK: - Permission

    private static func ensureAccessibility() throws {
        if SXMPermissions.accessibility() == .granted { return }
        // AXIsProcessTrusted() alone never prompts (mirrors the documented
        // screen-recording bug in SXMPermissions.swift); ask once so the user
        // actually sees the system dialog before we report it as missing.
        if SXMPermissions.requestAccessibility(prompt: true) == .granted { return }
        throw SXMFailure(.permissionRequired,
                         "Accessibility permission is required to inspect UI elements.",
                         detail: ["permission": "accessibility",
                                  "settingsUrl": "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility"])
    }

    // MARK: - Geometry

    private static func globalPoint(x: Double, y: Double, space: String) throws -> CGPoint {
        switch space {
        case SXMSpace.cgGlobalPoints.rawValue:
            return CGPoint(x: x, y: y)
        case SXMSpace.appKitPoints.rawValue:
            let flip = SXMGeometry.appKitFlipHeight
            return CGPoint(x: x, y: flip - y)
        default:
            throw SXMFailure(.invalidInput, "Unsupported space '\(space)'.")
        }
    }

    private static func approximatelyEqual(_ a: CGRect, _ b: CGRect, tolerance: CGFloat = 2) -> Bool {
        abs(a.origin.x - b.origin.x) <= tolerance &&
        abs(a.origin.y - b.origin.y) <= tolerance &&
        abs(a.width - b.width) <= tolerance &&
        abs(a.height - b.height) <= tolerance
    }

    // MARK: - Tree walking

    private static func buildTree(_ element: AXUIElement, depth: Int, maxDepth: Int, nodeCount: inout Int) -> [String: Any] {
        nodeCount += 1
        var node = readAttributes(of: element)

        guard depth < maxDepth, nodeCount < hardMaxNodes else {
            node["childrenTruncated"] = true
            return node
        }
        guard let children = copyAttribute(element, attrChildren) as? [AXUIElement], !children.isEmpty else {
            return node
        }

        var childNodes: [[String: Any]] = []
        for child in children {
            if nodeCount >= hardMaxNodes {
                node["childrenTruncated"] = true
                break
            }
            childNodes.append(buildTree(child, depth: depth + 1, maxDepth: maxDepth, nodeCount: &nodeCount))
        }
        node["children"] = childNodes
        return node
    }

    private static func isOpaque(_ node: [String: Any]) -> Bool {
        ["role", "subrole", "title", "value", "identifier", "frame"].allSatisfy { node[$0] == nil }
    }

    // MARK: - Attribute reading

    private static func readAttributes(of element: AXUIElement) -> [String: Any] {
        var node: [String: Any] = [:]
        if let role = stringAttribute(element, attrRole) { node["role"] = role }
        if let subrole = stringAttribute(element, attrSubrole) { node["subrole"] = subrole }
        if let title = stringAttribute(element, attrTitle) { node["title"] = title }
        if let identifier = stringAttribute(element, attrIdentifier) { node["identifier"] = identifier }
        if let value = valueAttribute(element) { node["value"] = truncate(value, limit: maxValueLength) }
        if let frame = frameAttribute(element) {
            node["frame"] = frame.asJson
            // AX position/size use the same top-left-origin, points, +y-down
            // convention as CoreGraphics global display space.
            node["frameSpace"] = SXMSpace.cgGlobalPoints.rawValue
        }
        return node
    }

    private static func copyAttribute(_ element: AXUIElement, _ attribute: String) -> AnyObject? {
        var value: AnyObject?
        let status = AXUIElementCopyAttributeValue(element, attribute as CFString, &value)
        return status == .success ? value : nil
    }

    private static func stringAttribute(_ element: AXUIElement, _ attribute: String) -> String? {
        copyAttribute(element, attribute) as? String
    }

    private static func valueAttribute(_ element: AXUIElement) -> String? {
        guard let raw = copyAttribute(element, attrValue) else { return nil }
        if let text = raw as? String { return text }
        if let number = raw as? NSNumber { return number.stringValue }
        // Sliders, checkboxes and similar controls expose non-string AXValue
        // types; describing rather than silently dropping keeps this honest
        // about what was actually returned.
        return "\(raw)"
    }

    private static func frameAttribute(_ element: AXUIElement) -> CGRect? {
        guard let positionRaw = copyAttribute(element, attrPosition),
              let sizeRaw = copyAttribute(element, attrSize),
              CFGetTypeID(positionRaw) == AXValueGetTypeID(),
              CFGetTypeID(sizeRaw) == AXValueGetTypeID() else {
            return nil
        }
        // Swift treats `as? AXValue` as an always-succeeding bridge (it's a
        // CoreFoundation toll-free type), which the compiler flags as an
        // error under this build's warnings-as-errors policy. The CFTypeID
        // check above is the real type guard; unsafeDowncast just names the
        // cast without re-triggering that diagnostic.
        let positionValue = unsafeDowncast(positionRaw, to: AXValue.self)
        let sizeValue = unsafeDowncast(sizeRaw, to: AXValue.self)
        guard AXValueGetType(positionValue) == .cgPoint,
              AXValueGetType(sizeValue) == .cgSize else {
            return nil
        }
        var point = CGPoint.zero
        var size = CGSize.zero
        guard AXValueGetValue(positionValue, .cgPoint, &point),
              AXValueGetValue(sizeValue, .cgSize, &size) else {
            return nil
        }
        return CGRect(origin: point, size: size)
    }

    private static func truncate(_ text: String, limit: Int) -> String {
        guard text.count > limit else { return text }
        return String(text.prefix(limit)) + "…[truncated]"
    }
}
