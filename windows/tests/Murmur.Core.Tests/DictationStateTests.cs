
namespace Murmur.Core.Tests;

public class DictationStateTests
{
    [Fact]
    public void Start_from_idle_records_push_to_talk() =>
        Assert.Equal(DictationState.Recording(RecordingMode.PushToTalk), DictationState.Idle.Applying(HotkeyAction.Start));

    [Fact]
    public void Lock_switches_recording_to_locked() =>
        Assert.Equal(
            DictationState.Recording(RecordingMode.Locked),
            DictationState.Recording(RecordingMode.PushToTalk).Applying(HotkeyAction.Lock));

    [Theory]
    [InlineData(RecordingMode.PushToTalk)]
    [InlineData(RecordingMode.Locked)]
    public void Stop_while_recording_finishes(RecordingMode mode) =>
        Assert.Equal(DictationState.Finishing, DictationState.Recording(mode).Applying(HotkeyAction.Stop));

    [Theory]
    [InlineData(RecordingMode.PushToTalk)]
    [InlineData(RecordingMode.Locked)]
    public void Cancel_while_recording_returns_to_idle(RecordingMode mode) =>
        Assert.Equal(DictationState.Idle, DictationState.Recording(mode).Applying(HotkeyAction.Cancel));

    [Fact]
    public void Start_after_failure_records_again() =>
        Assert.Equal(
            DictationState.Recording(RecordingMode.PushToTalk),
            DictationState.Failed(DictationError.ConnectionFailed).Applying(HotkeyAction.Start));

    [Theory]
    [InlineData(HotkeyAction.Start)]
    [InlineData(HotkeyAction.Lock)]
    [InlineData(HotkeyAction.Stop)]
    [InlineData(HotkeyAction.Cancel)]
    [InlineData(HotkeyAction.None)]
    public void Finishing_ignores_hotkey(HotkeyAction action) =>
        Assert.Equal(DictationState.Finishing, DictationState.Finishing.Applying(action));

    [Fact]
    public void Stop_while_idle_changes_nothing() =>
        Assert.Equal(DictationState.Idle, DictationState.Idle.Applying(HotkeyAction.Stop));

    [Fact]
    public void Only_recording_counts_as_recording()
    {
        Assert.True(DictationState.Recording(RecordingMode.Locked).IsRecording);
        Assert.False(DictationState.Finishing.IsRecording);
    }
}
