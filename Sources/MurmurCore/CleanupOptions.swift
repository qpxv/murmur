import Foundation

/// How polished the cleaned text should read.
public enum CleanupStyle: String, Codable, CaseIterable, Sendable {
    /// lowercase, barely any punctuation, words untouched
    case casual
    /// as spoken, just corrected
    case normal
    /// rewritten into polished, professional prose (same meaning)
    case formal
}

/// What Claude is allowed to change. Part of `Settings`.
public struct CleanupOptions: Codable, Equatable, Sendable {
    public var style: CleanupStyle
    public var fixesMisheardWords: Bool
    public var fixesPunctuation: Bool
    public var removesStutters: Bool

    public static let defaults = Self(
        style: .normal,
        fixesMisheardWords: true,
        fixesPunctuation: true,
        removesStutters: true
    )

    private init(style: CleanupStyle, fixesMisheardWords: Bool, fixesPunctuation: Bool, removesStutters: Bool) {
        self.style = style
        self.fixesMisheardWords = fixesMisheardWords
        self.fixesPunctuation = fixesPunctuation
        self.removesStutters = removesStutters
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let fallback = Self.defaults
        style = try container.decodeIfPresent(CleanupStyle.self, forKey: .style) ?? fallback.style
        fixesMisheardWords =
            try container.decodeIfPresent(Bool.self, forKey: .fixesMisheardWords) ?? fallback.fixesMisheardWords
        fixesPunctuation =
            try container.decodeIfPresent(Bool.self, forKey: .fixesPunctuation) ?? fallback.fixesPunctuation
        removesStutters =
            try container.decodeIfPresent(Bool.self, forKey: .removesStutters) ?? fallback.removesStutters
    }

    /// Whether there's anything for Claude to do. With every fix off and the normal style,
    /// calling Claude would only cost time, so the raw text is pasted directly.
    public var hasWork: Bool {
        style != .normal || fixesMisheardWords || fixesPunctuation || removesStutters
    }
}
