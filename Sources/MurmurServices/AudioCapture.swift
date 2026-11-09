@preconcurrency import AVFoundation
import Foundation
import MurmurCore
import os

/// Records the default input device and delivers 16 kHz mono 16-bit PCM plus a 0...1
/// loudness level for every block. Callbacks arrive on the audio thread.
@MainActor
public final class AudioCapture {
    public typealias ChunkHandler = @Sendable (_ pcm: Data, _ level: Float) -> Void

    private var engine: AVAudioEngine?
    private let logger = Logger(subsystem: Log.subsystem, category: "audio")

    public init() {}

    public func start(onChunk: @escaping ChunkHandler) throws(DictationError) {
        let engine = AVAudioEngine()
        let input = engine.inputNode
        let hardwareFormat = input.outputFormat(forBus: 0)
        guard hardwareFormat.sampleRate > 0,
            let targetFormat = AVAudioFormat(
                commonFormat: .pcmFormatInt16,
                sampleRate: AppConstants.Audio.sampleRate,
                channels: AppConstants.Audio.channelCount,
                interleaved: true
            ),
            let converter = AVAudioConverter(from: hardwareFormat, to: targetFormat)
        else {
            logger.error("no usable input format")
            throw .microphoneUnavailable
        }

        let tap = Self.makeTap(
            converter: ConverterBox(converter: converter, targetFormat: targetFormat),
            onChunk: onChunk
        )
        input.installTap(onBus: 0, bufferSize: AppConstants.Audio.tapBufferFrames, format: hardwareFormat, block: tap)
        do {
            try engine.start()
        } catch {
            input.removeTap(onBus: 0)
            logger.error("audio engine failed to start: \(error.localizedDescription)")
            throw .microphoneUnavailable
        }
        self.engine = engine
    }

    public func stop() {
        guard let engine else { return }
        engine.inputNode.removeTap(onBus: 0)
        engine.stop()
        self.engine = nil
    }

    // built outside the main actor: the tap runs on the realtime audio thread
    private nonisolated static func makeTap(converter: ConverterBox, onChunk: @escaping ChunkHandler)
        -> AVAudioNodeTapBlock
    {
        { @Sendable buffer, _ in
            let level = level(of: buffer)
            guard let pcm = converter.convert(buffer) else { return }
            onChunk(pcm, level)
        }
    }

    private nonisolated static func level(of buffer: AVAudioPCMBuffer) -> Float {
        guard let channel = buffer.floatChannelData?[0] else { return 0 }
        let samples = UnsafeBufferPointer(start: channel, count: Int(buffer.frameLength))
        return AudioLevel.normalized(rms: AudioLevel.rms(of: samples))
    }
}

/// Owns the sample-rate converter. Only ever used from the audio thread, one callback at a
/// time, which is why handing it across isolation is safe.
private final class ConverterBox: @unchecked Sendable {
    private let converter: AVAudioConverter
    private let targetFormat: AVAudioFormat

    init(converter: AVAudioConverter, targetFormat: AVAudioFormat) {
        self.converter = converter
        self.targetFormat = targetFormat
    }

    func convert(_ buffer: AVAudioPCMBuffer) -> Data? {
        let ratio = targetFormat.sampleRate / buffer.format.sampleRate
        let capacity = AVAudioFrameCount((Double(buffer.frameLength) * ratio).rounded(.up)) + 1
        guard let output = AVAudioPCMBuffer(pcmFormat: targetFormat, frameCapacity: capacity) else { return nil }

        var hasProvidedInput = false
        var error: NSError?
        let status = converter.convert(to: output, error: &error) { _, inputStatus in
            if hasProvidedInput {
                inputStatus.pointee = .noDataNow
                return nil
            }
            hasProvidedInput = true
            inputStatus.pointee = .haveData
            return buffer
        }
        guard status != .error, let samples = output.int16ChannelData?[0], output.frameLength > 0 else {
            return nil
        }
        return Data(bytes: samples, count: Int(output.frameLength) * MemoryLayout<Int16>.size)
    }
}
