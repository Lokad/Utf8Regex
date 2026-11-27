namespace Lokad.Utf8Regex.Posix;

/// <summary>Reports a pattern that uses syntax outside the supported POSIX ERE subset.</summary>
public sealed class PosixUnsupportedSyntaxException : ArgumentException
{
    internal PosixUnsupportedSyntaxException(string message) : base(message, "pattern") { }
}
