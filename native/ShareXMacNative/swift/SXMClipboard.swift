// Clipboard and Finder integration.
//
// Replaces upstream's ClipboardHelpers (Win32 clipboard) with NSPasteboard.
// PROJECT-SPEC.md section 8 and docs/INTERFACES-AND-DATA.md require preserving
// the image / text / file / HTML distinctions and the source's precedence rules,
// so each representation is a separate operation rather than one "copy" call.
//
// All AppKit work is dispatched to the main queue: NSPasteboard is not safe to
// touch from an arbitrary queue, and the native side never starts its own
// NSApplication (PROJECT-SPEC.md section 4).

import Foundation
import AppKit

enum SXMClipboard {

    /// Runs a block on the main queue and waits for it, without deadlocking when
    /// already on the main queue.
    private static func onMain<T>(_ work: @escaping () -> T) -> T {
        if Thread.isMainThread {
            return work()
        }
        var result: T!
        DispatchQueue.main.sync { result = work() }
        return result
    }

    /// Copies an image file's decoded contents as an image representation, which
    /// is what other applications paste. Upstream copies the image itself, not a
    /// file reference, for AfterCaptureTasks.CopyImageToClipboard.
    static func copyImage(fromFile path: String) throws {
        guard FileManager.default.fileExists(atPath: path) else {
            throw SXMFailure(.localIOFailure, "No file at '\(path)'.")
        }
        guard let image = NSImage(contentsOfFile: path) else {
            throw SXMFailure(.invalidInput, "Could not decode an image from '\(path)'.")
        }

        let ok: Bool = onMain {
            let pasteboard = NSPasteboard.general
            pasteboard.clearContents()
            return pasteboard.writeObjects([image])
        }

        if !ok {
            throw SXMFailure(.internalFailure, "The pasteboard rejected the image.")
        }
    }

    /// Copies raw PNG bytes without going through a file.
    static func copyImage(pngData: Data) throws {
        guard let image = NSImage(data: pngData) else {
            throw SXMFailure(.invalidInput, "Could not decode the supplied PNG bytes.")
        }

        let ok: Bool = onMain {
            let pasteboard = NSPasteboard.general
            pasteboard.clearContents()
            return pasteboard.writeObjects([image])
        }

        if !ok {
            throw SXMFailure(.internalFailure, "The pasteboard rejected the image.")
        }
    }

    static func copyText(_ text: String) throws {
        let ok: Bool = onMain {
            let pasteboard = NSPasteboard.general
            pasteboard.clearContents()
            return pasteboard.setString(text, forType: .string)
        }

        if !ok {
            throw SXMFailure(.internalFailure, "The pasteboard rejected the text.")
        }
    }

    /// Copies a file *reference*, the equivalent of copying in Finder. Distinct
    /// from copyImage and from copying the path as text: upstream treats
    /// CopyFileToClipboard, CopyFilePathToClipboard and CopyFolderPathToClipboard
    /// as three different, mutually exclusive operations.
    static func copyFile(_ path: String) throws {
        guard FileManager.default.fileExists(atPath: path) else {
            throw SXMFailure(.localIOFailure, "No file at '\(path)'.")
        }

        let url = URL(fileURLWithPath: path)
        let ok: Bool = onMain {
            let pasteboard = NSPasteboard.general
            pasteboard.clearContents()
            return pasteboard.writeObjects([url as NSURL])
        }

        if !ok {
            throw SXMFailure(.internalFailure, "The pasteboard rejected the file reference.")
        }
    }

    /// Reports what the clipboard currently holds, in the categories upstream's
    /// precedence rules care about.
    static func snapshot() -> [String: Any] {
        onMain {
            let pasteboard = NSPasteboard.general
            let types = pasteboard.types?.map { $0.rawValue } ?? []

            var result: [String: Any] = [
                "types": types,
                "hasImage": pasteboard.canReadObject(forClasses: [NSImage.self], options: nil),
                "hasFile": pasteboard.canReadObject(forClasses: [NSURL.self],
                                                    options: [.urlReadingFileURLsOnly: true]),
                "hasText": pasteboard.string(forType: .string) != nil,
                "changeCount": pasteboard.changeCount
            ]

            if let text = pasteboard.string(forType: .string) {
                // Length only: clipboard contents can be sensitive and must not
                // land in generic diagnostics.
                result["textLength"] = text.count
                if let url = URL(string: text), url.scheme == "http" || url.scheme == "https" {
                    result["looksLikeUrl"] = true
                }
            }

            return result
        }
    }

    static func clear() {
        // clearContents() returns the new change count, which we do not need.
        _ = onMain { NSPasteboard.general.clearContents() as Int }
    }
}

enum SXMFinder {

    private static func onMain<T>(_ work: @escaping () -> T) -> T {
        if Thread.isMainThread { return work() }
        var result: T!
        DispatchQueue.main.sync { result = work() }
        return result
    }

    private static func onMainVoid(_ work: @escaping () -> Void) {
        if Thread.isMainThread { work(); return }
        DispatchQueue.main.sync(execute: work)
    }

    /// Reveals a file in Finder with it selected — upstream's ShowInExplorer.
    static func reveal(_ path: String) throws {
        guard FileManager.default.fileExists(atPath: path) else {
            throw SXMFailure(.localIOFailure, "No file at '\(path)'.")
        }

        onMainVoid {
            NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: path)])
        }
    }

    /// Opens a file or folder with its default handler.
    static func open(_ path: String) throws {
        guard FileManager.default.fileExists(atPath: path) else {
            throw SXMFailure(.localIOFailure, "No file or folder at '\(path)'.")
        }

        let ok: Bool = onMain {
            NSWorkspace.shared.open(URL(fileURLWithPath: path))
        }

        if !ok {
            throw SXMFailure(.localIOFailure, "macOS declined to open '\(path)'.")
        }
    }

    /// Opens a URL. Restricted to http/https so a workflow result can never be
    /// used to launch an arbitrary scheme handler.
    static func openUrl(_ raw: String) throws {
        guard let url = URL(string: raw), let scheme = url.scheme?.lowercased() else {
            throw SXMFailure(.invalidInput, "'\(raw)' is not a valid URL.")
        }
        guard scheme == "http" || scheme == "https" else {
            throw SXMFailure(.invalidInput,
                             "Only http and https URLs may be opened, not '\(scheme)'.")
        }

        let ok: Bool = onMain { NSWorkspace.shared.open(url) }
        if !ok {
            throw SXMFailure(.localIOFailure, "macOS declined to open the URL.")
        }
    }
}
