import Foundation

/// Everything the user can configure. Exists exactly once; the UI binds to it
/// through `SettingsStore`. Start from `Settings.defaults` and change what you need.
public struct Settings: Codable, Equatable, Sendable {
    /// Sets the input device to full volume while recording, so whispering works.
    public var isMicBoostEnabled: Bool
    /// Core Audio UID of the chosen microphone; `nil` follows the system default.
    public var inputDeviceUID: String?
    /// Terms that must be spelled exactly like this (sent to speech-to-text and cleanup).
    public var vocabulary: [String]
    /// Where the overlay pill sits on screen.
    public var overlayPosition: OverlayPosition
    /// Let Claude fix misheard words and punctuation before pasting.
    public var isCleanupEnabled: Bool
    /// Which fixes Claude makes and in what style.
    public var cleanup: CleanupOptions
    /// The user's typing speed, for the "time saved" number on the dashboard.
    public var typingWordsPerMinute: Int
    /// Keep the text of past dictations. Off still counts words and time for the dashboard.
    public var isHistoryEnabled: Bool

    public static let defaults = Self(
        isMicBoostEnabled: true,
        inputDeviceUID: nil,
        vocabulary: ["Claude", "Claude Code", "Anthropic", "Opus", "Sonnet", "Haiku", "cmux", "SwiftUI"],
        overlayPosition: .bottomCenter,
        isCleanupEnabled: true,
        cleanup: .defaults,
        typingWordsPerMinute: 100,
        isHistoryEnabled: true
    )

    private init(
        isMicBoostEnabled: Bool,
        inputDeviceUID: String?,
        vocabulary: [String],
        overlayPosition: OverlayPosition,
        isCleanupEnabled: Bool,
        cleanup: CleanupOptions,
        typingWordsPerMinute: Int,
        isHistoryEnabled: Bool
    ) {
        self.isMicBoostEnabled = isMicBoostEnabled
        self.inputDeviceUID = inputDeviceUID
        self.vocabulary = vocabulary
        self.overlayPosition = overlayPosition
        self.isCleanupEnabled = isCleanupEnabled
        self.cleanup = cleanup
        self.typingWordsPerMinute = typingWordsPerMinute
        self.isHistoryEnabled = isHistoryEnabled
    }

    // missing keys fall back to the defaults, so adding a setting never wipes the saved ones
    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let fallback = Self.defaults
        isMicBoostEnabled =
            try container.decodeIfPresent(Bool.self, forKey: .isMicBoostEnabled) ?? fallback.isMicBoostEnabled
        inputDeviceUID = try container.decodeIfPresent(String.self, forKey: .inputDeviceUID)
        vocabulary = try container.decodeIfPresent([String].self, forKey: .vocabulary) ?? fallback.vocabulary
        overlayPosition =
            try container.decodeIfPresent(OverlayPosition.self, forKey: .overlayPosition)
            ?? fallback.overlayPosition
        isCleanupEnabled =
            try container.decodeIfPresent(Bool.self, forKey: .isCleanupEnabled) ?? fallback.isCleanupEnabled
        cleanup = try container.decodeIfPresent(CleanupOptions.self, forKey: .cleanup) ?? fallback.cleanup
        typingWordsPerMinute =
            try container.decodeIfPresent(Int.self, forKey: .typingWordsPerMinute)
            ?? fallback.typingWordsPerMinute
        isHistoryEnabled =
            try container.decodeIfPresent(Bool.self, forKey: .isHistoryEnabled) ?? fallback.isHistoryEnabled
    }

    /// Adds a vocabulary term unless it's blank or already there (ignoring case).
    /// Returns whether it was added.
    @discardableResult
    public mutating func addVocabularyTerm(_ term: String) -> Bool {
        let trimmed = term.trimmingCharacters(in: .whitespacesAndNewlines)
        let isDuplicate = vocabulary.contains { $0.caseInsensitiveCompare(trimmed) == .orderedSame }
        guard !trimmed.isEmpty, !isDuplicate else { return false }
        vocabulary.append(trimmed)
        return true
    }
}
