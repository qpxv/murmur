using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Murmur.Services;

/// <summary>
/// Watches Caps Lock system-wide through a low-level keyboard hook. Caps Lock itself is always
/// swallowed, so it never toggles (Shift still types capitals). Space and other keys are only
/// reported while Caps Lock is held, so normal typing is untouched. Runs on the thread that
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
    private const uint CapsLockKey = 0x14;
    private const uint SpaceKey = 0x20;
    private const nint Swallow = 1;

    private readonly Handler _handler;
    // kept in a field: the hook only holds a raw pointer, the gc must not collect the delegate
    private readonly HookProc _callback;
    private nint _hook;
    private bool _isCapsDown;
    private bool _isSwallowingSpace;

    public KeyboardHook(Handler handler)
    {
        _handler = handler;
        _callback = Callback;
    }

    public bool IsActive => _hook != 0;

    /// <summary>Installs the hook. Returns <c>false</c> when Windows refused it (logged).</summary>
    public bool Start()
    {
        if (IsActive)
        {
            return true;
        }
        SyntheticKeys.TurnOffCapsLock();
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
            if (!isInjected && Handle(info.VirtualKey, message))
            {
                return Swallow;
            }
        }
        return NativeMethods.CallNextHookEx(0, code, message, data);
    }

    private bool Handle(uint key, nint message)
    {
        var isDown = message is KeyDownMessage or SystemKeyDownMessage;
        var isUp = message is KeyUpMessage or SystemKeyUpMessage;
        var time = Stopwatch.GetElapsedTime(0);
        if (key == CapsLockKey)
        {
            // windows repeats a held key; only the first down and the final up mean anything
            if (isDown && !_isCapsDown)
            {
                _isCapsDown = true;
                _handler(KeyEvent.HotkeyDown, time);
            }
            else if (isUp && _isCapsDown)
            {
                _isCapsDown = false;
                _handler(KeyEvent.HotkeyUp, time);
            }
            return true;
        }
        if (key == SpaceKey && isDown && _isCapsDown)
        {
            _isSwallowingSpace = _handler(KeyEvent.Space, time);
            return _isSwallowingSpace;
        }
        if (key == SpaceKey && isUp && _isSwallowingSpace)
        {
            _isSwallowingSpace = false;
            return true;
        }
        if (isDown && _isCapsDown)
        {
            _handler(KeyEvent.OtherKey, time);
        }
        return false;
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
