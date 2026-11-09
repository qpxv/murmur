import MurmurCore
import MurmurServices
import SwiftUI

/// Settings window. Phase 2 scope: the Deepgram key, permissions and the overlay position.
struct SettingsView: View {
    @Bindable var settingsStore: SettingsStore
    @State private var deepgramKey = Keychain.read(account: AppConstants.Keychain.deepgramAccount) ?? ""
    @State private var isKeySaved = false

    private static let deepgramConsole = URL(string: "https://console.deepgram.com")

    var body: some View {
        Form {
            Section("Deepgram") {
                SecureField("API Key", text: $deepgramKey)
                    .onSubmit(saveKey)
                    .onChange(of: deepgramKey) { isKeySaved = false }
                HStack {
                    if let console = Self.deepgramConsole {
                        Link("Get a key", destination: console)
                    }
                    Spacer()
                    if isKeySaved {
                        Label("Saved", systemImage: "checkmark.circle.fill")
                            .foregroundStyle(.green)
                    }
                    Button("Save", action: saveKey)
                        .keyboardShortcut(.defaultAction)
                }
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

    private func saveKey() {
        let trimmed = deepgramKey.trimmingCharacters(in: .whitespacesAndNewlines)
        deepgramKey = trimmed
        isKeySaved = Keychain.write(trimmed, account: AppConstants.Keychain.deepgramAccount)
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
