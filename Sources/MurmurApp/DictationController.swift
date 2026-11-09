import MurmurCore
import MurmurServices
import Observation
import os

/// Runs one dictation at a time: hotkey -> mic -> Deepgram -> paste or clipboard.
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
    @ObservationIgnored private var stream: DeepgramStream?
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
        if settings.isMicBoostEnabled, let device = CoreAudioDevice.defaultInput() {
            micGain.boost(device: device)
        }
        try audio.start { [weak self] pcm, level in
            stream.send(pcm)
            Task { @MainActor in self?.record(level: level) }
        }
        Task { await stream.start() }
        self.stream = stream
        levels = Self.silentLevels
        state = state.applying(.start)
    }

    private func stop() {
        guard state.isRecording else { return }
        state = state.applying(.stop)
        stopCapture()
        guard let stream else {
            state = .idle
            return
        }
        self.stream = nil
        Task { await deliver(from: stream) }
    }

    private func deliver(from stream: DeepgramStream) async {
        do {
            let text = try await stream.finish()
            guard !text.isEmpty else {
                fail(.nothingHeard)
                return
            }
            if TextInserter.insert(text) == .copiedToClipboard {
                await Notifier.post(title: UserMessages.copiedTitle, body: UserMessages.copiedBody)
            }
            state = .idle
        } catch {
            fail(error)
        }
    }

    private func cancel() {
        stopCapture()
        if let stream {
            Task { await stream.cancel() }
        }
        stream = nil
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
