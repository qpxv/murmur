using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Murmur.App.Panes;

/// <summary>
/// Words and names that must come out spelled exactly right. They're boosted in Deepgram and
/// handed to Claude for cleanup.
/// </summary>
public static class VocabularyPane
{
    public static UIElement Build(AppServices services)
    {
        var settings = services.Settings;
        var terms = Ui.Stack(Theme.Window.RowSpacing);
        var count = Ui.Text("", Theme.Window.HeaderFontSize, FontWeights.SemiBold);
        var draft = new TextBox { Width = Theme.Window.FieldWidth };

        void Refresh()
        {
            count.Text = $"{settings.Current.Vocabulary.Count} terms";
            terms.Children.Clear();
            foreach (var term in settings.Current.Vocabulary)
            {
                var remove = new Button { Content = Ui.Glyph(Theme.Icons.Remove, Theme.Window.CaptionFontSize), ToolTip = "Remove" };
                remove.Click += (_, _) =>
                {
                    settings.Update(current => current with { Vocabulary = [.. current.Vocabulary.Where(existing => existing != term)] });
                    Refresh();
                };
                terms.Children.Add(Ui.Row(term, remove));
            }
        }

        // always clickable: a blank or duplicate entry is simply ignored
        void Add()
        {
            if (settings.Current.AddingVocabularyTerm(draft.Text) is { } updated)
            {
                settings.Update(_ => updated);
                draft.Clear();
                Refresh();
            }
        }

        draft.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                Add();
            }
        };
        Refresh();
        return Ui.Page(
            Pane.Vocabulary.Title(),
            Ui.Section(
                header: null,
                footer: "Names, products and jargon that get misheard, e.g. \"Claude Code\".",
                Ui.Horizontal(Theme.Window.RowSpacing, draft, Ui.Button("Add", Add, isAccent: true))),
            Ui.Section(
                header: null,
                footer: "When you start dictating, the text of the front window (a chat, a web page) is read, so names on it "
                    + "are spelled right. It's sent to Deepgram and Claude with that dictation, never saved.",
                Ui.Toggle(
                    "Use names from the screen",
                    settings.Current.IsScreenContextEnabled,
                    isOn => settings.Update(current => current with { IsScreenContextEnabled = isOn }))),
            Ui.Stack(Theme.Window.RowSpacing / 2, count, Ui.Section(header: null, footer: null, terms)));
    }
}
