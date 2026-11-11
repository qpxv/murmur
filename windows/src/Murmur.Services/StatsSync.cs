using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>Why a sync didn't work.</summary>
public enum SyncFailure
{
    /// <summary>Not a <c>postgres(ql)://user:password@host/db</c> connection string.</summary>
    InvalidDatabaseUrl,
    /// <summary>No connection (offline, DNS, timeout).</summary>
    Unreachable,
    /// <summary>The database answered with an error (wrong password, missing permission, ...).</summary>
    Rejected,
    UnreadableResponse,
}

public static class SyncFailures
{
    /// <summary>What the user sees in Home and Keys.</summary>
    public static string Message(this SyncFailure failure) => failure switch
    {
        SyncFailure.InvalidDatabaseUrl => "That isn't a Postgres connection string",
        SyncFailure.Unreachable => "Offline, couldn't reach the database",
        SyncFailure.Rejected => "The database refused, check the URL",
        SyncFailure.UnreadableResponse => "The database sent something unexpected",
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
    };
}

/// <summary>
/// Talks to the user's Neon database over its HTTPS SQL endpoint to share dictation stats between
/// devices (same table as the Mac). Sends numbers only; logs status and timing, never the URL.
/// </summary>
public sealed class StatsSync(string connectionString)
{
    private const string Category = "sync";
    private static readonly HttpClient Http = new() { Timeout = AppConstants.Sync.RequestTimeout };

    /// <summary>
    /// Uploads the rows (creating the table on first use). With no rows it only checks the
    /// connection. Returns <c>null</c> on success.
    /// </summary>
    public async Task<SyncFailure?> UploadAsync(IReadOnlyList<NeonSql.Row> rows) =>
        (await SendAsync(NeonSql.UploadBody(rows), $"upload {rows.Count}")).Failure;

    /// <summary>Every device's dictations, as dashboard summaries, or why they couldn't be read.</summary>
    public async Task<(IReadOnlyList<DictationSummary>? Summaries, SyncFailure? Failure)> FetchSummariesAsync()
    {
        var (body, failure) = await SendAsync(NeonSql.FetchBody(), "fetch");
        if (body is null)
        {
            return (null, failure);
        }
        return NeonSql.Summaries(body) is { } summaries ? (summaries, null) : (null, SyncFailure.UnreadableResponse);
    }

    private async Task<(string? Body, SyncFailure? Failure)> SendAsync(string body, string label)
    {
        if (NeonSql.Endpoint(connectionString) is not { } endpoint)
        {
            return (null, SyncFailure.InvalidDatabaseUrl);
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(AppConstants.Sync.ConnectionHeader, connectionString.Trim());
        request.Headers.Add(AppConstants.Sync.ArrayModeHeader, "true");
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var response = await Http.SendAsync(request);
            Log.Info(Category, $"sync {label}: {(int)response.StatusCode} in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
            return response.StatusCode == HttpStatusCode.OK
                ? (await response.Content.ReadAsStringAsync(), null)
                : (null, SyncFailure.Rejected);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Log.Error(Category, $"sync {label} unreachable", exception);
            return (null, SyncFailure.Unreachable);
        }
    }
}
