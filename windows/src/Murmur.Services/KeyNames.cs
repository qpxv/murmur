using System.Globalization;
using System.Runtime.InteropServices;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>What a key is called on screen, in the keyboard layout's own words ("Right Ctrl", "Alt Gr").</summary>
public static partial class KeyNames
{
    private const uint VirtualKeyToExtendedScanCode = 4;
    private const uint ExtendedPrefix = 0xE000;
    private const int ScanCodeShift = 16;
    private const int ExtendedBit = 1 << 24;
    private const uint ScanCodeMask = 0xFF;
    private const int MaxNameLength = 64;

    public static string Of(Hotkey hotkey)
    {
        var scanCode = NativeMethods.MapVirtualKeyW(hotkey.VirtualKey, VirtualKeyToExtendedScanCode);
        var isExtended = (scanCode & ExtendedPrefix) == ExtendedPrefix;
        var keyInfo = (int)((scanCode & ScanCodeMask) << ScanCodeShift) | (isExtended ? ExtendedBit : 0);
        var buffer = new char[MaxNameLength];
        var length = scanCode == 0 ? 0 : NativeMethods.GetKeyNameTextW(keyInfo, buffer, buffer.Length);
        return length > 0 ? Readable(new string(buffer, 0, length)) : $"Key {hotkey.VirtualKey:X2}";
    }

    // most layouts name keys in capitals ("CAPS LOCK", "RIGHT CTRL"); single characters stay as they are
    private static string Readable(string name) =>
        name.Length > 1 && !name.Any(char.IsLower)
            ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower(CultureInfo.CurrentCulture))
            : name;

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll")]
        public static partial uint MapVirtualKeyW(uint code, uint mapType);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
        public static partial int GetKeyNameTextW(int keyInfo, [Out] char[] name, int size);
    }
}
