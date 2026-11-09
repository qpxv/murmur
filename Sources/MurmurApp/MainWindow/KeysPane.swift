import AppKit
import MurmurCore
import MurmurServices
import SwiftUI

/// API keys (stored in the Keychain) and the macOS permissions Murmur needs.
struct KeysPane: View {
    // permissions change in System Settings, not here: re-read them whenever Murmur comes back to the front
    @State private var refreshCount = 0

    var body: some View {
        Form {
            Section {
                APIKeyField(title: "Deepgram", account: AppConstants.Keychain.deepgramAccount)
                APIKeyField(title: "Claude", account: AppConstants.Keychain.anthropicAccount)
            } header: {
                Text("API Keys")
            } footer: {
                Text("Stored in your Mac's Keychain. Never leaves this Mac except to call that service.")
            }
            Section("Permissions") {
                PermissionRow(
                    title: "Microphone",
                    isGranted: Permissions.isMicrophoneAuthorized,
                    action: Permissions.openMicrophoneSettings
                )
                PermissionRow(
                    title: "Accessibility (fn hotkey and pasting)",
                    isGranted: Permissions.isAccessibilityTrusted,
                    action: Permissions.openAccessibilitySettings
                )
                PermissionRow(
                    title: "🌐 key set to “Do Nothing”",
                    isGranted: Permissions.isFnKeyFree,
                    action: Permissions.openKeyboardSettings
                )
            }
            .id(refreshCount)
        }
        .formStyle(.grouped)
        .navigationTitle("Keys & Permissions")
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in
            refreshCount += 1
        }
    }
}
