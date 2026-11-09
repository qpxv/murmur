import Carbon.HIToolbox
import CoreGraphics
import Foundation
import os

/// Watches the fn key system-wide through a Core Graphics event tap (needs Accessibility).
/// Space and other keys are only reported while fn is held, so normal typing is untouched.
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

    private static let nanosecondsPerSecond: Double = 1_000_000_000
    private static let fnKeyCode = Int64(kVK_Function)
    private static let spaceKeyCode = Int64(kVK_Space)

    private let handler: Handler
    private let logger = Logger(subsystem: Log.subsystem, category: "hotkey")
    private var tap: CFMachPort?
    private var runLoopSource: CFRunLoopSource?
    private var isFnDown = false
    private var isSwallowingSpace = false

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

    /// Returns whether to swallow the event.
    fileprivate func handle(type: CGEventType, keyCode: Int64, flags: CGEventFlags, timestamp: UInt64) -> Bool {
        let time = Double(timestamp) / Self.nanosecondsPerSecond
        switch type {
        case .tapDisabledByTimeout, .tapDisabledByUserInput:
            // macos disables slow or interrupted taps; turn it straight back on
            if let tap { CGEvent.tapEnable(tap: tap, enable: true) }
            return false
        case .flagsChanged where keyCode == Self.fnKeyCode:
            let isDown = flags.contains(.maskSecondaryFn)
            guard isDown != isFnDown else { return false }
            isFnDown = isDown
            _ = handler(isDown ? .hotkeyDown(at: time) : .hotkeyUp(at: time))
            return false
        case .keyDown where keyCode == Self.spaceKeyCode && isFnDown:
            isSwallowingSpace = handler(.space)
            return isSwallowingSpace
        case .keyUp where keyCode == Self.spaceKeyCode && isSwallowingSpace:
            isSwallowingSpace = false
            return true
        case .keyDown where isFnDown:
            _ = handler(.otherKey)
            return false
        default:
            return false
        }
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
    let shouldSwallow = MainActor.assumeIsolated {
        Unmanaged<HotkeyMonitor>.fromOpaque(monitorPointer).takeUnretainedValue()
            .handle(type: type, keyCode: keyCode, flags: flags, timestamp: timestamp)
    }
    return shouldSwallow ? nil : Unmanaged.passUnretained(event)
}
