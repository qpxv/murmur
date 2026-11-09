import MurmurCore
import MurmurServices
import SwiftUI

/// The black pill (dynamic island style): a waveform glyph when idle, a live voice bar while
/// recording, a spinner while finishing, and a short message on errors.
struct PillView: View {
    let dictation: DictationController
    let settingsStore: SettingsStore

    var body: some View {
        content
            .frame(height: isIdle ? Theme.Overlay.idleHeight : Theme.Overlay.pillHeight)
            .padding(.horizontal, isIdle ? 0 : Theme.Overlay.horizontalPadding)
            .foregroundStyle(Theme.Overlay.foreground)
            .background(Theme.Overlay.background, in: .capsule)
            .overlay(Capsule().strokeBorder(Theme.Overlay.edge, lineWidth: Theme.Overlay.edgeWidth))
            .environment(\.colorScheme, .dark)
            .animation(.spring(duration: Theme.Overlay.morphDuration), value: dictation.state)
            .frame(
                maxWidth: .infinity,
                maxHeight: .infinity,
                alignment: settingsStore.settings.overlayPosition.alignment
            )
    }

    @ViewBuilder private var content: some View {
        switch dictation.state {
        case .idle:
            Image(systemName: "waveform")
                .font(.system(size: Theme.Overlay.idleIconSize, weight: .semibold))
                .frame(width: Theme.Overlay.idleWidth)
        case .recording(let mode):
            HStack(spacing: Theme.Overlay.contentSpacing) {
                VoiceBar(levels: dictation.levels)
                if mode == .locked {
                    Image(systemName: "lock.fill")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .transition(.scale.combined(with: .opacity))
                }
            }
        case .finishing:
            ProgressView().controlSize(.small)
        case .failed(let error):
            Label(error.message, systemImage: "exclamationmark.triangle.fill")
                .font(.callout)
                .lineLimit(1)
        }
    }

    private var isIdle: Bool {
        dictation.state == .idle
    }
}
