using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// Cleans one dictation chunk by chunk while the user is still talking. Segments come in from
/// Deepgram; a closed chunk waits until the next words arrive, then goes to Claude with both the
/// words before and after it as context, so sentences split across chunks still read as one.
/// <see cref="FinishAsync"/> sends what's left, waits a bounded time for the requests in flight
/// and assembles the result. Thread-safe.
/// </summary>
public sealed class CleanupSession : IDisposable
{
    private readonly ClaudeCleaner? _cleaner;
    private readonly IReadOnlyList<string> _vocabulary;
    private readonly string? _appName;
    private readonly Lock _gate = new();
    private readonly Chunker _chunker = new();
    private readonly TranscriptAssembler _assembler = new();
    private readonly List<Task> _requests = [];
    private readonly CancellationTokenSource _cancellation = new();
    // closed, but not sent yet: waiting for the words that follow it
    private WaitingChunk? _waiting;

    /// <param name="cleaner"><c>null</c> when cleanup is off or no Claude key is set: chunks then stay raw.</param>
    /// <param name="vocabulary">Terms Claude must spell exactly like this.</param>
    /// <param name="appName">The app the user dictates into, as context for Claude.</param>
    public CleanupSession(ClaudeCleaner? cleaner, IReadOnlyList<string> vocabulary, string? appName)
    {
        _cleaner = cleaner;
        _vocabulary = vocabulary;
        _appName = appName;
    }

    public void Add(DeepgramMessages.FinalResult result)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(result.Transcript))
            {
                SendWaiting(followedBy: result.Transcript);
            }
            if (_chunker.Add(result.Transcript, result.IsPause) is { } chunk)
            {
                Hold(chunk);
            }
        }
    }

    public async Task<DictationTranscript> FinishAsync()
    {
        Task[] pending;
        lock (_gate)
        {
            if (_chunker.Flush() is { } rest)
            {
                SendWaiting(followedBy: rest);
                Hold(rest);
            }
            SendWaiting(followedBy: "");
            pending = [.. _requests];
        }
        // whatever isn't back by then is pasted raw
        await Task.WhenAny(Task.WhenAll(pending), Task.Delay(AppConstants.Cleanup.FinishTimeout));
        await _cancellation.CancelAsync();
        lock (_gate)
        {
            return new DictationTranscript(_assembler.Text, _assembler.RawText, _assembler.HasCleanedText);
        }
    }

    public void Cancel() => _cancellation.Cancel();

    // the cleaner belongs to the caller, which reuses it (and its open connection) across dictations
    public void Dispose() => _cancellation.Dispose();

    private void Hold(string raw)
    {
        var previous = _assembler.LastRawWords(AppConstants.Cleanup.ContextWords);
        var number = _assembler.AddChunk(raw);
        _waiting = new WaitingChunk(number, raw, previous);
    }

    private void SendWaiting(string followedBy)
    {
        if (_waiting is not { } chunk)
        {
            return;
        }
        _waiting = null;
        if (_cleaner is not { } cleaner)
        {
            return;
        }
        var message = CleanupPrompt.UserMessage(
            chunk.Raw,
            chunk.Previous,
            Chunker.FirstWords(followedBy, AppConstants.Cleanup.ContextWords),
            _vocabulary,
            _appName);
        var token = _cancellation.Token;
        _requests.Add(Task.Run(
            async () =>
            {
                if (await cleaner.CleanAsync(message, token) is { } cleaned)
                {
                    lock (_gate)
                    {
                        _assembler.SetCleaned(cleaned, chunk.Number);
                    }
                }
            },
            token));
    }

    private sealed record WaitingChunk(int Number, string Raw, string Previous);
}
