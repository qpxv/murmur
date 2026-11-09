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

    /// Deletes every dictation, counts included.
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
