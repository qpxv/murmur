
namespace Murmur.Core.Tests;

public class CleanupResponseTests
{
    [Fact]
    public void End_turn_returns_joined_text_blocks_trimmed() =>
        Assert.Equal("Open Claude Code.", CleanupResponse.CleanedText("end_turn", [" Open Claude", " Code. "]));

    [Theory]
    [InlineData("refusal")]
    [InlineData("max_tokens")]
    [InlineData(null)]
    public void Abnormal_stop_keeps_raw_text(string? stopReason) =>
        Assert.Null(CleanupResponse.CleanedText(stopReason, ["partial"]));

    [Fact]
    public void Empty_text_keeps_raw_text() => Assert.Null(CleanupResponse.CleanedText("end_turn", []));

    [Fact]
    public void Request_settings_come_from_the_shared_config()
    {
        Assert.Equal("claude-opus-5-5", AppConstants.Cleanup.Model);
        Assert.Equal("low", AppConstants.Cleanup.Effort);
        Assert.Equal("server-side-fallback-2026-07-01", AppConstants.Cleanup.FallbackBeta);
        Assert.Equal(4096, AppConstants.Cleanup.MaxTokens);
        Assert.Equal(TimeSpan.FromSeconds(7), AppConstants.Cleanup.FinishTimeout);
    }
}
