namespace Murmur.Core;

/// <summary>
/// Everything the user can configure. Exists exactly once; the UI changes it through the settings
/// store. Missing JSON keys keep these defaults, so adding a setting never wipes the saved ones.
/// </summary>
public sealed record Settings
{
    /// <summary>Sets the input device to full volume while recording, so whispering works.</summary>
    public bool IsMicBoostEnabled { get; init; } = true;

    /// <summary>Endpoint id of the chosen microphone; <c>null</c> follows the Windows default.</summary>
    public string? InputDeviceId { get; init; }

    /// <summary>Terms that must be spelled exactly like this (sent to speech-to-text and cleanup).</summary>
    public IReadOnlyList<string> Vocabulary { get; init; } =
        ["Claude", "Claude Code", "Anthropic", "Opus", "Sonnet", "Haiku", "cmux", "SwiftUI"];

    /// <summary>Where the overlay pill sits on screen.</summary>
    public OverlayPosition OverlayPosition { get; init; } = OverlayPosition.BottomCenter;

    /// <summary>Let Claude fix misheard words and punctuation before pasting.</summary>
    public bool IsCleanupEnabled { get; init; } = true;

    /// <summary>Which fixes Claude makes and in what style.</summary>
    public CleanupOptions Cleanup { get; init; } = CleanupOptions.Defaults;

    /// <summary>The user's typing speed, for the "time saved" number on the dashboard.</summary>
    public int TypingWordsPerMinute { get; init; } = 100;

    /// <summary>Keep the text of past dictations. Off still counts words and time for the dashboard.</summary>
    public bool IsHistoryEnabled { get; init; } = true;

    /// <summary>How this device is labelled in synced stats; empty uses the computer's name.</summary>
    public string SyncDeviceName { get; init; } = "";

    public static Settings Defaults { get; } = new();

    /// <summary>
    /// The settings with a vocabulary term added, or <c>null</c> when it's blank or already there
    /// (ignoring case).
    /// </summary>
    public Settings? AddingVocabularyTerm(string term)
    {
        var trimmed = term.Trim();
        var isDuplicate = Vocabulary.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase));
        return trimmed.Length == 0 || isDuplicate ? null : this with { Vocabulary = [.. Vocabulary, trimmed] };
    }

    // records compare lists by reference; settings compare by content (tests, and "did anything change")
    public bool Equals(Settings? other) =>
        other is not null
        && IsMicBoostEnabled == other.IsMicBoostEnabled
        && InputDeviceId == other.InputDeviceId
        && Vocabulary.SequenceEqual(other.Vocabulary)
        && OverlayPosition == other.OverlayPosition
        && IsCleanupEnabled == other.IsCleanupEnabled
        && Cleanup == other.Cleanup
        && TypingWordsPerMinute == other.TypingWordsPerMinute
        && IsHistoryEnabled == other.IsHistoryEnabled
        && SyncDeviceName == other.SyncDeviceName;

    public override int GetHashCode() => HashCode.Combine(IsMicBoostEnabled, InputDeviceId, OverlayPosition, Cleanup);
}
