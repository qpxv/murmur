using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>Delivers finished text to wherever the user is right now.</summary>
public static partial class TextInserter
{
    public enum Outcome
    {
        Pasted,
        CopiedToClipboard,
    }

    private const string Category = "insert";

    /// <summary>
    /// Pastes into the focused text input and restores the previous clipboard afterwards. With no
    /// text input focused, or when the paste visibly didn't land, the text is left on the clipboard
    /// instead, so it is never lost. Call on the UI thread (the clipboard needs it).
    /// </summary>
    public static async Task<Outcome> InsertAsync(string text)
    {
        var input = await FocusedTextInputAsync();
        if (input is null)
        {
            SetClipboard(text);
            return Outcome.CopiedToClipboard;
        }

        var saved = SaveClipboard();
        var lengthBefore = await Task.Run(() => Length(input));
        if (!SetClipboard(text))
        {
            return Outcome.CopiedToClipboard;
        }
        var ourChange = NativeMethods.GetClipboardSequenceNumber();
        SyntheticKeys.PressPaste();

        await Task.Delay(AppConstants.Paste.ClipboardRestoreDelay);
        var lengthAfter = await Task.Run(() => Length(input));
        if (!TextInputRule.DidPasteLand(lengthBefore, lengthAfter))
        {
            Log.Error(Category, $"paste didn't land: field length stayed {lengthAfter}, leaving text on the clipboard");
            return Outcome.CopiedToClipboard;
        }
        // only restore if nothing else touched the clipboard in the meantime
        if (NativeMethods.GetClipboardSequenceNumber() == ourChange && saved is not null)
        {
            Retry(() => Clipboard.SetDataObject(saved, copy: true));
        }
        return Outcome.Pasted;
    }

    /// <summary>The name of the app the user is in, for the cleanup prompt and the history.</summary>
    public static string? ForegroundAppName()
    {
        try
        {
            var window = NativeMethods.GetForegroundWindow();
            _ = NativeMethods.GetWindowThreadProcessId(window, out var processId);
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.MainModule?.FileVersionInfo.FileDescription is { Length: > 0 } description ? description : process.ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Chromium apps (Chrome, Edge, Teams and other WebView2 apps) build their accessibility tree
    /// only once a client asks for it, so the first look at a freshly started one finds no text
    /// input. Asking is what starts the build; a moment later the focused field is there.
    /// </summary>
    private static async Task<AutomationElement?> FocusedTextInputAsync()
    {
        // ui automation runs off the ui thread: asking our own windows from it would deadlock
        if (await Task.Run(FocusedTextInput) is { } input)
        {
            return input;
        }
        await Task.Delay(AppConstants.Paste.AccessibilityWarmupDelay);
        return await Task.Run(FocusedTextInput);
    }

    private static AutomationElement? FocusedTextInput()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is null)
            {
                Log.Info(Category, "nothing focused");
                return null;
            }
            var controlType = element.Current.ControlType;
            var kind = controlType == ControlType.Edit ? TextInputRule.ControlKind.Edit
                : controlType == ControlType.Document ? TextInputRule.ControlKind.Document
                : TextInputRule.ControlKind.Other;
            var isValueReadOnly = element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) && value is ValuePattern valuePattern
                ? valuePattern.Current.IsReadOnly
                : (bool?)null;
            var hasTextPattern = element.TryGetCurrentPattern(TextPattern.Pattern, out _);
            var isInput = TextInputRule.IsTextInput(kind, isValueReadOnly, hasTextPattern);
            // class and framework only, never the element's text: enough to see why a paste went to the clipboard
            Log.Info(
                Category,
                $"focused {controlType.ProgrammaticName} class={element.Current.ClassName} framework={element.Current.FrameworkId} "
                + $"readonly={isValueReadOnly?.ToString() ?? "-"} text={hasTextPattern} -> {(isInput ? "paste" : "clipboard")}");
            return isInput ? element : null;
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            Log.Error(Category, "reading the focused element failed", exception);
            return null;
        }
    }

    // only the length is read, the text itself is dropped right away
    private static int? Length(AutomationElement element)
    {
        try
        {
            return element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) && value is ValuePattern valuePattern
                ? valuePattern.Current.Value.Length
                : null;
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Puts text on the clipboard, retrying while another app holds it. False if it stayed busy.</summary>
    public static bool Copy(string text) => SetClipboard(text);

    private static bool SetClipboard(string text) => Retry(() => Clipboard.SetDataObject(text, copy: true));

    // a copy of every format that can be read now, so delayed-rendered data doesn't vanish with its app
    private static DataObject? SaveClipboard()
    {
        try
        {
            var current = Clipboard.GetDataObject();
            if (current is null)
            {
                return null;
            }
            var copy = new DataObject();
            foreach (var format in current.GetFormats(autoConvert: false))
            {
                try
                {
                    if (current.GetData(format, autoConvert: false) is { } data)
                    {
                        copy.SetData(format, data, autoConvert: false);
                    }
                }
                catch (COMException)
                {
                    // some formats can't be read back (owner gone, private formats); skip them
                }
            }
            return copy;
        }
        catch (COMException exception)
        {
            Log.Error(Category, "saving the clipboard failed, it won't be restored", exception);
            return null;
        }
    }

    // another app can hold the clipboard open for a moment
    private static bool Retry(Action clipboardAction)
    {
        for (var attempt = 1; attempt <= AppConstants.Paste.ClipboardAttempts; attempt++)
        {
            try
            {
                clipboardAction();
                return true;
            }
            catch (COMException exception) when (attempt < AppConstants.Paste.ClipboardAttempts)
            {
                Log.Info(Category, $"clipboard busy (0x{exception.HResult:X8}), retrying");
                Thread.Sleep(AppConstants.Paste.ClipboardRetryDelay);
            }
            catch (COMException exception)
            {
                Log.Error(Category, "the clipboard stayed busy", exception);
            }
        }
        return false;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll")]
        public static partial uint GetClipboardSequenceNumber();

        [LibraryImport("user32.dll")]
        public static partial nint GetForegroundWindow();

        [LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(nint window, out uint processId);
    }
}
