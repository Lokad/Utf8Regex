namespace Lokad.Utf8Regex.Pcre2;

[Flags]
public enum Pcre2SubstitutionOptions
{
    None = 0,
    Extended = 1 << 0,
    UnsetEmpty = 1 << 1,
    UnknownUnset = 1 << 2,
    SubstituteMatched = 1 << 3,
    SubstituteLiteral = 1 << 4,
    SubstituteOverflowLength = 1 << 5,
    SubstituteReplacementOnly = 1 << 6,
}
