using System.Text.Json;

namespace Murmur.Core;

/// <summary>
/// The local dictation history as an immutable value, newest first. Every change returns a new
/// history; the store in Services persists it.
/// </summary>
public sealed class History
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public History(IEnumerable<DictationRecord> records)
    {
        Records = [.. records.OrderByDescending(record => record.Date)];
    }

    public static History Empty { get; } = new([]);

    /// <summary>Newest first.</summary>
    public IReadOnlyList<DictationRecord> Records { get; }

    public History Adding(DictationRecord record) => new([record, .. Records]);

    /// <summary>
    /// Dictations not uploaded to the shared stats yet. Each gets its sync id the first time and
    /// keeps it, so a retried upload is recognised as the same dictation.
    /// </summary>
    public (History History, IReadOnlyList<NeonSql.Row> Rows) PreparingSync(string device, Func<Guid> newId)
    {
        var updated = Records
            .Select(record => record.IsSynced || record.SyncId is not null ? record : record with { SyncId = newId() })
            .ToList();
        var rows = updated
            .Where(record => !record.IsSynced && record.SyncId is not null)
            .Select(record => new NeonSql.Row(record.SyncId ?? Guid.Empty, device, record.Date, record.WordCount, record.SpeakingSeconds))
            .ToList();
        return (new History(updated), rows);
    }

    public History MarkingSynced(IReadOnlySet<Guid> syncIds) =>
        new(Records.Select(record => record.SyncId is { } id && syncIds.Contains(id) ? record with { IsSynced = true } : record));

    /// <summary>Local dictations the shared stats don't contain yet, so Home can add them on top.</summary>
    public IReadOnlyList<DictationSummary> UnsyncedSummaries =>
        [.. Records.Where(record => !record.IsSynced).Select(record => record.Summary)];

    public IReadOnlyList<DictationSummary> Summaries => [.. Records.Select(record => record.Summary)];

    public string Encode() => JsonSerializer.Serialize(Records, Options);

    /// <summary>The saved history; <c>null</c> when the text isn't a history file at all.</summary>
    public static History? Decode(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<DictationRecord>>(json, Options) is { } records ? new History(records) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
