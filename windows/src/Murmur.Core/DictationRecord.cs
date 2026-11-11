namespace Murmur.Core;

/// <summary>One finished dictation, stored locally for the dashboard and history (same fields as the Mac).</summary>
public sealed record DictationRecord
{
    public required Guid Id { get; init; }
    public required DateTimeOffset Date { get; init; }
    /// <summary>What was pasted. Empty when the user turned history text off.</summary>
    public required string Text { get; init; }
    /// <summary>What Deepgram heard before cleanup. Empty when history text is off.</summary>
    public required string RawText { get; init; }
    public required int WordCount { get; init; }
    public required double SpeakingSeconds { get; init; }
    /// <summary>The app the text was delivered to.</summary>
    public string? AppName { get; init; }
    public bool WasCleaned { get; init; }
    /// <summary>
    /// Given on first upload, never by a default: a shared default would give every older record
    /// the same id and the database would silently drop all but one.
    /// </summary>
    public Guid? SyncId { get; init; }
    public bool IsSynced { get; init; }

    public DictationSummary Summary => new(Date, WordCount, SpeakingSeconds);

    /// <summary>A new record for a delivered transcript; the text is only kept when history text is on.</summary>
    public static DictationRecord From(DictationTranscript transcript, double speakingSeconds, string? appName, bool keepsText, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Date = now,
        Text = keepsText ? transcript.Text : "",
        RawText = keepsText ? transcript.RawText : "",
        WordCount = Chunker.WordCount(transcript.Text),
        SpeakingSeconds = speakingSeconds,
        AppName = appName,
        WasCleaned = transcript.WasCleaned,
    };
}
