namespace Murmur.Core;

/// <summary>
/// Groups finalized transcript segments into chunks for cleanup while the user is still talking.
/// A chunk closes at the first pause after <c>minWords</c>, or right away at <c>maxWords</c>, so each
/// cleanup request sees whole sentences and only the last chunk is left when they stop.
/// </summary>
public sealed class Chunker(int minWords, int maxWords)
{
    private readonly List<string> _segments = [];
    private int _wordCount;

    public Chunker()
        : this(AppConstants.Cleanup.MinChunkWords, AppConstants.Cleanup.MaxChunkWords)
    {
    }

    /// <summary>Adds a segment; returns the chunk text if this segment closed one.</summary>
    public string? Add(string segment, bool isPause)
    {
        var trimmed = segment.Trim();
        if (trimmed.Length > 0)
        {
            _segments.Add(trimmed);
            _wordCount += WordCount(trimmed);
        }
        var isLongEnough = _wordCount >= minWords && isPause;
        return isLongEnough || _wordCount >= maxWords ? Flush() : null;
    }

    /// <summary>Closes whatever is pending (when the user stops); <c>null</c> if nothing is.</summary>
    public string? Flush()
    {
        if (_segments.Count == 0)
        {
            return null;
        }
        var chunk = string.Join(' ', _segments);
        _segments.Clear();
        _wordCount = 0;
        return chunk;
    }

    public static int WordCount(string text) => Words(text).Length;

    public static string FirstWords(string text, int count) => string.Join(' ', Words(text).Take(count));

    public static string LastWords(string text, int count) => string.Join(' ', Words(text).TakeLast(count));

    // an empty separator list means "any whitespace" to string.Split
    private static readonly char[] AnyWhitespace = [];

    private static string[] Words(string text) => text.Split(AnyWhitespace, StringSplitOptions.RemoveEmptyEntries);
}
