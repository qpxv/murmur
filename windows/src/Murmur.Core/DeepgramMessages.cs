using System.Text;
using System.Text.Json;

namespace Murmur.Core;

/// <summary>Deepgram live-streaming wire format: the connection URL and the messages both ways.</summary>
public static class DeepgramMessages
{
    public const string Finalize = """{"type":"Finalize"}""";
    public const string CloseStream = """{"type":"CloseStream"}""";

    /// <summary>The listen URL for 16 kHz mono PCM, with every vocabulary term boosted as a key term.</summary>
    public static Uri ListenUri(IEnumerable<string> vocabulary)
    {
        (string Name, string Value)[] fixedItems =
        [
            ("model", AppConstants.Deepgram.Model),
            ("language", AppConstants.Deepgram.Language),
            ("encoding", "linear16"),
            ("sample_rate", AppConstants.Audio.SampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("channels", AppConstants.Audio.ChannelCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("smart_format", "true"),
            ("punctuate", "true"),
        ];
        var keyterms = vocabulary
            .Select(term => term.Trim())
            .Where(term => term.Length > 0)
            .Select(term => (Name: "keyterm", Value: term));
        var query = string.Join(
            '&',
            fixedItems.Concat(keyterms).Select(item => $"{item.Name}={Uri.EscapeDataString(item.Value)}"));
        return new UriBuilder(AppConstants.Deepgram.ListenUrl) { Query = query }.Uri;
    }

    /// <summary>
    /// One transcript message. Only finalized results are returned; everything else
    /// (metadata, speech-started events, interim results, garbage) is <c>null</c>.
    /// </summary>
    public static FinalResult? ParseFinalResult(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String
                || type.GetString() != "Results"
                || !Flag(root, "is_final"))
            {
                return null;
            }
            return new FinalResult(Transcript(root), IsPause: Flag(root, "speech_final"), IsFromFinalize: Flag(root, "from_finalize"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>One transcript message as UTF-8 bytes, as it arrives from the socket.</summary>
    public static FinalResult? ParseFinalResult(ReadOnlySpan<byte> utf8) => ParseFinalResult(Encoding.UTF8.GetString(utf8));

    // deepgram leaves flags out instead of sending false, so missing means false
    private static bool Flag(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string Transcript(JsonElement root)
    {
        if (!root.TryGetProperty("channel", out var channel)
            || channel.ValueKind != JsonValueKind.Object
            || !channel.TryGetProperty("alternatives", out var alternatives)
            || alternatives.ValueKind != JsonValueKind.Array
            || alternatives.GetArrayLength() == 0)
        {
            return "";
        }
        var first = alternatives.EnumerateArray().First();
        return first.ValueKind == JsonValueKind.Object
            && first.TryGetProperty("transcript", out var transcript)
            && transcript.ValueKind == JsonValueKind.String
            ? transcript.GetString() ?? ""
            : "";
    }

    /// <param name="Transcript">The finalized words of this segment.</param>
    /// <param name="IsPause">The speaker paused after this segment: a natural place to close a cleanup chunk.</param>
    /// <param name="IsFromFinalize">The answer to our Finalize message: everything said so far has now arrived.</param>
    public sealed record FinalResult(string Transcript, bool IsPause, bool IsFromFinalize);
}
