namespace Murmur.Core;

/// <summary>The outcome of one dictation: what gets pasted, and what Deepgram originally heard.</summary>
/// <param name="Text">What gets pasted.</param>
/// <param name="RawText">What Deepgram heard.</param>
/// <param name="WasCleaned">Whether Claude cleaned at least part of it.</param>
public sealed record DictationTranscript(string Text, string RawText, bool WasCleaned);
