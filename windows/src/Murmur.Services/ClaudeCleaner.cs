using System.Diagnostics;
using System.Net.Http;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// Sends one dictated chunk to Claude for cleanup (official SDK, same model, effort and
/// server-side refusal fallback as the Mac). Never throws: any failure returns <c>null</c> and the
/// caller keeps the raw text, so a cleanup problem can never lose speech.
/// </summary>
public sealed class ClaudeCleaner : IDisposable
{
    private const string Category = "cleanup";

    private readonly AnthropicClient _client;
    private readonly string _system;

    public ClaudeCleaner(string apiKey, CleanupOptions options)
    {
        _client = new AnthropicClient { ApiKey = apiKey, Timeout = AppConstants.Cleanup.RequestTimeout };
        _system = CleanupPrompt.System(options);
    }

    public async Task<string?> CleanAsync(string userMessage, CancellationToken cancellation)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await _client.Beta.Messages.Create(
                new MessageCreateParams
                {
                    Model = AppConstants.Cleanup.Model,
                    MaxTokens = AppConstants.Cleanup.MaxTokens,
                    System = _system,
                    Messages = [new BetaUserMessageParam { Content = userMessage }],
                    OutputConfig = new BetaOutputConfig { Effort = AppConstants.Cleanup.Effort },
                    // a refused chunk is rerouted inside the same call instead of coming back empty
                    Betas = [AppConstants.Cleanup.FallbackBeta],
                    Fallbacks = new Default(),
                },
                cancellation);
            // the wire value ("end_turn"), not the sdk's enum name
            var stopReason = response.StopReason?.Raw();
            // stop reason and timing only, never text
            Log.Info(Category, $"cleanup {stopReason} in {Milliseconds(started)} ms");
            var texts = response.Content.Select(block => block.Value).OfType<BetaTextBlock>().Select(block => block.Text);
            return CleanupResponse.CleanedText(stopReason, texts);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (AnthropicApiException exception)
        {
            Log.Error(Category, $"cleanup failed after {Milliseconds(started)} ms", exception);
            return null;
        }
        catch (HttpRequestException exception)
        {
            Log.Error(Category, $"cleanup unreachable after {Milliseconds(started)} ms", exception);
            return null;
        }
    }

    public void Dispose() => _client.Dispose();

    private static long Milliseconds(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
