import Foundation

/// What the Mac and Windows apps must agree on, read from the repo's `shared/` folder
/// (bundled as a SwiftPM resource through the `Shared` symlink).
enum SharedFiles {
    static let config: SharedConfig = load("config")
    static let cleanupPrompt: SharedCleanupPrompt = load("cleanup-prompt")

    private static func load<Value: Decodable>(_ name: String) -> Value {
        guard let url = Bundle.module.url(forResource: name, withExtension: "json", subdirectory: "Shared"),
            let data = try? Data(contentsOf: url),
            let value = try? JSONDecoder().decode(Value.self, from: data)
        else {
            // a build without its own resource is broken, not something to recover from
            fatalError("shared/\(name).json is missing from the bundle or doesn't decode")
        }
        return value
    }
}

/// `shared/config.json`.
struct SharedConfig: Decodable, Sendable {
    struct Cleanup: Decodable, Sendable {
        let messagesURL: String
        let model: String
        let apiVersion: String
        let fallbackBeta: String
        let effort: String
        let maxTokens: Int
        let minChunkWords: Int
        let maxChunkWords: Int
        let contextWords: Int
        let requestTimeoutSeconds: Double
        let finishTimeoutSeconds: Double
    }

    struct Deepgram: Decodable, Sendable {
        let listenURL: String
        let model: String
        let language: String
        let finalizeTimeoutSeconds: Double
    }

    let cleanup: Cleanup
    let deepgram: Deepgram
}

/// `shared/cleanup-prompt.json`: the building blocks of the cleanup system prompt.
struct SharedCleanupPrompt: Decodable, Sendable {
    struct Fixes: Decodable, Sendable {
        let misheardWords: String
        let punctuation: String
        let stutters: String
    }

    struct Styles: Decodable, Sendable {
        let casual: String
        let normal: String
        let formal: String
    }

    let intro: String
    let fixesHeader: String
    let fixes: Fixes
    let styles: Styles
    let boundaries: String
    let output: String
}
