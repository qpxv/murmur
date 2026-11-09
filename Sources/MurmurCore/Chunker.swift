import Foundation

/// Groups finalized transcript segments into chunks for cleanup while the user is still talking.
/// A chunk closes at the first pause after `minWords`, or right away at `maxWords`, so each
/// cleanup request sees whole sentences and only the last chunk is left when they stop.
public struct Chunker: Equatable, Sendable {
    private let minWords: Int
    private let maxWords: Int
    private var segments: [String] = []
    private var wordCount = 0

    public init(
        minWords: Int = AppConstants.Cleanup.minChunkWords,
        maxWords: Int = AppConstants.Cleanup.maxChunkWords
    ) {
        self.minWords = minWords
        self.maxWords = maxWords
    }

    /// Adds a segment; returns the chunk text if this segment closed one.
    public mutating func add(_ segment: String, isPause: Bool) -> String? {
        let trimmed = segment.trimmingCharacters(in: .whitespacesAndNewlines)
        if !trimmed.isEmpty {
            segments.append(trimmed)
            wordCount += Self.wordCount(of: trimmed)
        }
        let isLongEnough = wordCount >= minWords && isPause
        guard isLongEnough || wordCount >= maxWords else { return nil }
        return flush()
    }

    /// Closes whatever is pending (when the user stops); `nil` if nothing is.
    public mutating func flush() -> String? {
        guard !segments.isEmpty else { return nil }
        let chunk = segments.joined(separator: " ")
        segments = []
        wordCount = 0
        return chunk
    }

    public static func wordCount(of text: String) -> Int {
        text.split(whereSeparator: \.isWhitespace).count
    }

    public static func firstWords(of text: String, count: Int) -> String {
        text.split(whereSeparator: \.isWhitespace).prefix(count).joined(separator: " ")
    }
}
