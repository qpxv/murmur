namespace Murmur.Core;

/// <summary>How polished the cleaned text should read.</summary>
public enum CleanupStyle
{
    /// <summary>lowercase, barely any punctuation, words untouched</summary>
    Casual,
    /// <summary>as spoken, just corrected</summary>
    Normal,
    /// <summary>rewritten into polished, professional prose (same meaning)</summary>
    Formal,
}

/// <summary>
/// What Claude is allowed to change. Part of <see cref="Settings"/>. Missing JSON keys keep
/// these defaults, so saved settings survive upgrades.
/// </summary>
public sealed record CleanupOptions
{
    public CleanupStyle Style { get; init; } = CleanupStyle.Normal;
    public bool FixesMisheardWords { get; init; } = true;
    public bool FixesPunctuation { get; init; } = true;
    public bool RemovesStutters { get; init; } = true;

    public static CleanupOptions Defaults { get; } = new();

    /// <summary>
    /// Whether there's anything for Claude to do. With every fix off and the normal style,
    /// calling Claude would only cost time, so the raw text is pasted directly.
    /// </summary>
    public bool HasWork => Style != CleanupStyle.Normal || FixesMisheardWords || FixesPunctuation || RemovesStutters;
}
