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

        TrySelectRequiredLiteralPrefilter(findOptimizations.Root, out var requiredPrefilterLiteral, out var requiredPrefilterAlternateLiterals);
        TrySelectQuotedAsciiRunPrefilter(runtimeTree.Root, out var secondaryRequiredPrefilterQuotedAsciiClass, out var secondaryRequiredPrefilterQuotedAsciiLength);
        var secondaryRequiredPrefilterQuotedAsciiSet = CreateAsciiByteSet(secondaryRequiredPrefilterQuotedAsciiClass);
        TrySelectRequiredWindowPrefilters(
            runtimeTree.Root,
            requiredPrefilterAlternateLiterals,
            secondaryRequiredPrefilterQuotedAsciiSet,
            secondaryRequiredPrefilterQuotedAsciiLength,
            out var requiredWindowPrefilters);
        TrySelectOrderedAsciiWindow(runtimeTree.Root, findOptimizations, UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions), out var orderedLeadingLiterals, out var orderedTrailingLiteral);
        TrySelectOrderedWindowConstraints(runtimeTree.Root, out var orderedWindowMaxGap, out var orderedWindowSameLine);
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
                           LiteralUtf8 = Encoding.UTF8.GetBytes(leadingLiteral),
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
                           LiteralUtf8 = Encoding.UTF8.GetBytes(candidate),
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

    private static bool TrySelectQuotedAsciiRunPrefilter(Utf8TreeNode root, out string? asciiSet, out int runLength)
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

    private static bool TryExtractWindowAnchor(Utf8TreeNode node, out Utf8PreparedSearcherInfo searcherInfo)
    {
        node = UnwrapStructuralNode(node) ?? node;
        searcherInfo = default;

        if (TryFindQuotedAsciiRun(node, out var asciiSet, out var runLength))
        {
            searcherInfo = Utf8PreparedSearcherInfo.QuotedAsciiRun(CreateAsciiByteSet(asciiSet), runLength);
            return true;
        }

        var family = Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(node);
        if (family is { Length: > 0 } &&
            family.All(IsAscii) &&
            TryEncodeAlternates(family, out var alternateLiteralsUtf8))
        {
            searcherInfo = Utf8PreparedSearcherInfo.LiteralFamily(alternateLiteralsUtf8);
            return true;
        }

        return false;
    }

    private struct ScanFrame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int Index;
        internal bool Entered;
    }

    private static bool TryFindQuotedAsciiRun(Utf8TreeNode node, out string? asciiSet, out int runLength)
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

    private static Utf8SearchAsciiSet CreateAsciiByteSet(string? regexCharClass)
    {
        if (string.IsNullOrEmpty(regexCharClass))
        {
            return default;
        }

        var result = default(Utf8SearchAsciiSet);
        for (var value = 0; value < 128; value++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (Utf8OwnedCharClass.CharInClassBase((char)value, regexCharClass))
            {
                result = result.With((byte)value);
            }
        }

        return result;
    }

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

    private static bool TryGetExactAsciiSetLength(Utf8TreeNode node, out string? asciiSet, out int runLength)
    {
        node = UnwrapStructuralNode(node) ?? node;
        asciiSet = null;
        runLength = 0;

        if (node.Kind is Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setloopatomic &&
            node.Str is { Length: > 0 } set &&
            node.M > 0 &&
            node.M == node.N &&
            Utf8OwnedCharClass.IsAscii(set))
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
                child.Str is { Length: > 0 } childSet &&
                Utf8OwnedCharClass.IsAscii(childSet))
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

        if (simplePatternFacts?.AlternatePrefixes is { Length: > 0 } alternatePrefixes)
        {
            var longest = string.Empty;
            foreach (var prefix in alternatePrefixes)
            {
                if (prefix.Length > longest.Length)
                {
                    longest = prefix;
                }
            }

            if (longest.Length > 0)
            {
                return Utf8SearchFacts.Create(
                           simplePatternPlan.IgnoreCase ? Utf8SearchKind.AsciiFoldedByteLiteral : Utf8SearchKind.ExactAsciiLiteral,
                           new Utf8SearchFactData
                           {
                               LiteralUtf8 = Encoding.UTF8.GetBytes(longest),
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
                           LiteralUtf8 = Encoding.UTF8.GetBytes(literal),
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
        if (findOptimizations.AlternatePrefixes is { Length: > 0 } alternatePrefixes)
        {
            candidate = string.Empty;
            foreach (var prefix in alternatePrefixes)
            {
                if (prefix.Length > candidate.Length)
                {
                    candidate = prefix;
                }
            }

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

        if (((!TryExtractLeadingWindowPhrases(root, out var leadingPhrases) ||
              leadingPhrases.Length == 0 ||
              !TryEncodeAlternates(leadingPhrases, out leadingLiteralsUtf8)) &&
             !TrySelectOrderedWindowLeadingLiterals(findOptimizations, out leadingLiteralsUtf8)) ||
            !TryExtractTrailingLiteral(root, out var trailingLiteral) ||
            !IsAscii(trailingLiteral))
        {
            leadingLiteralsUtf8 = [];
            trailingLiteralUtf8 = [];
            return false;
        }

        trailingLiteralUtf8 = Encoding.UTF8.GetBytes(trailingLiteral);
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

        if (!TryExtractTrailingLiteral(ordered.Child(trailingIndex), out var trailingLiteral) ||
            !IsAscii(trailingLiteral) ||
            trailingLiteral.Length == 0)
        {
            return;
        }

        var totalMaxWidth = 0;
        var excludesNewlines = true;
        for (var i = leadingIndex + 1; i < trailingIndex; i++)
        {
            if (!TryGetFiniteAsciiWindowWidth(ordered.Child(i), out var childMaxWidth, out var childExcludesNewlines))
            {
                return;
            }

            totalMaxWidth += childMaxWidth;
            excludesNewlines &= childExcludesNewlines;
        }

        maxGap = totalMaxWidth;
        sameLine = excludesNewlines;
    }

    private static bool TrySelectOrderedWindowLeadingLiterals(Utf8OwnedFindFacts findOptimizations, out byte[][] leadingLiteralsUtf8)
    {
        if (findOptimizations.AlternatePrefixes is { Length: > 1 } alternatePrefixes &&
            alternatePrefixes.All(IsAscii) &&
            TryEncodeAlternates(alternatePrefixes, out leadingLiteralsUtf8))
        {
            return true;
        }

        if (TrySelectLeadingLiteral(findOptimizations, out var leadingLiteral) &&
            IsAscii(leadingLiteral))
        {
            leadingLiteralsUtf8 = [Encoding.UTF8.GetBytes(leadingLiteral)];
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

        while (leading.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && leading.ChildCount == 1)
        {
            leading = leading.Child(0);
        }

        if (leading.Kind == Utf8TreeNodeKind.Alternate)
        {
            var values = new string[leading.ChildCount];
            for (var i = 0; i < leading.ChildCount; i++)
            {
                if (!TryExtractAsciiWindowPhrase(leading.Child(i), out values[i]))
                {
                    phrases = [];
                    return false;
                }
            }

            phrases = values
                .Where(ContainsAsciiWordChar)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return phrases.Length > 0;
        }

        if (TryExtractAsciiWindowPhrase(leading, out var single) && ContainsAsciiWordChar(single))
        {
            phrases = [single];
            return true;
        }

        phrases = [];
        return false;
    }

    private static Utf8TreeNode? GetLeadingWindowNode(Utf8TreeNode node)
    {
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && node.ChildCount == 1)
        {
            node = node.Child(0);
        }

        if (node.Kind != Utf8TreeNodeKind.Concatenate)
        {
            return node;
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
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
        var builder = new StringBuilder();
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (!frame.Entered)
            {
                node = UnwrapStructuralNode(frame.Node) ?? frame.Node;
                if (TryGetLiteralText(node, out var literal) && IsAscii(literal))
                {
                    budget.ChargeWork((ulong)literal.Length);
                    builder.Append(literal);
                    frames.Pop();
                    continue;
                }
                if (IsWhitespaceLoop(node))
                {
                    builder.Append(' ');
                    frames.Pop();
                    continue;
                }
                if (node.Kind != Utf8TreeNodeKind.Concatenate) return false;
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
        phrase = builder.ToString();
        return phrase.Length > 0;
    }

    private static bool IsWhitespaceLoop(Utf8TreeNode node)
    {
        if (node.Kind is not (Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Setloopatomic or Utf8TreeNodeKind.Set))
        {
            return false;
        }

        return node.Str == Utf8OwnedCharClass.SpaceClass || node.Str == Utf8OwnedCharClass.ECMASpaceClass;
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

    private struct WidthFrame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int Index;
        internal int Total;
        internal bool Excludes = true;
        internal bool Waiting;
    }

    private static bool TryGetFiniteAsciiWindowWidth(Utf8TreeNode node, out int maxWidth, out bool excludesNewlines)
    {
        maxWidth = 0;
        excludesNewlines = false;
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<WidthFrame>.Create(budget, new(node));
        var result = false;
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var unwrapped = UnwrapStructuralNode(frame.Node);
            if (unwrapped is null)
            {
                maxWidth = 0;
                excludesNewlines = true;
                result = true;
                frames.Pop();
                continue;
            }
            node = unwrapped;
            if ((node.Kind is Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Alternate && node.ChildCount > 0) ||
                (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop && node.ChildCount == 1))
            {
                if (frame.Waiting)
                {
                    if (!result)
                    {
                        maxWidth = 0;
                        excludesNewlines = false;
                        frames.Pop();
                        continue;
                    }
                    if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop)
                    {
                        result = TryGetFiniteRepeatedWidth(node.N, maxWidth, excludesNewlines, out maxWidth, out excludesNewlines);
                        frames.Pop();
                        continue;
                    }
                    frame.Total = node.Kind == Utf8TreeNodeKind.Concatenate
                        ? frame.Total + maxWidth : Math.Max(frame.Total, maxWidth);
                    frame.Excludes &= excludesNewlines;
                    if (frame.Index == node.ChildCount)
                    {
                        maxWidth = frame.Total;
                        excludesNewlines = frame.Excludes;
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
            result = ReadScalar(node, out maxWidth, out excludesNewlines);
            frames.Pop();
        }
        return result;

        static bool ReadScalar(Utf8TreeNode node, out int maxWidth, out bool excludesNewlines)
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
                    maxWidth = 0;
                    excludesNewlines = true;
                    return true;

                case Utf8TreeNodeKind.One:
                    maxWidth = Encoding.UTF8.GetByteCount([node.Ch]);
                    excludesNewlines = node.Ch is not ('\r' or '\n');
                    return true;

                case Utf8TreeNodeKind.Multi when node.Str is { Length: > 0 } literal:
                    Utf8ConstructionScope.ChargeWork(2 * (ulong)literal.Length);
                    maxWidth = Encoding.UTF8.GetByteCount(literal);
                    excludesNewlines = literal.IndexOfAny(['\r', '\n']) < 0;
                    return true;

                case Utf8TreeNodeKind.Set when node.Str is { Length: > 0 } set:
                    if (!TryGetAsciiSetWidthAndLineSafety(set, out var setWidth, out var setExcludesNewlines))
                    {
                        break;
                    }

                    maxWidth = setWidth;
                    excludesNewlines = setExcludesNewlines;
                    return true;

                case Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Onelazy or Utf8TreeNodeKind.Oneloopatomic:
                    return TryGetFiniteRepeatedWidth(node.N, Encoding.UTF8.GetByteCount([node.Ch]), node.Ch is not ('\r' or '\n'), out maxWidth, out excludesNewlines);

                case Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Setloopatomic:
                    if (node.Str is not { Length: > 0 } repeatedSet ||
                        !TryGetAsciiSetWidthAndLineSafety(repeatedSet, out var repeatedSetWidth, out var repeatedSetExcludesNewlines))
                    {
                        break;
                    }

                    return TryGetFiniteRepeatedWidth(
                        node.N,
                        repeatedSetWidth,
                        repeatedSetExcludesNewlines,
                        out maxWidth,
                        out excludesNewlines);

            }

            maxWidth = 0;
            excludesNewlines = false;
            return false;
        }
    }

    private static bool TryGetFiniteLineBreakCount(Utf8TreeNode node, out int maxLineBreaks)
    {
        maxLineBreaks = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<WidthFrame>.Create(budget, new(node));
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
                    if (node.Str is { Length: > 0 } set &&
                        !Utf8OwnedCharClass.CharInClass('\r', set) &&
                        !Utf8OwnedCharClass.CharInClass('\n', set))
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
                        node.Str is { Length: > 0 } repeatedSet &&
                        !Utf8OwnedCharClass.CharInClass('\r', repeatedSet) &&
                        !Utf8OwnedCharClass.CharInClass('\n', repeatedSet))
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

    private static bool TryGetFiniteRepeatedWidth(int maxRepetitions, int unitWidth, bool excludesNewline, out int maxWidth, out bool excludesNewlines)
    {
        if (maxRepetitions < 0 || maxRepetitions == int.MaxValue)
        {
            maxWidth = 0;
            excludesNewlines = false;
            return false;
        }

        try
        {
            maxWidth = checked(maxRepetitions * unitWidth);
            excludesNewlines = excludesNewline;
            return true;
        }
        catch (OverflowException)
        {
            maxWidth = 0;
            excludesNewlines = false;
            return false;
        }
    }

    private static bool TryGetAsciiSetWidthAndLineSafety(string set, out int width, out bool excludesNewlines)
    {
        if (Utf8OwnedCharClass.IsAscii(set))
        {
            width = 1;
            excludesNewlines = !Utf8OwnedCharClass.CharInClass('\r', set) &&
                !Utf8OwnedCharClass.CharInClass('\n', set);
            return true;
        }

        if (Utf8OwnedCharClass.TryGetSingleRange(set, out var first, out var last) &&
            last <= 0x7F)
        {
            width = 1;
            excludesNewlines = !ContainsNewlineRange(first, last);
            return true;
        }

        if (Utf8OwnedCharClass.TryGetDoubleRange(set, out var firstRange, out var secondRange) &&
            secondRange.Last <= 0x7F)
        {
            width = 1;
            excludesNewlines = !ContainsNewlineRange(firstRange.First, firstRange.Last) &&
                !ContainsNewlineRange(secondRange.First, secondRange.Last);
            return true;
        }

        width = 0;
        excludesNewlines = false;
        return false;
    }

    private static bool ContainsNewlineRange(char first, char last) =>
        (first <= '\r' && '\r' <= last) || (first <= '\n' && '\n' <= last);

    private static bool IsLeadingAsciiWhitespaceNode(Utf8TreeNode node)
    {
        return IsWhitespaceLoop(UnwrapStructuralNode(node) ?? node);
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
                frame.Node = UnwrapStructuralNode(frame.Node) ?? frame.Node;
                if (TryGetLiteralText(frame.Node, out literal)) return true;
                if (frame.Node.Kind != Utf8TreeNodeKind.Concatenate) { frames.Pop(); continue; }
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
            case Utf8TreeNodeKind.One:
                literal = node.Ch.ToString();
                return true;
            case Utf8TreeNodeKind.Multi when !string.IsNullOrEmpty(node.Str):
                literal = node.Str;
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

        literalUtf8 = Encoding.UTF8.GetBytes(fixedDistance.Literal);
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

        // A lone surrogate UTF-8-encodes to the replacement character, which can
        // never witness the intended literal in well-formed input (and collides
        // with a genuine U+FFFD): only well-formed literals may arm byte search,
        // e.g. BCL extracts the lone high half from an ungrouped astral repeat.
        foreach (var candidate in requiredFamily)
        {
            for (var i = 0; i < candidate.Length; i++)
            {
                if (char.IsHighSurrogate(candidate, i))
                {
                    if (i + 1 >= candidate.Length || !char.IsLowSurrogate(candidate, i + 1))
                    {
                        return false;
                    }

                    i++;
                }
                else if (char.IsLowSurrogate(candidate, i))
                {
                    return false;
                }
            }
        }

        if (requiredFamily.Length == 1)
        {
            literalUtf8 = Encoding.UTF8.GetBytes(requiredFamily[0]);
            return literalUtf8.Length > 0;
        }

        alternateLiteralsUtf8 = new byte[requiredFamily.Length][];
        for (var i = 0; i < requiredFamily.Length; i++)
        {
            alternateLiteralsUtf8[i] = Encoding.UTF8.GetBytes(requiredFamily[i]);
            if (alternateLiteralsUtf8[i].Length == 0)
            {
                alternateLiteralsUtf8 = null;
                return false;
            }
        }

        return true;
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

        fixedDistanceSets = new Utf8FixedDistanceSet[extractedSets.Length];
        fixedDistanceSets[0] = extractedSets[primaryIndex];
        var destination = 1;
        for (var i = 0; i < extractedSets.Length; i++)
        {
            if (i == primaryIndex)
            {
                continue;
            }

            fixedDistanceSets[destination++] = extractedSets[i];
        }

        selectedMinRequiredLength = minRequiredLength;
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

        fixedDistanceSets = new Utf8FixedDistanceSet[runtimeSets.Length];
        for (var i = 0; i < runtimeSets.Length; i++)
        {
            var set = runtimeSets[i];
            byte[]? chars = null;
            if (set.Chars is { Length: > 0 } setChars)
            {
                chars = new byte[setChars.Length];
                for (var j = 0; j < setChars.Length; j++)
                {
                    if (setChars[j] > 0x7F)
                    {
                        fixedDistanceSets = [];
                        return false;
                    }

                    chars[j] = (byte)setChars[j];
                }
            }

            byte rangeLow = 0;
            byte rangeHigh = 0;
            var hasRange = false;
            if (set.Range is { } range)
            {
                if (range.LowInclusive > 0x7F || range.HighInclusive > 0x7F)
                {
                    fixedDistanceSets = [];
                    return false;
                }

                rangeLow = (byte)range.LowInclusive;
                rangeHigh = (byte)range.HighInclusive;
                hasRange = true;
            }

            fixedDistanceSets[i] = hasRange
                ? Utf8FixedDistanceSet.FromBytesAndRange(set.Distance, chars, set.Negated, rangeLow, rangeHigh)
                : Utf8FixedDistanceSet.FromBytes(set.Distance, chars, set.Negated);
        }

        minRequiredLength = findOptimizations.MinRequiredLength;
        return true;
    }

    private static (Utf8TreeNode? Node, int StartIndex) SkipLeadingAsciiWhitespacePrefix(Utf8TreeNode node)
    {
        var unwrapped = UnwrapStructuralNode(node);
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

        var firstContent = startIndex < node.ChildCount ? UnwrapStructuralNode(node.Child(startIndex)) : null;
        if (firstContent is null || !IsWhitespaceLoop(firstContent))
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
    }

    private static bool TryExtractDeterministicPrefixSets(Utf8TreeNode node, int startIndex, out Utf8FixedDistanceSet[] fixedDistanceSets)
    {
        var collectedSets = new List<Utf8FixedDistanceSet>();
        var offset = 0;
        if (!TryAppendDeterministicPrefixSets(node, collectedSets, ref offset, startIndex) || collectedSets.Count == 0)
        {
            fixedDistanceSets = [];
            return false;
        }

        fixedDistanceSets = [.. collectedSets];
        return true;
    }

    private static bool TryAppendDeterministicPrefixSets(Utf8TreeNode node, List<Utf8FixedDistanceSet> fixedDistanceSets, ref int offset, int startIndex)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(budget, new(node) { Index = startIndex });
        var result = false;
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = UnwrapStructuralNode(frame.Node) ?? frame.Node;
            if (node.Kind == Utf8TreeNodeKind.Concatenate && node.ChildCount > 0)
            {
                if (frame.Entered && !result)
                {
                    result = fixedDistanceSets.Count > 0;
                    frames.Pop();
                    continue;
                }
                if (frame.Index == node.ChildCount) { frames.Pop(); continue; }
                frame.Node = node;
                frame.Entered = true;
                var child = node.Child(frame.Index++);
                frames[index] = frame;
                frames.Push(new(child));
                continue;
            }
            result = ReadScalar(node, fixedDistanceSets, ref offset);
            frames.Pop();
        }
        return result;

        static bool ReadScalar(Utf8TreeNode node, List<Utf8FixedDistanceSet> fixedDistanceSets, ref int offset)
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
                    return true;

                case Utf8TreeNodeKind.One when node.Ch <= 0x7F:
                    return TryAddDeterministicPrefixSet(fixedDistanceSets, Utf8FixedDistanceSet.FromBytes(offset++, [(byte)node.Ch], negated: false));

                case Utf8TreeNodeKind.Multi when node.Str is { Length: > 0 } literal:
                    foreach (var ch in literal)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        if (ch > 0x7F || !TryAddDeterministicPrefixSet(fixedDistanceSets, Utf8FixedDistanceSet.FromBytes(offset++, [(byte)ch], negated: false)))
                        {
                            return false;
                        }
                    }

                    return true;

                case Utf8TreeNodeKind.Set when node.Str is { Length: > 0 } set && Utf8OwnedCharClass.IsAscii(set):
                    return TryCreateAsciiSet(set, offset++, out var fixedDistanceSet) &&
                        TryAddDeterministicPrefixSet(fixedDistanceSets, fixedDistanceSet);

                case Utf8TreeNodeKind.Concatenate:
                    return true;

                default:
                    return false;
            }
        }
    }

    private static bool TryChoosePrimaryDeterministicSet(Utf8FixedDistanceSet[] extractedSets, out int primaryIndex)
    {
        primaryIndex = -1;
        var bestScore = int.MinValue;
        for (var i = 0; i < extractedSets.Length; i++)
        {
            var score = ScoreDeterministicPrimarySet(extractedSets[i]);
            if (score > bestScore)
            {
                bestScore = score;
                primaryIndex = i;
            }
        }

        return primaryIndex >= 0;
    }

    private static int ScoreDeterministicPrimarySet(Utf8FixedDistanceSet set)
    {
        var cardinality = GetSetCardinality(set);
        if (cardinality <= 0)
        {
            return int.MinValue;
        }

        var score = GetDeterministicAnchorRarityScore(set) * 100;
        score += set.Distance * 10;
        score -= cardinality * 10;
        if (ContainsAsciiWordChar(set))
        {
            score += 500;
        }

        if (ContainsOnlyAsciiWhitespace(set))
        {
            score -= 2000;
        }

        return score;
    }

    private static int GetSetCardinality(Utf8FixedDistanceSet set)
    {
        return set.ByteSet.Count;
    }

    private static bool ContainsAsciiWordChar(Utf8FixedDistanceSet set)
    {
        for (var value = 0; value < 128; value++)
        {
            if (set.Contains((byte)value) && Utf8AsciiBytePredicates.IsWord((byte)value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsOnlyAsciiWhitespace(Utf8FixedDistanceSet set)
    {
        var sawMatch = false;
        for (var value = 0; value < 128; value++)
        {
            if (!set.Contains((byte)value))
            {
                continue;
            }

            sawMatch = true;
            if (!Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value))
            {
                return false;
            }
        }

        return sawMatch;
    }

    private static int GetDeterministicAnchorRarityScore(Utf8FixedDistanceSet set)
    {
        var best = int.MinValue;
        for (var value = 0; value < 128; value++)
        {
            if (set.Contains((byte)value))
            {
                best = Math.Max(best, Utf8AsciiSearchHeuristics.GetAnchorRarityScore((byte)value));
            }
        }

        return best == int.MinValue ? 0 : best;
    }

    private static bool TryCreateAsciiSet(string set, int distance, out Utf8FixedDistanceSet fixedDistanceSet)
    {
        fixedDistanceSet = default;
        if (!Utf8OwnedCharClass.CanEasilyEnumerateSetContents(set))
        {
            return false;
        }

        var chars = Utf8OwnedCharClass.GetSetChars(set);
        if (chars.Length == 0)
        {
            return false;
        }

        var utf8Chars = new byte[chars.Length];
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] > 0x7F)
            {
                return false;
            }

            utf8Chars[i] = (byte)chars[i];
        }

        fixedDistanceSet = Utf8FixedDistanceSet.FromBytes(distance, utf8Chars, negated: false);
        return true;
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

    private static bool TryAddDeterministicPrefixSet(List<Utf8FixedDistanceSet> fixedDistanceSets, Utf8FixedDistanceSet fixedDistanceSet)
    {
        fixedDistanceSets.Add(fixedDistanceSet);
        return fixedDistanceSets.Count <= MaxDeterministicPrefixSets;
    }

    private static bool IsAsciiWordByte(byte value)
    {
        return Utf8AsciiBytePredicates.IsWord(value);
    }

    private static bool IsAsciiWhitespaceByte(byte value)
    {
        return Utf8AsciiBytePredicates.IsSixByteWhitespace(value);
    }

    private static bool TryEncodeAlternates(string[] alternatePrefixes, out byte[][] alternateLiterals)
    {
        alternateLiterals = new byte[alternatePrefixes.Length][];
        for (var i = 0; i < alternatePrefixes.Length; i++)
        {
            if (alternatePrefixes[i].Length == 0)
            {
                alternateLiterals = [];
                return false;
            }

            alternateLiterals[i] = Encoding.UTF8.GetBytes(alternatePrefixes[i]);
        }

        return true;
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

    private static bool ContainsAsciiWordChar(string value)
    {
        foreach (var ch in value)
        {
            if (ch is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_')
            {
                return true;
            }
        }

        return false;
    }
}
