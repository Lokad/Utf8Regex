namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Specifies managed PCRE2-compatible compile options.</summary>
[Flags]
public enum Pcre2CompileOptions
{
    /// <summary>Uses default case-sensitive, single-line PCRE2 behavior.</summary>
    None = 0,

    /// <summary>Enables caseless matching.</summary>
    Caseless = 1 << 0,

    /// <summary>Makes <c>^</c> and <c>$</c> operate at recognized newline boundaries.</summary>
    Multiline = 1 << 1,

    /// <summary>Makes <c>.</c> match recognized newline sequences.</summary>
    DotAll = 1 << 2,

    /// <summary>Ignores unescaped pattern whitespace and enables <c>#</c> comments outside character classes.</summary>
    Extended = 1 << 3,

    /// <summary>Enables PCRE2's additional extended-mode whitespace rules inside character classes.</summary>
    ExtendedMore = 1 << 4,

    /// <summary>Restricts every match attempt to the supplied start offset.</summary>
    Anchored = 1 << 5,

    /// <summary>Requires every successful match to end at the end of the subject.</summary>
    EndAnchored = 1 << 6,

    /// <summary>Makes <c>$</c> match only the subject end, not the position before a final newline.</summary>
    DollarEndOnly = 1 << 7,

    /// <summary>Inverts the default greediness of quantifiers.</summary>
    Ungreedy = 1 << 8,

    /// <summary>Treats unnamed parentheses as noncapturing groups.</summary>
    NoAutoCapture = 1 << 9,

    /// <summary>Makes character-type escapes and word boundaries use Unicode properties.</summary>
    Ucp = 1 << 10,

    /// <summary>Restricts an unanchored match to the subject's first line.</summary>
    FirstLine = 1 << 11,
}
