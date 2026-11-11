import AppKit
import MurmurCore
import MurmurServices
import SwiftUI

/// The menu bar menu: setup hints first (only when something is missing), then quick settings.
struct MenuContent: View {
    let dictation: DictationController
    @Bindable var settingsStore: SettingsStore
    @Environment(\.openWindow) private var openWindow

    var body: some View {
        if !dictation.isHotkeyActive {
            Button("Turn On \(hotkeyName) Shortcut (Needs Accessibility)…") { dictation.enableHotkey() }
        }
        if settingsStore.settings.hotkey == .function && !Permissions.isFnKeyFree {
            Button("Set “Press 🌐 key to” to “Do Nothing”…") { Permissions.openKeyboardSettings() }
        }
        if !Permissions.isMicrophoneAuthorized {
            Button("Allow Microphone Access…") { Permissions.openMicrophoneSettings() }
        }
        if settingsStore.settings.isCleanupEnabled
            && !Keychain.contains(account: AppConstants.Keychain.anthropicAccount)
        {
            Button("Add Claude Key for Cleanup…", action: showMainWindow)
        }
        if dictation.isHotkeyActive {
            Text("Hold \(hotkeyName) to dictate, \(hotkeyName) + space to lock")
        }
        Divider()
        Toggle("Clean Up with Claude", isOn: $settingsStore.settings.isCleanupEnabled)
        Picker("Microphone", selection: $settingsStore.settings.inputDeviceUID) {
            Text("System Default").tag(String?.none)
            // read here, not cached: a cheap core audio query, and a newly plugged-in mic shows up
            ForEach(CoreAudioDevice.inputDevices()) { device in
                Text(device.name).tag(String?.some(device.uid))
            }
        }
        Toggle("Boost Mic While Recording", isOn: $settingsStore.settings.isMicBoostEnabled)
        Picker("Overlay Position", selection: $settingsStore.settings.overlayPosition) {
            ForEach(OverlayPosition.allCases, id: \.self) { position in
                Text(position.title).tag(position)
            }
        }
        // read on each menu open, so a change made in system settings shows up
        Toggle("Open at Login", isOn: Binding(get: { LoginItem.isEnabled }, set: LoginItem.setEnabled))
        Divider()
        Button("Open Murmur…", action: showMainWindow)
            .keyboardShortcut(",")
        Button("Restart Murmur") { Relaunch.now() }
            .keyboardShortcut("r")
        Button("Quit Murmur") { NSApplication.shared.terminate(nil) }
            .keyboardShortcut("q")
    }

    private var hotkeyName: String {
        KeyName.of(settingsStore.settings.hotkey)
    }

    private func showMainWindow() {
        // a menu bar app isn't active, so bring the window to the front
        NSApp.activate()
        openWindow(id: MainWindow.id)
    }
}
