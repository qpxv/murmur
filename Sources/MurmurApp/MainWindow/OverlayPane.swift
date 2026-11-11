import MurmurCore
import MurmurServices
import SwiftUI

/// Which key dictates and how it works, and where the pill sits.
struct OverlayPane: View {
    let dictation: DictationController
    @Bindable var settingsStore: SettingsStore
    @State private var isRecording = false
    @State private var rejection: HotkeyRejection?

    private var hotkey: Hotkey { settingsStore.settings.hotkey }
    private var name: String { KeyName.of(hotkey) }

    var body: some View {
        Form {
            Section {
                keyRow
                LabeledContent("Hold \(name)", value: "Talk, release to paste")
                LabeledContent("\(name) + space", value: "Lock recording, hands free")
                LabeledContent("\(name) (while locked)", value: "Stop and paste")
                LabeledContent("\(name) + another key", value: "Cancels")
                if hotkey == .function {
                    PermissionRow(
                        title: "🌐 key set to “Do Nothing”",
                        isGranted: Permissions.isFnKeyFree,
                        action: Permissions.openKeyboardSettings
                    )
                }
            } header: {
                Text("Dictate")
            } footer: {
                Text(footer)
            }
            Section("Pill position") {
                PositionPicker(selection: $settingsStore.settings.overlayPosition)
                    .frame(maxWidth: .infinity)
            }
        }
        .formStyle(.grouped)
        .navigationTitle("Shortcut & Overlay")
        .onDisappear { dictation.cancelHotkeyRecording() }
    }

    private var keyRow: some View {
        LabeledContent("Dictation key") {
            HStack {
                if let rejection {
                    Text(rejection.message).foregroundStyle(.orange)
                }
                Text(isRecording ? "Press a key…" : name)
                    .foregroundStyle(isRecording ? .secondary : .primary)
                Button(isRecording ? "Cancel" : "Record…", action: toggleRecording)
                    .disabled(!dictation.isHotkeyActive)
                if hotkey != .function && !isRecording {
                    Button("Reset") {
                        rejection = nil
                        dictation.changeHotkey(to: .function)
                    }
                }
            }
        }
    }

    private var footer: String {
        if !dictation.isHotkeyActive {
            return "Recording a key needs Accessibility access (Keys & Permissions)."
        }
        if hotkey.isTakenOver {
            return "\(name) only dictates while Murmur runs. It does nothing else."
        }
        return "Any single key works, e.g. right ⌘. It then only dictates. Escape cancels recording."
    }

    private func toggleRecording() {
        guard !isRecording else {
            dictation.cancelHotkeyRecording()
            return
        }
        rejection = nil
        isRecording = true
        dictation.recordHotkey { result in
            isRecording = false
            if case .rejected(let reason) = result {
                rejection = reason
            }
        }
    }
}

/// A miniature screen with the nine places the pill can sit; the chosen one shows the pill.
private struct PositionPicker: View {
    @Binding var selection: OverlayPosition

    private static let rows: [[OverlayPosition]] = [
        [.topLeft, .topCenter, .topRight],
        [.middleLeft, .middleCenter, .middleRight],
        [.bottomLeft, .bottomCenter, .bottomRight],
    ]

    var body: some View {
        Grid(horizontalSpacing: Theme.PositionPicker.cellSpacing, verticalSpacing: Theme.PositionPicker.cellSpacing) {
            ForEach(Self.rows, id: \.self) { row in
                GridRow {
                    ForEach(row, id: \.self) { position in
                        cell(position)
                    }
                }
            }
        }
        .padding(Theme.PositionPicker.cellSpacing)
        .frame(width: Theme.PositionPicker.screenWidth, height: Theme.PositionPicker.screenHeight)
        .background(.quaternary, in: .rect(cornerRadius: Theme.PositionPicker.screenCornerRadius))
    }

    private func cell(_ position: OverlayPosition) -> some View {
        Button {
            selection = position
        } label: {
            ZStack {
                Color.clear
                if position == selection {
                    Capsule()
                        .fill(Theme.Overlay.background)
                        .overlay(
                            Image(systemName: "waveform")
                                .font(.system(size: Theme.PositionPicker.glyphSize, weight: .semibold))
                                .foregroundStyle(Theme.Overlay.foreground)
                        )
                        .frame(width: Theme.PositionPicker.pillWidth, height: Theme.PositionPicker.pillHeight)
                } else {
                    Circle()
                        .fill(.secondary.opacity(Theme.PositionPicker.idleOpacity))
                        .frame(width: Theme.PositionPicker.dotSize, height: Theme.PositionPicker.dotSize)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: position.alignment)
            .contentShape(.rect)
        }
        .buttonStyle(.plain)
        .help(position.title)
        .animation(.spring(duration: Theme.Overlay.morphDuration), value: selection)
    }
}
