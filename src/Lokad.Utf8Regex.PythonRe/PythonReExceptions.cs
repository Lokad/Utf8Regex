namespace Lokad.Utf8Regex.PythonRe;

public sealed class PythonRePatternException : Exception
{
    /// <summary>Creates a Python pattern error whose source position is unknown.</summary>
    public PythonRePatternException(string message)
        : this(message, -1)
    {
    }

    /// <summary>Creates a Python pattern error at the specified zero-based pattern position.</summary>
    public PythonRePatternException(string message, int position)
        : base(message)
    {
        Position = position;
    }

    public int Position { get; }
}
