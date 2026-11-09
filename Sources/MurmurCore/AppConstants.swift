import Foundation

/// Single source of truth for every tunable number and identifier in the app.
/// Anything a second file needs goes here instead of being repeated.
public enum AppConstants {
    public enum Hotkey {
        /// An unlocked press shorter than this is treated as accidental and discarded.
        public static let minimumHold: TimeInterval = 0.3
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
    }

    public enum Deepgram {
        public static let listenURL = "wss://api.deepgram.com/v1/listen"
        public static let model = "nova-3"
        public static let language = "en"
        /// How long to wait for the last words after asking Deepgram to finalize.
        public static let finalizeTimeout: TimeInterval = 3
    }

    public enum Paste {
        /// Time the target app gets to read the pasteboard before the old clipboard returns.
        public static let clipboardRestoreDelay: TimeInterval = 0.8
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
    }
}
