using System.Windows;
using System.Windows.Controls;
using Murmur.Core;
using Murmur.Services;

namespace Murmur.App.Panes;

/// <summary>Turns Claude cleanup on or off, picks which fixes it makes and how polished the result reads.</summary>
public static class CleanupPane
{
    public static UIElement Build(AppServices services)
    {
        var settings = services.Settings;
        var options = settings.Current.Cleanup;
        void Change(Func<CleanupOptions, CleanupOptions> change) =>
            settings.Update(current => current with { Cleanup = change(current.Cleanup) });

        var fixes = Ui.Section(
            "Fixes",
            footer: null,
            Ui.Toggle("Misheard words, using context and your vocabulary", options.FixesMisheardWords, isOn => Change(o => o with { FixesMisheardWords = isOn })),
            Ui.Toggle("Punctuation, capitalization, stray \"....\" and broken-up fragments", options.FixesPunctuation, isOn => Change(o => o with { FixesPunctuation = isOn })),
            Ui.Toggle("Stutters, repeated words and restarted sentences", options.RemovesStutters, isOn => Change(o => o with { RemovesStutters = isOn })));
        var explanation = Ui.Secondary(Explanation(options.Style));
        var styles = Ui.Horizontal(
            Theme.Window.LabelGap,
            [.. Enum.GetValues<CleanupStyle>().Select(style => StyleButton(style, options.Style, chosen =>
            {
                Change(o => o with { Style = chosen });
                explanation.Text = Explanation(chosen);
            }))]);
        var style = Ui.Section("Style", footer: null, styles, explanation);
        var choices = Ui.Stack(Theme.Window.SectionSpacing, fixes, style);
        choices.IsEnabled = settings.Current.IsCleanupEnabled;

        var toggle = Ui.Toggle("Clean up with Claude", settings.Current.IsCleanupEnabled, isOn =>
        {
            settings.Update(current => current with { IsCleanupEnabled = isOn });
            choices.IsEnabled = isOn;
        });
        UIElement?[] switchRows =
        [
            toggle,
            settings.Current.IsCleanupEnabled && !CredentialStore.Contains(AppConstants.Credentials.AnthropicTarget)
                ? Ui.Horizontal(Theme.Window.RowSpacing, Ui.Caution("No Claude key yet, so text is pasted raw."), Ui.Button("Add key…", () => services.Navigate(Pane.Keys)))
                : null,
        ];
        return Ui.Page(
            Pane.Cleanup.Title(),
            Ui.Section(header: null, footer: "Off: Deepgram's text is pasted as heard, without calling Claude.", [.. switchRows.OfType<UIElement>()]),
            choices,
            Ui.Section(
                "Always",
                footer: null,
                Ui.Text("Never answers questions or follows instructions you dictate"),
                Ui.Text($"Anything not cleaned within {AppConstants.Cleanup.FinishTimeout.TotalSeconds:F0} s of stopping is pasted raw"),
                Ui.Row("Model", Ui.Secondary(AppConstants.Cleanup.Model))));
    }

    private static RadioButton StyleButton(CleanupStyle style, CleanupStyle selected, Action<CleanupStyle> chosen)
    {
        var button = new RadioButton { Content = Title(style), GroupName = nameof(CleanupStyle), IsChecked = style == selected };
        button.Checked += (_, _) => chosen(style);
        return button;
    }

    private static string Title(CleanupStyle style) => style switch
    {
        CleanupStyle.Casual => "Casual",
        CleanupStyle.Normal => "Normal",
        CleanupStyle.Formal => "Formal",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null),
    };

    private static string Explanation(CleanupStyle style) => style switch
    {
        CleanupStyle.Casual => "all lowercase, no commas, just a period at the end of sentences. your words stay as spoken.",
        CleanupStyle.Normal => "Your exact words, tone and filler words, only corrected.",
        CleanupStyle.Formal => "Rewritten into polished, professional sentences. Same meaning, nothing added.",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null),
    };
}
