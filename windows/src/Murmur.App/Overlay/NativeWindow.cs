using System.Runtime.InteropServices;

namespace Murmur.App.Overlay;

/// <summary>The extended window styles WPF has no property for.</summary>
internal static partial class NativeWindow
{
    public const nint NoActivate = 0x08000000;
    public const nint Transparent = 0x00000020;
    public const nint ToolWindow = 0x00000080;
    private const int ExtendedStyleIndex = -20;

    public static nint GetExtendedStyle(nint window) => GetWindowLongPtrW(window, ExtendedStyleIndex);

    public static void SetExtendedStyle(nint window, nint style) => SetWindowLongPtrW(window, ExtendedStyleIndex, style);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindowLongPtrW(nint window, int index);

    [LibraryImport("user32.dll")]
    private static partial nint SetWindowLongPtrW(nint window, int index, nint value);
}
