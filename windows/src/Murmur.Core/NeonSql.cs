using System.Globalization;
using System.Text.Json;

namespace Murmur.Core;

/// <summary>
/// Wire format for syncing dictation stats through Neon's HTTPS SQL endpoint
/// (<c>POST https://&lt;host&gt;/sql</c>, see Neon's serverless driver). Only numbers travel, never text.
/// Same table and queries as the Mac's <c>NeonSQL.swift</c>.
/// </summary>
public static class NeonSql
{
    /// <summary>One dictation as stored in the shared table.</summary>
    public sealed record Row(Guid Id, string Device, DateTimeOffset Date, int WordCount, double SpeakingSeconds);

    private const string Table = AppConstants.Sync.TableName;

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly Statement CreateTable = new(
        $"""
        create table if not exists {Table} (
          id uuid primary key,
          device text not null,
          dictated_at timestamptz not null,
          word_count integer not null check (word_count >= 0),
          speaking_seconds double precision not null check (speaking_seconds >= 0)
        )
        """,
        []);

    private sealed record Statement(string Query, IReadOnlyList<string> Params);

    private sealed record Batch(IReadOnlyList<Statement> Queries);

    /// <summary>
    /// The HTTPS endpoint for a <c>postgres(ql)://user:password@host/db</c> connection string, or
    /// <c>null</c> when it isn't one.
    /// </summary>
    public static Uri? Endpoint(string connectionString)
    {
        if (!Uri.TryCreate(connectionString.Trim(), UriKind.Absolute, out var url)
            || !AppConstants.Sync.DatabaseSchemes.Contains(url.Scheme)
            || string.IsNullOrEmpty(url.Host))
        {
            return null;
        }
        return new UriBuilder(Uri.UriSchemeHttps, url.Host) { Path = AppConstants.Sync.EndpointPath }.Uri;
    }

    /// <summary>
    /// Creates the table if needed, then inserts the rows, all in one transaction. Rows already
    /// there (same id) are skipped, so uploading twice never counts twice. With no rows it just
    /// makes sure the table exists (a connection test).
    /// </summary>
    public static string UploadBody(IEnumerable<Row> rows)
    {
        var inserts = rows.Select(row => new Statement(
            $"""
            insert into {Table} (id, device, dictated_at, word_count, speaking_seconds)
            values ($1, $2, $3, $4, $5) on conflict (id) do nothing
            """,
            [
                row.Id.ToString().ToUpperInvariant(),
                row.Device,
                // full iso 8601 with the zone ("...Z"): without it postgres would guess the zone
                row.Date.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                row.WordCount.ToString(CultureInfo.InvariantCulture),
                row.SpeakingSeconds.ToString(CultureInfo.InvariantCulture),
            ]));
        return JsonSerializer.Serialize(new Batch([CreateTable, .. inserts]), Options);
    }

    /// <summary>Reads every device's dictations as plain numbers (epoch seconds, words, seconds spoken).</summary>
    public static string FetchBody() => JsonSerializer.Serialize(
        new Statement($"select extract(epoch from dictated_at)::float8, word_count, speaking_seconds from {Table}", []),
        Options);

    /// <summary>
    /// Parses an array-mode response of <see cref="FetchBody"/>. Values may arrive as JSON numbers
    /// or as text; rows that can't be read are skipped. <c>null</c> when the response isn't a result at all.
    /// </summary>
    public static IReadOnlyList<DictationSummary>? Summaries(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("rows", out var rows)
                || rows.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            return [.. rows.EnumerateArray().Select(Summary).OfType<DictationSummary>()];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DictationSummary? Summary(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var values = row.EnumerateArray().Select(Number).ToList();
        if (values is not [{ } epoch, { } words, { } seconds, ..])
        {
            return null;
        }
        return new DictationSummary(DateTimeOffset.UnixEpoch.AddSeconds(epoch), (int)words, seconds);
    }

    private static double? Number(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
        _ => null,
    };
}
