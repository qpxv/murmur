
namespace Murmur.Core.Tests;

public class DictationStatsTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    // a wall-clock time in berlin, whatever zone the machine running the tests is in
    private static DateTimeOffset Date(int day, int month = 11, int hour = 12, int minute = 0)
    {
        var local = new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Berlin.GetUtcOffset(local));
    }

    private static DictationStats Stats(IReadOnlyCollection<DictationSummary> summaries, int typing = 40, DateTimeOffset? now = null) =>
        new(summaries, typing, now ?? Date(9), Berlin);

    private static DictationSummary Summary(DateTimeOffset date, int words = 1, double seconds = 1) => new(date, words, seconds);

    [Fact]
    public void Empty_history_is_all_zero()
    {
        var result = Stats([]);
        Assert.Equal(0, result.TotalWords);
        Assert.Equal(0, result.DictationCount);
        Assert.Equal(0, result.TimeSavedSeconds);
        Assert.Null(result.SpeakingWordsPerMinute);
        Assert.Equal(0, result.StreakDays);
    }

    [Fact]
    public void Totals_and_today_count()
    {
        var result = Stats([Summary(Date(9, hour: 9), 100, 40), Summary(Date(8), 50, 20)]);
        Assert.Equal(150, result.TotalWords);
        Assert.Equal(100, result.WordsToday);
        Assert.Equal(2, result.DictationCount);
        Assert.Equal(60, result.SpeakingSeconds);
    }

    [Fact]
    public void Time_saved_compares_typing_with_speaking()
    {
        // 200 words at 40 wpm = 300 s of typing, spoken in 90 s
        var result = Stats([Summary(Date(9), 200, 90)]);
        Assert.Equal(210, result.TimeSavedSeconds);
        Assert.Equal(133, result.SpeakingWordsPerMinute);
    }

    [Fact]
    public void Time_saved_is_never_negative() => Assert.Equal(0, Stats([Summary(Date(9), 10, 120)], typing: 200).TimeSavedSeconds);

    [Fact]
    public void Zero_typing_speed_saves_nothing() => Assert.Equal(0, Stats([Summary(Date(9), 10, 5)], typing: 0).TimeSavedSeconds);

    [Fact]
    public void Streak_counts_consecutive_days_ending_today() =>
        Assert.Equal(3, Stats([Summary(Date(9)), Summary(Date(8)), Summary(Date(7)), Summary(Date(5))]).StreakDays);

    [Fact]
    public void Streak_still_counts_when_today_has_no_dictation_yet() =>
        Assert.Equal(2, Stats([Summary(Date(8)), Summary(Date(7))]).StreakDays);

    [Fact]
    public void Streak_breaks_after_a_missed_day() => Assert.Equal(0, Stats([Summary(Date(6))]).StreakDays);

    [Fact]
    public void Streak_runs_across_month_boundary()
    {
        var result = Stats(
            [Summary(Date(1, hour: 0, minute: 30)), Summary(Date(31, 10, hour: 23, minute: 50))],
            now: Date(1, hour: 8));
        Assert.Equal(2, result.StreakDays);
        Assert.Equal(1, result.WordsToday);
    }

    [Fact]
    public void Today_follows_local_midnight_not_utc()
    {
        // 00:30 berlin is still the previous day in utc, but today locally
        var result = Stats([Summary(Date(9, hour: 0, minute: 30), 7, 3)], now: Date(9, hour: 23));
        Assert.Equal(7, result.WordsToday);
    }
}
