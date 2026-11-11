
namespace Murmur.Core.Tests;

public class AudioLevelTests
{
    [Fact]
    public void Rms_of_constant_signal_is_its_magnitude() => Assert.Equal(0.5f, AudioLevel.Rms([0.5f, -0.5f, 0.5f, -0.5f]));

    [Fact]
    public void Rms_of_no_samples_is_zero() => Assert.Equal(0f, AudioLevel.Rms([]));

    [Fact]
    public void Detects_clipping_on_either_side()
    {
        Assert.False(AudioLevel.IsClipped([0.2f, -0.5f, 0.9f], threshold: 0.99f));
        Assert.True(AudioLevel.IsClipped([0.2f, 1.0f, 0.1f], threshold: 0.99f));
        Assert.True(AudioLevel.IsClipped([-0.995f, 0.1f], threshold: 0.99f));
    }

    [Fact]
    public void Silence_is_empty_and_loud_is_full()
    {
        Assert.Equal(0f, AudioLevel.Normalized(0, floor: -60, ceiling: -20));
        Assert.Equal(0f, AudioLevel.Normalized(0.0001f, floor: -60, ceiling: -20));
        Assert.Equal(1f, AudioLevel.Normalized(1, floor: -60, ceiling: -20));
    }

    [Fact]
    public void Midpoint_of_decibel_range_is_half()
    {
        // -40 dBFS is halfway between -60 and -20
        Assert.Equal(0.5f, AudioLevel.Normalized(0.01f, floor: -60, ceiling: -20), 0.0001f);
    }

    [Fact]
    public void Pcm16_is_little_endian_and_clamped()
    {
        var bytes = AudioLevel.ToPcm16([0f, 1f, -1f, 2f]);
        Assert.Equal(8, bytes.Length);
        Assert.Equal(0, BitConverter.ToInt16(bytes, 0));
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(bytes, 2));
        Assert.Equal(-short.MaxValue, BitConverter.ToInt16(bytes, 4));
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(bytes, 6));
        Assert.Equal([0xFF, 0x7F], bytes[2..4]);
    }
}
