namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Specifies PCRE2-compatible replacement-template and output behavior.</summary>
[Flags]
public enum Pcre2SubstitutionOptions
{
    /// <summary>Uses ordinary PCRE2 replacement-template behavior.</summary>
    None = 0,

    /// <summary>Enables extended replacement escapes and conditional replacement syntax.</summary>
    Extended = 1 << 0,

    /// <summary>Expands an existing but unmatched capture as an empty value.</summary>
    UnsetEmpty = 1 << 1,

    /// <summary>Treats an unknown capture reference as an unset capture.</summary>
    UnknownUnset = 1 << 2,

    /// <summary>Accepts PCRE2's pre-matched substitution flag; this managed profile still performs its own matching.</summary>
    SubstituteMatched = 1 << 3,

    /// <summary>Treats the complete replacement pattern as literal text.</summary>
    SubstituteLiteral = 1 << 4,

    /// <summary>Reports the required output length through <c>bytesWritten</c> when the destination is too small.</summary>
    SubstituteOverflowLength = 1 << 5,

    /// <summary>Returns only expanded replacement fragments, omitting unmatched subject text.</summary>
    SubstituteReplacementOnly = 1 << 6,
}
