namespace Murmur.Core;

/// <summary>Maps raw microphone loudness to a 0...1 level for the voice bar, and converts samples for Deepgram.</summary>
public static class AudioLevel
{
    // decibels of an amplitude ratio are 20 * log10(ratio)
    private const float AmplitudeDecibelFactor = 20;

    /// <summary>Root mean square of a block of samples in -1...1.</summary>
    public static float Rms(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
        {
            return 0;
        }
        var sumOfSquares = 0f;
        foreach (var sample in samples)
        {
            sumOfSquares += sample * sample;
        }
        return MathF.Sqrt(sumOfSquares / samples.Length);
    }

    /// <summary>Whether any sample hit the ceiling, i.e. the input is too loud and distorts.</summary>
    public static bool IsClipped(ReadOnlySpan<float> samples, float threshold = AppConstants.Audio.ClippingThreshold)
    {
        foreach (var sample in samples)
        {
            if (MathF.Abs(sample) >= threshold)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Loudness on a decibel scale, so quiet speech still visibly moves the bar.</summary>
    public static float Normalized(
        float rms,
        float floor = AppConstants.Audio.SilenceFloorDecibels,
        float ceiling = AppConstants.Audio.FullScaleDecibels)
    {
        if (rms <= 0)
        {
            return 0;
        }
        var decibels = AmplitudeDecibelFactor * MathF.Log10(rms);
        var level = (decibels - floor) / (ceiling - floor);
        return Math.Clamp(level, 0, 1);
    }

    /// <summary>Float samples in -1...1 as little-endian 16-bit PCM (what Deepgram's linear16 expects).</summary>
    public static byte[] ToPcm16(ReadOnlySpan<float> samples)
    {
        var bytes = new byte[samples.Length * sizeof(short)];
        for (var index = 0; index < samples.Length; index++)
        {
            var value = (short)Math.Round(Math.Clamp(samples[index], -1f, 1f) * short.MaxValue);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * sizeof(short)), value);
        }
        return bytes;
    }
}
