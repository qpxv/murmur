
namespace Murmur.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var settings = Settings.Defaults with
        {
            IsMicBoostEnabled = false,
            InputDeviceId = "{0.0.1.00000000}.{mia-headset}",
            Vocabulary = ["Murmur"],
            OverlayPosition = OverlayPosition.TopRight,
            IsCleanupEnabled = false,
            Cleanup = CleanupOptions.Defaults with { Style = CleanupStyle.Casual, RemovesStutters = false },
            TypingWordsPerMinute = 65,
            IsHistoryEnabled = false,
            SyncDeviceName = "Mia's laptop",
        };
        Assert.Equal(settings, SettingsJson.Decode(SettingsJson.Encode(settings)));
    }

    [Fact]
    public void Missing_keys_fall_back_to_defaults()
    {
        var settings = SettingsJson.Decode("""{"isMicBoostEnabled": false, "cleanup": {"style": "formal"}}""");
        var expected = Settings.Defaults with
        {
            IsMicBoostEnabled = false,
            Cleanup = CleanupOptions.Defaults with { Style = CleanupStyle.Formal },
        };
        Assert.Equal(expected, settings);
    }

    [Fact]
    public void Unreadable_file_is_null() => Assert.Null(SettingsJson.Decode("not json"));

    [Fact]
    public void Adds_trimmed_new_term()
    {
        var settings = Settings.Defaults with { Vocabulary = ["Claude"] };
        Assert.Equal(["Claude", "Murmur"], settings.AddingVocabularyTerm("  Murmur \n")?.Vocabulary);
    }

    [Fact]
    public void Rejects_duplicate_ignoring_case()
    {
        var settings = Settings.Defaults with { Vocabulary = ["Claude Code"] };
        Assert.Null(settings.AddingVocabularyTerm("claude code"));
    }

    [Fact]
    public void Rejects_blank_term()
    {
        var settings = Settings.Defaults with { Vocabulary = [] };
        Assert.Null(settings.AddingVocabularyTerm("   "));
    }
}
