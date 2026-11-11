using Murmur.Core;

namespace Murmur.Services;

/// <summary>Where the app keeps its files: %LOCALAPPDATA%\Murmur (per user, never roams).</summary>
public static class AppPaths
{
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppConstants.Storage.FolderName);

    public static string Settings => Path.Combine(Folder, AppConstants.Storage.SettingsFile);

    public static string History => Path.Combine(Folder, AppConstants.Storage.HistoryFile);

    public static string Log => Path.Combine(Folder, AppConstants.Storage.LogFile);

    public static string MicRestore => Path.Combine(Folder, AppConstants.Storage.MicRestoreFile);

    /// <summary>Writes a file so a crash mid-write can never leave it half written.</summary>
    public static void WriteAtomically(string path, string contents)
    {
        Directory.CreateDirectory(Folder);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, contents);
        File.Move(temporary, path, overwrite: true);
    }
}
