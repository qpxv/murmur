import Foundation

/// Claude Messages API wire format for one cleanup request and its response.
public enum AnthropicMessages {
    /// JSON body for cleaning one chunk.
    public static func cleanupRequestBody(userMessage: String) throws -> Data {
        let request = Request(
            model: AppConstants.Cleanup.model,
            maxTokens: AppConstants.Cleanup.maxTokens,
            system: CleanupPrompt.system,
            messages: [Request.Message(role: "user", content: userMessage)],
            outputConfig: Request.OutputConfig(effort: AppConstants.Cleanup.effort),
            fallbacks: "default"
        )
        return try JSONEncoder().encode(request)
    }

    /// The cleaned text, or `nil` when the model didn't finish normally (refusal, cut off) or
    /// returned nothing, in which case the caller keeps the raw text.
    public static func cleanedText(from data: Data) -> String? {
        guard let response = try? JSONDecoder().decode(Response.self, from: data),
            response.stopReason == "end_turn"
        else { return nil }
        let text = response.content
            .filter { $0.type == "text" }
            .compactMap(\.text)
            .joined()
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return text.isEmpty ? nil : text
    }

    /// `stop_reason` or the API error type, for logs. Never contains user text.
    public static func outcome(from data: Data) -> String {
        if let response = try? JSONDecoder().decode(Response.self, from: data) {
            return response.stopReason ?? "unknown"
        }
        if let failure = try? JSONDecoder().decode(ErrorResponse.self, from: data) {
            return failure.error.type
        }
        return "unreadable"
    }
}

private struct Request: Encodable {
    struct Message: Encodable {
        let role: String
        let content: String
    }

    struct OutputConfig: Encodable {
        let effort: String
    }

    let model: String
    let maxTokens: Int
    let system: String
    let messages: [Message]
    let outputConfig: OutputConfig
    let fallbacks: String

    private enum CodingKeys: String, CodingKey {
        case model
        case maxTokens = "max_tokens"
        case system
        case messages
        case outputConfig = "output_config"
        case fallbacks
    }
}

private struct Response: Decodable {
    struct Block: Decodable {
        let type: String
        let text: String?
    }

    let content: [Block]
    let stopReason: String?

    private enum CodingKeys: String, CodingKey {
        case content
        case stopReason = "stop_reason"
    }
}

private struct ErrorResponse: Decodable {
    struct Detail: Decodable {
        let type: String
    }

    let error: Detail
}
