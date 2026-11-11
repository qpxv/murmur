
namespace Murmur.Core.Tests;

public class HotkeyGestureTests
{
    private static HotkeyGesture Gesture() => new(TimeSpan.FromSeconds(0.3));

    private static TimeSpan At(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void Holding_is_push_to_talk()
    {
        var gesture = Gesture();
        Assert.Equal(HotkeyAction.Start, gesture.HotkeyDown(At(2.0)));
        Assert.Equal(HotkeyAction.Stop, gesture.HotkeyUp(At(4.5)));
        Assert.Equal(HotkeyAction.Start, gesture.HotkeyDown(At(5.0)));
    }

    [Fact]
    public void Short_accidental_press_cancels()
    {
        var gesture = Gesture();
        gesture.HotkeyDown(At(10.0));
        Assert.Equal(HotkeyAction.Cancel, gesture.HotkeyUp(At(10.1)));
        Assert.Equal(HotkeyAction.Start, gesture.HotkeyDown(At(11.0)));
    }

    [Fact]
    public void Release_exactly_at_minimum_hold_stops()
    {
        var gesture = Gesture();
        gesture.HotkeyDown(At(0.0));
        Assert.Equal(HotkeyAction.Stop, gesture.HotkeyUp(At(0.3)));
    }

    [Fact]
    public void Space_while_holding_locks_and_release_keeps_recording()
    {
        var gesture = Gesture();
        gesture.HotkeyDown(At(1.0));
        Assert.Equal(HotkeyAction.Lock, gesture.SpaceDown());
        Assert.Equal(HotkeyAction.None, gesture.HotkeyUp(At(4.0)));
    }

    [Fact]
    public void Quick_hotkey_space_lock_is_not_cancelled()
    {
        var gesture = Gesture();
        gesture.HotkeyDown(At(1.0));
        gesture.SpaceDown();
        Assert.Equal(HotkeyAction.None, gesture.HotkeyUp(At(1.1)));
    }

    [Fact]
    public void Next_press_stops_locked_recording_and_swallows_its_release()
    {
        var gesture = Gesture();
        gesture.HotkeyDown(At(1.0));
        gesture.SpaceDown();
        gesture.HotkeyUp(At(2.0));
        Assert.Equal(HotkeyAction.Stop, gesture.HotkeyDown(At(60.0)));
        Assert.Equal(HotkeyAction.None, gesture.HotkeyUp(At(60.05)));
        Assert.Equal(HotkeyAction.Start, gesture.HotkeyDown(At(61.0)));
    }

    [Fact]
    public void Space_is_ignored_when_hotkey_is_not_held()
    {
        var gesture = Gesture();
        Assert.Equal(HotkeyAction.None, gesture.SpaceDown());
        gesture.HotkeyDown(At(1.0));
        gesture.SpaceDown();
        gesture.HotkeyUp(At(2.0));
        Assert.Equal(HotkeyAction.None, gesture.SpaceDown());
    }

    [Fact]
    public void Second_space_while_locked_held_is_ignored()
    {
        var gesture = Gesture();
        gesture.HotkeyDown(At(1.0));
        gesture.SpaceDown();
        Assert.Equal(HotkeyAction.None, gesture.SpaceDown());
    }

    [Fact]
    public void Other_key_while_holding_cancels_and_release_is_ignored()
    {
        var gesture = Gesture();
        gesture.HotkeyDown(At(1.0));
        Assert.Equal(HotkeyAction.Cancel, gesture.OtherKeyDown());
        Assert.Equal(HotkeyAction.None, gesture.HotkeyUp(At(3.0)));
        Assert.Equal(HotkeyAction.Start, gesture.HotkeyDown(At(4.0)));
    }

    [Fact]
    public void Other_key_while_locked_keeps_recording()
    {
        var gesture = Gesture();
        gesture.HotkeyDown(At(1.0));
        gesture.SpaceDown();
        gesture.HotkeyUp(At(2.0));
        Assert.Equal(HotkeyAction.None, gesture.OtherKeyDown());
        Assert.Equal(HotkeyAction.Stop, gesture.HotkeyDown(At(9.0)));
    }

    [Fact]
    public void Key_repeat_while_held_is_ignored()
    {
        var gesture = Gesture();
        gesture.HotkeyDown(At(0.0));
        Assert.Equal(HotkeyAction.None, gesture.HotkeyDown(At(0.5)));
        Assert.Equal(HotkeyAction.Stop, gesture.HotkeyUp(At(1.0)));
    }

    [Fact]
    public void Stray_release_while_idle_does_nothing()
    {
        Assert.Equal(HotkeyAction.None, Gesture().HotkeyUp(At(3.0)));
    }

    [Fact]
    public void Reset_drops_locked_recording()
    {
        var gesture = Gesture();
        gesture.HotkeyDown(At(0.0));
        gesture.SpaceDown();
        gesture.HotkeyUp(At(1.0));
        gesture.Reset();
        Assert.Equal(HotkeyAction.Start, gesture.HotkeyDown(At(2.0)));
    }
}
