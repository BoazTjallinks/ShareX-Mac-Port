// OCR via Apple Vision.
//
// Replaces upstream's Windows.Media.Ocr. PROJECT-SPEC.md section 4 lists
// "run OCR" as a native boundary operation; section 13 forbids claiming a
// parity that was never measured. Vision is a different engine on a
// different platform, so every result is stamped with "engine" and the OS
// version rather than being presented as byte-identical to Windows OCR.

import Foundation
import CoreGraphics
import ImageIO
import Vision

enum SXMVision {

    // Bounded so a caller cannot smuggle megabytes of transcript into a diagnostic
    // dump; the full text is still returned in "text", this only caps a single
    // observation's own string.
    private static let maxObservationTextLength = 20_000

    static func recognize(_ ctx: SXMContext) throws {
        if ctx.isCancelled { ctx.cancelled(); return }

        let image = try loadImage(ctx)
        if ctx.isCancelled { ctx.cancelled(); return }

        let levelString = ctx.string("recognitionLevel", default: "accurate")
        let usesLanguageCorrection = ctx.bool("usesLanguageCorrection", default: true)
        let languages = ctx.stringArray("languages")

        let request = VNRecognizeTextRequest()
        request.recognitionLevel = level(levelString)
        request.usesLanguageCorrection = usesLanguageCorrection
        if languages.isEmpty {
            // Let Vision pick rather than guessing a language ourselves.
            request.automaticallyDetectsLanguage = true
        } else {
            request.automaticallyDetectsLanguage = false
            request.recognitionLanguages = languages
        }

        if let roi = try ctx.optionalRect("regionOfInterest") {
            // Vision's regionOfInterest lives in the image's own normalised
            // 0...1 space (origin bottom-left), not one of the screen-geometry
            // SXMSpace cases in SXMGeometry.swift: it addresses pixels inside
            // the still image being OCR'd, not a display.
            guard roi.minX >= -0.0001, roi.minY >= -0.0001,
                  roi.maxX <= 1.0001, roi.maxY <= 1.0001 else {
                throw SXMFailure(.invalidInput,
                                 "regionOfInterest must lie within Vision's normalised 0...1 image space.",
                                 detail: ["space": "vision-normalized"])
            }
            request.regionOfInterest = roi
        }

        ctx.onCancel { request.cancel() }
        if ctx.isCancelled { ctx.cancelled(); return }

        let handler = VNImageRequestHandler(cgImage: image, options: [:])
        do {
            try handler.perform([request])
        } catch {
            if ctx.isCancelled { ctx.cancelled(); return }
            throw SXMFailure(.internalFailure, "Vision text recognition failed: \(error.localizedDescription)")
        }

        if ctx.isCancelled { ctx.cancelled(); return }

        // VNRecognizeTextRequest.results is already typed as
        // [VNRecognizedTextObservation]? in this SDK, so no further cast.
        let observations = request.results ?? []
        var lines: [String] = []
        var payloadObservations: [[String: Any]] = []
        for observation in observations {
            guard let candidate = observation.topCandidates(1).first else { continue }
            lines.append(candidate.string)
            var text = candidate.string
            if text.count > maxObservationTextLength {
                text = String(text.prefix(maxObservationTextLength)) + "…[truncated]"
            }
            payloadObservations.append([
                "text": text,
                "confidence": Double(candidate.confidence),
                "boundingBox": observation.boundingBox.asJson,
                "space": "vision-normalized"
            ])
        }

        let osVersion = ProcessInfo.processInfo.operatingSystemVersion
        ctx.succeed([
            "text": lines.joined(separator: "\n"),
            "observations": payloadObservations,
            "engine": "apple-vision",
            "osVersion": "\(osVersion.majorVersion).\(osVersion.minorVersion).\(osVersion.patchVersion)",
            "recognitionLevel": levelString,
            "usesLanguageCorrection": usesLanguageCorrection,
            "languagesRequested": languages,
            "automaticLanguageDetection": languages.isEmpty
        ])
    }

    static func languages(_ ctx: SXMContext) throws {
        if ctx.isCancelled { ctx.cancelled(); return }

        let levelString = ctx.string("recognitionLevel", default: "accurate")
        let request = VNRecognizeTextRequest()
        request.recognitionLevel = level(levelString)

        do {
            // Always discover from the live framework; a hard-coded language
            // list would silently drift from what this OS/SDK actually supports.
            let supported = try request.supportedRecognitionLanguages()
            ctx.succeed([
                "languages": supported,
                "recognitionLevel": levelString,
                "engine": "apple-vision"
            ])
        } catch {
            throw SXMFailure(.internalFailure,
                             "Could not discover supported OCR languages: \(error.localizedDescription)")
        }
    }

    // MARK: - Helpers

    private static func level(_ raw: String) -> VNRequestTextRecognitionLevel {
        raw == "fast" ? .fast : .accurate
    }

    private static func loadImage(_ ctx: SXMContext) throws -> CGImage {
        let data: Data
        if let path = ctx.args["path"] as? String {
            guard FileManager.default.fileExists(atPath: path) else {
                throw SXMFailure(.localIOFailure, "No file at '\(path)'.")
            }
            guard let contents = FileManager.default.contents(atPath: path) else {
                throw SXMFailure(.localIOFailure, "Could not read '\(path)'.")
            }
            data = contents
        } else if let base64 = ctx.args["pngBase64"] as? String {
            guard let decoded = Data(base64Encoded: base64) else {
                throw SXMFailure(.invalidInput, "pngBase64 must be base64-encoded image bytes.")
            }
            data = decoded
        } else {
            throw SXMFailure(.invalidInput, "ocr.recognize requires either 'path' or 'pngBase64'.")
        }

        guard let source = CGImageSourceCreateWithData(data as CFData, nil),
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
            throw SXMFailure(.invalidInput, "Could not decode an image from the supplied data.")
        }
        return image
    }
}
