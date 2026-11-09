import Foundation

/// Collects finalized transcript segments in arrival order and joins them into one text.
public struct TranscriptBuffer: Equatable, Sendable {
    private var segments: [String] = []

    public init() {}

    public mutating func append(_ segment: String) {
        let trimmed = segment.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        segments.append(trimmed)
    }

    public var text: String {
        segments.joined(separator: " ")
    }

    public var isEmpty: Bool {
        segments.isEmpty
    }
}
