using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Murmur.Core;
using Murmur.Services;

namespace Murmur.App.Panes;

/// <summary>
/// One past dictation: when, where, how long, the text, and a copy button. With
/// <c>showsOriginal</c>, the uncleaned Deepgram text can be expanded below.
/// </summary>
public static class DictationRow
{
    public static StackPanel Build(DictationRecord record, bool showsOriginal)
    {
        string?[] facts =
        [
            record.Date.ToLocalTime().ToString("ddd HH:mm", CultureInfo.CurrentCulture),
            record.AppName,
            $"{record.WordCount} words",
            record.WasCleaned ? "cleaned up by Claude" : null,
        ];
        var header = new DockPanel();
        if (record.Text.Length > 0)
        {
            var copy = CopyButton(record.Text);
            DockPanel.SetDock(copy, Dock.Right);
            header.Children.Add(copy);
        }
        var line = Ui.Horizontal(Theme.Window.LabelGap, [.. facts.OfType<string>().Select(Ui.Secondary)]);
        line.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(line);

        var row = Ui.Stack(Theme.Window.RowSpacing / 2, header, Body(record, showsOriginal));
        if (showsOriginal && record.RawText.Length > 0 && record.RawText != record.Text)
        {
            row.Children.Add(new Expander { Header = "Original", Content = Selectable(record.RawText) });
        }
        return row;
    }

    // shows "Copied" for a moment so the click visibly did something; a one-shot timer, stopped on tick
    private static Button CopyButton(string text)
    {
        var button = new Button { Content = CopyIcon(), ToolTip = "Copy text" };
        var reset = new DispatcherTimer { Interval = Theme.Window.CopiedFeedback };
        reset.Tick += (_, _) =>
        {
            reset.Stop();
            button.Content = CopyIcon();
        };
        button.Click += (_, _) =>
        {
            if (TextInserter.Copy(text))
            {
                button.Content = Ui.Done("Copied");
            }
            else
            {
                var failed = Ui.Caution("Clipboard busy, try again");
                failed.FontSize = Theme.Window.CaptionFontSize;
                button.Content = failed;
            }
            reset.Stop();
            reset.Start();
        };
        return button;
    }

    private static TextBlock CopyIcon() => Ui.Glyph(Theme.Icons.Copy, Theme.Window.CaptionFontSize);

    private static UIElement Body(DictationRecord record, bool showsAll)
    {
        if (record.Text.Length == 0)
        {
            return Ui.Secondary("Text not saved");
        }
        var text = Selectable(record.Text);
        if (!showsAll)
        {
            text.MaxHeight = Theme.Window.PreviewHeight;
        }
        return text;
    }

    // a borderless read-only text box, so the text can be selected and copied in parts
    private static TextBox Selectable(string text) => new()
    {
        Text = text,
        IsReadOnly = true,
        TextWrapping = TextWrapping.Wrap,
        BorderThickness = new Thickness(0),
        Background = null,
        Padding = new Thickness(0),
    };
}
