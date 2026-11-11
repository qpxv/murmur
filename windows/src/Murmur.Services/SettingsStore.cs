using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// Holds the one <see cref="Settings"/> value and persists it to settings.json. Everything that
/// changes a setting goes through <see cref="Update"/>, so <see cref="Changed"/> always fires.
/// </summary>
public sealed class SettingsStore
{
    private const string Category = "settings";

    public SettingsStore()
    {
        Current = Load();
    }

    public Settings Current { get; private set; }

    /// <summary>Raised on the thread that called <see cref="Update"/> (the UI thread).</summary>
    public event EventHandler? Changed;

    public void Update(Func<Settings, Settings> change)
    {
        var updated = change(Current);
        if (updated == Current)
        {
            return;
        }
        Current = updated;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static Settings Load()
    {
        try
        {
            if (!File.Exists(AppPaths.Settings))
            {
                return Settings.Defaults;
            }
            var settings = SettingsJson.Decode(File.ReadAllText(AppPaths.Settings));
            if (settings is null)
            {
                Log.Error(Category, "settings.json is unreadable, using defaults");
            }
            return settings ?? Settings.Defaults;
        }
        catch (IOException exception)
        {
            Log.Error(Category, "reading settings failed, using defaults", exception);
            return Settings.Defaults;
        }
    }

    private void Save()
    {
        try
        {
            AppPaths.WriteAtomically(AppPaths.Settings, SettingsJson.Encode(Current));
        }
        catch (IOException exception)
        {
            Log.Error(Category, "saving settings failed", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            Log.Error(Category, "saving settings failed", exception);
        }
    }
}
