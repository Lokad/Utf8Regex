namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Specifies PCRE2-compatible options for an individual match operation.</summary>
[Flags]
public enum Pcre2MatchOptions
{
    /// <summary>Uses the compiled expression's default matching behavior.</summary>
    None = 0,

    /// <summary>Restricts the match attempt to the supplied start offset.</summary>
    Anchored = 1 << 0,

    /// <summary>Requires the successful match to end at the end of the subject.</summary>
    EndAnchored = 1 << 1,

    /// <summary>Treats the start of the subject as not being a beginning-of-line position.</summary>
    NotBol = 1 << 2,

    /// <summary>Treats the end of the subject as not being an end-of-line position.</summary>
    NotEol = 1 << 3,

    /// <summary>Rejects every empty match.</summary>
    NotEmpty = 1 << 4,

    /// <summary>Rejects an empty match only at the supplied start offset.</summary>
    NotEmptyAtStart = 1 << 5,
}
