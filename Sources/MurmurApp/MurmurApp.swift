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
        Settings {
            SettingsView(settingsStore: appDelegate.settingsStore)
        }
    }
}

/// Owns the long-lived objects and wires them up at launch.
@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let settingsStore: SettingsStore
    let dictation: DictationController
    private var overlay: OverlayPanelController?

    override init() {
        let settingsStore = SettingsStore()
        self.settingsStore = settingsStore
        self.dictation = DictationController(settingsStore: settingsStore)
        super.init()
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        dictation.activate()
        overlay = OverlayPanelController(dictation: dictation, settingsStore: settingsStore)
        Task { await Notifier.requestAuthorization() }
    }

    func applicationWillTerminate(_ notification: Notification) {
        dictation.shutdown()
    }
}
