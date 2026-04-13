namespace Lokad.Utf8Regex.Pcre2;

[Flags]
public enum Pcre2MatchOptions
{
    None = 0,
    Anchored = 1 << 0,
    EndAnchored = 1 << 1,
    NotBol = 1 << 2,
    NotEol = 1 << 3,
    NotEmpty = 1 << 4,
    NotEmptyAtStart = 1 << 5,
}
