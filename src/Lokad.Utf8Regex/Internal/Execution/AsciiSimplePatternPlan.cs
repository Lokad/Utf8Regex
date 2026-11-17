using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.FrontEnd;
namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiSimplePatternPlan
{
    public AsciiSimplePatternPlan(
        AsciiSimplePatternToken[][] branches, int searchLiteralOffset, byte[][] searchLiterals, AsciiFixedLiteralCheck[] fixedLiteralChecks, bool isStartAnchored, bool isEndAnchored, bool allowsTrailingNewlineBeforeEnd, bool ignoreCase, bool isUtf8ByteSafe, AsciiSimplePatternRunPlan runPlan, AsciiSimplePatternAnchoredHeadTailRunPlan anchoredHeadTailRunPlan, AsciiSimplePatternAnchoredValidatorPlan anchoredValidatorPlan, bool isAbsoluteStartAnchored, bool isAbsoluteEndAnchored)
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
        AnchoredHeadTailRunPlan = anchoredHeadTailRunPlan;
        AnchoredValidatorPlan = anchoredValidatorPlan;
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

    public AsciiSimplePatternAnchoredHeadTailRunPlan AnchoredHeadTailRunPlan { get; }

    public AsciiSimplePatternAnchoredValidatorPlan AnchoredValidatorPlan { get; }

    public Utf8CompiledPatternCategory CompiledPatternCategory => AnchoredValidatorPlan.HasValue ? Utf8CompiledPatternCategory.AnchoredWhole : Utf8CompiledPatternCategory.None;

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

internal readonly struct AsciiSimplePatternAnchoredHeadTailRunPlan
{
    public AsciiSimplePatternAnchoredHeadTailRunPlan(
        AsciiCharClass headCharClass,
        AsciiCharClass tailCharClass,
        int tailMinLength)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            HeadCharClass = headCharClass;
            TailCharClass = tailCharClass;
            TailMinLength = tailMinLength;
            TailSearch = !tailCharClass.Negated && !tailCharClass.IsEmpty
                ? Utf8PreparedByteSearchFactory.CreateSet(tailCharClass) : null;
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    public AsciiCharClass HeadCharClass { get; }

    public AsciiCharClass TailCharClass { get; }

    public int TailMinLength { get; }

    public PreparedByteSet? TailSearch { get; }

    public bool HasValue => !HeadCharClass.IsEmpty && !TailCharClass.IsEmpty && TailMinLength >= 0;

    public bool IsMatch(ReadOnlySpan<byte> input)
    {
        if (!HasValue || input.Length < 1 + TailMinLength)
        {
            return false;
        }

        if (!HeadCharClass.Contains(input[0]))
        {
            return false;
        }

        if (!TailCharClass.Negated && TailSearch is not null)
        {
            return TailSearch.IndexOfAnyExcept(input[1..]) < 0;
        }

        for (var i = 1; i < input.Length; i++)
        {
            if (!TailCharClass.Contains(input[i]))
            {
                return false;
            }
        }

        return true;
    }
}

internal readonly struct AsciiSimplePatternAnchoredValidatorPlan
{
    public AsciiSimplePatternAnchoredValidatorPlan(AsciiSimplePatternAnchoredValidatorSegment[] segments, bool ignoreCase)
    {
        Segments = segments;
        IgnoreCase = ignoreCase;
    }

    public AsciiSimplePatternAnchoredValidatorSegment[] Segments { get; }

    public bool IgnoreCase { get; }

    public bool HasValue => Segments is { Length: > 0 };
}

internal readonly struct AsciiSimplePatternAnchoredValidatorSegment
{
    private readonly byte[]? _literal;
    private readonly AsciiCharClass _charClass;

    public AsciiSimplePatternAnchoredValidatorSegment(byte[] literal)
    {
        _literal = literal;
        _charClass = default;
        MinLength = literal.Length;
        MaxLength = literal.Length;
    }

    public AsciiSimplePatternAnchoredValidatorSegment(AsciiCharClass charClass, int minLength, int maxLength)
    {
        _literal = [];
        _charClass = charClass;
        PredicateKind = charClass.TryGetKnownPredicateKind(out var predicateKind)
            ? predicateKind
            : AsciiCharClassPredicateKind.None;
        MinLength = minLength;
        MaxLength = maxLength;
    }

    public byte[] Literal => _literal ?? [];

    public AsciiCharClass CharClass => _charClass;

    public AsciiCharClassPredicateKind PredicateKind { get; }

    public int MinLength { get; }

    public int MaxLength { get; }

    public bool IsLiteral => _literal is { Length: > 0 };
}
