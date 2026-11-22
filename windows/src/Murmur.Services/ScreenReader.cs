using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// Reads the text of the foreground window through UI Automation, for <see cref="ScreenContext"/>.
/// Never logs what it read, only how much and how long it took. Chromium apps build their tree
/// only once asked (<see cref="TextInserter"/> does after the first paste there), so the very first
/// dictation into a freshly started one gets no screen text.
/// </summary>
public static partial class ScreenReader
{
    private const string Category = "screen";

    /// <summary>
    /// What the foreground window shows. Gives up after <see cref="AppConstants.Screen.ReadTimeout"/>
    /// with whatever it has by then (a hung app answers late).
    /// </summary>
    public static async Task<ScreenContext> ContextAsync(IReadOnlyList<string> vocabulary, DictationLanguage language)
    {
        var started = Stopwatch.GetTimestamp();
        var walk = new Walk();
        // ui automation runs off the ui thread: asking our own windows from it would deadlock
        var reading = Task.Run(() => Read(walk));
        await Task.WhenAny(reading, Task.Delay(AppConstants.Screen.ReadTimeout));
        var lines = walk.Stop();
        var screen = ScreenContext.From(lines, vocabulary, language);
        Log.Info(
            Category,
            $"read {lines.Count} texts in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms: "
            + $"{screen.Text.Length} characters, {screen.Terms.Count} terms");
        return screen;
    }

    private static void Read(Walk walk)
    {
        var window = NativeMethods.GetForegroundWindow();
        _ = NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (window == 0 || processId == Environment.ProcessId)
        {
            return;
        }
        var cache = new CacheRequest { TreeScope = TreeScope.Element };
        cache.Add(AutomationElement.NameProperty);
        cache.Add(AutomationElement.IsPasswordProperty);
        cache.Add(ValuePattern.ValueProperty);
        try
        {
            using (cache.Activate())
            {
                Visit(AutomationElement.FromHandle(window), cache, walk);
            }
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            Log.Error(Category, "reading the window failed", exception);
        }
    }

    private static void Visit(AutomationElement root, CacheRequest cache, Walk walk)
    {
        var walker = TreeWalker.ControlViewWalker;
        var pending = new Stack<AutomationElement>([root]);
        var visited = 0;
        while (pending.TryPop(out var element) && visited < AppConstants.Screen.MaxElements && !walk.IsStopped)
        {
            visited++;
            // a password field and everything in it stays unread
            if (element.Cached.IsPassword)
            {
                continue;
            }
            walk.Add(element.Cached.Name);
            if (element.GetCachedPropertyValue(ValuePattern.ValueProperty, ignoreDefaultValue: true) is string value)
            {
                walk.Add(value);
            }
            var children = new List<AutomationElement>();
            for (var child = walker.GetFirstChild(element, cache); child is not null; child = walker.GetNextSibling(child, cache))
            {
                children.Add(child);
            }
            // reversed onto the stack, so the walk stays top to bottom
            children.Reverse();
            children.ForEach(pending.Push);
        }
    }

    /// <summary>
    /// The texts one read has found so far, shared between the reading thread and the caller, who
    /// takes them when the read is done or the time is up, whichever comes first.
    /// </summary>
    private sealed class Walk
    {
        private readonly Lock _gate = new();
        private readonly List<string> _lines = [];
        private bool _isStopped;

        public bool IsStopped
        {
            get
            {
                lock (_gate)
                {
                    return _isStopped;
                }
            }
        }

        public void Add(string text)
        {
            lock (_gate)
            {
                // a label and its value often repeat the same text
                if (_isStopped || text.Length == 0 || (_lines.Count > 0 && _lines[^1] == text))
                {
                    return;
                }
                _lines.Add(text);
            }
        }

        public IReadOnlyList<string> Stop()
        {
            lock (_gate)
            {
                _isStopped = true;
                return [.. _lines];
            }
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll")]
        public static partial nint GetForegroundWindow();

        [LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(nint window, out uint processId);
    }
}
