namespace Murmur.Core;

/// <summary>Messages the app shows outside of errors (tray balloons). Errors live in <see cref="DictationError"/>.</summary>
public static class UserMessages
{
    public const string CopiedTitle = "Copied to clipboard";
    public const string CopiedBody = "No text field was focused, so your dictation is on the clipboard. Paste it with Ctrl+V.";
    public const string HookFailedTitle = "Dictation shortcut unavailable";
    public const string HookFailedBody = "Murmur couldn't watch the keyboard. Copy diagnostics from the tray menu and send them over.";
    public const string DiagnosticsCopiedTitle = "Diagnostics copied";
    public const string DiagnosticsCopiedBody = "Paste them into the chat.";
    public const string UpToDateTitle = "Murmur is up to date";
    public const string UpdateUnreachableTitle = "Couldn't check for updates";
    public const string UpdateUnreachableBody = "GitHub didn't answer. Try again later.";
    public const string UpdateAvailableTitle = "Update Murmur";
    public const string UpdateDownloadingTitle = "Downloading the update";
    public const string UpdateDownloadingBody = "Murmur restarts by itself when it's done.";
    public const string UpdateFailedTitle = "The update didn't work";
    public const string UpdatedTitle = "Murmur updated";
    public const string UpdatedBody = "Your keys, settings and history are unchanged.";
    public const string UpdateFailedBody = "Murmur is unchanged. Copy diagnostics from the tray and send them over.";
}
