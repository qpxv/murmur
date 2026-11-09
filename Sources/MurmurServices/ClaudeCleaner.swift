import Foundation
import MurmurCore
import os

/// Sends one dictated chunk to Claude for cleanup. Never throws: any failure returns `nil`
/// and the caller keeps the raw text, so a cleanup problem can never lose speech.
public struct ClaudeCleaner: Sendable {
    private static let successStatus = 200

    private let apiKey: String
    private let systemPrompt: String
    private let logger = Logger(subsystem: Log.subsystem, category: "cleanup")

    public init(apiKey: String, options: CleanupOptions) {
        self.apiKey = apiKey
        self.systemPrompt = CleanupPrompt.system(for: options)
    }

    public func clean(_ userMessage: String) async -> String? {
        guard let url = URL(string: AppConstants.Cleanup.messagesURL) else { return nil }
        let started = ContinuousClock.now
        do {
            var request = URLRequest(url: url, timeoutInterval: AppConstants.Cleanup.requestTimeout)
            request.httpMethod = "POST"
            request.httpBody = try AnthropicMessages.cleanupRequestBody(system: systemPrompt, userMessage: userMessage)
            request.setValue("application/json", forHTTPHeaderField: "content-type")
            request.setValue(apiKey, forHTTPHeaderField: "x-api-key")
            request.setValue(AppConstants.Cleanup.apiVersion, forHTTPHeaderField: "anthropic-version")
            request.setValue(AppConstants.Cleanup.fallbackBeta, forHTTPHeaderField: "anthropic-beta")

            let (data, response) = try await URLSession.shared.data(for: request)
            let status = (response as? HTTPURLResponse)?.statusCode ?? 0
            let outcome = AnthropicMessages.outcome(from: data)
            // stop_reason / error type only, never text, so it's safe to log in the clear
            logger.notice("cleanup \(status) \(outcome, privacy: .public) in \(milliseconds(since: started)) ms")
            guard status == Self.successStatus else { return nil }
            return AnthropicMessages.cleanedText(from: data)
        } catch is CancellationError {
            return nil
        } catch {
            logger.error("cleanup failed after \(milliseconds(since: started)) ms: \(error.localizedDescription)")
            return nil
        }
    }

    private func milliseconds(since start: ContinuousClock.Instant) -> Int {
        Int((ContinuousClock.now - start) / .milliseconds(1))
    }
}
