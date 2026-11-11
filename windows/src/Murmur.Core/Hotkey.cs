namespace Murmur.Core;

/// <summary>
/// The one key that dictates: any single key the user recorded, Caps Lock by default. Stored as the
/// Windows virtual key the keyboard hook reports (left and right Ctrl/Alt/Shift are different keys).
/// While Murmur runs the key does nothing else.
/// </summary>
public sealed record Hotkey(uint VirtualKey)
{
    public static Hotkey CapsLock { get; } = new(VirtualKeys.CapsLock);

    /// <summary>Shift, Ctrl, Alt or Windows: these count only on release while recording a key.</summary>
    public bool IsModifier => VirtualKey is >= VirtualKeys.LeftShift and <= VirtualKeys.RightAlt
        or VirtualKeys.LeftWindows or VirtualKeys.RightWindows;
}

/// <summary>The Windows virtual key codes Murmur needs by name (winuser.h's VK_*).</summary>
public static class VirtualKeys
{
    public const uint CapsLock = 0x14;
    public const uint Escape = 0x1B;
    public const uint Space = 0x20;
    public const uint LeftWindows = 0x5B;
    public const uint RightWindows = 0x5C;
    public const uint LeftShift = 0xA0;
    public const uint LeftControl = 0xA2;
    public const uint RightAlt = 0xA5;
}

/// <summary>Why a pressed key can't become the dictation key.</summary>
public enum HotkeyRejection
{
    /// <summary>A modifier held together with another key (Ctrl + C): only single keys are allowed.</summary>
    Combination,
    /// <summary>Space locks a recording, so it can't also start one.</summary>
    Space,
}

/// <summary>Where recording a new dictation key stands after a key event.</summary>
public abstract record HotkeyRecording
{
    public static HotkeyRecording Waiting { get; } = new WaitingState();
    public static HotkeyRecording Cancelled { get; } = new CancelledState();

    public static HotkeyRecording Recorded(Hotkey hotkey) => new RecordedState(hotkey);

    public static HotkeyRecording Rejected(HotkeyRejection reason) => new RejectedState(reason);

    public sealed record WaitingState : HotkeyRecording;

    public sealed record CancelledState : HotkeyRecording;

    public sealed record RecordedState(Hotkey Hotkey) : HotkeyRecording;

    public sealed record RejectedState(HotkeyRejection Reason) : HotkeyRecording;
}

public static class HotkeyRejectionInfo
{
    public static string Message(this HotkeyRejection rejection) => rejection switch
    {
        HotkeyRejection.Combination => "Just one key, no combinations.",
        HotkeyRejection.Space => "Space locks a recording, pick another key.",
        _ => throw new ArgumentOutOfRangeException(nameof(rejection), rejection, null),
    };
}

/// <summary>
/// Decides which single key the user pressed while "Record…" is waiting (same rules as the Mac).
/// A normal key counts on key down; a modifier only on its release, so Ctrl + C is caught as a
/// combination instead of recording Ctrl. Escape cancels.
/// </summary>
public sealed class HotkeyRecorder
{
    // a modifier that went down and may still become the key, if nothing else is pressed before it's released
    private uint? _pendingModifier;

    /// <summary>A key went down (Windows repeats a held key, so the pending modifier may come again).</summary>
    public HotkeyRecording KeyDown(uint virtualKey)
    {
        if (_pendingModifier == virtualKey)
        {
            return HotkeyRecording.Waiting;
        }
        if (_pendingModifier is not null)
        {
            return HotkeyRecording.Rejected(HotkeyRejection.Combination);
        }
        var hotkey = new Hotkey(virtualKey);
        if (hotkey.IsModifier)
        {
            _pendingModifier = virtualKey;
            return HotkeyRecording.Waiting;
        }
        return virtualKey switch
        {
            VirtualKeys.Escape => HotkeyRecording.Cancelled,
            VirtualKeys.Space => HotkeyRecording.Rejected(HotkeyRejection.Space),
            _ => HotkeyRecording.Recorded(hotkey),
        };
    }

    /// <summary>A key went up. Only the pending modifier's release matters.</summary>
    public HotkeyRecording KeyUp(uint virtualKey) =>
        _pendingModifier == virtualKey ? HotkeyRecording.Recorded(new Hotkey(virtualKey)) : HotkeyRecording.Waiting;
}
