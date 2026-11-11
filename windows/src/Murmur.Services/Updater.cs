using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// Updates the exe in place from the GitHub releases: download next to the running exe, check the
/// checksum, swap the files (Windows lets a running exe be renamed, not overwritten) and start the
/// new one. Settings, keys and history live elsewhere, so nothing else changes.
/// </summary>
public static class Updater
{
    private const string Category = "update";
    private const string DownloadSuffix = ".download";
    private const string OldSuffix = ".old";

    private static readonly HttpClient Http = CreateClient();

    /// <summary>The version running now; <c>null</c> for a build without one.</summary>
    public static Version? CurrentVersion => Releases.AppVersion(DiagnosticsReport.Version);

    /// <summary>The release to offer, <c>null</c> when up to date; <c>false</c> when GitHub couldn't be asked.</summary>
    public static async Task<(Release? Newer, bool IsReachable)> CheckAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AppConstants.Update.ReleasesUrl);
            // only the api call: the exe download itself must not ask for json
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var current = CurrentVersion ?? new Version();
            var newer = Releases.Newer(Releases.Parse(body), current);
            Log.Info(Category, $"running {current}, newest offered {newer?.Version.ToString() ?? "none"}");
            return (newer, true);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Log.Error(Category, "checking for updates failed", exception);
            return (null, false);
        }
    }

    /// <summary>
    /// Downloads the release and puts it in place of the running exe. Returns whether it worked;
    /// on failure the running exe is left as it was.
    /// </summary>
    public static async Task<bool> InstallAsync(Release release)
    {
        if (Environment.ProcessPath is not { } exe)
        {
            return false;
        }
        var download = exe + DownloadSuffix;
        var old = exe + OldSuffix;
        try
        {
            await DownloadAsync(release.DownloadUrl, download);
            if (!await HasChecksumAsync(download, release.Sha256))
            {
                Log.Error(Category, "the download's checksum doesn't match, discarding it");
                File.Delete(download);
                return false;
            }
            File.Delete(old);
            File.Move(exe, old);
            try
            {
                File.Move(download, exe);
            }
            catch (IOException)
            {
                // put the running version back, so the next start still works
                File.Move(old, exe);
                throw;
            }
            Log.Info(Category, $"installed {release.Version}");
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            Log.Error(Category, "installing the update failed", exception);
            TryDelete(download);
            return false;
        }
    }

    /// <summary>Starts the (new) exe; it waits for this process to quit before it takes over.</summary>
    public static void StartNewVersion()
    {
        if (Environment.ProcessPath is { } exe)
        {
            Process.Start(new ProcessStartInfo(exe, AppConstants.Update.RestartArgument) { UseShellExecute = false })?.Dispose();
        }
    }

    /// <summary>Removes what the last update left behind (the previous exe can't be deleted while it runs).</summary>
    public static void CleanUp()
    {
        if (Environment.ProcessPath is { } exe)
        {
            TryDelete(exe + OldSuffix);
            TryDelete(exe + DownloadSuffix);
        }
    }

    private static async Task DownloadAsync(Uri url, string path)
    {
        var started = Stopwatch.GetTimestamp();
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using (var file = File.Create(path))
        {
            await response.Content.CopyToAsync(file);
        }
        Log.Info(Category, $"downloaded {new FileInfo(path).Length} bytes in {Stopwatch.GetElapsedTime(started).TotalSeconds:F1} s");
    }

    // github reports a sha256 per asset; a release without one is trusted as downloaded over https
    private static async Task<bool> HasChecksumAsync(string path, string? expected)
    {
        if (expected is null)
        {
            return true;
        }
        await using var file = File.OpenRead(path);
        var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(file));
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // still locked (the old exe is quitting); the next start tries again
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = AppConstants.Update.DownloadTimeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AppConstants.Update.UserAgent, CurrentVersion?.ToString() ?? "0"));
        return client;
    }
}
