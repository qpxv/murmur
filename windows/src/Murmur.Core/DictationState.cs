namespace Murmur.Core;

/// <summary>How the current recording ends: on hotkey release, or on the next hotkey press.</summary>
public enum RecordingMode
{
    PushToTalk,
    Locked,
}

/// <summary>The one state the whole dictation pipeline and its UI are in.</summary>
public abstract record DictationState
{
    private DictationState()
    {
    }

    public static readonly DictationState Idle = new IdleState();
    public static readonly DictationState Finishing = new FinishingState();

    public static DictationState Recording(RecordingMode mode) => new RecordingState(mode);

    public static DictationState Failed(DictationError error) => new FailedState(error);

    public sealed record IdleState : DictationState;

    public sealed record RecordingState(RecordingMode Mode) : DictationState;

    public sealed record FinishingState : DictationState;

    public sealed record FailedState(DictationError Error) : DictationState;

    public bool IsRecording => this is RecordingState;

    /// <summary>
    /// The state after a hotkey action. Actions that make no sense in the current state
    /// (a stop while idle, a lock while finishing) change nothing.
    /// </summary>
    public DictationState Applying(HotkeyAction action) => (this, action) switch
    {
        (IdleState or FailedState, HotkeyAction.Start) => Recording(RecordingMode.PushToTalk),
        (RecordingState, HotkeyAction.Lock) => Recording(RecordingMode.Locked),
        (RecordingState, HotkeyAction.Stop) => Finishing,
        (RecordingState, HotkeyAction.Cancel) => Idle,
        _ => this,
    };
}
