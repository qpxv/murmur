using System.Runtime.InteropServices;

namespace Murmur.Services;

/// <summary>Key presses Murmur sends itself (marked as injected, so its own hook lets them through).</summary>
public static partial class SyntheticKeys
{
    private const string Category = "keys";
    private const uint KeyboardInputType = 1;
    private const uint KeyUpFlag = 0x0002;
    private const ushort ControlKey = 0x11;
    private const ushort VKey = 0x56;
    private const ushort CapsLockKey = 0x14;
    private const int ToggledBit = 1;

    /// <summary>
    /// Caps Lock can't be switched by the user while Murmur swallows it, so if it was on when
    /// Murmur started, turn it off once (otherwise everything would be typed in capitals).
    /// </summary>
    public static void TurnOffCapsLock()
    {
        if ((NativeMethods.GetKeyState(CapsLockKey) & ToggledBit) == 0)
        {
            return;
        }
        Send([Key(CapsLockKey, isUp: false), Key(CapsLockKey, isUp: true)]);
        Log.Info(Category, "caps lock was on, turned it off");
    }

    public static void PressPaste() =>
        Send([Key(ControlKey, isUp: false), Key(VKey, isUp: false), Key(VKey, isUp: true), Key(ControlKey, isUp: true)]);

    private static Input Key(ushort key, bool isUp) => new()
    {
        Type = KeyboardInputType,
        Keyboard = new KeyboardInput { VirtualKey = key, Flags = isUp ? KeyUpFlag : 0 },
    };

    private static void Send(Input[] inputs)
    {
        unsafe
        {
            fixed (Input* first = inputs)
            {
                var sent = NativeMethods.SendInput((uint)inputs.Length, first, sizeof(Input));
                if (sent != inputs.Length)
                {
                    // usually uipi: the focused app runs elevated (as admin) and murmur doesn't
                    Log.Error(Category, $"sent {sent} of {inputs.Length} keys, win32 error {Marshal.GetLastPInvokeError()}");
                }
            }
        }
    }

    // the input union is as large as its biggest member (mouse input), so it is declared too
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;

        public KeyboardInput Keyboard
        {
            readonly get => Union.Keyboard;
            set => Union.Keyboard = value;
        }
    }

    private static unsafe partial class NativeMethods
    {
        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial uint SendInput(uint count, Input* inputs, int size);

        [LibraryImport("user32.dll")]
        public static partial short GetKeyState(int key);
    }
}
