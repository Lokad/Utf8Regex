namespace Lokad.Utf8Regex.PythonRe;

public sealed class PythonRePatternException : Exception
{
    public PythonRePatternException(string message, int position = -1)
        : base(message)
    {
        Position = position;
    }

    public int Position { get; }
}
