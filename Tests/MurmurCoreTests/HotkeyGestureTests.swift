import MurmurCore
import Testing

struct HotkeyGestureTests {
    @Test func holdingIsPushToTalk() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        #expect(gesture.hotkeyDown(at: 2.0) == .start)
        #expect(gesture.hotkeyUp(at: 4.5) == .stop)
        #expect(gesture.hotkeyDown(at: 5.0) == .start)
    }

    @Test func shortAccidentalPressCancels() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        _ = gesture.hotkeyDown(at: 10.0)
        #expect(gesture.hotkeyUp(at: 10.1) == .cancel)
        #expect(gesture.hotkeyDown(at: 11.0) == .start)
    }

    @Test func releaseExactlyAtMinimumHoldStops() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        _ = gesture.hotkeyDown(at: 0.0)
        #expect(gesture.hotkeyUp(at: 0.3) == .stop)
    }

    @Test func spaceWhileHoldingLocksAndReleaseKeepsRecording() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        _ = gesture.hotkeyDown(at: 1.0)
        #expect(gesture.spaceDown() == .lock)
        #expect(gesture.hotkeyUp(at: 4.0) == .none)
    }

    @Test func quickFnSpaceLockIsNotCancelled() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        _ = gesture.hotkeyDown(at: 1.0)
        _ = gesture.spaceDown()
        #expect(gesture.hotkeyUp(at: 1.1) == .none)
    }

    @Test func nextPressStopsLockedRecordingAndSwallowsItsRelease() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        _ = gesture.hotkeyDown(at: 1.0)
        _ = gesture.spaceDown()
        _ = gesture.hotkeyUp(at: 2.0)
        #expect(gesture.hotkeyDown(at: 60.0) == .stop)
        #expect(gesture.hotkeyUp(at: 60.05) == .none)
        #expect(gesture.hotkeyDown(at: 61.0) == .start)
    }

    @Test func spaceIsIgnoredWhenHotkeyIsNotHeld() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        #expect(gesture.spaceDown() == .none)
        _ = gesture.hotkeyDown(at: 1.0)
        _ = gesture.spaceDown()
        _ = gesture.hotkeyUp(at: 2.0)
        #expect(gesture.spaceDown() == .none)
    }

    @Test func secondSpaceWhileLockedHeldIsIgnored() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        _ = gesture.hotkeyDown(at: 1.0)
        _ = gesture.spaceDown()
        #expect(gesture.spaceDown() == .none)
    }

    @Test func otherKeyWhileHoldingCancelsAndReleaseIsIgnored() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        _ = gesture.hotkeyDown(at: 1.0)
        #expect(gesture.otherKeyDown() == .cancel)
        #expect(gesture.hotkeyUp(at: 3.0) == .none)
        #expect(gesture.hotkeyDown(at: 4.0) == .start)
    }

    @Test func otherKeyWhileLockedKeepsRecording() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        _ = gesture.hotkeyDown(at: 1.0)
        _ = gesture.spaceDown()
        _ = gesture.hotkeyUp(at: 2.0)
        #expect(gesture.otherKeyDown() == .none)
        #expect(gesture.hotkeyDown(at: 9.0) == .stop)
    }

    @Test func keyRepeatWhileHeldIsIgnored() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        _ = gesture.hotkeyDown(at: 0.0)
        #expect(gesture.hotkeyDown(at: 0.5) == .none)
        #expect(gesture.hotkeyUp(at: 1.0) == .stop)
    }

    @Test func strayReleaseWhileIdleDoesNothing() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        #expect(gesture.hotkeyUp(at: 3.0) == .none)
    }

    @Test func resetDropsLockedRecording() {
        var gesture = HotkeyGesture(minimumHold: 0.3)
        _ = gesture.hotkeyDown(at: 0.0)
        _ = gesture.spaceDown()
        _ = gesture.hotkeyUp(at: 1.0)
        gesture.reset()
        #expect(gesture.hotkeyDown(at: 2.0) == .start)
    }
}
