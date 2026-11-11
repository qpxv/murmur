using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Murmur.Core;
using Murmur.Services;

namespace Murmur.App.Panes;

/// <summary>Which key dictates and how it works, and where the pill sits.</summary>
public static class OverlayPane
{
    // the enum lists the positions row by row, three per row, which is the order the grid fills
    private const int GridSize = 3;

    public static UIElement Build(AppServices services) => Ui.Page(
        Pane.Overlay.Title(),
        DictateSection(services),
        Ui.Section("Pill position", footer: null, PositionPicker(services)));

    /// <summary>The dictation key with Record… and Reset, and what holding it does. Redrawn when the key changes.</summary>
    private static Border DictateSection(AppServices services)
    {
        var container = new Border();
        var isRecording = false;
        string? rejection = null;
        void Refresh()
        {
            var hotkey = services.Settings.Current.Hotkey;
            var name = KeyNames.Of(hotkey);
            var record = Ui.Button(isRecording ? "Cancel" : "Record…", () =>
            {
                if (isRecording)
                {
                    services.Dictation.CancelHotkeyRecording();
                    return;
                }
                isRecording = true;
                rejection = null;
                Refresh();
                services.Dictation.RecordHotkey(result =>
                {
                    isRecording = false;
                    rejection = result is HotkeyRecording.RejectedState rejected ? rejected.Reason.Message() : null;
                    Refresh();
                });
            });
            record.IsEnabled = services.Dictation.IsHotkeyActive;
            var reset = Ui.Button("Reset", () =>
            {
                rejection = null;
                services.Dictation.ChangeHotkey(Hotkey.CapsLock);
                Refresh();
            });
            reset.Visibility = hotkey == Hotkey.CapsLock || isRecording ? Visibility.Collapsed : Visibility.Visible;
            UIElement current = isRecording ? Ui.Secondary("Press a key…") : Ui.Text(name);
            UIElement[] parts = rejection is null ? [current, record, reset] : [Ui.Caution(rejection), current, record, reset];
            UIElement keyControls = Ui.Horizontal(Theme.Window.RowSpacing, parts);
            container.Child = Ui.Section(
                "Dictate",
                footer: Footer(hotkey, name),
                Ui.Row("Dictation key", keyControls),
                Ui.Row($"Hold {name}", Ui.Secondary("Talk, release to paste")),
                Ui.Row($"{name} + Space", Ui.Secondary("Lock recording, hands free")),
                Ui.Row($"{name} (while locked)", Ui.Secondary("Stop and paste")),
                Ui.Row($"{name} + another key", Ui.Secondary("Cancels")),
                Ui.Row("A quick tap", Ui.Secondary("Ignored")));
        }
        Refresh();
        // leaving the pane mid-recording must not leave the keyboard swallowed
        container.Unloaded += (_, _) => services.Dictation.CancelHotkeyRecording();
        return container;
    }

    private static string Footer(Hotkey hotkey, string name) =>
        hotkey == Hotkey.CapsLock
            ? "Caps Lock no longer switches capitals while Murmur runs (Shift still types them). Any single key works, Escape cancels recording."
            : $"{name} only dictates while Murmur runs, it does nothing else. Any single key works, Escape cancels recording.";

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
