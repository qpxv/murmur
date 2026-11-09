import Foundation

/// How the current recording ends: on hotkey release, or on the next hotkey press.
public enum RecordingMode: Equatable, Sendable {
    case pushToTalk
    case locked
}

/// The one state the whole dictation pipeline and its UI are in.
public enum DictationState: Equatable, Sendable {
    case idle
    case recording(RecordingMode)
    case finishing
    case failed(DictationError)

    public var isRecording: Bool {
        if case .recording = self { return true }
        return false
    }

    /// The state after a hotkey action. Actions that make no sense in the
    /// current state (a stop while idle, a lock while finishing) change nothing.
    public func applying(_ action: HotkeyAction) -> Self {
        switch (self, action) {
        case (.idle, .start), (.failed, .start):
            return .recording(.pushToTalk)
        case (.recording, .lock):
            return .recording(.locked)
        case (.recording, .stop):
            return .finishing
        case (.recording, .cancel):
            return .idle
        default:
            return self
        }
    }
}
