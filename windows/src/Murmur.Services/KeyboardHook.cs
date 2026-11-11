using System.Diagnostics;
using System.Runtime.InteropServices;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// Watches the dictation key (<see cref="Hotkey"/>) system-wide through a low-level keyboard hook.
/// The key itself is always swallowed, so it does nothing else while Murmur runs. Space and other
/// keys are only reported while it is held, so normal typing is untouched. Runs on the thread that
/// called <see cref="Start"/>, which must pump messages (the WPF UI thread does).
/// </summary>
public sealed partial class KeyboardHook : IDisposable
{
    public enum KeyEvent
    {
        HotkeyDown,
        HotkeyUp,
        Space,
        OtherKey,
    }

    /// <summary>Returns <c>true</c> to swallow the key (so e.g. the locking space is never typed).</summary>
    public delegate bool Handler(KeyEvent keyEvent, TimeSpan time);

    private const string Category = "hotkey";
    private const int LowLevelKeyboardHook = 13;
    private const nint KeyDownMessage = 0x0100;
    private const nint KeyUpMessage = 0x0101;
    private const nint SystemKeyDownMessage = 0x0104;
    private const nint SystemKeyUpMessage = 0x0105;
    private const uint InjectedFlag = 0x10;
    // altgr arrives as a made-up left ctrl with this scan code, then right alt
    private const uint AltGrControlScanCode = 0x21D;
    private const nint Swallow = 1;

    private readonly Handler _handler;
    // kept in a field: the hook only holds a raw pointer, the gc must not collect the delegate
    private readonly HookProc _callback;
    private nint _hook;
    private Hotkey _hotkey = Hotkey.CapsLock;
    private bool _isHotkeyDown;
    private bool _isSwallowingSpace;
    private (HotkeyRecorder Recorder, Action<HotkeyRecording> Completion)? _recording;

    public KeyboardHook(Handler handler)
    {
        _handler = handler;
        _callback = Callback;
    }

    public bool IsActive => _hook != 0;

    /// <summary>The key to watch. Changing it forgets a press in progress.</summary>
    public Hotkey Hotkey
    {
        get => _hotkey;
        set
        {
            _hotkey = value;
            _isHotkeyDown = false;
            _isSwallowingSpace = false;
            KeepCapsLockOff();
        }
    }

    /// <summary>Installs the hook. Returns <c>false</c> when Windows refused it (logged).</summary>
    public bool Start()
    {
        if (IsActive)
        {
            return true;
        }
        KeepCapsLockOff();
        _hook = NativeMethods.SetWindowsHookExW(
            LowLevelKeyboardHook,
            Marshal.GetFunctionPointerForDelegate(_callback),
            NativeMethods.GetModuleHandleW(null),
            0);
        if (_hook == 0)
        {
            Log.Error(Category, $"installing the keyboard hook failed with win32 error {Marshal.GetLastPInvokeError()}");
            return false;
        }
        Log.Info(Category, "keyboard hook installed");
        return true;
    }

    /// <summary>
    /// Catches the next single key (every key is swallowed meanwhile, dictation is paused) and
    /// reports it from inside the hook once it's decided: do the work after the hook returns.
    /// A recording already running is cancelled.
    /// </summary>
    public void Record(Action<HotkeyRecording> completion)
    {
        CancelRecording();
        _isHotkeyDown = false;
        _isSwallowingSpace = false;
        _recording = (new HotkeyRecorder(), completion);
    }

    public void CancelRecording()
    {
        if (_recording is not { } recording)
        {
            return;
        }
        _recording = null;
        recording.Completion(HotkeyRecording.Cancelled);
    }

    public void Dispose()
    {
        if (_hook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = 0;
        }
    }

    private nint Callback(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var info = Marshal.PtrToStructure<KeyInfo>(data);
            // our own ctrl+v and other tools' synthetic keys pass straight through
            var isInjected = (info.Flags & InjectedFlag) != 0;
            if (!isInjected && Handle(info, message))
            {
                return Swallow;
            }
        }
        return NativeMethods.CallNextHookEx(0, code, message, data);
    }

    private bool Handle(KeyInfo info, nint message)
    {
        var isDown = message is KeyDownMessage or SystemKeyDownMessage;
        var isUp = message is KeyUpMessage or SystemKeyUpMessage;
        var key = info.VirtualKey;
        if (key == VirtualKeys.LeftControl && info.ScanCode == AltGrControlScanCode)
        {
            // altgr's ctrl half: belongs to right alt, so it goes wherever right alt goes
            return _recording is not null || _hotkey.VirtualKey == VirtualKeys.RightAlt;
        }
        if (_recording is not null)
        {
            Record(key, isDown, isUp);
            return true;
        }
        var time = Stopwatch.GetElapsedTime(0);
        if (key == _hotkey.VirtualKey)
        {
            // windows repeats a held key; only the first down and the final up mean anything
            if (isDown && !_isHotkeyDown)
            {
                _isHotkeyDown = true;
                _handler(KeyEvent.HotkeyDown, time);
            }
            else if (isUp && _isHotkeyDown)
            {
                _isHotkeyDown = false;
                _handler(KeyEvent.HotkeyUp, time);
                // in case caps lock got switched while the hook couldn't see it (e.g. an admin window had focus)
                SynchronizationContext.Current?.Post(_ => KeepCapsLockOff(), null);
            }
            return true;
        }
        if (key == VirtualKeys.Space && isDown && _isHotkeyDown)
        {
            _isSwallowingSpace = _handler(KeyEvent.Space, time);
            return _isSwallowingSpace;
        }
        if (key == VirtualKeys.Space && isUp && _isSwallowingSpace)
        {
            _isSwallowingSpace = false;
            return true;
        }
        if (isDown && _isHotkeyDown)
        {
            _handler(KeyEvent.OtherKey, time);
        }
        return false;
    }

    private void Record(uint key, bool isDown, bool isUp)
    {
        if (_recording is not { } recording)
        {
            return;
        }
        var result = isDown ? recording.Recorder.KeyDown(key)
            : isUp ? recording.Recorder.KeyUp(key)
            : HotkeyRecording.Waiting;
        if (result == HotkeyRecording.Waiting)
        {
            return;
        }
        _recording = null;
        recording.Completion(result);
    }

    // caps lock can't be switched by the user while it's the dictation key, so it must never stay on
    private void KeepCapsLockOff()
    {
        if (_hotkey == Hotkey.CapsLock)
        {
            SyntheticKeys.TurnOffCapsLock();
        }
    }

    private delegate nint HookProc(int code, nint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KeyInfo
    {
        public readonly uint VirtualKey;
        public readonly uint ScanCode;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly nint ExtraInfo;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial nint SetWindowsHookExW(int hookType, nint callback, nint module, uint threadId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UnhookWindowsHookEx(nint hook);

        [LibraryImport("user32.dll")]
        public static partial nint CallNextHookEx(nint hook, int code, nint message, nint data);

        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
        public static partial nint GetModuleHandleW(string? moduleName);
    }
}
