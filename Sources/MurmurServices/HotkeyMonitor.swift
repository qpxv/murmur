import CoreGraphics
import Foundation
import MurmurCore
import os

/// Watches the dictation key system-wide through a Core Graphics event tap (needs Accessibility).
/// Space and other keys are only reported while the key is held, so normal typing is untouched.
/// The key itself never reaches other apps (except fn, see `Hotkey.isTakenOver`).
@MainActor
public final class HotkeyMonitor {
    public enum Event: Sendable {
        case hotkeyDown(at: TimeInterval)
        case hotkeyUp(at: TimeInterval)
        case space
        case otherKey
    }

    /// Returns `true` to swallow the key event (so e.g. the locking space is never typed).
    public typealias Handler = @MainActor (Event) -> Bool
    public typealias RecordingCompletion = @MainActor (HotkeyRecording) -> Void

    /// What the tap does with an event.
    fileprivate enum Verdict {
        case pass
        case swallow
        case passWithFlags(CGEventFlags)
    }

    private static let nanosecondsPerSecond: Double = 1_000_000_000

    /// The key to watch. Changing it forgets a press in progress.
    public var hotkey: Hotkey = .function {
        didSet {
            isHotkeyDown = false
            isSwallowingSpace = false
        }
    }

    private let handler: Handler
    private let logger = Logger(subsystem: Log.subsystem, category: "hotkey")
    private var tap: CFMachPort?
    private var runLoopSource: CFRunLoopSource?
    private var isHotkeyDown = false
    private var isSwallowingSpace = false
    private var recording: (recorder: HotkeyRecorder, completion: RecordingCompletion)?

    public init(handler: @escaping Handler) {
        self.handler = handler
    }

    /// Installs the tap. Returns `false` when Accessibility access is missing.
    public func start() -> Bool {
        guard tap == nil else { return true }
        let events: [CGEventType] = [.flagsChanged, .keyDown, .keyUp]
        let mask = events.reduce(CGEventMask(0)) { $0 | (CGEventMask(1) << $1.rawValue) }
        guard
            let tap = CGEvent.tapCreate(
                tap: .cgSessionEventTap,
                place: .headInsertEventTap,
                options: .defaultTap,
                eventsOfInterest: mask,
                callback: hotkeyTapCallback,
                userInfo: Unmanaged.passUnretained(self).toOpaque()
            )
        else {
            logger.error("event tap creation failed, accessibility not granted?")
            return false
        }
        let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        self.tap = tap
        self.runLoopSource = source
        return true
    }

    public func stop() {
        if let runLoopSource {
            CFRunLoopRemoveSource(CFRunLoopGetMain(), runLoopSource, .commonModes)
        }
        if let tap {
            CGEvent.tapEnable(tap: tap, enable: false)
        }
        tap = nil
        runLoopSource = nil
    }

    /// Catches the next single key (every key is swallowed meanwhile, dictation is paused) and
    /// reports it once it's decided. A recording already running is cancelled.
    public func record(completion: @escaping RecordingCompletion) {
        cancelRecording()
        isHotkeyDown = false
        isSwallowingSpace = false
        recording = (HotkeyRecorder(), completion)
    }

    public func cancelRecording() {
        guard let recording else { return }
        self.recording = nil
        recording.completion(.cancelled)
    }

    fileprivate func handle(type: CGEventType, keyCode: Int64, flags: CGEventFlags, timestamp: UInt64) -> Verdict {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            // macos disables slow or interrupted taps; turn it straight back on
            if let tap { CGEvent.tapEnable(tap: tap, enable: true) }
            return .pass
        }
        guard let code = UInt16(exactly: keyCode) else { return .pass }
        if recording != nil {
            return handleRecording(type: type, keyCode: code, flags: flags)
        }
        let time = Double(timestamp) / Self.nanosecondsPerSecond
        if code == hotkey.keyCode, let change = hotkeyChange(type: type, flags: flags) {
            let isDown = change == .pressed
            if isDown != isHotkeyDown {
                isHotkeyDown = isDown
                _ = handler(isDown ? .hotkeyDown(at: time) : .hotkeyUp(at: time))
            }
            return hotkey.isTakenOver ? .swallow : .pass
        }
        return handleOtherKey(type: type, keyCode: code, flags: flags)
    }

    private enum KeyChange {
        case pressed
        case released
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

    private func handleOtherKey(type: CGEventType, keyCode: UInt16, flags: CGEventFlags) -> Verdict {
        if type == .keyUp && keyCode == Hotkey.KeyCode.space && isSwallowingSpace {
            isSwallowingSpace = false
            return .swallow
        }
        guard isHotkeyDown else { return .pass }
        if type == .keyDown && keyCode == Hotkey.KeyCode.space {
            isSwallowingSpace = handler(.space)
            if isSwallowingSpace { return .swallow }
        } else if type == .keyDown {
            _ = handler(.otherKey)
        }
        let plainFlags = hotkey.removingModifier(from: flags)
        return plainFlags == flags ? .pass : .passWithFlags(plainFlags)
    }

    private func handleRecording(type: CGEventType, keyCode: UInt16, flags: CGEventFlags) -> Verdict {
        guard var current = recording else { return .pass }
        let result: HotkeyRecording
        switch type {
        case .keyDown:
            result = current.recorder.keyDown(keyCode)
        case .flagsChanged:
            // caps lock and unknown modifiers have no flag to read: treat them as a press
            let isDown = Hotkey(keyCode: keyCode).modifier?.isDown(in: flags) ?? true
            result = isDown ? current.recorder.modifierDown(keyCode) : current.recorder.modifierUp(keyCode)
        default:
            return .swallow
        }
        recording = current
        if result != .waiting {
            recording = nil
            current.completion(result)
        }
        return .swallow
    }
}

// a c function pointer can't capture context, so the monitor travels through userInfo.
// the tap's run loop source lives on the main run loop, so this always runs on the main thread.
private func hotkeyTapCallback(
    proxy _: CGEventTapProxy,
    type: CGEventType,
    event: CGEvent,
    userInfo: UnsafeMutableRawPointer?
) -> Unmanaged<CGEvent>? {
    guard let userInfo else { return Unmanaged.passUnretained(event) }
    let keyCode = event.getIntegerValueField(.keyboardEventKeycode)
    let flags = event.flags
    let timestamp = event.timestamp
    // the pointer never leaves the main thread: we're already on it, assumeIsolated just proves it
    nonisolated(unsafe) let monitorPointer = userInfo
    let verdict = MainActor.assumeIsolated {
        Unmanaged<HotkeyMonitor>.fromOpaque(monitorPointer).takeUnretainedValue()
            .handle(type: type, keyCode: keyCode, flags: flags, timestamp: timestamp)
    }
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
