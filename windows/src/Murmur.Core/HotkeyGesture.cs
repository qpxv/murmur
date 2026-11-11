namespace Murmur.Core;

/// <summary>What the dictation pipeline should do in response to a hotkey event.</summary>
public enum HotkeyAction
{
    None,
    Start,
    Lock,
    Stop,
    /// <summary>The press was too short to be meant, or the hotkey was used as a modifier: throw the recording away.</summary>
    Cancel,
}

/// <summary>
/// Turns raw hotkey events into dictation actions (same rules as the Mac's fn key):
/// holding the hotkey records until it is released; space while held locks recording on and the
/// next hotkey press stops it; an unlocked press shorter than the minimum hold cancels.
/// Timestamps come from any monotonic clock.
/// </summary>
public sealed class HotkeyGesture(TimeSpan minimumHold)
{
    private enum Phase
    {
        Idle,
        Holding,
        // locked while the hotkey is still down; its release must not stop anything
        LockedHeld,
        Locked,
        // the press that stopped a locked recording; its release is swallowed
        AwaitingRelease,
    }

    private Phase _phase = Phase.Idle;
    private TimeSpan _holdingSince;

    public HotkeyGesture()
        : this(AppConstants.Hotkey.MinimumHold)
    {
    }

    public HotkeyAction HotkeyDown(TimeSpan time)
    {
        switch (_phase)
        {
            case Phase.Idle:
                _phase = Phase.Holding;
                _holdingSince = time;
                return HotkeyAction.Start;
            case Phase.Locked:
                _phase = Phase.AwaitingRelease;
                return HotkeyAction.Stop;
            default:
                // key repeat or a duplicate event while already down
                return HotkeyAction.None;
        }
    }

    public HotkeyAction HotkeyUp(TimeSpan time)
    {
        switch (_phase)
        {
            case Phase.Holding:
                _phase = Phase.Idle;
                return time - _holdingSince < minimumHold ? HotkeyAction.Cancel : HotkeyAction.Stop;
            case Phase.LockedHeld:
                _phase = Phase.Locked;
                return HotkeyAction.None;
            case Phase.AwaitingRelease:
                _phase = Phase.Idle;
                return HotkeyAction.None;
            default:
                return HotkeyAction.None;
        }
    }

    /// <summary>
    /// Space pressed. Only meaningful while the hotkey is held; the caller swallows the key
    /// whenever this returns something other than <see cref="HotkeyAction.None"/>, so no space is typed.
    /// </summary>
    public HotkeyAction SpaceDown()
    {
        if (_phase != Phase.Holding)
        {
            return HotkeyAction.None;
        }
        _phase = Phase.LockedHeld;
        return HotkeyAction.Lock;
    }

    /// <summary>
    /// Any other key pressed while the hotkey is held means it is being used as a modifier,
    /// not for dictation: cancel instead of pasting.
    /// </summary>
    public HotkeyAction OtherKeyDown()
    {
        if (_phase != Phase.Holding)
        {
            return HotkeyAction.None;
        }
        _phase = Phase.Idle;
        return HotkeyAction.Cancel;
    }

    /// <summary>Forgets any press in progress, e.g. after the pipeline failed or was cancelled.</summary>
    public void Reset() => _phase = Phase.Idle;
}
