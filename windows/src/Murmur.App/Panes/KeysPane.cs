using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Murmur.Core;
using Murmur.Services;

namespace Murmur.App.Panes;

/// <summary>API keys (in the Credential Manager), stats sync, startup and the help for remote debugging.</summary>
public static class KeysPane
{
    private const string MicrophonePrivacySettings = "ms-settings:privacy-microphone";

    public static UIElement Build(AppServices services) => Ui.Page(
        Pane.Keys.Title(),
        Ui.Section(
            "API keys",
            "Stored in the Windows Credential Manager, for your user only. Never leaves this PC except to call that service.",
            ApiKeyField.Build("Deepgram", AppConstants.Credentials.DeepgramTarget),
            ApiKeyField.Build("Claude", AppConstants.Credentials.AnthropicTarget)),
        StatsSyncSection(services),
        Ui.Section(
            "Microphone access",
            "If dictation says \"Allow microphone access\", turn on \"Let desktop apps access your microphone\".",
            Ui.Button("Open microphone privacy settings", OpenMicrophoneSettings)),
        Ui.Section(
            "Startup",
            "Uses the copy of Murmur.exe that is running now. Keep it in one place (not Downloads) if you turn this on.",
            Ui.Toggle("Start Murmur with Windows", StartWithWindows.IsEnabled, StartWithWindows.SetEnabled)),
        Ui.Section(
            "Help",
            "Version, setup and the newest log lines (never keys or what you said), ready to paste into a chat.",
            Ui.Horizontal(Theme.Window.RowSpacing, Ui.Button("Copy diagnostics", services.CopyDiagnostics), Ui.Secondary(DiagnosticsReport.Version))));

    private static StackPanel StatsSyncSection(AppServices services)
    {
        var status = new ContentControl { Content = StatusText(services.Sync.Status) };
        var test = Ui.Button("Test connection", () => { });
        test.Click += async (_, _) =>
        {
            test.IsEnabled = false;
            status.Content = Ui.Secondary("Testing…");
            await services.Sync.TestConnectionAsync();
            status.Content = StatusText(services.Sync.Status);
            test.IsEnabled = true;
        };

        var deviceName = new TextBox { Width = Theme.Window.FieldWidth, Text = services.Settings.Current.SyncDeviceName };
        deviceName.LostFocus += (_, _) => services.Settings.Update(current => current with { SyncDeviceName = deviceName.Text.Trim() });

        var statusRow = new DockPanel();
        DockPanel.SetDock(test, Dock.Right);
        statusRow.Children.Add(test);
        statusRow.Children.Add(status);
        return Ui.Section(
            "Stats sync",
            "Combines Home's numbers across your devices (the Mac uses the same database). Only counts are shared "
            + "(time, words, seconds spoken), never what you said.",
            ApiKeyField.Build("Neon database URL", AppConstants.Credentials.NeonTarget),
            Ui.Row("This device's name", deviceName),
            Ui.Secondary($"Empty uses \"{Environment.MachineName}\"."),
            statusRow);
    }

    private static TextBlock StatusText(SyncStatus status) => status switch
    {
        SyncStatus.SyncedStatus synced => Ui.Success($"Connected, synced {synced.At:HH:mm}"),
        SyncStatus.FailedStatus failed => Ui.Caution(failed.Failure.Message()),
        _ => Ui.Secondary("Not set up"),
    };

    private static void OpenMicrophoneSettings() =>
        Process.Start(new ProcessStartInfo(MicrophonePrivacySettings) { UseShellExecute = true })?.Dispose();
}
