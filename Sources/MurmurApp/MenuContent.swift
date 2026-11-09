import AppKit
import MurmurCore
import MurmurServices
import SwiftUI

/// The menu bar menu: setup hints first (only when something is missing), then quick settings.
struct MenuContent: View {
    let dictation: DictationController
    @Bindable var settingsStore: SettingsStore
    @Environment(\.openSettings) private var openSettings

    var body: some View {
        if !dictation.isHotkeyActive {
            Button("Turn On fn Hotkey (Needs Accessibility)…") { dictation.enableHotkey() }
        }
        if !Permissions.isFnKeyFree {
            Button("Set “Press 🌐 key to” to “Do Nothing”…") { Permissions.openKeyboardSettings() }
        }
        if !Permissions.isMicrophoneAuthorized {
            Button("Allow Microphone Access…") { Permissions.openMicrophoneSettings() }
        }
        if dictation.isHotkeyActive {
            Text("Hold fn to dictate, fn + space to lock")
        }
        Divider()
        Toggle("Boost Mic While Recording", isOn: $settingsStore.settings.isMicBoostEnabled)
        Picker("Overlay Position", selection: $settingsStore.settings.overlayPosition) {
            ForEach(OverlayPosition.allCases, id: \.self) { position in
                Text(position.title).tag(position)
            }
        }
        Divider()
        Button("Settings…") {
            // a menu bar app isn't active, so bring the settings window to the front
            NSApp.activate()
            openSettings()
        }
        .keyboardShortcut(",")
        Button("Quit Murmur") { NSApplication.shared.terminate(nil) }
            .keyboardShortcut("q")
    }
}
