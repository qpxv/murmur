@preconcurrency import AVFoundation
import AudioToolbox
import CoreAudio
import Foundation
import MurmurCore
import os

/// What one block of microphone audio sounded like.
public struct AudioBlockMeasure: Sendable {
    /// 0...1 loudness for the voice bar.
    public let level: Float
    /// The input hit full scale, so it's distorting (too loud / gain too high).
    public let isClipped: Bool
}

/// Records one input device and delivers 16 kHz mono 16-bit PCM plus a measure of every block.
/// Callbacks arrive on a background queue.
///
/// Uses an input-only HAL unit instead of AVAudioEngine: the engine first opens the system
/// default input and also binds the default output. With AirPods connected (macOS makes them
/// the default input) that flips them into headset mode, which stops music and degrades the
/// sound, even when another mic is picked.
@MainActor
public final class AudioCapture {
    public typealias ChunkHandler = @Sendable (_ pcm: Data, _ measure: AudioBlockMeasure) -> Void

    // the hal unit's element 1 is the input side, element 0 the output side
    private static let inputElement: AudioUnitElement = 1
    private static let outputElement: AudioUnitElement = 0

    private var unit: AudioUnit?
    private var context: CaptureContext?
    private let logger = Logger(subsystem: Log.subsystem, category: "audio")

    public init() {}

    /// `device` is a specific microphone the user picked; `nil` records the system default input.
    public func start(device: AudioDeviceID?, onChunk: @escaping ChunkHandler) throws(DictationError) {
        guard let device = device ?? CoreAudioDevice.defaultInput() else {
            logger.error("no input device")
            throw .microphoneUnavailable
        }
        let unit = try makeInputUnit(device: device)
        do throws(DictationError) {
            let context = try makeContext(unit: unit, onChunk: onChunk)
            try check(Self.installInputCallback(unit: unit, context: context), "set the input callback")
            try check(AudioUnitInitialize(unit), "initialize the input")
            try check(AudioOutputUnitStart(unit), "start the input")
            self.unit = unit
            self.context = context
        } catch {
            AudioComponentInstanceDispose(unit)
            throw error
        }
    }

    public func stop() {
        guard let unit else { return }
        // synchronous: no callback runs after this returns, so the context can go
        AudioOutputUnitStop(unit)
        AudioUnitUninitialize(unit)
        AudioComponentInstanceDispose(unit)
        self.unit = nil
        context = nil
    }

    /// A hal unit that only records, already pointed at `device`, so nothing else gets opened.
    private func makeInputUnit(device: AudioDeviceID) throws(DictationError) -> AudioUnit {
        var description = AudioComponentDescription(
            componentType: kAudioUnitType_Output,
            componentSubType: kAudioUnitSubType_HALOutput,
            componentManufacturer: kAudioUnitManufacturer_Apple,
            componentFlags: 0,
            componentFlagsMask: 0
        )
        var instance: AudioUnit?
        guard let component = AudioComponentFindNext(nil, &description),
            AudioComponentInstanceNew(component, &instance) == noErr,
            let instance
        else {
            logger.error("no hal unit available")
            throw .microphoneUnavailable
        }
        do throws(DictationError) {
            try route(instance, to: device)
        } catch {
            AudioComponentInstanceDispose(instance)
            throw error
        }
        return instance
    }

    private func route(_ unit: AudioUnit, to device: AudioDeviceID) throws(DictationError) {
        let isOn: UInt32 = 1
        let isOff: UInt32 = 0
        try check(
            Self.set(unit, kAudioOutputUnitProperty_EnableIO, kAudioUnitScope_Input, Self.inputElement, isOn),
            "enable input"
        )
        try check(
            Self.set(unit, kAudioOutputUnitProperty_EnableIO, kAudioUnitScope_Output, Self.outputElement, isOff),
            "disable output"
        )
        try check(
            Self.set(unit, kAudioOutputUnitProperty_CurrentDevice, kAudioUnitScope_Global, 0, device),
            "select the mic"
        )
    }

    /// Asks for float samples at the device's own rate and channels: the hal unit can't
    /// resample input, so the conversion to what deepgram wants happens in `CaptureContext`.
    private func setClientFormat(of unit: AudioUnit) throws(DictationError) -> AVAudioFormat {
        guard
            let hardware = Self.get(
                unit,
                kAudioUnitProperty_StreamFormat,
                kAudioUnitScope_Input,
                Self.inputElement,
                AudioStreamBasicDescription()
            ),
            hardware.mSampleRate > 0,
            let format = AVAudioFormat(
                standardFormatWithSampleRate: hardware.mSampleRate,
                channels: hardware.mChannelsPerFrame
            )
        else {
            logger.error("no usable input format")
            throw .microphoneUnavailable
        }
        try check(
            Self.set(
                unit,
                kAudioUnitProperty_StreamFormat,
                kAudioUnitScope_Output,
                Self.inputElement,
                format.streamDescription.pointee
            ),
            "set the sample format"
        )
        return format
    }

    private func makeContext(unit: AudioUnit, onChunk: @escaping ChunkHandler) throws(DictationError)
        -> CaptureContext
    {
        let clientFormat = try setClientFormat(of: unit)
        guard
            let maxFrames = Self.get(
                unit,
                kAudioUnitProperty_MaximumFramesPerSlice,
                kAudioUnitScope_Global,
                0,
                UInt32(0)
            ),
            let targetFormat = AVAudioFormat(
                commonFormat: .pcmFormatInt16,
                sampleRate: AppConstants.Audio.sampleRate,
                channels: AppConstants.Audio.channelCount,
                interleaved: true
            ),
            let converter = AVAudioConverter(from: clientFormat, to: targetFormat),
            let context = CaptureContext(
                unit: unit,
                format: clientFormat,
                maxFrames: maxFrames,
                converter: converter,
                targetFormat: targetFormat,
                onChunk: onChunk
            )
        else {
            logger.error("couldn't set up the audio conversion")
            throw .microphoneUnavailable
        }
        return context
    }

    private func check(_ status: OSStatus, _ step: String) throws(DictationError) {
        guard status != noErr else { return }
        logger.error("couldn't \(step, privacy: .public): \(status)")
        throw .microphoneUnavailable
    }

    // outside the main actor: the callback is a c function run on the realtime audio thread
    private nonisolated static func installInputCallback(unit: AudioUnit, context: CaptureContext) -> OSStatus {
        let callback = AURenderCallbackStruct(
            inputProc: { refCon, flags, timeStamp, bus, frames, _ in
                Unmanaged<CaptureContext>.fromOpaque(refCon).takeUnretainedValue()
                    .render(flags: flags, timeStamp: timeStamp, bus: bus, frames: frames)
            },
            inputProcRefCon: Unmanaged.passUnretained(context).toOpaque()
        )
        return set(unit, kAudioOutputUnitProperty_SetInputCallback, kAudioUnitScope_Global, 0, callback)
    }

    private nonisolated static func set<Value: BitwiseCopyable>(
        _ unit: AudioUnit,
        _ property: AudioUnitPropertyID,
        _ scope: AudioUnitScope,
        _ element: AudioUnitElement,
        _ value: Value
    ) -> OSStatus {
        var value = value
        return AudioUnitSetProperty(unit, property, scope, element, &value, UInt32(MemoryLayout<Value>.size))
    }

    private nonisolated static func get<Value: BitwiseCopyable>(
        _ unit: AudioUnit,
        _ property: AudioUnitPropertyID,
        _ scope: AudioUnitScope,
        _ element: AudioUnitElement,
        _ initial: Value
    ) -> Value? {
        var value = initial
        var size = UInt32(MemoryLayout<Value>.size)
        guard AudioUnitGetProperty(unit, property, scope, element, &value, &size) == noErr else { return nil }
        return value
    }
}

/// Pulls audio out of the hal unit on the realtime thread, gathers it into blocks of
/// `AppConstants.Audio.blockFrames` and hands each block to a serial queue for measuring and
/// converting. `block` is only touched from the realtime thread and the converter only from the
/// queue, one at a time, which is why sharing it across threads is safe.
private final class CaptureContext: @unchecked Sendable {
    private let unit: AudioUnit
    private let renderBuffer: AVAudioPCMBuffer
    private var block: AVAudioPCMBuffer
    private let converter: AVAudioConverter
    private let targetFormat: AVAudioFormat
    private let onChunk: AudioCapture.ChunkHandler
    private let queue = DispatchQueue(label: "\(Log.subsystem).audio", qos: .userInteractive)

    init?(
        unit: AudioUnit,
        format: AVAudioFormat,
        maxFrames: UInt32,
        converter: AVAudioConverter,
        targetFormat: AVAudioFormat,
        onChunk: @escaping AudioCapture.ChunkHandler
    ) {
        guard let renderBuffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: maxFrames),
            let block = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: AppConstants.Audio.blockFrames)
        else { return nil }
        self.unit = unit
        self.renderBuffer = renderBuffer
        self.block = block
        self.converter = converter
        self.targetFormat = targetFormat
        self.onChunk = onChunk
    }

    func render(
        flags: UnsafeMutablePointer<AudioUnitRenderActionFlags>,
        timeStamp: UnsafePointer<AudioTimeStamp>,
        bus: UInt32,
        frames: UInt32
    ) -> OSStatus {
        guard frames <= renderBuffer.frameCapacity else { return kAudioUnitErr_TooManyFramesToProcess }
        // also resets each buffer's byte size, which the previous render may have shrunk
        renderBuffer.frameLength = frames
        let status = AudioUnitRender(unit, flags, timeStamp, bus, frames, renderBuffer.mutableAudioBufferList)
        guard status == noErr else { return status }
        append(renderBuffer)
        return noErr
    }

    private func append(_ source: AVAudioPCMBuffer) {
        guard let sourceChannels = source.floatChannelData else { return }
        var offset: AVAudioFrameCount = 0
        while offset < source.frameLength {
            guard let blockChannels = block.floatChannelData else { return }
            let count = min(source.frameLength - offset, block.frameCapacity - block.frameLength)
            for channel in 0..<Int(source.format.channelCount) {
                (blockChannels[channel] + Int(block.frameLength))
                    .update(from: sourceChannels[channel] + Int(offset), count: Int(count))
            }
            block.frameLength += count
            offset += count
            if block.frameLength == block.frameCapacity {
                emitBlock()
            }
        }
    }

    private func emitBlock() {
        guard let fresh = AVAudioPCMBuffer(pcmFormat: block.format, frameCapacity: block.frameCapacity) else {
            block.frameLength = 0
            return
        }
        let full = SendableBuffer(buffer: block)
        block = fresh
        queue.async { [self] in
            let measure = Self.measure(full.buffer)
            guard let pcm = convert(full.buffer) else { return }
            onChunk(pcm, measure)
        }
    }

    private static func measure(_ buffer: AVAudioPCMBuffer) -> AudioBlockMeasure {
        guard let channel = buffer.floatChannelData?[0] else {
            return AudioBlockMeasure(level: 0, isClipped: false)
        }
        let samples = UnsafeBufferPointer(start: channel, count: Int(buffer.frameLength))
        return AudioBlockMeasure(
            level: AudioLevel.normalized(rms: AudioLevel.rms(of: samples)),
            isClipped: AudioLevel.isClipped(samples)
        )
    }

    private func convert(_ buffer: AVAudioPCMBuffer) -> Data? {
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

/// A finished block: nothing writes to it once it's handed to the queue.
private struct SendableBuffer: @unchecked Sendable {
    let buffer: AVAudioPCMBuffer
}
