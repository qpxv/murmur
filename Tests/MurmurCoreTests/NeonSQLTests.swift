import Foundation
import MurmurCore
import Testing

struct NeonSQLTests {
    @Test func endpointUsesTheDatabaseHost() {
        let url = "postgresql://mia:secret@ep-quiet-lake-123456.eu-central-1.aws.neon.tech/neondb?sslmode=require"
        let endpoint = NeonSQL.endpoint(forDatabaseURL: url)
        #expect(endpoint?.absoluteString == "https://ep-quiet-lake-123456.eu-central-1.aws.neon.tech/sql")
    }

    @Test func pooledHostAndShortSchemeWork() {
        let url = "  postgres://noah:pw@ep-x-pooler.us-east-2.aws.neon.tech/db\n"
        let endpoint = NeonSQL.endpoint(forDatabaseURL: url)
        #expect(endpoint?.absoluteString == "https://ep-x-pooler.us-east-2.aws.neon.tech/sql")
    }

    @Test(arguments: ["https://ep-x.neon.tech/db", "not a url", "", "postgresql:///neondb"])
    func rejectsAnythingButAPostgresURLWithHost(input: String) {
        #expect(NeonSQL.endpoint(forDatabaseURL: input) == nil)
    }

    @Test func uploadCreatesTableThenInsertsEveryRowIdempotently() throws {
        let id = try #require(UUID(uuidString: "6F9619FF-8B86-D011-B42D-00CF4FC964FF"))
        let row = NeonSQL.Row(
            id: id,
            device: "Mia's MacBook",
            date: Date(timeIntervalSince1970: 1_800_000_000),
            wordCount: 42,
            speakingSeconds: 12.5
        )
        let body = try NeonSQL.uploadBody([row, row])
        let json = try #require(try JSONSerialization.jsonObject(with: body) as? [String: Any])
        let queries = try #require(json["queries"] as? [[String: Any]])
        #expect(queries.count == 3)
        #expect((queries[0]["query"] as? String)?.contains("create table if not exists murmur_dictations") == true)
        let insert = try #require(queries[1]["query"] as? String)
        #expect(insert.contains("on conflict (id) do nothing"))
        let params = try #require(queries[1]["params"] as? [String])
        #expect(
            params == [
                "6F9619FF-8B86-D011-B42D-00CF4FC964FF", "Mia's MacBook", "2027-01-15T08:00:00.000Z", "42", "12.5",
            ]
        )
    }

    @Test func emptyUploadOnlyMakesSureTheTableExists() throws {
        let body = try NeonSQL.uploadBody([])
        let json = try #require(try JSONSerialization.jsonObject(with: body) as? [String: Any])
        #expect((json["queries"] as? [Any])?.count == 1)
    }

    @Test func fetchSelectsPlainNumbers() throws {
        let body = try NeonSQL.fetchBody()
        let json = try #require(try JSONSerialization.jsonObject(with: body) as? [String: Any])
        let query = try #require(json["query"] as? String)
        #expect(query.contains("extract(epoch from dictated_at)"))
        #expect(query.contains("from murmur_dictations"))
    }

    @Test func parsesRowsWithNumbersOrText() {
        let json = """
            {"fields":[],"rows":[[1800000000,"120","45.5"],["1800000100.25",7,3]]}
            """
        let summaries = NeonSQL.summaries(from: Data(json.utf8))
        #expect(
            summaries == [
                DictationSummary(
                    date: Date(timeIntervalSince1970: 1_800_000_000),
                    wordCount: 120,
                    speakingSeconds: 45.5
                ),
                DictationSummary(date: Date(timeIntervalSince1970: 1_800_000_100.25), wordCount: 7, speakingSeconds: 3),
            ]
        )
    }

    @Test func skipsUnreadableRowsAndHandlesEmpty() {
        let json = #"{"rows":[[null,1,2],["x","y"],[1800000000,5,2]]}"#
        #expect(NeonSQL.summaries(from: Data(json.utf8))?.count == 1)
        #expect(NeonSQL.summaries(from: Data(#"{"rows":[]}"#.utf8)) == [])
    }

    @Test func errorBodyIsNotAResult() {
        let json = #"{"message":"password authentication failed","code":"28P01"}"#
        #expect(NeonSQL.summaries(from: Data(json.utf8)) == nil)
    }
}
