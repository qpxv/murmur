import Foundation

/// The outcome of one dictation: what gets pasted, and what Deepgram originally heard.
public struct DictationTranscript: Equatable, Sendable {
    public let text: String
    public let rawText: String
    /// Whether Claude cleaned at least part of it.
    public let wasCleaned: Bool

    public init(text: String, rawText: String, wasCleaned: Bool) {
        self.text = text
        self.rawText = rawText
        self.wasCleaned = wasCleaned
    }
}
