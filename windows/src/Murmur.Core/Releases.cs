using System.Text.Json;

namespace Murmur.Core;

/// <summary>A published windows release: its version and where its exe is.</summary>
/// <param name="Version">From the tag, e.g. 0.2.0 for <c>windows-v0.2.0</c>.</param>
/// <param name="DownloadUrl">The <c>Murmur.exe</c> asset.</param>
/// <param name="Sha256">The asset's checksum as GitHub reports it (lowercase hex); <c>null</c> if it reports none.</param>
public sealed record Release(Version Version, Uri DownloadUrl, string? Sha256);

/// <summary>Reads GitHub's release list and picks the update to offer.</summary>
public static class Releases
{
    private const string DigestPrefix = "sha256:";

    /// <summary>The app's own version from its informational version ("0.1.0+abc123" is 0.1.0).</summary>
    public static Version? AppVersion(string informationalVersion) =>
        Version.TryParse(informationalVersion.Split('+', '-')[0], out var version) ? version : null;

    /// <summary>The newest release above <paramref name="current"/>; <c>null</c> when there is none.</summary>
    public static Release? Newer(IEnumerable<Release> releases, Version current) =>
        releases.Where(release => release.Version > current).MaxBy(release => release.Version);

    /// <summary>
    /// Published windows releases that carry the exe, from the body of <c>GET /repos/.../releases</c>.
    /// Drafts, pre-releases, other tags and anything unreadable are skipped.
    /// </summary>
    public static IReadOnlyList<Release> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }
            return [.. document.RootElement.EnumerateArray().Select(ParseRelease).OfType<Release>()];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Release? ParseRelease(JsonElement release)
    {
        if (release.ValueKind != JsonValueKind.Object
            || IsTrue(release, "draft")
            || IsTrue(release, "prerelease")
            || Text(release, "tag_name") is not { } tag
            || !tag.StartsWith(AppConstants.Update.TagPrefix, StringComparison.Ordinal)
            || !Version.TryParse(tag[AppConstants.Update.TagPrefix.Length..], out var version)
            || !release.TryGetProperty("assets", out var assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var exe = assets.EnumerateArray().FirstOrDefault(asset => Text(asset, "name") == AppConstants.Update.AssetName);
        if (exe.ValueKind != JsonValueKind.Object
            || Text(exe, "browser_download_url") is not { } url
            || !Uri.TryCreate(url, UriKind.Absolute, out var downloadUrl))
        {
            return null;
        }
        var digest = Text(exe, "digest");
        var sha256 = digest is not null && digest.StartsWith(DigestPrefix, StringComparison.Ordinal)
            ? digest[DigestPrefix.Length..]
            : null;
        return new Release(version, downloadUrl, sha256);
    }

    private static bool IsTrue(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
