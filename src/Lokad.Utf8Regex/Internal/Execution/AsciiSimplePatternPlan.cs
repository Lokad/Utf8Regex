using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.FrontEnd;
namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiSimplePatternPlan
{
    public AsciiSimplePatternPlan(
        AsciiSimplePatternToken[][] branches, int searchLiteralOffset, byte[][] searchLiterals, AsciiFixedLiteralCheck[] fixedLiteralChecks, bool isStartAnchored, bool isEndAnchored, bool allowsTrailingNewlineBeforeEnd, bool ignoreCase, bool isUtf8ByteSafe, AsciiSimplePatternRunPlan runPlan, bool isAbsoluteStartAnchored, bool isAbsoluteEndAnchored)
    {
        Branches = branches;
        MinLength = 0;
        MaxLength = 0;
        if (branches.Length > 0)
        {
            MinLength = int.MaxValue;
            foreach (var branch in branches)
            {
                if (branch.Length < MinLength)
                {
                    MinLength = branch.Length;
                }

                if (branch.Length > MaxLength)
                {
                    MaxLength = branch.Length;
                }
            }
        }

        SearchLiteralOffset = searchLiteralOffset;
        SearchLiterals = searchLiterals;
        FixedLiteralChecks = fixedLiteralChecks;
        IsStartAnchored = isStartAnchored;
        IsAbsoluteStartAnchored = isAbsoluteStartAnchored;
        IsAbsoluteEndAnchored = isAbsoluteEndAnchored;
        IsEndAnchored = isEndAnchored;
        AllowsTrailingNewlineBeforeEnd = allowsTrailingNewlineBeforeEnd;
        IgnoreCase = ignoreCase;
        IsUtf8ByteSafe = isUtf8ByteSafe;
        RunPlan = runPlan;
    }

    public AsciiSimplePatternToken[][] Branches { get; }

    public int MinLength { get; }

    public int MaxLength { get; }

    public bool IsFixedLength => MinLength == MaxLength;

    public int SearchLiteralOffset { get; }

    public byte[][] SearchLiterals { get; }

    public AsciiFixedLiteralCheck[] FixedLiteralChecks { get; }

    public bool IsStartAnchored { get; }

    /// <summary>Gets whether the start anchor is absolute (`^`/`\A` outside multiline), pinning matches at offset 0. Line anchors (`(?m:^)`) legitimately match after `\n`.</summary>
    public bool IsAbsoluteStartAnchored { get; }

    /// <summary>Gets whether the end anchor is absolute (end of input modulo a single trailing line feed), as opposed to a line anchor matching before any line feed.</summary>
    public bool IsAbsoluteEndAnchored { get; }

    public bool IsEndAnchored { get; }

    public bool AllowsTrailingNewlineBeforeEnd { get; }

    public bool IgnoreCase { get; }

    public bool IsUtf8ByteSafe { get; }

    public AsciiSimplePatternRunPlan RunPlan { get; }

}

internal readonly struct AsciiSimplePatternRunPlan
{
    public AsciiSimplePatternRunPlan(AsciiCharClass charClass, int minLength, int maxLength)
    {
        CharClass = charClass;
        PredicateKind = charClass.TryGetKnownPredicateKind(out var predicateKind)
            ? predicateKind
            : AsciiCharClassPredicateKind.None;
        MinLength = minLength;
        MaxLength = maxLength;
        Search = Utf8PreparedByteSearchFactory.CreateFromPositiveClass(charClass);
    }

    public AsciiCharClass CharClass { get; }

    public AsciiCharClassPredicateKind PredicateKind { get; }

    public int MinLength { get; }

    public int MaxLength { get; }

    public PreparedByteSearch Search { get; }

    public bool HasValue => !CharClass.IsEmpty && MinLength > 0 && MaxLength >= MinLength;
}

internal readonly struct AsciiFixedLiteralCheck
{
    public AsciiFixedLiteralCheck(int offset, byte[] literal)
    {
        Offset = offset;
        Literal = literal;
    }

    public int Offset { get; }

    public byte[] Literal { get; }
}
