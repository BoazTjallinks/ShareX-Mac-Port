// Argument accessors. Unknown fields are tolerated; missing or wrongly typed
// required fields fail deterministically (docs/INTERFACES-AND-DATA.md).

import Foundation
import CoreGraphics

extension SXMContext {
    func string(_ key: String) throws -> String {
        guard let value = args[key] as? String else {
            throw SXMFailure(.invalidInput, "Missing or invalid string argument '\(key)'.")
        }
        return value
    }

    func string(_ key: String, default fallback: String) -> String {
        (args[key] as? String) ?? fallback
    }

    func number(_ key: String) throws -> Double {
        guard let value = args[key] as? NSNumber else {
            throw SXMFailure(.invalidInput, "Missing or invalid number argument '\(key)'.")
        }
        return value.doubleValue
    }

    func number(_ key: String, default fallback: Double) -> Double {
        (args[key] as? NSNumber)?.doubleValue ?? fallback
    }

    func integer(_ key: String) throws -> Int {
        guard let value = args[key] as? NSNumber else {
            throw SXMFailure(.invalidInput, "Missing or invalid integer argument '\(key)'.")
        }
        return value.intValue
    }

    func integer(_ key: String, default fallback: Int) -> Int {
        (args[key] as? NSNumber)?.intValue ?? fallback
    }

    func bool(_ key: String, default fallback: Bool) -> Bool {
        (args[key] as? NSNumber)?.boolValue ?? fallback
    }

    func dictionary(_ key: String) -> [String: Any]? {
        args[key] as? [String: Any]
    }

    func stringArray(_ key: String) -> [String] {
        (args[key] as? [Any])?.compactMap { $0 as? String } ?? []
    }

    /// Reads a rectangle expressed in a named coordinate space. The caller must
    /// always state the space; PROJECT-SPEC.md section 5 forbids implicitly
    /// mixing AppKit points, capture pixels and CoreGraphics global coordinates.
    func rect(_ key: String) throws -> CGRect {
        guard let raw = args[key] as? [String: Any],
              let x = (raw["x"] as? NSNumber)?.doubleValue,
              let y = (raw["y"] as? NSNumber)?.doubleValue,
              let w = (raw["width"] as? NSNumber)?.doubleValue,
              let h = (raw["height"] as? NSNumber)?.doubleValue else {
            throw SXMFailure(.invalidInput, "Missing or invalid rect argument '\(key)'.")
        }
        if w <= 0 || h <= 0 {
            throw SXMFailure(.invalidInput, "Rect '\(key)' has a non-positive extent.")
        }
        return CGRect(x: x, y: y, width: w, height: h)
    }

    func optionalRect(_ key: String) throws -> CGRect? {
        guard args[key] != nil else { return nil }
        return try rect(key)
    }
}

extension CGRect {
    var asJson: [String: Any] {
        ["x": origin.x, "y": origin.y, "width": size.width, "height": size.height]
    }
}
