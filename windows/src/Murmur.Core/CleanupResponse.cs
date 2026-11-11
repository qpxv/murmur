namespace Murmur.Core;

/// <summary>
/// What a Claude cleanup response means for the dictation. The SDK does the wire format; this is
/// the rule on top, the same as the Mac's <c>AnthropicMessages.cleanedText</c>.
/// </summary>
public static class CleanupResponse
{
    public const string EndTurn = "end_turn";

    /// <summary>
    /// The cleaned text, or <c>null</c> when the model didn't finish normally (refusal, cut off)
    /// or returned nothing, in which case the caller keeps the raw text.
    /// </summary>
    public static string? CleanedText(string? stopReason, IEnumerable<string> textBlocks)
    {
        if (stopReason != EndTurn)
        {
            return null;
        }
        var text = string.Concat(textBlocks).Trim();
        return text.Length == 0 ? null : text;
    }
}
