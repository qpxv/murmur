using System.Text.Json;

namespace Murmur.Core.Tests;

public class NeonSqlTests
{
    [Fact]
    public void Endpoint_uses_the_database_host()
    {
        const string url = "postgresql://mia:secret@ep-quiet-lake-123456.eu-central-1.aws.neon.tech/neondb?sslmode=require";
        Assert.Equal("https://ep-quiet-lake-123456.eu-central-1.aws.neon.tech/sql", NeonSql.Endpoint(url)?.AbsoluteUri);
    }

    [Fact]
    public void Pooled_host_and_short_scheme_work()
    {
        const string url = "  postgres://noah:pw@ep-x-pooler.us-east-2.aws.neon.tech/db\n";
        Assert.Equal("https://ep-x-pooler.us-east-2.aws.neon.tech/sql", NeonSql.Endpoint(url)?.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://ep-x.neon.tech/db")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData("postgresql:///neondb")]
    public void Rejects_anything_but_a_postgres_url_with_host(string input) => Assert.Null(NeonSql.Endpoint(input));

    [Fact]
    public void Upload_creates_table_then_inserts_every_row_idempotently()
    {
        var row = new NeonSql.Row(
            Guid.Parse("6F9619FF-8B86-D011-B42D-00CF4FC964FF"),
            "Mia's laptop",
            DateTimeOffset.FromUnixTimeSeconds(1_800_000_000),
            42,
            12.5);
        using var json = JsonDocument.Parse(NeonSql.UploadBody([row, row]));
        var queries = json.RootElement.GetProperty("queries").EnumerateArray().ToList();
        Assert.Equal(3, queries.Count);
        Assert.Contains("create table if not exists murmur_dictations", queries[0].GetProperty("query").GetString(), StringComparison.Ordinal);
        Assert.Contains("on conflict (id) do nothing", queries[1].GetProperty("query").GetString(), StringComparison.Ordinal);
        Assert.Equal(
            ["6F9619FF-8B86-D011-B42D-00CF4FC964FF", "Mia's laptop", "2027-01-15T08:00:00.000Z", "42", "12.5"],
            queries[1].GetProperty("params").EnumerateArray().Select(param => param.GetString()));
    }

    [Fact]
    public void Empty_upload_only_makes_sure_the_table_exists()
    {
        using var json = JsonDocument.Parse(NeonSql.UploadBody([]));
        Assert.Equal(1, json.RootElement.GetProperty("queries").GetArrayLength());
    }

    [Fact]
    public void Fetch_selects_plain_numbers()
    {
        using var json = JsonDocument.Parse(NeonSql.FetchBody());
        var query = json.RootElement.GetProperty("query").GetString();
        Assert.Contains("extract(epoch from dictated_at)", query, StringComparison.Ordinal);
        Assert.Contains("from murmur_dictations", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Parses_rows_with_numbers_or_text()
    {
        const string json = """{"fields":[],"rows":[[1800000000,"120","45.5"],["1800000100.25",7,3]]}""";
        Assert.Equal(
            [
                new DictationSummary(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), 120, 45.5),
                new DictationSummary(DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_100_250), 7, 3),
            ],
            NeonSql.Summaries(json));
    }

    [Fact]
    public void Skips_unreadable_rows_and_handles_empty()
    {
        Assert.Single(NeonSql.Summaries("""{"rows":[[null,1,2],["x","y"],[1800000000,5,2]]}""") ?? []);
        Assert.Equal([], NeonSql.Summaries("""{"rows":[]}"""));
    }

    [Fact]
    public void Error_body_is_not_a_result() =>
        Assert.Null(NeonSql.Summaries("""{"message":"password authentication failed","code":"28P01"}"""));
}
