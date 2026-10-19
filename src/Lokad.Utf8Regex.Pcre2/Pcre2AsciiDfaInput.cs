using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

internal enum Pcre2AsciiDfaStepKind : byte
{
    MatchByte = 0,
    MatchText = 1,
    MatchAsciiClass = 3,
    LoopByte = 4,
    LoopText = 5,
    LoopAsciiClass = 7,
    RequireBeginning = 8,
    RequireEnd = 9,
    RequireBoundary = 10,
    RequireNonBoundary = 11,
    MatchAnyText = 12,
    MatchAnyTextOptional = 13,
    LoopAnyText = 14,
    LoopProgram = 15,
    Accept = 16,
}

internal readonly struct Pcre2AsciiDfaStep
{
    private readonly byte[]? _text;
    private readonly byte[][]? _alternatives;
    private readonly Pcre2AsciiDfaStep[]? _program;
    private readonly AsciiCharClass _charClass;

    private Pcre2AsciiDfaStep(
        Pcre2AsciiDfaStepKind kind,
        byte value,
        byte[]? text,
        byte[][]? alternatives,
        Pcre2AsciiDfaStep[]? program,
        AsciiCharClass charClass,
        int min,
        int max,
        RegexOptions options)
    {
        Kind = kind;
        Value = value;
        _text = text;
        _alternatives = alternatives;
        _program = program;
        _charClass = charClass;
        Min = min;
        Max = max;
        Options = options;
    }

    public Pcre2AsciiDfaStepKind Kind { get; }

    public byte Value { get; }

    public byte[] Text => _text ?? [];

    public byte[][] Alternatives => _alternatives ?? [];

    public Pcre2AsciiDfaStep[] Program => _program ?? [];

    public AsciiCharClass CharClass => _charClass;

    public int Min { get; }

    public int Max { get; }

    public RegexOptions Options { get; }

    internal Utf8ExecutionNodeKind AnchorKind => Value != 0 ? (Utf8ExecutionNodeKind)Value
        : Kind == Pcre2AsciiDfaStepKind.RequireBeginning ? Utf8ExecutionNodeKind.Bol : Utf8ExecutionNodeKind.Eol;

    public static Pcre2AsciiDfaStep MatchByte(byte value) =>
        new(Pcre2AsciiDfaStepKind.MatchByte, value, null, null, null, default, 0, 0, RegexOptions.None);

    public static Pcre2AsciiDfaStep MatchText(byte[] text) =>
        new(Pcre2AsciiDfaStepKind.MatchText, 0, text, null, null, default, 0, 0, RegexOptions.None);

    public static Pcre2AsciiDfaStep MatchAsciiClass(AsciiCharClass asciiCharClass) =>
        new(Pcre2AsciiDfaStepKind.MatchAsciiClass, 0, null, null, null, asciiCharClass, 0, 0, RegexOptions.None);

    public static Pcre2AsciiDfaStep LoopByte(byte value, int min, int max) =>
        new(Pcre2AsciiDfaStepKind.LoopByte, value, null, null, null, default, min, max, RegexOptions.None);

    public static Pcre2AsciiDfaStep LoopText(byte[] text, int min, int max) =>
        new(Pcre2AsciiDfaStepKind.LoopText, 0, text, null, null, default, min, max, RegexOptions.None);

    public static Pcre2AsciiDfaStep LoopAsciiClass(AsciiCharClass asciiCharClass, int min, int max) =>
        new(Pcre2AsciiDfaStepKind.LoopAsciiClass, 0, null, null, null, asciiCharClass, min, max, RegexOptions.None);

    public static Pcre2AsciiDfaStep RequireBeginning(RegexOptions options) =>
        new(Pcre2AsciiDfaStepKind.RequireBeginning, 0, null, null, null, default, 0, 0, options);

    public static Pcre2AsciiDfaStep RequireEnd(RegexOptions options) =>
        new(Pcre2AsciiDfaStepKind.RequireEnd, 0, null, null, null, default, 0, 0, options);

    internal static Pcre2AsciiDfaStep RequireBeginning(Utf8ExecutionNodeKind kind, RegexOptions options) =>
        new(Pcre2AsciiDfaStepKind.RequireBeginning, (byte)kind, null, null, null, default, 0, 0, options);

    internal static Pcre2AsciiDfaStep RequireEnd(Utf8ExecutionNodeKind kind, RegexOptions options) =>
        new(Pcre2AsciiDfaStepKind.RequireEnd, (byte)kind, null, null, null, default, 0, 0, options);

    public static Pcre2AsciiDfaStep RequireBoundary() =>
        CreateMarker(Pcre2AsciiDfaStepKind.RequireBoundary);

    public static Pcre2AsciiDfaStep RequireNonBoundary() =>
        CreateMarker(Pcre2AsciiDfaStepKind.RequireNonBoundary);

    public static Pcre2AsciiDfaStep MatchAnyText(byte[][] alternatives) =>
        new(Pcre2AsciiDfaStepKind.MatchAnyText, 0, null, alternatives, null, default, 0, 0, RegexOptions.None);

    public static Pcre2AsciiDfaStep MatchAnyTextOptional(byte[][] alternatives) =>
        new(Pcre2AsciiDfaStepKind.MatchAnyTextOptional, 0, null, alternatives, null, default, 0, 0, RegexOptions.None);

    public static Pcre2AsciiDfaStep LoopAnyText(byte[][] alternatives, int min, int max) =>
        new(Pcre2AsciiDfaStepKind.LoopAnyText, 0, null, alternatives, null, default, min, max, RegexOptions.None);

    public static Pcre2AsciiDfaStep LoopProgram(Pcre2AsciiDfaStep[] program, int min, int max) =>
        new(Pcre2AsciiDfaStepKind.LoopProgram, 0, null, null, program, default, min, max, RegexOptions.None);

    public static Pcre2AsciiDfaStep Accept() =>
        CreateMarker(Pcre2AsciiDfaStepKind.Accept);

    private static Pcre2AsciiDfaStep CreateMarker(Pcre2AsciiDfaStepKind kind) =>
        new(kind, 0, null, null, null, default, 0, 0, RegexOptions.None);
}

internal readonly struct Pcre2AsciiDfaInput(Pcre2AsciiDfaStep[]? steps)
{
    public Pcre2AsciiDfaStep[] Steps { get; } = steps is { Length: > 0 } ? steps : [];
    public bool HasValue => Steps is { Length: > 0 };
}
