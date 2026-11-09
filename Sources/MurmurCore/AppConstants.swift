import Foundation

/// Single source of truth for every tunable number and identifier in the app.
/// Anything a second file needs goes here instead of being repeated.
public enum AppConstants {
    public enum Hotkey {
        /// An unlocked press shorter than this is treated as accidental and discarded.
        public static let minimumHold: TimeInterval = 0.3
        /// Wait after the system's "accessibility changed" broadcast before re-checking trust.
        public static let accessibilitySettleDelay: TimeInterval = 0.5
    }

    public enum Audio {
        /// What Deepgram receives: 16 kHz mono 16-bit PCM, plenty for speech.
        public static let sampleRate: Double = 16_000
        public static let channelCount: UInt32 = 1
        /// Frames per tap callback at the hardware rate (~20 ms at 48 kHz): keeps latency and
        /// the voice bar responsive.
        public static let tapBufferFrames: UInt32 = 1_024
        /// Loudness (dBFS) that maps to an empty / full voice bar.
        public static let silenceFloorDecibels: Float = -55
        public static let fullScaleDecibels: Float = -10
        /// A sample this close to full scale (1.0) counts as clipping.
        public static let clippingThreshold: Float = 0.99
        /// UID prefix of the private aggregate device core audio creates for a process's own
        /// AVAudioEngine; it shows up in the device list but isn't a microphone.
        public static let privateAggregateUIDPrefix = "CADefaultDeviceAggregate"
    }

    public enum Deepgram {
        public static let listenURL = "wss://api.deepgram.com/v1/listen"
        public static let model = "nova-3"
        public static let language = "en"
        /// How long to wait for the last words after asking Deepgram to finalize.
        public static let finalizeTimeout: TimeInterval = 3
    }

    public enum Cleanup {
        public static let messagesURL = "https://api.anthropic.com/v1/messages"
        public static let model = "claude-opus-5-5"
        public static let apiVersion = "2026-10-21"
        /// Enables `fallbacks: "default"`: a refused request is rerouted inside the same call.
        public static let fallbackBeta = "server-side-fallback-2026-07-01"
        /// Thinking can't be turned off on this model; low effort keeps it short.
        public static let effort = "low"
        /// Generous: thinking counts against it, and a 150-word chunk needs ~250 tokens of text.
        public static let maxTokens = 4_096
        /// A chunk closes at the next pause once it has this many words...
        public static let minChunkWords = 40
        /// ...or right away at this many, pause or not.
        public static let maxChunkWords = 150
        /// Words of the previous chunk sent along as read-only context.
        public static let contextWords = 30
        /// Per request. Chunks cleaned while you speak have time; the wait after stopping is capped below.
        public static let requestTimeout: TimeInterval = 20
        /// After you stop, how long to wait for cleanup before pasting raw text for unfinished chunks.
        public static let finishTimeout: TimeInterval = 7
    }

    public enum Sync {
        /// Holds one row per dictation from every device: numbers only, never text.
        public static let tableName = "murmur_dictations"
        /// Neon's HTTPS SQL endpoint lives at this path on the database host.
        public static let endpointPath = "/sql"
        public static let connectionHeader = "Neon-Connection-String"
        public static let arrayModeHeader = "Neon-Array-Mode"
        public static let databaseSchemes: Set<String> = ["postgres", "postgresql"]
        public static let requestTimeout: TimeInterval = 15
    }

    public enum Paste {
        /// Time the target app gets to read the pasteboard before the old clipboard returns.
        public static let clipboardRestoreDelay: TimeInterval = 0.8
        /// Time a Chromium app gets to build its accessibility tree after Murmur first asks for it.
        public static let accessibilityWarmupDelay: TimeInterval = 0.25
    }

    public enum Overlay {
        /// How long an error stays visible in the pill before it returns to idle.
        public static let errorDisplayDuration: TimeInterval = 3
        /// Number of bars in the voice bar, and therefore how many recent levels are kept.
        public static let voiceBarCount = 14
    }

    public enum Storage {
        public static let settingsKey = "settings.v1"
        public static let pendingMicRestoreKey = "micGain.pendingRestore.v1"
    }

    public enum Keychain {
        public static let deepgramAccount = "deepgram-api-key"
        public static let anthropicAccount = "anthropic-api-key"
        public static let neonAccount = "neon-database-url"
    }
}
