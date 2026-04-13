namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal sealed class RegexParseException : ArgumentException
{
    internal RegexParseException(RegexParseError error, int offset, string message)
        : base(message)
    {
        Error = error;
        Offset = offset;
    }

    public RegexParseError Error { get; }

    public int Offset { get; }
}
