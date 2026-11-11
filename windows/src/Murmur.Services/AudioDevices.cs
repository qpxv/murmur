using NAudio.CoreAudioApi;

namespace Murmur.Services;

/// <summary>A microphone as the settings show it.</summary>
public sealed record InputDevice(string Id, string Name);

/// <summary>The microphones Windows knows about (WASAPI capture endpoints).</summary>
public static class AudioDevices
{
    private const string Category = "audio";

    /// <summary>Every active microphone; read fresh each time, so a newly plugged-in one shows up.</summary>
    public static IReadOnlyList<InputDevice> Inputs()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return [.. enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .Select(device =>
                {
                    using (device)
                    {
                        return new InputDevice(device.ID, device.FriendlyName);
                    }
                })];
        }
        catch (System.Runtime.InteropServices.COMException exception)
        {
            Log.Error(Category, "listing microphones failed", exception);
            return [];
        }
    }

    /// <summary>The chosen microphone, or the Windows default when it's unset or gone; <c>null</c> when there is none.</summary>
    public static MMDevice? InputOrDefault(string? deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (deviceId is not null)
        {
            try
            {
                var chosen = enumerator.GetDevice(deviceId);
                if (chosen.State == DeviceState.Active)
                {
                    return chosen;
                }
                chosen.Dispose();
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                Log.Error(Category, "the chosen mic is gone, using the default", exception);
            }
        }
        return enumerator.TryGetDefaultAudioEndpoint(DataFlow.Capture, Role.Console, out var device) ? device : null;
    }

    /// <summary>The default microphone's name, for diagnostics.</summary>
    public static string DefaultInputName()
    {
        using var device = InputOrDefault(null);
        return device?.FriendlyName ?? "none";
    }
}
