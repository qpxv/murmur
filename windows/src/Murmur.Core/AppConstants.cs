namespace Murmur.Core;

/// <summary>
/// Single source of truth for every tunable number and identifier in the Windows app.
/// Values both apps must share come from <c>shared/config.json</c> through <see cref="SharedFiles"/>.
/// </summary>
public static class AppConstants
{
    public const string AppName = "Murmur";

    public static class Hotkey
    {
        /// <summary>An unlocked press shorter than this is treated as accidental and discarded.</summary>
        public static readonly TimeSpan MinimumHold = TimeSpan.FromMilliseconds(300);
    }

    public static class Audio
    {
        /// <summary>What Deepgram receives: 16 kHz mono 16-bit PCM, plenty for speech.</summary>
        public const int SampleRate = 16_000;
        public const int ChannelCount = 1;
        /// <summary>Capture buffer length: short enough for a responsive voice bar.</summary>
        public const int BufferMilliseconds = 20;
        /// <summary>Loudness (dBFS) that maps to an empty / full voice bar.</summary>
        public const float SilenceFloorDecibels = -55;
        public const float FullScaleDecibels = -10;
        /// <summary>A sample this close to full scale (1.0) counts as clipping.</summary>
        public const float ClippingThreshold = 0.99f;
        /// <summary>Mic volume while recording with the boost on (endpoint scalar, 0 to 1).</summary>
        public const float BoostedVolume = 1;
    }

    public static class Deepgram
    {
        private static readonly SharedConfig.DeepgramConfig Shared = SharedFiles.Config.Deepgram;
        public static readonly Uri ListenUrl = Shared.ListenUrl;
        public static readonly string Model = Shared.Model;
        public static readonly string Language = Shared.Language;
        /// <summary>How long to wait for the last words after asking Deepgram to finalize.</summary>
        public static readonly TimeSpan FinalizeTimeout = TimeSpan.FromSeconds(Shared.FinalizeTimeoutSeconds);
        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    }

    public static class Cleanup
    {
        private static readonly SharedConfig.CleanupConfig Shared = SharedFiles.Config.Cleanup;
        public static readonly string Model = Shared.Model;
        /// <summary>Enables <c>fallbacks: "default"</c>: a refused request is rerouted inside the same call.</summary>
        public static readonly string FallbackBeta = Shared.FallbackBeta;
        /// <summary>Thinking can't be turned off on this model; low effort keeps it short.</summary>
        public static readonly string Effort = Shared.Effort;
        public static readonly int MaxTokens = Shared.MaxTokens;
        /// <summary>A chunk closes at the next pause once it has this many words...</summary>
        public static readonly int MinChunkWords = Shared.MinChunkWords;
        /// <summary>...or right away at this many, pause or not.</summary>
        public static readonly int MaxChunkWords = Shared.MaxChunkWords;
        /// <summary>Words of the neighbouring chunks sent along as read-only context.</summary>
        public static readonly int ContextWords = Shared.ContextWords;
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(Shared.RequestTimeoutSeconds);
        /// <summary>After you stop, how long to wait for cleanup before pasting raw text for unfinished chunks.</summary>
        public static readonly TimeSpan FinishTimeout = TimeSpan.FromSeconds(Shared.FinishTimeoutSeconds);
    }

    public static class Sync
    {
        /// <summary>Holds one row per dictation from every device: numbers only, never text.</summary>
        public const string TableName = "murmur_dictations";
        /// <summary>Neon's HTTPS SQL endpoint lives at this path on the database host.</summary>
        public const string EndpointPath = "/sql";
        public const string ConnectionHeader = "Neon-Connection-String";
        public const string ArrayModeHeader = "Neon-Array-Mode";
        public static readonly IReadOnlySet<string> DatabaseSchemes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "postgres", "postgresql" };
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    }

    public static class Paste
    {
        /// <summary>Time the target app gets to read the clipboard before the old content returns.</summary>
        public static readonly TimeSpan ClipboardRestoreDelay = TimeSpan.FromMilliseconds(800);
        /// <summary>The clipboard can be held open by another app for a moment; retry this often.</summary>
        public const int ClipboardAttempts = 5;
        public static readonly TimeSpan ClipboardRetryDelay = TimeSpan.FromMilliseconds(50);
        /// <summary>
        /// Time a Chromium app (Chrome, Edge, Teams, other WebView2 apps) gets to build its
        /// accessibility tree after Murmur first asks for it.
        /// </summary>
        public static readonly TimeSpan AccessibilityWarmupDelay = TimeSpan.FromMilliseconds(250);
    }

    public static class Update
    {
        /// <summary>The repo's releases (public, so no token needed).</summary>
        public static readonly Uri ReleasesUrl = new("https://api.github.com/repos/qpxv/murmur/releases");
        /// <summary>Only tags with this prefix are windows releases (the repo may get others).</summary>
        public const string TagPrefix = "windows-v";
        public const string AssetName = "Murmur.exe";
        /// <summary>GitHub's API refuses requests without a user agent.</summary>
        public const string UserAgent = "Murmur-Updater";
        /// <summary>The exe is about 80 MB.</summary>
        public static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);
        /// <summary>Passed to the new exe, so it waits for the old one to quit before starting.</summary>
        public const string RestartArgument = "--after-update";
        public static readonly TimeSpan RestartWait = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan RestartPollInterval = TimeSpan.FromMilliseconds(200);
    }

    public static class Overlay
    {
        /// <summary>How long an error stays visible in the pill before it returns to idle.</summary>
        public static readonly TimeSpan ErrorDisplayDuration = TimeSpan.FromSeconds(3);
        /// <summary>Number of bars in the voice bar, and therefore how many recent levels are kept.</summary>
        public const int VoiceBarCount = 14;
    }

    public static class Storage
    {
        /// <summary>Folder under %LOCALAPPDATA% that holds settings, history and the log.</summary>
        public const string FolderName = AppName;
        public const string SettingsFile = "settings.json";
        public const string HistoryFile = "history.json";
        public const string LogFile = "murmur.log";
        /// <summary>The mic volume before a boost, so the next launch can undo a boost a crash left behind.</summary>
        public const string MicRestoreFile = "mic-restore.json";
        /// <summary>The log is cut back to its newest half once it grows past this.</summary>
        public const long MaxLogBytes = 1_000_000;
        /// <summary>Per-user autostart: a value under this registry key starts the exe at login.</summary>
        public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    }

    public static class Credentials
    {
        /// <summary>Credential Manager targets ("Windows Credentials", generic).</summary>
        public const string DeepgramTarget = "Murmur/deepgram-api-key";
        public const string AnthropicTarget = "Murmur/anthropic-api-key";
        public const string NeonTarget = "Murmur/neon-database-url";
    }

    public static class DiagnosticsReport
    {
        /// <summary>How many of the newest log lines "Copy diagnostics" includes.</summary>
        public const int LogLines = 80;
    }
}
