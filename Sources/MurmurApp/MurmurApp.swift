import AppKit
import MurmurServices
import SwiftUI

@main
struct MurmurApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        MenuBarExtra("Murmur", systemImage: "waveform") {
            MenuContent(dictation: appDelegate.dictation, settingsStore: appDelegate.settingsStore)
        }
        Window("Murmur", id: MainWindow.id) {
            MainWindow(
                settingsStore: appDelegate.settingsStore,
                historyStore: appDelegate.historyStore,
                statsSync: appDelegate.statsSync
            )
        }
        .modelContainer(appDelegate.historyStore.container)
        // opened from the menu bar, never on its own at launch
        .defaultLaunchBehavior(.suppressed)
    }
}

/// Owns the long-lived objects and wires them up at launch.
@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let settingsStore: SettingsStore
    let historyStore: HistoryStore
    let statsSync: StatsSyncController
    let dictation: DictationController
    private var overlay: OverlayPanelController?

    override init() {
        let settingsStore = SettingsStore()
        let historyStore = HistoryStore()
        let statsSync = StatsSyncController(historyStore: historyStore, settingsStore: settingsStore)
        self.settingsStore = settingsStore
        self.historyStore = historyStore
        self.statsSync = statsSync
        self.dictation = DictationController(
            settingsStore: settingsStore,
            historyStore: historyStore,
            statsSync: statsSync
        )
        super.init()
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        dictation.activate()
        overlay = OverlayPanelController(dictation: dictation, settingsStore: settingsStore)
        Task { await Notifier.requestAuthorization() }
        // dictations made while offline go up now
        Task { await statsSync.uploadPending() }
    }

    func applicationWillTerminate(_ notification: Notification) {
        dictation.shutdown()
    }
}
