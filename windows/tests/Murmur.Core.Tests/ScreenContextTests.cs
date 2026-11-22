
namespace Murmur.Core.Tests;

public class ScreenContextTests
{
    private static IReadOnlyList<string> Terms(string[] lines, string[]? vocabulary = null, DictationLanguage language = DictationLanguage.English) =>
        ScreenContext.From(lines, vocabulary ?? [], language).Terms;

    [Fact]
    public void Finds_domains_even_inside_urls_and_punctuation() =>
        Assert.Equal(["blogr.ai", "noah-site.dev"], Terms(["check (blogr.ai), it's live", "https://noah-site.dev/pricing"]));

    [Fact]
    public void Ignores_abbreviations_and_version_numbers() =>
        Assert.Empty(Terms(["e.g. we ship v1.2.3 at 10am on the 2nd"]));

    [Fact]
    public void Finds_inner_capitals_acronyms_and_model_names() =>
        Assert.Equal(["GitHub", "iPhone", "API", "GPT4"], Terms(["we use GitHub on an iPhone with the API and GPT4"]));

    [Fact]
    public void English_capital_mid_sentence_is_a_name_but_sentence_start_is_not() =>
        Assert.Equal(["Xaver"], Terms(["Thanks, ask Xaver. Then we see"]));

    [Fact]
    public void German_skips_plain_capitals() =>
        Assert.Equal(["blogr.ai"], Terms(["Das Haus von Xaver ist auf blogr.ai"], language: DictationLanguage.German));

    [Fact]
    public void Drops_possessive_ending() => Assert.Equal(["Mia"], Terms(["that is Mia's idea"]));

    [Fact]
    public void Leaves_out_vocabulary_ignoring_case() =>
        Assert.Equal(["blogr.ai"], Terms(["open the Claude app and blogr.ai"], vocabulary: ["claude"]));

    [Fact]
    public void Most_frequent_first_keeping_first_spelling() =>
        Assert.Equal(["Lena", "Noah"], Terms(["hi to Noah and Lena", "hi to Lena", "hi to LENA"]));

    [Fact]
    public void Caps_the_number_of_terms()
    {
        var lines = Enumerable.Range(0, AppConstants.Screen.MaxKeyterms + 5).Select(number => $"see site{number}.dev").ToArray();
        Assert.Equal(AppConstants.Screen.MaxKeyterms, Terms(lines).Count);
    }

    [Fact]
    public void Keeps_the_end_of_long_text_and_drops_blank_lines()
    {
        var screen = ScreenContext.From([new string('a', AppConstants.Screen.MaxCharacters), "  ", " newest "], [], DictationLanguage.English);
        Assert.Equal(AppConstants.Screen.MaxCharacters, screen.Text.Length);
        Assert.EndsWith("a\nnewest", screen.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_screen_has_no_text_or_terms()
    {
        var screen = ScreenContext.From([], [], DictationLanguage.English);
        Assert.Equal("", screen.Text);
        Assert.Empty(screen.Terms);
    }
}
