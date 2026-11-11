using System.Windows;
using System.Windows.Controls;
using Murmur.App.Panes;
using Murmur.Core;

namespace Murmur.App;

/// <summary>
/// The app window: sidebar on the left, the selected pane on the right. Opened from the tray icon;
/// closing it only closes the window, Murmur keeps running in the tray.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly ListBox _sidebar = new();
    private readonly ContentControl _content = new();

    public MainWindow(AppServices services, Pane initial)
    {
        _services = services with { Navigate = Navigate };
        // implicit styles only match the exact type: without this a subclass misses the fluent theme
        // (white background with dark-mode text)
        SetResourceReference(StyleProperty, typeof(Window));
        Title = AppConstants.AppName;
        Width = Theme.Window.Width;
        Height = Theme.Window.Height;
        MinWidth = Theme.Window.MinWidth;
        MinHeight = Theme.Window.MinHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        foreach (var pane in PaneInfo.All)
        {
            var label = Ui.Horizontal(Theme.Window.LabelGap, Ui.Glyph(pane.Glyph(), Theme.Window.HeaderFontSize), Ui.Text(pane.Title()));
            _sidebar.Items.Add(new ListBoxItem { Content = label, Tag = pane, Padding = new Thickness(Theme.Window.SidebarItemPadding) });
        }
        _sidebar.SelectionChanged += (_, _) => Render();
        _sidebar.Margin = new Thickness(Theme.Window.SidebarItemPadding);

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Theme.Window.SidebarWidth) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.Children.Add(_sidebar);
        Grid.SetColumn(_content, 1);
        layout.Children.Add(_content);
        Content = layout;

        // home and history show live data: re-render them when it changes, and stop listening once closed
        _services.History.Changed += OnDataChanged;
        _services.Sync.Changed += OnDataChanged;
        Closed += (_, _) =>
        {
            _services.History.Changed -= OnDataChanged;
            _services.Sync.Changed -= OnDataChanged;
        };
        Navigate(initial);
    }

    public Pane Current => _sidebar.SelectedItem is ListBoxItem { Tag: Pane pane } ? pane : Pane.Home;

    /// <summary>Switches to a pane (and renders it fresh, even if it is already shown).</summary>
    public void Navigate(Pane pane)
    {
        var item = _sidebar.Items.OfType<ListBoxItem>().First(candidate => candidate.Tag is Pane tagged && tagged == pane);
        if (_sidebar.SelectedItem == item)
        {
            Render();
            return;
        }
        _sidebar.SelectedItem = item;
    }

    private void Render()
    {
        var pane = Current;
        _content.Content = pane switch
        {
            Pane.Home => HomePane.Build(_services),
            Pane.History => HistoryPane.Build(_services),
            Pane.Vocabulary => VocabularyPane.Build(_services),
            Pane.Cleanup => CleanupPane.Build(_services),
            Pane.Audio => AudioPane.Build(_services),
            Pane.Overlay => OverlayPane.Build(_services),
            _ => KeysPane.Build(_services),
        };
        if (pane == Pane.Home)
        {
            // fetches every device's numbers; the pane re-renders when they arrive
            _ = _services.Sync.RefreshAsync();
        }
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        // only the panes that show history or synced numbers; the others hold half-typed input
        if (Current is Pane.Home or Pane.History)
        {
            _content.Content = Current == Pane.Home ? HomePane.Build(_services) : HistoryPane.Build(_services);
        }
    }
}
