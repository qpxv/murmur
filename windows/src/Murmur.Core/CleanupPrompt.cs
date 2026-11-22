namespace Murmur.Core;

/// <summary>
/// What Claude is told when cleaning a dictated chunk. The system prompt depends only on the
/// cleanup options (so it stays identical across requests); the user message carries everything
/// that varies. The texts live in <c>shared/cleanup-prompt.json</c>, which the Mac app uses too.
/// </summary>
public static class CleanupPrompt
{
    private static readonly SharedCleanupPrompt Texts = SharedFiles.CleanupPrompt;

    public static string System(CleanupOptions options)
    {
        string?[] fixes =
        [
            options.FixesMisheardWords ? Texts.Fixes.MisheardWords : null,
            options.FixesPunctuation ? Texts.Fixes.Punctuation : null,
            options.RemovesStutters ? Texts.Fixes.Stutters : null,
        ];
        var chosen = fixes.OfType<string>().ToList();
        var changes = chosen.Count == 0 ? "" : Texts.FixesHeader + "\n" + string.Join("\n", chosen);
        string[] parts = [Texts.Intro, changes, Style(options.Style), Texts.Boundaries, Texts.Screen, Texts.Output];
        return string.Join("\n\n", parts.Where(part => part.Length > 0));
    }

    public static string UserMessage(string transcript, string previous, string next, CleanupContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var terms = context.Vocabulary.Where(term => !string.IsNullOrWhiteSpace(term)).ToList();
        string?[] lines =
        [
            terms.Count == 0 ? null : $"<vocabulary>{string.Join(", ", terms)}</vocabulary>",
            context.AppName is null ? null : $"<app>{context.AppName}</app>",
            context.Screen.Length == 0 ? null : $"<screen>\n{context.Screen}\n</screen>",
            previous.Length == 0 ? null : $"<previous>{previous}</previous>",
            $"<transcript>\n{transcript}\n</transcript>",
            next.Length == 0 ? null : $"<next>{next}</next>",
        ];
        return string.Join("\n", lines.OfType<string>());
    }

    private static string Style(CleanupStyle style) => style switch
    {
        CleanupStyle.Casual => Texts.Styles.Casual,
        CleanupStyle.Normal => Texts.Styles.Normal,
        CleanupStyle.Formal => Texts.Styles.Formal,
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null),
    };
}
