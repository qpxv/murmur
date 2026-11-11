namespace Murmur.Core.Tests;

public class HotkeyRecorderTests
{
    private const uint LetterF = 0x46;
    private const uint LetterC = 0x43;
    private const uint RightControl = 0xA3;
    private const uint LeftControl = 0xA2;
    private const uint LeftAlt = 0xA4;
    private const uint RightAlt = 0xA5;

    [Fact]
    public void Normal_key_is_recorded_on_key_down() =>
        Assert.Equal(HotkeyRecording.Recorded(new Hotkey(LetterF)), new HotkeyRecorder().KeyDown(LetterF));

    [Fact]
    public void Caps_lock_can_be_recorded_back() =>
        Assert.Equal(HotkeyRecording.Recorded(Hotkey.CapsLock), new HotkeyRecorder().KeyDown(VirtualKeys.CapsLock));

    [Fact]
    public void Modifier_is_recorded_on_its_release_even_after_key_repeat()
    {
        var recorder = new HotkeyRecorder();
        Assert.Equal(HotkeyRecording.Waiting, recorder.KeyDown(RightAlt));
        Assert.Equal(HotkeyRecording.Waiting, recorder.KeyDown(RightAlt));
        Assert.Equal(HotkeyRecording.Recorded(new Hotkey(RightAlt)), recorder.KeyUp(RightAlt));
    }

    [Fact]
    public void Modifier_plus_key_is_a_combination()
    {
        var recorder = new HotkeyRecorder();
        recorder.KeyDown(LeftControl);
        Assert.Equal(HotkeyRecording.Rejected(HotkeyRejection.Combination), recorder.KeyDown(LetterC));
    }

    [Fact]
    public void Two_modifiers_are_a_combination()
    {
        var recorder = new HotkeyRecorder();
        recorder.KeyDown(RightControl);
        Assert.Equal(HotkeyRecording.Rejected(HotkeyRejection.Combination), recorder.KeyDown(LeftAlt));
    }

    [Fact]
    public void Release_of_a_key_held_before_recording_is_ignored()
    {
        var recorder = new HotkeyRecorder();
        Assert.Equal(HotkeyRecording.Waiting, recorder.KeyUp(LeftControl));
        Assert.Equal(HotkeyRecording.Recorded(new Hotkey(LetterF)), recorder.KeyDown(LetterF));
    }

    [Fact]
    public void Space_is_rejected() =>
        Assert.Equal(HotkeyRecording.Rejected(HotkeyRejection.Space), new HotkeyRecorder().KeyDown(VirtualKeys.Space));

    [Fact]
    public void Escape_cancels() =>
        Assert.Equal(HotkeyRecording.Cancelled, new HotkeyRecorder().KeyDown(VirtualKeys.Escape));

    [Fact]
    public void Windows_keys_count_as_modifiers()
    {
        Assert.True(new Hotkey(VirtualKeys.LeftWindows).IsModifier);
        Assert.True(new Hotkey(RightControl).IsModifier);
        Assert.False(Hotkey.CapsLock.IsModifier);
    }
}
