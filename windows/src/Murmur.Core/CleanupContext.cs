namespace Murmur.Core;

/// <summary>What Claude knows about a whole dictation, sent along with each of its chunks.</summary>
/// <param name="Vocabulary">Terms that must be spelled exactly like this.</param>
/// <param name="AppName">The app the user dictates into.</param>
/// <param name="Screen">The front window's text from <see cref="ScreenContext"/>, empty when not read.</param>
public sealed record CleanupContext(IReadOnlyList<string> Vocabulary, string? AppName, string Screen);
