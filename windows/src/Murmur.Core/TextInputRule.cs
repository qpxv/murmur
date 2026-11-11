namespace Murmur.Core;

/// <summary>
/// Decides from UI Automation properties whether the focused element accepts typed text,
/// i.e. whether to paste into it or fall back to the clipboard.
/// </summary>
public static class TextInputRule
{
    /// <summary>The UI Automation control types the rule cares about.</summary>
    public enum ControlKind
    {
        Edit,
        Document,
        Other,
    }

    /// <param name="kind">The focused element's control type; <c>null</c> when nothing is focused.</param>
    /// <param name="isValueReadOnly">
    /// The element's value pattern <c>IsReadOnly</c>; <c>null</c> when it has no value pattern.
    /// </param>
    /// <param name="hasTextPattern">
    /// Whether it exposes the text pattern (a caret and text), like terminals and rich editors do.
    /// </param>
    public static bool IsTextInput(ControlKind? kind, bool? isValueReadOnly, bool hasTextPattern)
    {
        // a read-only value is a web page or a label, whatever its control type says
        if (kind is null || isValueReadOnly == true)
        {
            return false;
        }
        return kind is ControlKind.Edit or ControlKind.Document || isValueReadOnly == false || hasTextPattern;
    }

    /// <summary>
    /// A paste landed when the field's length changed. Fields that don't report a length
    /// count as landed, since there is nothing to check against.
    /// </summary>
    public static bool DidPasteLand(int? lengthBefore, int? lengthAfter) =>
        lengthBefore is not { } before || lengthAfter is not { } after || before != after;
}
