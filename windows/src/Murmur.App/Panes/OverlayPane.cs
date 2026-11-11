using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Murmur.Core;

namespace Murmur.App.Panes;

/// <summary>How the Caps Lock shortcut works, and where the pill sits.</summary>
public static class OverlayPane
{
    // the enum lists the positions row by row, three per row, which is the order the grid fills
    private const int GridSize = 3;

    public static UIElement Build(AppServices services) => Ui.Page(
        Pane.Overlay.Title(),
        Ui.Section(
            "Dictate",
            footer: "Caps Lock no longer switches capitals while Murmur runs. Shift still types them.",
            Ui.Row("Hold Caps Lock", Ui.Secondary("Talk, release to paste")),
            Ui.Row("Caps Lock + Space", Ui.Secondary("Lock recording, hands free")),
            Ui.Row("Caps Lock (while locked)", Ui.Secondary("Stop and paste")),
            Ui.Row("Caps Lock + another key", Ui.Secondary("Cancels")),
            Ui.Row("A quick tap", Ui.Secondary("Ignored"))),
        Ui.Section("Pill position", footer: null, PositionPicker(services)));

    /// <summary>A miniature screen with the nine places the pill can sit; the chosen one shows the pill.</summary>
    private static Border PositionPicker(AppServices services)
    {
        var grid = new UniformGrid { Rows = GridSize, Columns = GridSize };
        void Refresh()
        {
            grid.Children.Clear();
            foreach (var position in Enum.GetValues<OverlayPosition>())
            {
                grid.Children.Add(Cell(position, position == services.Settings.Current.OverlayPosition, () =>
                {
                    services.Settings.Update(current => current with { OverlayPosition = position });
                    Refresh();
                }));
            }
        }
        Refresh();
        var screen = new Border
        {
            Width = Theme.PositionPicker.ScreenWidth,
            Height = Theme.PositionPicker.ScreenHeight,
            CornerRadius = new CornerRadius(Theme.PositionPicker.ScreenCornerRadius),
            Padding = new Thickness(Theme.PositionPicker.CellSpacing),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = grid,
        };
        screen.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
        return screen;
    }

    private static Button Cell(OverlayPosition position, bool isSelected, Action choose)
    {
        FrameworkElement mark = isSelected
            ? new Border
            {
                Width = Theme.PositionPicker.PillWidth,
                Height = Theme.PositionPicker.PillHeight,
                CornerRadius = new CornerRadius(Theme.PositionPicker.PillHeight / 2),
                Background = Theme.Pill.Background,
            }
            : new Ellipse
            {
                Width = Theme.PositionPicker.DotSize,
                Height = Theme.PositionPicker.DotSize,
                Fill = Brushes.Gray,
                Opacity = Theme.PositionPicker.IdleOpacity,
            };
        mark.HorizontalAlignment = position.Horizontal() switch
        {
            HorizontalEdge.Leading => HorizontalAlignment.Left,
            HorizontalEdge.Center => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Right,
        };
        mark.VerticalAlignment = position.Vertical() switch
        {
            VerticalEdge.Top => VerticalAlignment.Top,
            VerticalEdge.Middle => VerticalAlignment.Center,
            _ => VerticalAlignment.Bottom,
        };
        var cell = new Button
        {
            Content = mark,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(Theme.PositionPicker.CellSpacing / 2),
            ToolTip = position.ToString(),
        };
        cell.Click += (_, _) => choose();
        return cell;
    }
}
