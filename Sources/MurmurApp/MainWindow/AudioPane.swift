import MurmurServices
import SwiftUI

/// Microphone choice and the whisper-friendly volume boost.
struct AudioPane: View {
    @Bindable var settingsStore: SettingsStore
    @State private var devices: [InputDevice] = []

    var body: some View {
        Form {
            Section {
                Picker("Microphone", selection: $settingsStore.settings.inputDeviceUID) {
                    Text("System Default").tag(String?.none)
                    ForEach(devices) { device in
                        Text(device.name).tag(String?.some(device.uid))
                    }
                }
            } footer: {
                Text("If the chosen microphone is unplugged, the system default is used.")
            }
            Section {
                Toggle("Boost mic while recording", isOn: $settingsStore.settings.isMicBoostEnabled)
            } footer: {
                Text("Turns input volume to 100% while you dictate, so even whispers come through, then puts it back.")
            }
        }
        .formStyle(.grouped)
        .navigationTitle("Audio")
        .onAppear { devices = CoreAudioDevice.inputDevices() }
    }
}
