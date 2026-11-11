namespace Murmur.Core;

/// <summary>Everything that can stop a dictation. <see cref="DictationErrors.Message"/> is what the pill shows.</summary>
public enum DictationError
{
    MissingDeepgramKey,
    InvalidDeepgramKey,
    MicrophoneDenied,
    MicrophoneUnavailable,
    ConnectionFailed,
    NothingHeard,
    /// <summary>A bug: the details are in the log.</summary>
    Unexpected,
}

public static class DictationErrors
{
    public static string Message(this DictationError error) => error switch
    {
        DictationError.MissingDeepgramKey => "Add your Deepgram key in Keys",
        DictationError.InvalidDeepgramKey => "Deepgram rejected the key, check Keys",
        DictationError.MicrophoneDenied => "Allow microphone access in Windows Settings",
        DictationError.MicrophoneUnavailable => "Microphone couldn't start",
        DictationError.ConnectionFailed => "Couldn't reach Deepgram",
        DictationError.NothingHeard => "Didn't catch anything",
        DictationError.Unexpected => "Something went wrong, copy diagnostics from the tray",
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };
}
