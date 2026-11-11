namespace Murmur.Core;

/// <summary>
/// Keeps every chunk's raw and cleaned text by chunk number, so cleanups that finish out of
/// order still assemble in spoken order, and a chunk without a cleanup uses its raw text.
/// </summary>
public sealed class TranscriptAssembler
{
    private readonly Dictionary<int, string> _rawChunks = [];
    private readonly Dictionary<int, string> _cleanedChunks = [];
    private int _nextNumber;

    /// <summary>Registers a new chunk and returns its number.</summary>
    public int AddChunk(string raw)
    {
        var number = _nextNumber;
        _rawChunks[number] = raw;
        _nextNumber++;
        return number;
    }

    public void SetCleaned(string text, int chunk)
    {
        var trimmed = text.Trim();
        if (!_rawChunks.ContainsKey(chunk) || trimmed.Length == 0)
        {
            return;
        }
        _cleanedChunks[chunk] = trimmed;
    }

    /// <summary>The final text: cleaned where available, raw otherwise, in spoken order.</summary>
    public string Text => Join(number => _cleanedChunks.GetValueOrDefault(number) ?? _rawChunks.GetValueOrDefault(number));

    /// <summary>What Deepgram heard, untouched.</summary>
    public string RawText => Join(_rawChunks.GetValueOrDefault);

    /// <summary>Raw text of the most recent chunk, as context for cleaning the next one.</summary>
    public string LastRawWords(int count) =>
        _rawChunks.TryGetValue(_nextNumber - 1, out var last) ? Chunker.LastWords(last, count) : "";

    public bool IsEmpty => _rawChunks.Count == 0;

    /// <summary>Whether at least one chunk was cleaned.</summary>
    public bool HasCleanedText => _cleanedChunks.Count > 0;

    private string Join(Func<int, string?> textForChunk) =>
        string.Join(' ', Enumerable.Range(0, _nextNumber).Select(textForChunk).OfType<string>());
}
