using System.Windows;
using System.Windows.Controls;

namespace Murmur.App.Panes;

/// <summary>
/// The building blocks every pane is made of (page, section card, labelled row), so they all look
/// the same and every size comes from <see cref="Theme"/>. Colors are the Fluent theme's own
/// resources, so light and dark mode follow Windows.
/// </summary>
public static class Ui
{
    // keys of the wpf fluent theme's brushes
    private const string CardBrush = "CardBackgroundFillColorDefaultBrush";
    private const string CardStrokeBrush = "CardStrokeColorDefaultBrush";
    private const string SecondaryTextBrush = "TextFillColorSecondaryBrush";
    private const string CautionBrush = "SystemFillColorCautionBrush";
    private const string SuccessBrush = "SystemFillColorSuccessBrush";

    /// <summary>A scrolling page with a title and its sections.</summary>
    public static ScrollViewer Page(string title, params UIElement[] sections)
    {
        var content = Stack(Theme.Window.SectionSpacing, [Text(title, Theme.Window.TitleFontSize, FontWeights.SemiBold), .. sections]);
        content.Margin = new Thickness(Theme.Window.ContentPadding);
        return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>A titled card of rows with an optional explanation underneath.</summary>
    public static StackPanel Section(string? header, string? footer, params UIElement[] rows)
    {
        var card = new Border
        {
            Padding = new Thickness(Theme.Window.CardPadding),
            CornerRadius = new CornerRadius(Theme.Window.CardCornerRadius),
            BorderThickness = new Thickness(1),
            Child = Stack(Theme.Window.RowSpacing, rows),
        };
        card.SetResourceReference(Border.BackgroundProperty, CardBrush);
        card.SetResourceReference(Border.BorderBrushProperty, CardStrokeBrush);
        UIElement?[] parts =
        [
            header is null ? null : Text(header, Theme.Window.HeaderFontSize, FontWeights.SemiBold),
            card,
            footer is null ? null : Secondary(footer),
        ];
        return Stack(Theme.Window.RowSpacing / 2, [.. parts.OfType<UIElement>()]);
    }

    /// <summary>A label on the left and its control on the right.</summary>
    public static Grid Row(string label, UIElement control)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = Text(label);
        text.VerticalAlignment = VerticalAlignment.Center;
        text.Margin = new Thickness(0, 0, Theme.Window.LabelGap, 0);
        grid.Children.Add(text);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    public static StackPanel Stack(double spacing, params UIElement[] children) => Line(Orientation.Vertical, spacing, children);

    public static StackPanel Horizontal(double spacing, params UIElement[] children) => Line(Orientation.Horizontal, spacing, children);

    public static TextBlock Text(string text, double? size = null, FontWeight? weight = null)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        if (size is { } fontSize)
        {
            block.FontSize = fontSize;
        }
        if (weight is { } fontWeight)
        {
            block.FontWeight = fontWeight;
        }
        return block;
    }

    public static TextBlock Secondary(string text)
    {
        var block = Text(text, Theme.Window.CaptionFontSize);
        block.SetResourceReference(TextBlock.ForegroundProperty, SecondaryTextBrush);
        return block;
    }

    /// <summary>A warning line (orange in both themes).</summary>
    public static TextBlock Caution(string text) => Colored(Text(text), CautionBrush);

    /// <summary>A success line (green in both themes).</summary>
    public static TextBlock Success(string text) => Colored(Text(text), SuccessBrush);

    public static TextBlock Glyph(string glyph, double size)
    {
        var block = new TextBlock { Text = glyph, FontFamily = Theme.Icons.Font, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        block.SetResourceReference(TextBlock.ForegroundProperty, SecondaryTextBrush);
        return block;
    }

    public static CheckBox Toggle(string label, bool isOn, Action<bool> changed)
    {
        var box = new CheckBox { Content = label, IsChecked = isOn };
        box.Checked += (_, _) => changed(true);
        box.Unchecked += (_, _) => changed(false);
        return box;
    }

    public static Button Button(string label, Action clicked, bool isAccent = false)
    {
        var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left };
        if (isAccent)
        {
            button.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle");
        }
        button.Click += (_, _) => clicked();
        return button;
    }

    /// <summary>An empty state: what's missing and how to fill it.</summary>
    public static StackPanel Empty(string title, string explanation)
    {
        var panel = Stack(Theme.Window.RowSpacing / 2, Text(title, Theme.Window.HeaderFontSize, FontWeights.SemiBold), Secondary(explanation));
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Margin = new Thickness(0, Theme.Window.SectionSpacing, 0, Theme.Window.SectionSpacing);
        return panel;
    }

    private static TextBlock Colored(TextBlock block, string brush)
    {
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }

    private static StackPanel Line(Orientation orientation, double spacing, UIElement[] children)
    {
        var panel = new StackPanel { Orientation = orientation };
        foreach (var child in children)
        {
            // wpf's StackPanel has no spacing: every child after the first gets a leading margin
            if (panel.Children.Count > 0 && child is FrameworkElement element)
            {
                element.Margin = orientation == Orientation.Vertical
                    ? new Thickness(element.Margin.Left, spacing, element.Margin.Right, element.Margin.Bottom)
                    : new Thickness(spacing, element.Margin.Top, element.Margin.Right, element.Margin.Bottom);
            }
            panel.Children.Add(child);
        }
        return panel;
    }
}
