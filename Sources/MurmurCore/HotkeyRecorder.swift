import Foundation

/// Why a pressed key can't become the dictation key.
public enum HotkeyRejection: Equatable, Sendable {
    /// A modifier held together with another key (⌘ + c): only single keys are allowed.
    case combination
    /// Space locks a recording, so it can't also start one.
    case space
    /// macOS only reports Caps Lock switching on and off, never it being held.
    case capsLock

    public var message: String {
        switch self {
        case .combination: "Just one key, no combinations."
        case .space: "Space locks a recording, pick another key."
        case .capsLock: "macOS can't tell when Caps Lock is held, pick another key."
        }
    }
}

/// Where recording a new dictation key stands after a key event.
public enum HotkeyRecording: Equatable, Sendable {
    case waiting
    case recorded(Hotkey)
    case rejected(HotkeyRejection)
    case cancelled
}

/// Decides which single key the user pressed while "Record…" is waiting.
/// A normal key counts on key down; a modifier only on its release, so ⌘ + c is caught as a
/// combination instead of recording ⌘. Escape cancels.
public struct HotkeyRecorder: Equatable, Sendable {
    // a modifier that went down and may still become the key, if nothing else is pressed before it's released
    private var pendingModifier: UInt16?

    public init() {}

    public mutating func modifierDown(_ keyCode: UInt16) -> HotkeyRecording {
        guard keyCode != Hotkey.KeyCode.capsLock else { return .rejected(.capsLock) }
        guard pendingModifier != keyCode else { return .waiting }
        guard pendingModifier == nil else { return .rejected(.combination) }
        pendingModifier = keyCode
        return .waiting
    }

    public mutating func modifierUp(_ keyCode: UInt16) -> HotkeyRecording {
        // a modifier that was already held when recording started
        guard pendingModifier == keyCode else { return .waiting }
        return .recorded(Hotkey(keyCode: keyCode))
    }

    public mutating func keyDown(_ keyCode: UInt16) -> HotkeyRecording {
        guard pendingModifier == nil else { return .rejected(.combination) }
        switch keyCode {
        case Hotkey.KeyCode.escape: return .cancelled
        case Hotkey.KeyCode.space: return .rejected(.space)
        default: return .recorded(Hotkey(keyCode: keyCode))
        }
    }
}
