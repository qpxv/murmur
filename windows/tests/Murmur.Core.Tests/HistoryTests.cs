namespace Murmur.Core.Tests;

public class HistoryTests
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(1));

    private static DictationRecord Record(int minutes, int words = 5, Guid? syncId = null, bool isSynced = false) => new()
    {
        Id = Guid.NewGuid(),
        Date = Monday.AddMinutes(minutes),
        Text = "open Claude Code",
        RawText = "open clock code",
        WordCount = words,
        SpeakingSeconds = 2.5,
        SyncId = syncId,
        IsSynced = isSynced,
    };

    [Fact]
    public void Records_are_kept_newest_first()
    {
        var history = History.Empty.Adding(Record(1)).Adding(Record(3)).Adding(Record(2));
        Assert.Equal([3, 2, 1], history.Records.Select(record => (int)(record.Date - Monday).TotalMinutes));
    }

    [Fact]
    public void Text_is_dropped_when_history_text_is_off()
    {
        var transcript = new DictationTranscript("Open Claude Code.", "open clock code", WasCleaned: true);
        var record = DictationRecord.From(transcript, 3, "Notepad", keepsText: false, Monday);
        Assert.Equal("", record.Text);
        Assert.Equal("", record.RawText);
        Assert.Equal(3, record.WordCount);
        Assert.True(record.WasCleaned);
    }

    [Fact]
    public void Sync_ids_are_given_once_and_kept_for_retries()
    {
        var ids = new Queue<Guid>([Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222")]);
        var (prepared, rows) = History.Empty.Adding(Record(1)).PreparingSync("mia-laptop", ids.Dequeue);
        var (retried, retriedRows) = prepared.PreparingSync("mia-laptop", ids.Dequeue);
        Assert.Equal(rows.Select(row => row.Id), retriedRows.Select(row => row.Id));
        Assert.Single(ids);
        Assert.Equal(retried.Records[0].SyncId, rows[0].Id);
        Assert.Equal("mia-laptop", rows[0].Device);
    }

    [Fact]
    public void Synced_records_are_not_uploaded_again()
    {
        var syncId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var history = History.Empty.Adding(Record(1, syncId: syncId)).Adding(Record(2, words: 9));
        var marked = history.MarkingSynced(new HashSet<Guid> { syncId });
        var (_, rows) = marked.PreparingSync("noah-pc", Guid.NewGuid);
        Assert.Single(rows);
        Assert.Equal(9, rows[0].WordCount);
        Assert.Equal([9], marked.UnsyncedSummaries.Select(summary => summary.WordCount));
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var history = History.Empty.Adding(Record(1, syncId: Guid.Parse("44444444-4444-4444-4444-444444444444"), isSynced: true)).Adding(Record(2));
        var decoded = History.Decode(history.Encode());
        Assert.Equal(history.Records, decoded?.Records);
    }

    [Fact]
    public void Unreadable_file_is_null() => Assert.Null(History.Decode("{not a list"));
}
