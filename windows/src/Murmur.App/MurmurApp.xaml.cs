using System.Windows;
using Murmur.App.Overlay;
using Murmur.App.Panes;
using Murmur.Core;
using Murmur.Services;

namespace Murmur.App;

/// <summary>Owns the long-lived objects and wires them up at launch.</summary>
public sealed partial class MurmurApp : Application, IDisposable
{
    private const string Category = "app";
    // one murmur per user session: a second copy would install a second keyboard hook
    private const string InstanceMutexName = @"Local\Murmur.SingleInstance";
    // starting the exe again while it runs opens the window of the running copy
    private const string OpenWindowEventName = @"Local\Murmur.OpenWindow";

    private Mutex? _instance;
    private EventWaitHandle? _openWindowSignal;
    private RegisteredWaitHandle? _openWindowWait;
    private TrayIcon? _tray;
    private DictationController? _dictation;
    private AppServices? _services;
    private MainWindow? _window;
    private bool _isUpdating;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        HandleCrashes();
        var isAfterUpdate = e.Args.Contains(AppConstants.Update.RestartArgument);
        var isFirstInstance = AcquireInstance(waitForPrevious: isAfterUpdate);
        _openWindowSignal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, OpenWindowEventName);
        if (!isFirstInstance)
        {
            Log.Info(Category, "already running, opening its window instead");
            _openWindowSignal.Set();
            Shutdown();
            return;
        }
        Log.Info(Category, $"started {DiagnosticsReport.Version} on windows {Environment.OSVersion.Version}");
        Updater.CleanUp();
        // the wait costs nothing until the event is set
        _openWindowWait = ThreadPool.RegisterWaitForSingleObject(
            _openWindowSignal,
            (_, _) => Dispatcher.BeginInvoke(() => OpenWindow(null)),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        var settings = new SettingsStore();
        var history = new HistoryStore();
        var sync = new StatsSyncController(history, settings);
        var dictation = new DictationController(settings, Dispatcher);
        var pill = new PillWindow(settings);
        var tray = new TrayIcon(
            openWindow: () => OpenWindow(null),
            checkForUpdates: CheckForUpdates,
            copyDiagnostics: CopyDiagnostics,
            quit: Shutdown);
        _services = new AppServices(settings, dictation, history, sync, Navigate: _ => { }, CopyDiagnostics, CheckForUpdates);
        _dictation = dictation;
        _tray = tray;

        dictation.StateChanged += (_, _) => pill.Render(dictation.State);
        dictation.LevelsChanged += (_, _) => pill.ShowLevels(dictation.Levels);
        dictation.CopiedToClipboard += (_, _) => tray.ShowBalloon(UserMessages.CopiedTitle, UserMessages.CopiedBody);
        dictation.Delivered += (_, delivered) =>
        {
            history.Record(delivered.Transcript, delivered.SpeakingSeconds, delivered.AppName, settings.Current.IsHistoryEnabled);
            // in the background: the paste already happened, sync never holds it up
            _ = sync.UploadPendingAsync();
        };
        pill.Show();

        var isHotkeyActive = dictation.Activate();
        tray.SetHint(isHotkeyActive, KeyNames.Of(settings.Current.Hotkey));
        settings.Changed += (_, _) => tray.SetHint(dictation.IsHotkeyActive, KeyNames.Of(settings.Current.Hotkey));
        if (!isHotkeyActive)
        {
            tray.ShowBalloon(UserMessages.HookFailedTitle, UserMessages.HookFailedBody);
        }
        // dictations made while offline go up now
        _ = sync.UploadPendingAsync();
        // first start: nothing works without a deepgram key, so show where it goes
        if (!CredentialStore.Contains(AppConstants.Credentials.DeepgramTarget))
        {
            OpenWindow(Pane.Keys);
        }
        if (isAfterUpdate)
        {
            tray.ShowBalloon(UserMessages.UpdatedTitle, UserMessages.UpdatedBody);
        }
    }

    // after an update the previous copy is still quitting for a moment: wait for it to let go
    private bool AcquireInstance(bool waitForPrevious)
    {
        var deadline = DateTimeOffset.UtcNow + AppConstants.Update.RestartWait;
        while (true)
        {
            _instance = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
            if (isFirstInstance || !waitForPrevious || DateTimeOffset.UtcNow >= deadline)
            {
                return isFirstInstance;
            }
            _instance.Dispose();
            Thread.Sleep(AppConstants.Update.RestartPollInterval);
        }
    }

    private void CheckForUpdates() => _ = CheckForUpdatesAsync();

    private async Task CheckForUpdatesAsync()
    {
        if (_isUpdating || _tray is not { } tray)
        {
            return;
        }
        _isUpdating = true;
        try
        {
            var (newer, isReachable) = await Updater.CheckAsync();
            if (!isReachable)
            {
                tray.ShowBalloon(UserMessages.UpdateUnreachableTitle, UserMessages.UpdateUnreachableBody);
                return;
            }
            if (newer is null)
            {
                tray.ShowBalloon(UserMessages.UpToDateTitle, $"You have the newest version ({Updater.CurrentVersion}).");
                return;
            }
            var answer = MessageBox.Show(
                $"Murmur {newer.Version} is available (you have {Updater.CurrentVersion}). Update now? Murmur restarts, "
                + "your keys, settings and history stay.",
                UserMessages.UpdateAvailableTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }
            tray.ShowBalloon(UserMessages.UpdateDownloadingTitle, UserMessages.UpdateDownloadingBody);
            if (!await Updater.InstallAsync(newer))
            {
                tray.ShowBalloon(UserMessages.UpdateFailedTitle, UserMessages.UpdateFailedBody);
                return;
            }
            // let go of the hook, the mic and the single-instance lock before the new copy starts
            Dispose();
            Updater.StartNewVersion();
            Shutdown();
        }
        finally
        {
            _isUpdating = false;
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
        _openWindowWait?.Unregister(null);
        _openWindowWait = null;
        _openWindowSignal?.Dispose();
        _openWindowSignal = null;
        _instance?.Dispose();
        _instance = null;
    }

    private void OpenWindow(Pane? pane)
    {
        if (_services is not { } services)
        {
            return;
        }
        if (_window is null)
        {
            _window = new MainWindow(services, pane ?? Pane.Home);
            _window.Closed += (_, _) => _window = null;
            _window.Show();
        }
        else if (pane is { } chosen)
        {
            _window.Navigate(chosen);
        }
        // a tray app isn't in the foreground, so bring the window forward explicitly
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }
        _window.Activate();
    }

    private void CopyDiagnostics()
    {
        var report = DiagnosticsReport.Build(
        [
            new("hotkey hook", _dictation?.IsHotkeyActive == true ? "running" : "not running"),
            new("state", _dictation?.State.ToString() ?? "none"),
            new("default mic", AudioDevices.DefaultInputName()),
            new("mics", string.Join(", ", AudioDevices.Inputs().Select(device => device.Name))),
            new("cleanup", _services?.Settings.Current.IsCleanupEnabled == true ? "on" : "off"),
            new("history", $"{_services?.History.Current.Records.Count ?? 0} dictations"),
            new("sync", _services?.Sync.Status.ToString() ?? "none"),
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
