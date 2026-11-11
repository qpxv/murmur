using System.Runtime.InteropServices;
using Murmur.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Murmur.Services;

/// <summary>What one block of microphone audio sounded like.</summary>
/// <param name="Level">0...1 loudness for the voice bar.</param>
/// <param name="IsClipped">The input hit full scale, so it's distorting (too loud / gain too high).</param>
public readonly record struct AudioBlockMeasure(float Level, bool IsClipped);

/// <summary>
/// Records a microphone through WASAPI (shared mode, in the device's own format) and delivers
/// 16 kHz mono 16-bit PCM plus a measure of every block. Callbacks arrive on the capture thread.
/// </summary>
public sealed class AudioCapture : IDisposable
{
    public delegate void BlockHandler(byte[] pcm, AudioBlockMeasure measure);

    private const string Category = "audio";
    // windows privacy settings ("let desktop apps access your microphone") answer with this
    private const int AccessDenied = unchecked((int)0x80070005);

    private WasapiRecorder? _recorder;
    // the recorder works on this endpoint until it stops, so it lives exactly as long
    private MMDevice? _device;

    /// <param name="deviceId">The microphone the user picked; <c>null</c> follows the Windows default.</param>
    /// <param name="onBlock">Gets every converted block, on the capture thread.</param>
    /// <exception cref="DictationException">The microphone can't record (denied or unavailable).</exception>
    public void Start(string? deviceId, BlockHandler onBlock)
    {
        Stop();
        var device = AudioDevices.InputOrDefault(deviceId)
            ?? throw new DictationException(DictationError.MicrophoneUnavailable);
        WasapiRecorder? recorder = null;
        try
        {
            // the device's own (mix) format, converted here: no reliance on the engine's auto-convert
            recorder = new WasapiRecorderBuilder()
                .WithDevice(device)
                .WithSharedMode()
                .WithEventSync()
                .WithBufferLength(AppConstants.Audio.BufferMilliseconds)
                .Build();
            var converter = new Converter(recorder.WaveFormat);
            recorder.DataAvailable += (buffer, _, _, _) =>
            {
                var samples = converter.Convert(buffer);
                if (samples.Length > 0)
                {
                    var level = AudioLevel.Normalized(AudioLevel.Rms(samples));
                    onBlock(AudioLevel.ToPcm16(samples), new AudioBlockMeasure(level, AudioLevel.IsClipped(samples)));
                }
            };
            recorder.RecordingStopped += (_, args) =>
            {
                if (args.Exception is { } exception)
                {
                    Log.Error(Category, "recording stopped by an error", exception);
                }
            };
            recorder.StartRecording();
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            recorder?.Dispose();
            device.Dispose();
            Log.Error(Category, $"starting the mic failed (0x{exception.HResult:X8})", exception);
            throw new DictationException(
                exception.HResult == AccessDenied ? DictationError.MicrophoneDenied : DictationError.MicrophoneUnavailable);
        }
        Log.Info(Category, $"recording {recorder.WaveFormat.SampleRate} Hz, {recorder.WaveFormat.Channels} ch, {recorder.WaveFormat.Encoding}");
        _recorder = recorder;
        _device = device;
    }

    public void Stop()
    {
        _recorder?.StopRecording();
        _recorder?.Dispose();
        _recorder = null;
        _device?.Dispose();
        _device = null;
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Turns the device's own format (usually 48 kHz stereo float) into 16 kHz mono float samples.
    /// Only ever used from the capture thread, one block at a time.
    /// </summary>
    private sealed class Converter
    {
        private readonly BufferedWaveProvider _input;
        private readonly WdlResamplingSampleProvider _output;
        private float[] _scratch = [];

        public Converter(WaveFormat format)
        {
            _input = new BufferedWaveProvider(format) { DiscardOnBufferOverflow = true, ReadFully = false };
            _output = new WdlResamplingSampleProvider(new MonoSampleProvider(_input.ToSampleProvider()), AppConstants.Audio.SampleRate);
        }

        public ReadOnlySpan<float> Convert(ReadOnlySpan<byte> block)
        {
            _input.AddSamples(block);
            // generous: at least as many output samples as input bytes could ever turn into
            if (_scratch.Length < block.Length)
            {
                _scratch = new float[block.Length];
            }
            var count = _output.Read(_scratch);
            return _scratch.AsSpan(0, count);
        }
    }

    /// <summary>Averages any number of channels into one.</summary>
    private sealed class MonoSampleProvider(ISampleProvider source) : ISampleProvider
    {
        private float[] _frames = [];

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, AppConstants.Audio.ChannelCount);

        public int Read(Span<float> buffer)
        {
            var channels = source.WaveFormat.Channels;
            var needed = buffer.Length * channels;
            if (_frames.Length < needed)
            {
                _frames = new float[needed];
            }
            var read = source.Read(_frames.AsSpan(0, needed)) / channels;
            for (var frame = 0; frame < read; frame++)
            {
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    sum += _frames[(frame * channels) + channel];
                }
                buffer[frame] = sum / channels;
            }
            return read;
        }
    }
}
