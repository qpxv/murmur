import AppKit
import MurmurCore
import MurmurServices
import Observation
import os

/// Runs one dictation at a time: hotkey -> mic -> Deepgram -> Claude cleanup -> paste or clipboard.
/// Owns the one `DictationState` the overlay and menu render.
@MainActor
@Observable
final class DictationController {
    private(set) var state: DictationState = .idle
    /// Most recent mic levels (0...1), oldest first, for the voice bar.
    private(set) var levels = DictationController.silentLevels
    private(set) var isHotkeyActive = false

    private static let silentLevels = [Float](repeating: 0, count: AppConstants.Overlay.voiceBarCount)

    @ObservationIgnored private let settingsStore: SettingsStore
    @ObservationIgnored private let micGain = MicGain()
    @ObservationIgnored private let audio = AudioCapture()
    @ObservationIgnored private var gesture = HotkeyGesture()
    @ObservationIgnored private var active: ActiveDictation?
    @ObservationIgnored private var errorResetTask: Task<Void, Never>?
    @ObservationIgnored private var hotkeys: HotkeyMonitor?
    @ObservationIgnored private let logger = Logger(subsystem: Log.subsystem, category: "dictation")

    init(settingsStore: SettingsStore) {
        self.settingsStore = settingsStore
    }

    /// Called once at launch: undo a mic boost a crash left behind, ask for permissions,
    /// start listening for fn.
    func activate() {
        micGain.restorePendingFromLastRun()
        Task { _ = await Permissions.requestMicrophone() }
        enableHotkey()
    }

    /// Installs the fn listener, prompting for Accessibility if it's missing.
    func enableHotkey() {
        let monitor = hotkeys ?? HotkeyMonitor { [weak self] event in self?.handle(event) ?? false }
        hotkeys = monitor
        isHotkeyActive = monitor.start()
        if !isHotkeyActive {
            Permissions.promptForAccessibility()
        }
    }

    /// Called on quit: never leave the mic boosted or a socket open.
    func shutdown() {
        cancel()
    }

    private func handle(_ event: HotkeyMonitor.Event) -> Bool {
        let action: HotkeyAction =
            switch event {
            case .hotkeyDown(let time): gesture.hotkeyDown(at: time)
            case .hotkeyUp(let time): gesture.hotkeyUp(at: time)
            case .space: gesture.spaceDown()
            case .otherKey: gesture.otherKeyDown()
            }
        perform(action)
        // every space typed while fn is down during a recording belongs to the lock gesture
        if case .space = event { return state.isRecording }
        return false
    }

    private func perform(_ action: HotkeyAction) {
        switch action {
        case .start: start()
        case .lock: state = state.applying(.lock)
        case .stop: stop()
        case .cancel: cancel()
        case .none: break
        }
    }

    private func start() {
        guard state.applying(.start) != state else {
            // still finishing the previous dictation
            gesture.reset()
            return
        }
        guard let apiKey = Keychain.read(account: AppConstants.Keychain.deepgramAccount), !apiKey.isEmpty else {
            fail(.missingDeepgramKey)
            return
        }
        guard Permissions.isMicrophoneAuthorized else {
            fail(.microphoneDenied)
            return
        }
        do {
            try begin(apiKey: apiKey)
        } catch {
            fail(error)
        }
    }

    private func begin(apiKey: String) throws(DictationError) {
        errorResetTask?.cancel()
        let settings = settingsStore.settings
        let stream = try DeepgramStream(apiKey: apiKey, vocabulary: settings.vocabulary)
        let session = CleanupSession(
            cleaner: makeCleaner(settings: settings),
            vocabulary: settings.vocabulary,
            appName: NSWorkspace.shared.frontmostApplication?.localizedName
        )
        if settings.isMicBoostEnabled, let device = CoreAudioDevice.defaultInput() {
            micGain.boost(device: device)
        }
        try audio.start { [weak self] pcm, level in
            stream.send(pcm)
            Task { @MainActor in self?.record(level: level) }
        }
        Task { await stream.start() }
        let forwarding = Task {
            for await result in stream.results {
                await session.add(result)
            }
        }
        active = ActiveDictation(stream: stream, session: session, forwarding: forwarding)
        levels = Self.silentLevels
        state = state.applying(.start)
    }

    private func makeCleaner(settings: Settings) -> ClaudeCleaner? {
        guard settings.isCleanupEnabled else { return nil }
        guard let key = Keychain.read(account: AppConstants.Keychain.anthropicAccount), !key.isEmpty else {
            logger.info("cleanup is on but no claude key is set, pasting raw text")
            return nil
        }
        return ClaudeCleaner(apiKey: key)
    }

    private func stop() {
        guard state.isRecording else { return }
        state = state.applying(.stop)
        stopCapture()
        guard let active else {
            state = .idle
            return
        }
        self.active = nil
        Task { await deliver(active) }
    }

    private func deliver(_ dictation: ActiveDictation) async {
        do {
            try await dictation.stream.finish()
        } catch {
            await dictation.session.cancel()
            fail(error)
            return
        }
        // every segment has reached the session once forwarding ends with the stream
        await dictation.forwarding.value
        let transcript = await dictation.session.finish()
        guard !transcript.text.isEmpty else {
            fail(.nothingHeard)
            return
        }
        if TextInserter.insert(transcript.text) == .copiedToClipboard {
            await Notifier.post(title: UserMessages.copiedTitle, body: UserMessages.copiedBody)
        }
        state = .idle
    }

    private func cancel() {
        stopCapture()
        if let active {
            Task {
                await active.stream.cancel()
                await active.session.cancel()
            }
        }
        active = nil
        gesture.reset()
        state = state.isRecording ? state.applying(.cancel) : state
    }

    private func stopCapture() {
        audio.stop()
        micGain.restore()
        levels = Self.silentLevels
    }

    private func fail(_ error: DictationError) {
        logger.error("dictation failed: \(String(describing: error))")
        cancel()
        state = .failed(error)
        errorResetTask?.cancel()
        errorResetTask = Task {
            try? await Task.sleep(for: .seconds(AppConstants.Overlay.errorDisplayDuration))
            guard !Task.isCancelled, case .failed = state else { return }
            state = .idle
        }
    }

    private func record(level: Float) {
        guard state.isRecording else { return }
        levels.removeFirst()
        levels.append(level)
    }
}

/// The pieces of the one dictation in progress, created and torn down together.
private struct ActiveDictation {
    let stream: DeepgramStream
    let session: CleanupSession
    /// Moves Deepgram's segments into the cleanup session; ends when the stream ends.
    let forwarding: Task<Void, Never>
}
