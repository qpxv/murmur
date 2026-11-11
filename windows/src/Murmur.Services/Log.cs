using System.Globalization;
using System.Text;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// The app's own log file (%LOCALAPPDATA%\Murmur\murmur.log), so a problem on a machine Claude
/// can't reach can be pasted into chat. Timings, errors and counts only: never transcripts or keys.
/// </summary>
public static class Log
{
    private static readonly Lock Gate = new();

    public static void Info(string category, string message) => Write("info", category, message);

    public static void Error(string category, string message) => Write("error", category, message);

    /// <summary>An exception's type and message (and stack, for crashes). Exception messages never carry user text here.</summary>
    public static void Error(string category, string message, Exception exception) =>
        Write("error", category, $"{message}: {exception.GetType().Name}: {exception.Message}");

    public static void Crash(string category, Exception exception) =>
        Write("crash", category, exception.ToString());

    /// <summary>The newest lines of the log, oldest first.</summary>
    public static IReadOnlyList<string> LastLines(int count)
    {
        lock (Gate)
        {
            try
            {
                return File.Exists(AppPaths.Log) ? [.. File.ReadLines(AppPaths.Log).TakeLast(count)] : [];
            }
            catch (IOException exception)
            {
                return [$"(log unreadable: {exception.Message})"];
            }
        }
    }

    private static void Write(string level, string category, string message)
    {
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {level} [{category}] {message}{Environment.NewLine}");
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.Folder);
                TrimIfLarge();
                File.AppendAllText(AppPaths.Log, line, Encoding.UTF8);
            }
            catch (IOException)
            {
                // logging must never take the app down; there's nowhere left to report this
            }
            catch (UnauthorizedAccessException)
            {
                // same: a locked-down profile folder just means no log
            }
        }
    }

    // keeps the newest half once the file is too big, so it never grows without bound
    private static void TrimIfLarge()
    {
        var file = new FileInfo(AppPaths.Log);
        if (!file.Exists || file.Length < AppConstants.Storage.MaxLogBytes)
        {
            return;
        }
        var lines = File.ReadAllLines(AppPaths.Log);
        File.WriteAllLines(AppPaths.Log, lines.Skip(lines.Length / 2));
    }
}
