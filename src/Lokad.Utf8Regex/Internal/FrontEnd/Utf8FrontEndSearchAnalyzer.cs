namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEndSearchAnalyzer
{
    private const int MaxDeterministicPrefixSets = 32;

    public static Utf8AnalyzedSearchInfo Analyze(Utf8SemanticRegex semanticRegex)
    {
        var runtimeTree = semanticRegex.RuntimeTree;
        var findOptimizations = runtimeTree?.FindOptimizations;
        if (findOptimizations is null || runtimeTree is null)
        {
            return new Utf8AnalyzedSearchInfo(Utf8SearchKind.None, null);
        }

        TrySelectRequiredLiteralPrefilter(findOptimizations.Root, out var requiredPrefilterLiteral, out var requiredPrefilterAlternateLiterals);
        TrySelectQuotedAsciiRunPrefilter(runtimeTree.Root, out var secondaryRequiredPrefilterQuotedAsciiSet, out var secondaryRequiredPrefilterQuotedAsciiLength);
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
        int? exactRequiredLength = findOptimizations.IsFixedLength ? findOptimizations.MinRequiredLength : null;
        var maxPossibleLength = findOptimizations.IsFixedLength ? null : findOptimizations.MaxPossibleLength;

        if (TrySelectTrailingFixedLengthAnchor(findOptimizations, out var trailingKind, out var trailingLength))
        {
            return new Utf8AnalyzedSearchInfo(
                trailingKind,
                requiredPrefilterLiteralUtf8: requiredPrefilterLiteral,
                requiredPrefilterAlternateLiteralsUtf8: requiredPrefilterAlternateLiterals,
                secondaryRequiredPrefilterQuotedAsciiSet: secondaryRequiredPrefilterQuotedAsciiSet,
                secondaryRequiredPrefilterQuotedAsciiLength: secondaryRequiredPrefilterQuotedAsciiLength,
                requiredWindowPrefilters: requiredWindowPrefilters,
                orderedWindowLeadingLiteralsUtf8: orderedLeadingLiterals,
                orderedWindowTrailingLiteralUtf8: orderedTrailingLiteral,
                orderedWindowMaxGap: orderedWindowMaxGap,
                orderedWindowSameLine: orderedWindowSameLine,
                fallbackStartTransform: fallbackStartTransform,
                minRequiredLength: trailingLength,
                exactRequiredLength: exactRequiredLength,
                maxPossibleLength: maxPossibleLength,
                leadingBoundary: leadingBoundary,
                trailingBoundary: trailingBoundary);
        }

        if (TrySelectFixedDistanceSets(findOptimizations, UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions), out var fixedDistanceSets, out var fixedDistanceMinRequiredLength))
        {
            return new Utf8AnalyzedSearchInfo(
                Utf8SearchKind.FixedDistanceAsciiSets,
                requiredPrefilterLiteralUtf8: requiredPrefilterLiteral,
                requiredPrefilterAlternateLiteralsUtf8: requiredPrefilterAlternateLiterals,
                secondaryRequiredPrefilterQuotedAsciiSet: secondaryRequiredPrefilterQuotedAsciiSet,
                secondaryRequiredPrefilterQuotedAsciiLength: secondaryRequiredPrefilterQuotedAsciiLength,
                requiredWindowPrefilters: requiredWindowPrefilters,
                orderedWindowLeadingLiteralsUtf8: orderedLeadingLiterals,
                orderedWindowTrailingLiteralUtf8: orderedTrailingLiteral,
                orderedWindowMaxGap: orderedWindowMaxGap,
                orderedWindowSameLine: orderedWindowSameLine,
                fallbackStartTransform: fallbackStartTransform,
                fixedDistanceSets: fixedDistanceSets,
                minRequiredLength: fixedDistanceMinRequiredLength,
                exactRequiredLength: exactRequiredLength,
                maxPossibleLength: maxPossibleLength,
                leadingBoundary: leadingBoundary,
                trailingBoundary: trailingBoundary);
        }

        if (TrySelectDeterministicPrefixSets(runtimeTree.Root, findOptimizations.MinRequiredLength, fallbackStartTransform, out var deterministicPrefixSets, out var deterministicPrefixMinRequiredLength))
        {
            return new Utf8AnalyzedSearchInfo(
                Utf8SearchKind.FixedDistanceAsciiSets,
                requiredPrefilterLiteralUtf8: requiredPrefilterLiteral,
                requiredPrefilterAlternateLiteralsUtf8: requiredPrefilterAlternateLiterals,
                secondaryRequiredPrefilterQuotedAsciiSet: secondaryRequiredPrefilterQuotedAsciiSet,
                secondaryRequiredPrefilterQuotedAsciiLength: secondaryRequiredPrefilterQuotedAsciiLength,
                requiredWindowPrefilters: requiredWindowPrefilters,
                orderedWindowLeadingLiteralsUtf8: orderedLeadingLiterals,
                orderedWindowTrailingLiteralUtf8: orderedTrailingLiteral,
                orderedWindowMaxGap: orderedWindowMaxGap,
                orderedWindowSameLine: orderedWindowSameLine,
                fallbackStartTransform: fallbackStartTransform,
                fixedDistanceSets: deterministicPrefixSets,
                minRequiredLength: deterministicPrefixMinRequiredLength,
                exactRequiredLength: exactRequiredLength,
                maxPossibleLength: maxPossibleLength,
                leadingBoundary: leadingBoundary,
                trailingBoundary: trailingBoundary);
        }

        if (TrySelectFixedDistanceLiteral(findOptimizations, UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions), out var fixedLiteral, out var distance, out var minRequiredLength))
        {
            return new Utf8AnalyzedSearchInfo(
                Utf8SearchKind.FixedDistanceAsciiLiteral,
                literalUtf8: fixedLiteral,
                requiredPrefilterLiteralUtf8: requiredPrefilterLiteral,
                requiredPrefilterAlternateLiteralsUtf8: requiredPrefilterAlternateLiterals,
                secondaryRequiredPrefilterQuotedAsciiSet: secondaryRequiredPrefilterQuotedAsciiSet,
                secondaryRequiredPrefilterQuotedAsciiLength: secondaryRequiredPrefilterQuotedAsciiLength,
                requiredWindowPrefilters: requiredWindowPrefilters,
                orderedWindowLeadingLiteralsUtf8: orderedLeadingLiterals,
                orderedWindowTrailingLiteralUtf8: orderedTrailingLiteral,
                orderedWindowMaxGap: orderedWindowMaxGap,
                orderedWindowSameLine: orderedWindowSameLine,
                fallbackStartTransform: fallbackStartTransform,
                distance: distance,
                minRequiredLength: minRequiredLength,
                exactRequiredLength: exactRequiredLength,
                maxPossibleLength: maxPossibleLength,
                leadingBoundary: leadingBoundary,
                trailingBoundary: trailingBoundary);
        }

        if (TrySelectAlternatePrefixes(findOptimizations, UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions), out var alternateLiterals))
        {
            var searchKind = alternateLiterals.Any(static literal => literal.Any(static b => b > 0x7F))
                ? Utf8SearchKind.ExactUtf8Literals
                : Utf8SearchKind.ExactAsciiLiterals;
            return new Utf8AnalyzedSearchInfo(
                searchKind,
                alternateLiteralsUtf8: alternateLiterals,
                canGuideFallbackStarts: CanGuideFallbackStarts(semanticRegex.ExecutionOptions),
                requiredPrefilterLiteralUtf8: requiredPrefilterLiteral,
                requiredPrefilterAlternateLiteralsUtf8: requiredPrefilterAlternateLiterals,
                secondaryRequiredPrefilterQuotedAsciiSet: secondaryRequiredPrefilterQuotedAsciiSet,
                secondaryRequiredPrefilterQuotedAsciiLength: secondaryRequiredPrefilterQuotedAsciiLength,
                requiredWindowPrefilters: requiredWindowPrefilters,
                orderedWindowLeadingLiteralsUtf8: orderedLeadingLiterals,
                orderedWindowTrailingLiteralUtf8: orderedTrailingLiteral,
                orderedWindowMaxGap: orderedWindowMaxGap,
                orderedWindowSameLine: orderedWindowSameLine,
                fallbackStartTransform: fallbackStartTransform,
                exactRequiredLength: exactRequiredLength,
                maxPossibleLength: maxPossibleLength,
                leadingBoundary: leadingBoundary,
                trailingBoundary: trailingBoundary);
        }

        if (TrySelectLeadingLiteral(findOptimizations, out var leadingLiteral) && IsAscii(leadingLiteral))
        {
            var searchKind = UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions)
                ? Utf8SearchKind.AsciiLiteralIgnoreCase
                : Utf8SearchKind.ExactAsciiLiteral;

            return new Utf8AnalyzedSearchInfo(
                searchKind,
                literalUtf8: Encoding.UTF8.GetBytes(leadingLiteral),
                canGuideFallbackStarts: CanGuideFallbackStarts(semanticRegex.ExecutionOptions),
                requiredPrefilterLiteralUtf8: requiredPrefilterLiteral,
                requiredPrefilterAlternateLiteralsUtf8: requiredPrefilterAlternateLiterals,
                secondaryRequiredPrefilterQuotedAsciiSet: secondaryRequiredPrefilterQuotedAsciiSet,
                secondaryRequiredPrefilterQuotedAsciiLength: secondaryRequiredPrefilterQuotedAsciiLength,
                requiredWindowPrefilters: requiredWindowPrefilters,
                orderedWindowLeadingLiteralsUtf8: orderedLeadingLiterals,
                orderedWindowTrailingLiteralUtf8: orderedTrailingLiteral,
                orderedWindowMaxGap: orderedWindowMaxGap,
                orderedWindowSameLine: orderedWindowSameLine,
                fallbackStartTransform: fallbackStartTransform,
                exactRequiredLength: exactRequiredLength,
                maxPossibleLength: maxPossibleLength,
                leadingBoundary: leadingBoundary,
                trailingBoundary: trailingBoundary);
        }

        if (TrySelectCandidateLiteral(findOptimizations, out var candidate) && IsAscii(candidate))
        {
            var searchKind = UsesInvariantIgnoreCase(semanticRegex.ExecutionOptions)
                ? Utf8SearchKind.AsciiLiteralIgnoreCase
                : Utf8SearchKind.ExactAsciiLiteral;

            return new Utf8AnalyzedSearchInfo(
                searchKind,
                literalUtf8: Encoding.UTF8.GetBytes(candidate),
                requiredPrefilterLiteralUtf8: requiredPrefilterLiteral,
                requiredPrefilterAlternateLiteralsUtf8: requiredPrefilterAlternateLiterals,
                requiredWindowPrefilters: requiredWindowPrefilters,
                orderedWindowLeadingLiteralsUtf8: orderedLeadingLiterals,
                orderedWindowTrailingLiteralUtf8: orderedTrailingLiteral,
                orderedWindowMaxGap: orderedWindowMaxGap,
                orderedWindowSameLine: orderedWindowSameLine,
                fallbackStartTransform: fallbackStartTransform,
                exactRequiredLength: exactRequiredLength,
                maxPossibleLength: maxPossibleLength,
                leadingBoundary: leadingBoundary,
                trailingBoundary: trailingBoundary);
        }

        if (requiredPrefilterLiteral is not null ||
            requiredPrefilterAlternateLiterals is not null ||
            secondaryRequiredPrefilterQuotedAsciiSet is not null)
        {
            return new Utf8AnalyzedSearchInfo(
                Utf8SearchKind.None,
                requiredPrefilterLiteralUtf8: requiredPrefilterLiteral,
                requiredPrefilterAlternateLiteralsUtf8: requiredPrefilterAlternateLiterals,
                secondaryRequiredPrefilterQuotedAsciiSet: secondaryRequiredPrefilterQuotedAsciiSet,
                secondaryRequiredPrefilterQuotedAsciiLength: secondaryRequiredPrefilterQuotedAsciiLength,
                requiredWindowPrefilters: requiredWindowPrefilters,
                orderedWindowLeadingLiteralsUtf8: orderedLeadingLiterals,
                orderedWindowTrailingLiteralUtf8: orderedTrailingLiteral,
                orderedWindowMaxGap: orderedWindowMaxGap,
                orderedWindowSameLine: orderedWindowSameLine,
                fallbackStartTransform: fallbackStartTransform,
                exactRequiredLength: exactRequiredLength,
                maxPossibleLength: maxPossibleLength,
                leadingBoundary: leadingBoundary,
                trailingBoundary: trailingBoundary);
        }

        return new Utf8AnalyzedSearchInfo(
            Utf8SearchKind.None,
            secondaryRequiredPrefilterQuotedAsciiSet: secondaryRequiredPrefilterQuotedAsciiSet,
            secondaryRequiredPrefilterQuotedAsciiLength: secondaryRequiredPrefilterQuotedAsciiLength,
            requiredWindowPrefilters: requiredWindowPrefilters,
            orderedWindowLeadingLiteralsUtf8: orderedLeadingLiterals,
            orderedWindowTrailingLiteralUtf8: orderedTrailingLiteral,
            orderedWindowMaxGap: orderedWindowMaxGap,
            orderedWindowSameLine: orderedWindowSameLine,
            fallbackStartTransform: fallbackStartTransform,
            exactRequiredLength: exactRequiredLength,
            maxPossibleLength: maxPossibleLength,
            leadingBoundary: leadingBoundary,
            trailingBoundary: trailingBoundary);
    }

    private static bool TrySelectQuotedAsciiRunPrefilter(Runtime.RegexNode root, out string? asciiSet, out int runLength)
    {
        asciiSet = null;
        runLength = 0;
        return TryFindQuotedAsciiRun(root, out asciiSet, out runLength);
    }

    private static void TrySelectRequiredWindowPrefilters(
        Runtime.RegexNode root,
        byte[][]? requiredPrefilterAlternateLiteralsUtf8,
        string? quotedAsciiSet,
        int quotedAsciiLength,
        out Utf8WindowSearchInfo[]? requiredWindowPrefilters)
    {
        requiredWindowPrefilters = null;

        if (requiredPrefilterAlternateLiteralsUtf8 is not { Length: > 0 } literalFamily ||
            string.IsNullOrEmpty(quotedAsciiSet) ||
            quotedAsciiLength <= 0)
        {
            return;
        }

        root = UnwrapStructuralNode(root) ?? root;
        var maxLines = TryGetFiniteLineBreakCount(root, out var totalLineBreaks) && totalLineBreaks > 0
            ? totalLineBreaks + 1
            : root.Kind == Runtime.RegexNodeKind.Alternate && root.ChildCount > 1 ? 5 : 0;

        if (maxLines > 0)
        {
            var family = Utf8PreparedSearcherInfo.LiteralFamily(literalFamily);
            var quoted = Utf8PreparedSearcherInfo.QuotedAsciiRun(quotedAsciiSet, quotedAsciiLength);
            requiredWindowPrefilters =
            [
                new Utf8WindowSearchInfo(family, quoted, maxLines: maxLines),
                new Utf8WindowSearchInfo(quoted, family, maxLines: maxLines),
            ];
        }
    }

    private static bool TryExtractWindowAnchor(Runtime.RegexNode node, out Utf8PreparedSearcherInfo searcherInfo)
    {
        node = UnwrapStructuralNode(node) ?? node;
        searcherInfo = default;

        if (TryFindQuotedAsciiRun(node, out var asciiSet, out var runLength))
        {
            searcherInfo = Utf8PreparedSearcherInfo.QuotedAsciiRun(asciiSet!, runLength);
            return true;
        }

        var family = Runtime.RegexRequiredLiteralAnalyzer.FindBestRequiredLiteralFamily(node);
        if (family is { Length: > 0 } &&
            family.All(IsAscii) &&
            TryEncodeAlternates(family, out var alternateLiteralsUtf8))
        {
            searcherInfo = Utf8PreparedSearcherInfo.LiteralFamily(alternateLiteralsUtf8);
            return true;
        }

        return false;
    }

    private static bool TryFindQuotedAsciiRun(Runtime.RegexNode node, out string? asciiSet, out int runLength)
    {
        node = UnwrapStructuralNode(node) ?? node;
        asciiSet = null;
        runLength = 0;

        if (node.Kind == Runtime.RegexNodeKind.Concatenate &&
            node.ChildCount >= 3)
        {
            for (var i = 0; i <= node.ChildCount - 3; i++)
            {
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

        for (var i = 0; i < node.ChildCount; i++)
        {
            if (TryFindQuotedAsciiRun(node.Child(i), out asciiSet, out runLength))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsQuoteNode(Runtime.RegexNode node)
    {
        node = UnwrapStructuralNode(node) ?? node;
        if (TryGetLiteralText(node, out var literal))
        {
            return literal is "\"" or "'";
        }

        if (node.Kind == Runtime.RegexNodeKind.Alternate)
        {
            for (var i = 0; i < node.ChildCount; i++)
            {
                if (!IsQuoteNode(node.Child(i)))
                {
                    return false;
                }
            }

            return node.ChildCount > 0;
        }

        return false;
    }

    private static bool TryGetExactAsciiSetLength(Runtime.RegexNode node, out string? asciiSet, out int runLength)
    {
        node = UnwrapStructuralNode(node) ?? node;
        asciiSet = null;
        runLength = 0;

        if (node.Kind is Runtime.RegexNodeKind.Setloop or Runtime.RegexNodeKind.Setloopatomic &&
            node.Str is { Length: > 0 } set &&
            node.M > 0 &&
            node.M == node.N &&
            Runtime.RegexCharClass.IsAscii(set))
        {
            asciiSet = set;
            runLength = node.M;
            return true;
        }

        if (node.Kind is Runtime.RegexNodeKind.Loop or Runtime.RegexNodeKind.Lazyloop &&
            node.ChildCount == 1 &&
            node.M > 0 &&
            node.M == node.N)
        {
            var child = UnwrapStructuralNode(node.Child(0)) ?? node.Child(0);
            if (child.Kind == Runtime.RegexNodeKind.Set &&
                child.Str is { Length: > 0 } childSet &&
                Runtime.RegexCharClass.IsAscii(childSet))
            {
                asciiSet = childSet;
                runLength = node.M;
                return true;
            }
        }

        return false;
    }

    public static Utf8AnalyzedSearchInfo AnalyzeLiteral(
        byte[] literalUtf8,
        NativeExecutionKind executionKind,
        byte[]? trailingLiteralUtf8 = null,
        Utf8BoundaryRequirement leadingBoundary = Utf8BoundaryRequirement.None,
        Utf8BoundaryRequirement trailingBoundary = Utf8BoundaryRequirement.None)
    {
        var searchKind = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCase
            ? Utf8SearchKind.AsciiLiteralIgnoreCase
            : Utf8SearchKind.ExactAsciiLiteral;

        return new Utf8AnalyzedSearchInfo(
            searchKind,
            literalUtf8,
            trailingLiteralUtf8: trailingLiteralUtf8,
            leadingBoundary: leadingBoundary,
            trailingBoundary: trailingBoundary);
    }

    public static Utf8AnalyzedSearchInfo AnalyzeSimplePattern(
        Utf8SemanticRegex semanticRegex,
        AsciiSimplePatternPlan simplePatternPlan,
        Utf8SearchPlan fallbackSearchPlan)
    {
        var exactRequiredLength = semanticRegex.RuntimeTree?.FindOptimizations?.IsFixedLength == true
            ? semanticRegex.RuntimeTree.FindOptimizations.MinRequiredLength
            : fallbackSearchPlan.ExactRequiredLength;
        var maxPossibleLength = semanticRegex.RuntimeTree?.FindOptimizations?.IsFixedLength == false
            ? semanticRegex.RuntimeTree.FindOptimizations.MaxPossibleLength
            : fallbackSearchPlan.MaxPossibleLength;

        if (semanticRegex.RuntimeTree?.FindOptimizations is { } findOptimizations)
        {
            if (TrySelectTrailingFixedLengthAnchor(findOptimizations, out var trailingKind, out var trailingLength))
            {
                return new Utf8AnalyzedSearchInfo(
                    trailingKind,
                    minRequiredLength: trailingLength,
                    exactRequiredLength: exactRequiredLength,
                    maxPossibleLength: maxPossibleLength);
            }

            if (TrySelectFixedDistanceLiteral(findOptimizations, simplePatternPlan.IgnoreCase, out var fixedLiteral, out var distance, out var minRequiredLength))
            {
                if (fixedLiteral.Length == 2)
                {
                    return new Utf8AnalyzedSearchInfo(
                        Utf8SearchKind.FixedDistanceAsciiChar,
                        [fixedLiteral[1]],
                        distance: distance + 1,
                        minRequiredLength: minRequiredLength,
                        exactRequiredLength: exactRequiredLength,
                        maxPossibleLength: maxPossibleLength);
                }

                return new Utf8AnalyzedSearchInfo(
                    Utf8SearchKind.FixedDistanceAsciiLiteral,
                    fixedLiteral,
                    distance: distance,
                    minRequiredLength: minRequiredLength,
                    exactRequiredLength: exactRequiredLength,
                    maxPossibleLength: maxPossibleLength);
            }

            if (TrySelectFixedDistanceSets(findOptimizations, simplePatternPlan.IgnoreCase, out var fixedDistanceSets, out var fixedDistanceMinRequiredLength))
            {
                return new Utf8AnalyzedSearchInfo(
                    Utf8SearchKind.FixedDistanceAsciiSets,
                    fixedDistanceSets: fixedDistanceSets,
                    minRequiredLength: fixedDistanceMinRequiredLength,
                    exactRequiredLength: exactRequiredLength,
                    maxPossibleLength: maxPossibleLength);
            }
        }

        if (!simplePatternPlan.IgnoreCase &&
            semanticRegex.RuntimeTree?.FindOptimizations?.AlternatePrefixes is { Length: > 1 } simplePatternAlternatePrefixes &&
            TryEncodeAlternates(simplePatternAlternatePrefixes, out var alternateLiterals))
        {
            var searchKind = alternateLiterals.Any(static literal => literal.Any(static b => b > 0x7F))
                ? Utf8SearchKind.ExactUtf8Literals
                : Utf8SearchKind.ExactAsciiLiterals;
            return new Utf8AnalyzedSearchInfo(searchKind, null, alternateLiterals, exactRequiredLength: exactRequiredLength, maxPossibleLength: maxPossibleLength);
        }

        if (semanticRegex.RuntimeTree?.FindOptimizations?.AlternatePrefixes is { Length: > 0 } alternatePrefixes)
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
                return new Utf8AnalyzedSearchInfo(
                    simplePatternPlan.IgnoreCase ? Utf8SearchKind.AsciiLiteralIgnoreCase : Utf8SearchKind.ExactAsciiLiteral,
                    Encoding.UTF8.GetBytes(longest),
                    exactRequiredLength: exactRequiredLength,
                    maxPossibleLength: maxPossibleLength);
            }
        }

        if (semanticRegex.RuntimeTree?.FindOptimizations?.LongestLiteral is { Length: > 0 } literal)
        {
            return new Utf8AnalyzedSearchInfo(
                simplePatternPlan.IgnoreCase ? Utf8SearchKind.AsciiLiteralIgnoreCase : Utf8SearchKind.ExactAsciiLiteral,
                Encoding.UTF8.GetBytes(literal),
                exactRequiredLength: exactRequiredLength,
                maxPossibleLength: maxPossibleLength);
        }

        return new Utf8AnalyzedSearchInfo(
            fallbackSearchPlan.Kind,
            literalUtf8: fallbackSearchPlan.LiteralUtf8,
            alternateLiteralsUtf8: fallbackSearchPlan.AlternateLiteralsUtf8,
            fixedDistanceSets: fallbackSearchPlan.FixedDistanceSets,
            trailingLiteralUtf8: fallbackSearchPlan.TrailingLiteralUtf8,
            distance: fallbackSearchPlan.Distance,
            minRequiredLength: fallbackSearchPlan.MinRequiredLength,
            exactRequiredLength: fallbackSearchPlan.ExactRequiredLength,
            maxPossibleLength: fallbackSearchPlan.MaxPossibleLength,
            leadingBoundary: fallbackSearchPlan.LeadingBoundary,
            trailingBoundary: fallbackSearchPlan.TrailingBoundary);
    }

    private static bool TrySelectCandidateLiteral(Runtime.RegexFindOptimizations findOptimizations, out string candidate)
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

    private static bool TrySelectLeadingLiteral(Runtime.RegexFindOptimizations findOptimizations, out string leadingLiteral)
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
        Runtime.RegexNode root,
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

        if (body.Kind != Runtime.RegexNodeKind.Concatenate || body.ChildCount == 0)
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

    private static bool TryGetBoundaryRequirement(Runtime.RegexNodeKind kind, out Utf8BoundaryRequirement requirement)
    {
        requirement = kind switch
        {
            Runtime.RegexNodeKind.Boundary => Utf8BoundaryRequirement.Boundary,
            Runtime.RegexNodeKind.NonBoundary => Utf8BoundaryRequirement.NonBoundary,
            _ => Utf8BoundaryRequirement.None,
        };

        return requirement != Utf8BoundaryRequirement.None;
    }

    private static int FindLeadingBoundaryChildIndex(Runtime.RegexNode node)
    {
        for (var i = 0; i < node.ChildCount; i++)
        {
            var kind = node.Child(i).Kind;
            if (kind is Runtime.RegexNodeKind.Boundary or Runtime.RegexNodeKind.NonBoundary)
            {
                return i;
            }

            if (kind is not Runtime.RegexNodeKind.Bol and
                not Runtime.RegexNodeKind.Eol and
                not Runtime.RegexNodeKind.Beginning and
                not Runtime.RegexNodeKind.Start)
            {
                break;
            }
        }

        return -1;
    }

    private static int FindTrailingBoundaryChildIndex(Runtime.RegexNode node)
    {
        for (var i = node.ChildCount - 1; i >= 0; i--)
        {
            var kind = node.Child(i).Kind;
            if (kind is Runtime.RegexNodeKind.Boundary or Runtime.RegexNodeKind.NonBoundary)
            {
                return i;
            }

            if (kind is not Runtime.RegexNodeKind.Bol and
                not Runtime.RegexNodeKind.Eol and
                not Runtime.RegexNodeKind.End and
                not Runtime.RegexNodeKind.EndZ)
            {
                break;
            }
        }

        return -1;
    }

    private static bool TrySelectFallbackStartTransform(Runtime.RegexNode root, out Utf8FallbackStartTransform transform)
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
        Runtime.RegexNode root,
        Runtime.RegexFindOptimizations findOptimizations,
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
        Runtime.RegexNode root,
        out int? maxGap,
        out bool sameLine)
    {
        maxGap = null;
        sameLine = false;

        var ordered = UnwrapStructuralNode(root);
        if (ordered is null || ordered.Kind != Runtime.RegexNodeKind.Concatenate)
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

    private static bool TrySelectOrderedWindowLeadingLiterals(Runtime.RegexFindOptimizations findOptimizations, out byte[][] leadingLiteralsUtf8)
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

    private static bool TryExtractLeadingWindowPhrases(Runtime.RegexNode root, out string[] phrases)
    {
        var leading = GetLeadingWindowNode(root);
        if (leading is null)
        {
            phrases = [];
            return false;
        }

        while (leading.Kind is Runtime.RegexNodeKind.Capture or Runtime.RegexNodeKind.Group or Runtime.RegexNodeKind.Atomic && leading.ChildCount == 1)
        {
            leading = leading.Child(0);
        }

        if (leading.Kind == Runtime.RegexNodeKind.Alternate)
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

    private static Runtime.RegexNode? GetLeadingWindowNode(Runtime.RegexNode node)
    {
        while (node.Kind is Runtime.RegexNodeKind.Capture or Runtime.RegexNodeKind.Group or Runtime.RegexNodeKind.Atomic && node.ChildCount == 1)
        {
            node = node.Child(0);
        }

        if (node.Kind != Runtime.RegexNodeKind.Concatenate)
        {
            return node;
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i);
            if (child.Kind is Runtime.RegexNodeKind.Bol or Runtime.RegexNodeKind.Eol or Runtime.RegexNodeKind.Boundary or Runtime.RegexNodeKind.NonBoundary)
            {
                continue;
            }

            return child;
        }

        return null;
    }

    private static bool TryExtractAsciiWindowPhrase(Runtime.RegexNode node, out string phrase)
    {
        phrase = string.Empty;

        while (node.Kind is Runtime.RegexNodeKind.Capture or Runtime.RegexNodeKind.Group or Runtime.RegexNodeKind.Atomic && node.ChildCount == 1)
        {
            node = node.Child(0);
        }

        if (TryGetLiteralText(node, out var literal) && IsAscii(literal))
        {
            phrase = literal;
            return phrase.Length > 0;
        }

        if (IsWhitespaceLoop(node))
        {
            phrase = " ";
            return true;
        }

        if (node.Kind != Runtime.RegexNodeKind.Concatenate)
        {
            return false;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i);
            if (child.Kind is Runtime.RegexNodeKind.Bol or Runtime.RegexNodeKind.Eol or Runtime.RegexNodeKind.Boundary or Runtime.RegexNodeKind.NonBoundary)
            {
                continue;
            }

            if (!TryExtractAsciiWindowPhrase(child, out var childPhrase))
            {
                return false;
            }

            builder.Append(childPhrase);
        }

        phrase = builder.ToString();
        return phrase.Length > 0;
    }

    private static bool IsWhitespaceLoop(Runtime.RegexNode node)
    {
        if (node.Kind is not (Runtime.RegexNodeKind.Setloop or Runtime.RegexNodeKind.Setlazy or Runtime.RegexNodeKind.Setloopatomic or Runtime.RegexNodeKind.Set))
        {
            return false;
        }

        return node.Str is Runtime.RegexCharClass.SpaceClass or Runtime.RegexCharClass.ECMASpaceClass;
    }

    private static int FindFirstSignificantChildIndex(Runtime.RegexNode node)
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

    private static int FindLastSignificantChildIndex(Runtime.RegexNode node)
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

    private static bool IsStructuralAnchor(Runtime.RegexNode node)
    {
        return node.Kind is Runtime.RegexNodeKind.Bol or Runtime.RegexNodeKind.Eol or Runtime.RegexNodeKind.Boundary or Runtime.RegexNodeKind.NonBoundary or Runtime.RegexNodeKind.Beginning or Runtime.RegexNodeKind.Start or Runtime.RegexNodeKind.End or Runtime.RegexNodeKind.EndZ;
    }

    private static bool TryGetFiniteAsciiWindowWidth(Runtime.RegexNode node, out int maxWidth, out bool excludesNewlines)
    {
        var unwrapped = UnwrapStructuralNode(node);
        if (unwrapped is null)
        {
            maxWidth = 0;
            excludesNewlines = true;
            return true;
        }

        node = unwrapped;

        switch (node.Kind)
        {
            case Runtime.RegexNodeKind.Empty:
            case Runtime.RegexNodeKind.Bol:
            case Runtime.RegexNodeKind.Beginning:
            case Runtime.RegexNodeKind.Start:
            case Runtime.RegexNodeKind.Eol:
            case Runtime.RegexNodeKind.End:
            case Runtime.RegexNodeKind.EndZ:
            case Runtime.RegexNodeKind.Boundary:
            case Runtime.RegexNodeKind.NonBoundary:
                maxWidth = 0;
                excludesNewlines = true;
                return true;

            case Runtime.RegexNodeKind.One:
                maxWidth = Encoding.UTF8.GetByteCount([node.Ch]);
                excludesNewlines = node.Ch is not ('\r' or '\n');
                return true;

            case Runtime.RegexNodeKind.Multi when node.Str is { Length: > 0 } literal:
                maxWidth = Encoding.UTF8.GetByteCount(literal);
                excludesNewlines = literal.IndexOfAny(['\r', '\n']) < 0;
                return true;

            case Runtime.RegexNodeKind.Set when node.Str is { Length: > 0 } set:
                if (!TryGetAsciiSetWidthAndLineSafety(set, out var setWidth, out var setExcludesNewlines))
                {
                    break;
                }

                maxWidth = setWidth;
                excludesNewlines = setExcludesNewlines;
                return true;

            case Runtime.RegexNodeKind.Oneloop or Runtime.RegexNodeKind.Onelazy or Runtime.RegexNodeKind.Oneloopatomic:
                return TryGetFiniteRepeatedWidth(node.N, Encoding.UTF8.GetByteCount([node.Ch]), node.Ch is not ('\r' or '\n'), out maxWidth, out excludesNewlines);

            case Runtime.RegexNodeKind.Setloop or Runtime.RegexNodeKind.Setlazy or Runtime.RegexNodeKind.Setloopatomic:
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

            case Runtime.RegexNodeKind.Loop or Runtime.RegexNodeKind.Lazyloop:
                if (node.ChildCount != 1 ||
                    !TryGetFiniteAsciiWindowWidth(node.Child(0), out var childLoopWidth, out var childLoopExcludesNewlines))
                {
                    break;
                }

                return TryGetFiniteRepeatedWidth(node.N, childLoopWidth, childLoopExcludesNewlines, out maxWidth, out excludesNewlines);

            case Runtime.RegexNodeKind.Concatenate:
                {
                    var total = 0;
                    var allExcludeNewlines = true;
                    for (var i = 0; i < node.ChildCount; i++)
                    {
                        if (!TryGetFiniteAsciiWindowWidth(node.Child(i), out var childWidth, out var childExcludes))
                        {
                            break;
                        }

                        total += childWidth;
                        allExcludeNewlines &= childExcludes;

                        if (i == node.ChildCount - 1)
                        {
                            maxWidth = total;
                            excludesNewlines = allExcludeNewlines;
                            return true;
                        }
                    }
                }
                break;

            case Runtime.RegexNodeKind.Alternate:
                {
                    var max = 0;
                    var allExcludeNewlines = true;
                    for (var i = 0; i < node.ChildCount; i++)
                    {
                        if (!TryGetFiniteAsciiWindowWidth(node.Child(i), out var childWidth, out var childExcludes))
                        {
                            break;
                        }

                        max = Math.Max(max, childWidth);
                        allExcludeNewlines &= childExcludes;

                        if (i == node.ChildCount - 1)
                        {
                            maxWidth = max;
                            excludesNewlines = allExcludeNewlines;
                            return true;
                        }
                    }
                }
                break;
        }

        maxWidth = 0;
        excludesNewlines = false;
        return false;
    }

    private static bool TryGetFiniteLineBreakCount(Runtime.RegexNode node, out int maxLineBreaks)
    {
        var unwrapped = UnwrapStructuralNode(node);
        if (unwrapped is null)
        {
            maxLineBreaks = 0;
            return true;
        }

        node = unwrapped;

        switch (node.Kind)
        {
            case Runtime.RegexNodeKind.Empty:
            case Runtime.RegexNodeKind.Bol:
            case Runtime.RegexNodeKind.Beginning:
            case Runtime.RegexNodeKind.Start:
            case Runtime.RegexNodeKind.Eol:
            case Runtime.RegexNodeKind.End:
            case Runtime.RegexNodeKind.EndZ:
            case Runtime.RegexNodeKind.Boundary:
            case Runtime.RegexNodeKind.NonBoundary:
                maxLineBreaks = 0;
                return true;

            case Runtime.RegexNodeKind.One:
                maxLineBreaks = node.Ch is '\r' or '\n' ? 1 : 0;
                return true;

            case Runtime.RegexNodeKind.Multi when node.Str is { Length: > 0 } literal:
                maxLineBreaks = literal.Count(static ch => ch is '\r' or '\n');
                return true;

            case Runtime.RegexNodeKind.Set:
                if (node.Str is { Length: > 0 } set &&
                    !Runtime.RegexCharClass.CharInClass('\r', set) &&
                    !Runtime.RegexCharClass.CharInClass('\n', set))
                {
                    maxLineBreaks = 0;
                    return true;
                }

                break;

            case Runtime.RegexNodeKind.Oneloop:
            case Runtime.RegexNodeKind.Onelazy:
            case Runtime.RegexNodeKind.Oneloopatomic:
                if (node.N >= 0 && node.N != int.MaxValue)
                {
                    maxLineBreaks = node.Ch is '\r' or '\n' ? node.N : 0;
                    return true;
                }

                break;

            case Runtime.RegexNodeKind.Setloop:
            case Runtime.RegexNodeKind.Setlazy:
            case Runtime.RegexNodeKind.Setloopatomic:
                if (node.N >= 0 &&
                    node.N != int.MaxValue &&
                    node.Str is { Length: > 0 } repeatedSet &&
                    !Runtime.RegexCharClass.CharInClass('\r', repeatedSet) &&
                    !Runtime.RegexCharClass.CharInClass('\n', repeatedSet))
                {
                    maxLineBreaks = 0;
                    return true;
                }

                break;

            case Runtime.RegexNodeKind.Loop:
            case Runtime.RegexNodeKind.Lazyloop:
                if (node.ChildCount == 1 &&
                    node.N >= 0 &&
                    node.N != int.MaxValue &&
                    TryGetFiniteLineBreakCount(node.Child(0), out var loopChildBreaks))
                {
                    maxLineBreaks = checked(node.N * loopChildBreaks);
                    return true;
                }

                break;

            case Runtime.RegexNodeKind.Concatenate:
                {
                    var total = 0;
                    for (var i = 0; i < node.ChildCount; i++)
                    {
                        if (!TryGetFiniteLineBreakCount(node.Child(i), out var childBreaks))
                        {
                            break;
                        }

                        total += childBreaks;
                        if (i == node.ChildCount - 1)
                        {
                            maxLineBreaks = total;
                            return true;
                        }
                    }
                }

                break;

            case Runtime.RegexNodeKind.Alternate:
                {
                    var max = 0;
                    for (var i = 0; i < node.ChildCount; i++)
                    {
                        if (!TryGetFiniteLineBreakCount(node.Child(i), out var branchChildBreaks))
                        {
                            break;
                        }

                        max = Math.Max(max, branchChildBreaks);
                        if (i == node.ChildCount - 1)
                        {
                            maxLineBreaks = max;
                            return true;
                        }
                    }
                }

                break;
        }

        maxLineBreaks = 0;
        return false;
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
        if (Runtime.RegexCharClass.IsAscii(set))
        {
            width = 1;
            excludesNewlines = !Runtime.RegexCharClass.CharInClass('\r', set) &&
                !Runtime.RegexCharClass.CharInClass('\n', set);
            return true;
        }

        if (Runtime.RegexCharClass.TryGetSingleRange(set, out var first, out var last) &&
            last <= 0x7F)
        {
            width = 1;
            excludesNewlines = !ContainsNewlineRange(first, last);
            return true;
        }

        if (Runtime.RegexCharClass.TryGetDoubleRange(set, out var firstRange, out var secondRange) &&
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

    private static bool IsLeadingAsciiWhitespaceNode(Runtime.RegexNode node)
    {
        while (node.Kind is Runtime.RegexNodeKind.Capture or Runtime.RegexNodeKind.Group or Runtime.RegexNodeKind.Atomic && node.ChildCount == 1)
        {
            node = node.Child(0);
        }

        return IsWhitespaceLoop(node);
    }

    private static bool TryExtractTrailingLiteral(Runtime.RegexNode node, out string literal)
    {
        while (node.Kind is Runtime.RegexNodeKind.Capture or Runtime.RegexNodeKind.Group or Runtime.RegexNodeKind.Atomic && node.ChildCount == 1)
        {
            node = node.Child(0);
        }

        if (TryGetLiteralText(node, out literal))
        {
            return true;
        }

        if (node.Kind != Runtime.RegexNodeKind.Concatenate)
        {
            literal = string.Empty;
            return false;
        }

        for (var i = node.ChildCount - 1; i >= 0; i--)
        {
            var child = node.Child(i);
            if (TryGetLiteralText(child, out literal))
            {
                return true;
            }

            if (child.Kind is Runtime.RegexNodeKind.Bol or Runtime.RegexNodeKind.Eol or Runtime.RegexNodeKind.End or Runtime.RegexNodeKind.EndZ or Runtime.RegexNodeKind.Boundary or Runtime.RegexNodeKind.NonBoundary)
            {
                continue;
            }

            if (TryExtractTrailingLiteral(child, out literal))
            {
                return true;
            }
        }

        literal = string.Empty;
        return false;
    }

    private static bool TryGetLiteralText(Runtime.RegexNode node, out string literal)
    {
        switch (node.Kind)
        {
            case Runtime.RegexNodeKind.One:
                literal = node.Ch.ToString();
                return true;
            case Runtime.RegexNodeKind.Multi when !string.IsNullOrEmpty(node.Str):
                literal = node.Str;
                return true;
            default:
                literal = string.Empty;
                return false;
        }
    }

    private static bool TrySelectAlternatePrefixes(Runtime.RegexFindOptimizations findOptimizations, bool ignoreCase, out byte[][] alternateLiterals)
    {
        alternateLiterals = [];
        if (ignoreCase ||
            findOptimizations.AlternatePrefixes is not { Length: > 1 } alternatePrefixes)
        {
            return false;
        }

        return TryEncodeAlternates(alternatePrefixes, out alternateLiterals);
    }

    private static bool TrySelectTrailingFixedLengthAnchor(Runtime.RegexFindOptimizations findOptimizations, out Utf8SearchKind kind, out int minRequiredLength)
    {
        kind = Utf8SearchKind.None;
        minRequiredLength = 0;
        if (!findOptimizations.IsFixedLength)
        {
            return false;
        }

        kind = findOptimizations.TrailingAnchor switch
        {
            Runtime.RegexNodeKind.End => Utf8SearchKind.TrailingAnchorFixedLengthEnd,
            Runtime.RegexNodeKind.EndZ => Utf8SearchKind.TrailingAnchorFixedLengthEndZ,
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
        Runtime.RegexFindOptimizations findOptimizations,
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
        Runtime.RegexNode root,
        out byte[]? literalUtf8,
        out byte[][]? alternateLiteralsUtf8)
    {
        literalUtf8 = null;
        alternateLiteralsUtf8 = null;

        var requiredFamily = Runtime.RegexRequiredLiteralAnalyzer.FindBestRequiredLiteralFamily(root);
        if (requiredFamily is not { Length: > 0 })
        {
            return false;
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
        Runtime.RegexNode root,
        int minRequiredLength,
        Utf8FallbackStartTransform fallbackStartTransform,
        out Utf8FixedDistanceSet[] fixedDistanceSets,
        out int selectedMinRequiredLength)
    {
        fixedDistanceSets = [];
        selectedMinRequiredLength = 0;

        var prefixRoot = fallbackStartTransform.Kind == Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace
            ? SkipLeadingAsciiWhitespacePrefix(root)
            : root;
        if (prefixRoot is null ||
            !TryExtractDeterministicPrefixSets(prefixRoot, out var extractedSets) ||
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
        Runtime.RegexFindOptimizations findOptimizations,
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

            fixedDistanceSets[i] = new Utf8FixedDistanceSet(set.Distance, chars, set.Negated, rangeLow, rangeHigh, hasRange);
        }

        minRequiredLength = findOptimizations.MinRequiredLength;
        return true;
    }

    private static Runtime.RegexNode? SkipLeadingAsciiWhitespacePrefix(Runtime.RegexNode node)
    {
        var unwrapped = UnwrapStructuralNode(node);
        if (unwrapped is null || unwrapped.Kind != Runtime.RegexNodeKind.Concatenate)
        {
            return unwrapped;
        }

        node = unwrapped;
        var startIndex = 0;
        while (startIndex < node.ChildCount &&
               node.Child(startIndex).Kind is Runtime.RegexNodeKind.Bol or Runtime.RegexNodeKind.Eol or Runtime.RegexNodeKind.Boundary or Runtime.RegexNodeKind.NonBoundary or Runtime.RegexNodeKind.Beginning or Runtime.RegexNodeKind.Start)
        {
            startIndex++;
        }

        var firstContent = startIndex < node.ChildCount ? UnwrapStructuralNode(node.Child(startIndex)) : null;
        if (firstContent is null || !IsWhitespaceLoop(firstContent))
        {
            return node;
        }

        startIndex++;
        while (startIndex < node.ChildCount &&
               node.Child(startIndex).Kind is Runtime.RegexNodeKind.Bol or Runtime.RegexNodeKind.Eol or Runtime.RegexNodeKind.Boundary or Runtime.RegexNodeKind.NonBoundary or Runtime.RegexNodeKind.Beginning or Runtime.RegexNodeKind.Start)
        {
            startIndex++;
        }

        if (startIndex >= node.ChildCount)
        {
            return null;
        }

        if (startIndex == node.ChildCount - 1)
        {
            return node.Child(startIndex);
        }

        var children = new Runtime.RegexNode[node.ChildCount - startIndex];
        for (var i = startIndex; i < node.ChildCount; i++)
        {
            children[i - startIndex] = node.Child(i);
        }

        var concatenation = new Runtime.RegexNode(Runtime.RegexNodeKind.Concatenate, node.Options);
        foreach (var child in children)
        {
            concatenation.AddChild(child);
        }

        return concatenation;
    }

    private static bool TryExtractDeterministicPrefixSets(Runtime.RegexNode node, out Utf8FixedDistanceSet[] fixedDistanceSets)
    {
        var collectedSets = new List<Utf8FixedDistanceSet>();
        var offset = 0;
        if (!TryAppendDeterministicPrefixSets(node, collectedSets, ref offset) || collectedSets.Count == 0)
        {
            fixedDistanceSets = [];
            return false;
        }

        fixedDistanceSets = [.. collectedSets];
        return true;
    }

    private static bool TryAppendDeterministicPrefixSets(Runtime.RegexNode node, List<Utf8FixedDistanceSet> fixedDistanceSets, ref int offset)
    {
        var unwrapped = UnwrapStructuralNode(node);
        if (unwrapped is null)
        {
            return false;
        }

        node = unwrapped;

        switch (node.Kind)
        {
            case Runtime.RegexNodeKind.Empty:
            case Runtime.RegexNodeKind.Bol:
            case Runtime.RegexNodeKind.Beginning:
            case Runtime.RegexNodeKind.Start:
            case Runtime.RegexNodeKind.Eol:
            case Runtime.RegexNodeKind.End:
            case Runtime.RegexNodeKind.EndZ:
            case Runtime.RegexNodeKind.Boundary:
            case Runtime.RegexNodeKind.NonBoundary:
                return true;

            case Runtime.RegexNodeKind.One when node.Ch <= 0x7F:
                return TryAddDeterministicPrefixSet(fixedDistanceSets, new Utf8FixedDistanceSet(offset++, [(byte)node.Ch], negated: false));

            case Runtime.RegexNodeKind.Multi when node.Str is { Length: > 0 } literal:
                foreach (var ch in literal)
                {
                    if (ch > 0x7F || !TryAddDeterministicPrefixSet(fixedDistanceSets, new Utf8FixedDistanceSet(offset++, [(byte)ch], negated: false)))
                    {
                        return false;
                    }
                }

                return true;

            case Runtime.RegexNodeKind.Set when node.Str is { Length: > 0 } set && Runtime.RegexCharClass.IsAscii(set):
                return TryCreateAsciiSet(set, offset++, out var fixedDistanceSet) &&
                    TryAddDeterministicPrefixSet(fixedDistanceSets, fixedDistanceSet);

            case Runtime.RegexNodeKind.Concatenate:
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!TryAppendDeterministicPrefixSets(node.Child(i), fixedDistanceSets, ref offset))
                    {
                        return fixedDistanceSets.Count > 0;
                    }
                }

                return true;

            default:
                return false;
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
        if (set.Chars is { Length: > 0 } chars)
        {
            return chars.Length;
        }

        if (set.HasRange)
        {
            return set.RangeHigh - set.RangeLow + 1;
        }

        return 0;
    }

    private static bool ContainsAsciiWordChar(Utf8FixedDistanceSet set)
    {
        if (set.Chars is { Length: > 0 } chars)
        {
            foreach (var ch in chars)
            {
                if (IsAsciiWordByte(ch))
                {
                    return true;
                }
            }
        }
        else if (set.HasRange)
        {
            for (var value = set.RangeLow; value <= set.RangeHigh; value++)
            {
                if (IsAsciiWordByte(value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsOnlyAsciiWhitespace(Utf8FixedDistanceSet set)
    {
        if (set.Chars is { Length: > 0 } chars)
        {
            return chars.All(IsAsciiWhitespaceByte);
        }

        if (set.HasRange)
        {
            for (var value = set.RangeLow; value <= set.RangeHigh; value++)
            {
                if (!IsAsciiWhitespaceByte(value))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    private static int GetDeterministicAnchorRarityScore(Utf8FixedDistanceSet set)
    {
        var best = int.MinValue;
        if (set.Chars is { Length: > 0 } chars)
        {
            foreach (var ch in chars)
            {
                best = Math.Max(best, GetAsciiAnchorRarityScore(ch));
            }
        }
        else if (set.HasRange)
        {
            for (var value = set.RangeLow; value <= set.RangeHigh; value++)
            {
                best = Math.Max(best, GetAsciiAnchorRarityScore(value));
            }
        }

        return best == int.MinValue ? 0 : best;
    }

    private static int GetAsciiAnchorRarityScore(byte value)
    {
        value = (byte)char.ToUpperInvariant((char)value);
        return value switch
        {
            (byte)'Q' or (byte)'J' or (byte)'X' or (byte)'Z' => 10,
            (byte)'K' or (byte)'V' or (byte)'B' or (byte)'P' or (byte)'Y' or (byte)'G' or (byte)'W' => 8,
            (byte)'F' or (byte)'M' or (byte)'U' or (byte)'C' or (byte)'L' or (byte)'D' => 6,
            (byte)'R' or (byte)'H' or (byte)'S' or (byte)'N' or (byte)'I' or (byte)'O' => 4,
            (byte)'A' or (byte)'T' or (byte)'E' => 2,
            _ when value is >= (byte)'0' and <= (byte)'9' => 5,
            (byte)'_' => 3,
            _ => 1,
        };
    }

    private static bool TryCreateAsciiSet(string set, int distance, out Utf8FixedDistanceSet fixedDistanceSet)
    {
        fixedDistanceSet = default;
        if (!Runtime.RegexCharClass.CanEasilyEnumerateSetContents(set))
        {
            return false;
        }

        var chars = Runtime.RegexCharClass.GetSetChars(set);
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

        fixedDistanceSet = new Utf8FixedDistanceSet(distance, utf8Chars, negated: false);
        return true;
    }

    private static Runtime.RegexNode? UnwrapStructuralNode(Runtime.RegexNode? node)
    {
        while (node is not null &&
               node.Kind is Runtime.RegexNodeKind.Capture or Runtime.RegexNodeKind.Group or Runtime.RegexNodeKind.Atomic &&
               node.ChildCount == 1)
        {
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
        return value is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z' or (byte)'_';
    }

    private static bool IsAsciiWhitespaceByte(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C;
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
