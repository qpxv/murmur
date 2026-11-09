import Foundation
import MurmurCore
import MurmurServices
import Observation
import SystemConfiguration

/// Keeps the dashboard's numbers shared across devices through the user's Neon database.
/// Event-driven only: uploads after each dictation and at launch, fetches when Home opens.
/// Without a database URL it does nothing and Home shows this Mac's numbers.
@MainActor
@Observable
final class StatsSyncController {
    enum Status: Equatable {
        case notConfigured
        case synced(at: Date)
        case failed(StatsSync.Failure)
    }

    private(set) var status: Status = .notConfigured
    /// Every device's dictations from the last successful fetch; `nil` before one.
    private(set) var sharedSummaries: [DictationSummary]?

    @ObservationIgnored private let historyStore: HistoryStore
    @ObservationIgnored private let settingsStore: SettingsStore
    @ObservationIgnored private var isUploading = false

    init(historyStore: HistoryStore, settingsStore: SettingsStore) {
        self.historyStore = historyStore
        self.settingsStore = settingsStore
    }

    /// The numbers Home should show: everyone's when synced (plus this Mac's not uploaded yet),
    /// otherwise this Mac's own.
    func summaries(local: [DictationSummary]) -> [DictationSummary] {
        guard let sharedSummaries else { return local }
        return sharedSummaries + historyStore.unsyncedSummaries()
    }

    /// Uploads this Mac's dictations the database doesn't have yet. Failures just leave them
    /// pending for the next attempt; dictation itself never waits on this.
    func uploadPending() async {
        guard !isUploading, let sync = makeSync() else { return }
        let rows = historyStore.pendingSyncRows(device: deviceName)
        guard !rows.isEmpty else { return }
        isUploading = true
        defer { isUploading = false }
        do {
            try await sync.upload(rows)
            historyStore.markSynced(Set(rows.map(\.id)))
            // keep the shown totals right until the next fetch
            sharedSummaries?.append(contentsOf: rows.map(Self.summary))
        } catch {
            status = .failed(error)
        }
    }

    /// Uploads what's pending, then loads every device's numbers.
    func refresh() async {
        guard let sync = makeSync() else {
            status = .notConfigured
            sharedSummaries = nil
            return
        }
        await uploadPending()
        do {
            sharedSummaries = try await sync.fetchSummaries()
            status = .synced(at: .now)
        } catch {
            status = .failed(error)
        }
    }

    /// Checks the database URL (creating the table if needed), then syncs.
    func testConnection() async {
        guard let sync = makeSync() else {
            status = .notConfigured
            return
        }
        do {
            try await sync.upload([])
            await refresh()
        } catch {
            status = .failed(error)
        }
    }

    /// The name this device's dictations carry in the shared table.
    var deviceName: String {
        let chosen = settingsStore.settings.syncDeviceName.trimmingCharacters(in: .whitespaces)
        guard chosen.isEmpty else { return chosen }
        return (SCDynamicStoreCopyComputerName(nil, nil) as String?) ?? "Mac"
    }

    private func makeSync() -> StatsSync? {
        guard let url = Keychain.read(account: AppConstants.Keychain.neonAccount), !url.isEmpty else { return nil }
        return StatsSync(databaseURL: url)
    }

    private static func summary(of row: NeonSQL.Row) -> DictationSummary {
        DictationSummary(date: row.date, wordCount: row.wordCount, speakingSeconds: row.speakingSeconds)
    }
}

extension StatsSync.Failure {
    /// What the user sees in Home and Keys.
    var message: String {
        switch self {
        case .invalidDatabaseURL: "That isn't a Postgres connection string"
        case .unreachable: "Offline, couldn't reach the database"
        case .rejected(let status): "The database refused (HTTP \(status)), check the URL"
        case .unreadableResponse: "The database sent something unexpected"
        }
    }
}
