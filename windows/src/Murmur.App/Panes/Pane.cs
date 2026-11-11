namespace Murmur.App.Panes;

/// <summary>The sections of the main window's sidebar, in display order (same as the Mac).</summary>
public enum Pane
{
    Home,
    History,
    Vocabulary,
    Cleanup,
    Audio,
    Overlay,
    Keys,
}

public static class PaneInfo
{
    public static IReadOnlyList<Pane> All { get; } = Enum.GetValues<Pane>();

    public static string Title(this Pane pane) => pane switch
    {
        Pane.Home => "Home",
        Pane.History => "History",
        Pane.Vocabulary => "Vocabulary",
        Pane.Cleanup => "Cleanup",
        Pane.Audio => "Audio",
        Pane.Overlay => "Shortcut & Overlay",
        Pane.Keys => "Keys & Startup",
        _ => throw new ArgumentOutOfRangeException(nameof(pane), pane, null),
    };

    public static string Glyph(this Pane pane) => pane switch
    {
        Pane.Home => Theme.Icons.Home,
        Pane.History => Theme.Icons.History,
        Pane.Vocabulary => Theme.Icons.Vocabulary,
        Pane.Cleanup => Theme.Icons.Cleanup,
        Pane.Audio => Theme.Icons.Microphone,
        Pane.Overlay => Theme.Icons.Keyboard,
        Pane.Keys => Theme.Icons.Keys,
        _ => throw new ArgumentOutOfRangeException(nameof(pane), pane, null),
    };
}
