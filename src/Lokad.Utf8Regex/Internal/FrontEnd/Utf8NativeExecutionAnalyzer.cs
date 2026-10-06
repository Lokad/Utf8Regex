using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
namespace Lokad.Utf8Regex.Internal.FrontEnd;


internal static class Utf8NativeExecutionAnalyzer
{
    public static bool TryAnalyze(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        if (TryAnalyzeAsciiStructuralIdentifierFamilyTree(semanticRegex, executionPattern, executionOptions, out analyzedRegex))
        {
            return true;
        }

        if (TryAnalyzeLiteralTree(semanticRegex, executionPattern, executionOptions, out analyzedRegex))
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
        out Utf8RegexAnalysis analyzedRegex)
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

        // Named capture wrappers retain the same literal discovery. The
        // NonBacktracking admission proof separately excludes shared declarations
        // and scoped folding; offsets/results retain the owned unit verifier.
        if ((executionOptions & RegexOptions.NonBacktracking) != 0 &&
            (executionOptions & RegexOptions.IgnoreCase) == 0 &&
            semanticRegex.RuntimeTree is { Root.ChildCount: 1 } literalTree &&
            TryGetLiteralBranchText(literalTree.Root.Child(0), out var wrappedLiteral))
        {
            return TryCreateLiteralAnalyzedRegex(semanticRegex, wrappedLiteral, executionOptions, out analyzedRegex);
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

        if ((executionOptions & (RegexOptions.RightToLeft | RegexOptions.IgnoreCase)) == 0 &&
            TryExtractLiteralWithPositiveLiteralLookaheadTree(semanticRegex, out var lookaheadLiteral, out var trailingLiteralUtf8))
        {
            return TryCreateLiteralAnalyzedRegex(semanticRegex, lookaheadLiteral, executionOptions, out analyzedRegex, trailingLiteralUtf8: trailingLiteralUtf8);
        }

        if (TryExtractSingleCapturedLiteralPattern(executionPattern, out var capturedLiteral))
        {
            return TryCreateLiteralAnalyzedRegex(semanticRegex, capturedLiteral, executionOptions, out analyzedRegex);
        }

        if ((executionOptions & (RegexOptions.RightToLeft | RegexOptions.IgnoreCase)) == 0 &&
            TryExtractLiteralAlternationWithPositiveLiteralLookaheadTree(semanticRegex, executionOptions, out var lookaheadAlternates, out var alternationTrailingLiteralUtf8, out var lookaheadAlternationExecutionKind, out var lookaheadAlternationSearchKind))
        {
            analyzedRegex = Utf8RegexAnalysis.Create(
                semanticRegex,
                executionPattern,
                Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
                Utf8SearchFacts.Create(
                    lookaheadAlternationSearchKind,
                    new Utf8SearchFactData
                    {
                        AlternateLiteralsUtf8 = lookaheadAlternates,
                        TrailingLiteralUtf8 = alternationTrailingLiteralUtf8,
                    }),
                lookaheadAlternationExecutionKind);
            return true;
        }

        if ((executionOptions & RegexOptions.RightToLeft) == 0 &&
            (executionOptions & RegexOptions.IgnoreCase) != 0 &&
            TryExtractPlainAsciiLiteralAlternationPattern(executionPattern, out var ignoreCaseAlternateLiterals))
        {
            analyzedRegex = Utf8RegexAnalysis.Create(
                semanticRegex,
                executionPattern,
                Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
                Utf8SearchFacts.Create(
                    Utf8SearchKind.AsciiFoldedByteLiterals,
                    new Utf8SearchFactData
                    {
                        AlternateLiteralsUtf8 = ignoreCaseAlternateLiterals,
                    }),
                NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals);
            return true;
        }

        if ((executionOptions & RegexOptions.RightToLeft) == 0 &&
            TryExtractUtf8LiteralAlternationTree(semanticRegex, out var alternateLiterals, out var alternationLeadingBoundary, out var alternationTrailingBoundary))
        {
            var ignoreCase = (executionOptions & RegexOptions.IgnoreCase) != 0;
            var hasNonAscii = alternateLiterals.Any(static literal => literal.Any(static b => b > 0x7F));
            if (ignoreCase && hasNonAscii)
            {
                analyzedRegex = default;
                return false;
            }

            var searchKind = ignoreCase
                ? Utf8SearchKind.AsciiFoldedByteLiterals
                : !hasNonAscii
                    ? Utf8SearchKind.ExactAsciiLiterals
                    : Utf8SearchKind.ExactUtf8Literals;
            var executionKind = ignoreCase
                ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                : NativeExecutionKind.ExactUtf8Literals;
            analyzedRegex = Utf8RegexAnalysis.Create(
                semanticRegex,
                executionPattern,
                Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
                Utf8SearchFacts.Create(
                    searchKind,
                    new Utf8SearchFactData
                    {
                        AlternateLiteralsUtf8 = alternateLiterals,
                        LeadingBoundary = alternationLeadingBoundary,
                        TrailingBoundary = alternationTrailingBoundary,
                    }),
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
        out Utf8RegexAnalysis analyzedRegex) =>
        TryCreateLiteralAnalyzedRegex(
            semanticRegex,
            literalPattern,
            executionOptions,
            out analyzedRegex,
            [],
            Utf8BoundaryRequirement.None,
            Utf8BoundaryRequirement.None);

    private static bool TryCreateLiteralAnalyzedRegex(
        Utf8SemanticRegex semanticRegex,
        string literalPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex,
        byte[] trailingLiteralUtf8) =>
        TryCreateLiteralAnalyzedRegex(
            semanticRegex,
            literalPattern,
            executionOptions,
            out analyzedRegex,
            trailingLiteralUtf8,
            Utf8BoundaryRequirement.None,
            Utf8BoundaryRequirement.None);

    private static bool TryCreateLiteralAnalyzedRegex(
        Utf8SemanticRegex semanticRegex,
        string literalPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex,
        Utf8BoundaryRequirement leadingBoundary,
        Utf8BoundaryRequirement trailingBoundary) =>
        TryCreateLiteralAnalyzedRegex(
            semanticRegex,
            literalPattern,
            executionOptions,
            out analyzedRegex,
            [],
            leadingBoundary,
            trailingBoundary);

    private static bool TryCreateLiteralAnalyzedRegex(
        Utf8SemanticRegex semanticRegex,
        string literalPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex,
        byte[] trailingLiteralUtf8,
        Utf8BoundaryRequirement leadingBoundary,
        Utf8BoundaryRequirement trailingBoundary)
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

        analyzedRegex = Utf8RegexAnalysis.CreateLiteral(
            semanticRegex,
            literalPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            Utf8FrontEndSearchAnalyzer.AnalyzeLiteral(literalUtf8, executionKind, trailingLiteralUtf8, leadingBoundary, trailingBoundary),
            executionKind,
            literalUtf8);
        return true;
    }

    private static bool TryAnalyzeSimplePatternTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        if (!SupportsNativeOptions(executionOptions) || IsPlainLiteral(executionPattern))
        {
            analyzedRegex = default;
            return false;
        }

        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture ||
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

        if (!Utf8AsciiSimplePatternLowerer.TryCreatePlan(semanticRegex, executionOptions, out var simplePatternPlan, out var searchFacts))
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

            // R05: enumerated branch sets feed the longest-match automaton once they
            // reach its selection floor, but BCL stops ordered repetition at first
            // success, so mixed-length sets would project longest spans where the
            // oracle stops early (e.g. (a|ab){2,3} light [1:4] vs oracle [1:2]).
            // Decline those shapes to the ordered slice-or-fallback routes;
            // uniform sets cannot diverge and small direct sets resolve in order.
            if (literalBranches.Length >= Search.PreparedMultiLiteralSearch.AutomatonThreshold)
            {
                var firstBranchLength = literalBranches[0].Length;
                for (var branchIndex = 1; branchIndex < literalBranches.Length; branchIndex++)
                {
                    if (literalBranches[branchIndex].Length != firstBranchLength)
                    {
                        analyzedRegex = default;
                        return false;
                    }
                }
            }

            var ignoreCase = (executionOptions & RegexOptions.IgnoreCase) != 0;
            analyzedRegex = Utf8RegexAnalysis.Create(
                semanticRegex,
                executionPattern,
                Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
                Utf8SearchFacts.Create(
                    ignoreCase ? Utf8SearchKind.AsciiFoldedByteLiterals : Utf8SearchKind.ExactAsciiLiterals,
                    new Utf8SearchFactData
                    {
                        AlternateLiteralsUtf8 = literalBranches,
                    }),
                ignoreCase ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals : NativeExecutionKind.ExactUtf8Literals);
            return true;
        }

        // Scoped case folding has no per-branch flag in plan execution: a sensitive
        // top level with an i-scoped group would search and match case-sensitively
        // where BCL folds (e.g. (?i:[ab]){2}|(?:a.)). Decline those non-literal shapes
        // to the BCL-exact fallback; literal-extractable branches above keep theirs.
        if ((executionOptions & RegexOptions.IgnoreCase) == 0 &&
            Utf8AsciiSimplePatternLowerer.HasScopedIgnoreCase(root.Child(0)))
        {
            analyzedRegex = default;
            return false;
        }

        var features = Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex);
        var analyzedSearchFacts = Utf8FrontEndSearchAnalyzer.AnalyzeSimplePattern(semanticRegex, simplePatternPlan, searchFacts);
        analyzedRegex = searchFacts.LiteralUtf8 is { } literalUtf8
            ? Utf8RegexAnalysis.CreateSimple(semanticRegex, executionPattern, features, analyzedSearchFacts, simplePatternPlan, literalUtf8)
            : Utf8RegexAnalysis.CreateSimple(semanticRegex, executionPattern, features, analyzedSearchFacts, simplePatternPlan);
        return true;
    }

    private static bool TryAnalyzeAnchoredAsciiHeadTailRunTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture ||
            root.ChildCount != 1)
        {
            return false;
        }

        var body = UnwrapRuntimeNode(root.Child(0));
        if (body.Kind != Utf8TreeNodeKind.Concatenate ||
            body.ChildCount != 4 ||
            body.Child(0).Kind is not Utf8TreeNodeKind.Beginning and not Utf8TreeNodeKind.Start ||
            !TryGetExactAsciiSetAtom(body.Child(1), out var headSet, out var headLength) ||
            headLength != 1 ||
            !TryGetSetLoop(body.Child(2), out var tailSetNode, out var tailMin, out var tailMax) ||
            tailSetNode.Str is not { } tailSet ||
            tailMax != int.MaxValue ||
            !Utf8OwnedCharClass.IsAscii(tailSet) ||
            body.Child(3).Kind is not Utf8TreeNodeKind.EndZ and not Utf8TreeNodeKind.End)
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
            allowsTrailingNewlineBeforeEnd: body.Child(3).Kind == Utf8TreeNodeKind.EndZ,
            ignoreCase: (executionOptions & RegexOptions.IgnoreCase) != 0,
            isUtf8ByteSafe: true,
            runPlan: default,
            anchoredHeadTailRunPlan: new AsciiSimplePatternAnchoredHeadTailRunPlan(headCharClass, tailCharClass, tailMin),
            anchoredValidatorPlan: new AsciiSimplePatternAnchoredValidatorPlan(
            [
                new AsciiSimplePatternAnchoredValidatorSegment(headCharClass, 1, 1),
                new AsciiSimplePatternAnchoredValidatorSegment(tailCharClass, tailMin, int.MaxValue),
            ],
            ignoreCase: (executionOptions & RegexOptions.IgnoreCase) != 0),
            anchoredBoundedDatePlan: default,
            anchoredOptionalFieldPlan: default,
            repeatedDigitGroupPlan: default,
            boundedSuffixLiteralPlan: default,
            symmetricLiteralWindowPlan: default,
            isAbsoluteStartAnchored: true,
            isAbsoluteEndAnchored: true);

        analyzedRegex = Utf8RegexAnalysis.CreateSimple(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            default,
            simplePatternPlan);
        return true;
    }

    private static bool IsSupportedSimplePatternNode(Utf8TreeNode node)
    {
        var pending = new Stack<Utf8TreeNode>();
        pending.Push(node);
        while (pending.TryPop(out node))
        {
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.Capture:
                    if (node.N != -1) return false;
                    goto case Utf8TreeNodeKind.Group;
                case Utf8TreeNodeKind.Group:
                    if (node.ChildCount != 1) return false;
                    pending.Push(node.Child(0));
                    break;
                case Utf8TreeNodeKind.Concatenate:
                case Utf8TreeNodeKind.Alternate:
                    for (var index = node.ChildCount - 1; index >= 0; index--)
                        pending.Push(node.Child(index));
                    break;
                case Utf8TreeNodeKind.One:
                case Utf8TreeNodeKind.Multi:
                case Utf8TreeNodeKind.Bol:
                case Utf8TreeNodeKind.Eol:
                case Utf8TreeNodeKind.Beginning:
                case Utf8TreeNodeKind.Start:
                case Utf8TreeNodeKind.EndZ:
                case Utf8TreeNodeKind.End:
                case Utf8TreeNodeKind.Empty:
                    break;
                case Utf8TreeNodeKind.Set:
                    if (node.Str is null || Utf8OwnedCharClass.IsSubtraction(node.Str)) return false;
                    break;
                case Utf8TreeNodeKind.Oneloop:
                case Utf8TreeNodeKind.Setloop:
                    if (node.M < 0 || node.N < node.M || node.N == int.MaxValue) return false;
                    break;
                case Utf8TreeNodeKind.Loop:
                    if (node.M < 0 || node.N < node.M || node.N == int.MaxValue || node.ChildCount != 1) return false;
                    pending.Push(node.Child(0));
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    private static bool IsSupportedSimplePatternLookaroundNode(Utf8TreeNode node)
    {
        var pending = new Stack<Utf8TreeNode>();
        pending.Push(node);
        while (pending.TryPop(out node))
        {
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.Capture:
                case Utf8TreeNodeKind.Group:
                    if (node.ChildCount != 1) return false;
                    pending.Push(node.Child(0));
                    break;
                case Utf8TreeNodeKind.One:
                case Utf8TreeNodeKind.Multi:
                case Utf8TreeNodeKind.Set:
                    break;
                case Utf8TreeNodeKind.Concatenate:
                    for (var index = node.ChildCount - 1; index >= 0; index--)
                        pending.Push(node.Child(index));
                    break;
                default:
                    return false;
            }
        }
        return true;
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
            root.Kind != Utf8TreeNodeKind.Capture ||
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
        Utf8TreeNode body,
        RegexOptions executionOptions,
        out string literal,
        out string trailingLiteral)
    {
        literal = string.Empty;
        trailingLiteral = string.Empty;

        if (body.Kind != Utf8TreeNodeKind.Concatenate ||
            body.ChildCount < 2)
        {
            return false;
        }

        var lookaheadNode = body.Child(body.ChildCount - 1);
        if (lookaheadNode.Kind != Utf8TreeNodeKind.PositiveLookaround ||
            (lookaheadNode.Options & RegexOptions.RightToLeft) != 0 ||
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
            root.Kind != Utf8TreeNodeKind.Capture ||
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
            ? Utf8SearchKind.AsciiFoldedByteLiterals
            : hasNonAscii
                ? Utf8SearchKind.ExactUtf8Literals
                : Utf8SearchKind.ExactAsciiLiterals;
        alternateLiteralsUtf8 = encoded;
        return true;
    }

    private static bool TryExtractPositiveLiteralLookahead(
        Utf8TreeNode body,
        RegexOptions executionOptions,
        out Utf8TreeNode substantiveNode,
        out string trailingLiteral)
    {
        substantiveNode = body;
        trailingLiteral = string.Empty;

        if (body.Kind != Utf8TreeNodeKind.Concatenate ||
            body.ChildCount != 2 ||
            body.Child(1).Kind != Utf8TreeNodeKind.PositiveLookaround ||
            (body.Child(1).Options & RegexOptions.RightToLeft) != 0 ||
            body.Child(1).ChildCount != 1 ||
            !TryGetNativeLookaheadLiteralText(body.Child(1).Child(0), executionOptions, out trailingLiteral))
        {
            return false;
        }

        substantiveNode = body.Child(0);
        return true;
    }

    private static bool TryGetNativeLookaheadLiteralText(Utf8TreeNode node, RegexOptions executionOptions, out string literal)
    {
        if (TryGetLiteralText(node, out literal))
        {
            return true;
        }

        return (executionOptions & RegexOptions.IgnoreCase) != 0 &&
            (executionOptions & RegexOptions.CultureInvariant) != 0 &&
            TryGetInvariantIgnoreCaseAsciiLiteralText(node, out literal);
    }

    private static bool TryGetNativeLookaheadAlternationTexts(Utf8TreeNode node, RegexOptions executionOptions, out string[] literals)
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
            RegexOptions.RightToLeft |
            // Literal discovery has no backtracking choices or repeated capture
            // history. The same native result therefore serves NonBacktracking;
            // unsupported syntax/option combinations retain constructor validation.
            RegexOptions.NonBacktracking;
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
            root.Kind != Utf8TreeNodeKind.Capture ||
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
        Utf8TreeNode node,
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

        if (node.Kind != Utf8TreeNodeKind.Concatenate)
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

    private static bool TryUnwrapLiteralAlternation(Utf8TreeNode node, out string[] literals)
    {
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group && node.ChildCount == 1)
            node = node.Child(0);
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.Alternate:
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

    private static bool TryGetLiteralBranchText(Utf8TreeNode node, out string literal)
    {
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group && node.ChildCount == 1)
            node = node.Child(0);
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.One:
            case Utf8TreeNodeKind.Multi:
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
            root.Kind != Utf8TreeNodeKind.Capture ||
            root.ChildCount != 1)
        {
            return false;
        }

        var body = root.Child(0);
        if (body.Kind != Utf8TreeNodeKind.Concatenate)
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
        Utf8TreeNode body,
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

    private static bool TryAnalyzeAsciiStructuralIdentifierFamilyTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture ||
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

        analyzedRegex = Utf8RegexAnalysis.CreateStructuralIdentifier(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            Utf8SearchFacts.Create(
                Utf8SearchKind.ExactAsciiLiterals,
                new Utf8SearchFactData
                {
                    AlternateLiteralsUtf8 = prefixes,
                    LeadingBoundary = leadingBoundary,
                }),
            plan);
        return true;
    }

    private static bool TryExtractAsciiStructuralIdentifierFamily(
        Utf8TreeNode node,
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
        if (node.Kind != Utf8TreeNodeKind.Concatenate)
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

        if (startChildIndex > endChildIndex)
        {
            return false;
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
        out Utf8RegexAnalysis analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryExtractAsciiStructuralTokenWindow(root.Child(0), out var plan))
        {
            return false;
        }

        analyzedRegex = Utf8RegexAnalysis.CreateStructuralTokenWindow(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            Utf8SearchFacts.Create(
                Utf8SearchKind.ExactAsciiLiteral,
                new Utf8SearchFactData
                {
                    LiteralUtf8 = plan.AnchorLiteralUtf8,
                    MinRequiredLength = plan.LeadingLength + plan.SeparatorMinCount + plan.AnchorLiteralUtf8.Length + plan.SeparatorMinCount + plan.TrailingLength,
                }),
            plan,
            plan.AnchorLiteralUtf8);
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
        Utf8TreeNode node,
        out AsciiStructuralTokenWindowPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != Utf8TreeNodeKind.Concatenate ||
            node.ChildCount != 7 ||
            !TryGetExactAsciiSetLoop(node.Child(0), out var leadingSet, out var leadingLength) ||
            !TryGetIdentifierSeparatorLoop(node.Child(1), out var separatorSet, out var separatorMinCount) ||
            !TryGetBoundedAnyTextLoop(node.Child(2), out var leadingGapMax, out _) ||
            !TryGetAsciiLiteral(node.Child(3), out var anchorLiteralUtf8) ||
            !TryGetBoundedAnyTextLoop(node.Child(4), out var trailingGapMax, out _) ||
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
        out Utf8RegexAnalysis analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture ||
            root.ChildCount != 1 ||
            (!TryExtractOrderedLiteralWindow(root.Child(0), out var plan) &&
             !TryExtractSymmetricAlternateOrderedLiteralWindow(root.Child(0), out plan) &&
             !TryExtractOrderedLiteralLookaheadWindow(root.Child(0), out plan)))
        {
            return false;
        }

        var searchFacts = plan.IsLiteralFamily
            ? Utf8SearchFacts.Create(
                  Utf8SearchKind.ExactAsciiLiterals,
                  new Utf8SearchFactData
                  {
                      AlternateLiteralsUtf8 = plan.LeadingLiteralsUtf8 ?? [],
                      CanGuideFallbackStarts = true,
                      MinRequiredLength = plan.LeadingLiteralUtf8.Length + plan.TrailingLiteralUtf8.Length,
                  })
            : Utf8SearchFacts.Create(
                  Utf8SearchKind.ExactAsciiLiteral,
                  new Utf8SearchFactData
                  {
                      LiteralUtf8 = plan.LeadingLiteralUtf8,
                      CanGuideFallbackStarts = true,
                      MinRequiredLength = plan.LeadingLiteralUtf8.Length + plan.TrailingLiteralUtf8.Length,
                  });

        var features = Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex);
        analyzedRegex = plan.IsLiteralFamily
            ? Utf8RegexAnalysis.CreateOrderedLiteralWindow(semanticRegex, executionPattern, features, searchFacts, plan)
            : Utf8RegexAnalysis.CreateOrderedLiteralWindow(semanticRegex, executionPattern, features, searchFacts, plan, plan.LeadingLiteralUtf8);
        return true;
    }

    private static bool TryExtractOrderedLiteralWindow(
        Utf8TreeNode node,
        out AsciiOrderedLiteralWindowPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != Utf8TreeNodeKind.Concatenate)
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
        var separator = default(AsciiOrderedWindowSeparatorPlan);
        if (index < childCount &&
            TryGetIdentifierSeparatorLoop(node.Child(index), out var separatorSet, out var sepMin))
        {
            gapLeadingSeparatorMinCount = sepMin;
            hasSeparator = true;
            separator = new AsciiOrderedWindowSeparatorPlan(separatorSet, sepMin,
                UnwrapRuntimeNode(node.Child(index)).Kind is Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Lazyloop or Utf8TreeNodeKind.Onelazy);
            index++;
        }

        // Bounded gap (e.g., .{0,80}). Optional when a separator was found —
        // the pattern can be Literal + \s* + Literal with no bounded repeat.
        var maxGap = 0;
        var minGap = 0;
        var gapSameLine = false;
        if (index < childCount &&
            TryGetBoundedGapLoop(node.Child(index), out maxGap, out minGap, out gapSameLine))
        {
            if (IsLazyGapRange(node.Child(index), minGap, maxGap))
            {
                return false;
            }

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
            minGap,
            gapSameLine,
            separator,
            branchSeparators: null,
            yieldLeadingLiteralOnly: false,
            leadingBoundary1,
            trailingBoundary1,
            leadingBoundary2,
            trailingBoundary2);
        return true;
    }

    private static bool TryExtractOrderedLiteralLookaheadWindow(
        Utf8TreeNode node,
        out AsciiOrderedLiteralWindowPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != Utf8TreeNodeKind.Concatenate ||
            node.ChildCount != 2 ||
            !TryGetAsciiLiteral(node.Child(0), out var leadingLiteral))
        {
            return false;
        }

        var lookahead = UnwrapRuntimeNode(node.Child(1));
        if (lookahead.Kind != Utf8TreeNodeKind.PositiveLookaround ||
            (lookahead.Options & RegexOptions.RightToLeft) != 0 ||
            lookahead.ChildCount != 1 ||
            !TryExtractSeparatorLiteralLookahead(lookahead.Child(0), out var separator, out var trailingLiteral))
        {
            return false;
        }

        plan = new AsciiOrderedLiteralWindowPlan(
            leadingLiteral,
            leadingLiteralsUtf8: null,
            trailingLiteral,
            trailingLiteralsUtf8: null,
            maxGap: 0,
            minGap: 0,
            gapSameLine: false,
            separator,
            branchSeparators: null,
            yieldLeadingLiteralOnly: true,
            Utf8BoundaryRequirement.None,
            Utf8BoundaryRequirement.None,
            Utf8BoundaryRequirement.None,
            Utf8BoundaryRequirement.None);
        return true;
    }

    private static bool TryExtractSeparatorLiteralLookahead(
        Utf8TreeNode node,
        out AsciiOrderedWindowSeparatorPlan separator,
        out byte[] trailingLiteralUtf8)
    {
        separator = default;
        trailingLiteralUtf8 = [];
        node = UnwrapRuntimeNode(node);
        if (node.Kind != Utf8TreeNodeKind.Concatenate ||
            node.ChildCount != 2 ||
            !TryGetIdentifierSeparatorLoop(node.Child(0), out var separatorSet, out var separatorMinCount) ||
            !TryGetAsciiLiteral(node.Child(1), out trailingLiteralUtf8))
        {
            trailingLiteralUtf8 = [];
            return false;
        }

        separator = new AsciiOrderedWindowSeparatorPlan(separatorSet, separatorMinCount,
            UnwrapRuntimeNode(node.Child(0)).Kind is Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Lazyloop or Utf8TreeNodeKind.Onelazy);
        return separatorMinCount > 0;
    }

    private static bool TryExtractSymmetricAlternateOrderedLiteralWindow(
        Utf8TreeNode node,
        out AsciiOrderedLiteralWindowPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != Utf8TreeNodeKind.Alternate || node.ChildCount < 2)
        {
            return false;
        }

        var leadingLiterals = new byte[node.ChildCount][];
        var trailingLiterals = new byte[node.ChildCount][];
        var separators = new AsciiOrderedWindowSeparatorPlan[node.ChildCount];
        Utf8BoundaryRequirement? leadingBoundary1 = null;
        Utf8BoundaryRequirement? trailingBoundary1 = null;
        Utf8BoundaryRequirement? leadingBoundary2 = null;
        Utf8BoundaryRequirement? trailingBoundary2 = null;
        int? separatorMin = null;
        int? maxGap = null;
        int? minGap = null;
        bool? gapSameLine = null;

        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryExtractOrderedLiteralWindowBranch(
                node.Child(i),
                out leadingLiterals[i],
                out trailingLiterals[i],
                out var branchMaxGap,
                out var branchMinGap,
                out var branchGapSameLine,
                out separators[i],
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
            separatorMin ??= separators[i].Minimum;
            maxGap ??= branchMaxGap;
            minGap ??= branchMinGap;
            gapSameLine ??= branchGapSameLine;

            if (leadingBoundary1 != branchLeadingBoundary1 ||
                trailingBoundary1 != branchTrailingBoundary1 ||
                leadingBoundary2 != branchLeadingBoundary2 ||
                trailingBoundary2 != branchTrailingBoundary2 ||
                separatorMin != separators[i].Minimum ||
                maxGap != branchMaxGap ||
                minGap != branchMinGap ||
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

        if (maxGap is not { } selectedMaxGap ||
            minGap is not { } selectedMinGap ||
            gapSameLine is not { } selectedGapSameLine ||
            separatorMin is null ||
            leadingBoundary1 is not { } selectedLeadingBoundary1 ||
            trailingBoundary1 is not { } selectedTrailingBoundary1 ||
            leadingBoundary2 is not { } selectedLeadingBoundary2 ||
            trailingBoundary2 is not { } selectedTrailingBoundary2)
        {
            return false;
        }

        var shortestLeading = leadingLiterals.OrderBy(static l => l.Length).First();
        var shortestTrailing = trailingLiterals.OrderBy(static l => l.Length).First();
        plan = new AsciiOrderedLiteralWindowPlan(
            shortestLeading,
            leadingLiterals,
            shortestTrailing,
            trailingLiterals,
            selectedMaxGap,
            selectedMinGap,
            selectedGapSameLine,
            separators[0],
            separators,
            yieldLeadingLiteralOnly: false,
            selectedLeadingBoundary1,
            selectedTrailingBoundary1,
            selectedLeadingBoundary2,
            selectedTrailingBoundary2);
        return true;
    }

    private static bool TryExtractOrderedLiteralWindowBranch(
        Utf8TreeNode node,
        out byte[] leadingLiteral,
        out byte[] trailingLiteral,
        out int maxGap,
        out int minGap,
        out bool gapSameLine,
        out AsciiOrderedWindowSeparatorPlan separator,
        out Utf8BoundaryRequirement leadingBoundary1,
        out Utf8BoundaryRequirement trailingBoundary1,
        out Utf8BoundaryRequirement leadingBoundary2,
        out Utf8BoundaryRequirement trailingBoundary2)
    {
        leadingLiteral = [];
        trailingLiteral = [];
        maxGap = 0;
        minGap = 0;
        gapSameLine = false;
        separator = default;
        leadingBoundary1 = Utf8BoundaryRequirement.None;
        trailingBoundary1 = Utf8BoundaryRequirement.None;
        leadingBoundary2 = Utf8BoundaryRequirement.None;
        trailingBoundary2 = Utf8BoundaryRequirement.None;

        node = UnwrapRuntimeNode(node);
        if (node.Kind != Utf8TreeNodeKind.Concatenate)
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
            TryGetIdentifierSeparatorLoop(node.Child(index), out var separatorSet, out var sepMin))
        {
            separator = new AsciiOrderedWindowSeparatorPlan(separatorSet, sepMin,
                UnwrapRuntimeNode(node.Child(index)).Kind is Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Lazyloop or Utf8TreeNodeKind.Onelazy);
            index++;
        }

        if (index >= childCount ||
            !TryGetBoundedGapLoop(node.Child(index), out maxGap, out minGap, out gapSameLine) ||
            IsLazyGapRange(node.Child(index), minGap, maxGap))
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
        out Utf8RegexAnalysis analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryExtractAsciiStructuralRepeatedSegment(root.Child(0), out var plan))
        {
            return false;
        }

        analyzedRegex = Utf8RegexAnalysis.CreateStructuralRepeatedSegment(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            Utf8SearchFacts.Create(
                Utf8SearchKind.None,
                new Utf8SearchFactData
                {
                    MinRequiredLength = plan.RepetitionMinCount * (1 + plan.TrailingMinCount + plan.SeparatorMinCount),
                }),
            plan);
        return true;
    }

    private static bool TryAnalyzeAsciiStructuralQuotedRelationTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        analyzedRegex = default;
        if (!SupportsNativeOptions(executionOptions) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 ||
            semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryExtractAsciiStructuralQuotedRelation(root.Child(0), out var plan))
        {
            return false;
        }

        analyzedRegex = Utf8RegexAnalysis.CreateStructuralQuotedRelation(
            semanticRegex,
            executionPattern,
            Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
            Utf8SearchFacts.Create(Utf8SearchKind.None),
            plan);
        return true;
    }

    private static bool TryExtractAsciiStructuralRepeatedSegment(
        Utf8TreeNode node,
        out AsciiStructuralRepeatedSegmentPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind is not Utf8TreeNodeKind.Loop and not Utf8TreeNodeKind.Lazyloop ||
            node.ChildCount != 1 ||
            node.M <= 0 ||
            node.N < node.M ||
            node.N == int.MaxValue)
        {
            return false;
        }

        var segment = UnwrapRuntimeNode(node.Child(0));
        if (segment.Kind != Utf8TreeNodeKind.Concatenate ||
            segment.ChildCount != 3 ||
            !TryGetExactAsciiSetAtom(segment.Child(0), out var leadingSet, out var leadingLength) ||
            leadingLength != 1 ||
            !TryGetSetLoop(segment.Child(1), out var trailingSetNode, out var trailingMin, out var trailingMax) ||
            trailingSetNode.Str is null ||
            trailingMin <= 0 ||
            trailingMax != int.MaxValue ||
            !Utf8OwnedCharClass.IsAscii(trailingSetNode.Str) ||
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
        Utf8TreeNode node,
        out AsciiStructuralQuotedRelationPlan plan)
    {
        plan = default;
        node = UnwrapRuntimeNode(node);
        if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop &&
            node.ChildCount == 1 &&
            node.M >= 1 &&
            node.N == int.MaxValue)
        {
            node = UnwrapRuntimeNode(node.Child(0));
        }

        if (node.Kind != Utf8TreeNodeKind.Alternate ||
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
        Utf8TreeNode node,
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
        if (node.Kind != Utf8TreeNodeKind.Concatenate ||
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
        Utf8TreeNode concatenateNode,
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
        Utf8TreeNode concatenateNode,
        int startIndex,
        int count,
        out Utf8TreeNode slice)
    {
        slice = concatenateNode;
        concatenateNode = UnwrapRuntimeNode(concatenateNode);
        if (concatenateNode.Kind != Utf8TreeNodeKind.Concatenate ||
            count <= 0 ||
            startIndex < 0 ||
            startIndex + count > concatenateNode.ChildCount)
        {
            return false;
        }

        slice = new Utf8TreeNode(Utf8TreeNodeKind.Concatenate, concatenateNode.Options);
        for (var i = 0; i < count; i++)
        {
            slice.AddChild(concatenateNode.Child(startIndex + i));
        }

        return true;
    }

    private static bool TryGetFiniteLineBreakCount(
        Utf8TreeNode node,
        int startChildIndex,
        int endChildIndex,
        out int maxLineBreaks)
    {
        maxLineBreaks = 0;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != Utf8TreeNodeKind.Concatenate ||
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
        Utf8TreeNode node,
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
        Utf8TreeNode node,
        out string runtimeSet,
        out int runLength,
        out bool allowRepeat)
    {
        runtimeSet = string.Empty;
        runLength = 0;
        allowRepeat = false;
        node = UnwrapRuntimeNode(node);

        if ((node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop) &&
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
        Utf8TreeNode node,
        out string runtimeSet,
        out int runLength)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = string.Empty;
        runLength = 0;
        return node.Kind == Utf8TreeNodeKind.Concatenate &&
            node.ChildCount == 3 &&
            IsQuoteNode(node.Child(0)) &&
            TryGetExactAsciiSetLoop(node.Child(1), out runtimeSet, out runLength) &&
            IsQuoteNode(node.Child(2));
    }

    private static bool TryExtractQuotedPrefixedAsciiRun(
        Utf8TreeNode node,
        out byte[][] prefixesUtf8,
        out string prefixedTailSet,
        out int prefixedTailLength)
    {
        prefixesUtf8 = [];
        prefixedTailSet = string.Empty;
        prefixedTailLength = 0;
        node = UnwrapRuntimeNode(node);
        if (node.Kind != Utf8TreeNodeKind.Concatenate ||
            node.ChildCount != 3 ||
            !IsQuoteNode(node.Child(0)) ||
            !IsQuoteNode(node.Child(2)))
        {
            return false;
        }

        var body = UnwrapRuntimeNode(node.Child(1));
        if (body.Kind != Utf8TreeNodeKind.Concatenate ||
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

    private static bool TryGetAsciiLiteralAlternation(Utf8TreeNode node, out byte[][] alternatesUtf8)
    {
        alternatesUtf8 = [];
        node = UnwrapRuntimeNode(node);
        if (TryGetAsciiLiteral(node, out var single))
        {
            alternatesUtf8 = [single];
            return true;
        }

        if (node.Kind != Utf8TreeNodeKind.Alternate || node.ChildCount <= 1)
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

    private static bool IsQuoteNode(Utf8TreeNode node)
    {
        node = UnwrapRuntimeNode(node);
        if (TryGetLiteralText(node, out var literal))
        {
            return literal is "\"" or "'";
        }

        if (node.Kind != Utf8TreeNodeKind.Alternate)
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
        if (runtimeSet == Utf8OwnedCharClass.SpaceClass ||
            runtimeSet == Utf8OwnedCharClass.ECMASpaceClass)
        {
            separatorSet = runtimeSet;
            return true;
        }

        if (Utf8OwnedCharClass.IsAscii(runtimeSet) &&
            IsAsciiWhitespaceOnlySet(runtimeSet))
        {
            separatorSet = runtimeSet;
            return true;
        }

        return false;
    }

    private static bool TryExtractAsciiStructuralSuffixParts(
        Utf8TreeNode node,
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

    private static bool TryGetAsciiSet(Utf8TreeNode node, out string runtimeSet)
    {
        runtimeSet = string.Empty;
        if (node.Kind != Utf8TreeNodeKind.Set ||
            node.Str is null ||
            !Utf8OwnedCharClass.IsAscii(node.Str))
        {
            return false;
        }

        runtimeSet = node.Str;
        return true;
    }

    private static bool TryGetAsciiBoundedSetLoop(Utf8TreeNode node, out string runtimeSet, out int minCount, out int maxCount)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = string.Empty;
        minCount = 0;
        maxCount = 0;
        if (node.Kind != Utf8TreeNodeKind.Setloop ||
            node.Str is null ||
            !Utf8OwnedCharClass.IsAscii(node.Str))
        {
            return false;
        }

        runtimeSet = node.Str;
        minCount = Math.Max(node.M - 1, 0);
        maxCount = Math.Max(node.N - 1, 0);
        return true;
    }

    private static bool TryGetIdentifierSeparatorLoop(Utf8TreeNode node, out string runtimeSet, out int minCount)
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

        if (setNode.Str == Utf8OwnedCharClass.SpaceClass ||
            setNode.Str == Utf8OwnedCharClass.ECMASpaceClass)
        {
            runtimeSet = setNode.Str;
            minCount = min;
            return true;
        }

        if (Utf8OwnedCharClass.IsAscii(setNode.Str) &&
            IsAsciiWhitespaceOnlySet(setNode.Str))
        {
            runtimeSet = setNode.Str;
            minCount = min;
            return true;
        }

        return false;
    }

    // Membership must honor negation: the raw test matches the exclusion
    // range of a negated set (dot and negated-newline loops passed as
    // whitespace-only because their lone excluded newline is whitespace),
    // misrouting dot gaps into separator-window plans.
    private static bool IsAsciiWhitespaceOnlySet(string runtimeSet)
    {
        var sawMatch = false;
        for (var value = 0; value < 128; value++)
        {
            if (!Utf8OwnedCharClass.CharInClass((char)value, runtimeSet))
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

    private static bool TryGetAsciiLiteral(Utf8TreeNode node, out byte[] literalUtf8)
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

    private static bool TryGetExactAsciiSetLoop(Utf8TreeNode node, out string runtimeSet, out int length)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = string.Empty;
        length = 0;
        if (!TryGetSetLoop(node, out var setNode, out var min, out var max) ||
            setNode.Str is null ||
            min <= 0 ||
            min != max ||
            !Utf8OwnedCharClass.IsAscii(setNode.Str))
        {
            return false;
        }

        runtimeSet = setNode.Str;
        length = min;
        return true;
    }

    private static bool TryGetExactAsciiSetAtom(Utf8TreeNode node, out string runtimeSet, out int length)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = string.Empty;
        length = 0;

        if (node.Kind == Utf8TreeNodeKind.Set &&
            node.Str is { } set &&
            Utf8OwnedCharClass.IsAscii(set))
        {
            runtimeSet = set;
            length = 1;
            return true;
        }

        return TryGetExactAsciiSetLoop(node, out runtimeSet, out length);
    }

    private static bool TryGetFiniteLineBreakCount(Utf8TreeNode node, out int maxLineBreaks)
    {
        maxLineBreaks = 0;
        node = UnwrapRuntimeNode(node);
        var values = new Dictionary<Utf8TreeNode, int>();
        var pending = new Stack<(Utf8TreeNode Node, bool Complete)>();
        pending.Push((node, false));
        while (pending.TryPop(out var frame))
        {
            var current = UnwrapRuntimeNode(frame.Node);
            switch (current.Kind)
            {
                case Utf8TreeNodeKind.One:
                case Utf8TreeNodeKind.Multi:
                    if (!TryGetLiteralText(current, out var literal)) return false;
                    values[current] = literal.Count(static ch => ch == '\n');
                    break;
                case Utf8TreeNodeKind.Set when current.Str is { } set:
                    values[current] = Utf8OwnedCharClass.CharInClass('\n', set) ? 1 : 0;
                    break;
                case Utf8TreeNodeKind.Loop:
                case Utf8TreeNodeKind.Lazyloop:
                case Utf8TreeNodeKind.Setloop:
                case Utf8TreeNodeKind.Setlazy:
                case Utf8TreeNodeKind.Setloopatomic:
                    if (TryGetSetLoop(current, out var setNode, out _, out _) &&
                        setNode.Str is { } loopSet && !Utf8OwnedCharClass.CharInClass('\n', loopSet))
                    {
                        values[current] = 0;
                        break;
                    }
                    if (current.ChildCount != 1) return false;
                    if (!frame.Complete)
                    {
                        pending.Push((current, true));
                        pending.Push((current.Child(0), false));
                        break;
                    }
                    var childBreaks = values[UnwrapRuntimeNode(current.Child(0))];
                    if (current.N == int.MaxValue && childBreaks != 0) return false;
                    values[current] = checked(childBreaks * current.N);
                    break;
                case Utf8TreeNodeKind.Concatenate:
                case Utf8TreeNodeKind.Alternate:
                    if (!frame.Complete)
                    {
                        pending.Push((current, true));
                        for (var index = current.ChildCount - 1; index >= 0; index--)
                            pending.Push((current.Child(index), false));
                        break;
                    }
                    var total = 0;
                    for (var index = 0; index < current.ChildCount; index++)
                    {
                        var count = values[UnwrapRuntimeNode(current.Child(index))];
                        total = current.Kind == Utf8TreeNodeKind.Alternate ? Math.Max(total, count) : checked(total + count);
                    }
                    values[current] = total;
                    break;
                case Utf8TreeNodeKind.Bol:
                case Utf8TreeNodeKind.Beginning:
                case Utf8TreeNodeKind.Start:
                case Utf8TreeNodeKind.Eol:
                case Utf8TreeNodeKind.End:
                case Utf8TreeNodeKind.EndZ:
                case Utf8TreeNodeKind.Boundary:
                case Utf8TreeNodeKind.NonBoundary:
                    values[current] = 0;
                    break;
                default:
                    return false;
            }
        }
        maxLineBreaks = values[node];
        return true;
    }


    // The direct window executor selects the trailing literal greedily
    // (LastIndexOf), so lazy ranges decline window plans while greedy and
    // fixed gaps keep them; BCL parity holds through the fallback route.
    private static bool IsLazyGapRange(Utf8TreeNode node, int minGap, int maxGap)
    {
        if (minGap == maxGap)
        {
            return false;
        }

        var unwrapped = UnwrapRuntimeNode(node);
        return unwrapped.Kind is Utf8TreeNodeKind.Lazyloop or Utf8TreeNodeKind.Setlazy;
    }

    private static bool TryGetBoundedAnyTextLoop(Utf8TreeNode node, out int maxCount, out int minCount)
    {
        node = UnwrapRuntimeNode(node);
        maxCount = 0;
        minCount = 0;
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
        minCount = min;
        return true;
    }

    /// <summary>
    /// Accepts both [\s\S]{0,N} (any ASCII) and .{0,N} (any ASCII except \n).
    /// Returns gapSameLine=true when the gap excludes \n (dot without Singleline).
    /// </summary>
    private static bool TryGetBoundedGapLoop(Utf8TreeNode node, out int maxCount, out int minCount, out bool gapSameLine)
    {
        gapSameLine = false;
        if (TryGetBoundedAnyTextLoop(node, out maxCount, out minCount))
        {
            return true;
        }

        node = UnwrapRuntimeNode(node);
        maxCount = 0;
        minCount = 0;
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
        minCount = min;
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
            var matches = Utf8OwnedCharClass.CharInClass((char)value, runtimeSet);
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
            var matches = Utf8OwnedCharClass.CharInClass((char)value, runtimeSet);
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
        return DotNetAsciiCharClassProjector.TryProjectWholeClass(runtimeSet, out charClass);
    }

    private static Utf8TreeNode UnwrapRuntimeNode(Utf8TreeNode node)
    {
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic &&
               node.ChildCount == 1)
        {
            node = node.Child(0);
        }

        return node;
    }

    private static bool TryGetSetLoop(Utf8TreeNode node, out Utf8TreeNode setNode, out int min, out int max)
    {
        setNode = node;
        min = 0;
        max = 0;

        switch (node.Kind)
        {
            case Utf8TreeNodeKind.Setloop:
            case Utf8TreeNodeKind.Setlazy:
            case Utf8TreeNodeKind.Setloopatomic:
                min = node.M;
                max = node.N;
                return true;

            case Utf8TreeNodeKind.Loop:
            case Utf8TreeNodeKind.Lazyloop:
                if (node.ChildCount == 1)
                {
                    var child = UnwrapRuntimeNode(node.Child(0));
                    if (child.Kind == Utf8TreeNodeKind.Set)
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

    private static bool TryExtractLiteralFamily(Utf8TreeNode node, out byte[][] literalsUtf8)
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

    private static bool TryExtractLiteralFamilyStrings(Utf8TreeNode node, out string[] literals)
        => TryExtractLiteralFamilyStringsCore(node, new Utf8PreparationBudget(4096, 256), false, out literals);

    private static bool TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings(Utf8TreeNode node, out string[] literals)
        => TryExtractLiteralFamilyStringsCore(node, new Utf8PreparationBudget(4096, 256), true, out literals);

    private static bool TryExtractLiteralFamilyStringsCore(Utf8TreeNode node, Utf8PreparationBudget budget, bool foldCase, out string[] literals)
    {
        literals = [];
        // Preserve pre-order branch reservations and post-order concatenation
        // products while suspending parent products on an explicit stack.
        var frames = new Stack<(Utf8TreeNode Node, int Child, string[] Branches, List<string>? Parts)>();
        string[] values;
        while (true)
        {
            if (foldCase) node = UnwrapRuntimeNode(node);
            else
                while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group && node.ChildCount == 1)
                    node = node.Child(0);
            if (node.Kind is Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Alternate)
            {
                var alternate = node.Kind == Utf8TreeNodeKind.Alternate;
                if (alternate && !budget.TryReserve(node.ChildCount, node.ChildCount)) return false;
                var branches = alternate ? new string[node.ChildCount] : Array.Empty<string>();
                List<string>? parts = alternate ? null : [string.Empty];
                if (node.ChildCount != 0)
                {
                    frames.Push((node, 0, branches, parts));
                    node = node.Child(0);
                    continue;
                }
                values = alternate ? [] : [string.Empty];
            }
            else if (foldCase && node.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Multi or Utf8TreeNodeKind.Set &&
                TryGetInvariantIgnoreCaseAsciiLiteralText(node, out var folded))
                values = [folded];
            else if (!foldCase && node.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Multi &&
                TryGetLiteralText(node, out var literal))
                values = [literal];
            else if (!foldCase && node.Kind is Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setloopatomic or Utf8TreeNodeKind.Setlazy &&
                node.Str is { } set && node.M >= 1 && node.N == int.MaxValue && IsAsciiWhitespaceOnlySet(set))
                values = [" "];
            else
                return false;

            while (frames.TryPop(out var frame))
            {
                var parts = frame.Parts;
                if (parts is null)
                {
                    if (values.Length != 1) return false;
                    frame.Branches[frame.Child] = values[0];
                }
                else
                {
                    if (!budget.TryReserveProduct(parts.Count, values.Length, 256, out var product)) return false;
                    var next = new List<string>(product);
                    foreach (var left in parts)
                    foreach (var right in values)
                        next.Add(left + right);
                    parts = next;
                }
                var nextChild = frame.Child + 1;
                if (nextChild == frame.Node.ChildCount)
                {
                    values = parts is null ? frame.Branches : [.. parts];
                    continue;
                }
                frames.Push((frame.Node, nextChild, frame.Branches, parts));
                node = frame.Node.Child(nextChild);
                break;
            }
            if (frames.Count == 0)
            {
                literals = values;
                return true;
            }
        }
    }

    private static bool TryGetInvariantIgnoreCaseAsciiLiteralText(Utf8TreeNode node, out string literal)
    {
        literal = string.Empty;
        var builder = new StringBuilder();
        var pending = new Stack<(Utf8TreeNode Node, int Start, bool Complete)>();
        pending.Push((node, 0, false));
        while (pending.TryPop(out var frame))
        {
            node = UnwrapRuntimeNode(frame.Node);
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.One when node.Ch <= 0x7F:
                    builder.Append((char)Internal.Search.AsciiSearch.FoldCase((byte)node.Ch));
                    break;
                case Utf8TreeNodeKind.Multi when node.Str is { } text:
                    foreach (var unit in text)
                    {
                        if (unit > 0x7F) return false;
                        builder.Append((char)Internal.Search.AsciiSearch.FoldCase((byte)unit));
                    }
                    break;
                case Utf8TreeNodeKind.Set when node.Str is { } set:
                    if (!TryGetInvariantIgnoreCaseAsciiSetLiteral(set, out var folded)) return false;
                    builder.Append((char)folded);
                    break;
                case Utf8TreeNodeKind.Concatenate:
                    if (frame.Complete)
                    {
                        if (builder.Length == frame.Start) return false;
                        break;
                    }
                    pending.Push((node, builder.Length, true));
                    for (var index = node.ChildCount - 1; index >= 0; index--)
                        pending.Push((node.Child(index), 0, false));
                    break;
                default:
                    return false;
            }
        }
        literal = builder.ToString();
        return true;
    }

    private static bool TryGetInvariantIgnoreCaseAsciiSetLiteral(string runtimeSet, out byte literal)
    {
        literal = 0;
        if (!Utf8OwnedCharClass.IsAscii(runtimeSet))
        {
            return false;
        }

        var seen = false;
        var folded = byte.MaxValue;
        for (var value = 0; value < 128; value++)
        {
            if (!Utf8OwnedCharClass.CharInClassBase((char)value, runtimeSet))
            {
                continue;
            }

            var candidate = Internal.Search.AsciiSearch.FoldCase((byte)value);
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

    private static bool TryGetLiteralText(Utf8TreeNode node, out string literal)
    {
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.One:
                literal = node.Ch.ToString();
                return true;

            case Utf8TreeNodeKind.Multi when node.Str is not null:
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
