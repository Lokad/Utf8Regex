using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

// Only the flat operations emitted by the anchored boolean analyzer belong here.
internal enum Pcre2AsciiDfaStepKind : byte
{
    MatchAsciiClass,
    LoopAsciiClass,
    RequireBeginning,
    RequireEnd,
    Accept,
}

internal readonly struct Pcre2AsciiDfaStep
{
    private Pcre2AsciiDfaStep(Pcre2AsciiDfaStepKind kind, AsciiCharClass charClass, int min, int max)
    { Kind = kind; CharClass = charClass; Min = min; Max = max; }

    public Pcre2AsciiDfaStepKind Kind { get; }
    public AsciiCharClass CharClass { get; }
    public int Min { get; }
    public int Max { get; }
    public static Pcre2AsciiDfaStep MatchAsciiClass(AsciiCharClass value) => new(Pcre2AsciiDfaStepKind.MatchAsciiClass, value, 0, 0);
    public static Pcre2AsciiDfaStep LoopAsciiClass(AsciiCharClass value, int min, int max) => new(Pcre2AsciiDfaStepKind.LoopAsciiClass, value, min, max);
    public static Pcre2AsciiDfaStep RequireBeginning() => new(Pcre2AsciiDfaStepKind.RequireBeginning, default, 0, 0);
    public static Pcre2AsciiDfaStep RequireEnd() => new(Pcre2AsciiDfaStepKind.RequireEnd, default, 0, 0);
    public static Pcre2AsciiDfaStep Accept() => new(Pcre2AsciiDfaStepKind.Accept, default, 0, 0);
}

internal readonly struct Pcre2AsciiDfaInput(Pcre2AsciiDfaStep[]? steps)
{
    public Pcre2AsciiDfaStep[] Steps { get; } = steps is { Length: > 0 } ? steps : [];
    public bool HasValue => Steps is { Length: > 0 };
}
