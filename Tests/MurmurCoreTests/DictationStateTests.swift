import MurmurCore
import Testing

struct DictationStateTests {
    @Test func startFromIdleRecordsPushToTalk() {
        #expect(DictationState.idle.applying(.start) == .recording(.pushToTalk))
    }

    @Test func lockSwitchesRecordingToLocked() {
        #expect(DictationState.recording(.pushToTalk).applying(.lock) == .recording(.locked))
    }

    @Test(arguments: [RecordingMode.pushToTalk, .locked])
    func stopWhileRecordingFinishes(mode: RecordingMode) {
        #expect(DictationState.recording(mode).applying(.stop) == .finishing)
    }

    @Test(arguments: [RecordingMode.pushToTalk, .locked])
    func cancelWhileRecordingReturnsToIdle(mode: RecordingMode) {
        #expect(DictationState.recording(mode).applying(.cancel) == .idle)
    }

    @Test func startAfterFailureRecordsAgain() {
        #expect(DictationState.failed(.connectionFailed).applying(.start) == .recording(.pushToTalk))
    }

    @Test(arguments: [HotkeyAction.start, .lock, .stop, .cancel, .none])
    func finishingIgnoresHotkey(action: HotkeyAction) {
        #expect(DictationState.finishing.applying(action) == .finishing)
    }

    @Test func stopWhileIdleChangesNothing() {
        #expect(DictationState.idle.applying(.stop) == .idle)
    }
}
