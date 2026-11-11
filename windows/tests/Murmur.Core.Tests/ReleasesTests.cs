namespace Murmur.Core.Tests;

public class ReleasesTests
{
    private const string Body = """
        [
          {"tag_name":"windows-v0.3.0","draft":true,"prerelease":false,
           "assets":[{"name":"Murmur.exe","browser_download_url":"https://github.com/x/y/releases/download/windows-v0.3.0/Murmur.exe"}]},
          {"tag_name":"windows-v0.2.1","draft":false,"prerelease":true,
           "assets":[{"name":"Murmur.exe","browser_download_url":"https://github.com/x/y/releases/download/windows-v0.2.1/Murmur.exe"}]},
          {"tag_name":"windows-v0.2.0","draft":false,"prerelease":false,
           "assets":[{"name":"notes.txt","browser_download_url":"https://github.com/x/y/notes.txt"},
                     {"name":"Murmur.exe","digest":"sha256:54208b02","browser_download_url":"https://github.com/x/y/releases/download/windows-v0.2.0/Murmur.exe"}]},
          {"tag_name":"mac-v9.0.0","draft":false,"prerelease":false,
           "assets":[{"name":"Murmur.exe","browser_download_url":"https://github.com/x/y/mac.exe"}]},
          {"tag_name":"windows-v0.1.0","draft":false,"prerelease":false,
           "assets":[{"name":"Murmur.exe","browser_download_url":"https://github.com/x/y/releases/download/windows-v0.1.0/Murmur.exe"}]},
          {"tag_name":"windows-v0.0.9","draft":false,"prerelease":false,"assets":[]}
        ]
        """;

    [Fact]
    public void Only_published_windows_releases_with_the_exe_count()
    {
        var releases = Releases.Parse(Body);
        Assert.Equal([new Version(0, 2, 0), new Version(0, 1, 0)], releases.Select(release => release.Version));
    }

    [Fact]
    public void Reads_the_download_and_checksum()
    {
        var release = Releases.Parse(Body)[0];
        Assert.Equal("https://github.com/x/y/releases/download/windows-v0.2.0/Murmur.exe", release.DownloadUrl.AbsoluteUri);
        Assert.Equal("54208b02", release.Sha256);
        Assert.Null(Releases.Parse(Body)[1].Sha256);
    }

    [Fact]
    public void Offers_the_newest_release_above_the_running_version() =>
        Assert.Equal(new Version(0, 2, 0), Releases.Newer(Releases.Parse(Body), new Version(0, 1, 0))?.Version);

    [Theory]
    [InlineData("0.2.0")]
    [InlineData("0.10.0")]
    public void Nothing_to_offer_when_up_to_date_or_ahead(string current) =>
        Assert.Null(Releases.Newer(Releases.Parse(Body), Version.Parse(current)));

    [Fact]
    public void Versions_compare_as_numbers_not_text() =>
        Assert.Equal(
            new Version(0, 10, 0),
            Releases.Newer([new Release(new Version(0, 9, 0), new Uri("https://x/a"), null), new Release(new Version(0, 10, 0), new Uri("https://x/b"), null)], new Version(0, 2, 0))?.Version);

    [Theory]
    [InlineData("0.1.0+6d224985cd63", "0.1.0")]
    [InlineData("0.2.0", "0.2.0")]
    [InlineData("1.0.0-dev", "1.0.0")]
    public void App_version_drops_the_commit_hash(string informational, string expected) =>
        Assert.Equal(Version.Parse(expected), Releases.AppVersion(informational));

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"message":"API rate limit exceeded"}""")]
    public void Error_bodies_have_no_releases(string body) => Assert.Empty(Releases.Parse(body));
}
