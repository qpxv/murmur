import AppKit
import MurmurCore
import MurmurServices
import SwiftUI

/// The floating, click-through panel that holds the pill. It never takes focus, shows on every
/// Space and over full-screen apps, and stays put in Mission Control.
@MainActor
final class OverlayPanelController {
    private let panel: NSPanel
    private let settingsStore: SettingsStore
    private var screenObserver: (any NSObjectProtocol)?

    init(dictation: DictationController, settingsStore: SettingsStore) {
        self.settingsStore = settingsStore
        panel = NSPanel(
            contentRect: NSRect(origin: .zero, size: Theme.Overlay.canvasSize),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        panel.isFloatingPanel = true
        panel.level = .statusBar
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = false
        panel.ignoresMouseEvents = true
        panel.hidesOnDeactivate = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        panel.contentView = NSHostingView(rootView: PillView(dictation: dictation, settingsStore: settingsStore))

        reposition()
        panel.orderFrontRegardless()
        trackPositionSetting()
        screenObserver = NotificationCenter.default.addObserver(
            forName: NSApplication.didChangeScreenParametersNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated { self?.reposition() }
        }
    }

    private func reposition() {
        guard let screen = NSScreen.main ?? NSScreen.screens.first else { return }
        let origin = settingsStore.settings.overlayPosition.origin(
            for: Theme.Overlay.canvasSize,
            in: screen.visibleFrame,
            margin: Theme.Overlay.screenMargin
        )
        panel.setFrameOrigin(origin)
    }

    private func trackPositionSetting() {
        withObservationTracking {
            _ = settingsStore.settings.overlayPosition
        } onChange: { [weak self] in
            Task { @MainActor in
                self?.reposition()
                self?.trackPositionSetting()
            }
        }
    }
}
