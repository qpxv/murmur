import AppKit
import MurmurCore
import MurmurServices
import SwiftUI

/// API keys (stored in the Keychain) and the macOS permissions Murmur needs.
struct KeysPane: View {
    @Bindable var settingsStore: SettingsStore
    let statsSync: StatsSyncController
    // permissions change in System Settings, not here: re-read them whenever Murmur comes back to the front
    @State private var refreshCount = 0
    @State private var isTesting = false

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
            statsSyncSection
            Section("Permissions") {
                PermissionRow(
                    title: "Microphone",
                    isGranted: Permissions.isMicrophoneAuthorized,
                    action: Permissions.openMicrophoneSettings
                )
                PermissionRow(
                    title: "Accessibility (dictation key and pasting)",
                    isGranted: Permissions.isAccessibilityTrusted,
                    action: Permissions.openAccessibilitySettings
                )
                if settingsStore.settings.hotkey == .function {
                    PermissionRow(
                        title: "🌐 key set to “Do Nothing”",
                        isGranted: Permissions.isFnKeyFree,
                        action: Permissions.openKeyboardSettings
                    )
                }
            }
            .id(refreshCount)
            startupSection
        }
        .formStyle(.grouped)
        .navigationTitle("Keys & Permissions")
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in
            refreshCount += 1
        }
    }

    private var startupSection: some View {
        Section("Startup") {
            Toggle(
                "Open Murmur at login",
                isOn: Binding(
                    get: { LoginItem.isEnabled },
                    set: { isEnabled in
                        LoginItem.setEnabled(isEnabled)
                        refreshCount += 1
                    }
                )
            )
            if LoginItem.needsApproval {
                PermissionRow(
                    title: "Allowed in Login Items",
                    isGranted: false,
                    action: LoginItem.openSettings
                )
            }
        }
    }

    private var statsSyncSection: some View {
        Section {
            APIKeyField(title: "Neon database URL", account: AppConstants.Keychain.neonAccount)
            TextField(
                "This device's name",
                text: $settingsStore.settings.syncDeviceName,
                prompt: Text(statsSync.deviceName)
            )
            HStack {
                syncStatus
                Spacer()
                Button("Test Connection") {
                    isTesting = true
                    Task {
                        await statsSync.testConnection()
                        isTesting = false
                    }
                }
                .disabled(isTesting)
            }
        } header: {
            Text("Stats Sync")
        } footer: {
            Text(
                "Combines Home's numbers across your devices. Only counts are shared "
                    + "(time, words, seconds spoken), never what you said."
            )
        }
    }

    @ViewBuilder private var syncStatus: some View {
        if isTesting {
            ProgressView().controlSize(.small)
        } else {
            switch statsSync.status {
            case .notConfigured:
                Text("Not set up").foregroundStyle(.secondary)
            case .synced(let date):
                Label {
                    Text("Connected, synced \(date, format: .dateTime.hour().minute())")
                } icon: {
                    Image(systemName: "checkmark.circle.fill")
                }
                .foregroundStyle(.green)
            case .failed(let failure):
                Label(failure.message, systemImage: "exclamationmark.circle.fill")
                    .foregroundStyle(.orange)
            }
        }
    }
}
