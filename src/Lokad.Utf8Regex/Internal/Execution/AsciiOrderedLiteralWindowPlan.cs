using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiOrderedWindowSeparatorPlan
{
    internal AsciiOrderedWindowSeparatorPlan(string runtimeSet, int minimum, bool lazy)
    {
        HasValue = runtimeSet.Length > 0;
        Minimum = minimum;
        IsLazy = lazy;
        CharClass = AsciiStructuralIdentifierFamilyPlan.TryCreateAsciiCharClass(runtimeSet, out var projected)
            ? projected : default;
    }

    internal bool HasValue { get; }
    internal int Minimum { get; }
    internal bool IsLazy { get; }
    internal AsciiCharClass CharClass { get; }
}

internal readonly struct AsciiOrderedLiteralWindowPlan
{
    private readonly byte[][]? _leadingLiteralsUtf8;
    private readonly byte[][]? _trailingLiteralsUtf8;

    public AsciiOrderedLiteralWindowPlan(
        byte[] leadingLiteralUtf8,
        byte[][]? leadingLiteralsUtf8,
        byte[] trailingLiteralUtf8,
        byte[][]? trailingLiteralsUtf8,
        int maxGap,
        int minGap,
        bool gapSameLine,
        AsciiOrderedWindowSeparatorPlan separator,
        AsciiOrderedWindowSeparatorPlan[]? branchSeparators,
        bool yieldLeadingLiteralOnly,
        Utf8BoundaryRequirement leadingLiteralLeadingBoundary,
        Utf8BoundaryRequirement leadingLiteralTrailingBoundary,
        Utf8BoundaryRequirement trailingLiteralLeadingBoundary,
        Utf8BoundaryRequirement trailingLiteralTrailingBoundary)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            LeadingLiteralUtf8 = leadingLiteralUtf8;
            _leadingLiteralsUtf8 = leadingLiteralsUtf8;
            TrailingLiteralUtf8 = trailingLiteralUtf8;
            _trailingLiteralsUtf8 = trailingLiteralsUtf8;
            MaxGap = maxGap;
            MinGap = minGap;
            GapSameLine = gapSameLine;
            Separator = separator;
            BranchSeparators = branchSeparators ?? [];
            YieldLeadingLiteralOnly = yieldLeadingLiteralOnly;
            LeadingLiteralLeadingBoundary = leadingLiteralLeadingBoundary;
            LeadingLiteralTrailingBoundary = leadingLiteralTrailingBoundary;
            TrailingLiteralLeadingBoundary = trailingLiteralLeadingBoundary;
            TrailingLiteralTrailingBoundary = trailingLiteralTrailingBoundary;
            var trailingCount = HasPairedTrailingLiterals ? TrailingLiteralsUtf8.Length : 1;
            TrailingSearchers = owner.AllocateArray<PreparedSearcher>(trailingCount, false);
            RequiresSemanticExecutor = Utf8SearchPlan.GetLiteralOverlap(leadingLiteralsUtf8) != Utf8AlternateLiteralOverlapKind.None;
            for (var i = 0; i < trailingCount; i++)
            {
                owner.ChargeWork(1);
                var trailing = HasPairedTrailingLiterals ? TrailingLiteralsUtf8[i] : trailingLiteralUtf8;
                // The exact short-needle constructor runs at most three scans.
                // It borrows the byte payload and acquires no secondary arrays.
                owner.ChargeWork(3ul * (ulong)trailing.Length);
                TrailingSearchers[i] = new PreparedSearcher(new PreparedSubstringSearch(trailing, false), false);
                var currentSeparator = BranchSeparators.Length > 0 ? BranchSeparators[i] : separator;
                var leading = HasPairedTrailingLiterals ? LeadingLiteralsUtf8[i] : leadingLiteralUtf8;
                if (gapSameLine) owner.ChargeWork((ulong)trailing.Length);
                RequiresSemanticExecutor |= gapSameLine && trailing.AsSpan().Contains((byte)'\n') ||
                    currentSeparator.HasValue && (currentSeparator.Minimum == 0 || currentSeparator.IsLazy && maxGap > 0 ||
                        currentSeparator.CharClass.Contains(leading[^1]) || currentSeparator.CharClass.Contains(trailing[0]));
                if (currentSeparator.HasValue)
                {
                    // A family fast path must admit every leading alternative,
                    // including a longer one whose last byte joins the separator run.
                    if (!HasPairedTrailingLiterals)
                        foreach (var alternative in LeadingLiteralsUtf8)
                        {
                            owner.ChargeWork(1);
                            RequiresSemanticExecutor |= currentSeparator.CharClass.Contains(alternative[^1]);
                        }
                    owner.ChargeWork(128);
                    for (var value = 0; value < 128; value++)
                        RequiresSemanticExecutor |= currentSeparator.CharClass.Contains((byte)value) != Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value);
                }
            }
            construction.Complete();
        }
        finally { owner.LeaveDepth(); }
    }

    /// <summary>Search anchor literal (shortest, or the only one for single-literal patterns).</summary>
    public byte[] LeadingLiteralUtf8 { get; }

    /// <summary>All leading literal alternatives; empty for single-literal patterns.</summary>
    public byte[][] LeadingLiteralsUtf8 => _leadingLiteralsUtf8 ?? [];

    public byte[] TrailingLiteralUtf8 { get; }

    /// <summary>Trailing literal alternatives aligned with <see cref="LeadingLiteralsUtf8"/>; otherwise empty.</summary>
    public byte[][] TrailingLiteralsUtf8 => _trailingLiteralsUtf8 ?? [];

    public int MaxGap { get; }

    public int MinGap { get; }

    /// <summary>
    /// When true, the gap must not contain a newline (the gap set is '.' without Singleline).
    /// </summary>
    public bool GapSameLine { get; }

    /// <summary>
    /// Minimum whitespace characters required between literal1 and the gap (from \s+ or \s*).
    /// Zero means no separator is required.
    /// </summary>
    public int GapLeadingSeparatorMinCount => Separator.Minimum;

    internal AsciiOrderedWindowSeparatorPlan Separator { get; }

    internal AsciiOrderedWindowSeparatorPlan[] BranchSeparators { get; }

    internal PreparedSearcher[] TrailingSearchers { get; }

    // Old specialized kernels remain only where their semantic envelope holds.
    internal bool RequiresSemanticExecutor { get; }

    /// <summary>
    /// When true, the match length stays on the leading literal and the trailing literal only acts as a lookahead guard.
    /// </summary>
    public bool YieldLeadingLiteralOnly { get; }

    public Utf8BoundaryRequirement LeadingLiteralLeadingBoundary { get; }

    public Utf8BoundaryRequirement LeadingLiteralTrailingBoundary { get; }

    public Utf8BoundaryRequirement TrailingLiteralLeadingBoundary { get; }

    public Utf8BoundaryRequirement TrailingLiteralTrailingBoundary { get; }

    public bool HasValue => LeadingLiteralUtf8 is { Length: > 0 } && TrailingLiteralUtf8 is { Length: > 0 };

    public bool IsLiteralFamily => LeadingLiteralsUtf8.Length > 1;

    public bool HasPairedTrailingLiterals =>
        LeadingLiteralsUtf8.Length > 1 &&
        TrailingLiteralsUtf8.Length > 1 &&
        LeadingLiteralsUtf8.Length == TrailingLiteralsUtf8.Length;
}
