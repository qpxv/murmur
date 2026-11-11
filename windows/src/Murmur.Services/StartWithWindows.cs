using Microsoft.Win32;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// Starts Murmur at login through the per-user Run key. Windows holds the state (the user can also
/// switch it off in Task Manager's startup apps), never <see cref="Settings"/>. Off by default.
/// </summary>
public static class StartWithWindows
{
    private const string Category = "startup";
    private const string ValueName = AppConstants.AppName;

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppConstants.Storage.RunKey);
            return key?.GetValue(ValueName) is string command && command.Contains(ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetEnabled(bool isEnabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(AppConstants.Storage.RunKey);
            if (isEnabled)
            {
                // quoted: the download folder path can contain spaces
                key.SetValue(ValueName, $"\"{ExecutablePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            Log.Info(Category, isEnabled ? "start with windows on" : "start with windows off");
        }
        catch (UnauthorizedAccessException exception)
        {
            Log.Error(Category, "changing the run key failed", exception);
        }
        catch (System.Security.SecurityException exception)
        {
            Log.Error(Category, "changing the run key failed", exception);
        }
    }

    private static string ExecutablePath => Environment.ProcessPath ?? "";
}
