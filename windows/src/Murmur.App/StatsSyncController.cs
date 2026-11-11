using Murmur.Core;
using Murmur.Services;

namespace Murmur.App;

/// <summary>Where Home's numbers come from right now.</summary>
public abstract record SyncStatus
{
    private SyncStatus()
    {
    }

    public static readonly SyncStatus NotConfigured = new NotConfiguredStatus();

    public sealed record NotConfiguredStatus : SyncStatus;

    public sealed record SyncedStatus(DateTimeOffset At) : SyncStatus;

    public sealed record FailedStatus(SyncFailure Failure) : SyncStatus;
}

/// <summary>
/// Keeps the dashboard's numbers shared across devices through the user's Neon database.
/// Event-driven only: uploads after each dictation and at launch, fetches when Home opens.
/// Without a database URL it does nothing and Home shows this PC's numbers. UI thread only.
/// </summary>
public sealed class StatsSyncController(HistoryStore history, SettingsStore settings)
{
    private bool _isUploading;

    public SyncStatus Status { get; private set; } = SyncStatus.NotConfigured;

    /// <summary>Every device's dictations from the last successful fetch; <c>null</c> before one.</summary>
    public IReadOnlyList<DictationSummary>? SharedSummaries { get; private set; }

    /// <summary>Raised whenever <see cref="Status"/> or <see cref="SharedSummaries"/> changes.</summary>
    public event EventHandler? Changed;

    /// <summary>The name this device's dictations carry in the shared table.</summary>
    public string DeviceName => settings.Current.SyncDeviceName.Trim() is { Length: > 0 } chosen ? chosen : Environment.MachineName;

    /// <summary>
    /// The numbers Home should show: everyone's when synced (plus this PC's not uploaded yet),
    /// otherwise this PC's own.
    /// </summary>
    public IReadOnlyList<DictationSummary> Summaries() =>
        SharedSummaries is { } shared ? [.. shared, .. history.Current.UnsyncedSummaries] : history.Current.Summaries;

    /// <summary>
    /// Uploads this PC's dictations the database doesn't have yet. Failures just leave them
    /// pending for the next attempt; dictation itself never waits on this.
    /// </summary>
    public async Task UploadPendingAsync()
    {
        if (_isUploading || MakeSync() is not { } sync)
        {
            return;
        }
        var rows = history.PendingSyncRows(DeviceName);
        if (rows.Count == 0)
        {
            return;
        }
        _isUploading = true;
        try
        {
            if (await sync.UploadAsync(rows) is { } failure)
            {
                SetStatus(new SyncStatus.FailedStatus(failure));
                return;
            }
            history.MarkSynced(rows.Select(row => row.Id).ToHashSet());
            // keep the shown totals right until the next fetch
            if (SharedSummaries is { } shared)
            {
                SharedSummaries = [.. shared, .. rows.Select(row => new DictationSummary(row.Date, row.WordCount, row.SpeakingSeconds))];
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _isUploading = false;
        }
    }

    /// <summary>Uploads what's pending, then loads every device's numbers.</summary>
    public async Task RefreshAsync()
    {
        if (MakeSync() is not { } sync)
        {
            SharedSummaries = null;
            SetStatus(SyncStatus.NotConfigured);
            return;
        }
        await UploadPendingAsync();
        var (summaries, failure) = await sync.FetchSummariesAsync();
        if (summaries is null)
        {
            SetStatus(new SyncStatus.FailedStatus(failure ?? SyncFailure.UnreadableResponse));
            return;
        }
        SharedSummaries = summaries;
        SetStatus(new SyncStatus.SyncedStatus(DateTimeOffset.Now));
    }

    /// <summary>Checks the database URL (creating the table if needed), then syncs.</summary>
    public async Task TestConnectionAsync()
    {
        if (MakeSync() is not { } sync)
        {
            SetStatus(SyncStatus.NotConfigured);
            return;
        }
        if (await sync.UploadAsync([]) is { } failure)
        {
            SetStatus(new SyncStatus.FailedStatus(failure));
            return;
        }
        await RefreshAsync();
    }

    private static StatsSync? MakeSync() =>
        CredentialStore.Read(AppConstants.Credentials.NeonTarget) is { } url ? new StatsSync(url) : null;

    private void SetStatus(SyncStatus status)
    {
        Status = status;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
