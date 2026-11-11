using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// The local dictation history in %LOCALAPPDATA%\Murmur\history.json. A file that can't be read
/// is moved aside (never overwritten) and the app carries on with an empty history: dictation must
/// never break because of it. Use from the UI thread.
/// </summary>
public sealed class HistoryStore
{
    private const string Category = "history";

    public HistoryStore()
    {
        Current = Load();
    }

    public History Current { get; private set; }

    /// <summary>Raised on the UI thread after every change.</summary>
    public event EventHandler? Changed;

    public void Record(DictationTranscript transcript, double speakingSeconds, string? appName, bool keepsText) =>
        Replace(Current.Adding(DictationRecord.From(transcript, speakingSeconds, appName, keepsText, DateTimeOffset.Now)));

    /// <summary>The rows to upload, with sync ids given and saved first.</summary>
    public IReadOnlyList<NeonSql.Row> PendingSyncRows(string device)
    {
        var (prepared, rows) = Current.PreparingSync(device, Guid.NewGuid);
        if (!prepared.Records.SequenceEqual(Current.Records))
        {
            Replace(prepared);
        }
        return rows;
    }

    public void MarkSynced(IReadOnlySet<Guid> syncIds) => Replace(Current.MarkingSynced(syncIds));

    /// <summary>Deletes every dictation on this PC. Stats already synced to the database stay there.</summary>
    public void Clear() => Replace(History.Empty);

    private void Replace(History history)
    {
        Current = history;
        try
        {
            AppPaths.WriteAtomically(AppPaths.History, history.Encode());
        }
        catch (IOException exception)
        {
            Log.Error(Category, "saving history failed", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            Log.Error(Category, "saving history failed", exception);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static History Load()
    {
        try
        {
            if (!File.Exists(AppPaths.History))
            {
                return History.Empty;
            }
            if (History.Decode(File.ReadAllText(AppPaths.History)) is { } history)
            {
                return history;
            }
            var aside = AppPaths.History + $".unreadable-{DateTimeOffset.Now:yyyyMMddHHmmss}";
            File.Move(AppPaths.History, aside);
            Log.Error(Category, $"history.json is unreadable, moved it to {Path.GetFileName(aside)} and started fresh");
        }
        catch (IOException exception)
        {
            Log.Error(Category, "reading history failed, keeping it in memory only", exception);
        }
        return History.Empty;
    }
}
