using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Murmur.Core;
using Murmur.Services;

namespace Murmur.App.Panes;

/// <summary>Dashboard: totals, time saved against the user's typing speed, and the latest dictations.</summary>
public static class HomePane
{
    public static UIElement Build(AppServices services)
    {
        var stats = new DictationStats(
            services.Sync.Summaries(),
            services.Settings.Current.TypingWordsPerMinute,
            DateTimeOffset.Now,
            TimeZoneInfo.Local);
        return Ui.Page(
            Pane.Home.Title(),
            Source(services.Sync.Status),
            Cards(stats),
            TypingSpeed(services.Settings),
            Recent(services.History.Current.Records));
    }

    // where the numbers come from, so a missing device is never a mystery
    private static TextBlock Source(SyncStatus status) => status switch
    {
        SyncStatus.SyncedStatus synced => Ui.Secondary($"All devices, synced {synced.At:HH:mm}"),
        SyncStatus.FailedStatus failed => Ui.Caution($"This PC only. {failed.Failure.Message()}"),
        _ => Ui.Secondary("This PC only"),
    };

    private static WrapPanel Cards(DictationStats stats)
    {
        (string Title, string Value)[] cards =
        [
            ("Words today", stats.WordsToday.ToString("N0", CultureInfo.CurrentCulture)),
            ("Words in total", stats.TotalWords.ToString("N0", CultureInfo.CurrentCulture)),
            ("Time saved", Durations.Format(stats.TimeSavedSeconds)),
            ("Speaking speed", stats.SpeakingWordsPerMinute is { } speed ? $"{speed} WPM" : "-"),
            ("Dictations", stats.DictationCount.ToString("N0", CultureInfo.CurrentCulture)),
            ("Day streak", stats.StreakDays.ToString("N0", CultureInfo.CurrentCulture)),
        ];
        var panel = new WrapPanel();
        foreach (var (title, value) in cards)
        {
            var card = Ui.Section(header: null, footer: null, Ui.Secondary(title), Ui.Text(value, Theme.StatCard.ValueFontSize, FontWeights.SemiBold));
            card.MinWidth = Theme.StatCard.MinWidth;
            card.Margin = new Thickness(0, 0, Theme.StatCard.Spacing, Theme.StatCard.Spacing);
            panel.Children.Add(card);
        }
        return panel;
    }

    private static StackPanel TypingSpeed(SettingsStore settings)
    {
        var field = new TextBox
        {
            Text = settings.Current.TypingWordsPerMinute.ToString(CultureInfo.CurrentCulture),
            Width = Theme.Window.NarrowFieldWidth,
            TextAlignment = TextAlignment.Right,
        };
        void Commit()
        {
            if (int.TryParse(field.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var speed))
            {
                var clamped = Math.Clamp(speed, Theme.Typing.MinWordsPerMinute, Theme.Typing.MaxWordsPerMinute);
                settings.Update(current => current with { TypingWordsPerMinute = clamped });
            }
            field.Text = settings.Current.TypingWordsPerMinute.ToString(CultureInfo.CurrentCulture);
        }
        field.LostFocus += (_, _) => Commit();
        field.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                Commit();
            }
        };
        return Ui.Section(
            header: null,
            footer: "Used for \"Time saved\": how long typing the same words would have taken.",
            Ui.Row("Your typing speed", Ui.Horizontal(Theme.Window.RowSpacing, field, Ui.Secondary("WPM"))));
    }

    private static StackPanel Recent(IReadOnlyList<DictationRecord> records)
    {
        var title = Ui.Text("Recent", Theme.Window.HeaderFontSize, FontWeights.SemiBold);
        if (records.Count == 0)
        {
            return Ui.Stack(Theme.Window.RowSpacing, title, Ui.Empty("No dictations yet", "Hold Caps Lock and start talking. Your dictations show up here."));
        }
        var rows = records.Take(Theme.Window.RecentCount).Select(record => DictationRow.Build(record, showsOriginal: false));
        return Ui.Section("Recent", footer: null, [.. rows]);
    }
}
