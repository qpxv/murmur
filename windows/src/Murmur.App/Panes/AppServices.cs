using Murmur.Services;

namespace Murmur.App.Panes;

/// <summary>The long-lived objects the panes read and change, handed over in one piece.</summary>
/// <param name="Settings">The one settings value.</param>
/// <param name="Dictation">Runs dictations; also changes and records the dictation key.</param>
/// <param name="History">The local dictation history.</param>
/// <param name="Sync">Shared stats across devices.</param>
/// <param name="Navigate">Switches the window to another pane (e.g. "Add key" jumps to Keys).</param>
/// <param name="CopyDiagnostics">Puts the diagnostics report on the clipboard.</param>
/// <param name="CheckForUpdates">Looks for a newer release and offers to install it.</param>
public sealed record AppServices(
    SettingsStore Settings,
    DictationController Dictation,
    HistoryStore History,
    StatsSyncController Sync,
    Action<Pane> Navigate,
    Action CopyDiagnostics,
    Action CheckForUpdates);
