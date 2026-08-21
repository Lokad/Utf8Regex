namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Reports a CPython-compatible pattern syntax or compile-option error.</summary>
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

    /// <summary>Gets the zero-based UTF-16 position in the pattern, or <c>-1</c> when no position is available.</summary>
    public int Position { get; }
}
