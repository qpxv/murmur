import Foundation

/// Keeps every chunk's raw and cleaned text by chunk number, so cleanups that finish out of
/// order still assemble in spoken order, and a chunk without a cleanup uses its raw text.
public struct TranscriptAssembler: Equatable, Sendable {
    private var rawChunks: [Int: String] = [:]
    private var cleanedChunks: [Int: String] = [:]
    private var nextNumber = 0

    public init() {}

    /// Registers a new chunk and returns its number.
    public mutating func addChunk(_ raw: String) -> Int {
        let number = nextNumber
        rawChunks[number] = raw
        nextNumber += 1
        return number
    }

    public mutating func setCleaned(_ text: String, forChunk number: Int) {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard rawChunks[number] != nil, !trimmed.isEmpty else { return }
        cleanedChunks[number] = trimmed
    }

    /// The final text: cleaned where available, raw otherwise, in spoken order.
    public var text: String {
        join { cleanedChunks[$0] ?? rawChunks[$0] }
    }

    /// What Deepgram heard, untouched.
    public var rawText: String {
        join { rawChunks[$0] }
    }

    /// Raw text of the most recent chunk, as context for cleaning the next one.
    public func lastRawWords(_ count: Int) -> String {
        guard let last = rawChunks[nextNumber - 1] else { return "" }
        return last.split(whereSeparator: \.isWhitespace).suffix(count).joined(separator: " ")
    }

    public var isEmpty: Bool {
        rawChunks.isEmpty
    }

    /// Whether at least one chunk was cleaned.
    public var hasCleanedText: Bool {
        !cleanedChunks.isEmpty
    }

    private func join(_ textForChunk: (Int) -> String?) -> String {
        (0..<nextNumber).compactMap(textForChunk).joined(separator: " ")
    }
}
