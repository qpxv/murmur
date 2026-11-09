import Foundation
import MurmurCore
import SwiftData

/// One finished dictation, stored locally for the dashboard and history.
@Model
public final class DictationRecord {
    public var date: Date
    /// What was pasted. Empty when the user turned history text off.
    public var text: String
    /// What Deepgram heard before cleanup. Empty when history text is off.
    public var rawText: String
    public var wordCount: Int
    public var speakingSeconds: Double
    /// The app that was frontmost when the text was delivered.
    public var appName: String?
    public var wasCleaned: Bool

    public init(
        date: Date,
        text: String,
        rawText: String,
        wordCount: Int,
        speakingSeconds: Double,
        appName: String?,
        wasCleaned: Bool
    ) {
        self.date = date
        self.text = text
        self.rawText = rawText
        self.wordCount = wordCount
        self.speakingSeconds = speakingSeconds
        self.appName = appName
        self.wasCleaned = wasCleaned
    }

    public var summary: DictationSummary {
        DictationSummary(date: date, wordCount: wordCount, speakingSeconds: speakingSeconds)
    }
}
