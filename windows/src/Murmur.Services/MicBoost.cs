using System.Runtime.InteropServices;
using System.Text.Json;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// Turns the microphone up to full volume while recording, so quiet speech and whispers come
/// through, and puts it back afterwards. The original volume is written to disk before boosting,
/// so it is also restored on the next launch after a crash.
/// </summary>
public sealed class MicBoost
{
    private const string Category = "mic-boost";

    private Snapshot? _active;

    /// <param name="DeviceId">The endpoint the volume belongs to.</param>
    /// <param name="Volume">Its master volume (0...1) before the boost.</param>
    private sealed record Snapshot(string DeviceId, float Volume);

    public void Boost(string? deviceId)
    {
        if (_active is not null)
        {
            return;
        }
        try
        {
            using var device = AudioDevices.InputOrDefault(deviceId);
            if (device is null)
            {
                return;
            }
            var snapshot = new Snapshot(device.ID, device.AudioEndpointVolume.MasterVolumeLevelScalar);
            Persist(snapshot);
            _active = snapshot;
            device.AudioEndpointVolume.MasterVolumeLevelScalar = AppConstants.Audio.BoostedVolume;
        }
        catch (COMException exception)
        {
            Log.Error(Category, "boosting the mic failed", exception);
        }
    }

    public void Restore()
    {
        if (_active is not { } snapshot)
        {
            return;
        }
        _active = null;
        Apply(snapshot);
        File.Delete(AppPaths.MicRestore);
    }

    /// <summary>Restores a boost that the last run never undid (crash, killed process).</summary>
    public static void RestorePendingFromLastRun()
    {
        try
        {
            if (!File.Exists(AppPaths.MicRestore))
            {
                return;
            }
            var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(AppPaths.MicRestore));
            File.Delete(AppPaths.MicRestore);
            if (snapshot is not null)
            {
                Apply(snapshot);
                Log.Info(Category, "restored mic volume left boosted by the previous run");
            }
        }
        catch (JsonException exception)
        {
            Log.Error(Category, "the saved mic volume is unreadable", exception);
            File.Delete(AppPaths.MicRestore);
        }
        catch (IOException exception)
        {
            Log.Error(Category, "reading the saved mic volume failed", exception);
        }
    }

    private static void Apply(Snapshot snapshot)
    {
        try
        {
            using var device = AudioDevices.InputOrDefault(snapshot.DeviceId);
            // only the device that was boosted: never "restore" some other default mic
            if (device is not null && device.ID == snapshot.DeviceId)
            {
                device.AudioEndpointVolume.MasterVolumeLevelScalar = snapshot.Volume;
            }
        }
        catch (COMException exception)
        {
            Log.Error(Category, "restoring the mic volume failed", exception);
        }
    }

    private static void Persist(Snapshot snapshot)
    {
        try
        {
            AppPaths.WriteAtomically(AppPaths.MicRestore, JsonSerializer.Serialize(snapshot));
        }
        catch (IOException exception)
        {
            Log.Error(Category, "saving the mic volume failed", exception);
        }
    }
}
