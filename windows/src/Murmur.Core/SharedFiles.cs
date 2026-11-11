using System.Text.Json;
using System.Text.Json.Serialization;

namespace Murmur.Core;

/// <summary>
/// What the Mac and Windows apps must agree on, read from the repo's <c>shared/</c> folder
/// (embedded into this assembly at build time).
/// </summary>
public static class SharedFiles
{
    // strict: a renamed or missing field fails the tests instead of silently becoming 0 or null
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static SharedConfig Config { get; } = Load<SharedConfig>("config");

    public static SharedCleanupPrompt CleanupPrompt { get; } = Load<SharedCleanupPrompt>("cleanup-prompt");

    private static T Load<T>(string name)
    {
        using var stream = typeof(SharedFiles).Assembly.GetManifestResourceStream($"Murmur.Shared.{name}.json")
            ?? throw new InvalidOperationException($"shared/{name}.json is not embedded in the build");
        // a build without its own embedded file is broken, not something to recover from
        return JsonSerializer.Deserialize<T>(stream, Options)
            ?? throw new InvalidOperationException($"shared/{name}.json is empty");
    }
}

/// <summary><c>shared/config.json</c>.</summary>
public sealed record SharedConfig(SharedConfig.CleanupConfig Cleanup, SharedConfig.DeepgramConfig Deepgram)
{
    public sealed record CleanupConfig(
        Uri MessagesUrl,
        string Model,
        string ApiVersion,
        string FallbackBeta,
        string Effort,
        int MaxTokens,
        int MinChunkWords,
        int MaxChunkWords,
        int ContextWords,
        double RequestTimeoutSeconds,
        double FinishTimeoutSeconds);

    public sealed record DeepgramConfig(Uri ListenUrl, string Model, string Language, double FinalizeTimeoutSeconds);
}

/// <summary><c>shared/cleanup-prompt.json</c>: the building blocks of the cleanup system prompt.</summary>
public sealed record SharedCleanupPrompt(
    string Intro,
    string FixesHeader,
    SharedCleanupPrompt.FixTexts Fixes,
    SharedCleanupPrompt.StyleTexts Styles,
    string Boundaries,
    string Output)
{
    public sealed record FixTexts(string MisheardWords, string Punctuation, string Stutters);

    public sealed record StyleTexts(string Casual, string Normal, string Formal);
}
