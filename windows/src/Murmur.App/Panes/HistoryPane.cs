using System.Windows;
using System.Windows.Controls;
using Murmur.Core;

namespace Murmur.App.Panes;

/// <summary>Every past dictation, newest first, searchable, with the original Deepgram text on demand.</summary>
public static class HistoryPane
{
    public static UIElement Build(AppServices services)
    {
        var records = services.History.Current.Records;
        var list = Ui.Stack(Theme.Window.RowSpacing);
        var search = new TextBox { Width = Theme.Window.FieldWidth };
        search.TextChanged += (_, _) => Fill(list, records, search.Text);
        Fill(list, records, "");

        var saveText = Ui.Toggle("Save text", services.Settings.Current.IsHistoryEnabled, isOn =>
            services.Settings.Update(settings => settings with { IsHistoryEnabled = isOn }));
        saveText.ToolTip = "Save what you said. Off: only word counts and times are kept";
        var clear = Ui.Button("Clear history…", () => ConfirmClear(services));
        clear.IsEnabled = records.Count > 0;

        var tools = Ui.Horizontal(Theme.Window.LabelGap, Ui.Horizontal(Theme.Window.RowSpacing, Ui.Text("Search"), search), saveText, clear);
        return Ui.Page(Pane.History.Title(), tools, list);
    }

    private static void Fill(StackPanel list, IReadOnlyList<DictationRecord> records, string search)
    {
        list.Children.Clear();
        if (records.Count == 0)
        {
            list.Children.Add(Ui.Empty("No history yet", "Finished dictations are listed here."));
            return;
        }
        var matches = records.Where(record => Matches(record, search.Trim())).ToList();
        if (matches.Count == 0)
        {
            list.Children.Add(Ui.Empty("Nothing found", $"No dictation contains \"{search.Trim()}\"."));
            return;
        }
        foreach (var record in matches.Take(Theme.Window.HistoryPageSize))
        {
            list.Children.Add(Ui.Section(header: null, footer: null, DictationRow.Build(record, showsOriginal: true)));
        }
        if (matches.Count > Theme.Window.HistoryPageSize)
        {
            list.Children.Add(Ui.Secondary($"Showing the newest {Theme.Window.HistoryPageSize} of {matches.Count}. Search to find older ones."));
        }
    }

    private static bool Matches(DictationRecord record, string search) =>
        search.Length == 0
        || record.Text.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || (record.AppName ?? "").Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private static void ConfirmClear(AppServices services)
    {
        var answer = MessageBox.Show(
            "Deletes this PC's history. Numbers already synced to other devices stay. It can't be undone.",
            "Delete all dictations?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            services.History.Clear();
        }
    }
}
