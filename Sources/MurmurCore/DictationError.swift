import Foundation

/// Everything that can stop a dictation, with the message the overlay shows for it.
public enum DictationError: Error, Equatable, Sendable {
    case missingDeepgramKey
    case invalidDeepgramKey
    case microphoneDenied
    case accessibilityDenied
    case microphoneUnavailable
    case connectionFailed
    case nothingHeard

    public var message: String {
        switch self {
        case .missingDeepgramKey: "Add your Deepgram key in Settings"
        case .invalidDeepgramKey: "Deepgram rejected the key, check Settings"
        case .microphoneDenied: "Allow microphone access in System Settings"
        case .accessibilityDenied: "Allow accessibility access in System Settings"
        case .microphoneUnavailable: "Microphone couldn't start"
        case .connectionFailed: "Couldn't reach Deepgram"
        case .nothingHeard: "Didn't catch anything"
        }
    }
}
