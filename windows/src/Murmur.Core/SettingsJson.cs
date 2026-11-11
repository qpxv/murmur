using System.Text.Json;
using System.Text.Json.Serialization;

namespace Murmur.Core;

/// <summary>How <see cref="Settings"/> is written to and read from disk.</summary>
public static class SettingsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Encode(Settings settings) => JsonSerializer.Serialize(settings, Options);

    /// <summary>The saved settings; <c>null</c> when the text isn't settings JSON at all.</summary>
    public static Settings? Decode(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Settings>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
