import Foundation
import MurmurCore
import SwiftData
import os

/// The local dictation history (SwiftData, in Application Support). Views read it through
/// `@Query` on `container`; writes go through here.
@MainActor
public final class HistoryStore {
    public let container: ModelContainer
    private let logger = Logger(subsystem: Log.subsystem, category: "history")

    public init() {
        container = Self.makeContainer()
    }

    public func record(_ transcript: DictationTranscript, speakingSeconds: Double, appName: String?, keepsText: Bool) {
        let record = DictationRecord(
            date: .now,
            text: keepsText ? transcript.text : "",
            rawText: keepsText ? transcript.rawText : "",
            wordCount: Chunker.wordCount(of: transcript.text),
            speakingSeconds: speakingSeconds,
            appName: appName,
            wasCleaned: transcript.wasCleaned
        )
        container.mainContext.insert(record)
        save()
    }

    /// Dictations not uploaded to the shared stats yet, as rows. Each gets its sync id here the
    /// first time and keeps it, so a retried upload is recognised as the same dictation.
    public func pendingSyncRows(device: String) -> [NeonSQL.Row] {
        let pending = unsyncedRecords()
        for record in pending where record.syncID == nil {
            record.syncID = UUID()
        }
        save()
        return pending.compactMap { record in
            record.syncID.map { id in
                NeonSQL.Row(
                    id: id,
                    device: device,
                    date: record.date,
                    wordCount: record.wordCount,
                    speakingSeconds: record.speakingSeconds
                )
            }
        }
    }

    public func markSynced(_ ids: Set<UUID>) {
        for record in unsyncedRecords() {
            if let id = record.syncID, ids.contains(id) {
                record.isSynced = true
            }
        }
        save()
    }

    /// Local dictations the shared stats don't contain yet, so Home can add them on top.
    public func unsyncedSummaries() -> [DictationSummary] {
        unsyncedRecords().map(\.summary)
    }

    private func unsyncedRecords() -> [DictationRecord] {
        let descriptor = FetchDescriptor<DictationRecord>(predicate: #Predicate { !$0.isSynced })
        do {
            return try container.mainContext.fetch(descriptor)
        } catch {
            logger.error("reading unsynced dictations failed: \(error.localizedDescription)")
            return []
        }
    }

    /// Deletes every dictation on this Mac. Stats already synced to the database stay there.
    public func clear() {
        do {
            try container.mainContext.delete(model: DictationRecord.self)
        } catch {
            logger.error("clearing history failed: \(error.localizedDescription)")
        }
        save()
    }

    private func save() {
        do {
            try container.mainContext.save()
        } catch {
            logger.error("saving history failed: \(error.localizedDescription)")
        }
    }

    // a broken store must never stop dictation: fall back to an in-memory history
    private static func makeContainer() -> ModelContainer {
        let logger = Logger(subsystem: Log.subsystem, category: "history")
        do {
            let directory = URL.applicationSupportDirectory.appending(path: Log.subsystem, directoryHint: .isDirectory)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let configuration = ModelConfiguration(url: directory.appending(path: "history.store"))
            return try ModelContainer(for: DictationRecord.self, configurations: configuration)
        } catch {
            logger.error("history store unavailable, using memory: \(error.localizedDescription)")
        }
        do {
            let configuration = ModelConfiguration(isStoredInMemoryOnly: true)
            return try ModelContainer(for: DictationRecord.self, configurations: configuration)
        } catch {
            fatalError("SwiftData can't create even an in-memory container: \(error)")
        }
    }
}
