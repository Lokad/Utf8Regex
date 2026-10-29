namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Identifies the input responsible for a PythonRe pattern error.</summary>
public enum PythonReErrorSource
{
    /// <summary>The regex pattern, including inline options.</summary>
    Pattern,
    /// <summary>A replacement template.</summary>
    Replacement,
    /// <summary>The supplied compile options, independently of pattern text.</summary>
    CompileOptions,
}

/// <summary>Classifies a PythonRe pattern error without inspecting its message.</summary>
public enum PythonReErrorKind
{
    /// <summary>Invalid pattern/template syntax, references or lookbehind width.</summary>
    Syntax,
    /// <summary>Incompatible or disallowed option combinations.</summary>
    InvalidOptions,
    /// <summary>A feature deliberately excluded from this profile.</summary>
    UnsupportedFeature,
}

/// <summary>Reports a PythonRe pattern, replacement-template or compile-option error.</summary>
public sealed class PythonRePatternException : Exception
{
    /// <summary>Creates a Python pattern error whose source position is unknown.</summary>
    public PythonRePatternException(string message)
        : this(message, -1)
    {
    }

    /// <summary>Creates a Python pattern error at the specified zero-based pattern position.</summary>
    public PythonRePatternException(string message, int position)
        : this(message, position, PythonReErrorSource.Pattern)
    {
    }

    internal PythonRePatternException(string message, int position, PythonReErrorSource errorSource,
        PythonReErrorKind errorKind = PythonReErrorKind.Syntax)
        : base(message)
    {
        Position = position;
        ErrorSource = errorSource;
        ErrorKind = errorKind;
    }

    /// <summary>Gets the zero-based UTF-16 position in the identified source, or <c>-1</c> when unavailable.</summary>
    public int Position { get; }

    /// <summary>Gets whether pattern text, replacement text or supplied compile options caused this error.</summary>
    public PythonReErrorSource ErrorSource { get; }

    /// <summary>Gets the stable classification independently of diagnostic prose.</summary>
    public PythonReErrorKind ErrorKind { get; }
}
