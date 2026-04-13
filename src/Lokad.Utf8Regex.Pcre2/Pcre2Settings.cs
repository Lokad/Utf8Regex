namespace Lokad.Utf8Regex.Pcre2;

public enum Pcre2PartialMode
{
    None = 0,
    Soft = 1,
    Hard = 2,
}

public enum Pcre2NewlineConvention
{
    Default = 0,
    Cr,
    Lf,
    Crlf,
    Any,
    AnyCrlf,
    Nul,
}

public enum Pcre2BsrConvention
{
    Default = 0,
    AnyCrlf,
    Unicode,
}

public enum Pcre2BackslashCPolicy
{
    Forbid = 0,
    Allow = 1,
}

public readonly struct Utf8Pcre2CompileSettings
{
    public Pcre2NewlineConvention Newline { get; init; }

    public Pcre2BsrConvention Bsr { get; init; }

    public bool AllowDuplicateNames { get; init; }

    public Pcre2BackslashCPolicy BackslashC { get; init; }

    public bool AllowLookaroundBackslashK { get; init; }
}

public readonly struct Utf8Pcre2ExecutionLimits
{
    public uint MatchLimit { get; init; }

    public uint DepthLimit { get; init; }

    public ulong HeapLimitInBytes { get; init; }
}
