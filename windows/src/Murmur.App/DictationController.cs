using System.Diagnostics;
using System.Windows.Threading;
using Murmur.Core;
using Murmur.Services;

namespace Murmur.App;

/// <summary>
/// Runs one dictation at a time: Caps Lock -> mic -> Deepgram -> Claude cleanup -> paste or
/// clipboard. Owns the one <see cref="DictationState"/> the pill renders. Lives on the UI thread.
/// </summary>
public sealed class DictationController : IDisposable
{
    private const string Category = "dictation";

    private readonly SettingsStore _settings;
    private readonly Dispatcher _dispatcher;
    private readonly HotkeyGesture _gesture = new();
    private readonly AudioCapture _audio = new();
    private readonly MicBoost _micBoost = new();
    private readonly KeyboardHook _hook;
    private readonly float[] _levels = new float[AppConstants.Overlay.VoiceBarCount];
    private ActiveDictation? _active;
    private CachedCleaner? _cleaner;
    private DispatcherTimer? _errorReset;
    // audio blocks of the current recording, logged on stop: 0 means the mic delivered nothing
    private int _capturedBlocks;
    // blocks that hit full scale: a high share means the mic boost distorts the voice
    private int _clippedBlocks;
    // the loudest block since the voice bar last moved, and when it moved
    private float _pendingLevel;
    private long _lastBarAt;

    public DictationController(SettingsStore settings, Dispatcher dispatcher)
    {
        _settings = settings;
        _dispatcher = dispatcher;
        _hook = new KeyboardHook(Handle);
    }

    public DictationState State { get; private set; } = DictationState.Idle;

    public bool IsHotkeyActive => _hook.IsActive;

    /// <summary>Raised on the UI thread whenever <see cref="State"/> changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>The newest mic levels (0...1, oldest first) while recording.</summary>
    public IReadOnlyList<float> Levels => _levels;

    /// <summary>Raised on the UI thread whenever <see cref="Levels"/> changes.</summary>
    public event EventHandler? LevelsChanged;

    /// <summary>A dictation ended up on the clipboard because no text field was focused.</summary>
    public event EventHandler? CopiedToClipboard;

    /// <summary>A dictation finished and was pasted or copied.</summary>
    public event EventHandler<DeliveredEventArgs>? Delivered;

    /// <summary>Called once at launch: undo a mic boost a crash left behind, start listening for Caps Lock.</summary>
    public bool Activate()
    {
        MicBoost.RestorePendingFromLastRun();
        return _hook.Start();
    }

    /// <summary>Called on quit: never leave the mic boosted or a socket open.</summary>
    public void Dispose()
    {
        Cancel();
        _hook.Dispose();
        _audio.Dispose();
        _cleaner?.Cleaner.Dispose();
    }

    // runs inside the keyboard hook: decide quickly, do the work after the hook has returned
    private bool Handle(KeyboardHook.KeyEvent keyEvent, TimeSpan time)
    {
        var action = keyEvent switch
        {
            KeyboardHook.KeyEvent.HotkeyDown => _gesture.HotkeyDown(time),
            KeyboardHook.KeyEvent.HotkeyUp => _gesture.HotkeyUp(time),
            KeyboardHook.KeyEvent.Space => _gesture.SpaceDown(),
            _ => _gesture.OtherKeyDown(),
        };
        if (action != HotkeyAction.None)
        {
            _dispatcher.BeginInvoke(() => Perform(action));
        }
        // every space typed while caps lock is down during a recording belongs to the lock gesture
        return keyEvent == KeyboardHook.KeyEvent.Space && (action == HotkeyAction.Lock || State.IsRecording);
    }

    private void Perform(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.Start:
                Start();
                break;
            case HotkeyAction.Lock:
                SetState(State.Applying(HotkeyAction.Lock));
                break;
            case HotkeyAction.Stop:
                Stop();
                break;
            case HotkeyAction.Cancel:
                Cancel();
                break;
            default:
                break;
        }
    }

    private void Start()
    {
        if (State.Applying(HotkeyAction.Start) == State)
        {
            // still finishing the previous dictation
            _gesture.Reset();
            return;
        }
        if (CredentialStore.Read(AppConstants.Credentials.DeepgramTarget) is not { } apiKey)
        {
            Fail(DictationError.MissingDeepgramKey);
            return;
        }
        try
        {
            Begin(apiKey);
        }
        catch (DictationException exception)
        {
            Fail(exception.Error);
        }
    }

    private void Begin(string apiKey)
    {
        _errorReset?.Stop();
        var settings = _settings.Current;
#pragma warning disable CA2000 // owned by the ActiveDictation below, disposed when it's delivered or cancelled
        var deepgram = new DeepgramSession(apiKey, settings.Vocabulary);
        var session = new CleanupSession(Cleaner(settings), settings.Vocabulary, TextInserter.ForegroundAppName());
#pragma warning restore CA2000
        if (settings.IsMicBoostEnabled)
        {
            _micBoost.Boost(settings.InputDeviceId);
        }
        _capturedBlocks = 0;
        _clippedBlocks = 0;
        _pendingLevel = 0;
        _lastBarAt = Stopwatch.GetTimestamp();
        try
        {
            _audio.Start(settings.InputDeviceId, (pcm, measure) =>
            {
                deepgram.Send(pcm);
                _dispatcher.BeginInvoke(() => Record(measure));
            });
        }
        catch (DictationException)
        {
            _micBoost.Restore();
            deepgram.Dispose();
            session.Dispose();
            throw;
        }
#pragma warning disable CA2025 // both are disposed only after the forwarding task has finished (DeliverAsync, DisposeWhenDoneAsync)
        deepgram.Start();
        var forwarding = ForwardAsync(deepgram, session);
#pragma warning restore CA2025
        _active = new ActiveDictation(deepgram, session, forwarding, Stopwatch.GetTimestamp());
        Array.Clear(_levels);
        SetState(State.Applying(HotkeyAction.Start));
    }

    // moves deepgram's segments into the cleanup session; ends when the stream ends
    private static async Task ForwardAsync(DeepgramSession deepgram, CleanupSession session)
    {
        await foreach (var result in deepgram.Results.ReadAllAsync())
        {
            session.Add(result);
        }
    }

    private ClaudeCleaner? Cleaner(Settings settings)
    {
        if (!settings.IsCleanupEnabled || !settings.Cleanup.HasWork)
        {
            return null;
        }
        if (CredentialStore.Read(AppConstants.Credentials.AnthropicTarget) is not { } key)
        {
            Log.Info(Category, "cleanup is on but no claude key is set, pasting raw text");
            return null;
        }
        // one client per key and options, so its connection stays warm between dictations
        if (_cleaner is { } cached && cached.ApiKey == key && cached.Options == settings.Cleanup)
        {
            return cached.Cleaner;
        }
        _cleaner?.Cleaner.Dispose();
#pragma warning disable CA2000 // kept in _cleaner and disposed when replaced or on quit
        _cleaner = new CachedCleaner(key, settings.Cleanup, new ClaudeCleaner(key, settings.Cleanup));
#pragma warning restore CA2000
        return _cleaner.Cleaner;
    }

    private void Stop()
    {
        if (!State.IsRecording)
        {
            return;
        }
        SetState(State.Applying(HotkeyAction.Stop));
        if (_active is not { } active)
        {
            StopCapture();
            SetState(DictationState.Idle);
            return;
        }
        _active = null;
        var speakingSeconds = Stopwatch.GetElapsedTime(active.StartedAt).TotalSeconds;
        _ = DeliverAsync(active, speakingSeconds);
    }

    private async Task DeliverAsync(ActiveDictation dictation, double speakingSeconds)
    {
        try
        {
            // the last word is still on its way (bluetooth delay, audio buffers): keep the mic open a moment
            await Task.Delay(AppConstants.Audio.StopTail);
            StopCapture();
            Log.Info(Category, $"recording stopped after {speakingSeconds:F1} s: {_capturedBlocks} audio blocks, {_clippedBlocks} clipped");
            await dictation.Deepgram.FinishAsync();
            // every segment has reached the session once forwarding ends with the stream
            await dictation.Forwarding;
            var transcript = await dictation.Session.FinishAsync();
            if (string.IsNullOrWhiteSpace(transcript.Text))
            {
                Fail(DictationError.NothingHeard);
                return;
            }
            var appName = TextInserter.ForegroundAppName();
            var outcome = await TextInserter.InsertAsync(transcript.Text);
            SetState(DictationState.Idle);
            Log.Info(Category, $"delivered {Chunker.WordCount(transcript.Text)} words, cleaned={transcript.WasCleaned}, {outcome}");
            Delivered?.Invoke(this, new DeliveredEventArgs(transcript, speakingSeconds, appName));
            if (outcome == TextInserter.Outcome.CopiedToClipboard)
            {
                CopiedToClipboard?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (DictationException exception)
        {
            dictation.Session.Cancel();
            Fail(exception.Error);
        }
#pragma warning disable CA1031 // the delivery runs detached: anything unexpected must still end in a visible state, not a stuck spinner
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Log.Crash(Category, exception);
            dictation.Session.Cancel();
            Fail(DictationError.Unexpected);
        }
        finally
        {
            dictation.Deepgram.Dispose();
            dictation.Session.Dispose();
        }
    }

    private void Cancel()
    {
        StopCapture();
        if (_active is { } active)
        {
            active.Deepgram.Cancel();
            active.Session.Cancel();
            _ = DisposeWhenDoneAsync(active);
        }
        _active = null;
        _gesture.Reset();
        if (State.IsRecording)
        {
            SetState(State.Applying(HotkeyAction.Cancel));
        }
    }

    // the socket and session go once forwarding has seen the stream end
    private static async Task DisposeWhenDoneAsync(ActiveDictation dictation)
    {
        await dictation.Forwarding;
        dictation.Deepgram.Dispose();
        dictation.Session.Dispose();
    }

    private void StopCapture()
    {
        _audio.Stop();
        _micBoost.Restore();
        Array.Clear(_levels);
    }

    private void Fail(DictationError error)
    {
        // only the error name (e.g. NothingHeard), never text
        Log.Error(Category, $"dictation failed: {error}");
        Cancel();
        SetState(DictationState.Failed(error));
        // one-shot: back to idle after a moment, unless something else happened meanwhile
        _errorReset?.Stop();
        _errorReset = new DispatcherTimer(AppConstants.Overlay.ErrorDisplayDuration, DispatcherPriority.Normal, (_, _) =>
        {
            _errorReset?.Stop();
            if (State is DictationState.FailedState)
            {
                SetState(DictationState.Idle);
            }
        }, _dispatcher);
    }

    private void Record(AudioBlockMeasure measure)
    {
        if (!State.IsRecording)
        {
            return;
        }
        _capturedBlocks++;
        if (_capturedBlocks == 1 && _active is { } active)
        {
            // a bluetooth headset switching into its headset mode shows up here as a long wait
            Log.Info(Category, $"first audio after {Stopwatch.GetElapsedTime(active.StartedAt).TotalMilliseconds:F0} ms");
        }
        if (measure.IsClipped)
        {
            _clippedBlocks++;
        }
        _pendingLevel = Math.Max(_pendingLevel, measure.Level);
        if (Stopwatch.GetElapsedTime(_lastBarAt) < AppConstants.Overlay.VoiceBarStep)
        {
            return;
        }
        _lastBarAt = Stopwatch.GetTimestamp();
        Array.Copy(_levels, 1, _levels, 0, _levels.Length - 1);
        _levels[^1] = _pendingLevel;
        _pendingLevel = 0;
        LevelsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetState(DictationState state)
    {
        if (state == State)
        {
            return;
        }
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The pieces of the one dictation in progress, created and torn down together.</summary>
    private sealed record ActiveDictation(DeepgramSession Deepgram, CleanupSession Session, Task Forwarding, long StartedAt);

    private sealed record CachedCleaner(string ApiKey, CleanupOptions Options, ClaudeCleaner Cleaner);
}

/// <summary>A finished dictation, for the history.</summary>
public sealed class DeliveredEventArgs(DictationTranscript transcript, double speakingSeconds, string? appName) : EventArgs
{
    public DictationTranscript Transcript { get; } = transcript;
    public double SpeakingSeconds { get; } = speakingSeconds;
    /// <summary>The app the text was delivered to.</summary>
    public string? AppName { get; } = appName;
}
