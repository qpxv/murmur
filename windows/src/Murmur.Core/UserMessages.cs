namespace Murmur.Core;

/// <summary>Messages the app shows outside of errors (tray balloons). Errors live in <see cref="DictationError"/>.</summary>
public static class UserMessages
{
    public const string CopiedTitle = "Copied to clipboard";
    public const string CopiedBody = "No text field was focused, so your dictation is on the clipboard. Paste it with Ctrl+V.";
    public const string HookFailedTitle = "Caps Lock shortcut unavailable";
    public const string HookFailedBody = "Murmur couldn't watch the keyboard. Copy diagnostics from the tray menu and send them over.";
    public const string DiagnosticsCopiedTitle = "Diagnostics copied";
    public const string DiagnosticsCopiedBody = "Paste them into the chat.";
}
