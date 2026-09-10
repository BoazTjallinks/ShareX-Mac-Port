// Screen colour sampling via ScreenCaptureKit.
//
// Replaces upstream's user32/gdi32 pixel reads. "Pixel color readout" and the
// region magnifier are named parts of the selection overlay in
// PROJECT-SPEC.md section 5. Both routes here go through
// SXMCapture.captureGlobalRect(), which itself calls
// SXMCapture.requirePermission() first, so the permission-prompt behaviour
// matches every other capture entry point instead of diverging for a "small"
// capture.

import Foundation
import AppKit
import CoreGraphics

enum SXMPixel {

    // A colour probe or magnifier never needs much source material; these
    // bounds sit far below SXMCapture.maxPixels so a bad request cannot drive
    // a full-desktop composition just to read a few pixels.
    private static let minSourceSize: Double = 4
    private static let maxSourceSize: Double = 64      // points, per side
    private static let minZoom: Double = 1
    private static let maxZoom: Double = 16
    private static let maxOutputSide = 512              // pixels, hard cap

    static func colorAt(_ ctx: SXMContext) throws {
        let x = try ctx.number("x")
        let y = try ctx.number("y")
        let space = try ctx.string("space")
        let point = try globalPoint(x: x, y: y, space: space)

        let work = Task {
            do {
                // A 2x2-point probe tolerates SXMGeometry.pixelAligned rounding;
                // the exact source pixel is picked out below from the returned
                // compositionScale/sourceRect rather than assumed.
                let probe = CGRect(x: point.x - 1, y: point.y - 1, width: 2, height: 2)
                let output = try await SXMCapture.captureGlobalRect(probe,
                                                                    compositionScale: nil,
                                                                    showsCursor: false,
                                                                    excludeSelf: true)
                if ctx.isCancelled { ctx.cancelled(); return }

                let (px, py) = pixelIndex(of: point, in: output)
                guard let cropped = output.image.cropping(to: CGRect(x: px, y: py, width: 1, height: 1)) else {
                    throw SXMFailure(.internalFailure, "Could not isolate the requested pixel.")
                }
                let (r, g, b, a) = try rgba(of: cropped)

                ctx.succeed([
                    "r": Int(r), "g": Int(g), "b": Int(b), "a": Int(a),
                    "hex": String(format: "#%02X%02X%02X", r, g, b),
                    "colorSpace": "sRGB",
                    "space": space,
                    "point": ["x": x, "y": y]
                ])
            } catch let failure as SXMFailure {
                ctx.fail(failure)
            } catch {
                ctx.fail(.internalFailure, "Pixel read failed: \(error.localizedDescription)")
            }
        }
        ctx.onCancel { work.cancel() }
    }

    static func magnifier(_ ctx: SXMContext) throws {
        let x = try ctx.number("x")
        let y = try ctx.number("y")
        let space = try ctx.string("space")
        let point = try globalPoint(x: x, y: y, space: space)

        let requestedSize = ctx.number("size", default: 20)
        let requestedZoom = ctx.number("zoom", default: 8)
        let size = min(max(requestedSize, minSourceSize), maxSourceSize)
        let zoom = min(max(requestedZoom, minZoom), maxZoom)

        let work = Task {
            do {
                let probe = CGRect(x: point.x - size / 2, y: point.y - size / 2, width: size, height: size)
                let output = try await SXMCapture.captureGlobalRect(probe,
                                                                    compositionScale: nil,
                                                                    showsCursor: false,
                                                                    excludeSelf: true)
                if ctx.isCancelled { ctx.cancelled(); return }

                let sourceWidth = output.image.width
                let sourceHeight = output.image.height

                // Re-clamp against the hard output-side cap regardless of what
                // size/zoom the caller asked for: a magnifier PNG has no
                // business being huge even if both inputs were individually
                // within their own bounds.
                var appliedZoom = zoom
                let longestSourceSide = Double(max(sourceWidth, sourceHeight))
                if longestSourceSide * appliedZoom > Double(maxOutputSide) {
                    appliedZoom = Double(maxOutputSide) / longestSourceSide
                }
                let outWidth = max(1, Int((Double(sourceWidth) * appliedZoom).rounded()))
                let outHeight = max(1, Int((Double(sourceHeight) * appliedZoom).rounded()))

                guard let colorSpace = CGColorSpace(name: CGColorSpace.sRGB) else {
                    throw SXMFailure(.internalFailure, "sRGB color space unavailable.")
                }
                guard let context = CGContext(data: nil,
                                              width: outWidth,
                                              height: outHeight,
                                              bitsPerComponent: 8,
                                              bytesPerRow: 0,
                                              space: colorSpace,
                                              bitmapInfo: CGImageAlphaInfo.premultipliedFirst.rawValue
                                                | CGBitmapInfo.byteOrder32Little.rawValue) else {
                    throw SXMFailure(.internalFailure, "Could not allocate the magnifier bitmap.")
                }
                // Nearest-neighbour: a magnifier should show crisp source
                // pixels, not a blurred resample.
                context.interpolationQuality = .none
                context.draw(output.image, in: CGRect(x: 0, y: 0, width: outWidth, height: outHeight))
                guard let zoomed = context.makeImage() else {
                    throw SXMFailure(.internalFailure, "Magnifier composition produced no image.")
                }
                let png = try SXMCapture.encodePng(zoomed)

                ctx.succeed([
                    "requestedSize": requestedSize,
                    "requestedZoom": requestedZoom,
                    "size": size,
                    "appliedZoom": appliedZoom,
                    "pixelWidth": outWidth,
                    "pixelHeight": outHeight,
                    "sourcePixelWidth": sourceWidth,
                    "sourcePixelHeight": sourceHeight,
                    "compositionScale": output.compositionScale,
                    "sourceRect": output.sourceRectGlobalPoints.asJson,
                    "sourceSpace": SXMSpace.cgGlobalPoints.rawValue,
                    "space": space,
                    "point": ["x": x, "y": y],
                    "colorSpace": "sRGB",
                    "format": "png"
                ], blob: png)
            } catch let failure as SXMFailure {
                ctx.fail(failure)
            } catch {
                ctx.fail(.internalFailure, "Magnifier capture failed: \(error.localizedDescription)")
            }
        }
        ctx.onCancel { work.cancel() }
    }

    // MARK: - Helpers

    /// Converts a point given in a named space into CoreGraphics global
    /// points, the only space SXMCapture accepts. PROJECT-SPEC.md section 5:
    /// never mix coordinate spaces implicitly.
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

    /// Maps a CoreGraphics-global-points location onto the pixel that actually
    /// contains it inside a capture output, using the output's own reported
    /// sourceRect/compositionScale rather than assuming a fixed relationship.
    private static func pixelIndex(of point: CGPoint, in output: SXMCaptureOutput) -> (x: Int, y: Int) {
        let rect = output.sourceRectGlobalPoints
        let scale = output.compositionScale
        let px = Int(((point.x - rect.minX) * scale).rounded(.down))
        let py = Int(((point.y - rect.minY) * scale).rounded(.down))
        return (max(0, min(output.image.width - 1, px)), max(0, min(output.image.height - 1, py)))
    }

    /// Reads back exact RGBA bytes for a 1x1 image by drawing it into a
    /// context we own, rather than parsing the source CGImage's raw buffer
    /// (whose byte layout depends on which SXMCapture path produced it).
    private static func rgba(of pixelImage: CGImage) throws -> (UInt8, UInt8, UInt8, UInt8) {
        guard let colorSpace = CGColorSpace(name: CGColorSpace.sRGB) else {
            throw SXMFailure(.internalFailure, "sRGB color space unavailable.")
        }
        guard let context = CGContext(data: nil,
                                      width: 1,
                                      height: 1,
                                      bitsPerComponent: 8,
                                      bytesPerRow: 0,
                                      space: colorSpace,
                                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else {
            throw SXMFailure(.internalFailure, "Could not allocate the pixel-read bitmap.")
        }
        context.draw(pixelImage, in: CGRect(x: 0, y: 0, width: 1, height: 1))
        guard let data = context.data else {
            throw SXMFailure(.internalFailure, "Could not read back the pixel buffer.")
        }
        let bytes = data.bindMemory(to: UInt8.self, capacity: context.bytesPerRow)
        return (bytes[0], bytes[1], bytes[2], bytes[3])
    }
}
