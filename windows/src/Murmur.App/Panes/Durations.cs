namespace Murmur.App.Panes;

/// <summary>Short durations for the dashboard: "1h 23m", "4m 10s", "12s".</summary>
public static class Durations
{
    /// <summary>"1h 23m", not "1h 23m 4s".</summary>
    private const int UnitCount = 2;

    public static string Format(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, Math.Round(seconds)));
        (int Value, string Unit)[] parts = [((int)time.TotalHours, "h"), (time.Minutes, "m"), (time.Seconds, "s")];
        var shown = parts.SkipWhile(part => part.Value == 0).Take(UnitCount).Where(part => part.Value > 0).ToList();
        return shown.Count == 0 ? "0s" : string.Join(' ', shown.Select(part => $"{part.Value}{part.Unit}"));
    }
}
