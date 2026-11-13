import CoreGraphics
import Foundation
import MurmurCore
import Synchronization
import os

/// Watches the dictation key system-wide through a Core Graphics event tap (needs Accessibility).
/// Space and other keys are only reported while the key is held, so normal typing is untouched.
/// The key itself never reaches other apps (except fn, see `Hotkey.isTakenOver`).
///
/// The tap runs on its own thread and only decides what to swallow; the handler runs afterwards on
/// the main actor. macOS lets an event through when a tap answers too slowly, so a busy main thread
/// (starting the mic, an accessibility call into a hanging app) used to type the dictation key.
@MainActor
public final class HotkeyMonitor {
    public enum Event: Sendable {
        case hotkeyDown(at: TimeInterval)
        case hotkeyUp(at: TimeInterval)
        case space
        case otherKey
    }

    public typealias Handler = @MainActor (Event) -> Void
    public typealias RecordingCompletion = @MainActor (HotkeyRecording) -> Void

    /// The key to watch. Changing it forgets a press in progress.
    public var hotkey: Hotkey = .function {
        didSet {
            let hotkey = hotkey
            eventTap.state.withLock { $0.change(hotkey: hotkey) }
        }
    }

    /// Whether a recording is running. A space pressed while the key is held then locks it and is
    /// swallowed, so it's never typed.
    public var isRecording = false {
        didSet {
            let isRecording = isRecording
            eventTap.state.withLock { $0.isRecording = isRecording }
        }
    }

    private let handler: Handler
    private let logger = Logger(subsystem: Log.subsystem, category: "hotkey")
    private let eventTap: EventTap
    private var isStarted = false
    private var recordingCompletion: RecordingCompletion?

    public init(handler: @escaping Handler) {
        self.handler = handler
        let (outputs, continuation) = AsyncStream.makeStream(of: TapOutput.self)
        eventTap = EventTap(outputs: continuation)
        Task { [weak self] in
            for await output in outputs {
                self?.receive(output)
            }
        }
    }

    /// Installs the tap. Returns `false` when Accessibility access is missing.
    public func start() -> Bool {
        guard !isStarted else { return true }
        guard eventTap.start() else {
            logger.error("event tap creation failed, accessibility not granted?")
            return false
        }
        isStarted = true
        return true
    }

    /// Catches the next single key (every key is swallowed meanwhile, dictation is paused) and
    /// reports it once it's decided. A recording already running is cancelled.
    public func record(completion: @escaping RecordingCompletion) {
        cancelRecording()
        recordingCompletion = completion
        eventTap.state.withLock { $0.startRecording() }
    }

    public func cancelRecording() {
        eventTap.state.withLock { $0.recorder = nil }
        guard let completion = recordingCompletion else { return }
        recordingCompletion = nil
        completion(.cancelled)
    }

    private func receive(_ output: TapOutput) {
        switch output {
        case .event(let event):
            handler(event)
        case .recording(let result):
            // nil when the recording was cancelled after the tap had already decided
            guard let completion = recordingCompletion else { return }
            recordingCompletion = nil
            completion(result)
        }
    }
}

/// What the tap does with an event.
private enum Verdict {
    case pass
    case swallow
    case passWithFlags(CGEventFlags)
}

/// What the tap hands to the main actor after deciding.
private enum TapOutput: Sendable {
    case event(HotkeyMonitor.Event)
    case recording(HotkeyRecording)
}

/// Everything the tap thread decides with. The main actor changes it only through the lock.
private struct TapState {
    var port: CFMachPort?
    var hotkey: Hotkey = .function
    var isRecording = false
    var recorder: HotkeyRecorder?
    private var isHotkeyDown = false
    private var isSwallowingSpace = false

    private static let nanosecondsPerSecond: Double = 1_000_000_000

    private enum KeyChange {
        case pressed
        case released
    }

    mutating func change(hotkey: Hotkey) {
        self.hotkey = hotkey
        isHotkeyDown = false
        isSwallowingSpace = false
    }

    mutating func startRecording() {
        recorder = HotkeyRecorder()
        isHotkeyDown = false
        isSwallowingSpace = false
    }

    mutating func decide(type: CGEventType, keyCode: UInt16, flags: CGEventFlags, timestamp: UInt64) -> (
        Verdict, TapOutput?
    ) {
        if recorder != nil {
            return decideRecording(type: type, keyCode: keyCode, flags: flags)
        }
        if keyCode == hotkey.keyCode, let change = hotkeyChange(type: type, flags: flags) {
            let verdict: Verdict = hotkey.isTakenOver ? .swallow : .pass
            let isDown = change == .pressed
            guard isDown != isHotkeyDown else { return (verdict, nil) }
            isHotkeyDown = isDown
            let time = Double(timestamp) / Self.nanosecondsPerSecond
            return (verdict, .event(isDown ? .hotkeyDown(at: time) : .hotkeyUp(at: time)))
        }
        return decideOtherKey(type: type, keyCode: keyCode, flags: flags)
    }

    // nil when the event isn't the hotkey's kind (a modifier only changes flags, other keys go down and up)
    private func hotkeyChange(type: CGEventType, flags: CGEventFlags) -> KeyChange? {
        if let modifier = hotkey.modifier {
            guard type == .flagsChanged else { return nil }
            return modifier.isDown(in: flags) ? .pressed : .released
        }
        switch type {
        case .keyDown: return .pressed
        case .keyUp: return .released
        default: return nil
        }
    }

    private mutating func decideOtherKey(type: CGEventType, keyCode: UInt16, flags: CGEventFlags) -> (
        Verdict, TapOutput?
    ) {
        if type == .keyUp && keyCode == Hotkey.KeyCode.space && isSwallowingSpace {
            isSwallowingSpace = false
            return (.swallow, nil)
        }
        guard isHotkeyDown else { return (.pass, nil) }
        let plainFlags = hotkey.removingModifier(from: flags)
        let verdict: Verdict = plainFlags == flags ? .pass : .passWithFlags(plainFlags)
        guard type == .keyDown else { return (verdict, nil) }
        guard keyCode == Hotkey.KeyCode.space else { return (verdict, .event(.otherKey)) }
        // every space typed while the dictation key is down during a recording belongs to the lock gesture
        isSwallowingSpace = isRecording
        return (isSwallowingSpace ? .swallow : verdict, .event(.space))
    }

    private mutating func decideRecording(type: CGEventType, keyCode: UInt16, flags: CGEventFlags) -> (
        Verdict, TapOutput?
    ) {
        guard var current = recorder else { return (.pass, nil) }
        let result: HotkeyRecording
        switch type {
        case .keyDown:
            result = current.keyDown(keyCode)
        case .flagsChanged:
            // caps lock and unknown modifiers have no flag to read: treat them as a press
            let isDown = Hotkey(keyCode: keyCode).modifier?.isDown(in: flags) ?? true
            result = isDown ? current.modifierDown(keyCode) : current.modifierUp(keyCode)
        default:
            return (.swallow, nil)
        }
        guard result != .waiting else {
            recorder = current
            return (.swallow, nil)
        }
        recorder = nil
        return (.swallow, .recording(result))
    }
}

/// The part of the monitor the tap thread uses.
private final class EventTap: Sendable {
    let state = Mutex(TapState())
    private let outputs: AsyncStream<TapOutput>.Continuation
    private let logger = Logger(subsystem: Log.subsystem, category: "hotkey")

    init(outputs: AsyncStream<TapOutput>.Continuation) {
        self.outputs = outputs
    }

    /// Creates the tap and runs it on its own thread. `false` when macOS refuses (no Accessibility).
    /// The caller keeps this object alive for as long as the tap runs.
    func start() -> Bool {
        let events: [CGEventType] = [.flagsChanged, .keyDown, .keyUp]
        let mask = events.reduce(CGEventMask(0)) { $0 | (CGEventMask(1) << $1.rawValue) }
        let isCreated = state.withLock { [self] state in
            state.port = CGEvent.tapCreate(
                tap: .cgSessionEventTap,
                place: .headInsertEventTap,
                options: .defaultTap,
                eventsOfInterest: mask,
                callback: hotkeyTapCallback,
                userInfo: Unmanaged.passUnretained(self).toOpaque()
            )
            return state.port != nil
        }
        guard isCreated else { return false }
        let thread = Thread { [self] in
            state.withLock { state in
                guard let port = state.port else { return }
                let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, port, 0)
                CFRunLoopAddSource(CFRunLoopGetCurrent(), source, .commonModes)
            }
            CFRunLoopRun()
        }
        thread.name = "\(Log.subsystem).hotkey"
        thread.qualityOfService = .userInteractive
        thread.start()
        return true
    }

    func handle(type: CGEventType, keyCode: Int64, flags: CGEventFlags, timestamp: UInt64) -> Verdict {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            if type == .tapDisabledByTimeout {
                logger.notice("event tap timed out, turning it back on")
            }
            // macos disables slow or interrupted taps; turn it straight back on
            state.withLock { tapState in
                if let port = tapState.port { CGEvent.tapEnable(tap: port, enable: true) }
            }
            return .pass
        }
        guard let code = UInt16(exactly: keyCode) else { return .pass }
        let (verdict, output) = state.withLock {
            $0.decide(type: type, keyCode: code, flags: flags, timestamp: timestamp)
        }
        if let output { outputs.yield(output) }
        return verdict
    }
}

// a c function pointer can't capture context, so the event tap travels through userInfo.
// runs on the tap's own thread.
private func hotkeyTapCallback(
    proxy _: CGEventTapProxy,
    type: CGEventType,
    event: CGEvent,
    userInfo: UnsafeMutableRawPointer?
) -> Unmanaged<CGEvent>? {
    guard let userInfo else { return Unmanaged.passUnretained(event) }
    let eventTap = Unmanaged<EventTap>.fromOpaque(userInfo).takeUnretainedValue()
    let verdict = eventTap.handle(
        type: type,
        keyCode: event.getIntegerValueField(.keyboardEventKeycode),
        flags: event.flags,
        timestamp: event.timestamp
    )
    switch verdict {
    case .pass:
        return Unmanaged.passUnretained(event)
    case .swallow:
        return nil
    case .passWithFlags(let plainFlags):
        event.flags = plainFlags
        return Unmanaged.passUnretained(event)
    }
}
