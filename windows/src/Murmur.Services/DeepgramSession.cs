using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// One live Deepgram transcription (a WebSocket) for one dictation. Audio is queued with
/// <see cref="Send"/> (safe from the capture thread) and streamed in order once the socket is
/// open. Finalized segments come out of <see cref="Results"/> as they arrive. <see cref="FinishAsync"/>
/// flushes the last words; <see cref="Results"/> completes right after.
/// </summary>
public sealed class DeepgramSession : IDisposable
{
    private const string Category = "deepgram";
    private const int ReceiveBufferBytes = 16 * 1024;

    private readonly ClientWebSocket _socket = new();
    private readonly Uri _uri;
    private readonly Channel<byte[]> _audio = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<DeepgramMessages.FinalResult> _results =
        Channel.CreateUnbounded<DeepgramMessages.FinalResult>(new UnboundedChannelOptions { SingleWriter = true });
    private readonly TaskCompletionSource _finalized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _closing = new();
    private Task _sending = Task.CompletedTask;
    private Task _receiving = Task.CompletedTask;
    private volatile bool _hasHeardSpeech;
    private volatile bool _isClosed;
    private DictationError? _failure;

    public DeepgramSession(string apiKey, IReadOnlyList<string> vocabulary)
    {
        _uri = DeepgramMessages.ListenUri(vocabulary);
        _socket.Options.SetRequestHeader("Authorization", $"Token {apiKey}");
        // keeps the http status of a refused upgrade, so a wrong key can be told apart from no network
        _socket.Options.CollectHttpResponseDetails = true;
    }

    /// <summary>Every finalized segment, in order. Completes when the stream finishes or is cancelled.</summary>
    public ChannelReader<DeepgramMessages.FinalResult> Results => _results.Reader;

    /// <summary>Connects in the background; audio sent meanwhile waits in the queue.</summary>
    public void Start()
    {
        var connecting = ConnectAsync();
        _sending = SendAudioAsync(connecting);
        _receiving = ReceiveAsync(connecting);
    }

    /// <summary>Queues audio for sending. Safe to call from any thread.</summary>
    public void Send(byte[] pcm) => _audio.Writer.TryWrite(pcm);

    /// <summary>
    /// Sends the remaining audio and waits for Deepgram to finalize it. Only throws when the
    /// connection failed before any speech arrived; text that already came through is kept.
    /// </summary>
    /// <exception cref="DictationException">Nothing was heard because the connection failed.</exception>
    public async Task FinishAsync()
    {
        _audio.Writer.TryComplete();
        await _sending;
        if (_failure is null)
        {
            await SendTextAsync(DeepgramMessages.Finalize);
            await Task.WhenAny(_finalized.Task, Task.Delay(AppConstants.Deepgram.FinalizeTimeout));
            await SendTextAsync(DeepgramMessages.CloseStream);
        }
        await CloseAsync();
        if (!_hasHeardSpeech && _failure is { } failure)
        {
            throw new DictationException(failure);
        }
    }

    public void Cancel()
    {
        _audio.Writer.TryComplete();
        _ = CloseAsync();
    }

    public void Dispose()
    {
        _socket.Dispose();
        _closing.Dispose();
    }

    private async Task<bool> ConnectAsync()
    {
        var started = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
        timeout.CancelAfter(AppConstants.Deepgram.ConnectTimeout);
        try
        {
            await _socket.ConnectAsync(_uri, timeout.Token);
            Log.Info(Category, $"connected in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
            return true;
        }
        catch (WebSocketException exception)
        {
            Fail(_socket.HttpStatusCode == HttpStatusCode.Unauthorized ? DictationError.InvalidDeepgramKey : DictationError.ConnectionFailed, exception);
        }
        catch (OperationCanceledException exception) when (!_closing.IsCancellationRequested)
        {
            Fail(DictationError.ConnectionFailed, exception);
        }
        catch (OperationCanceledException)
        {
            // cancelled by us
        }
        return false;
    }

    private async Task SendAudioAsync(Task<bool> connecting)
    {
        if (!await connecting)
        {
            return;
        }
        try
        {
            await foreach (var chunk in _audio.Reader.ReadAllAsync(_closing.Token))
            {
                await _socket.SendAsync(chunk, WebSocketMessageType.Binary, endOfMessage: true, _closing.Token);
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            Fail(DictationError.ConnectionFailed, exception);
        }
    }

    private async Task SendTextAsync(string text)
    {
        try
        {
            await _socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, endOfMessage: true, _closing.Token);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            Fail(DictationError.ConnectionFailed, exception);
        }
    }

    private async Task ReceiveAsync(Task<bool> connecting)
    {
        try
        {
            if (!await connecting)
            {
                return;
            }
            var buffer = new byte[ReceiveBufferBytes];
            var message = new ArrayBufferWriter<byte>(ReceiveBufferBytes);
            while (!_closing.IsCancellationRequested)
            {
                var received = await _socket.ReceiveAsync(buffer, _closing.Token);
                if (received.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
                message.Write(buffer.AsSpan(0, received.Count));
                if (received.EndOfMessage)
                {
                    Handle(message.WrittenSpan);
                    message.ResetWrittenCount();
                }
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            Fail(DictationError.ConnectionFailed, exception);
        }
        finally
        {
            _results.Writer.TryComplete();
            _finalized.TrySetResult();
        }
    }

    private void Handle(ReadOnlySpan<byte> json)
    {
        if (DeepgramMessages.ParseFinalResult(json) is not { } result)
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(result.Transcript))
        {
            _hasHeardSpeech = true;
        }
        _results.Writer.TryWrite(result);
        if (result.IsFromFinalize)
        {
            _finalized.TrySetResult();
        }
    }

    private async Task CloseAsync()
    {
        if (_isClosed)
        {
            return;
        }
        _isClosed = true;
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                using var timeout = new CancellationTokenSource(AppConstants.Deepgram.FinalizeTimeout);
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
            // closing is best effort; the socket is torn down below either way
        }
        await _closing.CancelAsync();
        await Task.WhenAll(_sending, _receiving);
        _results.Writer.TryComplete();
        _finalized.TrySetResult();
    }

    private void Fail(DictationError error, Exception exception)
    {
        // the pending receive always fails once we close the socket ourselves; that's not a failure
        if (_failure is not null || _isClosed)
        {
            return;
        }
        _failure = error;
        Log.Error(Category, $"connection failed ({error}, http {(int)_socket.HttpStatusCode})", exception);
    }
}
