namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Specifies whether a probe may report a match truncated by the subject end.</summary>
public enum Pcre2PartialMode
{
    /// <summary>Reports only complete matches.</summary>
    None = 0,

    /// <summary>Prefers a complete match and reports a partial match only when no complete match is found.</summary>
    Soft = 1,

    /// <summary>Reports the first viable partial match even when later alternatives could complete.</summary>
    Hard = 2,
}

/// <summary>Specifies which byte sequences PCRE2 newline-sensitive constructs recognize.</summary>
public enum Pcre2NewlineConvention
{
    /// <summary>Uses the managed profile default, line feed.</summary>
    Default = 0,

    /// <summary>Recognizes carriage return.</summary>
    Cr,

    /// <summary>Recognizes line feed.</summary>
    Lf,

    /// <summary>Recognizes carriage-return/line-feed pairs.</summary>
    Crlf,

    /// <summary>Recognizes every Unicode vertical whitespace sequence supported by PCRE2.</summary>
    Any,

    /// <summary>Recognizes carriage return, line feed, and carriage-return/line-feed pairs.</summary>
    AnyCrlf,

    /// <summary>Recognizes the NUL scalar as a newline.</summary>
    Nul,
}

/// <summary>Specifies the newline sequences matched by the PCRE2 <c>\R</c> escape.</summary>
public enum Pcre2BsrConvention
{
    /// <summary>Uses the managed profile default, Unicode newline sequences.</summary>
    Default = 0,

    /// <summary>Restricts <c>\R</c> to carriage return, line feed, and their pair.</summary>
    AnyCrlf,

    /// <summary>Makes <c>\R</c> recognize Unicode newline sequences.</summary>
    Unicode,
}

/// <summary>Specifies whether the byte-oriented PCRE2 <c>\C</c> escape is accepted.</summary>
public enum Pcre2BackslashCPolicy
{
    /// <summary>Rejects <c>\C</c> during compilation.</summary>
    Forbid = 0,

    /// <summary>Allows <c>\C</c>, including matches that split UTF-8 scalars and lack a UTF-16 projection.</summary>
    Allow = 1,
}

/// <summary>Specifies compile-time behavior that is independent of the PCRE2 option flags.</summary>
public readonly struct Utf8Pcre2CompileSettings
{
    /// <summary>Gets the newline convention used by anchors, dot, and newline-sensitive constructs.</summary>
    public Pcre2NewlineConvention Newline { get; init; }

    /// <summary>Gets the sequence convention used by <c>\R</c>.</summary>
    public Pcre2BsrConvention Bsr { get; init; }

    /// <summary>Gets whether multiple named groups may use the same name.</summary>
    public bool AllowDuplicateNames { get; init; }

    /// <summary>Gets whether the byte-oriented <c>\C</c> escape is allowed.</summary>
    public Pcre2BackslashCPolicy BackslashC { get; init; }

    /// <summary>Gets whether <c>\K</c> is allowed inside positive assertions.</summary>
    public bool AllowLookaroundBackslashK { get; init; }
}

/// <summary>Specifies optional managed PCRE2 execution budgets; zero disables each corresponding limit.</summary>
public readonly struct Utf8Pcre2ExecutionLimits
{
    /// <summary>Gets the maximum combined candidate and backtracking step count, or zero for no limit.</summary>
    public uint MatchLimit { get; init; }

    /// <summary>Gets the maximum backtracking or recursive execution depth, or zero for no limit.</summary>
    public uint DepthLimit { get; init; }

    /// <summary>Gets the maximum metered workspace size in bytes, or zero for no limit.</summary>
    public ulong HeapLimitInBytes { get; init; }
}
