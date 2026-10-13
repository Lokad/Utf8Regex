using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEndSearchAnalyzer
{
    private const int MaxDeterministicPrefixSets = 32;

    public static Utf8SearchFacts Analyze(Utf8SemanticRegex semanticRegex)
    {
        var runtimeTree = semanticRegex.RuntimeTree;
        var findOptimizations = Utf8OwnedFindFacts.FromTree(runtimeTree);
        if (findOptimizations is null || runtimeTree is null)
        {
            return Utf8SearchFacts.Create(Utf8SearchKind.None);
        }

        TrySelectRequiredLiteralPrefilter(runtimeTree.Root, out var requiredPrefilterLiteral, out var requiredPrefilterAlternateLiterals);
        TrySelectQuotedAsciiRunPrefilter(runtimeTree.Root, out var secondaryRequiredPrefilterQuotedAsciiClass, out var secondaryRequiredPrefilterQuotedAsciiLength);
        var secondaryRequiredPrefilterQuotedAsciiSet = CreateAsciiByteSet(secondaryRequiredPrefilterQuotedAsciiClass);
        TrySelectRequiredWindowPrefilters(
            runtimeTree.Root,
            requiredPrefilterAlternateLiterals,
            secondaryRequiredPrefilterQuotedAsciiSet,
            secondaryRequiredPrefilterQuotedAsciiLength,
            out var requiredWindowPrefilters);
        TrySelectOrderedAsciiWindow(runtimeTree.Root, findOptimizations, UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions), out var orderedLeadingLiterals, out var orderedTrailingLiteral);
        TrySelectOrderedWindowConstraints(runtimeTree.Root, orderedLeadingLiterals, orderedTrailingLiteral,
            out var orderedWindowMaxGap, out var orderedWindowSameLine);
        TrySelectFallbackStartTransform(runtimeTree.Root, out var fallbackStartTransform);
        TrySelectBoundaryRequirements(runtimeTree.Root, out var leadingBoundary, out var trailingBoundary);
        requiredPrefilterLiteral ??= [];
        requiredPrefilterAlternateLiterals ??= [];
        requiredWindowPrefilters ??= [];
        int? exactRequiredLength = findOptimizations.IsFixedLength ? findOptimizations.MinRequiredLength : null;
        var maxPossibleLength = findOptimizations.IsFixedLength ? null : findOptimizations.MaxPossibleLength;

        if (TrySelectTrailingFixedLengthAnchor(findOptimizations, out var trailingKind, out var trailingLength))
        {
            return Utf8SearchFacts.Create(
                       trailingKind,
                       new Utf8SearchFactData
                       {
                           RequiredPrefilterLiteralUtf8 = requiredPrefilterLiteral ?? [],
                           RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiterals ?? [],
                           SecondaryRequiredPrefilterQuotedAsciiSet = secondaryRequiredPrefilterQuotedAsciiSet,
                           SecondaryRequiredPrefilterQuotedAsciiLength = secondaryRequiredPrefilterQuotedAsciiLength,
                           RequiredWindowPrefilters = requiredWindowPrefilters ?? [],
                           OrderedWindowLeadingLiteralsUtf8 = orderedLeadingLiterals,
                           OrderedWindowTrailingLiteralUtf8 = orderedTrailingLiteral,
                           OrderedWindowMaxGap = orderedWindowMaxGap,
                           OrderedWindowSameLine = orderedWindowSameLine,
                           FallbackStartTransform = fallbackStartTransform,
                           MinRequiredLength = trailingLength,
                           ExactRequiredLength = exactRequiredLength,
                           MaxPossibleLength = maxPossibleLength,
                           LeadingBoundary = leadingBoundary,
                           TrailingBoundary = trailingBoundary,
                       });
        }

        if (TrySelectFixedDistanceSets(findOptimizations, UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions), out var fixedDistanceSets, out var fixedDistanceMinRequiredLength))
        {
            return Utf8SearchFacts.Create(
                       Utf8SearchKind.FixedDistanceAsciiSets,
                       new Utf8SearchFactData
                       {
                           RequiredPrefilterLiteralUtf8 = requiredPrefilterLiteral ?? [],
                           RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiterals ?? [],
                           SecondaryRequiredPrefilterQuotedAsciiSet = secondaryRequiredPrefilterQuotedAsciiSet,
                           SecondaryRequiredPrefilterQuotedAsciiLength = secondaryRequiredPrefilterQuotedAsciiLength,
                           RequiredWindowPrefilters = requiredWindowPrefilters ?? [],
                           OrderedWindowLeadingLiteralsUtf8 = orderedLeadingLiterals,
                           OrderedWindowTrailingLiteralUtf8 = orderedTrailingLiteral,
                           OrderedWindowMaxGap = orderedWindowMaxGap,
                           OrderedWindowSameLine = orderedWindowSameLine,
                           FallbackStartTransform = fallbackStartTransform,
                           FixedDistanceSets = fixedDistanceSets,
                           MinRequiredLength = fixedDistanceMinRequiredLength,
                           ExactRequiredLength = exactRequiredLength,
                           MaxPossibleLength = maxPossibleLength,
                           LeadingBoundary = leadingBoundary,
                           TrailingBoundary = trailingBoundary,
                       });
        }

        if (TrySelectDeterministicPrefixSets(runtimeTree.Root, findOptimizations.MinRequiredLength, fallbackStartTransform, out var deterministicPrefixSets, out var deterministicPrefixMinRequiredLength))
        {
            return Utf8SearchFacts.Create(
                       Utf8SearchKind.FixedDistanceAsciiSets,
                       new Utf8SearchFactData
                       {
                           RequiredPrefilterLiteralUtf8 = requiredPrefilterLiteral ?? [],
                           RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiterals ?? [],
                           SecondaryRequiredPrefilterQuotedAsciiSet = secondaryRequiredPrefilterQuotedAsciiSet,
                           SecondaryRequiredPrefilterQuotedAsciiLength = secondaryRequiredPrefilterQuotedAsciiLength,
                           RequiredWindowPrefilters = requiredWindowPrefilters ?? [],
                           OrderedWindowLeadingLiteralsUtf8 = orderedLeadingLiterals,
                           OrderedWindowTrailingLiteralUtf8 = orderedTrailingLiteral,
                           OrderedWindowMaxGap = orderedWindowMaxGap,
                           OrderedWindowSameLine = orderedWindowSameLine,
                           FallbackStartTransform = fallbackStartTransform,
                           FixedDistanceSets = deterministicPrefixSets,
                           MinRequiredLength = deterministicPrefixMinRequiredLength,
                           ExactRequiredLength = exactRequiredLength,
                           MaxPossibleLength = maxPossibleLength,
                           LeadingBoundary = leadingBoundary,
                           TrailingBoundary = trailingBoundary,
                       });
        }

        if (TrySelectFixedDistanceLiteral(findOptimizations, UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions), out var fixedLiteral, out var distance, out var minRequiredLength))
        {
            return Utf8SearchFacts.Create(
                       Utf8SearchKind.FixedDistanceAsciiLiteral,
                       new Utf8SearchFactData
                       {
                           LiteralUtf8 = fixedLiteral,
                           RequiredPrefilterLiteralUtf8 = requiredPrefilterLiteral ?? [],
                           RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiterals ?? [],
                           SecondaryRequiredPrefilterQuotedAsciiSet = secondaryRequiredPrefilterQuotedAsciiSet,
                           SecondaryRequiredPrefilterQuotedAsciiLength = secondaryRequiredPrefilterQuotedAsciiLength,
                           RequiredWindowPrefilters = requiredWindowPrefilters ?? [],
                           OrderedWindowLeadingLiteralsUtf8 = orderedLeadingLiterals,
                           OrderedWindowTrailingLiteralUtf8 = orderedTrailingLiteral,
                           OrderedWindowMaxGap = orderedWindowMaxGap,
                           OrderedWindowSameLine = orderedWindowSameLine,
                           FallbackStartTransform = fallbackStartTransform,
                           Distance = distance,
                           MinRequiredLength = minRequiredLength,
                           ExactRequiredLength = exactRequiredLength,
                           MaxPossibleLength = maxPossibleLength,
                           LeadingBoundary = leadingBoundary,
                           TrailingBoundary = trailingBoundary,
                       });
        }

        if (TrySelectAlternatePrefixes(findOptimizations, UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions), out var alternateLiterals))
        {
            var searchKind = alternateLiterals.Any(static literal => literal.Any(static b => b > 0x7F))
                ? Utf8SearchKind.ExactUtf8Literals
                : Utf8SearchKind.ExactAsciiLiterals;
            return Utf8SearchFacts.Create(
                       searchKind,
                       new Utf8SearchFactData
                       {
                           AlternateLiteralsUtf8 = alternateLiterals,
                           CanGuideFallbackStarts = CanGuideFallbackStarts(semanticRegex.ExecutionOptions),
                           RequiredPrefilterLiteralUtf8 = requiredPrefilterLiteral ?? [],
                           RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiterals ?? [],
                           SecondaryRequiredPrefilterQuotedAsciiSet = secondaryRequiredPrefilterQuotedAsciiSet,
                           SecondaryRequiredPrefilterQuotedAsciiLength = secondaryRequiredPrefilterQuotedAsciiLength,
                           RequiredWindowPrefilters = requiredWindowPrefilters ?? [],
                           OrderedWindowLeadingLiteralsUtf8 = orderedLeadingLiterals,
                           OrderedWindowTrailingLiteralUtf8 = orderedTrailingLiteral,
                           OrderedWindowMaxGap = orderedWindowMaxGap,
                           OrderedWindowSameLine = orderedWindowSameLine,
                           FallbackStartTransform = fallbackStartTransform,
                           ExactRequiredLength = exactRequiredLength,
                           MaxPossibleLength = maxPossibleLength,
                           LeadingBoundary = leadingBoundary,
                           TrailingBoundary = trailingBoundary,
                       });
        }

        if (TrySelectLeadingLiteral(findOptimizations, out var leadingLiteral) && IsAscii(leadingLiteral))
        {
            var searchKind = UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions)
                ? Utf8SearchKind.AsciiFoldedByteLiteral
                : Utf8SearchKind.ExactAsciiLiteral;

            return Utf8SearchFacts.Create(
                       searchKind,
                       new Utf8SearchFactData
                       {
                           LiteralUtf8 = EncodeLiteral(leadingLiteral),
                           CanGuideFallbackStarts = CanGuideFallbackStarts(semanticRegex.ExecutionOptions),
                           RequiredPrefilterLiteralUtf8 = requiredPrefilterLiteral ?? [],
                           RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiterals ?? [],
                           SecondaryRequiredPrefilterQuotedAsciiSet = secondaryRequiredPrefilterQuotedAsciiSet,
                           SecondaryRequiredPrefilterQuotedAsciiLength = secondaryRequiredPrefilterQuotedAsciiLength,
                           RequiredWindowPrefilters = requiredWindowPrefilters ?? [],
                           OrderedWindowLeadingLiteralsUtf8 = orderedLeadingLiterals,
                           OrderedWindowTrailingLiteralUtf8 = orderedTrailingLiteral,
                           OrderedWindowMaxGap = orderedWindowMaxGap,
                           OrderedWindowSameLine = orderedWindowSameLine,
                           FallbackStartTransform = fallbackStartTransform,
                           ExactRequiredLength = exactRequiredLength,
                           MaxPossibleLength = maxPossibleLength,
                           LeadingBoundary = leadingBoundary,
                           TrailingBoundary = trailingBoundary,
                       });
        }

        if (TrySelectCandidateLiteral(findOptimizations, out var candidate) && IsAscii(candidate))
        {
            var searchKind = UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions)
                ? Utf8SearchKind.AsciiFoldedByteLiteral
                : Utf8SearchKind.ExactAsciiLiteral;

            return Utf8SearchFacts.Create(
                       searchKind,
                       new Utf8SearchFactData
                       {
                           LiteralUtf8 = EncodeLiteral(candidate),
                           RequiredPrefilterLiteralUtf8 = requiredPrefilterLiteral ?? [],
                           RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiterals ?? [],
                           RequiredWindowPrefilters = requiredWindowPrefilters ?? [],
                           OrderedWindowLeadingLiteralsUtf8 = orderedLeadingLiterals,
                           OrderedWindowTrailingLiteralUtf8 = orderedTrailingLiteral,
                           OrderedWindowMaxGap = orderedWindowMaxGap,
                           OrderedWindowSameLine = orderedWindowSameLine,
                           FallbackStartTransform = fallbackStartTransform,
                           ExactRequiredLength = exactRequiredLength,
                           MaxPossibleLength = maxPossibleLength,
                           LeadingBoundary = leadingBoundary,
                           TrailingBoundary = trailingBoundary,
                       });
        }

        if (requiredPrefilterLiteral is not null ||
            requiredPrefilterAlternateLiterals is not null ||
            secondaryRequiredPrefilterQuotedAsciiSet.HasValue)
        {
            return Utf8SearchFacts.Create(
                       Utf8SearchKind.None,
                       new Utf8SearchFactData
                       {
                           RequiredPrefilterLiteralUtf8 = requiredPrefilterLiteral ?? [],
                           RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiterals ?? [],
                           SecondaryRequiredPrefilterQuotedAsciiSet = secondaryRequiredPrefilterQuotedAsciiSet,
                           SecondaryRequiredPrefilterQuotedAsciiLength = secondaryRequiredPrefilterQuotedAsciiLength,
                           RequiredWindowPrefilters = requiredWindowPrefilters ?? [],
                           OrderedWindowLeadingLiteralsUtf8 = orderedLeadingLiterals,
                           OrderedWindowTrailingLiteralUtf8 = orderedTrailingLiteral,
                           OrderedWindowMaxGap = orderedWindowMaxGap,
                           OrderedWindowSameLine = orderedWindowSameLine,
                           FallbackStartTransform = fallbackStartTransform,
                           ExactRequiredLength = exactRequiredLength,
                           MaxPossibleLength = maxPossibleLength,
                           LeadingBoundary = leadingBoundary,
                           TrailingBoundary = trailingBoundary,
                       });
        }

        return Utf8SearchFacts.Create(
                   Utf8SearchKind.None,
                   new Utf8SearchFactData
                   {
                       SecondaryRequiredPrefilterQuotedAsciiSet = secondaryRequiredPrefilterQuotedAsciiSet,
                       SecondaryRequiredPrefilterQuotedAsciiLength = secondaryRequiredPrefilterQuotedAsciiLength,
                       RequiredWindowPrefilters = requiredWindowPrefilters ?? [],
                       OrderedWindowLeadingLiteralsUtf8 = orderedLeadingLiterals,
                       OrderedWindowTrailingLiteralUtf8 = orderedTrailingLiteral,
                       OrderedWindowMaxGap = orderedWindowMaxGap,
                       OrderedWindowSameLine = orderedWindowSameLine,
                       FallbackStartTransform = fallbackStartTransform,
                       ExactRequiredLength = exactRequiredLength,
                       MaxPossibleLength = maxPossibleLength,
                       LeadingBoundary = leadingBoundary,
                       TrailingBoundary = trailingBoundary,
                   });
    }

    private static bool TrySelectQuotedAsciiRunPrefilter(Utf8TreeNode root, out AsciiCharClass? asciiSet, out int runLength)
    {
        asciiSet = null;
        runLength = 0;
        return TryFindQuotedAsciiRun(root, out asciiSet, out runLength);
    }

    private static void TrySelectRequiredWindowPrefilters(
        Utf8TreeNode root,
        byte[][]? requiredPrefilterAlternateLiteralsUtf8,
        Utf8SearchAsciiSet quotedAsciiSet,
        int quotedAsciiLength,
        out Utf8WindowSearchFacts[]? requiredWindowPrefilters)
    {
        requiredWindowPrefilters = null;

        if (requiredPrefilterAlternateLiteralsUtf8 is not { Length: > 0 } literalFamily ||
            !quotedAsciiSet.HasValue ||
            quotedAsciiLength <= 0)
        {
            return;
        }

        root = UnwrapStructuralNode(root) ?? root;
        var maxLines = TryGetFiniteLineBreakCount(root, out var totalLineBreaks) && totalLineBreaks > 0
            ? totalLineBreaks + 1
            : root.Kind == Utf8TreeNodeKind.Alternate && root.ChildCount > 1 ? 5 : 0;

        if (maxLines > 0)
        {
            var family = Utf8PreparedSearcherInfo.LiteralFamily(literalFamily);
            var quoted = Utf8PreparedSearcherInfo.QuotedAsciiRun(quotedAsciiSet, quotedAsciiLength);
            requiredWindowPrefilters =
            [
                Utf8WindowSearchFacts.WithinLines(family, quoted, maxLines),
                Utf8WindowSearchFacts.WithinLines(quoted, family, maxLines),
            ];
        }
    }

    private struct ScanFrame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int Index;
        internal bool Entered;
    }

    private static bool TryFindQuotedAsciiRun(Utf8TreeNode node, out AsciiCharClass? asciiSet, out int runLength)
    {
        asciiSet = null;
        runLength = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (!frame.Entered)
            {
                frame.Node = UnwrapStructuralNode(frame.Node) ?? frame.Node;
                frame.Entered = true;
                node = frame.Node;
                if (node.Kind == Utf8TreeNodeKind.Concatenate && node.ChildCount >= 3)
                {
                    for (var i = 0; i <= node.ChildCount - 3; i++)
                    {
                        budget.ChargeWork(1);
                        if (IsQuoteNode(node.Child(i)) &&
                            TryGetExactAsciiSetLength(node.Child(i + 1), out var set, out var length) &&
                            IsQuoteNode(node.Child(i + 2)))
                        {
                            asciiSet = set;
                            runLength = length;
                            return true;
                        }
                    }
                }
            }
            node = frame.Node;
            if (frame.Index == node.ChildCount) { frames.Pop(); continue; }
            var child = node.Child(frame.Index++);
            frames[index] = frame;
            frames.Push(new(child));
        }
        return false;
    }

    private static Utf8SearchAsciiSet CreateAsciiByteSet(AsciiCharClass? charClass) =>
        charClass is { } mask ? Utf8SearchAsciiSet.FromMasks(mask.ActualLowMask, mask.ActualHighMask) : default;

    private static bool IsQuoteNode(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var current = UnwrapStructuralNode(frame.Node) ?? frame.Node;
            if (TryGetLiteralText(current, out var literal))
            {
                if (literal is not ("\"" or "'")) return false;
                frames.Pop();
            }
            else if (current.Kind == Utf8TreeNodeKind.Alternate && current.ChildCount > 0)
            {
                if (frame.Index == current.ChildCount) { frames.Pop(); continue; }
                frame.Node = current;
                var child = current.Child(frame.Index++);
                frames[index] = frame;
                frames.Push(new(child));
            }
            else return false;
        }
        return true;
    }

    private static bool TryGetExactAsciiSetLength(Utf8TreeNode node, out AsciiCharClass? asciiSet, out int runLength)
    {
        node = UnwrapStructuralNode(node) ?? node;
        asciiSet = null;
        runLength = 0;

        if (node.Kind is Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setloopatomic &&
            node.M > 0 &&
            node.M == node.N &&
            Utf8ScalarAsciiProjector.TryProjectPositiveAsciiDefinition(node.ScalarClass, out var set))
        {
            asciiSet = set;
            runLength = node.M;
            return true;
        }

        if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop &&
            node.ChildCount == 1 &&
            node.M > 0 &&
            node.M == node.N)
        {
            var child = UnwrapStructuralNode(node.Child(0)) ?? node.Child(0);
            if (child.Kind == Utf8TreeNodeKind.Set &&
                Utf8ScalarAsciiProjector.TryProjectPositiveAsciiDefinition(child.ScalarClass, out var childSet))
            {
                asciiSet = childSet;
                runLength = node.M;
                return true;
            }
        }

        return false;
    }

    public static Utf8SearchFacts AnalyzeLiteral(
        byte[] literalUtf8,
        NativeExecutionKind executionKind) =>
        AnalyzeLiteral(
            literalUtf8,
            executionKind,
            [],
            Utf8BoundaryRequirement.None,
            Utf8BoundaryRequirement.None);

    public static Utf8SearchFacts AnalyzeLiteral(
        byte[] literalUtf8,
        NativeExecutionKind executionKind,
        byte[] trailingLiteralUtf8,
        Utf8BoundaryRequirement leadingBoundary,
        Utf8BoundaryRequirement trailingBoundary)
    {
        var searchKind = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCase
            ? Utf8SearchKind.AsciiFoldedByteLiteral
            : Utf8SearchKind.ExactAsciiLiteral;

        return Utf8SearchFacts.Create(
                   searchKind,
                   new Utf8SearchFactData
                   {
                       LiteralUtf8 = literalUtf8,
                        TrailingLiteralUtf8 = trailingLiteralUtf8,
                       LeadingBoundary = leadingBoundary,
                       TrailingBoundary = trailingBoundary,
                   });
    }

    public static Utf8SearchFacts AnalyzeSimplePattern(
        Utf8SemanticRegex semanticRegex,
        AsciiSimplePatternPlan simplePatternPlan,
        Utf8SearchFacts fallbackSearchFacts)
    {
        var simplePatternFacts = Utf8OwnedFindFacts.FromTree(semanticRegex.RuntimeTree);
        var exactRequiredLength = simplePatternFacts?.IsFixedLength == true
            ? simplePatternFacts.MinRequiredLength
            : fallbackSearchFacts.ExactRequiredLength;
        var maxPossibleLength = simplePatternFacts?.IsFixedLength == false
            ? simplePatternFacts.MaxPossibleLength
            : fallbackSearchFacts.MaxPossibleLength;

        if (simplePatternFacts is { } findOptimizations)
        {
            if (TrySelectTrailingFixedLengthAnchor(findOptimizations, out var trailingKind, out var trailingLength))
            {
                return Utf8SearchFacts.Create(
                           trailingKind,
                           new Utf8SearchFactData
                           {
                               MinRequiredLength = trailingLength,
                               ExactRequiredLength = exactRequiredLength,
                               MaxPossibleLength = maxPossibleLength,
                           });
            }

            if (TrySelectFixedDistanceLiteral(findOptimizations, simplePatternPlan.IgnoreCase, out var fixedLiteral, out var distance, out var minRequiredLength))
            {
                if (fixedLiteral.Length == 2)
                {
                    return Utf8SearchFacts.Create(
                               Utf8SearchKind.FixedDistanceAsciiChar,
                               new Utf8SearchFactData
                               {
                                   LiteralUtf8 = [fixedLiteral[1]],
                                   Distance = distance + 1,
                                   MinRequiredLength = minRequiredLength,
                                   ExactRequiredLength = exactRequiredLength,
                                   MaxPossibleLength = maxPossibleLength,
                               });
                }

                return Utf8SearchFacts.Create(
                           Utf8SearchKind.FixedDistanceAsciiLiteral,
                           new Utf8SearchFactData
                           {
                               LiteralUtf8 = fixedLiteral,
                               Distance = distance,
                               MinRequiredLength = minRequiredLength,
                               ExactRequiredLength = exactRequiredLength,
                               MaxPossibleLength = maxPossibleLength,
                           });
            }

            if (TrySelectFixedDistanceSets(findOptimizations, simplePatternPlan.IgnoreCase, out var fixedDistanceSets, out var fixedDistanceMinRequiredLength))
            {
                return Utf8SearchFacts.Create(
                           Utf8SearchKind.FixedDistanceAsciiSets,
                           new Utf8SearchFactData
                           {
                               FixedDistanceSets = fixedDistanceSets,
                               MinRequiredLength = fixedDistanceMinRequiredLength,
                               ExactRequiredLength = exactRequiredLength,
                               MaxPossibleLength = maxPossibleLength,
                           });
            }
        }

        if (!simplePatternPlan.IgnoreCase &&
            simplePatternFacts?.AlternatePrefixes is { Length: > 1 } simplePatternAlternatePrefixes &&
            TryEncodeAlternates(simplePatternAlternatePrefixes, out var alternateLiterals))
        {
            var searchKind = alternateLiterals.Any(static literal => literal.Any(static b => b > 0x7F))
                ? Utf8SearchKind.ExactUtf8Literals
                : Utf8SearchKind.ExactAsciiLiterals;
            return Utf8SearchFacts.Create(
                       searchKind,
                       new Utf8SearchFactData
                       {
                           AlternateLiteralsUtf8 = alternateLiterals,
                           ExactRequiredLength = exactRequiredLength,
                           MaxPossibleLength = maxPossibleLength,
                       });
        }

        if (simplePatternFacts?.AlternatePrefixes is { Length: 1 } alternatePrefixes)
        {
            // A covering family is a disjunction. Only its single-member form
            // can be consumed as one mandatory literal.
            var literalPrefix = alternatePrefixes[0];
            if (literalPrefix.Length > 0)
            {
                return Utf8SearchFacts.Create(
                           simplePatternPlan.IgnoreCase ? Utf8SearchKind.AsciiFoldedByteLiteral : Utf8SearchKind.ExactAsciiLiteral,
                           new Utf8SearchFactData
                           {
                           LiteralUtf8 = EncodeLiteral(literalPrefix),
                               ExactRequiredLength = exactRequiredLength,
                               MaxPossibleLength = maxPossibleLength,
                           });
            }
        }

        if (simplePatternFacts?.LongestLiteral is { Length: > 0 } literal)
        {
            return Utf8SearchFacts.Create(
                       simplePatternPlan.IgnoreCase ? Utf8SearchKind.AsciiFoldedByteLiteral : Utf8SearchKind.ExactAsciiLiteral,
                       new Utf8SearchFactData
                       {
                           LiteralUtf8 = EncodeLiteral(literal),
                           ExactRequiredLength = exactRequiredLength,
                           MaxPossibleLength = maxPossibleLength,
                       });
        }

        return Utf8SearchFacts.Create(
                   fallbackSearchFacts.Kind,
                   new Utf8SearchFactData
                   {
                       LiteralUtf8 = fallbackSearchFacts.LiteralUtf8,
                       AlternateLiteralsUtf8 = fallbackSearchFacts.AlternateLiteralsUtf8,
                       FixedDistanceSets = fallbackSearchFacts.FixedDistanceSets,
                       TrailingLiteralUtf8 = fallbackSearchFacts.TrailingLiteralUtf8,
                       Distance = fallbackSearchFacts.Distance,
                       MinRequiredLength = fallbackSearchFacts.MinRequiredLength,
                       ExactRequiredLength = fallbackSearchFacts.ExactRequiredLength,
                       MaxPossibleLength = fallbackSearchFacts.MaxPossibleLength,
                       LeadingBoundary = fallbackSearchFacts.LeadingBoundary,
                       TrailingBoundary = fallbackSearchFacts.TrailingBoundary,
                   });
    }

    private static bool TrySelectCandidateLiteral(Utf8OwnedFindFacts findOptimizations, out string candidate)
    {
        if (findOptimizations.AlternatePrefixes is { Length: 1 } alternatePrefixes)
        {
            candidate = alternatePrefixes[0];
            if (candidate.Length > 0)
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(findOptimizations.LongestLiteral))
        {
            candidate = findOptimizations.LongestLiteral;
            return true;
        }

        if (!string.IsNullOrEmpty(findOptimizations.LeadingLiteral))
        {
            candidate = findOptimizations.LeadingLiteral;
            return true;
        }

        candidate = string.Empty;
        return false;
    }

    private static bool TrySelectLeadingLiteral(Utf8OwnedFindFacts findOptimizations, out string leadingLiteral)
    {
        leadingLiteral = string.Empty;
        if (string.IsNullOrEmpty(findOptimizations.LeadingLiteral))
        {
            return false;
        }

        leadingLiteral = findOptimizations.LeadingLiteral;
        return true;
    }

    private static bool UsesInvariantIgnoreCase(RegexOptions options)
    {
        return (options & RegexOptions.IgnoreCase) != 0 &&
            (options & RegexOptions.CultureInvariant) != 0;
    }

    private static bool CanGuideFallbackStarts(RegexOptions options)
    {
        return (options & RegexOptions.IgnoreCase) == 0 || UsesInvariantIgnoreCase(options);
    }

    private static void TrySelectBoundaryRequirements(
        Utf8TreeNode root,
        out Utf8BoundaryRequirement leadingBoundary,
        out Utf8BoundaryRequirement trailingBoundary)
    {
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;

        var body = UnwrapStructuralNode(root);
        if (body is null)
        {
            return;
        }

        if (TryGetBoundaryRequirement(body.Kind, out var wholeBoundary))
        {
            leadingBoundary = wholeBoundary;
            trailingBoundary = wholeBoundary;
            return;
        }

        if (body.Kind != Utf8TreeNodeKind.Concatenate || body.ChildCount == 0)
        {
            return;
        }

        var startIndex = FindLeadingBoundaryChildIndex(body);
        var endIndex = FindTrailingBoundaryChildIndex(body);
        if (startIndex < 0 || endIndex < startIndex)
        {
            return;
        }

        if (TryGetBoundaryRequirement(body.Child(startIndex).Kind, out var startBoundary))
        {
            leadingBoundary = startBoundary;
        }

        if (TryGetBoundaryRequirement(body.Child(endIndex).Kind, out var endBoundary))
        {
            trailingBoundary = endBoundary;
        }
    }

    private static bool TryGetBoundaryRequirement(Utf8TreeNodeKind kind, out Utf8BoundaryRequirement requirement)
    {
        requirement = kind switch
        {
            Utf8TreeNodeKind.Boundary => Utf8BoundaryRequirement.Boundary,
            Utf8TreeNodeKind.NonBoundary => Utf8BoundaryRequirement.NonBoundary,
            _ => Utf8BoundaryRequirement.None,
        };

        return requirement != Utf8BoundaryRequirement.None;
    }

    private static int FindLeadingBoundaryChildIndex(Utf8TreeNode node)
    {
        for (var i = 0; i < node.ChildCount; i++)
        {
            var kind = node.Child(i).Kind;
            if (kind is Utf8TreeNodeKind.Boundary or Utf8TreeNodeKind.NonBoundary)
            {
                return i;
            }

            if (kind is not Utf8TreeNodeKind.Bol and
                not Utf8TreeNodeKind.Eol and
                not Utf8TreeNodeKind.Beginning and
                not Utf8TreeNodeKind.Start)
            {
                break;
            }
        }

        return -1;
    }

    private static int FindTrailingBoundaryChildIndex(Utf8TreeNode node)
    {
        for (var i = node.ChildCount - 1; i >= 0; i--)
        {
            var kind = node.Child(i).Kind;
            if (kind is Utf8TreeNodeKind.Boundary or Utf8TreeNodeKind.NonBoundary)
            {
                return i;
            }

            if (kind is not Utf8TreeNodeKind.Bol and
                not Utf8TreeNodeKind.Eol and
                not Utf8TreeNodeKind.End and
                not Utf8TreeNodeKind.EndZ)
            {
                break;
            }
        }

        return -1;
    }

    private static bool TrySelectFallbackStartTransform(Utf8TreeNode root, out Utf8FallbackStartTransform transform)
    {
        var leading = GetLeadingWindowNode(root);
        if (leading is not null && IsLeadingAsciiWhitespaceNode(leading))
        {
            transform = new Utf8FallbackStartTransform(0, Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace);
            return true;
        }

        transform = default;
        return false;
    }

    private static bool TrySelectOrderedAsciiWindow(
        Utf8TreeNode root,
        Utf8OwnedFindFacts findOptimizations,
        bool ignoreCase,
        out byte[][] leadingLiteralsUtf8,
        out byte[] trailingLiteralUtf8)
    {
        leadingLiteralsUtf8 = [];
        trailingLiteralUtf8 = [];
        if (ignoreCase)
        {
            return false;
        }

        var ordered = UnwrapStructuralNode(root);
        if (ordered is null || ordered.Kind != Utf8TreeNodeKind.Concatenate) return false;
        var first = FindFirstSignificantChildIndex(ordered);
        var last = FindLastSignificantChildIndex(ordered);
        if (first < 0 || last <= first) return false;

        if (((!TryExtractLeadingWindowPhrases(root, out var leadingPhrases) ||
              leadingPhrases.Length == 0 ||
              !TryEncodeAlternates(leadingPhrases, out leadingLiteralsUtf8)) &&
             !TrySelectOrderedWindowLeadingLiterals(findOptimizations, out leadingLiteralsUtf8)) ||
            !TryExtractTrailingLiteral(ordered.Child(last), out var trailingLiteral) ||
            !IsAscii(trailingLiteral))
        {
            leadingLiteralsUtf8 = [];
            trailingLiteralUtf8 = [];
            return false;
        }

        trailingLiteralUtf8 = EncodeLiteral(trailingLiteral);
        if (trailingLiteralUtf8.Length == 0)
        {
            leadingLiteralsUtf8 = [];
            trailingLiteralUtf8 = [];
            return false;
        }

        for (var i = 0; i < leadingLiteralsUtf8.Length; i++)
        {
            if (leadingLiteralsUtf8[i].AsSpan().SequenceEqual(trailingLiteralUtf8))
            {
                leadingLiteralsUtf8 = [];
                trailingLiteralUtf8 = [];
                return false;
            }
        }

        return true;
    }

    private static void TrySelectOrderedWindowConstraints(
        Utf8TreeNode root,
        byte[][] leadingLiteralsUtf8,
        byte[] trailingLiteralUtf8,
        out int? maxGap,
        out bool sameLine)
    {
        maxGap = null;
        sameLine = false;

        var ordered = UnwrapStructuralNode(root);
        if (ordered is null || ordered.Kind != Utf8TreeNodeKind.Concatenate)
        {
            return;
        }

        var leadingIndex = FindFirstSignificantChildIndex(ordered);
        var trailingIndex = FindLastSignificantChildIndex(ordered);
        if (leadingIndex < 0 || trailingIndex <= leadingIndex)
        {
            return;
        }

        // Middle-node bounds apply only when both selected anchors consume
        // their complete edge nodes. Otherwise an uncounted edge fragment can
        // make a finite gap too short; retain the ordered proof without a cap.
        if (!TryExtractCompleteWindowPhrases(ordered.Child(leadingIndex), out var completeLeading) ||
            !TryExtractAsciiWindowPhrase(ordered.Child(trailingIndex), out var completeTrailing) ||
            !SameAscii(completeTrailing, trailingLiteralUtf8)) return;
        foreach (var phrase in completeLeading)
        {
            var covered = false;
            foreach (var literal in leadingLiteralsUtf8)
                if (SameAscii(phrase, literal)) { covered = true; break; }
            if (!covered) return;
        }

        var totalMaxWidth = 0;
        var excludesNewlines = true;
        for (var i = leadingIndex + 1; i < trailingIndex; i++)
        {
            if (!TryGetFiniteScalarWindowByteWidth(ordered.Child(i), out var childMaxWidth, out var childExcludesNewlines))
            {
                return;
            }

            if (childMaxWidth > int.MaxValue - totalMaxWidth) return;
            totalMaxWidth += childMaxWidth;
            excludesNewlines &= childExcludesNewlines;
        }

        maxGap = totalMaxWidth;
        sameLine = excludesNewlines;

        static bool SameAscii(string text, byte[] bytes)
        {
            if (text.Length != bytes.Length) return false;
            Utf8ConstructionScope.ChargeWork((ulong)text.Length);
            for (var index = 0; index < text.Length; index++) if (text[index] != bytes[index]) return false;
            return true;
        }
    }

    private static bool TrySelectOrderedWindowLeadingLiterals(Utf8OwnedFindFacts findOptimizations, out byte[][] leadingLiteralsUtf8)
    {
        if (findOptimizations.AlternatePrefixes is { Length: > 0 } alternatePrefixes &&
            alternatePrefixes.All(IsAscii) &&
            TryEncodeAlternates(alternatePrefixes, out leadingLiteralsUtf8))
        {
            return true;
        }

        if (TrySelectLeadingLiteral(findOptimizations, out var leadingLiteral) &&
            IsAscii(leadingLiteral))
        {
            leadingLiteralsUtf8 = [EncodeLiteral(leadingLiteral)];
            return true;
        }

        leadingLiteralsUtf8 = [];
        return false;
    }

    private static bool TryExtractLeadingWindowPhrases(Utf8TreeNode root, out string[] phrases)
    {
        var leading = GetLeadingWindowNode(root);
        if (leading is null)
        {
            phrases = [];
            return false;
        }

        return TryExtractCompleteWindowPhrases(leading, out phrases);
    }

    private static bool TryExtractCompleteWindowPhrases(Utf8TreeNode leading, out string[] phrases)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;

        using (var wrappers = Utf8ConstructionFrames<Utf8TreeNode>.Create(budget, leading))
        {
            while (leading.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && leading.ChildCount == 1)
            {
                budget.ChargeWork(1);
                leading = leading.Child(0);
                wrappers.Push(leading);
            }
        }

        if (leading.Kind == Utf8TreeNodeKind.Alternate)
        {
            using var values = Utf8ConstructionBuffer<string>.Create(budget);
            using var seen = Utf8ConstructionMap<string, bool>.Create(budget, StringComparer.Ordinal);
            for (var i = 0; i < leading.ChildCount; i++)
            {
                budget.ChargeWork(1);
                if (!TryExtractAsciiWindowPhrase(leading.Child(i), out var phrase))
                {
                    phrases = [];
                    return false;
                }
                // Every alternative is part of the covering disjunction,
                // including punctuation-only phrases. Deduplicate whole values
                // in source order without filtering out a possible match.
                if (!seen.ContainsKey(phrase)) { seen[phrase] = true; values.Add(phrase); }
            }
            if (values.Count == 0) { phrases = []; return false; }
            phrases = values.Take();
            construction.Complete();
            return true;
        }

        if (TryExtractAsciiWindowPhrase(leading, out var single))
        {
            phrases = budget.AllocateArray<string>(1, false);
            phrases[0] = single;
            construction.Complete();
            return true;
        }

        phrases = [];
        return false;
    }

    private static Utf8TreeNode? GetLeadingWindowNode(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<Utf8TreeNode>.Create(budget, node);
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && node.ChildCount == 1)
        {
            budget.ChargeWork(1);
            node = node.Child(0);
            frames.Push(node);
        }

        if (node.Kind != Utf8TreeNodeKind.Concatenate)
        {
            return node;
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
            budget.ChargeWork(1);
            var child = node.Child(i);
            if (child.Kind is Utf8TreeNodeKind.Bol or Utf8TreeNodeKind.Eol or Utf8TreeNodeKind.Boundary or Utf8TreeNodeKind.NonBoundary)
            {
                continue;
            }

            return child;
        }

        return null;
    }

    private struct PhraseFrame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int Index;
        internal int Start;
        internal bool Entered;
    }

    private static bool TryExtractAsciiWindowPhrase(Utf8TreeNode node, out string phrase)
    {
        phrase = string.Empty;
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<PhraseFrame>.Create(budget, new(node));
        using var builder = Utf8ConstructionText.Create();
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (!frame.Entered)
            {
                node = frame.Node;
                if (TryGetLiteralText(node, out var literal) && IsAscii(literal))
                {
                    budget.ChargeWork((ulong)literal.Length);
                    builder.Append(literal);
                    frames.Pop();
                    continue;
                }
                // A whitespace predicate is not one literal space. Optional,
                // tab or Unicode whitespace cannot certify an exact phrase.
                if (node.Kind != Utf8TreeNodeKind.Concatenate &&
                    !(node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && node.ChildCount == 1)) return false;
                frame.Node = node;
                frame.Start = builder.Length;
                frame.Entered = true;
            }
            node = frame.Node;
            if (frame.Index == node.ChildCount)
            {
                // Each suspended concatenation must contribute a nonempty phrase.
                if (builder.Length == frame.Start) return false;
                frames.Pop();
                continue;
            }
            var child = node.Child(frame.Index++);
            frames[index] = frame;
            if (child.Kind is not (Utf8TreeNodeKind.Bol or Utf8TreeNodeKind.Eol or Utf8TreeNodeKind.Boundary or Utf8TreeNodeKind.NonBoundary))
                frames.Push(new(child));
        }
        if (builder.Length == 0) return false;
        phrase = builder.ToText();
        construction.Complete();
        return true;
    }

    private static bool IsWhitespaceLoop(Utf8TreeNode node)
    {
        if (node.Kind is not (Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Setloopatomic or Utf8TreeNodeKind.Set))
        {
            return false;
        }

        return Utf8ScalarAsciiProjector.IsPositiveWhitespace(node.ScalarClass);
    }

    private static int FindFirstSignificantChildIndex(Utf8TreeNode node)
    {
        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!IsStructuralAnchor(node.Child(i)))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindLastSignificantChildIndex(Utf8TreeNode node)
    {
        for (var i = node.ChildCount - 1; i >= 0; i--)
        {
            if (!IsStructuralAnchor(node.Child(i)))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsStructuralAnchor(Utf8TreeNode node)
    {
        return node.Kind is Utf8TreeNodeKind.Bol or Utf8TreeNodeKind.Eol or Utf8TreeNodeKind.Boundary or Utf8TreeNodeKind.NonBoundary or Utf8TreeNodeKind.Beginning or Utf8TreeNodeKind.Start or Utf8TreeNodeKind.End or Utf8TreeNodeKind.EndZ;
    }

    private struct LineBreakFrame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int Index;
        internal int Total;
        internal bool Waiting;
    }

    // Window distances are bytes even when atoms fold to different scalar
    // widths. Reuse the shared bound proof; do not maintain another formula.
    private static bool TryGetFiniteScalarWindowByteWidth(Utf8TreeNode node, out int maxWidth, out bool excludesNewlines)
    {
        maxWidth = 0;
        excludesNewlines = false;
        using var construction = Utf8ConstructionScope.Enter();
        var width = Utf8OwnedFindOptimizations.ComputeByteWidth(node);
        if (width.Maximum is not int maximum) return false;
        excludesNewlines = maximum == 0 ||
            TryGetFiniteLineBreakCount(node, out var lineBreaks) && lineBreaks == 0;
        maxWidth = maximum;
        return true;
    }

    private static bool TryGetFiniteLineBreakCount(Utf8TreeNode node, out int maxLineBreaks)
    {
        maxLineBreaks = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<LineBreakFrame>.Create(budget, new(node));
        var result = false;
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var unwrapped = UnwrapStructuralNode(frame.Node);
            if (unwrapped is null)
            {
                maxLineBreaks = 0;
                result = true;
                frames.Pop();
                continue;
            }
            node = unwrapped;
            if ((node.Kind is Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Alternate && node.ChildCount > 0) ||
                (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop && node.ChildCount == 1 && node.N >= 0 && node.N != int.MaxValue))
            {
                if (frame.Waiting)
                {
                    if (!result)
                    {
                        maxLineBreaks = 0;
                        frames.Pop();
                        continue;
                    }
                    if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop)
                    {
                        maxLineBreaks = checked(node.N * maxLineBreaks);
                        frames.Pop();
                        continue;
                    }
                    frame.Total = node.Kind == Utf8TreeNodeKind.Concatenate
                        ? frame.Total + maxLineBreaks : Math.Max(frame.Total, maxLineBreaks);
                    if (frame.Index == node.ChildCount)
                    {
                        maxLineBreaks = frame.Total;
                        frames.Pop();
                        continue;
                    }
                }
                frame.Node = node;
                frame.Waiting = true;
                var child = node.Child(frame.Index++);
                frames[index] = frame;
                frames.Push(new(child));
                continue;
            }
            result = ReadScalar(node, out maxLineBreaks);
            frames.Pop();
        }
        return result;

        static bool ExcludesLineBreaks(Utf8ScalarClass? scalarClass) =>
            Utf8ScalarAsciiProjector.TryProjectAsciiQuery(scalarClass, out var mask) &&
            !mask.Contains((byte)'\r') && !mask.Contains((byte)'\n');

        static bool ReadScalar(Utf8TreeNode node, out int maxLineBreaks)
        {
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.Empty:
                case Utf8TreeNodeKind.Bol:
                case Utf8TreeNodeKind.Beginning:
                case Utf8TreeNodeKind.Start:
                case Utf8TreeNodeKind.Eol:
                case Utf8TreeNodeKind.End:
                case Utf8TreeNodeKind.EndZ:
                case Utf8TreeNodeKind.Boundary:
                case Utf8TreeNodeKind.NonBoundary:
                    maxLineBreaks = 0;
                    return true;

                case Utf8TreeNodeKind.One:
                    maxLineBreaks = node.Ch is '\r' or '\n' ? 1 : 0;
                    return true;

                case Utf8TreeNodeKind.Multi when node.Str is { Length: > 0 } literal:
                    Utf8ConstructionScope.ChargeWork((ulong)literal.Length);
                    maxLineBreaks = 0;
                    foreach (var ch in literal) if (ch is '\r' or '\n') maxLineBreaks++;
                    return true;

                case Utf8TreeNodeKind.Set:
                    if (ExcludesLineBreaks(node.ScalarClass))
                    {
                        maxLineBreaks = 0;
                        return true;
                    }

                    break;

                case Utf8TreeNodeKind.Oneloop:
                case Utf8TreeNodeKind.Onelazy:
                case Utf8TreeNodeKind.Oneloopatomic:
                    if (node.N >= 0 && node.N != int.MaxValue)
                    {
                        maxLineBreaks = node.Ch is '\r' or '\n' ? node.N : 0;
                        return true;
                    }

                    break;

                case Utf8TreeNodeKind.Setloop:
                case Utf8TreeNodeKind.Setlazy:
                case Utf8TreeNodeKind.Setloopatomic:
                    if (node.N >= 0 &&
                        node.N != int.MaxValue &&
                        ExcludesLineBreaks(node.ScalarClass))
                    {
                        maxLineBreaks = 0;
                        return true;
                    }

                    break;

            }

            maxLineBreaks = 0;
            return false;
        }
    }

    private static bool IsLeadingAsciiWhitespaceNode(Utf8TreeNode node)
    {
        // The inverse candidate transform only walks six ASCII whitespace
        // bytes. Unicode \s also includes wider scalars; that definition cannot
        // certify distances relative to this transform's requirement base.
        node = UnwrapStructuralNode(node) ?? node;
        return node.Kind is Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Setloopatomic or Utf8TreeNodeKind.Set &&
            Utf8ScalarAsciiProjector.TryProjectPositiveAsciiDefinition(node.ScalarClass, out var mask) &&
            mask.ActualLowMask == 0x100003e00UL && mask.ActualHighMask == 0;
    }

    private static bool TryExtractTrailingLiteral(Utf8TreeNode node, out string literal)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (!frame.Entered)
            {
                if (TryGetLiteralText(frame.Node, out literal)) { construction.Complete(); return true; }
                if (frame.Node.Kind != Utf8TreeNodeKind.Concatenate &&
                    !(frame.Node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && frame.Node.ChildCount == 1)) { frames.Pop(); continue; }
                frame.Index = frame.Node.ChildCount;
                frame.Entered = true;
            }
            if (frame.Index == 0) { frames.Pop(); continue; }
            // Visit the rightmost subtree first, without reserving all siblings.
            var child = frame.Node.Child(--frame.Index);
            frames[index] = frame;
            if (child.Kind is not (Utf8TreeNodeKind.Bol or Utf8TreeNodeKind.Eol or Utf8TreeNodeKind.End or Utf8TreeNodeKind.EndZ or Utf8TreeNodeKind.Boundary or Utf8TreeNodeKind.NonBoundary))
                frames.Push(new(child));
        }
        literal = string.Empty;
        return false;
    }

    private static bool TryGetLiteralText(Utf8TreeNode node, out string literal)
    {
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.One when Utf8OwnedFindOptimizations.IsExactLiteral(node):
                literal = Utf8ConstructionScope.CurrentBudget.CopyText([node.Ch]);
                return true;
            case Utf8TreeNodeKind.Multi when node.Str is { } text && Utf8OwnedFindOptimizations.IsExactLiteral(node):
                literal = text;
                return true;
            default:
                literal = string.Empty;
                return false;
        }
    }

    private static bool TrySelectAlternatePrefixes(Utf8OwnedFindFacts findOptimizations, bool ignoreCase, out byte[][] alternateLiterals)
    {
        alternateLiterals = [];
        if (ignoreCase ||
            findOptimizations.AlternatePrefixes is not { Length: > 1 } alternatePrefixes)
        {
            return false;
        }

        return TryEncodeAlternates(alternatePrefixes, out alternateLiterals);
    }

    private static bool TrySelectTrailingFixedLengthAnchor(Utf8OwnedFindFacts findOptimizations, out Utf8SearchKind kind, out int minRequiredLength)
    {
        kind = Utf8SearchKind.None;
        minRequiredLength = 0;
        if (!findOptimizations.IsFixedLength)
        {
            return false;
        }

        kind = findOptimizations.TrailingAnchor switch
        {
            Utf8TreeNodeKind.End => Utf8SearchKind.TrailingAnchorFixedLengthEnd,
            Utf8TreeNodeKind.EndZ => Utf8SearchKind.TrailingAnchorFixedLengthEndZ,
            _ => Utf8SearchKind.None,
        };
        if (kind == Utf8SearchKind.None)
        {
            return false;
        }

        minRequiredLength = findOptimizations.MinRequiredLength;
        return minRequiredLength > 0;
    }

    private static bool TrySelectFixedDistanceLiteral(
        Utf8OwnedFindFacts findOptimizations,
        bool ignoreCase,
        out byte[] literalUtf8,
        out int distance,
        out int minRequiredLength)
    {
        literalUtf8 = [];
        distance = 0;
        minRequiredLength = 0;
        if (ignoreCase ||
            findOptimizations.FixedDistanceLiteral is not { } fixedDistance ||
            !IsAscii(fixedDistance.Literal) ||
            fixedDistance.Literal.Length <= 1)
        {
            return false;
        }

        literalUtf8 = EncodeLiteral(fixedDistance.Literal);
        distance = fixedDistance.Distance;
        minRequiredLength = findOptimizations.MinRequiredLength;
        return true;
    }

    private static bool TrySelectRequiredLiteralPrefilter(
        Utf8TreeNode root,
        out byte[]? literalUtf8,
        out byte[][]? alternateLiteralsUtf8)
    {
        literalUtf8 = null;
        alternateLiteralsUtf8 = null;

        var requiredFamily = Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(root);
        if (requiredFamily is not { Length: > 0 })
        {
            return false;
        }

        // The producer proves whole-family coverage and complete exact scalars.
        // Strict admitted encoding retains that proof; no replacement encoding.
        if (requiredFamily.Length == 1)
        {
            literalUtf8 = EncodeLiteral(requiredFamily[0]);
            return literalUtf8.Length > 0;
        }

        return TryEncodeAlternates(requiredFamily, out alternateLiteralsUtf8);
    }

    private static bool TrySelectDeterministicPrefixSets(
        Utf8TreeNode root,
        int minRequiredLength,
        Utf8FallbackStartTransform fallbackStartTransform,
        out Utf8FixedDistanceSet[] fixedDistanceSets,
        out int selectedMinRequiredLength)
    {
        fixedDistanceSets = [];
        selectedMinRequiredLength = 0;

        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var rootFrames = Utf8ConstructionFrames<Utf8TreeNode>.Create(budget, root);

        if (fallbackStartTransform.Kind == Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace &&
            !TrySelectFallbackStartTransform(root, out _)) return false;

        var prefix = fallbackStartTransform.Kind == Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace
            ? SkipLeadingAsciiWhitespacePrefix(root)
            : (Node: root, StartIndex: 0);
        if (prefix.Node is null ||
            !TryExtractDeterministicPrefixSets(prefix.Node, prefix.StartIndex, out var extractedSets) ||
            extractedSets.Length < 3 ||
            !TryChoosePrimaryDeterministicSet(extractedSets, out var primaryIndex) ||
            primaryIndex <= 0)
        {
            return false;
        }

        // The admitted extracted payload becomes the selected payload. Moving
        // the primary entry preserves the order of every remaining constraint.
        var primary = extractedSets[primaryIndex];
        for (var i = primaryIndex; i > 0; i--)
        {
            budget.ChargeWork(1);
            extractedSets[i] = extractedSets[i - 1];
        }
        extractedSets[0] = primary;
        fixedDistanceSets = extractedSets;

        selectedMinRequiredLength = minRequiredLength;
        construction.Complete();
        return true;
    }

    private static bool TrySelectFixedDistanceSets(
        Utf8OwnedFindFacts findOptimizations,
        bool ignoreCase,
        out Utf8FixedDistanceSet[] fixedDistanceSets,
        out int minRequiredLength)
    {
        fixedDistanceSets = [];
        minRequiredLength = 0;
        if (ignoreCase ||
            findOptimizations.FixedDistanceSets is not { Length: > 0 } runtimeSets)
        {
            return false;
        }

        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        fixedDistanceSets = budget.AllocateArray<Utf8FixedDistanceSet>(runtimeSets.Length, false);
        for (var index = 0; index < runtimeSets.Length; index++)
        {
            budget.ChargeWork(1);
            var set = runtimeSets[index];
            fixedDistanceSets[index] = Utf8FixedDistanceSet.FromMask(set.Distance, set.CharClass);
        }
        construction.Complete();

        minRequiredLength = findOptimizations.MinRequiredLength;
        return true;
    }

    private static (Utf8TreeNode? Node, int StartIndex) SkipLeadingAsciiWhitespacePrefix(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<Utf8TreeNode>.Create(budget, node);
        var unwrapped = Unwrap(node);
        if (unwrapped is null || unwrapped.Kind != Utf8TreeNodeKind.Concatenate)
        {
            return (unwrapped, 0);
        }

        node = unwrapped;
        var startIndex = 0;
        while (startIndex < node.ChildCount &&
               node.Child(startIndex).Kind is Utf8TreeNodeKind.Bol or Utf8TreeNodeKind.Eol or Utf8TreeNodeKind.Boundary or Utf8TreeNodeKind.NonBoundary or Utf8TreeNodeKind.Beginning or Utf8TreeNodeKind.Start)
        {
            Utf8ConstructionScope.ChargeWork(1);
            startIndex++;
        }

        Utf8TreeNode? firstContent = null;
        if (startIndex < node.ChildCount)
        {
            firstContent = node.Child(startIndex);
            frames.Push(firstContent);
            firstContent = Unwrap(firstContent);
        }
        if (firstContent is null || !(IsWhitespaceLoop(firstContent) || IsLeadingAsciiWhitespaceNode(firstContent)))
        {
            return (node, 0);
        }

        startIndex++;
        while (startIndex < node.ChildCount &&
               node.Child(startIndex).Kind is Utf8TreeNodeKind.Bol or Utf8TreeNodeKind.Eol or Utf8TreeNodeKind.Boundary or Utf8TreeNodeKind.NonBoundary or Utf8TreeNodeKind.Beginning or Utf8TreeNodeKind.Start)
        {
            Utf8ConstructionScope.ChargeWork(1);
            startIndex++;
        }

        if (startIndex >= node.ChildCount)
        {
            return (null, 0);
        }

        if (startIndex == node.ChildCount - 1)
        {
            return (node.Child(startIndex), 0);
        }

        // Borrow the suffix without allocating a node/array or reparenting it.
        return (node, startIndex);

        Utf8TreeNode Unwrap(Utf8TreeNode value)
        {
            while (value.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && value.ChildCount == 1)
            {
                budget.ChargeWork(1);
                value = value.Child(0);
                frames.Push(value);
            }
            return value;
        }
    }

    // The tree keeps logical subject order in both execution directions. Stop
    // the whole walk at the first unproved width: an inner group's partial
    // prefix never certifies the distance of its following siblings.
    private static bool TryExtractDeterministicPrefixSets(Utf8TreeNode node, int startIndex, out Utf8FixedDistanceSet[] fixedDistanceSets)
    {
        fixedDistanceSets = [];
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(budget, new(node) { Index = startIndex });
        using var sets = Utf8ConstructionBuffer<Utf8FixedDistanceSet>.Create(budget);
        var distance = 0;
        while (frames.Count != 0 && sets.Count < MaxDeterministicPrefixSets)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = frame.Node;
            if (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && node.ChildCount == 1 ||
                node.Kind == Utf8TreeNodeKind.Concatenate)
            {
                if (frame.Index < 0 || frame.Index > node.ChildCount) break;
                if (frame.Index == node.ChildCount) { frames.Pop(); continue; }
                var child = node.Child(frame.Index++);
                frames[index] = frame;
                frames.Push(new(child));
                continue;
            }
            if (!AppendAtom(node)) break;
            frames.Pop();
        }
        if (sets.Count == 0) return false;
        fixedDistanceSets = sets.Take();
        construction.Complete();
        return true;

        bool AppendAtom(Utf8TreeNode atom)
        {
            if (atom.Kind == Utf8TreeNodeKind.One) return AppendScalar(atom.Ch, atom);
            if (atom.Kind == Utf8TreeNodeKind.Multi && atom.Str is { } literal)
            {
                // Admission bounds the pattern scan; UTF-16 is only the pattern
                // text representation. Each decoded scalar consumes its UTF-8 width.
                budget.ChargeWork((ulong)literal.Length);
                for (var offset = 0; offset < literal.Length;)
                {
                    if (sets.Count == MaxDeterministicPrefixSets) return false;
                    if (Rune.DecodeFromUtf16(literal.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done ||
                        !AppendScalar(scalar.Value, atom)) return false;
                    offset += consumed;
                }
                return true;
            }
            if (atom.Kind == Utf8TreeNodeKind.Set &&
                Utf8ScalarAsciiProjector.TryProjectPositiveAsciiDefinition(atom.ScalarClass, out var definition) &&
                Utf8ScalarAsciiProjector.TryCloseAsciiCaseFiber(definition, atom.MatchCaseInsensitive, atom.Options, out var mask))
            {
                if (distance == int.MaxValue) return false;
                sets.Add(Utf8FixedDistanceSet.FromMask(distance, mask));
                distance++;
                return true;
            }
            return Advance(Utf8OwnedFindOptimizations.ComputeByteWidth(atom));
        }

        bool AppendScalar(int scalar, Utf8TreeNode atom)
        {
            if (scalar < 128 &&
                Utf8ScalarAsciiProjector.TryCloseAsciiCaseFiber(AsciiCharClass.ForByte((byte)scalar),
                    atom.MatchCaseInsensitive, atom.Options, out var mask))
            {
                if (distance == int.MaxValue) return false;
                sets.Add(Utf8FixedDistanceSet.FromMask(distance, mask));
                distance++;
                return true;
            }
            return Advance(Utf8OwnedFindOptimizations.ComputeScalarByteWidth(scalar, atom.MatchCaseInsensitive, atom.Options));
        }

        bool Advance(Utf8OwnedFindOptimizations.ByteWidth width)
        {
            if (!width.IsFixed || width.Minimum > int.MaxValue - distance) return false;
            distance += width.Minimum;
            return true;
        }
    }

    private static bool TryChoosePrimaryDeterministicSet(Utf8FixedDistanceSet[] extractedSets, out int primaryIndex)
    {
        primaryIndex = -1;
        var bestScore = long.MinValue;
        for (var i = 0; i < extractedSets.Length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var score = ScoreDeterministicPrimarySet(extractedSets[i]);
            if (score > bestScore)
            {
                bestScore = score;
                primaryIndex = i;
            }
        }
        return primaryIndex >= 0;
    }

    private static long ScoreDeterministicPrimarySet(Utf8FixedDistanceSet set)
    {
        var cardinality = set.ByteSet.Count;
        if (cardinality == 0) return long.MinValue;
        var rarity = int.MinValue;
        var hasWord = false;
        var onlyWhitespace = true;
        for (var value = 0; value < 128; value++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!set.Contains((byte)value)) continue;
            rarity = Math.Max(rarity, Utf8AsciiSearchHeuristics.GetAnchorRarityScore((byte)value));
            hasWord |= Utf8AsciiBytePredicates.IsWord((byte)value);
            onlyWhitespace &= Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value);
        }
        return (long)rarity * 100 + (long)set.Distance * 10 - cardinality * 10 +
            (hasWord ? 500 : 0) - (onlyWhitespace ? 2000 : 0);
    }

    private static Utf8TreeNode? UnwrapStructuralNode(Utf8TreeNode? node)
    {
        while (node is not null &&
               node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic &&
               node.ChildCount == 1)
        {
            Utf8ConstructionScope.ChargeWork(1);
            node = node.Child(0);
        }

        return node;
    }


    private static bool IsAsciiWordByte(byte value)
    {
        return Utf8AsciiBytePredicates.IsWord(value);
    }

    private static bool TryEncodeAlternates(string[] alternatePrefixes, out byte[][] alternateLiterals)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        alternateLiterals = budget.AllocateArray<byte[]>(alternatePrefixes.Length, false);
        for (var i = 0; i < alternatePrefixes.Length; i++)
        {
            budget.ChargeWork(1);
            if (alternatePrefixes[i].Length == 0)
            {
                alternateLiterals = [];
                return false;
            }

            alternateLiterals[i] = budget.EncodeUtf8(alternatePrefixes[i]);
        }
        construction.Complete();
        return true;
    }

    // Strict scalar validation/counting precedes admitted byte acquisition.
    // This materializes pattern metadata, never a decoded subject.
    private static byte[] EncodeLiteral(string literal)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var result = construction.Budget.EncodeUtf8(literal);
        construction.Complete();
        return result;
    }

    private static bool IsAscii(string value)
    {
        foreach (var ch in value)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (ch > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

}
