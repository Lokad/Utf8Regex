namespace Lokad.Utf8Regex.Pcre2;

[Flags]
public enum Pcre2CompileOptions
{
    None = 0,
    Caseless = 1 << 0,
    Multiline = 1 << 1,
    DotAll = 1 << 2,
    Extended = 1 << 3,
    ExtendedMore = 1 << 4,
    Anchored = 1 << 5,
    EndAnchored = 1 << 6,
    DollarEndOnly = 1 << 7,
    Ungreedy = 1 << 8,
    NoAutoCapture = 1 << 9,
    Ucp = 1 << 10,
    FirstLine = 1 << 11,
}
