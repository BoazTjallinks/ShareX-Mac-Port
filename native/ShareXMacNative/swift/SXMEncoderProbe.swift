// Encoder discovery.
//
// PROJECT-SPEC.md section 6: "Discover actual encoder support, rather than
// assuming a codec is compiled in." This probes what AVAssetWriter will really
// accept on this machine and what capture inputs the running OS exposes. An
// unavailable codec is reported as unavailable; it is never substituted.

import Foundation
import AVFoundation

enum SXMEncoderProbe {

    /// Codecs this port offers as recording intermediates, paired with the
    /// SXMRecordingIntermediate name the ABI accepts.
    private static let candidates: [(intermediate: String, codec: AVVideoCodecType)] = [
        ("h264", .h264),
        ("hevc", .hevc),
        ("prores4444", .proRes4444)
    ]

    static func snapshot() -> [String: Any] {
        var video: [[String: Any]] = []
        for candidate in candidates {
            video.append([
                "intermediate": candidate.intermediate,
                "codec": candidate.codec.rawValue,
                "available": accepts(candidate.codec)
            ])
        }

        // The lossless path needs no encoder at all, so it is always available;
        // it is the route GIF and other pixel-sensitive output must use rather
        // than being fed from a lossy intermediate.
        video.append([
            "intermediate": "pngSequence",
            "codec": "none",
            "available": true,
            "note": "Lossless numbered PNG frames; no AVAssetWriter involved."
        ])

        var microphone = false
        if #available(macOS 15.0, *) { microphone = true }

        return [
            "video": video,
            "audio": [
                "systemAudio": true,
                "microphone": microphone,
                "microphoneNote": microphone
                    ? "SCStreamConfiguration.captureMicrophone available."
                    : "Microphone capture requires macOS 15 or later."
            ],
            "container": AVFileType.mp4.rawValue,
            "probedOn": ProcessInfo.processInfo.operatingSystemVersionString
        ]
    }

    /// Asks AVFoundation directly instead of hard-coding a support table.
    /// A writer is created against a throwaway path and never started.
    private static func accepts(_ codec: AVVideoCodecType) -> Bool {
        let url = URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent("sxm-probe-\(UUID().uuidString).mp4")
        defer { try? FileManager.default.removeItem(at: url) }

        guard let writer = try? AVAssetWriter(outputURL: url, fileType: .mp4) else {
            return false
        }
        let settings: [String: Any] = [
            AVVideoCodecKey: codec,
            AVVideoWidthKey: 640,
            AVVideoHeightKey: 480
        ]
        guard writer.canApply(outputSettings: settings, forMediaType: .video) else {
            return false
        }
        let input = AVAssetWriterInput(mediaType: .video, outputSettings: settings)
        return writer.canAdd(input)
    }
}
