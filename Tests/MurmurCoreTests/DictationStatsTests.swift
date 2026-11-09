import Foundation
import MurmurCore
import Testing

struct DictationStatsTests {
    private let calendar: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "Europe/Berlin") ?? .gmt
        return calendar
    }()

    private func date(_ day: Int, _ month: Int = 11, hour: Int = 12, minute: Int = 0) -> Date {
        let components = DateComponents(year: 2026, month: month, day: day, hour: hour, minute: minute)
        return calendar.date(from: components) ?? .distantPast
    }

    private func stats(_ summaries: [DictationSummary], typing: Int = 40, now: Date? = nil) -> DictationStats {
        DictationStats(summaries: summaries, typingWordsPerMinute: typing, now: now ?? date(9), calendar: calendar)
    }

    @Test func emptyHistoryIsAllZero() {
        let result = stats([])
        #expect(result.totalWords == 0)
        #expect(result.dictationCount == 0)
        #expect(result.timeSavedSeconds == 0)
        #expect(result.speakingWordsPerMinute == nil)
        #expect(result.streakDays == 0)
    }

    @Test func totalsAndTodayCount() {
        let result = stats([
            DictationSummary(date: date(9, hour: 9), wordCount: 100, speakingSeconds: 40),
            DictationSummary(date: date(8), wordCount: 50, speakingSeconds: 20),
        ])
        #expect(result.totalWords == 150)
        #expect(result.wordsToday == 100)
        #expect(result.dictationCount == 2)
        #expect(result.speakingSeconds == 60)
    }

    @Test func timeSavedComparesTypingWithSpeaking() {
        // 200 words at 40 wpm = 300 s of typing, spoken in 90 s
        let result = stats([DictationSummary(date: date(9), wordCount: 200, speakingSeconds: 90)])
        #expect(result.timeSavedSeconds == 210)
        #expect(result.speakingWordsPerMinute == 133)
    }

    @Test func timeSavedIsNeverNegative() {
        let result = stats([DictationSummary(date: date(9), wordCount: 10, speakingSeconds: 120)], typing: 200)
        #expect(result.timeSavedSeconds == 0)
    }

    @Test func zeroTypingSpeedSavesNothing() {
        let result = stats([DictationSummary(date: date(9), wordCount: 10, speakingSeconds: 5)], typing: 0)
        #expect(result.timeSavedSeconds == 0)
    }

    @Test func streakCountsConsecutiveDaysEndingToday() {
        let result = stats([
            DictationSummary(date: date(9), wordCount: 1, speakingSeconds: 1),
            DictationSummary(date: date(8), wordCount: 1, speakingSeconds: 1),
            DictationSummary(date: date(7), wordCount: 1, speakingSeconds: 1),
            DictationSummary(date: date(5), wordCount: 1, speakingSeconds: 1),
        ])
        #expect(result.streakDays == 3)
    }

    @Test func streakStillCountsWhenTodayHasNoDictationYet() {
        let result = stats([
            DictationSummary(date: date(8), wordCount: 1, speakingSeconds: 1),
            DictationSummary(date: date(7), wordCount: 1, speakingSeconds: 1),
        ])
        #expect(result.streakDays == 2)
    }

    @Test func streakBreaksAfterAMissedDay() {
        let result = stats([DictationSummary(date: date(6), wordCount: 1, speakingSeconds: 1)])
        #expect(result.streakDays == 0)
    }

    @Test func streakRunsAcrossMonthBoundary() {
        let result = stats(
            [
                DictationSummary(date: date(1, hour: 0, minute: 30), wordCount: 1, speakingSeconds: 1),
                DictationSummary(date: date(31, 10, hour: 23, minute: 50), wordCount: 1, speakingSeconds: 1),
            ],
            now: date(1, hour: 8)
        )
        #expect(result.streakDays == 2)
        #expect(result.wordsToday == 1)
    }

    @Test func todayFollowsLocalMidnightNotUTC() {
        // 00:30 berlin is still the previous day in utc, but today locally
        let result = stats(
            [DictationSummary(date: date(9, hour: 0, minute: 30), wordCount: 7, speakingSeconds: 3)],
            now: date(9, hour: 23)
        )
        #expect(result.wordsToday == 7)
    }
}
