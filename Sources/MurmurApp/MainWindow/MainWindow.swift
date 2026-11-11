import AppKit
import MurmurServices
import SwiftUI

/// The app window: sidebar on the left, the selected pane on the right. While it's open Murmur
/// behaves like a normal app (Dock icon, ⌘-Tab); once closed it's back to menu bar only.
struct MainWindow: View {
    static let id = "main"

    let dictation: DictationController
    @Bindable var settingsStore: SettingsStore
    let historyStore: HistoryStore
    let statsSync: StatsSyncController
    @State private var selection: Pane? = .home

    var body: some View {
        NavigationSplitView {
            List(Pane.allCases, selection: $selection) { pane in
                Label(pane.title, systemImage: pane.symbol).tag(pane)
            }
            .navigationSplitViewColumnWidth(Theme.Window.sidebarWidth)
        } detail: {
            detail(for: selection ?? .home)
        }
        .frame(minWidth: Theme.Window.minWidth, minHeight: Theme.Window.minHeight)
        .onAppear {
            NSApp.setActivationPolicy(.regular)
            NSApp.activate()
        }
        .onDisappear {
            NSApp.setActivationPolicy(.accessory)
        }
    }

    @ViewBuilder private func detail(for pane: Pane) -> some View {
        switch pane {
        case .home: HomePane(settingsStore: settingsStore, statsSync: statsSync)
        case .history: HistoryPane(settingsStore: settingsStore, historyStore: historyStore)
        case .vocabulary: VocabularyPane(settingsStore: settingsStore)
        case .cleanup: CleanupPane(settingsStore: settingsStore, selection: $selection)
        case .audio: AudioPane(settingsStore: settingsStore)
        case .overlay: OverlayPane(dictation: dictation, settingsStore: settingsStore)
        case .keys: KeysPane(settingsStore: settingsStore, statsSync: statsSync)
        }
    }
}
