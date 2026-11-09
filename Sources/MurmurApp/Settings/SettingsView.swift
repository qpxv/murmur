import MurmurCore
import MurmurServices
import SwiftUI

/// Settings window: API keys, permissions and the overlay position.
struct SettingsView: View {
    @Bindable var settingsStore: SettingsStore

    var body: some View {
        Form {
            Section("API Keys") {
                APIKeyField(title: "Deepgram", account: AppConstants.Keychain.deepgramAccount)
                APIKeyField(title: "Claude", account: AppConstants.Keychain.anthropicAccount)
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
            Section("Overlay") {
                Picker("Position", selection: $settingsStore.settings.overlayPosition) {
                    ForEach(OverlayPosition.allCases, id: \.self) { position in
                        Text(position.title).tag(position)
                    }
                }
            }
        }
        .formStyle(.grouped)
        .frame(width: Theme.Settings.windowWidth)
        .fixedSize()
    }
}

private struct PermissionRow: View {
    let title: String
    let isGranted: Bool
    let action: @MainActor () -> Void

    var body: some View {
        HStack {
            Image(systemName: isGranted ? "checkmark.circle.fill" : "exclamationmark.circle.fill")
                .foregroundStyle(isGranted ? .green : .orange)
            Text(title)
            Spacer()
            if !isGranted {
                Button("Open Settings…", action: action)
            }
        }
    }
}
