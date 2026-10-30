using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
namespace Lokad.Utf8Regex.Internal.FrontEnd;


internal static partial class Utf8AsciiSimplePatternLowerer
{
    private readonly record struct ScanFrame(Utf8TreeNode Node, int NextChild, bool Entered);

    private const int MaxExpandedBranches = 64;
    private const int MaxLiteralCharClassExpansion = 4;
    private const int MaxPreparationWork = 4096;
    private const int MaxPreparationOutput = 4096;
    // Keep the optimizer's existing admission boundary after removing CLR recursion.
    // Declining this speculative plan leaves the other owned execution routes available.
    private const int MaxLoweringDepth = 256;

    public static bool TryCreatePlan(
        Utf8SemanticRegex semanticRegex,
        RegexOptions options,
        out AsciiSimplePatternPlan simplePatternPlan,
        out Utf8SearchFacts searchFacts)
    {
        simplePatternPlan = default;
        searchFacts = default;
        if (semanticRegex.RuntimeTree?.Root is not { Kind: Utf8TreeNodeKind.Capture, ChildCount: 1 }) return false;
        using var construction = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(construction.Budget);
        var preparation = construction.Budget.AllocateObject((MaxPreparationWork, MaxPreparationOutput),
            static state => new Utf8PreparationBudget(state.MaxPreparationWork, state.MaxPreparationOutput));
        if (!CreatePlan(storage, semanticRegex, options, preparation, out var result, out var facts)) return false;
        RetainPlanArrays(storage, result);
        storage.Complete();
        construction.Complete();
        simplePatternPlan = result;
        searchFacts = facts;
        return true;
    }

    private static bool CreatePlan(Utf8ConstructionSession storage, Utf8SemanticRegex semanticRegex,
        RegexOptions options, Utf8PreparationBudget preparationBudget,
        out AsciiSimplePatternPlan simplePatternPlan, out Utf8SearchFacts searchFacts)
    {
        simplePatternPlan = default;
        searchFacts = default;

        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryLowerBranches(storage, root.Child(0), preparationBudget, 0, out var loweredBranches))
        {
            return false;
        }

        if (loweredBranches.Count == 0 || loweredBranches[0].Tokens.Count == 0)
        {
            return false;
        }

        var isStartAnchored = loweredBranches[0].IsStartAnchored;
        var isEndAnchored = loweredBranches[0].IsEndAnchored;
        // Absolute and line anchors lower identically above; the verifier may only
        // pin absolute positions when every branch agrees on an absolute anchor.
        var isAbsoluteStartAnchored = loweredBranches[0].IsAbsoluteStartAnchored;
        var isAbsoluteEndAnchored = loweredBranches[0].IsAbsoluteEndAnchored;
        var allowsTrailingNewlineBeforeEnd = AllowsTrailingNewlineBeforeEnd(root.Child(0));
        for (var i = 1; i < loweredBranches.Count; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (loweredBranches[i].Tokens.Count == 0 ||
                loweredBranches[i].IsStartAnchored != isStartAnchored ||
                loweredBranches[i].IsEndAnchored != isEndAnchored ||
                loweredBranches[i].IsAbsoluteStartAnchored != isAbsoluteStartAnchored ||
                loweredBranches[i].IsAbsoluteEndAnchored != isAbsoluteEndAnchored)
            {
                return false;
            }
        }

        var branches = storage.Allocate<AsciiSimplePatternToken[]>(loweredBranches.Count, false);
        var checks = storage.Buffer<AsciiFixedLiteralCheck>();
        for (var index = 0; index < loweredBranches.Count; index++)
        {
            storage.Budget.ChargeWork(1);
            branches[index] = storage.Copy(loweredBranches[index].Tokens, false);
            checks.AddRange(loweredBranches[index].FixedChecks.Span);
        }
        var allFixedChecks = storage.Copy(checks, false);
        if (!TryGetLengthBounds(branches, out var minLength, out _) || minLength == 0)
        {
            return false;
        }

        var ignoreCase = (options & RegexOptions.IgnoreCase) != 0;
        if (ignoreCase && HasCaseClearingGroup(root.Child(0)))
        {
            return false;
        }

        if (ignoreCase)
        {
            NormalizeBranchesForIgnoreCase(branches);
        }

        ExtractSearchLiterals(storage, branches, out var searchLiteralOffset, out var searchLiterals, out var fixedLiteralChecks);
        if (allFixedChecks.Length > 0)
        {
            if (ignoreCase)
            {
                NormalizeFixedChecksForIgnoreCase(storage, allFixedChecks);
            }

            if (fixedLiteralChecks.Length == 0) fixedLiteralChecks = allFixedChecks;
            else
            {
                var combined = storage.Buffer<AsciiFixedLiteralCheck>();
                combined.AddRange(fixedLiteralChecks);
                combined.AddRange(allFixedChecks);
                fixedLiteralChecks = storage.Copy(combined, false);
            }
        }

        storage.Budget.ChargeWork((ulong)branches.Length);
        simplePatternPlan = new AsciiSimplePatternPlan(
            branches,
            searchLiteralOffset,
            searchLiterals,
            fixedLiteralChecks,
            isStartAnchored,
            isEndAnchored,
            allowsTrailingNewlineBeforeEnd,
            ignoreCase,
            IsUtf8ByteSafe(branches),
            TryExtractCharClassRunPlan(branches, isStartAnchored, isEndAnchored, out var runPlan) ? runPlan : default,
            anchoredHeadTailRunPlan: default,
            anchoredValidatorPlan: TryExtractAnchoredValidatorPlan(storage, branches, isStartAnchored, isEndAnchored, ignoreCase, out var anchoredValidatorPlan)
                ? anchoredValidatorPlan
                : default,
            anchoredBoundedDatePlan: TryExtractAnchoredBoundedDatePlan(branches, isStartAnchored, isEndAnchored, out var anchoredBoundedDatePlan)
                ? anchoredBoundedDatePlan
                : default,
            anchoredOptionalFieldPlan: TryExtractAnchoredOptionalFieldPlan(branches, isStartAnchored, isEndAnchored, ignoreCase, out var anchoredOptionalFieldPlan)
                ? anchoredOptionalFieldPlan
                : default,
            repeatedDigitGroupPlan: TryExtractRepeatedDigitGroupPlan(storage, branches, ignoreCase, out var repeatedDigitGroupPlan)
                ? repeatedDigitGroupPlan
                : default,
            boundedSuffixLiteralPlan: !ignoreCase &&
                TryExtractBoundedSuffixLiteralPlan(
                    storage, branches,
                    isStartAnchored,
                    isEndAnchored,
                    allowUnicodeWhitespace: (options & RegexOptions.ECMAScript) == 0,
                    out var boundedSuffixLiteralPlan)
                ? boundedSuffixLiteralPlan
                : default,
            symmetricLiteralWindowPlan: TryExtractSymmetricLiteralWindowPlan(storage, branches, isStartAnchored, isEndAnchored, out var symmetricLiteralWindowPlan)
                ? symmetricLiteralWindowPlan
                : default,
            isAbsoluteStartAnchored: isAbsoluteStartAnchored,
            isAbsoluteEndAnchored: isAbsoluteEndAnchored);
        searchFacts = searchLiterals.Length switch
        {
            1 => Utf8SearchFacts.Create(
                     ignoreCase ? Utf8SearchKind.AsciiFoldedByteLiteral : Utf8SearchKind.ExactAsciiLiteral,
                     new Utf8SearchFactData
                     {
                         LiteralUtf8 = searchLiterals[0],
                     }),
            > 1 when !ignoreCase && searchLiteralOffset == 0 => Utf8SearchFacts.Create(
                                                                    Utf8SearchKind.ExactAsciiLiterals,
                                                                    new Utf8SearchFactData
                                                                    {
                                                                        AlternateLiteralsUtf8 = searchLiterals,
                                                                    }),
            _ => Utf8SearchFacts.Create(Utf8SearchKind.None),
        };

        return true;
    }

    private static void RetainPlanArrays(Utf8ConstructionSession storage, AsciiSimplePatternPlan plan)
    {
        storage.Retain(plan.Branches);
        foreach (var branch in plan.Branches) { Utf8ConstructionScope.ChargeWork(1); storage.Retain(branch); }
        storage.Retain(plan.SearchLiterals);
        foreach (var literal in plan.SearchLiterals) { Utf8ConstructionScope.ChargeWork(1); storage.Retain(literal); }
        storage.Retain(plan.FixedLiteralChecks);
        foreach (var check in plan.FixedLiteralChecks) { Utf8ConstructionScope.ChargeWork(1); storage.Retain(check.Literal); }
        if (plan.AnchoredValidatorPlan.HasValue)
        {
            storage.Retain(plan.AnchoredValidatorPlan.Segments);
            foreach (var segment in plan.AnchoredValidatorPlan.Segments)
                { Utf8ConstructionScope.ChargeWork(1); if (segment.IsLiteral) storage.Retain(segment.Literal); }
        }
        if (plan.RepeatedDigitGroupPlan.HasValue) storage.Retain(plan.RepeatedDigitGroupPlan.SeparatorBytes);
        if (plan.BoundedSuffixLiteralPlan.HasValue) storage.Retain(plan.BoundedSuffixLiteralPlan.LiteralUtf8);
        if (plan.SymmetricLiteralWindowPlan.HasValue)
        {
            storage.Retain(plan.SymmetricLiteralWindowPlan.FirstLiteralUtf8);
            storage.Retain(plan.SymmetricLiteralWindowPlan.SecondLiteralUtf8);
        }
    }

    private static bool HasCaseClearingGroup(Utf8TreeNode node) => HasGroupWithCase(node, false);

    // Byte plans have one folding flag. These two callers reject opposite scoped
    // changes when a plan would otherwise lose the scalar program's node options.
    internal static bool HasScopedIgnoreCase(Utf8TreeNode node) => HasGroupWithCase(node, true);

    private static bool HasGroupWithCase(Utf8TreeNode node, bool ignoreCase)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(budget, new(node, 0, false));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = frame.Node;
            if (!frame.Entered)
            {
                frame = frame with { Entered = true };
                if (node.Kind == Utf8TreeNodeKind.Group && ((node.Options & RegexOptions.IgnoreCase) != 0) == ignoreCase)
                    return true;
            }
            if (frame.NextChild == node.ChildCount) { frames.Pop(); continue; }
            frames[index] = frame with { NextChild = frame.NextChild + 1 };
            frames.Push(new(node.Child(frame.NextChild), 0, false));
        }
        return false;
    }

    private static bool IsUtf8ByteSafe(AsciiSimplePatternToken[][] branches)
    {
        foreach (var branch in branches)
        {
            Utf8ConstructionScope.ChargeWork(1);
            foreach (var token in branch)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (token.Kind == AsciiSimplePatternTokenKind.Dot || token.RequiresAsciiInput)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryGetUniformRepeatedClass(
        AsciiSimplePatternToken[] tokens,
        int start,
        int endExclusive,
        out AsciiCharClass charClass,
        out bool requiresAsciiInput)
    {
        charClass = default;
        requiresAsciiInput = false;
        if (endExclusive <= start ||
            tokens[start].Kind != AsciiSimplePatternTokenKind.CharClass ||
            tokens[start].CharClass.IsEmpty)
        {
            return false;
        }

        var firstCharClass = tokens[start].CharClass;
        var firstRequiresAsciiInput = tokens[start].RequiresAsciiInput;

        for (var i = start + 1; i < endExclusive; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (tokens[i].Kind != AsciiSimplePatternTokenKind.CharClass ||
                tokens[i].CharClass.IsEmpty ||
                !firstCharClass.HasSameDefinition(tokens[i].CharClass) ||
                tokens[i].RequiresAsciiInput != firstRequiresAsciiInput)
            {
                return false;
            }
        }

        charClass = firstCharClass;
        requiresAsciiInput = firstRequiresAsciiInput;
        return true;
    }

    private static bool TryLowerLiteral(char ch, out AsciiSimplePatternToken token)
    {
        if (ch > 0x7F)
        {
            token = default;
            return false;
        }

        token = new AsciiSimplePatternToken((byte)ch);
        return true;
    }

    private static bool TryLowerLiteralText(Utf8ConstructionSession storage, Utf8TreeNode node, Utf8PreparationBudget budget, out Utf8ConstructionBuffer<AsciiSimplePatternToken> tokens)
    {
        tokens = storage.Buffer<AsciiSimplePatternToken>();
        if (node.LiteralUtf8 is { } bytes)
        {
            if (!budget.TryReserve(bytes.Length, bytes.Length)) return false;
            foreach (var value in bytes)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (value > 0x7f) return false;
            }
            foreach (var value in bytes) { Utf8ConstructionScope.ChargeWork(1); tokens.Add(new AsciiSimplePatternToken(value)); }
            return true;
        }
        var text = node.Str;
        if (text is null || !budget.TryReserve(text.Length, text.Length))
        {
            return false;
        }

        foreach (var ch in text)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (ch > 0x7F) return false;
        }

        foreach (var ch in text) { Utf8ConstructionScope.ChargeWork(1); tokens.Add(new AsciiSimplePatternToken((byte)ch)); }

        return true;
    }

    private static bool TryGetLengthBounds(AsciiSimplePatternToken[][] branches, out int minLength, out int maxLength)
    {
        minLength = int.MaxValue;
        maxLength = 0;
        foreach (var branch in branches)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (branch.Length == 0)
            {
                return false;
            }

            if (branch.Length < minLength)
            {
                minLength = branch.Length;
            }

            if (branch.Length > maxLength)
            {
                maxLength = branch.Length;
            }
        }

        return true;
    }

    private readonly record struct LoweredBranch(
        Utf8ConstructionBuffer<AsciiSimplePatternToken> Tokens,
        Utf8ConstructionBuffer<AsciiFixedLiteralCheck> FixedChecks,
        bool IsStartAnchored,
        bool IsEndAnchored,
        bool IsAbsoluteStartAnchored,
        bool IsAbsoluteEndAnchored);
}
