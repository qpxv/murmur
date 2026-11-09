import MurmurCore
import MurmurServices
import SwiftUI

/// How the fn shortcut works, and where the pill sits.
struct OverlayPane: View {
    @Bindable var settingsStore: SettingsStore

    var body: some View {
        Form {
            Section("Dictate") {
                LabeledContent("Hold fn", value: "Talk, release to paste")
                LabeledContent("fn + space", value: "Lock recording, hands free")
                LabeledContent("fn (while locked)", value: "Stop and paste")
                LabeledContent("fn + another key", value: "Cancels, it's a shortcut")
                PermissionRow(
                    title: "🌐 key set to “Do Nothing”",
                    isGranted: Permissions.isFnKeyFree,
                    action: Permissions.openKeyboardSettings
                )
            }
            Section("Pill position") {
                PositionPicker(selection: $settingsStore.settings.overlayPosition)
                    .frame(maxWidth: .infinity)
            }
        }
        .formStyle(.grouped)
        .navigationTitle("Shortcut & Overlay")
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
