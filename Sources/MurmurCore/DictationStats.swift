import Foundation

/// The numbers of one past dictation that the dashboard needs.
public struct DictationSummary: Equatable, Sendable {
    public let date: Date
    public let wordCount: Int
    public let speakingSeconds: Double

    public init(date: Date, wordCount: Int, speakingSeconds: Double) {
        self.date = date
        self.wordCount = wordCount
        self.speakingSeconds = speakingSeconds
    }
}

/// Dashboard numbers derived from the dictation history. "Today" and streaks follow `calendar`
/// (the Mac's local calendar in the app, a fixed one in tests).
public struct DictationStats: Equatable, Sendable {
    private static let secondsPerMinute: Double = 60

    public let totalWords: Int
    public let wordsToday: Int
    public let dictationCount: Int
    public let speakingSeconds: Double
    /// How much longer typing the same words would have taken. Never negative.
    public let timeSavedSeconds: Double
    /// Words per minute while speaking; `nil` before any speech was timed.
    public let speakingWordsPerMinute: Int?
    /// Consecutive days with at least one dictation, ending today (or yesterday if today has none yet).
    public let streakDays: Int

    public init(summaries: [DictationSummary], typingWordsPerMinute: Int, now: Date, calendar: Calendar) {
        totalWords = summaries.reduce(0) { $0 + $1.wordCount }
        wordsToday =
            summaries
            .filter { calendar.isDate($0.date, inSameDayAs: now) }
            .reduce(0) { $0 + $1.wordCount }
        dictationCount = summaries.count
        speakingSeconds = summaries.reduce(0) { $0 + $1.speakingSeconds }

        let typingSeconds =
            typingWordsPerMinute > 0
            ? Double(totalWords) / Double(typingWordsPerMinute) * Self.secondsPerMinute
            : 0
        timeSavedSeconds = max(0, typingSeconds - speakingSeconds)
        speakingWordsPerMinute =
            speakingSeconds > 0
            ? Int((Double(totalWords) / (speakingSeconds / Self.secondsPerMinute)).rounded())
            : nil
        streakDays = Self.streak(of: summaries.map(\.date), now: now, calendar: calendar)
    }

    private static func streak(of dates: [Date], now: Date, calendar: Calendar) -> Int {
        let activeDays = Set(dates.map { calendar.startOfDay(for: $0) })
        let today = calendar.startOfDay(for: now)
        guard let yesterday = calendar.date(byAdding: .day, value: -1, to: today) else { return 0 }
        let start = activeDays.contains(today) ? today : yesterday
        let days = sequence(first: start) { calendar.date(byAdding: .day, value: -1, to: $0) }
        return days.prefix { activeDays.contains($0) }.count
    }
}
