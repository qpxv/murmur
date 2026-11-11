
namespace Murmur.Core.Tests;

public class CleanupPromptTests
{
    [Fact]
    public void Default_prompt_has_every_fix_and_keeps_wording()
    {
        var prompt = CleanupPrompt.System(CleanupOptions.Defaults);
        Assert.Contains("misheard", prompt);
        Assert.Contains("contradicts what is said right after", prompt);
        Assert.Contains("grammatical and still impossible", prompt);
        Assert.Contains("when in doubt, leave the words", prompt);
        Assert.Contains("punctuation", prompt);
        Assert.Contains("stutters", prompt);
        Assert.Contains("Keep everything else exactly as spoken", prompt);
        Assert.Contains("<previous>", prompt);
        Assert.Contains("continues in <next>", prompt);
    }

    [Fact]
    public void Punctuation_joins_fragments_and_stutters_drop_restarts()
    {
        var prompt = CleanupPrompt.System(CleanupOptions.Defaults);
        Assert.Contains("never close a short fragment", prompt);
        Assert.Contains("keep only the completed version", prompt);
    }

    [Fact]
    public void Disabled_fixes_are_left_out()
    {
        var prompt = CleanupPrompt.System(CleanupOptions.Defaults with { FixesPunctuation = false, RemovesStutters = false });
        Assert.Contains("misheard", prompt);
        Assert.DoesNotContain("Fix punctuation", prompt);
        Assert.DoesNotContain("stutters", prompt);
    }

    [Fact]
    public void Casual_asks_for_lowercase_and_no_commas()
    {
        var prompt = CleanupPrompt.System(CleanupOptions.Defaults with { Style = CleanupStyle.Casual });
        Assert.Contains("lowercase", prompt);
        Assert.Contains("no commas", prompt);
        Assert.DoesNotContain("Style: formal", prompt);
    }

    [Fact]
    public void Formal_allows_rephrasing_but_not_new_content()
    {
        var prompt = CleanupPrompt.System(CleanupOptions.Defaults with { Style = CleanupStyle.Formal });
        Assert.Contains("Style: formal", prompt);
        Assert.Contains("add nothing new", prompt);
        Assert.DoesNotContain("Keep everything else exactly as spoken", prompt);
    }

    [Fact]
    public void Style_alone_still_needs_claude()
    {
        var options = CleanupOptions.Defaults with { FixesMisheardWords = false, FixesPunctuation = false, RemovesStutters = false };
        Assert.False(options.HasWork);
        var casual = options with { Style = CleanupStyle.Casual };
        Assert.True(casual.HasWork);
        Assert.DoesNotContain("Make these fixes", CleanupPrompt.System(casual));
    }

    [Fact]
    public void Fixes_are_listed_under_one_header_and_parts_are_separated_by_blank_lines()
    {
        var prompt = CleanupPrompt.System(CleanupOptions.Defaults);
        Assert.Contains("\n\nMake these fixes:\n- Correct words", prompt);
        Assert.Contains("as they are.\n- Fix punctuation", prompt);
        Assert.StartsWith("You clean up dictated text.", prompt, StringComparison.Ordinal);
        Assert.EndsWith("\n\nReturn only the cleaned text, with no preamble, quotes or notes.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void User_message_wraps_every_part()
    {
        var message = CleanupPrompt.UserMessage(
            transcript: "open clock code",
            previous: "earlier words",
            next: "later words",
            vocabulary: ["Claude Code", " ", "cmux"],
            appName: "Windows Terminal");
        Assert.Equal(
            """
            <vocabulary>Claude Code, cmux</vocabulary>
            <app>Windows Terminal</app>
            <previous>earlier words</previous>
            <transcript>
            open clock code
            </transcript>
            <next>later words</next>
            """.ReplaceLineEndings("\n"),
            message);
    }

    [Fact]
    public void User_message_leaves_out_empty_parts()
    {
        var message = CleanupPrompt.UserMessage(transcript: "hi", previous: "", next: "", vocabulary: [], appName: null);
        Assert.Equal("<transcript>\nhi\n</transcript>", message);
    }
}
