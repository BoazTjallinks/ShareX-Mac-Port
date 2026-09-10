// Native screen-recording pipeline.
//
// PROJECT-SPEC.md section 6: ScreenCaptureKit supplies screen, system audio and
// microphone samples; AVFoundation does the muxing/encoding. This file owns the
// state machine, the shared timeline (pause exclusion via CMSampleBuffer
// retiming) and the three encode paths (H.264/HEVC/ProRes via AVAssetWriter,
// or a lossless numbered-PNG sequence for GIF/pixel-sensitive output).
//
// Design requirement 1 ("ONE owner for the stream and encoder"): every mutable
// piece of session state (the state machine, pausedDuration, per-track last
// timestamps, counters, the writer/inputs, the PNG frame index) is touched only
// from `sampleQueue`, a single serial DispatchQueue that doubles as the
// SCStream sampleHandlerQueue for all three output types. That makes this
// class's own serialization the single source of truth: sample delivery,
// pause/resume bookkeeping and stop/abort finalization can never interleave
// unsafely, without a second lock to reason about.

import Foundation
import AppKit
import CoreGraphics
import CoreMedia
import CoreVideo
import CoreImage
import AVFoundation
import ScreenCaptureKit

enum SXMRecordingState: String {
    case starting
    case recording
    case paused
    case stopping
    case finalized
    case aborted
    case failed
}

enum SXMRecordingIntermediate: String {
    case h264
    case hevc
    case prores4444
    case pngSequence

    /// nil for pngSequence: that path never touches AVAssetWriter for video
    /// (design requirement 4 — GIF/pixel-sensitive output must never be fed
    /// from a lossy intermediate).
    var avVideoCodec: AVVideoCodecType? {
        switch self {
        case .h264: return .h264
        case .hevc: return .hevc
        case .prores4444: return .proRes4444
        case .pngSequence: return nil
        }
    }
}

final class SXMRecordingSession: NSObject {

    let sessionId: UInt64
    private let intermediate: SXMRecordingIntermediate
    private let pixelWidth: Int
    private let pixelHeight: Int
    private let outputURL: URL
    private let maxDurationSeconds: Double?
    private let maxBytes: UInt64
    private let bitrate: Int?

    /// Serializes everything: sample delivery, state transitions, timeline math.
    /// Also used as the SCStream sampleHandlerQueue for all output types so
    /// video/system-audio/microphone callbacks are strictly ordered relative to
    /// pause/resume/stop (PROJECT-SPEC.md section 6: "keep video and all audio
    /// on a common timeline").
    private let sampleQueue = DispatchQueue(label: "com.tjallinks.sharexmac.recording.session")

    private var state: SXMRecordingState = .starting
    private var stream: SCStream?
    private var writer: AVAssetWriter?
    private var videoInput: AVAssetWriterInput?
    private var systemAudioInput: AVAssetWriterInput?
    private var microphoneInput: AVAssetWriterInput?
    private var pngDirectory: URL?
    private var pngFrameIndex: Int = 0
    private lazy var ciContext = CIContext(options: nil)

    private var requestedSystemAudio = false
    private var requestedMicrophone = false
    private var discoveredMicrophoneFormat: (sampleRate: Double, channels: Int)?

    // Shared timeline (design requirement 2).
    private var pausedDuration: CMTime = .zero
    private var pauseClockStart: CMTime?
    private var sessionStarted = false
    private var firstAdjustedPTS: CMTime?
    private var latestAdjustedPTS: CMTime?
    private var lastAppendedPTS: [SCStreamOutputType: CMTime] = [:]

    private var framesWritten = 0
    private var droppedFrames = 0
    private var droppedLateBuffers = 0
    private var totalBytesWritten: UInt64 = 0
    private var lastProgressEmit = Date.distantPast

    init(sessionId: UInt64,
        intermediate: SXMRecordingIntermediate,
        pixelWidth: Int,
        pixelHeight: Int,
        frameRateForEstimate: Double?,
        maxDurationSeconds: Double?,
        maxBytes: UInt64,
        requestedOutputPath: String?,
        bitrate: Int?) throws {
        self.sessionId = sessionId
        self.intermediate = intermediate
        self.pixelWidth = pixelWidth
        self.pixelHeight = pixelHeight
        self.maxDurationSeconds = maxDurationSeconds
        self.maxBytes = maxBytes
        self.bitrate = bitrate

        if let requestedOutputPath {
            self.outputURL = URL(fileURLWithPath: requestedOutputPath)
        } else if intermediate == .pngSequence {
            self.outputURL = FileManager.default.temporaryDirectory
                .appendingPathComponent("sharexmac-recording-\(sessionId)", isDirectory: true)
        } else {
            self.outputURL = FileManager.default.temporaryDirectory
                .appendingPathComponent("sharexmac-recording-\(sessionId).mov")
        }
        if intermediate == .pngSequence {
            self.pngDirectory = self.outputURL
        }

        // Design requirement 5: a lossless PNG sequence has no compression to
        // absorb an unbounded recording, so storage must be bounded up front,
        // not discovered after the disk fills up.
        if intermediate == .pngSequence {
            guard let maxDurationSeconds, maxDurationSeconds > 0 else {
                throw SXMFailure(.invalidConfiguration,
                                 "intermediate 'pngSequence' requires a positive maxDurationSeconds so storage can be bounded before recording starts.")
            }
            // Assume up to 60fps when frameRate is unset (SCStreamConfiguration's
            // own default cadence) rather than a lower guess, so the bound errs
            // toward rejecting a request instead of under-estimating disk use.
            let assumedFrameRate = (frameRateForEstimate.map { $0 > 0 ? $0 : 60.0 }) ?? 60.0
            let estimatedBytes = Double(pixelWidth) * Double(pixelHeight) * 4.0 * assumedFrameRate * maxDurationSeconds
            guard estimatedBytes.isFinite, estimatedBytes <= Double(maxBytes) else {
                throw SXMFailure(.invalidConfiguration,
                                 "Estimated pngSequence storage exceeds maxBytes.",
                                 detail: ["estimatedBytes": estimatedBytes, "maxBytes": Int(maxBytes)])
            }
        }
        super.init()
    }

    // MARK: - Start

    func beginCapture(resolution: SXMRecordingTargetResolution,
                      showsCursor: Bool,
                      captureSystemAudio: Bool,
                      captureMicrophone: Bool,
                      frameRate: Double?,
                      isCancelled: @escaping () -> Bool,
                      onRunning: @escaping (Result<[String: Any], SXMFailure>) -> Void) throws {
        requestedSystemAudio = captureSystemAudio
        requestedMicrophone = captureMicrophone

        let config = SCStreamConfiguration()
        config.width = pixelWidth
        config.height = pixelHeight
        config.pixelFormat = kCVPixelFormatType_32BGRA
        config.showsCursor = showsCursor
        config.colorSpaceName = CGColorSpace.sRGB
        config.queueDepth = 8
        if let sourceRect = resolution.sourceRectDisplayPoints {
            config.sourceRect = sourceRect
            config.scalesToFit = false
        }
        if let frameRate, frameRate > 0 {
            config.minimumFrameInterval = CMTime(seconds: 1.0 / frameRate, preferredTimescale: 600)
        }
        config.capturesAudio = captureSystemAudio
        if captureSystemAudio {
            config.sampleRate = 48000
            config.channelCount = 2
            // Keep ShareX's own UI sounds out of its own system-audio track.
            config.excludesCurrentProcessAudio = true
        }
        if captureMicrophone {
            if #available(macOS 15.0, *) {
                config.captureMicrophone = true
            } else {
                // The caller already rejected this before resolving a target;
                // beginCapture should never be reached in that case, but an
                // unavailable capability must never be silently dropped here.
                throw SXMFailure(.unsupportedCapability,
                                 "Microphone capture requires macOS 15 or later.",
                                 detail: ["feature": "microphoneCapture"])
            }
        }

        try configureOutputs(captureSystemAudio: captureSystemAudio, captureMicrophone: captureMicrophone)

        let stream = SCStream(filter: resolution.contentFilter, configuration: config, delegate: self)
        self.stream = stream

        do {
            try stream.addStreamOutput(self, type: .screen, sampleHandlerQueue: sampleQueue)
            if captureSystemAudio {
                try stream.addStreamOutput(self, type: .audio, sampleHandlerQueue: sampleQueue)
            }
            if captureMicrophone, #available(macOS 15.0, *) {
                try stream.addStreamOutput(self, type: .microphone, sampleHandlerQueue: sampleQueue)
            }
        } catch {
            throw SXMFailure(.internalFailure, "Could not attach a stream output: \(error.localizedDescription)")
        }

        if let writer, !writer.startWriting() {
            throw SXMFailure(.encoderFailure,
                             "AVAssetWriter could not start writing: \(writer.error?.localizedDescription ?? "unknown error")")
        }

        stream.startCapture { [weak self] error in
            guard let self else { return }
            self.sampleQueue.async {
                if let error {
                    self.state = .failed
                    self.cleanupAfterFailedStart()
                    onRunning(.failure(SXMFailure(.internalFailure,
                                                  "Could not start the capture stream: \(error.localizedDescription)")))
                    return
                }
                guard self.state == .starting else { return }
                if isCancelled() {
                    self.state = .aborted
                    self.cleanupAfterFailedStart()
                    onRunning(.failure(SXMFailure(.userCancelled, "Operation cancelled.")))
                    return
                }
                self.state = .recording
                self.emitState()
                onRunning(.success(self.startedPayloadLocked()))
            }
        }
    }

    private func configureOutputs(captureSystemAudio: Bool, captureMicrophone: Bool) throws {
        switch intermediate {
        case .pngSequence:
            try preparePngSequence()
            if captureSystemAudio || captureMicrophone {
                try prepareAudioOnlyWriter(captureSystemAudio: captureSystemAudio, captureMicrophone: captureMicrophone)
            }
        case .h264, .hevc, .prores4444:
            try prepareEncodedWriter(captureSystemAudio: captureSystemAudio, captureMicrophone: captureMicrophone)
        }
    }

    private func preparePngSequence() throws {
        let dir = outputURL
        do {
            try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        } catch {
            throw SXMFailure(.localIOFailure, "Could not create the frame directory at '\(dir.path)': \(error.localizedDescription)")
        }
        pngDirectory = dir
    }

    private func prepareEncodedWriter(captureSystemAudio: Bool, captureMicrophone: Bool) throws {
        guard let codec = intermediate.avVideoCodec else {
            throw SXMFailure(.internalFailure, "No AVVideoCodecType mapped for intermediate '\(intermediate.rawValue)'.")
        }
        let newWriter: AVAssetWriter
        do {
            newWriter = try AVAssetWriter(url: outputURL, fileType: .mov)
        } catch {
            throw SXMFailure(.localIOFailure,
                             "Could not create the recording file at '\(outputURL.path)': \(error.localizedDescription)")
        }

        var videoSettings: [String: Any] = [
            AVVideoCodecKey: codec,
            AVVideoWidthKey: pixelWidth,
            AVVideoHeightKey: pixelHeight
        ]
        // ProRes has no user bitrate knob (fixed quality per profile); leaving
        // it unset is the correct response, not a silently-ignored request
        // (design requirement 4: an unavailable option stays unavailable).
        if let bitrate, codec != .proRes4444 {
            videoSettings[AVVideoCompressionPropertiesKey] = [AVVideoAverageBitRateKey: bitrate]
        }
        guard newWriter.canApply(outputSettings: videoSettings, forMediaType: .video) else {
            throw SXMFailure(.unsupportedCapability,
                             "This machine's AVAssetWriter cannot produce '\(intermediate.rawValue)' video.",
                             detail: ["intermediate": intermediate.rawValue])
        }
        let vInput = AVAssetWriterInput(mediaType: .video, outputSettings: videoSettings)
        vInput.expectsMediaDataInRealTime = true
        guard newWriter.canAdd(vInput) else {
            throw SXMFailure(.encoderFailure, "The video track could not be added to the recording file.")
        }
        newWriter.add(vInput)
        videoInput = vInput

        if captureSystemAudio {
            systemAudioInput = try makePassthroughAudioInput(writer: newWriter, label: "system audio")
        }
        if captureMicrophone {
            microphoneInput = try makePassthroughAudioInput(writer: newWriter, label: "microphone")
        }
        writer = newWriter
    }

    private func prepareAudioOnlyWriter(captureSystemAudio: Bool, captureMicrophone: Bool) throws {
        guard let dir = pngDirectory else {
            throw SXMFailure(.internalFailure, "pngSequence audio setup requires the frame directory to exist first.")
        }
        let audioURL = dir.appendingPathComponent("audio.mov")
        let newWriter: AVAssetWriter
        do {
            newWriter = try AVAssetWriter(url: audioURL, fileType: .mov)
        } catch {
            throw SXMFailure(.localIOFailure, "Could not create the audio file at '\(audioURL.path)': \(error.localizedDescription)")
        }
        if captureSystemAudio {
            systemAudioInput = try makePassthroughAudioInput(writer: newWriter, label: "system audio")
        }
        if captureMicrophone {
            microphoneInput = try makePassthroughAudioInput(writer: newWriter, label: "microphone")
        }
        writer = newWriter
    }

    /// Passthrough (nil outputSettings) stores exactly the PCM format
    /// ScreenCaptureKit delivers for this track, instead of us pre-declaring an
    /// encoder configuration. System audio's format is deterministic (we set
    /// sampleRate/channelCount on SCStreamConfiguration above); microphone's is
    /// the selected device's *native* format, which is only knowable once its
    /// first buffer arrives. Passthrough sidesteps needing that format upfront
    /// while still losslessly recording each track's real format — a more
    /// direct way to "handle differing sample rates/channel layouts" than
    /// hand-building matching AAC settings (PROJECT-SPEC.md section 6). Mic and
    /// system audio still land on two separate inputs, never mixed.
    private func makePassthroughAudioInput(writer: AVAssetWriter, label: String) throws -> AVAssetWriterInput {
        let input = AVAssetWriterInput(mediaType: .audio, outputSettings: nil)
        input.expectsMediaDataInRealTime = true
        guard writer.canAdd(input) else {
            throw SXMFailure(.encoderFailure, "The \(label) track could not be added to the recording file.")
        }
        writer.add(input)
        return input
    }

    // MARK: - Control (pause/resume/stop/abort/status)
    //
    // Invalid transitions throw SXM_INVALID_CONFIGURATION rather than silently
    // succeeding (design requirement 1). All of them run on sampleQueue so they
    // can never race a concurrent sample buffer callback.

    func pause() throws -> [String: Any] {
        var result: Result<[String: Any], SXMFailure>?
        sampleQueue.sync {
            guard state == .recording else {
                result = .failure(invalidTransition("recording.pause", required: "recording"))
                return
            }
            pauseClockStart = CMClockGetTime(effectiveClock())
            state = .paused
            emitState()
            result = .success(statusPayloadLocked())
        }
        return try unwrap(result)
    }

    func resume() throws -> [String: Any] {
        var result: Result<[String: Any], SXMFailure>?
        sampleQueue.sync {
            guard state == .paused else {
                result = .failure(invalidTransition("recording.resume", required: "paused"))
                return
            }
            if let start = pauseClockStart {
                let now = CMClockGetTime(effectiveClock())
                let delta = CMTimeSubtract(now, start)
                if delta.isValid, delta.isNumeric, delta.seconds > 0 {
                    pausedDuration = CMTimeAdd(pausedDuration, delta)
                }
            }
            pauseClockStart = nil
            state = .recording
            emitState()
            result = .success(statusPayloadLocked())
        }
        return try unwrap(result)
    }

    func status() -> [String: Any] {
        sampleQueue.sync { statusPayloadLocked() }
    }

    func abort() throws -> [String: Any] {
        var result: Result<[String: Any], SXMFailure>?
        sampleQueue.sync {
            guard state == .starting || state == .recording || state == .paused else {
                result = .failure(invalidTransition("recording.abort", required: "starting, recording or paused"))
                return
            }
            state = .aborted
            stream?.stopCapture(completionHandler: { _ in })
            videoInput?.markAsFinished()
            systemAudioInput?.markAsFinished()
            microphoneInput?.markAsFinished()
            // Explicitly this job's own output only (design requirement 1).
            disposeOwnedOutput()
            emitState()
            result = .success(statusPayloadLocked())
        }
        SXMRecordingRegistry.shared.remove(sessionId)
        return try unwrap(result)
    }

    func stop(completion: @escaping (Result<[String: Any], SXMFailure>) -> Void) {
        sampleQueue.async { [weak self] in
            guard let self else { return }
            guard self.state == .recording || self.state == .paused else {
                completion(.failure(self.invalidTransition("recording.stop", required: "recording or paused")))
                return
            }
            self.state = .stopping
            self.emitState()
            self.finalize(completion: completion)
        }
    }

    /// Runs on sampleQueue. Reached from exactly one guarded transition per
    /// session (explicit stop(), auto-stop, or an unexpected stream failure),
    /// so flush-and-finalize happens exactly once (design requirement 1).
    private func finalize(completion: @escaping (Result<[String: Any], SXMFailure>) -> Void) {
        stream?.stopCapture { [weak self] _ in
            guard let self else { return }
            self.sampleQueue.async {
                self.videoInput?.markAsFinished()
                self.systemAudioInput?.markAsFinished()
                self.microphoneInput?.markAsFinished()

                guard let writer = self.writer else {
                    // pngSequence with no audio requested: nothing to finalize
                    // but the frame directory itself.
                    self.state = .finalized
                    self.emitState()
                    completion(.success(self.finalizedPayloadLocked(outputPath: self.outputURL.path)))
                    SXMRecordingRegistry.shared.remove(self.sessionId)
                    return
                }
                writer.finishWriting {
                    self.sampleQueue.async {
                        switch writer.status {
                        case .completed:
                            self.state = .finalized
                            self.emitState()
                            completion(.success(self.finalizedPayloadLocked(outputPath: self.outputURL.path)))
                        default:
                            // Design requirement 6: preserve recoverable output;
                            // report its path and the error, never delete it.
                            self.state = .failed
                            self.emitState()
                            let message = writer.error?.localizedDescription ?? "Finalization did not complete."
                            completion(.failure(SXMFailure(.encoderFailure,
                                                           "Recording finalization failed: \(message)",
                                                           detail: ["sessionId": Int(self.sessionId),
                                                                    "recoverableOutputPath": writer.outputURL.path])))
                        }
                        SXMRecordingRegistry.shared.remove(self.sessionId)
                    }
                }
            }
        }
    }

    private func triggerAutoStop(reason: String) {
        state = .stopping
        emitState()
        finalize { [weak self] result in
            guard let self else { return }
            switch result {
            case .success(let payload):
                SXMDispatcher.shared.emit(subscription: self.sessionId, event: [
                    "kind": "recording.state", "sessionId": Int(self.sessionId),
                    "state": "finalized", "reason": reason, "outputPath": payload["outputPath"] as Any
                ])
            case .failure(let failure):
                SXMDispatcher.shared.emit(subscription: self.sessionId, event: [
                    "kind": "recording.state", "sessionId": Int(self.sessionId),
                    "state": "failed", "reason": reason, "error": failure.message
                ])
            }
        }
    }

    private func cleanupAfterFailedStart() {
        disposeOwnedOutput()
        stream?.stopCapture(completionHandler: { _ in })
        SXMRecordingRegistry.shared.remove(sessionId)
    }

    private func disposeOwnedOutput() {
        writer?.cancelWriting()
        if intermediate == .pngSequence, let dir = pngDirectory {
            try? FileManager.default.removeItem(at: dir)
        }
    }

    private func invalidTransition(_ op: String, required: String) -> SXMFailure {
        SXMFailure(.invalidConfiguration,
                  "\(op) requires state '\(required)'; current state is '\(state.rawValue)'.",
                  detail: ["sessionId": Int(sessionId), "state": state.rawValue])
    }

    private func unwrap(_ result: Result<[String: Any], SXMFailure>?) throws -> [String: Any] {
        switch result {
        case .success(let payload): return payload
        case .failure(let failure): throw failure
        case nil: throw SXMFailure(.internalFailure, "No result was produced.")
        }
    }

    private func effectiveClock() -> CMClock {
        stream?.synchronizationClock ?? CMClockGetHostTimeClock()
    }

    // MARK: - Sample buffer handling (runs on sampleQueue)

    fileprivate func handleSampleBuffer(_ sampleBuffer: CMSampleBuffer, type: SCStreamOutputType) {
        guard sampleBuffer.isValid else { droppedLateBuffers += 1; return }
        guard state == .recording else {
            // Paused/starting/stopping/finalized/aborted/failed: intentionally
            // excluded, not a late/out-of-order buffer, so it is not counted as
            // dropped (design requirement 2: pause excludes the interval).
            return
        }

        var timing = CMSampleTimingInfo(duration: .invalid, presentationTimeStamp: .invalid, decodeTimeStamp: .invalid)
        guard CMSampleBufferGetSampleTimingInfo(sampleBuffer, at: 0, timingInfoOut: &timing) == noErr else {
            droppedLateBuffers += 1
            return
        }
        let adjustedPTS = CMTimeSubtract(timing.presentationTimeStamp, pausedDuration)
        guard adjustedPTS.isValid, adjustedPTS.isNumeric else {
            droppedLateBuffers += 1
            return
        }
        // Monotonicity per track (design requirement 2): never append backward.
        if let last = lastAppendedPTS[type], CMTimeCompare(adjustedPTS, last) <= 0 {
            droppedLateBuffers += 1
            return
        }
        lastAppendedPTS[type] = adjustedPTS

        if firstAdjustedPTS == nil {
            firstAdjustedPTS = adjustedPTS
        }
        if latestAdjustedPTS == nil || CMTimeCompare(adjustedPTS, latestAdjustedPTS!) > 0 {
            latestAdjustedPTS = adjustedPTS
        }
        if !sessionStarted {
            writer?.startSession(atSourceTime: adjustedPTS)
            sessionStarted = true
        }

        switch type {
        case .screen:
            handleVideoBuffer(sampleBuffer, originalTiming: timing, adjustedPTS: adjustedPTS)
        case .audio:
            handleAudioBuffer(sampleBuffer, input: systemAudioInput, originalTiming: timing, adjustedPTS: adjustedPTS,
                              label: "system audio")
        case .microphone:
            if discoveredMicrophoneFormat == nil,
               let formatDescription = CMSampleBufferGetFormatDescription(sampleBuffer),
               let asbd = CMAudioFormatDescriptionGetStreamBasicDescription(formatDescription) {
                discoveredMicrophoneFormat = (asbd.pointee.mSampleRate, Int(asbd.pointee.mChannelsPerFrame))
            }
            handleAudioBuffer(sampleBuffer, input: microphoneInput, originalTiming: timing, adjustedPTS: adjustedPTS,
                              label: "microphone")
        @unknown default:
            break
        }

        maybeEmitProgress()
        maybeAutoStop()
    }

    private func handleVideoBuffer(_ sampleBuffer: CMSampleBuffer, originalTiming: CMSampleTimingInfo, adjustedPTS: CMTime) {
        if intermediate == .pngSequence {
            writePngFrame(sampleBuffer)
            return
        }
        guard let input = videoInput else { droppedFrames += 1; return }
        // Backpressure (design requirement 5): drop-and-count, never queue.
        guard input.isReadyForMoreMediaData else { droppedFrames += 1; return }
        guard let retimed = retimedBuffer(sampleBuffer, originalTiming: originalTiming, adjustedPTS: adjustedPTS) else {
            droppedLateBuffers += 1
            return
        }
        if input.append(retimed) {
            framesWritten += 1
        } else {
            droppedFrames += 1
        }
    }

    private func handleAudioBuffer(_ sampleBuffer: CMSampleBuffer,
                                   input: AVAssetWriterInput?,
                                   originalTiming: CMSampleTimingInfo,
                                   adjustedPTS: CMTime,
                                   label: String) {
        guard let input else { return }   // track not requested; nothing to do
        guard input.isReadyForMoreMediaData else { droppedFrames += 1; return }
        guard let retimed = retimedBuffer(sampleBuffer, originalTiming: originalTiming, adjustedPTS: adjustedPTS) else {
            droppedLateBuffers += 1
            return
        }
        if !input.append(retimed) {
            droppedFrames += 1
        }
    }

    /// Rewrites presentationTimeStamp/decodeTimeStamp so the pause interval is
    /// excluded from the written timeline (design requirement 2).
    private func retimedBuffer(_ sampleBuffer: CMSampleBuffer,
                               originalTiming: CMSampleTimingInfo,
                               adjustedPTS: CMTime) -> CMSampleBuffer? {
        var newTiming = originalTiming
        newTiming.presentationTimeStamp = adjustedPTS
        if originalTiming.decodeTimeStamp.isValid {
            newTiming.decodeTimeStamp = CMTimeSubtract(originalTiming.decodeTimeStamp, pausedDuration)
        }
        var copy: CMSampleBuffer?
        let status = CMSampleBufferCreateCopyWithNewTiming(allocator: kCFAllocatorDefault,
                                                           sampleBuffer: sampleBuffer,
                                                           sampleTimingEntryCount: 1,
                                                           sampleTimingArray: &newTiming,
                                                           sampleBufferOut: &copy)
        guard status == noErr else { return nil }
        return copy
    }

    private func writePngFrame(_ sampleBuffer: CMSampleBuffer) {
        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else {
            droppedFrames += 1
            return
        }
        let ciImage = CIImage(cvPixelBuffer: pixelBuffer)
        guard let cgImage = ciContext.createCGImage(ciImage, from: ciImage.extent) else {
            droppedFrames += 1
            return
        }
        guard let dir = pngDirectory else {
            droppedFrames += 1
            return
        }
        let index = pngFrameIndex
        pngFrameIndex += 1
        let url = dir.appendingPathComponent(String(format: "frame_%08d.png", index))
        do {
            let data = try SXMCapture.encodePng(cgImage)
            try data.write(to: url, options: .atomic)
            totalBytesWritten += UInt64(data.count)
            framesWritten += 1
        } catch {
            droppedFrames += 1
        }
    }

    private func maybeAutoStop() {
        guard state == .recording || state == .paused else { return }
        if let maxDurationSeconds, let first = firstAdjustedPTS, let latest = latestAdjustedPTS {
            let elapsed = CMTimeGetSeconds(CMTimeSubtract(latest, first))
            if elapsed.isFinite, elapsed >= maxDurationSeconds {
                triggerAutoStop(reason: "maxDurationSeconds")
                return
            }
        }
        if currentByteEstimateLocked() > maxBytes {
            triggerAutoStop(reason: "maxBytes")
        }
    }

    // MARK: - Reporting

    private func maybeEmitProgress() {
        let now = Date()
        // Coalesce to at most ~2 events/sec (ABI surface requirement).
        guard now.timeIntervalSince(lastProgressEmit) >= 0.5 else { return }
        lastProgressEmit = now
        SXMDispatcher.shared.emit(subscription: sessionId, event: [
            "kind": "recording.progress",
            "sessionId": Int(sessionId),
            "state": state.rawValue,
            "elapsedMs": elapsedMsLocked(),
            "frames": framesWritten,
            "droppedFrames": droppedFrames + droppedLateBuffers,
            "bytes": Int(currentByteEstimateLocked())
        ])
    }

    private func emitState() {
        SXMDispatcher.shared.emit(subscription: sessionId, event: [
            "kind": "recording.state",
            "sessionId": Int(sessionId),
            "state": state.rawValue,
            "elapsedMs": elapsedMsLocked(),
            "frames": framesWritten,
            "droppedFrames": droppedFrames + droppedLateBuffers,
            "bytes": Int(currentByteEstimateLocked())
        ])
    }

    private func elapsedMsLocked() -> Int {
        guard let first = firstAdjustedPTS, let latest = latestAdjustedPTS else { return 0 }
        let seconds = CMTimeGetSeconds(CMTimeSubtract(latest, first))
        guard seconds.isFinite, seconds >= 0 else { return 0 }
        return Int(seconds * 1000.0)
    }

    private func currentByteEstimateLocked() -> UInt64 {
        if intermediate == .pngSequence {
            return totalBytesWritten
        }
        guard let writer else { return totalBytesWritten }
        if let attrs = try? FileManager.default.attributesOfItem(atPath: writer.outputURL.path),
           let size = attrs[.size] as? NSNumber {
            return size.uint64Value
        }
        return totalBytesWritten
    }

    private func audioTracksLocked() -> [[String: Any]] {
        var tracks: [[String: Any]] = []
        if requestedSystemAudio {
            tracks.append(["kind": "systemAudio", "sampleRate": 48000, "channelCount": 2, "formatKnown": true])
        }
        if requestedMicrophone {
            if let mic = discoveredMicrophoneFormat {
                tracks.append(["kind": "microphone", "sampleRate": mic.sampleRate,
                               "channelCount": mic.channels, "formatKnown": true])
            } else {
                tracks.append(["kind": "microphone", "formatKnown": false,
                               "note": "Native device format is discovered from the first captured buffer."])
            }
        }
        return tracks
    }

    private func startedPayloadLocked() -> [String: Any] {
        [
            "sessionId": Int(sessionId),
            "outputPath": outputURL.path,
            "pixelWidth": pixelWidth,
            "pixelHeight": pixelHeight,
            "intermediate": intermediate.rawValue,
            "audioTracks": audioTracksLocked(),
            "state": state.rawValue
        ]
    }

    private func statusPayloadLocked() -> [String: Any] {
        [
            "sessionId": Int(sessionId),
            "state": state.rawValue,
            "elapsedMs": elapsedMsLocked(),
            "frames": framesWritten,
            "droppedFrames": droppedFrames,
            "droppedLateBuffers": droppedLateBuffers,
            "bytes": Int(currentByteEstimateLocked()),
            "pixelWidth": pixelWidth,
            "pixelHeight": pixelHeight,
            "intermediate": intermediate.rawValue,
            "outputPath": outputURL.path,
            "audioTracks": audioTracksLocked()
        ]
    }

    private func finalizedPayloadLocked(outputPath: String) -> [String: Any] {
        var fileSize = totalBytesWritten
        if let attrs = try? FileManager.default.attributesOfItem(atPath: outputPath),
           let size = attrs[.size] as? NSNumber {
            fileSize = size.uint64Value
        }
        return [
            "sessionId": Int(sessionId),
            "state": state.rawValue,
            "outputPath": outputPath,
            "durationMs": elapsedMsLocked(),
            "frames": framesWritten,
            "droppedFrames": droppedFrames,
            "droppedLateBuffers": droppedLateBuffers,
            "fileSizeBytes": Int(fileSize),
            "audioTracks": audioTracksLocked()
        ]
    }
}

// MARK: - SCStreamOutput / SCStreamDelegate
//
// Selectors are pinned explicitly rather than relying on the Swift importer's
// guess at "ofType:" -> "of:" name simplification, so protocol conformance is
// correct by construction rather than by assumption.

extension SXMRecordingSession: SCStreamOutput {
    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        handleSampleBuffer(sampleBuffer, type: type)
    }
}

extension SXMRecordingSession: SCStreamDelegate {
    func stream(_ stream: SCStream, didStopWithError error: Error) {
        sampleQueue.async { [weak self] in
            guard let self else { return }
            guard self.state == .recording || self.state == .paused else { return }
            self.state = .stopping
            self.emitState()
            self.finalize { [weak self] result in
                guard let self else { return }
                switch result {
                case .success(let payload):
                    SXMDispatcher.shared.emit(subscription: self.sessionId, event: [
                        "kind": "recording.state", "sessionId": Int(self.sessionId),
                        "state": "finalized", "reason": "streamStopped",
                        "error": error.localizedDescription, "outputPath": payload["outputPath"] as Any
                    ])
                case .failure(let failure):
                    SXMDispatcher.shared.emit(subscription: self.sessionId, event: [
                        "kind": "recording.state", "sessionId": Int(self.sessionId),
                        "state": "failed", "reason": "streamStopped", "error": failure.message
                    ])
                }
            }
        }
    }
}

// MARK: - Factory

enum SXMRecordingSessionFactory {

    @discardableResult
    static func start(args: [String: Any],
                      isCancelled: @escaping () -> Bool,
                      completion: @escaping (Result<[String: Any], SXMFailure>) -> Void) -> Task<Void, Never> {
        Task {
            var createdSession: SXMRecordingSession?
            do {
                try SXMCapture.requirePermission()

                guard let targetDict = args["target"] as? [String: Any] else {
                    throw SXMFailure(.invalidInput, "target is required for recording.start.")
                }
                let intermediateRaw = (args["intermediate"] as? String) ?? "h264"
                guard let intermediate = SXMRecordingIntermediate(rawValue: intermediateRaw) else {
                    throw SXMFailure(.invalidInput, "Unknown intermediate '\(intermediateRaw)'.")
                }
                // ShareX-style recording default: cursor visible unless told otherwise.
                let showsCursor = (args["showsCursor"] as? NSNumber)?.boolValue ?? true
                let captureSystemAudio = (args["captureSystemAudio"] as? NSNumber)?.boolValue ?? false
                let captureMicrophone = (args["captureMicrophone"] as? NSNumber)?.boolValue ?? false
                let frameRate = (args["frameRate"] as? NSNumber)?.doubleValue
                let bitrate = (args["bitrate"] as? NSNumber)?.intValue
                let maxDurationSeconds = (args["maxDurationSeconds"] as? NSNumber)?.doubleValue
                // Default 20 GB (design requirement 5).
                let maxBytes = (args["maxBytes"] as? NSNumber)?.uint64Value ?? (20 * 1024 * 1024 * 1024)
                let excludeSelf = (args["excludeSelf"] as? NSNumber)?.boolValue ?? true
                let requestedOutputPath = args["outputPath"] as? String

                if captureMicrophone {
                    guard #available(macOS 15.0, *) else {
                        throw SXMFailure(.unsupportedCapability,
                                         "Microphone capture requires macOS 15 or later (SCStreamConfiguration.captureMicrophone).",
                                         detail: ["feature": "microphoneCapture"])
                    }
                }

                let content = try await SXMCapture.shareableContent()
                let resolution = try SXMRecordingTarget.resolve(targetDict, content: content, excludeSelf: excludeSelf)

                if isCancelled() {
                    completion(.failure(SXMFailure(.userCancelled, "Operation cancelled.")))
                    return
                }

                let sessionId = SXMRecordingRegistry.shared.allocateSessionId()
                let session = try SXMRecordingSession(sessionId: sessionId,
                                                      intermediate: intermediate,
                                                      pixelWidth: resolution.pixelWidth,
                                                      pixelHeight: resolution.pixelHeight,
                                                      frameRateForEstimate: frameRate,
                                                      maxDurationSeconds: maxDurationSeconds,
                                                      maxBytes: maxBytes,
                                                      requestedOutputPath: requestedOutputPath,
                                                      bitrate: bitrate)
                createdSession = session
                SXMRecordingRegistry.shared.insert(session)

                try session.beginCapture(resolution: resolution,
                                         showsCursor: showsCursor,
                                         captureSystemAudio: captureSystemAudio,
                                         captureMicrophone: captureMicrophone,
                                         frameRate: frameRate,
                                         isCancelled: isCancelled,
                                         onRunning: completion)
            } catch let failure as SXMFailure {
                createdSession?.cleanupAfterFailedStartPublic()
                completion(.failure(failure))
            } catch {
                createdSession?.cleanupAfterFailedStartPublic()
                completion(.failure(SXMFailure(.internalFailure, "Recording start failed: \(error.localizedDescription)")))
            }
        }
    }
}

extension SXMRecordingSession {
    /// Internal cleanup exposed to the factory's catch block only; not part of
    /// the public control surface (pause/resume/stop/abort/status).
    fileprivate func cleanupAfterFailedStartPublic() {
        sampleQueue.sync { cleanupAfterFailedStart() }
    }
}
