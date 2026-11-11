import CoreGraphics
import MurmurCore
import Testing

struct HotkeyRecorderTests {
    private let letterF: UInt16 = 0x03
    private let letterC: UInt16 = 0x08

    @Test func normalKeyIsRecordedOnKeyDown() {
        var recorder = HotkeyRecorder()
        #expect(recorder.keyDown(letterF) == .recorded(Hotkey(keyCode: letterF)))
    }

    @Test func modifierIsRecordedOnItsRelease() {
        var recorder = HotkeyRecorder()
        #expect(recorder.modifierDown(Hotkey.KeyCode.rightCommand) == .waiting)
        #expect(
            recorder.modifierUp(Hotkey.KeyCode.rightCommand)
                == .recorded(Hotkey(keyCode: Hotkey.KeyCode.rightCommand))
        )
    }

    @Test func repeatedModifierDownStillWaits() {
        var recorder = HotkeyRecorder()
        _ = recorder.modifierDown(Hotkey.KeyCode.rightOption)
        #expect(recorder.modifierDown(Hotkey.KeyCode.rightOption) == .waiting)
    }

    @Test func fnCanBeRecordedBack() {
        var recorder = HotkeyRecorder()
        _ = recorder.modifierDown(Hotkey.KeyCode.function)
        #expect(recorder.modifierUp(Hotkey.KeyCode.function) == .recorded(.function))
    }

    @Test func modifierPlusKeyIsACombination() {
        var recorder = HotkeyRecorder()
        _ = recorder.modifierDown(Hotkey.KeyCode.command)
        #expect(recorder.keyDown(letterC) == .rejected(.combination))
    }

    @Test func twoModifiersAreACombination() {
        var recorder = HotkeyRecorder()
        _ = recorder.modifierDown(Hotkey.KeyCode.control)
        #expect(recorder.modifierDown(Hotkey.KeyCode.option) == .rejected(.combination))
    }

    @Test func releaseOfAModifierHeldBeforeRecordingIsIgnored() {
        var recorder = HotkeyRecorder()
        #expect(recorder.modifierUp(Hotkey.KeyCode.shift) == .waiting)
        #expect(recorder.keyDown(letterF) == .recorded(Hotkey(keyCode: letterF)))
    }

    @Test func spaceIsRejected() {
        var recorder = HotkeyRecorder()
        #expect(recorder.keyDown(Hotkey.KeyCode.space) == .rejected(.space))
    }

    @Test func capsLockIsRejected() {
        var recorder = HotkeyRecorder()
        #expect(recorder.modifierDown(Hotkey.KeyCode.capsLock) == .rejected(.capsLock))
    }

    @Test func escapeCancels() {
        var recorder = HotkeyRecorder()
        #expect(recorder.keyDown(Hotkey.KeyCode.escape) == .cancelled)
    }
}

struct HotkeyTests {
    // NX_DEVICELCMDKEYMASK and NX_DEVICERCMDKEYMASK
    private let leftCommandBit = CGEventFlags(rawValue: 0x08)
    private let rightCommandBit = CGEventFlags(rawValue: 0x10)
    private let rightCommand = Hotkey(keyCode: Hotkey.KeyCode.rightCommand)

    @Test func heldRightCommandIsRemovedFromOtherKeys() {
        let flags: CGEventFlags = [.maskCommand, rightCommandBit, .maskShift]
        #expect(rightCommand.removingModifier(from: flags) == [.maskShift])
    }

    @Test func leftCommandStillCountsWhileRightIsTheHotkey() {
        let flags: CGEventFlags = [.maskCommand, rightCommandBit, leftCommandBit]
        #expect(rightCommand.removingModifier(from: flags) == [.maskCommand, leftCommandBit])
    }

    @Test func fnIsNeverRemoved() {
        let flags: CGEventFlags = [.maskSecondaryFn]
        #expect(Hotkey.function.removingModifier(from: flags) == flags)
        #expect(!Hotkey.function.isTakenOver)
    }

    @Test func normalKeyLeavesFlagsAlone() {
        let flags: CGEventFlags = [.maskCommand, leftCommandBit]
        #expect(Hotkey(keyCode: 0x60).removingModifier(from: flags) == flags)
        #expect(Hotkey(keyCode: 0x60).modifier == nil)
    }

    @Test func modifierIsDownWhenItsOwnBitIsSet() {
        let modifier = rightCommand.modifier
        #expect(modifier?.isDown(in: [.maskCommand, rightCommandBit]) == true)
        #expect(modifier?.isDown(in: [.maskCommand, leftCommandBit]) == false)
    }
}
