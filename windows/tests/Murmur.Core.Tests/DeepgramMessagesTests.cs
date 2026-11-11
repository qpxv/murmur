using System.Web;

namespace Murmur.Core.Tests;

public class DeepgramMessagesTests
{
    [Fact]
    public void Listen_url_carries_audio_format_and_model()
    {
        var uri = DeepgramMessages.ListenUri([]);
        var query = HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal("wss", uri.Scheme);
        Assert.Equal("api.deepgram.com", uri.Host);
        Assert.Equal("/v1/listen", uri.AbsolutePath);
        Assert.Equal("nova-3", query["model"]);
        Assert.Equal("linear16", query["encoding"]);
        Assert.Equal("16000", query["sample_rate"]);
        Assert.Equal("1", query["channels"]);
    }

    [Fact]
    public void Each_vocabulary_term_becomes_its_own_keyterm()
    {
        var uri = DeepgramMessages.ListenUri(["Claude Code", " cmux ", ""]);
        var query = HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal(["Claude Code", "cmux"], query.GetValues("keyterm") ?? []);
        Assert.Contains("keyterm=Claude%20Code", uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void Parses_final_result()
    {
        const string json = """
            {"type":"Results","is_final":true,"speech_final":true,
             "channel":{"alternatives":[{"transcript":"open Claude Code","confidence":0.98}]}}
            """;
        Assert.Equal(new DeepgramMessages.FinalResult("open Claude Code", IsPause: true, IsFromFinalize: false), DeepgramMessages.ParseFinalResult(json));
    }

    [Fact]
    public void Final_without_speech_final_is_not_a_pause()
    {
        const string json = """{"type":"Results","is_final":true,"channel":{"alternatives":[{"transcript":"and then"}]}}""";
        Assert.False(DeepgramMessages.ParseFinalResult(json)?.IsPause);
    }

    [Fact]
    public void Recognizes_finalize_answer()
    {
        const string json = """{"type":"Results","is_final":true,"from_finalize":true,"channel":{"alternatives":[{"transcript":""}]}}""";
        Assert.Equal(new DeepgramMessages.FinalResult("", IsPause: false, IsFromFinalize: true), DeepgramMessages.ParseFinalResult(json));
    }

    [Fact]
    public void Ignores_interim_results()
    {
        const string json = """{"type":"Results","is_final":false,"channel":{"alternatives":[{"transcript":"open clo"}]}}""";
        Assert.Null(DeepgramMessages.ParseFinalResult(json));
    }

    [Theory]
    [InlineData("""{"type":"Metadata","request_id":"abc"}""")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Ignores_other_message_types_and_garbage(string json) => Assert.Null(DeepgramMessages.ParseFinalResult(json));
}
