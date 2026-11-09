import Foundation
import MurmurCore
import os

/// Talks to the user's Neon database over its HTTPS SQL endpoint to share dictation stats
/// between devices. Sends numbers only; logs status and timing, never the database URL.
public struct StatsSync: Sendable {
    public enum Failure: Error, Equatable, Sendable {
        /// Not a `postgres(ql)://user:password@host/db` connection string.
        case invalidDatabaseURL
        /// No connection (offline, DNS, timeout).
        case unreachable
        /// The database answered with an error (wrong password, missing permission, …).
        case rejected(status: Int)
        case unreadableResponse
    }

    private static let successStatus = 200
    private let databaseURL: String
    private let logger = Logger(subsystem: Log.subsystem, category: "sync")

    public init(databaseURL: String) {
        self.databaseURL = databaseURL
    }

    /// Uploads the rows (creating the table on first use). With no rows it only checks the
    /// connection and makes sure the table exists.
    public func upload(_ rows: [NeonSQL.Row]) async throws(Failure) {
        let body: Data
        do {
            body = try NeonSQL.uploadBody(rows)
        } catch {
            throw .unreadableResponse
        }
        _ = try await send(body, label: "upload \(rows.count)")
    }

    /// Every device's dictations, as dashboard summaries.
    public func fetchSummaries() async throws(Failure) -> [DictationSummary] {
        let body: Data
        do {
            body = try NeonSQL.fetchBody()
        } catch {
            throw .unreadableResponse
        }
        let data = try await send(body, label: "fetch")
        guard let summaries = NeonSQL.summaries(from: data) else { throw .unreadableResponse }
        return summaries
    }

    private func send(_ body: Data, label: String) async throws(Failure) -> Data {
        guard let endpoint = NeonSQL.endpoint(forDatabaseURL: databaseURL) else { throw .invalidDatabaseURL }
        var request = URLRequest(url: endpoint, timeoutInterval: AppConstants.Sync.requestTimeout)
        request.httpMethod = "POST"
        request.httpBody = body
        request.setValue("application/json", forHTTPHeaderField: "content-type")
        request.setValue(
            databaseURL.trimmingCharacters(in: .whitespacesAndNewlines),
            forHTTPHeaderField: AppConstants.Sync.connectionHeader
        )
        request.setValue("true", forHTTPHeaderField: AppConstants.Sync.arrayModeHeader)

        let started = ContinuousClock.now
        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await URLSession.shared.data(for: request)
        } catch {
            logger.error("sync \(label, privacy: .public) unreachable: \(error.localizedDescription, privacy: .public)")
            throw .unreachable
        }
        let status = (response as? HTTPURLResponse)?.statusCode ?? 0
        let milliseconds = Int((ContinuousClock.now - started) / .milliseconds(1))
        logger.notice("sync \(label, privacy: .public): \(status) in \(milliseconds) ms")
        guard status == Self.successStatus else { throw .rejected(status: status) }
        return data
    }
}
