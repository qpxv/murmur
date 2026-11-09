import MurmurServices
import SwiftData
import SwiftUI

/// Every past dictation, newest first, searchable, with the original Deepgram text on demand.
struct HistoryPane: View {
    @Bindable var settingsStore: SettingsStore
    let historyStore: HistoryStore
    @Query(sort: \DictationRecord.date, order: .reverse) private var records: [DictationRecord]
    @State private var search = ""
    @State private var isConfirmingClear = false

    var body: some View {
        Group {
            if records.isEmpty {
                ContentUnavailableView(
                    "No history yet",
                    systemImage: "clock.arrow.circlepath",
                    description: Text("Finished dictations are listed here.")
                )
            } else if filtered.isEmpty {
                ContentUnavailableView.search(text: search)
            } else {
                List(filtered) { record in
                    DictationRow(record: record, showsOriginal: true)
                }
            }
        }
        .navigationTitle("History")
        .searchable(text: $search, prompt: "Search dictations")
        .toolbar {
            // separate items with a spacer: in one group they share a single cramped capsule
            ToolbarItem {
                Toggle("Save Text", isOn: $settingsStore.settings.isHistoryEnabled)
                    .toggleStyle(.switch)
                    .controlSize(.mini)
                    .padding(.horizontal, Theme.Window.toolbarItemPadding)
                    .help("Save what you said. Off: only word counts and times are kept")
            }
            ToolbarSpacer(.fixed)
            ToolbarItem {
                Button("Clear History…", systemImage: "trash") { isConfirmingClear = true }
                    .disabled(records.isEmpty)
            }
        }
        .confirmationDialog("Delete all dictations?", isPresented: $isConfirmingClear) {
            Button("Delete All", role: .destructive) { historyStore.clear() }
        } message: {
            Text("Deletes this Mac's history. Numbers already synced to other devices stay. It can't be undone.")
        }
    }

    private var filtered: [DictationRecord] {
        guard !search.isEmpty else { return records }
        return records.filter {
            $0.text.localizedCaseInsensitiveContains(search)
                || ($0.appName ?? "").localizedCaseInsensitiveContains(search)
        }
    }
}
