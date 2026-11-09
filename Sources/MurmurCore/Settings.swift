import Foundation

/// Everything the user can configure. Exists exactly once; the UI binds to it
/// through `SettingsStore`.
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

    public static let defaults = Self(
        isMicBoostEnabled: true,
        inputDeviceUID: nil,
        vocabulary: ["Claude", "Claude Code", "Anthropic", "Opus", "Sonnet", "Haiku", "cmux", "SwiftUI"],
        overlayPosition: .bottomCenter,
        isCleanupEnabled: true
    )

    public init(
        isMicBoostEnabled: Bool,
        inputDeviceUID: String?,
        vocabulary: [String],
        overlayPosition: OverlayPosition,
        isCleanupEnabled: Bool
    ) {
        self.isMicBoostEnabled = isMicBoostEnabled
        self.inputDeviceUID = inputDeviceUID
        self.vocabulary = vocabulary
        self.overlayPosition = overlayPosition
        self.isCleanupEnabled = isCleanupEnabled
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
    }
}
