using System.Windows;
using Murmur.App.Overlay;
using Murmur.Core;
using Murmur.Services;

namespace Murmur.App;

/// <summary>Owns the long-lived objects and wires them up at launch.</summary>
public sealed partial class MurmurApp : Application, IDisposable
{
    private const string Category = "app";
    // one murmur per user session: a second copy would install a second caps lock hook
    private const string InstanceMutexName = @"Local\Murmur.SingleInstance";

    private Mutex? _instance;
    private TrayIcon? _tray;
    private PillWindow? _pill;
    private DictationController? _dictation;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        HandleCrashes();
        _instance = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            Log.Info(Category, "already running, exiting");
            Shutdown();
            return;
        }
        Log.Info(Category, $"started {DiagnosticsReport.Version} on windows {Environment.OSVersion.Version}");

        var settings = new SettingsStore();
        var dictation = new DictationController(settings, Dispatcher);
        var pill = new PillWindow(settings);
        var tray = new TrayIcon(openWindow: OpenWindow, copyDiagnostics: CopyDiagnostics, quit: Shutdown);
        _dictation = dictation;
        _pill = pill;
        _tray = tray;

        dictation.StateChanged += (_, _) => pill.Render(dictation.State);
        dictation.LevelsChanged += (_, _) => pill.ShowLevels(dictation.Levels);
        dictation.CopiedToClipboard += (_, _) => tray.ShowBalloon(UserMessages.CopiedTitle, UserMessages.CopiedBody);
        pill.Show();

        var isHotkeyActive = dictation.Activate();
        tray.SetHotkeyActive(isHotkeyActive);
        if (!isHotkeyActive)
        {
            tray.ShowBalloon(UserMessages.HookFailedTitle, UserMessages.HookFailedBody);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info(Category, "quit");
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        _dictation?.Dispose();
        _dictation = null;
        _tray?.Dispose();
        _tray = null;
        _instance?.Dispose();
        _instance = null;
    }

    private static void OpenWindow()
    {
        // the main window arrives with the app window phase
    }

    private void CopyDiagnostics()
    {
        var report = DiagnosticsReport.Build(
        [
            new("caps lock hook", _dictation?.IsHotkeyActive == true ? "running" : "not running"),
            new("state", _dictation?.State.ToString() ?? "none"),
            new("default mic", AudioDevices.DefaultInputName()),
            new("mics", string.Join(", ", AudioDevices.Inputs().Select(device => device.Name))),
            new("overlay", _pill is null ? "missing" : "shown"),
        ]);
        try
        {
            Clipboard.SetText(report);
            _tray?.ShowBalloon(UserMessages.DiagnosticsCopiedTitle, UserMessages.DiagnosticsCopiedBody);
        }
        catch (System.Runtime.InteropServices.ExternalException exception)
        {
            Log.Error(Category, "copying diagnostics failed", exception);
        }
    }

    // a crash on someone else's laptop is only fixable if it lands in the log
    private void HandleCrashes()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Crash("ui", args.Exception);
            // keep running: losing the tray and the hotkey is worse than one failed action
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Log.Crash("domain", exception);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Crash("task", args.Exception);
            args.SetObserved();
        };
    }
}
