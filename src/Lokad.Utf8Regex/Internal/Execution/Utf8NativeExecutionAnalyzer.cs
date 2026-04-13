namespace Lokad.Utf8Regex.Internal.Execution;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static class Utf8NativeExecutionAnalyzer
{
    public static bool TryAnalyze(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8AnalyzedRegex analyzedRegex)
    {
        if (TryAnalyzeLiteralTree(semanticRegex, executionPattern, executionOptions, out analyzedRegex))
        {
            return true;
        }

        if (TryAnalyzeAsciiStructuralIdentifierFamilyTree(semanticRegex, executionPattern, executionOptions, out analyzedRegex))
        {
            return true;
        }

        if (TryAnalyzeAsciiStructuralTokenWindowTree(semanticRegex, executionPattern, executionOptions, out analyzedRegex))
        {
            return true;
        }

        if (TryAnalyzeAsciiOrderedLiteralWindowTree(semanticRegex, executionPattern, executionOptions, out analyzedRegex))
        {
            return true;
        }

        if (TryAnalyzeAsciiStructuralRepeatedSegmentTree(semanticRegex, executionPattern, executionOptions, out analyzedRegex))
        {
            return true;
        }

        if (TryAnalyzeAsciiStructuralQuotedRelationTree(semanticRegex, executionPattern, executionOptions, out analyzedRegex))
        {
            return true;
        }

        if (TryAnalyzeAnchoredAsciiHeadTailRunTree(semanticRegex, executionPattern, executionOptions, out analyzedRegex))
        {
            return true;
        }

        if (TryAnalyzeSimplePatternTree(semanticRegex, executionPattern, executionOptions, out analyzedRegex))
        {
            return true;
        }

        analyzedRegex = default;
        return false;
    }

    private static bool TryAnalyzeLiteralTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8AnalyzedRegex analyzedRegex)
    {
        if (!SupportsNativeLiteralOptions(executionOptions))
        {
            analyzedRegex = default;
            return false;
        }

        if (IsPlainLiteral(executionPattern) && IsNativeLiteralExecutionPattern(executionPattern, executionOptions))
        {
            return TryCreateLiteralAnalyzedRegex(semanticRegex, executionPattern, executionOptions, out analyzedRegex);
        }

        if (TryExtractBoundaryWrappedLiteralTree(semanticRegex, out var boundaryLiteral, out var leadingBoundary, out var trailingBoundary))
        {
            return TryCreateLiteralAnalyzedRegex(
                semanticRegex,
                boundaryLiteral,
                executionOptions,
                out analyzedRegex,
                leadingBoundary: leadingBoundary,
                trailingBoundary: trailingBoundary);
        }

        if ((executionOptions & RegexOptions.RightToLeft) == 0 &&
            TryExtractLiteralWithPositiveLiteralLookaheadTree(semanticRegex, out var lookaheadLiteral, out var trailingLiteralUtf8))
        {
            return TryCreateLiteralAnalyzedRegex(semanticRegex, lookaheadLiteral, executionOptions, out analyzedRegex, trailingLiteralUtf8: trailingLiteralUtf8);
        }

        if (TryExtractSingleCapturedLiteralPattern(executionPattern, out var capturedLiteral))
        {
            return TryCreateLiteralAnalyzedRegex(semanticRegex, capturedLiteral, executionOptions, out analyzedRegex);
        }

        if ((executionOptions & RegexOptions.RightToLeft) == 0 &&
            TryExtractLiteralAlternationWithPositiveLiteralLookaheadTree(semanticRegex, executionOptions, out var lookaheadAlternates, out var alternationTrailingLiteralUtf8, out var lookaheadAlternationExecutionKind, out var lookaheadAlternationSearchKind))
        {
            analyzedRegex = new Utf8AnalyzedRegex(
                semanticRegex,
                executionPattern,
                Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
                new Utf8AnalyzedSearchInfo(
                    lookaheadAlternationSearchKind,
                    null,
                    lookaheadAlternates,
                    trailingLiteralUtf8: alternationTrailingLiteralUtf8),
                lookaheadAlternationExecutionKind);
            return true;
        }

        if ((executionOptions & RegexOptions.RightToLeft) == 0 &&
            (executionOptions & RegexOptions.IgnoreCase) != 0 &&
            TryExtractPlainAsciiLiteralAlternationPattern(executionPattern, out var ignoreCaseAlternateLiterals))
        {
            analyzedRegex = new Utf8AnalyzedRegex(
                semanticRegex,
                executionPattern,
                Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
                new Utf8AnalyzedSearchInfo(
                    Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals,
                    null,
                    ignoreCaseAlternateLiterals),
                NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals);
            return true;
        }

        if ((executionOptions & RegexOptions.RightToLeft) == 0 &&
            TryExtractUtf8LiteralAlternationTree(semanticRegex, out var alternateLiterals, out var alternationLeadingBoundary, out var alternationTrailingBoundary))
        {
            if (alternationLeadingBoundary != Utf8BoundaryRequirement.None ||
                alternationTrailingBoundary != Utf8BoundaryRequirement.None)
            {
                analyzedRegex = default;
                return false;
            }

            var ignoreCase = (executionOptions & RegexOptions.IgnoreCase) != 0;
            var hasNonAscii = alternateLiterals.Any(static literal => literal.Any(static b => b > 0x7F));
            if (ignoreCase && hasNonAscii)
            {
                analyzedRegex = default;
                return false;
            }

            var searchKind = ignoreCase
                ? Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals
                : !hasNonAscii &&
                  alternationLeadingBoundary == Utf8BoundaryRequirement.None &&
                  alternationTrailingBoundary == Utf8BoundaryRequirement.None
                    ? Utf8SearchKind.ExactAsciiLiterals
                    : Utf8SearchKind.ExactUtf8Literals;
            var executionKind = ignoreCase
                ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                : NativeExecutionKind.ExactUtf8Literals;
            analyzedRegex = new Utf8AnalyzedRegex(
                semanticRegex,
                executionPattern,
                Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
                new Utf8AnalyzedSearchInfo(
                    searchKind,
                    null,
                    alternateLiterals,
                    leadingBoundary: alternationLeadingBoundary,
                    trailingBoundary: alternationTrailingBoundary),
                executionKind);
            return true;
        }

        analyzedRegex = default;
        return false;
    }

    private static bool TryCreateLiteralAnalyzedRegex(
        Utf8SemanticRegex semanticRegex,
        string literalPattern,
        RegexOptions executionOptions,
        out Utf8AnalyzedRegex analyzedRegex,
        byte[]? trailingLiteralUtf8 = null,
        Utf8BoundaryRequirement leadingBoundary = Utf8BoundaryRequirement.None,
        Utf8BoundaryRequirement trailingBoundary = Utf8BoundaryRequirement.None)
    {
        if ((executionOptions & RegexOptions.IgnoreCase) != 0 &&
            !IsPlainAsciiLiteral(literalPattern))
        {
            analyzedRegex = default;
            return false;
        }

        var literalUtf8 = Encoding.UTF8.GetBytes(literalPattern);
        var executionKind = (executionOptions & RegexOptions.IgnoreCase) != 0
            ? NativeExecutionKind.AsciiLiteralIgnoreCase
            : IsPlainAsciiLiteral(literalPattern)
                ? NativeExecutionKind.ExactAsciiLiteral
                : NativeExecutionKind.ExactUtf8Literal;

        analyzedRegex = new Utf8AnalyzedRegex(
            semanticRegex,
            literalPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            Utf8FrontEndSearchAnalyzer.AnalyzeLiteral(literalUtf8, executionKind, trailingLiteralUtf8, leadingBoundary, trailingBoundary),
            executionKind,
            literalUtf8: literalUtf8);
        return true;
    }

    private static bool TryAnalyzeSimplePatternTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8AnalyzedRegex analyzedRegex)
    {
        if (!SupportsNativeOptions(executionOptions) || IsPlainLiteral(executionPattern))
        {
            analyzedRegex = default;
            return false;
        }

        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1)
        {
            analyzedRegex = default;
            return false;
        }

        var body = root.Child(0);
        if (!IsSupportedSimplePatternNode(body))
        {
            analyzedRegex = default;
            return false;
        }

        if (!Utf8AsciiSimplePatternLowerer.TryCreatePlan(semanticRegex, executionOptions, out var simplePatternPlan, out var searchPlan))
        {
            analyzedRegex = default;
            return false;
        }

        if (Utf8AsciiSimplePatternLowerer.TryExtractLiteralBranches(simplePatternPlan, out var literalBranches))
        {
            if (literalBranches.Length == 1)
            {
                return TryCreateLiteralAnalyzedRegex(
                    semanticRegex,
                    Encoding.UTF8.GetString(literalBranches[0]),
                    executionOptions,
                    out analyzedRegex);
            }

            var ignoreCase = (executionOptions & RegexOptions.IgnoreCase) != 0;
            analyzedRegex = new Utf8AnalyzedRegex(
                semanticRegex,
                executionPattern,
                Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
                new Utf8AnalyzedSearchInfo(
                    ignoreCase ? Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals : Utf8SearchKind.ExactAsciiLiterals,
                    null,
                    literalBranches),
                ignoreCase ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals : NativeExecutionKind.ExactUtf8Literals);
            return true;
        }

        analyzedRegex = new Utf8AnalyzedRegex(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            Utf8FrontEndSearchAnalyzer.AnalyzeSimplePattern(semanticRegex, simplePatternPlan, searchPlan),
            NativeExecutionKind.AsciiSimplePattern,
            simplePatternPlan,
            literalUtf8: searchPlan.LiteralUtf8);
        return true;
    }

    private static bool TryAnalyzeAnchoredAsciiHeadTailRunTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8AnalyzedRegex analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1)
        {
            return false;
        }

        var body = UnwrapRuntimeNode(root.Child(0));
        if (body.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            body.ChildCount != 4 ||
            body.Child(0).Kind is not RuntimeFrontEnd.RegexNodeKind.Beginning and not RuntimeFrontEnd.RegexNodeKind.Start ||
            !TryGetExactAsciiSetAtom(body.Child(1), out var headSet, out var headLength) ||
            headLength != 1 ||
            !TryGetSetLoop(body.Child(2), out var tailSetNode, out var tailMin, out var tailMax) ||
            tailSetNode.Str is not { } tailSet ||
            tailMax != int.MaxValue ||
            !RuntimeFrontEnd.RegexCharClass.IsAscii(tailSet) ||
            body.Child(3).Kind is not RuntimeFrontEnd.RegexNodeKind.EndZ and not RuntimeFrontEnd.RegexNodeKind.End)
        {
            return false;
        }

        if (!TryCreateAsciiCharClass(headSet, out var headCharClass) ||
            !TryCreateAsciiCharClass(tailSet, out var tailCharClass))
        {
            return false;
        }

        var simplePatternPlan = new AsciiSimplePatternPlan(
            [],
            searchLiteralOffset: 0,
            searchLiterals: [],
            fixedLiteralChecks: [],
            isStartAnchored: true,
            isEndAnchored: true,
            allowsTrailingNewlineBeforeEnd: body.Child(3).Kind == RuntimeFrontEnd.RegexNodeKind.EndZ,
            ignoreCase: (executionOptions & RegexOptions.IgnoreCase) != 0,
            isUtf8ByteSafe: true,
            anchoredHeadTailRunPlan: new AsciiSimplePatternAnchoredHeadTailRunPlan(headCharClass, tailCharClass, tailMin),
            anchoredValidatorPlan: new AsciiSimplePatternAnchoredValidatorPlan(
            [
                new AsciiSimplePatternAnchoredValidatorSegment(headCharClass, 1, 1),
                new AsciiSimplePatternAnchoredValidatorSegment(tailCharClass, tailMin, int.MaxValue),
            ],
            ignoreCase: (executionOptions & RegexOptions.IgnoreCase) != 0));

        analyzedRegex = new Utf8AnalyzedRegex(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            default,
            NativeExecutionKind.AsciiSimplePattern,
            simplePatternPlan);
        return true;
    }

    private static bool IsSupportedSimplePatternNode(RuntimeFrontEnd.RegexNode node)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Capture:
                if (node.N != -1)
                {
                    return false;
                }

                goto case RuntimeFrontEnd.RegexNodeKind.Group;

            case RuntimeFrontEnd.RegexNodeKind.Group:
                return node.ChildCount == 1 && IsSupportedSimplePatternNode(node.Child(0));

            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
            case RuntimeFrontEnd.RegexNodeKind.Alternate:
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!IsSupportedSimplePatternNode(node.Child(i)))
                    {
                        return false;
                    }
                }

                return true;

            case RuntimeFrontEnd.RegexNodeKind.One:
            case RuntimeFrontEnd.RegexNodeKind.Multi:
            case RuntimeFrontEnd.RegexNodeKind.Bol:
            case RuntimeFrontEnd.RegexNodeKind.Eol:
            case RuntimeFrontEnd.RegexNodeKind.Beginning:
            case RuntimeFrontEnd.RegexNodeKind.Start:
            case RuntimeFrontEnd.RegexNodeKind.EndZ:
            case RuntimeFrontEnd.RegexNodeKind.End:
            case RuntimeFrontEnd.RegexNodeKind.Empty:
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Set:
                return node.Str is not null && !RuntimeFrontEnd.RegexCharClass.IsSubtraction(node.Str);

            case RuntimeFrontEnd.RegexNodeKind.Oneloop:
            case RuntimeFrontEnd.RegexNodeKind.Setloop:
                return node.M >= 0 &&
                    node.N >= node.M &&
                    node.N != int.MaxValue;

            case RuntimeFrontEnd.RegexNodeKind.Loop:
                return node.M >= 0 &&
                    node.N >= node.M &&
                    node.N != int.MaxValue &&
                    node.ChildCount == 1 &&
                    IsSupportedSimplePatternNode(node.Child(0));

            case RuntimeFrontEnd.RegexNodeKind.PositiveLookaround:
                return node.ChildCount == 1 &&
                    IsSupportedSimplePatternLookaroundNode(node.Child(0));

            default:
                return false;
        }
    }

    private static bool IsSupportedSimplePatternLookaroundNode(RuntimeFrontEnd.RegexNode node)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Capture:
            case RuntimeFrontEnd.RegexNodeKind.Group:
                return node.ChildCount == 1 && IsSupportedSimplePatternLookaroundNode(node.Child(0));

            case RuntimeFrontEnd.RegexNodeKind.One:
            case RuntimeFrontEnd.RegexNodeKind.Multi:
            case RuntimeFrontEnd.RegexNodeKind.Set:
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!IsSupportedSimplePatternLookaroundNode(node.Child(i)))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    private static bool SupportsNativeOptions(RegexOptions options)
    {
        var allowedOptions =
            RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase |
            RegexOptions.Multiline |
            RegexOptions.Singleline |
            RegexOptions.ExplicitCapture |
            RegexOptions.IgnorePatternWhitespace;
        if ((options & ~allowedOptions) != 0)
        {
            return false;
        }

        return (options & RegexOptions.IgnoreCase) == 0 || (options & RegexOptions.CultureInvariant) != 0;
    }

    private static bool TryExtractLiteralWithPositiveLiteralLookaheadTree(
        Utf8SemanticRegex semanticRegex,
        out string literal,
        out byte[] trailingLiteralUtf8)
    {
        literal = string.Empty;
        trailingLiteralUtf8 = [];

        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryExtractLiteralPrefixWithPositiveLiteralLookahead(root.Child(0), semanticRegex.ExecutionOptions, out literal, out var trailingLiteral) ||
            literal.Length == 0 ||
            !IsNativeLiteralExecutionPattern(semanticRegex.ExecutionPattern, semanticRegex.ExecutionOptions))
        {
            return false;
        }

        trailingLiteralUtf8 = Encoding.UTF8.GetBytes(trailingLiteral);
        return trailingLiteralUtf8.Length > 0;
    }

    private static bool TryExtractLiteralPrefixWithPositiveLiteralLookahead(
        RuntimeFrontEnd.RegexNode body,
        RegexOptions executionOptions,
        out string literal,
        out string trailingLiteral)
    {
        literal = string.Empty;
        trailingLiteral = string.Empty;

        if (body.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            body.ChildCount < 2)
        {
            return false;
        }

        var lookaheadNode = body.Child(body.ChildCount - 1);
        if (lookaheadNode.Kind != RuntimeFrontEnd.RegexNodeKind.PositiveLookaround ||
            lookaheadNode.ChildCount != 1 ||
            !TryGetNativeLookaheadLiteralText(lookaheadNode.Child(0), executionOptions, out trailingLiteral))
        {
            return false;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < body.ChildCount - 1; i++)
        {
            if (!TryGetNativeLookaheadLiteralText(body.Child(i), executionOptions, out var part))
            {
                literal = string.Empty;
                trailingLiteral = string.Empty;
                return false;
            }

            builder.Append(part);
        }

        literal = builder.ToString();
        return literal.Length > 0;
    }

    private static bool TryExtractLiteralAlternationWithPositiveLiteralLookaheadTree(
        Utf8SemanticRegex semanticRegex,
        RegexOptions executionOptions,
        out byte[][] alternateLiteralsUtf8,
        out byte[] trailingLiteralUtf8,
        out NativeExecutionKind executionKind,
        out Utf8SearchKind searchKind)
    {
        alternateLiteralsUtf8 = [];
        trailingLiteralUtf8 = [];
        executionKind = default;
        searchKind = default;

        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryExtractPositiveLiteralLookahead(root.Child(0), executionOptions, out var substantiveNode, out var trailingLiteral) ||
            !TryGetNativeLookaheadAlternationTexts(substantiveNode, executionOptions, out var literalTexts) ||
            literalTexts.Length <= 1 ||
            !IsNativeLiteralExecutionPattern(semanticRegex.ExecutionPattern, semanticRegex.ExecutionOptions))
        {
            return false;
        }

        var encoded = new byte[literalTexts.Length][];
        var hasNonAscii = false;
        for (var i = 0; i < literalTexts.Length; i++)
        {
            var bytes = Encoding.UTF8.GetBytes(literalTexts[i]);
            if (bytes.Length == 0)
            {
                return false;
            }

            if (bytes.Any(static b => b > 0x7F))
            {
                hasNonAscii = true;
            }

            encoded[i] = bytes;
        }

        trailingLiteralUtf8 = Encoding.UTF8.GetBytes(trailingLiteral);
        if (trailingLiteralUtf8.Length == 0)
        {
            return false;
        }

        var ignoreCase = (executionOptions & RegexOptions.IgnoreCase) != 0;
        if (ignoreCase && hasNonAscii)
        {
            return false;
        }

        executionKind = ignoreCase
            ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
            : NativeExecutionKind.ExactUtf8Literals;
        searchKind = ignoreCase
            ? Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals
            : hasNonAscii
                ? Utf8SearchKind.ExactUtf8Literals
                : Utf8SearchKind.ExactAsciiLiterals;
        alternateLiteralsUtf8 = encoded;
        return true;
    }

    private static bool TryExtractPositiveLiteralLookahead(
        RuntimeFrontEnd.RegexNode body,
        RegexOptions executionOptions,
        out RuntimeFrontEnd.RegexNode substantiveNode,
        out string trailingLiteral)
    {
        substantiveNode = default!;
        trailingLiteral = string.Empty;

        if (body.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            body.ChildCount != 2 ||
            body.Child(1).Kind != RuntimeFrontEnd.RegexNodeKind.PositiveLookaround ||
            body.Child(1).ChildCount != 1 ||
            !TryGetNativeLookaheadLiteralText(body.Child(1).Child(0), executionOptions, out trailingLiteral))
        {
            return false;
        }

        substantiveNode = body.Child(0);
        return true;
    }

    private static bool TryGetNativeLookaheadLiteralText(RuntimeFrontEnd.RegexNode node, RegexOptions executionOptions, out string literal)
    {
        if (TryGetLiteralText(node, out literal))
        {
            return true;
        }

        return (executionOptions & RegexOptions.IgnoreCase) != 0 &&
            (executionOptions & RegexOptions.CultureInvariant) != 0 &&
            TryGetInvariantIgnoreCaseAsciiLiteralText(node, out literal);
    }

    private static bool TryGetNativeLookaheadAlternationTexts(RuntimeFrontEnd.RegexNode node, RegexOptions executionOptions, out string[] literals)
    {
        if (TryUnwrapLiteralAlternation(node, out literals))
        {
            return true;
        }

        return (executionOptions & RegexOptions.IgnoreCase) != 0 &&
            (executionOptions & RegexOptions.CultureInvariant) != 0 &&
            TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings(node, out literals);
    }

    private static bool SupportsNativeLiteralOptions(RegexOptions options)
    {
        var allowedOptions =
            RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.RightToLeft;
        if ((options & ~allowedOptions) != 0)
        {
            return false;
        }

        return (options & RegexOptions.IgnoreCase) == 0 || (options & RegexOptions.CultureInvariant) != 0;
    }

    private static bool IsPlainAsciiLiteral(string pattern)
    {
        foreach (var ch in pattern)
        {
            if (ch > 0x7F || Utf8RegexSyntax.IsRegexMetaCharacter(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPlainLiteral(string pattern)
    {
        foreach (var ch in pattern)
        {
            if (Utf8RegexSyntax.IsRegexMetaCharacter(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryExtractSingleCapturedLiteralPattern(string pattern, out string literal)
    {
        if (pattern.Length >= 3 &&
            pattern[0] == '(' &&
            pattern[^1] == ')')
        {
            var inner = pattern[1..^1];
            if (IsPlainLiteral(inner))
            {
                literal = inner;
                return true;
            }
        }

        literal = string.Empty;
        return false;
    }

    private static bool TryExtractUtf8LiteralAlternationTree(
        Utf8SemanticRegex semanticRegex,
        out byte[][] alternateLiteralsUtf8,
        out Utf8BoundaryRequirement leadingBoundary,
        out Utf8BoundaryRequirement trailingBoundary)
    {
        alternateLiteralsUtf8 = [];
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;
        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1)
        {
            return false;
        }

        if (!TryUnwrapLiteralAlternationWithBoundaries(root.Child(0), out var literalTexts, out leadingBoundary, out trailingBoundary) ||
            literalTexts.Length <= 1)
        {
            return false;
        }

        var hasNonAscii = false;
        alternateLiteralsUtf8 = new byte[literalTexts.Length][];
        for (var i = 0; i < literalTexts.Length; i++)
        {
            var literal = literalTexts[i];
            if (literal.Length == 0)
            {
                alternateLiteralsUtf8 = [];
                leadingBoundary = Utf8BoundaryRequirement.None;
                trailingBoundary = Utf8BoundaryRequirement.None;
                return false;
            }

            if (!hasNonAscii)
            {
                foreach (var ch in literal)
                {
                    if (ch > 0x7F)
                    {
                        hasNonAscii = true;
                        break;
                    }
                }
            }

            alternateLiteralsUtf8[i] = Encoding.UTF8.GetBytes(literal);
        }

        return true;
    }

    private static bool TryExtractPlainAsciiLiteralAlternationPattern(string pattern, out byte[][] alternateLiteralsUtf8)
    {
        alternateLiteralsUtf8 = [];
        if (string.IsNullOrEmpty(pattern) || pattern[0] == '|' || pattern[^1] == '|')
        {
            return false;
        }

        var parts = pattern.Split('|');
        if (parts.Length <= 1)
        {
            return false;
        }

        alternateLiteralsUtf8 = new byte[parts.Length][];
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part) || !IsPlainLiteral(part) || !IsPlainAsciiLiteral(part))
            {
                alternateLiteralsUtf8 = [];
                return false;
            }

            alternateLiteralsUtf8[i] = Encoding.UTF8.GetBytes(part);
        }

        return true;
    }

    private static bool TryUnwrapLiteralAlternationWithBoundaries(
        RuntimeFrontEnd.RegexNode node,
        out string[] literals,
        out Utf8BoundaryRequirement leadingBoundary,
        out Utf8BoundaryRequirement trailingBoundary)
    {
        literals = [];
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;

        if (TryUnwrapLiteralAlternation(node, out literals))
        {
            return true;
        }

        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate)
        {
            return false;
        }

        if (node.ChildCount == 2)
        {
            if (TryGetBoundaryRequirement(node.Child(0).Kind, out leadingBoundary) &&
                TryUnwrapLiteralAlternation(node.Child(1), out literals))
            {
                return true;
            }

            if (TryUnwrapLiteralAlternation(node.Child(0), out literals) &&
                TryGetBoundaryRequirement(node.Child(1).Kind, out trailingBoundary))
            {
                return true;
            }
        }
        else if (node.ChildCount == 3 &&
            TryGetBoundaryRequirement(node.Child(0).Kind, out leadingBoundary) &&
            TryUnwrapLiteralAlternation(node.Child(1), out literals) &&
            TryGetBoundaryRequirement(node.Child(2).Kind, out trailingBoundary))
        {
            return true;
        }

        literals = [];
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;
        return false;
    }

    private static bool TryUnwrapLiteralAlternation(RuntimeFrontEnd.RegexNode node, out string[] literals)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Capture:
            case RuntimeFrontEnd.RegexNodeKind.Group:
                if (node.ChildCount == 1)
                {
                    return TryUnwrapLiteralAlternation(node.Child(0), out literals);
                }

                break;

            case RuntimeFrontEnd.RegexNodeKind.Alternate:
                literals = new string[node.ChildCount];
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!TryGetLiteralBranchText(node.Child(i), out literals[i]))
                    {
                        literals = [];
                        return false;
                    }
                }

                return true;
        }

        literals = [];
        return false;
    }

    private static bool TryGetLiteralBranchText(RuntimeFrontEnd.RegexNode node, out string literal)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Capture:
            case RuntimeFrontEnd.RegexNodeKind.Group:
                if (node.ChildCount == 1)
                {
                    return TryGetLiteralBranchText(node.Child(0), out literal);
                }

                break;

            case RuntimeFrontEnd.RegexNodeKind.One:
            case RuntimeFrontEnd.RegexNodeKind.Multi:
                return TryGetLiteralText(node, out literal);
        }

        literal = string.Empty;
        return false;
    }

    private static bool TryExtractBoundaryWrappedLiteralTree(
        Utf8SemanticRegex semanticRegex,
        out string literal,
        out Utf8BoundaryRequirement leadingBoundary,
        out Utf8BoundaryRequirement trailingBoundary)
    {
        literal = string.Empty;
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;

        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1)
        {
            return false;
        }

        var body = root.Child(0);
        if (body.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate)
        {
            return false;
        }

        if (!TryExtractBoundaryWrappedLiteral(body, out literal, out leadingBoundary, out trailingBoundary))
        {
            leadingBoundary = Utf8BoundaryRequirement.None;
            trailingBoundary = Utf8BoundaryRequirement.None;
            literal = string.Empty;
            return false;
        }

        return literal.Length > 0 && IsNativeLiteralExecutionPattern(semanticRegex.ExecutionPattern, semanticRegex.ExecutionOptions);
    }

    private static bool TryExtractBoundaryWrappedLiteral(
        RuntimeFrontEnd.RegexNode body,
        out string literal,
        out Utf8BoundaryRequirement leadingBoundary,
        out Utf8BoundaryRequirement trailingBoundary)
    {
        literal = string.Empty;
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;

        if (body.ChildCount == 3)
        {
            return TryGetBoundaryRequirement(body.Child(0).Kind, out leadingBoundary) &&
                TryGetLiteralText(body.Child(1), out literal) &&
                TryGetBoundaryRequirement(body.Child(2).Kind, out trailingBoundary);
        }

        if (body.ChildCount != 2)
        {
            return false;
        }

        if (TryGetBoundaryRequirement(body.Child(0).Kind, out leadingBoundary) &&
            TryGetLiteralText(body.Child(1), out literal))
        {
            return true;
        }

        if (TryGetLiteralText(body.Child(0), out literal) &&
            TryGetBoundaryRequirement(body.Child(1).Kind, out trailingBoundary))
        {
            return true;
        }

        literal = string.Empty;
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;
        return false;
    }

    private static bool TryGetBoundaryRequirement(RuntimeFrontEnd.RegexNodeKind kind, out Utf8BoundaryRequirement requirement)
    {
        requirement = kind switch
        {
            RuntimeFrontEnd.RegexNodeKind.Boundary => Utf8BoundaryRequirement.Boundary,
            RuntimeFrontEnd.RegexNodeKind.NonBoundary => Utf8BoundaryRequirement.NonBoundary,
            _ => Utf8BoundaryRequirement.None,
        };

        return requirement != Utf8BoundaryRequirement.None;
    }

    private static bool TryAnalyzeAsciiStructuralIdentifierFamilyTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8AnalyzedRegex analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryExtractAsciiStructuralIdentifierFamily(root.Child(0), out var prefixes, out var separatorSet, out var separatorMinCount, out var identifierStartSet, out var identifierTailSet, out var identifierTailMinCount, out var identifierTailMaxCount, out var suffixUtf8, out var suffixParts, out var leadingBoundary, out var trailingBoundary) ||
            prefixes.Length <= 1)
        {
            return false;
        }

        var plan = new AsciiStructuralIdentifierFamilyPlan(
            prefixes,
            separatorSet,
            separatorMinCount,
            identifierStartSet,
            identifierTailSet,
            identifierTailMinCount,
            identifierTailMaxCount,
            suffixUtf8,
            suffixParts,
            leadingBoundary,
            trailingBoundary);

        analyzedRegex = new Utf8AnalyzedRegex(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            new Utf8AnalyzedSearchInfo(
                Utf8SearchKind.ExactAsciiLiterals,
                null,
                prefixes,
                leadingBoundary: leadingBoundary),
            NativeExecutionKind.AsciiStructuralIdentifierFamily,
            asyncIdentifierFamilyPlan: plan);
        return true;
    }

    private static bool TryExtractAsciiStructuralIdentifierFamily(
        RuntimeFrontEnd.RegexNode node,
        out byte[][] prefixes,
        out string? separatorSet,
        out int separatorMinCount,
        out string identifierStartSet,
        out string identifierTailSet,
        out int identifierTailMinCount,
        out int identifierTailMaxCount,
        out byte[]? suffixUtf8,
        out AsciiStructuralSuffixPart[] suffixParts,
        out Utf8BoundaryRequirement leadingBoundary,
        out Utf8BoundaryRequirement trailingBoundary)
    {
        prefixes = [];
        separatorSet = null;
        separatorMinCount = 0;
        identifierStartSet = string.Empty;
        identifierTailSet = string.Empty;
        identifierTailMinCount = 0;
        identifierTailMaxCount = 0;
        suffixUtf8 = null;
        suffixParts = [];
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;

        node = UnwrapRuntimeNode(node);
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate)
        {
            return false;
        }

        var startChildIndex = 0;
        var endChildIndex = node.ChildCount - 1;
        if (startChildIndex <= endChildIndex &&
            TryGetBoundaryRequirement(node.Child(startChildIndex).Kind, out leadingBoundary))
        {
            startChildIndex++;
        }

        if (startChildIndex <= endChildIndex &&
            TryGetBoundaryRequirement(node.Child(endChildIndex).Kind, out trailingBoundary))
        {
            endChildIndex--;
        }

        if (!TryExtractLiteralFamily(node.Child(startChildIndex), out prefixes))
        {
            return false;
        }

        var currentIndex = startChildIndex + 1;
        if (currentIndex <= endChildIndex &&
            TryGetIdentifierSeparatorLoop(node.Child(currentIndex), out var separatorCandidateSet, out var separatorCandidateMinCount))
        {
            separatorSet = separatorCandidateSet;
            separatorMinCount = separatorCandidateMinCount;
            currentIndex++;
        }

        if (currentIndex > endChildIndex)
        {
            return (leadingBoundary != Utf8BoundaryRequirement.None ||
                    trailingBoundary != Utf8BoundaryRequirement.None) &&
                   GetCommonPrefixLength(prefixes) >= 3;
        }

        if (!TryGetAsciiSet(node.Child(currentIndex), out identifierStartSet))
        {
            if (separatorSet is not null &&
                TryExtractAsciiStructuralSuffixParts(node, currentIndex, endChildIndex, out suffixParts))
            {
                if (suffixParts.Length == 1 && suffixParts[0].IsLiteral)
                {
                    suffixUtf8 = suffixParts[0].LiteralUtf8;
                }

                return true;
            }

            ResetAsciiStructuralIdentifierFamilyExtraction(out prefixes, out separatorSet, out separatorMinCount, out identifierStartSet, out identifierTailSet, out identifierTailMinCount, out identifierTailMaxCount, out suffixUtf8, out suffixParts, out leadingBoundary, out trailingBoundary);
            return false;
        }

        currentIndex++;
        if (currentIndex > endChildIndex ||
            !TryGetAsciiBoundedSetLoop(node.Child(currentIndex), out identifierTailSet, out identifierTailMinCount, out identifierTailMaxCount))
        {
            ResetAsciiStructuralIdentifierFamilyExtraction(out prefixes, out separatorSet, out separatorMinCount, out identifierStartSet, out identifierTailSet, out identifierTailMinCount, out identifierTailMaxCount, out suffixUtf8, out suffixParts, out leadingBoundary, out trailingBoundary);
            return false;
        }

        currentIndex++;
        if (currentIndex <= endChildIndex)
        {
            if (!TryExtractAsciiStructuralSuffixParts(node, currentIndex, endChildIndex, out suffixParts))
            {
                ResetAsciiStructuralIdentifierFamilyExtraction(out prefixes, out separatorSet, out separatorMinCount, out identifierStartSet, out identifierTailSet, out identifierTailMinCount, out identifierTailMaxCount, out suffixUtf8, out suffixParts, out leadingBoundary, out trailingBoundary);
                return false;
            }

            if (suffixParts.Length == 1 && suffixParts[0].IsLiteral)
            {
                suffixUtf8 = suffixParts[0].LiteralUtf8;
            }
        }

        return true;
    }

    private static bool TryAnalyzeAsciiStructuralTokenWindowTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8AnalyzedRegex analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryExtractAsciiStructuralTokenWindow(root.Child(0), out var plan))
        {
            return false;
        }

        analyzedRegex = new Utf8AnalyzedRegex(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            new Utf8AnalyzedSearchInfo(
                Utf8SearchKind.ExactAsciiLiteral,
                literalUtf8: plan.AnchorLiteralUtf8,
                minRequiredLength: plan.LeadingLength + plan.SeparatorMinCount + plan.AnchorLiteralUtf8.Length + plan.SeparatorMinCount + plan.TrailingLength),
            NativeExecutionKind.AsciiStructuralTokenWindow,
            tokenWindowPlan: plan,
            literalUtf8: plan.AnchorLiteralUtf8);
        return true;
    }

    private static void ResetAsciiStructuralIdentifierFamilyExtraction(
        out byte[][] prefixes,
        out string? separatorSet,
        out int separatorMinCount,
        out string identifierStartSet,
        out string identifierTailSet,
        out int identifierTailMinCount,
        out int identifierTailMaxCount,
        out byte[]? suffixUtf8,
        out AsciiStructuralSuffixPart[] suffixParts,
        out Utf8BoundaryRequirement leadingBoundary,
        out Utf8BoundaryRequirement trailingBoundary)
    {
        prefixes = [];
        separatorSet = null;
        separatorMinCount = 0;
        identifierStartSet = string.Empty;
        identifierTailSet = string.Empty;
        identifierTailMinCount = 0;
        identifierTailMaxCount = 0;
        suffixUtf8 = null;
        suffixParts = [];
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;
    }

    private static int GetCommonPrefixLength(byte[][] prefixes)
    {
        if (prefixes.Length == 0)
        {
            return 0;
        }

        var commonPrefixLength = prefixes[0].Length;
        for (var i = 1; i < prefixes.Length && commonPrefixLength > 0; i++)
        {
            commonPrefixLength = Math.Min(commonPrefixLength, prefixes[i].Length);
            var prefix = prefixes[i];
            var matchLength = 0;
            while (matchLength < commonPrefixLength &&
                   prefixes[0][matchLength] == prefix[matchLength])
            {
                matchLength++;
            }

            commonPrefixLength = matchLength;
        }

        return commonPrefixLength;
    }

    private static bool TryExtractAsciiStructuralTokenWindow(
        RuntimeFrontEnd.RegexNode node,
        out AsciiStructuralTokenWindowPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            node.ChildCount != 7 ||
            !TryGetExactAsciiSetLoop(node.Child(0), out var leadingSet, out var leadingLength) ||
            !TryGetIdentifierSeparatorLoop(node.Child(1), out var separatorSet, out var separatorMinCount) ||
            !TryGetBoundedAnyTextLoop(node.Child(2), out var leadingGapMax) ||
            !TryGetAsciiLiteral(node.Child(3), out var anchorLiteralUtf8) ||
            !TryGetBoundedAnyTextLoop(node.Child(4), out var trailingGapMax) ||
            !TryGetIdentifierSeparatorLoop(node.Child(5), out var trailingSeparatorSet, out var trailingSeparatorMinCount) ||
            !TryGetExactAsciiSetLoop(node.Child(6), out var trailingSet, out var trailingLength) ||
            separatorSet != trailingSeparatorSet ||
            separatorMinCount != trailingSeparatorMinCount ||
            !TryCreateAsciiCharClass(leadingSet, out var leadingCharClass) ||
            !TryCreateAsciiCharClass(trailingSet, out var trailingCharClass))
        {
            return false;
        }

        plan = new AsciiStructuralTokenWindowPlan(
            leadingCharClass,
            leadingLength,
            separatorSet,
            separatorMinCount,
            leadingGapMax,
            anchorLiteralUtf8,
            trailingGapMax,
            trailingCharClass,
            trailingLength);
        return true;
    }

    private static bool TryAnalyzeAsciiOrderedLiteralWindowTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8AnalyzedRegex analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1 ||
            (!TryExtractOrderedLiteralWindow(root.Child(0), out var plan) &&
             !TryExtractSymmetricAlternateOrderedLiteralWindow(root.Child(0), out plan) &&
             !TryExtractOrderedLiteralLookaheadWindow(root.Child(0), out plan)))
        {
            return false;
        }

        var searchInfo = plan.IsLiteralFamily
            ? new Utf8AnalyzedSearchInfo(
                Utf8SearchKind.ExactAsciiLiterals,
                alternateLiteralsUtf8: plan.LeadingLiteralsUtf8,
                canGuideFallbackStarts: true,
                minRequiredLength: plan.LeadingLiteralUtf8.Length + plan.TrailingLiteralUtf8.Length)
            : new Utf8AnalyzedSearchInfo(
                Utf8SearchKind.ExactAsciiLiteral,
                literalUtf8: plan.LeadingLiteralUtf8,
                canGuideFallbackStarts: true,
                minRequiredLength: plan.LeadingLiteralUtf8.Length + plan.TrailingLiteralUtf8.Length);

        analyzedRegex = new Utf8AnalyzedRegex(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            searchInfo,
            NativeExecutionKind.AsciiOrderedLiteralWindow,
            orderedLiteralWindowPlan: plan,
            literalUtf8: plan.IsLiteralFamily ? null : plan.LeadingLiteralUtf8);
        return true;
    }

    private static bool TryExtractOrderedLiteralWindow(
        RuntimeFrontEnd.RegexNode node,
        out AsciiOrderedLiteralWindowPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate)
        {
            return false;
        }

        // Match: \b? Literal1 \b? \s*? Gap{0,N} \b? Literal2 \b?
        var index = 0;
        var childCount = node.ChildCount;

        var leadingBoundary1 = Utf8BoundaryRequirement.None;
        if (index < childCount &&
            TryGetBoundaryRequirement(node.Child(index).Kind, out leadingBoundary1))
        {
            index++;
        }

        byte[][]? leadingLiterals = null;
        byte[] leadingLiteral;
        if (index < childCount && TryGetAsciiLiteral(node.Child(index), out leadingLiteral))
        {
            index++;
        }
        else if (index < childCount && TryGetAsciiLiteralAlternation(node.Child(index), out var alternates) && alternates.Length >= 2)
        {
            leadingLiterals = alternates;
            leadingLiteral = alternates.OrderBy(static a => a.Length).First();
            index++;
        }
        else
        {
            return false;
        }

        var trailingBoundary1 = Utf8BoundaryRequirement.None;
        if (index < childCount &&
            TryGetBoundaryRequirement(node.Child(index).Kind, out trailingBoundary1))
        {
            index++;
        }

        // Optional whitespace separator before the gap (\s+ or \s*).
        var gapLeadingSeparatorMinCount = 0;
        var hasSeparator = false;
        if (index < childCount &&
            TryGetIdentifierSeparatorLoop(node.Child(index), out _, out var sepMin))
        {
            gapLeadingSeparatorMinCount = sepMin;
            hasSeparator = true;
            index++;
        }

        // Bounded gap (e.g., .{0,80}). Optional when a separator was found —
        // the pattern can be Literal + \s* + Literal with no bounded repeat.
        var maxGap = 0;
        var gapSameLine = false;
        if (index < childCount &&
            TryGetBoundedGapLoop(node.Child(index), out maxGap, out gapSameLine))
        {
            index++;
        }
        else if (!hasSeparator)
        {
            return false;
        }
        else
        {
            // Separator-only path requires at least \s+ (min >= 1) to avoid matching
            // patterns like a.*b where .* could be misclassified as a separator.
            if (gapLeadingSeparatorMinCount < 1)
            {
                return false;
            }
        }

        var leadingBoundary2 = Utf8BoundaryRequirement.None;
        if (index < childCount &&
            TryGetBoundaryRequirement(node.Child(index).Kind, out leadingBoundary2))
        {
            index++;
        }

        if (index >= childCount || !TryGetAsciiLiteral(node.Child(index), out var trailingLiteral))
        {
            return false;
        }

        index++;

        var trailingBoundary2 = Utf8BoundaryRequirement.None;
        if (index < childCount &&
            TryGetBoundaryRequirement(node.Child(index).Kind, out trailingBoundary2))
        {
            index++;
        }

        if (index != childCount)
        {
            return false;
        }

        plan = new AsciiOrderedLiteralWindowPlan(
            leadingLiteral,
            leadingLiterals,
            trailingLiteral,
            trailingLiteralsUtf8: null,
            maxGap,
            gapSameLine,
            gapLeadingSeparatorMinCount,
            yieldLeadingLiteralOnly: false,
            leadingBoundary1,
            trailingBoundary1,
            leadingBoundary2,
            trailingBoundary2);
        return true;
    }

    private static bool TryExtractOrderedLiteralLookaheadWindow(
        RuntimeFrontEnd.RegexNode node,
        out AsciiOrderedLiteralWindowPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            node.ChildCount != 2 ||
            !TryGetAsciiLiteral(node.Child(0), out var leadingLiteral))
        {
            return false;
        }

        var lookahead = UnwrapRuntimeNode(node.Child(1));
        if (lookahead.Kind != RuntimeFrontEnd.RegexNodeKind.PositiveLookaround ||
            lookahead.ChildCount != 1 ||
            !TryExtractSeparatorLiteralLookahead(lookahead.Child(0), out var separatorMinCount, out var trailingLiteral))
        {
            return false;
        }

        plan = new AsciiOrderedLiteralWindowPlan(
            leadingLiteral,
            leadingLiteralsUtf8: null,
            trailingLiteral,
            trailingLiteralsUtf8: null,
            maxGap: 0,
            gapSameLine: false,
            gapLeadingSeparatorMinCount: separatorMinCount,
            yieldLeadingLiteralOnly: true,
            Utf8BoundaryRequirement.None,
            Utf8BoundaryRequirement.None,
            Utf8BoundaryRequirement.None,
            Utf8BoundaryRequirement.None);
        return true;
    }

    private static bool TryExtractSeparatorLiteralLookahead(
        RuntimeFrontEnd.RegexNode node,
        out int separatorMinCount,
        out byte[] trailingLiteralUtf8)
    {
        separatorMinCount = 0;
        trailingLiteralUtf8 = [];
        node = UnwrapRuntimeNode(node);
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            node.ChildCount != 2 ||
            !TryGetIdentifierSeparatorLoop(node.Child(0), out _, out separatorMinCount) ||
            !TryGetAsciiLiteral(node.Child(1), out trailingLiteralUtf8))
        {
            separatorMinCount = 0;
            trailingLiteralUtf8 = [];
            return false;
        }

        return separatorMinCount > 0;
    }

    private static bool TryExtractSymmetricAlternateOrderedLiteralWindow(
        RuntimeFrontEnd.RegexNode node,
        out AsciiOrderedLiteralWindowPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Alternate || node.ChildCount < 2)
        {
            return false;
        }

        var leadingLiterals = new byte[node.ChildCount][];
        var trailingLiterals = new byte[node.ChildCount][];
        Utf8BoundaryRequirement? leadingBoundary1 = null;
        Utf8BoundaryRequirement? trailingBoundary1 = null;
        Utf8BoundaryRequirement? leadingBoundary2 = null;
        Utf8BoundaryRequirement? trailingBoundary2 = null;
        int? separatorMin = null;
        int? maxGap = null;
        bool? gapSameLine = null;

        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryExtractOrderedLiteralWindowBranch(
                node.Child(i),
                out leadingLiterals[i],
                out trailingLiterals[i],
                out var branchMaxGap,
                out var branchGapSameLine,
                out var branchSeparatorMin,
                out var branchLeadingBoundary1,
                out var branchTrailingBoundary1,
                out var branchLeadingBoundary2,
                out var branchTrailingBoundary2))
            {
                return false;
            }

            leadingBoundary1 ??= branchLeadingBoundary1;
            trailingBoundary1 ??= branchTrailingBoundary1;
            leadingBoundary2 ??= branchLeadingBoundary2;
            trailingBoundary2 ??= branchTrailingBoundary2;
            separatorMin ??= branchSeparatorMin;
            maxGap ??= branchMaxGap;
            gapSameLine ??= branchGapSameLine;

            if (leadingBoundary1 != branchLeadingBoundary1 ||
                trailingBoundary1 != branchTrailingBoundary1 ||
                leadingBoundary2 != branchLeadingBoundary2 ||
                trailingBoundary2 != branchTrailingBoundary2 ||
                separatorMin != branchSeparatorMin ||
                maxGap != branchMaxGap ||
                gapSameLine != branchGapSameLine)
            {
                return false;
            }
        }

        for (var i = 0; i < leadingLiterals.Length; i++)
        {
            for (var j = i + 1; j < leadingLiterals.Length; j++)
            {
                if (leadingLiterals[i].AsSpan().SequenceEqual(leadingLiterals[j]))
                {
                    return false;
                }
            }
        }

        var shortestLeading = leadingLiterals.OrderBy(static l => l.Length).First();
        var shortestTrailing = trailingLiterals.OrderBy(static l => l.Length).First();
        plan = new AsciiOrderedLiteralWindowPlan(
            shortestLeading,
            leadingLiterals,
            shortestTrailing,
            trailingLiterals,
            maxGap!.Value,
            gapSameLine!.Value,
            separatorMin!.Value,
            yieldLeadingLiteralOnly: false,
            leadingBoundary1!.Value,
            trailingBoundary1!.Value,
            leadingBoundary2!.Value,
            trailingBoundary2!.Value);
        return true;
    }

    private static bool TryExtractOrderedLiteralWindowBranch(
        RuntimeFrontEnd.RegexNode node,
        out byte[] leadingLiteral,
        out byte[] trailingLiteral,
        out int maxGap,
        out bool gapSameLine,
        out int gapLeadingSeparatorMinCount,
        out Utf8BoundaryRequirement leadingBoundary1,
        out Utf8BoundaryRequirement trailingBoundary1,
        out Utf8BoundaryRequirement leadingBoundary2,
        out Utf8BoundaryRequirement trailingBoundary2)
    {
        leadingLiteral = [];
        trailingLiteral = [];
        maxGap = 0;
        gapSameLine = false;
        gapLeadingSeparatorMinCount = 0;
        leadingBoundary1 = Utf8BoundaryRequirement.None;
        trailingBoundary1 = Utf8BoundaryRequirement.None;
        leadingBoundary2 = Utf8BoundaryRequirement.None;
        trailingBoundary2 = Utf8BoundaryRequirement.None;

        node = UnwrapRuntimeNode(node);
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate)
        {
            return false;
        }

        var index = 0;
        var childCount = node.ChildCount;
        if (index < childCount &&
            TryGetBoundaryRequirement(node.Child(index).Kind, out leadingBoundary1))
        {
            index++;
        }

        if (index >= childCount || !TryGetAsciiLiteral(node.Child(index), out leadingLiteral))
        {
            return false;
        }

        index++;
        if (index < childCount &&
            TryGetBoundaryRequirement(node.Child(index).Kind, out trailingBoundary1))
        {
            index++;
        }

        if (index < childCount &&
            TryGetIdentifierSeparatorLoop(node.Child(index), out _, out var sepMin))
        {
            gapLeadingSeparatorMinCount = sepMin;
            index++;
        }

        if (index >= childCount ||
            !TryGetBoundedGapLoop(node.Child(index), out maxGap, out gapSameLine))
        {
            return false;
        }

        index++;
        if (index < childCount &&
            TryGetBoundaryRequirement(node.Child(index).Kind, out leadingBoundary2))
        {
            index++;
        }

        if (index >= childCount || !TryGetAsciiLiteral(node.Child(index), out trailingLiteral))
        {
            return false;
        }

        index++;
        if (index < childCount &&
            TryGetBoundaryRequirement(node.Child(index).Kind, out trailingBoundary2))
        {
            index++;
        }

        return index == childCount;
    }

    private static bool TryAnalyzeAsciiStructuralRepeatedSegmentTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8AnalyzedRegex analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryExtractAsciiStructuralRepeatedSegment(root.Child(0), out var plan))
        {
            return false;
        }

        analyzedRegex = new Utf8AnalyzedRegex(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            new Utf8AnalyzedSearchInfo(
                Utf8SearchKind.None,
                minRequiredLength: plan.RepetitionMinCount * (1 + plan.TrailingMinCount + plan.SeparatorMinCount)),
            NativeExecutionKind.AsciiStructuralRepeatedSegment,
            repeatedSegmentPlan: plan);
        return true;
    }

    private static bool TryAnalyzeAsciiStructuralQuotedRelationTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8AnalyzedRegex analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryExtractAsciiStructuralQuotedRelation(root.Child(0), out var plan))
        {
            return false;
        }

        analyzedRegex = new Utf8AnalyzedRegex(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            new Utf8AnalyzedSearchInfo(Utf8SearchKind.None),
            NativeExecutionKind.AsciiStructuralQuotedRelation,
            quotedRelationPlan: plan);
        return true;
    }

    private static bool TryExtractAsciiStructuralRepeatedSegment(
        RuntimeFrontEnd.RegexNode node,
        out AsciiStructuralRepeatedSegmentPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind is not RuntimeFrontEnd.RegexNodeKind.Loop and not RuntimeFrontEnd.RegexNodeKind.Lazyloop ||
            node.ChildCount != 1 ||
            node.M <= 0 ||
            node.N < node.M ||
            node.N == int.MaxValue)
        {
            return false;
        }

        var segment = UnwrapRuntimeNode(node.Child(0));
        if (segment.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            segment.ChildCount != 3 ||
            !TryGetExactAsciiSetAtom(segment.Child(0), out var leadingSet, out var leadingLength) ||
            leadingLength != 1 ||
            !TryGetSetLoop(segment.Child(1), out var trailingSetNode, out var trailingMin, out var trailingMax) ||
            trailingSetNode.Str is null ||
            trailingMin <= 0 ||
            trailingMax != int.MaxValue ||
            !RuntimeFrontEnd.RegexCharClass.IsAscii(trailingSetNode.Str) ||
            !TryGetSetLoop(segment.Child(2), out var separatorSetNode, out var separatorMin, out var separatorMax) ||
            separatorSetNode.Str is null ||
            separatorMin < 0 ||
            separatorMax != int.MaxValue ||
            !TryCreateAsciiCharClass(leadingSet, out var leadingCharClass) ||
            !TryCreateAsciiCharClass(trailingSetNode.Str, out var trailingCharClass) ||
            !TryGetAsciiSeparatorSet(separatorSetNode.Str, out var separatorSet))
        {
            return false;
        }

        plan = new AsciiStructuralRepeatedSegmentPlan(
            leadingCharClass,
            trailingCharClass,
            trailingMin,
            separatorSet,
            separatorMin,
            node.M,
            node.N);
        return true;
    }

    private static bool TryExtractAsciiStructuralQuotedRelation(
        RuntimeFrontEnd.RegexNode node,
        out AsciiStructuralQuotedRelationPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind is RuntimeFrontEnd.RegexNodeKind.Loop or RuntimeFrontEnd.RegexNodeKind.Lazyloop &&
            node.ChildCount == 1 &&
            node.M >= 1 &&
            node.N == int.MaxValue)
        {
            node = UnwrapRuntimeNode(node.Child(0));
        }

        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Alternate ||
            node.ChildCount != 2 ||
            !TryExtractAsciiStructuralQuotedRelationBranch(node.Child(0), out var firstBranch, out var firstPrefixes, out var firstTailSet, out var firstTailLength, out var firstRunSet, out var firstRunLength) ||
            !TryExtractAsciiStructuralQuotedRelationBranch(node.Child(1), out var secondBranch, out var secondPrefixes, out var secondTailSet, out var secondTailLength, out var secondRunSet, out var secondRunLength) ||
            !PrefixesEqual(firstPrefixes, secondPrefixes) ||
            firstTailSet != secondTailSet ||
            firstTailLength != secondTailLength ||
            firstRunSet != secondRunSet ||
            firstRunLength != secondRunLength ||
            !TryCreateAsciiCharClass(firstTailSet, out var prefixedTailClass) ||
            !TryCreateAsciiCharClass(firstRunSet, out var quotedRunClass))
        {
            return false;
        }

        plan = new AsciiStructuralQuotedRelationPlan(
            firstPrefixes,
            prefixedTailClass,
            firstTailLength,
            quotedRunClass,
            firstRunLength,
            firstBranch,
            secondBranch);
        return true;
    }

    private static bool TryExtractAsciiStructuralQuotedRelationBranch(
        RuntimeFrontEnd.RegexNode node,
        out AsciiStructuralQuotedRelationBranchPlan branch,
        out byte[][] prefixesUtf8,
        out string prefixedTailSet,
        out int prefixedTailLength,
        out string quotedRunSet,
        out int quotedRunLength)
    {
        branch = default;
        prefixesUtf8 = [];
        prefixedTailSet = string.Empty;
        prefixedTailLength = 0;
        quotedRunSet = string.Empty;
        quotedRunLength = 0;

        node = UnwrapRuntimeNode(node);
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            node.ChildCount < 3 ||
            !TryExtractQuotedRelationOperandAtEdge(
                node,
                fromStart: true,
                out var leadingConsumeCount,
                out var leadingKind,
                out var leadingRepeat,
                out prefixesUtf8,
                out prefixedTailSet,
                out prefixedTailLength,
                out quotedRunSet,
                out quotedRunLength) ||
            !TryExtractQuotedRelationOperandAtEdge(
                node,
                fromStart: false,
                out var trailingConsumeCount,
                out var trailingKind,
                out var trailingRepeat,
                out var trailingPrefixes,
                out var trailingTailSet,
                out var trailingTailLength,
                out var trailingRunSet,
                out var trailingRunLength) ||
            !TryGetFiniteLineBreakCount(node, leadingConsumeCount, node.ChildCount - trailingConsumeCount - 1, out var maxLineBreaks))
        {
            return false;
        }

        if ((leadingKind == AsciiStructuralQuotedOperandKind.QuotedPrefixedRun &&
             (!PrefixesEqual(prefixesUtf8, trailingPrefixes) && trailingKind == AsciiStructuralQuotedOperandKind.QuotedPrefixedRun ||
              (trailingKind == AsciiStructuralQuotedOperandKind.QuotedPrefixedRun && (prefixedTailSet != trailingTailSet || prefixedTailLength != trailingTailLength)))) ||
            (leadingKind == AsciiStructuralQuotedOperandKind.QuotedAsciiRun &&
             trailingKind == AsciiStructuralQuotedOperandKind.QuotedAsciiRun &&
             (quotedRunSet != trailingRunSet || quotedRunLength != trailingRunLength)))
        {
            return false;
        }

        if (leadingKind == AsciiStructuralQuotedOperandKind.QuotedAsciiRun)
        {
            if (trailingKind != AsciiStructuralQuotedOperandKind.QuotedPrefixedRun)
            {
                return false;
            }

            prefixesUtf8 = trailingPrefixes;
            prefixedTailSet = trailingTailSet;
            prefixedTailLength = trailingTailLength;
        }
        else if (trailingKind == AsciiStructuralQuotedOperandKind.QuotedAsciiRun)
        {
            quotedRunSet = trailingRunSet;
            quotedRunLength = trailingRunLength;
        }

        branch = new AsciiStructuralQuotedRelationBranchPlan(
            leadingKind,
            leadingRepeat,
            trailingKind,
            trailingRepeat,
            maxLineBreaks);
        return true;
    }

    private static bool TryExtractQuotedRelationOperandAtEdge(
        RuntimeFrontEnd.RegexNode concatenateNode,
        bool fromStart,
        out int consumeCount,
        out AsciiStructuralQuotedOperandKind kind,
        out bool allowRepeat,
        out byte[][] prefixesUtf8,
        out string prefixedTailSet,
        out int prefixedTailLength,
        out string quotedRunSet,
        out int quotedRunLength)
    {
        consumeCount = 0;
        kind = AsciiStructuralQuotedOperandKind.None;
        allowRepeat = false;
        prefixesUtf8 = [];
        prefixedTailSet = string.Empty;
        prefixedTailLength = 0;
        quotedRunSet = string.Empty;
        quotedRunLength = 0;

        var singleIndex = fromStart ? 0 : concatenateNode.ChildCount - 1;
        if (TryExtractQuotedRelationOperand(
            concatenateNode.Child(singleIndex),
            out kind,
            out allowRepeat,
            out prefixesUtf8,
            out prefixedTailSet,
            out prefixedTailLength,
            out quotedRunSet,
            out quotedRunLength))
        {
            consumeCount = 1;
            return true;
        }

        if (concatenateNode.ChildCount < 3)
        {
            return false;
        }

        var spanStart = fromStart ? 0 : concatenateNode.ChildCount - 3;
        if (!TryCreateConcatenateSlice(concatenateNode, spanStart, 3, out var slice) ||
            !TryExtractQuotedRelationOperand(
                slice,
                out kind,
                out allowRepeat,
                out prefixesUtf8,
                out prefixedTailSet,
                out prefixedTailLength,
                out quotedRunSet,
                out quotedRunLength))
        {
            return false;
        }

        consumeCount = 3;
        return true;
    }

    private static bool TryCreateConcatenateSlice(
        RuntimeFrontEnd.RegexNode concatenateNode,
        int startIndex,
        int count,
        out RuntimeFrontEnd.RegexNode slice)
    {
        slice = default!;
        concatenateNode = UnwrapRuntimeNode(concatenateNode);
        if (concatenateNode.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            count <= 0 ||
            startIndex < 0 ||
            startIndex + count > concatenateNode.ChildCount)
        {
            return false;
        }

        slice = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Concatenate, concatenateNode.Options);
        for (var i = 0; i < count; i++)
        {
            slice.AddChild(concatenateNode.Child(startIndex + i));
        }

        return true;
    }

    private static bool TryGetFiniteLineBreakCount(
        RuntimeFrontEnd.RegexNode node,
        int startChildIndex,
        int endChildIndex,
        out int maxLineBreaks)
    {
        maxLineBreaks = 0;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            startChildIndex < 0 ||
            endChildIndex >= node.ChildCount)
        {
            return false;
        }

        if (endChildIndex < startChildIndex)
        {
            maxLineBreaks = 0;
            return true;
        }

        var total = 0;
        for (var i = startChildIndex; i <= endChildIndex; i++)
        {
            if (!TryGetFiniteLineBreakCount(node.Child(i), out var childBreaks))
            {
                maxLineBreaks = 0;
                return false;
            }

            total = checked(total + childBreaks);
        }

        maxLineBreaks = total;
        return true;
    }

    private static bool TryExtractQuotedRelationOperand(
        RuntimeFrontEnd.RegexNode node,
        out AsciiStructuralQuotedOperandKind kind,
        out bool allowRepeat,
        out byte[][] prefixesUtf8,
        out string prefixedTailSet,
        out int prefixedTailLength,
        out string quotedRunSet,
        out int quotedRunLength)
    {
        kind = AsciiStructuralQuotedOperandKind.None;
        allowRepeat = false;
        prefixesUtf8 = [];
        prefixedTailSet = string.Empty;
        prefixedTailLength = 0;
        quotedRunSet = string.Empty;
        quotedRunLength = 0;

        node = UnwrapRuntimeNode(node);
        if (TryExtractQuotedPrefixedAsciiRun(node, out prefixesUtf8, out prefixedTailSet, out prefixedTailLength))
        {
            kind = AsciiStructuralQuotedOperandKind.QuotedPrefixedRun;
            return true;
        }

        if (TryExtractQuotedAsciiRunWithOptionalRepeat(node, out quotedRunSet, out quotedRunLength, out allowRepeat))
        {
            kind = AsciiStructuralQuotedOperandKind.QuotedAsciiRun;
            return true;
        }

        return false;
    }

    private static bool TryExtractQuotedAsciiRunWithOptionalRepeat(
        RuntimeFrontEnd.RegexNode node,
        out string runtimeSet,
        out int runLength,
        out bool allowRepeat)
    {
        runtimeSet = string.Empty;
        runLength = 0;
        allowRepeat = false;
        node = UnwrapRuntimeNode(node);

        if ((node.Kind is RuntimeFrontEnd.RegexNodeKind.Loop or RuntimeFrontEnd.RegexNodeKind.Lazyloop) &&
            node.ChildCount == 1 &&
            node.M >= 1 &&
            node.N == int.MaxValue &&
            TryExtractQuotedAsciiRun(node.Child(0), out runtimeSet, out runLength))
        {
            allowRepeat = true;
            return true;
        }

        return TryExtractQuotedAsciiRun(node, out runtimeSet, out runLength);
    }

    private static bool TryExtractQuotedAsciiRun(
        RuntimeFrontEnd.RegexNode node,
        out string runtimeSet,
        out int runLength)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = string.Empty;
        runLength = 0;
        return node.Kind == RuntimeFrontEnd.RegexNodeKind.Concatenate &&
            node.ChildCount == 3 &&
            IsQuoteNode(node.Child(0)) &&
            TryGetExactAsciiSetLoop(node.Child(1), out runtimeSet, out runLength) &&
            IsQuoteNode(node.Child(2));
    }

    private static bool TryExtractQuotedPrefixedAsciiRun(
        RuntimeFrontEnd.RegexNode node,
        out byte[][] prefixesUtf8,
        out string prefixedTailSet,
        out int prefixedTailLength)
    {
        prefixesUtf8 = [];
        prefixedTailSet = string.Empty;
        prefixedTailLength = 0;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            node.ChildCount != 3 ||
            !IsQuoteNode(node.Child(0)) ||
            !IsQuoteNode(node.Child(2)))
        {
            return false;
        }

        var body = UnwrapRuntimeNode(node.Child(1));
        if (body.Kind != RuntimeFrontEnd.RegexNodeKind.Concatenate ||
            body.ChildCount != 2 ||
            !TryGetAsciiLiteralAlternation(body.Child(0), out prefixesUtf8) ||
            !TryGetExactAsciiSetLoop(body.Child(1), out prefixedTailSet, out prefixedTailLength))
        {
            prefixesUtf8 = [];
            prefixedTailSet = string.Empty;
            prefixedTailLength = 0;
            return false;
        }

        return prefixesUtf8.Length > 0 && prefixedTailLength > 0;
    }

    private static bool TryGetAsciiLiteralAlternation(RuntimeFrontEnd.RegexNode node, out byte[][] alternatesUtf8)
    {
        alternatesUtf8 = [];
        node = UnwrapRuntimeNode(node);
        if (TryGetAsciiLiteral(node, out var single))
        {
            alternatesUtf8 = [single];
            return true;
        }

        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Alternate || node.ChildCount <= 1)
        {
            return false;
        }

        alternatesUtf8 = new byte[node.ChildCount][];
        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryGetAsciiLiteral(node.Child(i), out alternatesUtf8[i]))
            {
                alternatesUtf8 = [];
                return false;
            }
        }

        return true;
    }

    private static bool IsQuoteNode(RuntimeFrontEnd.RegexNode node)
    {
        node = UnwrapRuntimeNode(node);
        if (TryGetLiteralText(node, out var literal))
        {
            return literal is "\"" or "'";
        }

        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Alternate)
        {
            return false;
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryGetLiteralText(UnwrapRuntimeNode(node.Child(i)), out var alternate) ||
                alternate is not "\"" and not "'")
            {
                return false;
            }
        }

        return node.ChildCount > 0;
    }

    private static bool PrefixesEqual(byte[][] left, byte[][] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!left[i].AsSpan().SequenceEqual(right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetAsciiSeparatorSet(string runtimeSet, out string separatorSet)
    {
        separatorSet = string.Empty;
        if (runtimeSet == RuntimeFrontEnd.RegexCharClass.SpaceClass ||
            runtimeSet == RuntimeFrontEnd.RegexCharClass.ECMASpaceClass)
        {
            separatorSet = runtimeSet;
            return true;
        }

        if (RuntimeFrontEnd.RegexCharClass.IsAscii(runtimeSet) &&
            IsAsciiWhitespaceOnlySet(runtimeSet))
        {
            separatorSet = runtimeSet;
            return true;
        }

        return false;
    }

    private static bool TryExtractAsciiStructuralSuffixParts(
        RuntimeFrontEnd.RegexNode node,
        int startChildIndex,
        int endChildIndex,
        out AsciiStructuralSuffixPart[] suffixParts)
    {
        var parts = new List<AsciiStructuralSuffixPart>(endChildIndex - startChildIndex + 1);
        for (var i = startChildIndex; i <= endChildIndex; i++)
        {
            var child = node.Child(i);
            if (TryGetIdentifierSeparatorLoop(child, out var separatorSet, out var separatorMinCount))
            {
                parts.Add(AsciiStructuralSuffixPart.CreateSeparator(separatorSet, separatorMinCount));
                continue;
            }

            if (TryGetAsciiLiteral(child, out var literalUtf8))
            {
                parts.Add(AsciiStructuralSuffixPart.CreateLiteral(literalUtf8));
                continue;
            }

            suffixParts = [];
            return false;
        }

        suffixParts = [.. parts];
        return suffixParts.Length > 0;
    }

    private static bool TryGetAsciiSet(RuntimeFrontEnd.RegexNode node, out string runtimeSet)
    {
        runtimeSet = string.Empty;
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Set ||
            node.Str is null ||
            !RuntimeFrontEnd.RegexCharClass.IsAscii(node.Str))
        {
            return false;
        }

        runtimeSet = node.Str;
        return true;
    }

    private static bool TryGetAsciiBoundedSetLoop(RuntimeFrontEnd.RegexNode node, out string runtimeSet, out int minCount, out int maxCount)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = string.Empty;
        minCount = 0;
        maxCount = 0;
        if (node.Kind != RuntimeFrontEnd.RegexNodeKind.Setloop ||
            node.Str is null ||
            !RuntimeFrontEnd.RegexCharClass.IsAscii(node.Str))
        {
            return false;
        }

        runtimeSet = node.Str;
        minCount = Math.Max(node.M - 1, 0);
        maxCount = Math.Max(node.N - 1, 0);
        return true;
    }

    private static bool TryGetIdentifierSeparatorLoop(RuntimeFrontEnd.RegexNode node, out string runtimeSet, out int minCount)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = string.Empty;
        minCount = 0;
        if (!TryGetSetLoop(node, out var setNode, out var min, out var max) ||
            setNode.Str is null ||
            min < 0 ||
            max != int.MaxValue)
        {
            return false;
        }

        if (setNode.Str == RuntimeFrontEnd.RegexCharClass.SpaceClass ||
            setNode.Str == RuntimeFrontEnd.RegexCharClass.ECMASpaceClass)
        {
            runtimeSet = setNode.Str;
            minCount = min;
            return true;
        }

        if (RuntimeFrontEnd.RegexCharClass.IsAscii(setNode.Str) &&
            IsAsciiWhitespaceOnlySet(setNode.Str))
        {
            runtimeSet = setNode.Str;
            minCount = min;
            return true;
        }

        return false;
    }

    private static bool IsAsciiWhitespaceOnlySet(string runtimeSet)
    {
        var sawMatch = false;
        for (var value = 0; value < 128; value++)
        {
            if (!RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)value, runtimeSet))
            {
                continue;
            }

            sawMatch = true;
            if (value is not (' ' or '\t' or '\r' or '\n' or '\f' or '\v'))
            {
                return false;
            }
        }

        return sawMatch;
    }

    private static bool TryGetAsciiLiteral(RuntimeFrontEnd.RegexNode node, out byte[] literalUtf8)
    {
        node = UnwrapRuntimeNode(node);
        literalUtf8 = [];
        if (!TryGetLiteralText(node, out var literal) ||
            !IsAsciiLiteralText(literal))
        {
            return false;
        }

        literalUtf8 = Encoding.UTF8.GetBytes(literal);
        return literalUtf8.Length > 0;
    }

    private static bool TryGetExactAsciiSetLoop(RuntimeFrontEnd.RegexNode node, out string runtimeSet, out int length)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = string.Empty;
        length = 0;
        if (!TryGetSetLoop(node, out var setNode, out var min, out var max) ||
            setNode.Str is null ||
            min <= 0 ||
            min != max ||
            !RuntimeFrontEnd.RegexCharClass.IsAscii(setNode.Str))
        {
            return false;
        }

        runtimeSet = setNode.Str;
        length = min;
        return true;
    }

    private static bool TryGetExactAsciiSetAtom(RuntimeFrontEnd.RegexNode node, out string runtimeSet, out int length)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = string.Empty;
        length = 0;

        if (node.Kind == RuntimeFrontEnd.RegexNodeKind.Set &&
            node.Str is { } set &&
            RuntimeFrontEnd.RegexCharClass.IsAscii(set))
        {
            runtimeSet = set;
            length = 1;
            return true;
        }

        return TryGetExactAsciiSetLoop(node, out runtimeSet, out length);
    }

    private static bool TryGetFiniteLineBreakCount(RuntimeFrontEnd.RegexNode node, out int maxLineBreaks)
    {
        node = UnwrapRuntimeNode(node);
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.One:
            case RuntimeFrontEnd.RegexNodeKind.Multi:
                if (TryGetLiteralText(node, out var literal))
                {
                    maxLineBreaks = literal.Count(static ch => ch == '\n');
                    return true;
                }

                break;

            case RuntimeFrontEnd.RegexNodeKind.Set when node.Str is { } set:
                maxLineBreaks = RuntimeFrontEnd.RegexCharClass.CharInClass('\n', set) ? 1 : 0;
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Loop:
            case RuntimeFrontEnd.RegexNodeKind.Lazyloop:
            case RuntimeFrontEnd.RegexNodeKind.Setloop:
            case RuntimeFrontEnd.RegexNodeKind.Setlazy:
            case RuntimeFrontEnd.RegexNodeKind.Setloopatomic:
                if (TryGetSetLoop(node, out var loopSetNode, out _, out var loopSetMax) &&
                    loopSetNode.Str is { } loopSet &&
                    !RuntimeFrontEnd.RegexCharClass.CharInClass('\n', loopSet))
                {
                    maxLineBreaks = 0;
                    return true;
                }

                if (node.ChildCount == 1 &&
                    TryGetFiniteLineBreakCount(node.Child(0), out var loopChildBreaks) &&
                    (node.N != int.MaxValue || loopChildBreaks == 0))
                {
                    maxLineBreaks = checked(loopChildBreaks * node.N);
                    return true;
                }

                break;

            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                {
                    var total = 0;
                    for (var i = 0; i < node.ChildCount; i++)
                    {
                        if (!TryGetFiniteLineBreakCount(node.Child(i), out var childBreaks))
                        {
                            maxLineBreaks = 0;
                            return false;
                        }

                        total = checked(total + childBreaks);
                    }

                    maxLineBreaks = total;
                    return true;
                }

            case RuntimeFrontEnd.RegexNodeKind.Alternate:
                {
                    var max = 0;
                    for (var i = 0; i < node.ChildCount; i++)
                    {
                        if (!TryGetFiniteLineBreakCount(node.Child(i), out var branchBreaks))
                        {
                            maxLineBreaks = 0;
                            return false;
                        }

                        max = Math.Max(max, branchBreaks);
                    }

                    maxLineBreaks = max;
                    return true;
                }

            case RuntimeFrontEnd.RegexNodeKind.Bol:
            case RuntimeFrontEnd.RegexNodeKind.Beginning:
            case RuntimeFrontEnd.RegexNodeKind.Start:
            case RuntimeFrontEnd.RegexNodeKind.Eol:
            case RuntimeFrontEnd.RegexNodeKind.End:
            case RuntimeFrontEnd.RegexNodeKind.EndZ:
            case RuntimeFrontEnd.RegexNodeKind.Boundary:
            case RuntimeFrontEnd.RegexNodeKind.NonBoundary:
                maxLineBreaks = 0;
                return true;
        }

        maxLineBreaks = 0;
        return false;
    }

    private static bool TryGetBoundedAnyTextLoop(RuntimeFrontEnd.RegexNode node, out int maxCount)
    {
        node = UnwrapRuntimeNode(node);
        maxCount = 0;
        if (!TryGetSetLoop(node, out var setNode, out var min, out var max) ||
            setNode.Str is null ||
            min < 0 ||
            max < min ||
            max == int.MaxValue ||
            !IsAsciiAnyTextSet(setNode.Str))
        {
            return false;
        }

        maxCount = max;
        return true;
    }

    /// <summary>
    /// Accepts both [\s\S]{0,N} (any ASCII) and .{0,N} (any ASCII except \n).
    /// Returns gapSameLine=true when the gap excludes \n (dot without Singleline).
    /// </summary>
    private static bool TryGetBoundedGapLoop(RuntimeFrontEnd.RegexNode node, out int maxCount, out bool gapSameLine)
    {
        gapSameLine = false;
        if (TryGetBoundedAnyTextLoop(node, out maxCount))
        {
            return true;
        }

        node = UnwrapRuntimeNode(node);
        maxCount = 0;
        if (!TryGetSetLoop(node, out var setNode, out var min, out var max) ||
            setNode.Str is null ||
            min < 0 ||
            max < min ||
            max == int.MaxValue ||
            !IsAsciiDotSet(setNode.Str))
        {
            return false;
        }

        maxCount = max;
        gapSameLine = true;
        return true;
    }

    /// <summary>
    /// Returns true if the set matches all ASCII characters EXCEPT \n (i.e., '.' without Singleline).
    /// </summary>
    private static bool IsAsciiDotSet(string runtimeSet)
    {
        for (var value = 0; value < 128; value++)
        {
            var matches = RuntimeFrontEnd.RegexCharClass.CharInClass((char)value, runtimeSet);
            if (value == '\n')
            {
                if (matches)
                {
                    return false;
                }
            }
            else if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiAnyTextSet(string runtimeSet)
    {
        var sawNewLine = false;
        for (var value = 0; value < 128; value++)
        {
            var matches = RuntimeFrontEnd.RegexCharClass.CharInClass((char)value, runtimeSet);
            if (!matches)
            {
                return false;
            }

            sawNewLine |= value == '\n';
        }

        return sawNewLine;
    }

    private static bool TryCreateAsciiCharClass(string runtimeSet, out AsciiCharClass charClass)
    {
        if (!RuntimeFrontEnd.RegexCharClass.IsAscii(runtimeSet))
        {
            charClass = null!;
            return false;
        }

        var negated = RuntimeFrontEnd.RegexCharClass.IsNegated(runtimeSet);
        var matches = new bool[128];
        for (var i = 0; i < matches.Length; i++)
        {
            matches[i] = RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)i, runtimeSet);
        }

        charClass = new AsciiCharClass(matches, negated);
        return true;
    }

    private static RuntimeFrontEnd.RegexNode UnwrapRuntimeNode(RuntimeFrontEnd.RegexNode node)
    {
        while (node.Kind is RuntimeFrontEnd.RegexNodeKind.Capture or RuntimeFrontEnd.RegexNodeKind.Group or RuntimeFrontEnd.RegexNodeKind.Atomic &&
               node.ChildCount == 1)
        {
            node = node.Child(0);
        }

        return node;
    }

    private static bool TryGetSetLoop(RuntimeFrontEnd.RegexNode node, out RuntimeFrontEnd.RegexNode setNode, out int min, out int max)
    {
        setNode = node;
        min = 0;
        max = 0;

        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Setloop:
            case RuntimeFrontEnd.RegexNodeKind.Setlazy:
            case RuntimeFrontEnd.RegexNodeKind.Setloopatomic:
                min = node.M;
                max = node.N;
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Loop:
            case RuntimeFrontEnd.RegexNodeKind.Lazyloop:
                if (node.ChildCount == 1)
                {
                    var child = UnwrapRuntimeNode(node.Child(0));
                    if (child.Kind == RuntimeFrontEnd.RegexNodeKind.Set)
                    {
                        setNode = child;
                        min = node.M;
                        max = node.N;
                        return true;
                    }
                }

                break;
        }

        return false;
    }

    private static bool IsAsciiLiteralText(string literal)
    {
        foreach (var ch in literal)
        {
            if (ch > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryExtractLiteralFamily(RuntimeFrontEnd.RegexNode node, out byte[][] literalsUtf8)
    {
        literalsUtf8 = [];
        if (!TryExtractLiteralFamilyStrings(node, out var literals) || literals.Length == 0)
        {
            return false;
        }

        literalsUtf8 = new byte[literals.Length][];
        for (var i = 0; i < literals.Length; i++)
        {
            if (!IsPlainAsciiLiteral(literals[i]))
            {
                literalsUtf8 = [];
                return false;
            }

            literalsUtf8[i] = Encoding.UTF8.GetBytes(literals[i]);
        }

        return true;
    }

    private static bool TryExtractLiteralFamilyStrings(RuntimeFrontEnd.RegexNode node, out string[] literals)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Capture:
            case RuntimeFrontEnd.RegexNodeKind.Group:
                if (node.ChildCount == 1)
                {
                    return TryExtractLiteralFamilyStrings(node.Child(0), out literals);
                }

                break;

            case RuntimeFrontEnd.RegexNodeKind.One:
            case RuntimeFrontEnd.RegexNodeKind.Multi:
                if (TryGetLiteralText(node, out var literal))
                {
                    literals = [literal];
                    return true;
                }

                break;

            case RuntimeFrontEnd.RegexNodeKind.Setloop:
            case RuntimeFrontEnd.RegexNodeKind.Setloopatomic:
            case RuntimeFrontEnd.RegexNodeKind.Setlazy:
                if (node.Str is not null &&
                    node.M >= 1 &&
                    node.N == int.MaxValue &&
                    IsAsciiWhitespaceOnlySet(node.Str))
                {
                    literals = [" "];
                    return true;
                }

                break;

            case RuntimeFrontEnd.RegexNodeKind.Alternate:
                literals = new string[node.ChildCount];
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!TryExtractLiteralFamilyStrings(node.Child(i), out var branchLiterals) || branchLiterals.Length != 1)
                    {
                        literals = [];
                        return false;
                    }

                    literals[i] = branchLiterals[0];
                }

                return true;

            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                var current = new List<string> { string.Empty };
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!TryExtractLiteralFamilyStrings(node.Child(i), out var partLiterals))
                    {
                        literals = [];
                        return false;
                    }

                    var next = new List<string>(current.Count * partLiterals.Length);
                    foreach (var left in current)
                    {
                        foreach (var right in partLiterals)
                        {
                            next.Add(left + right);
                        }
                    }

                    current = next;
                }

                literals = [.. current];
                return true;
        }

        literals = [];
        return false;
    }

    private static bool TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings(RuntimeFrontEnd.RegexNode node, out string[] literals)
    {
        node = UnwrapRuntimeNode(node);
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.One:
            case RuntimeFrontEnd.RegexNodeKind.Multi:
            case RuntimeFrontEnd.RegexNodeKind.Set:
                if (TryGetInvariantIgnoreCaseAsciiLiteralText(node, out var literal))
                {
                    literals = [literal];
                    return true;
                }

                break;

            case RuntimeFrontEnd.RegexNodeKind.Alternate:
                literals = new string[node.ChildCount];
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings(node.Child(i), out var branchLiterals) || branchLiterals.Length != 1)
                    {
                        literals = [];
                        return false;
                    }

                    literals[i] = branchLiterals[0];
                }

                return true;

            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                var current = new List<string> { string.Empty };
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings(node.Child(i), out var partLiterals))
                    {
                        literals = [];
                        return false;
                    }

                    var next = new List<string>(current.Count * partLiterals.Length);
                    foreach (var left in current)
                    {
                        foreach (var right in partLiterals)
                        {
                            next.Add(left + right);
                        }
                    }

                    current = next;
                }

                literals = [.. current];
                return true;
        }

        literals = [];
        return false;
    }

    private static bool TryGetInvariantIgnoreCaseAsciiLiteralText(RuntimeFrontEnd.RegexNode node, out string literal)
    {
        node = UnwrapRuntimeNode(node);
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.One when node.Ch <= 0x7F:
                literal = ((char)Internal.Utilities.AsciiSearch.FoldCase((byte)node.Ch)).ToString();
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Multi when node.Str is { } text:
                var chars = new char[text.Length];
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] > 0x7F)
                    {
                        literal = string.Empty;
                        return false;
                    }

                    chars[i] = (char)Internal.Utilities.AsciiSearch.FoldCase((byte)text[i]);
                }

                literal = new string(chars);
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Set when node.Str is { } set:
                literal = string.Empty;
                if (!TryGetInvariantIgnoreCaseAsciiSetLiteral(set, out var folded))
                {
                    return false;
                }

                literal = ((char)folded).ToString();
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                var builder = new StringBuilder();
                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!TryGetInvariantIgnoreCaseAsciiLiteralText(node.Child(i), out var part))
                    {
                        literal = string.Empty;
                        return false;
                    }

                    builder.Append(part);
                }

                literal = builder.ToString();
                return literal.Length > 0;
        }

        literal = string.Empty;
        return false;
    }

    private static bool TryGetInvariantIgnoreCaseAsciiSetLiteral(string runtimeSet, out byte literal)
    {
        literal = 0;
        if (!RuntimeFrontEnd.RegexCharClass.IsAscii(runtimeSet))
        {
            return false;
        }

        var seen = false;
        var folded = byte.MaxValue;
        for (var value = 0; value < 128; value++)
        {
            if (!RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)value, runtimeSet))
            {
                continue;
            }

            var candidate = Internal.Utilities.AsciiSearch.FoldCase((byte)value);
            if (!seen)
            {
                seen = true;
                folded = candidate;
                continue;
            }

            if (folded != candidate)
            {
                return false;
            }
        }

        if (!seen)
        {
            return false;
        }

        literal = folded;
        return true;
    }

    private static bool TryGetLiteralText(RuntimeFrontEnd.RegexNode node, out string literal)
    {
        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.One:
                literal = node.Ch.ToString();
                return true;

            case RuntimeFrontEnd.RegexNodeKind.Multi when node.Str is not null:
                literal = node.Str;
                return true;

            default:
                literal = string.Empty;
                return false;
        }
    }

    private static bool IsNativeLiteralExecutionPattern(string executionPattern, RegexOptions executionOptions)
    {
        if ((executionOptions & RegexOptions.IgnorePatternWhitespace) == 0)
        {
            return true;
        }

        foreach (var ch in executionPattern)
        {
            if (char.IsWhiteSpace(ch) || ch == '#')
            {
                return false;
            }
        }

        return true;
    }
}

