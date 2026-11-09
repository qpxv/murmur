import Foundation

/// What the dictation pipeline should do in response to a hotkey event.
public enum HotkeyAction: Equatable, Sendable {
    case none
    case start
    case lock
    case stop
    /// The press was too short to be meant: throw the recording away.
    case cancel
}

/// Turns raw hotkey events into dictation actions.
///
/// - Holding the hotkey records until it is released (push to talk).
/// - Pressing space while the hotkey is held locks recording on, so both hands are free.
///   The next hotkey press stops it.
/// - A press shorter than `minimumHold` that was never locked is an accident and cancels.
///
/// Timestamps are seconds on any monotonic clock.
public struct HotkeyGesture: Equatable, Sendable {
    private enum Phase: Equatable, Sendable {
        case idle
        case holding(since: TimeInterval)
        // locked while the hotkey is still down; its release must not stop anything
        case lockedHeld
        case locked
        // the press that stopped a locked recording; its release is swallowed
        case awaitingRelease
    }

    private var phase: Phase = .idle
    private let minimumHold: TimeInterval

    public init(minimumHold: TimeInterval = AppConstants.Hotkey.minimumHold) {
        self.minimumHold = minimumHold
    }

    public mutating func hotkeyDown(at time: TimeInterval) -> HotkeyAction {
        switch phase {
        case .idle:
            phase = .holding(since: time)
            return .start
        case .locked:
            phase = .awaitingRelease
            return .stop
        case .holding, .lockedHeld, .awaitingRelease:
            // key repeat or a duplicate event while already down
            return .none
        }
    }

    public mutating func hotkeyUp(at time: TimeInterval) -> HotkeyAction {
        switch phase {
        case .holding(let since):
            phase = .idle
            return time - since < minimumHold ? .cancel : .stop
        case .lockedHeld:
            phase = .locked
            return .none
        case .awaitingRelease:
            phase = .idle
            return .none
        case .idle, .locked:
            return .none
        }
    }

    /// Space pressed. Only meaningful while the hotkey is held; the caller should swallow
    /// the key event whenever this returns something other than `.none`, so no space is typed.
    public mutating func spaceDown() -> HotkeyAction {
        guard case .holding = phase else { return .none }
        phase = .lockedHeld
        return .lock
    }

    /// Any other key pressed while the hotkey is held means it is being used as a modifier
    /// (fn + arrow, fn + delete), not for dictation: cancel instead of pasting.
    public mutating func otherKeyDown() -> HotkeyAction {
        guard case .holding = phase else { return .none }
        phase = .idle
        return .cancel
    }

    /// Forgets any press in progress, e.g. after the pipeline failed or was cancelled.
    public mutating func reset() {
        phase = .idle
    }
}
