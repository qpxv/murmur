import Foundation

/// Wire format for syncing dictation stats through Neon's HTTPS SQL endpoint
/// (`POST https://<host>/sql`, see Neon's serverless driver). Only numbers travel, never text.
public enum NeonSQL {
    /// One dictation as stored in the shared table.
    public struct Row: Equatable, Sendable {
        public let id: UUID
        public let device: String
        public let date: Date
        public let wordCount: Int
        public let speakingSeconds: Double

        public init(id: UUID, device: String, date: Date, wordCount: Int, speakingSeconds: Double) {
            self.id = id
            self.device = device
            self.date = date
            self.wordCount = wordCount
            self.speakingSeconds = speakingSeconds
        }
    }

    private struct Query: Encodable {
        let query: String
        let params: [String]
    }

    private struct Batch: Encodable {
        let queries: [Query]
    }

    private static let table = AppConstants.Sync.tableName

    private static let createTable = Query(
        query: """
            create table if not exists \(table) (
              id uuid primary key,
              device text not null,
              dictated_at timestamptz not null,
              word_count integer not null check (word_count >= 0),
              speaking_seconds double precision not null check (speaking_seconds >= 0)
            )
            """,
        params: []
    )

    /// The HTTPS endpoint for a `postgres(ql)://user:password@host/db` connection string, or
    /// `nil` when it isn't one.
    public static func endpoint(forDatabaseURL databaseURL: String) -> URL? {
        let trimmed = databaseURL.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let components = URLComponents(string: trimmed),
            let scheme = components.scheme?.lowercased(),
            AppConstants.Sync.databaseSchemes.contains(scheme),
            let host = components.host, !host.isEmpty
        else { return nil }
        var endpoint = URLComponents()
        endpoint.scheme = "https"
        endpoint.host = host
        endpoint.path = AppConstants.Sync.endpointPath
        return endpoint.url
    }

    /// Creates the table if needed, then inserts the rows, all in one transaction. Rows already
    /// there (same id) are skipped, so uploading twice never counts twice. With no rows it just
    /// makes sure the table exists (a connection test).
    public static func uploadBody(_ rows: [Row]) throws -> Data {
        let inserts = rows.map { row in
            Query(
                query: """
                    insert into \(table) (id, device, dictated_at, word_count, speaking_seconds)
                    values ($1, $2, $3, $4, $5) on conflict (id) do nothing
                    """,
                params: [
                    row.id.uuidString,
                    row.device,
                    // full iso 8601 with the zone ("…Z"): without it postgres would guess the zone
                    row.date.formatted(Date.ISO8601FormatStyle(includingFractionalSeconds: true)),
                    String(row.wordCount),
                    String(row.speakingSeconds),
                ]
            )
        }
        return try JSONEncoder().encode(Batch(queries: [createTable] + inserts))
    }

    /// Reads every device's dictations as plain numbers (epoch seconds, words, seconds spoken).
    public static func fetchBody() throws -> Data {
        let query = Query(
            query: "select extract(epoch from dictated_at)::float8, word_count, speaking_seconds from \(table)",
            params: []
        )
        return try JSONEncoder().encode(query)
    }

    /// Parses an array-mode response of `fetchBody()`. Values may arrive as JSON numbers or as
    /// text; rows that can't be read are skipped. `nil` when the response isn't a result at all.
    public static func summaries(from data: Data) -> [DictationSummary]? {
        guard let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
            let rows = object["rows"] as? [[Any]]
        else { return nil }
        return rows.compactMap(summary)
    }

    private static func summary(from row: [Any]) -> DictationSummary? {
        let epochIndex = 0
        let wordsIndex = 1
        let secondsIndex = 2
        guard row.count > secondsIndex,
            let epoch = number(row[epochIndex]),
            let words = number(row[wordsIndex]),
            let seconds = number(row[secondsIndex])
        else { return nil }
        return DictationSummary(
            date: Date(timeIntervalSince1970: epoch),
            wordCount: Int(words),
            speakingSeconds: seconds
        )
    }

    private static func number(_ value: Any) -> Double? {
        if let number = value as? NSNumber { return number.doubleValue }
        if let text = value as? String { return Double(text) }
        return nil
    }
}
