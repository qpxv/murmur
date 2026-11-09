import Foundation
import MurmurCore
import Testing

struct CleanupRequestTests {
    @Test func userMessageWrapsEveryPart() {
        let message = CleanupPrompt.userMessage(
            transcript: "open clock code",
            previous: "earlier words",
            next: "later words",
            vocabulary: ["Claude Code", " ", "cmux"],
            appName: "Ghostty"
        )
        #expect(
            message == """
                <vocabulary>Claude Code, cmux</vocabulary>
                <app>Ghostty</app>
                <previous>earlier words</previous>
                <transcript>
                open clock code
                </transcript>
                <next>later words</next>
                """
        )
    }

    @Test func userMessageLeavesOutEmptyParts() {
        let message = CleanupPrompt.userMessage(
            transcript: "hi",
            previous: "",
            next: "",
            vocabulary: [],
            appName: nil
        )
        #expect(message == "<transcript>\nhi\n</transcript>")
    }

    @Test func requestBodyCarriesModelEffortAndFallbacks() throws {
        let body = try AnthropicMessages.cleanupRequestBody(system: "be tidy", userMessage: "hi")
        let json = try #require(try JSONSerialization.jsonObject(with: body) as? [String: Any])
        #expect(json["model"] as? String == "claude-opus-5-5")
        #expect(json["fallbacks"] as? String == "default")
        #expect((json["output_config"] as? [String: String])?["effort"] == "low")
        #expect(json["max_tokens"] as? Int == AppConstants.Cleanup.maxTokens)
        #expect(json["system"] as? String == "be tidy")
        #expect(json["thinking"] == nil)
    }

    @Test func endTurnReturnsJoinedTextBlocksOnly() {
        let json = """
            {"content":[{"type":"thinking","thinking":""},{"type":"text","text":" Open Claude Code. "}],
             "stop_reason":"end_turn"}
            """
        #expect(AnthropicMessages.cleanedText(from: Data(json.utf8)) == "Open Claude Code.")
    }

    @Test(arguments: ["refusal", "max_tokens"])
    func abnormalStopKeepsRawText(stopReason: String) {
        let json = """
            {"content":[{"type":"text","text":"partial"}],"stop_reason":"\(stopReason)"}
            """
        #expect(AnthropicMessages.cleanedText(from: Data(json.utf8)) == nil)
        #expect(AnthropicMessages.outcome(from: Data(json.utf8)) == stopReason)
    }

    @Test func emptyTextKeepsRawText() {
        let json = #"{"content":[],"stop_reason":"end_turn"}"#
        #expect(AnthropicMessages.cleanedText(from: Data(json.utf8)) == nil)
    }

    @Test func errorBodyIsReadableForLogsButYieldsNoText() {
        let json = #"{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}"#
        #expect(AnthropicMessages.cleanedText(from: Data(json.utf8)) == nil)
        #expect(AnthropicMessages.outcome(from: Data(json.utf8)) == "overloaded_error")
    }
}
