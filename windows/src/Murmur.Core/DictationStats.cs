namespace Murmur.Core;

/// <summary>The numbers of one past dictation that the dashboard needs.</summary>
public sealed record DictationSummary(DateTimeOffset Date, int WordCount, double SpeakingSeconds);

/// <summary>
/// Dashboard numbers derived from the dictation history. "Today" and streaks follow
/// <c>zone</c> (the PC's local zone in the app, a fixed one in tests).
/// </summary>
public sealed record DictationStats
{
    private const double SecondsPerMinute = 60;

    public int TotalWords { get; }
    public int WordsToday { get; }
    public int DictationCount { get; }
    public double SpeakingSeconds { get; }

    /// <summary>How much longer typing the same words would have taken. Never negative.</summary>
    public double TimeSavedSeconds { get; }

    /// <summary>Words per minute while speaking; <c>null</c> before any speech was timed.</summary>
    public int? SpeakingWordsPerMinute { get; }

    /// <summary>Consecutive days with at least one dictation, ending today (or yesterday if today has none yet).</summary>
    public int StreakDays { get; }

    public DictationStats(IReadOnlyCollection<DictationSummary> summaries, int typingWordsPerMinute, DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = Day(now, zone);
        TotalWords = summaries.Sum(summary => summary.WordCount);
        WordsToday = summaries.Where(summary => Day(summary.Date, zone) == today).Sum(summary => summary.WordCount);
        DictationCount = summaries.Count;
        SpeakingSeconds = summaries.Sum(summary => summary.SpeakingSeconds);

        var typingSeconds = typingWordsPerMinute > 0 ? (double)TotalWords / typingWordsPerMinute * SecondsPerMinute : 0;
        TimeSavedSeconds = Math.Max(0, typingSeconds - SpeakingSeconds);
        SpeakingWordsPerMinute = SpeakingSeconds > 0
            ? (int)Math.Round(TotalWords / (SpeakingSeconds / SecondsPerMinute), MidpointRounding.AwayFromZero)
            : null;
        StreakDays = Streak(summaries.Select(summary => Day(summary.Date, zone)).ToHashSet(), today);
    }

    private static DateOnly Day(DateTimeOffset date, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(date, zone).DateTime);

    private static int Streak(HashSet<DateOnly> activeDays, DateOnly today)
    {
        var start = activeDays.Contains(today) ? today : today.AddDays(-1);
        return Enumerable.Range(0, activeDays.Count)
            .Select(offset => start.AddDays(-offset))
            .TakeWhile(activeDays.Contains)
            .Count();
    }
}
