namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Specifies CPython-compatible regular-expression compile flags.</summary>
[Flags]
public enum PythonReCompileOptions
{
    /// <summary>Uses the default Unicode, single-line, case-sensitive behavior.</summary>
    None = 0,

    /// <summary>Enables case-insensitive matching.</summary>
    IgnoreCase = 1 << 0,

    /// <summary>Requests locale-dependent matching, which this culture-invariant adapter rejects.</summary>
    Locale = 1 << 1,

    /// <summary>Makes <c>^</c> and <c>$</c> operate at line boundaries.</summary>
    Multiline = 1 << 2,

    /// <summary>Makes <c>.</c> match newline characters.</summary>
    DotAll = 1 << 3,

    /// <summary>Ignores unescaped pattern whitespace and permits comments.</summary>
    Verbose = 1 << 4,

    /// <summary>Restricts word, digit, whitespace, and boundary categories to ASCII behavior.</summary>
    Ascii = 1 << 5,

    /// <summary>Explicitly selects Unicode category behavior.</summary>
    Unicode = 1 << 6,
}
