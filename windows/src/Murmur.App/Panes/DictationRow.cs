using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Murmur.Core;

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
            var copy = new Button { Content = Ui.Glyph(Theme.Icons.Copy, Theme.Window.CaptionFontSize), ToolTip = "Copy text" };
            copy.Click += (_, _) => Clipboard.SetText(record.Text);
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
