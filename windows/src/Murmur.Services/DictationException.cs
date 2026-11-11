using Murmur.Core;

namespace Murmur.Services;

/// <summary>A service failure that ends a dictation; the controller shows <see cref="Error"/> in the pill.</summary>
public sealed class DictationException : Exception
{
    public DictationException(DictationError error)
        : base(error.ToString())
    {
        Error = error;
    }

    public DictationException()
        : this(DictationError.ConnectionFailed)
    {
    }

    public DictationException(string message)
        : base(message)
    {
        Error = DictationError.ConnectionFailed;
    }

    public DictationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = DictationError.ConnectionFailed;
    }

    public DictationError Error { get; }
}
