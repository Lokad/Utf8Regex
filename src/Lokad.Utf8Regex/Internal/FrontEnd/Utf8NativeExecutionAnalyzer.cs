using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
namespace Lokad.Utf8Regex.Internal.FrontEnd;


internal static class Utf8NativeExecutionAnalyzer
{
    private readonly record struct ScanFrame(Utf8TreeNode Node, int NextChild, bool Entered);

    public static bool TryAnalyze(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        if (semanticRegex.RuntimeTree is { UsesIgnoreCase: true } foldedTree &&
            Utf8SliceCaseEquivalences.GetBehavior(foldedTree.Culture, executionOptions) != Utf8ScalarCaseBehavior.Invariant)
        {
            // Native invariant byte folds cannot stand in for construction-
            // culture scalar equivalence, including scoped inline options.
            analyzedRegex = default;
            return false;
        }
        if (semanticRegex.RuntimeTree is { } tree && ContainsImpossibleAtom(tree.Root))
        {
            // Native byte plans must never encode an impossible literal as
            // U+FFFD. The owned scalar lowerer emits failure for these atoms,
            // including under zero repetition and alternative/assertion bodies.
            analyzedRegex = default;
            return false;
        }
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

    private static bool ContainsImpossibleAtom(Utf8TreeNode root)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(budget, new(root, 0, false));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var node = frame.Node;
            if (!frame.Entered)
            {
                // Authoritative failure atoms must never be encoded as U+FFFD
                // by a native shortcut. Pattern UTF-16 text is interoperability
                // input; every decoded literal must represent a complete scalar.
                if (node.Kind == Utf8TreeNodeKind.Nothing) return true;
                if (node.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Onelazy or Utf8TreeNodeKind.Oneloopatomic)
                {
                    if (char.IsSurrogate(node.Ch)) return true;
                }
                else if (node.Kind == Utf8TreeNodeKind.Multi && node.Str is { } text)
                {
                    for (var offset = 0; offset < text.Length;)
                    {
                        budget.ChargeWork(1);
                        if (System.Text.Rune.DecodeFromUtf16(text.AsSpan(offset), out _, out var width) != System.Buffers.OperationStatus.Done)
                            return true;
                        offset += width;
                    }
                }
                frame = frame with { Entered = true, NextChild = node.ChildCount };
            }
            // Preserve the original last-child-first early-exit order, while
            // retaining only one live continuation per ancestor.
            if (frame.NextChild == 0) { frames.Pop(); continue; }
            frames[index] = frame with { NextChild = frame.NextChild - 1 };
            frames.Push(new(node.Child(frame.NextChild - 1), 0, false));
        }
        return false;
    }

    private static bool TryAnalyzeLiteralTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            if (!SupportsNativeLiteralOptions(executionOptions))
            {
                analyzedRegex = default;
                return false;
            }

            if (IsPlainLiteral(executionPattern) && IsNativeLiteralExecutionPattern(executionPattern, executionOptions))
            {
                var accepted = TryCreateLiteralAnalyzedRegex(semanticRegex, executionPattern, executionOptions, out analyzedRegex);
                if (accepted) construction.Complete();
                return accepted;
            }

            // Named capture wrappers retain the same literal discovery. The
            // NonBacktracking admission proof separately excludes shared declarations
            // and scoped folding; offsets/results retain the owned scalar program.
            if ((executionOptions & RegexOptions.NonBacktracking) != 0 &&
                (executionOptions & RegexOptions.IgnoreCase) == 0 &&
                semanticRegex.RuntimeTree is { Root.ChildCount: 1 } literalTree &&
                TryGetLiteralBranchText(literalTree.Root.Child(0), out var wrappedLiteral))
            {
                var accepted = TryCreateLiteralAnalyzedRegex(semanticRegex, wrappedLiteral, executionOptions, out analyzedRegex);
                if (accepted) construction.Complete();
                return accepted;
            }

            if (TryExtractBoundaryWrappedLiteralTree(semanticRegex, out var boundaryLiteral, out var leadingBoundary, out var trailingBoundary))
            {
                var accepted = TryCreateLiteralAnalyzedRegex(
                    semanticRegex,
                    boundaryLiteral,
                    executionOptions,
                    out analyzedRegex,
                    leadingBoundary: leadingBoundary,
                    trailingBoundary: trailingBoundary);
                if (accepted) construction.Complete();
                return accepted;
            }

            if ((executionOptions & (RegexOptions.RightToLeft | RegexOptions.IgnoreCase)) == 0 &&
                TryExtractLiteralWithPositiveLiteralLookaheadTree(semanticRegex, out var lookaheadLiteral, out var trailingLiteralUtf8))
            {
                var accepted = TryCreateLiteralAnalyzedRegex(semanticRegex, lookaheadLiteral, executionOptions, out analyzedRegex, trailingLiteralUtf8: trailingLiteralUtf8);
                if (accepted) construction.Complete();
                return accepted;
            }

            if (TryExtractSingleCapturedLiteralPattern(executionPattern, out var capturedLiteral))
            {
                var accepted = TryCreateLiteralAnalyzedRegex(semanticRegex, capturedLiteral, executionOptions, out analyzedRegex);
                if (accepted) construction.Complete();
                return accepted;
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
                construction.Complete();
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
                construction.Complete();
                return true;
            }

            if ((executionOptions & RegexOptions.RightToLeft) == 0 &&
                TryExtractUtf8LiteralAlternationTree(semanticRegex, out var alternateLiterals, out var alternationLeadingBoundary, out var alternationTrailingBoundary))
            {
                var ignoreCase = (executionOptions & RegexOptions.IgnoreCase) != 0;
                var hasNonAscii = false;
                foreach (var literal in alternateLiterals)
                {
                    owner.ChargeWork((ulong)literal.Length + 1);
                    foreach (var value in literal) if (value > 0x7f) { hasNonAscii = true; break; }
                }
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
                construction.Complete();
                return true;
            }

            analyzedRegex = default;
            return false;
        }
        finally { owner.LeaveDepth(); }
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
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            if ((executionOptions & RegexOptions.IgnoreCase) != 0 &&
                !IsPlainAsciiLiteral(literalPattern))
            {
                analyzedRegex = default;
                return false;
            }

            var literalUtf8 = owner.EncodeUtf8(literalPattern);
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
            construction.Complete();
            return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryAnalyzeSimplePatternTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
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
                    // Keep literal-pattern text for diagnostics/classification,
                    // while byte execution shares the admitted expansion payload.
                    var bytes = literalBranches[0]; var text = owner.CopyAsciiText(bytes);
                    var plainAscii = IsPlainAsciiLiteral(text);
                    var folded = (executionOptions & RegexOptions.IgnoreCase) != 0;
                    if (folded && !plainAscii) { analyzedRegex = default; return false; }
                    var kind = folded ? NativeExecutionKind.AsciiLiteralIgnoreCase :
                        plainAscii ? NativeExecutionKind.ExactAsciiLiteral : NativeExecutionKind.ExactUtf8Literal;
                    analyzedRegex = Utf8RegexAnalysis.CreateLiteral(semanticRegex, text,
                        Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex),
                        Utf8FrontEndSearchAnalyzer.AnalyzeLiteral(bytes, kind), kind, bytes);
                    owner.ReleaseArray(literalBranches, false);
                    construction.Complete(); return true;
                }

                // Scalar regex alternatives preserve ordered repetition. A longest-
                // match automaton cannot substitute for mixed-length ordered choices
                // (e.g. (a|ab){2,3} on abab). Keep those on owned ordered execution;
                // uniform sets and small direct sets preserve the same selection.
                if (literalBranches.Length >= Search.PreparedMultiLiteralSearch.AutomatonThreshold)
                {
                    var firstBranchLength = literalBranches[0].Length;
                    for (var branchIndex = 1; branchIndex < literalBranches.Length; branchIndex++)
                    {
                        owner.ChargeWork(1);
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
                construction.Complete();
                return true;
            }

            // Simple byte plans have one folding flag. Scoped folding beneath a
            // sensitive top level needs the owned scalar program's per-node behavior;
            // decline this plan after the literal-extraction routes above.
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
            construction.Complete();
            return true;
        }
        finally { owner.LeaveDepth(); }
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
            !TryGetExactAsciiSetAtom(body.Child(1), out var headCharClass, out var headLength) ||
            headLength != 1 ||
            !TryGetSetLoop(body.Child(2), out var tailSetNode, out var tailMin, out var tailMax) ||
            !Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(tailSetNode.ScalarClass, out var tailCharClass) ||
            tailMax != int.MaxValue ||
            body.Child(3).Kind is not Utf8TreeNodeKind.EndZ and not Utf8TreeNodeKind.End)
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
                switch (node.Kind)
                {
                    case Utf8TreeNodeKind.Capture:
                        if (node.N != -1) return false;
                        goto case Utf8TreeNodeKind.Group;
                    case Utf8TreeNodeKind.Group:
                        if (node.ChildCount != 1) return false;
                        break;
                    case Utf8TreeNodeKind.Concatenate:
                    case Utf8TreeNodeKind.Alternate:
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
                        if (node.ScalarClass is not { } scalarClass || scalarClass.Data.Length < 3 ||
                            scalarClass.Data.Length != 3L + scalarClass.Data[1] + scalarClass.Data[2]) return false;
                        break;
                    case Utf8TreeNodeKind.Oneloop:
                    case Utf8TreeNodeKind.Setloop:
                        if (node.M < 0 || node.N < node.M || node.N == int.MaxValue) return false;
                        break;
                    case Utf8TreeNodeKind.Loop:
                        if (node.M < 0 || node.N < node.M || node.N == int.MaxValue || node.ChildCount != 1) return false;
                        break;
                    default:
                        return false;
                }
            }
            var childCount = node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Alternate or Utf8TreeNodeKind.Loop
                ? node.ChildCount : 0;
            if (frame.NextChild == childCount) { frames.Pop(); continue; }
            frames[index] = frame with { NextChild = frame.NextChild + 1 };
            frames.Push(new(node.Child(frame.NextChild), 0, false));
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
        Utf8SemanticRegex semanticRegex, out string literal, out byte[] trailingLiteralUtf8)
    {
        literal = string.Empty; trailingLiteralUtf8 = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture || root.ChildCount != 1) return false;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(root, 0, false));
        owner.ChargeWork(1);
        if (!TryExtractLiteralPrefixWithPositiveLiteralLookahead(root.Child(0), semanticRegex.ExecutionOptions, out literal, out var trailingLiteral) ||
            literal.Length == 0 || !IsNativeLiteralExecutionPattern(semanticRegex.ExecutionPattern, semanticRegex.ExecutionOptions)) return false;
        trailingLiteralUtf8 = owner.EncodeUtf8(trailingLiteral);
        if (trailingLiteralUtf8.Length == 0) return false;
        construction.Complete(); return true;
    }

    private static bool TryExtractLiteralPrefixWithPositiveLiteralLookahead(
        Utf8TreeNode body, RegexOptions executionOptions, out string literal, out string trailingLiteral)
    {
        literal = string.Empty; trailingLiteral = string.Empty;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(body, 0, false));
        owner.ChargeWork(1);
        if (body.Kind != Utf8TreeNodeKind.Concatenate || body.ChildCount < 2) return false;
        var lookahead = body.Child(body.ChildCount - 1);
        frames.Push(new(lookahead, 0, false)); owner.ChargeWork(1);
        if (lookahead.Kind != Utf8TreeNodeKind.PositiveLookaround ||
            (lookahead.Options & RegexOptions.RightToLeft) != 0 || lookahead.ChildCount != 1 ||
            !TryGetNativeLookaheadLiteralText(lookahead.Child(0), executionOptions, out trailingLiteral)) return false;
        frames.Pop();
        using var text = Utf8ConstructionText.Create();
        for (var index = 0; index < body.ChildCount - 1; index++)
        {
            owner.ChargeWork(1);
            if (!TryGetNativeLookaheadLiteralText(body.Child(index), executionOptions, out var part))
            { literal = string.Empty; trailingLiteral = string.Empty; return false; }
            text.Append(part);
        }
        literal = text.ToText();
        if (literal.Length == 0) return false;
        construction.Complete(); return true;
    }

    private static bool TryExtractLiteralAlternationWithPositiveLiteralLookaheadTree(
        Utf8SemanticRegex semanticRegex, RegexOptions executionOptions,
        out byte[][] alternateLiteralsUtf8, out byte[] trailingLiteralUtf8,
        out NativeExecutionKind executionKind, out Utf8SearchKind searchKind)
    {
        alternateLiteralsUtf8 = []; trailingLiteralUtf8 = []; executionKind = default; searchKind = default;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture || root.ChildCount != 1) return false;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(root, 0, false));
        owner.ChargeWork(1);
        if (!TryExtractPositiveLiteralLookahead(root.Child(0), executionOptions, out var substantiveNode, out var trailingLiteral) ||
            !TryGetNativeLookaheadAlternationTexts(substantiveNode, executionOptions, out var literalTexts) ||
            literalTexts.Length <= 1 || !IsNativeLiteralExecutionPattern(semanticRegex.ExecutionPattern, semanticRegex.ExecutionOptions)) return false;
        var encoded = owner.AllocateArray<byte[]>(literalTexts.Length, false);
        var hasNonAscii = false;
        for (var index = 0; index < literalTexts.Length; index++)
        {
            owner.ChargeWork(1);
            var bytes = owner.EncodeUtf8(literalTexts[index]);
            if (bytes.Length == 0) return false;
            owner.ChargeWork((ulong)bytes.Length);
            foreach (var value in bytes) if (value > 0x7f) { hasNonAscii = true; break; }
            encoded[index] = bytes;
        }
        trailingLiteralUtf8 = owner.EncodeUtf8(trailingLiteral);
        if (trailingLiteralUtf8.Length == 0) return false;
        var ignoreCase = (executionOptions & RegexOptions.IgnoreCase) != 0;
        if (ignoreCase && hasNonAscii) return false;
        executionKind = ignoreCase ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals : NativeExecutionKind.ExactUtf8Literals;
        searchKind = ignoreCase ? Utf8SearchKind.AsciiFoldedByteLiterals :
            hasNonAscii ? Utf8SearchKind.ExactUtf8Literals : Utf8SearchKind.ExactAsciiLiterals;
        owner.ReleaseArray(literalTexts, false);
        alternateLiteralsUtf8 = encoded; construction.Complete(); return true;
    }

    private static bool TryExtractPositiveLiteralLookahead(
        Utf8TreeNode body,
        RegexOptions executionOptions,
        out Utf8TreeNode substantiveNode,
        out string trailingLiteral)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(body, 0, false));
        owner.ChargeWork(1);
        substantiveNode = body;
        trailingLiteral = string.Empty;

        if (body.Kind != Utf8TreeNodeKind.Concatenate ||
            body.ChildCount != 2 ||
            body.Child(1).Kind != Utf8TreeNodeKind.PositiveLookaround ||
            (body.Child(1).Options & RegexOptions.RightToLeft) != 0 ||
            body.Child(1).ChildCount != 1)
        {
            return false;
        }

        frames.Push(new(body.Child(1), 0, false)); owner.ChargeWork(1);
        if (!TryGetNativeLookaheadLiteralText(body.Child(1).Child(0), executionOptions, out trailingLiteral)) return false;
        substantiveNode = body.Child(0);
        construction.Complete();
        return true;
    }

    private static bool TryGetNativeLookaheadLiteralText(Utf8TreeNode node, RegexOptions executionOptions, out string literal)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var accepted = TryGetLiteralText(node, out literal) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 && (executionOptions & RegexOptions.CultureInvariant) != 0 &&
            TryGetInvariantIgnoreCaseAsciiLiteralText(node, out literal);
        if (accepted) construction.Complete();
        return accepted;
    }

    private static bool TryGetNativeLookaheadAlternationTexts(Utf8TreeNode node, RegexOptions executionOptions, out string[] literals)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var accepted = TryUnwrapLiteralAlternation(node, out literals) ||
            (executionOptions & RegexOptions.IgnoreCase) != 0 && (executionOptions & RegexOptions.CultureInvariant) != 0 &&
            TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings(node, out literals);
        if (accepted) construction.Complete();
        return accepted;
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

    private static bool IsPlainAsciiLiteral(ReadOnlySpan<char> pattern)
    {
        Utf8ConstructionScope.ChargeWork((ulong)pattern.Length);
        foreach (var ch in pattern)
        {
            if (ch > 0x7F || Utf8RegexSyntax.IsRegexMetaCharacter(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPlainLiteral(ReadOnlySpan<char> pattern)
    {
        Utf8ConstructionScope.ChargeWork((ulong)pattern.Length);
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
        literal = string.Empty;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            if (pattern.Length < 3 || pattern[0] != '(' || pattern[^1] != ')') return false;
            var inner = pattern.AsSpan(1, pattern.Length - 2);
            if (!IsPlainLiteral(inner)) return false;
            literal = owner.CopyText(inner); construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractUtf8LiteralAlternationTree(
        Utf8SemanticRegex semanticRegex, out byte[][] alternateLiteralsUtf8,
        out Utf8BoundaryRequirement leadingBoundary, out Utf8BoundaryRequirement trailingBoundary)
    {
        alternateLiteralsUtf8 = []; leadingBoundary = Utf8BoundaryRequirement.None; trailingBoundary = Utf8BoundaryRequirement.None;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture || root.ChildCount != 1) return false;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(root, 0, false));
        owner.ChargeWork(1);
        if (!TryUnwrapLiteralAlternationWithBoundaries(root.Child(0), out var literalTexts, out leadingBoundary, out trailingBoundary) || literalTexts.Length <= 1) return false;
        var encoded = owner.AllocateArray<byte[]>(literalTexts.Length, false);
        for (var index = 0; index < literalTexts.Length; index++)
        {
            owner.ChargeWork(1);
            if (literalTexts[index].Length == 0)
            { leadingBoundary = Utf8BoundaryRequirement.None; trailingBoundary = Utf8BoundaryRequirement.None; return false; }
            encoded[index] = owner.EncodeUtf8(literalTexts[index]);
        }
        owner.ReleaseArray(literalTexts, false);
        alternateLiteralsUtf8 = encoded; construction.Complete(); return true;
    }

    private static bool TryExtractPlainAsciiLiteralAlternationPattern(string pattern, out byte[][] alternateLiteralsUtf8)
    {
        alternateLiteralsUtf8 = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork((ulong)pattern.Length + 1);
            if (pattern.Length == 0 || pattern[0] == '|' || pattern[^1] == '|') return false;
            var count = 1; var length = 0;
            foreach (var value in pattern)
            {
                if (value == '|')
                { if (length == 0) return false; count++; length = 0; }
                else
                { if (value > 0x7f || Utf8RegexSyntax.IsRegexMetaCharacter(value)) return false; length++; }
            }
            if (count <= 1) return false;
            var result = owner.AllocateArray<byte[]>(count, false);
            var start = 0; var index = 0;
            for (var offset = 0; offset <= pattern.Length; offset++)
            {
                owner.ChargeWork(1);
                if (offset < pattern.Length && pattern[offset] != '|') continue;
                result[index++] = owner.EncodeUtf8(pattern.AsSpan(start, offset - start));
                start = offset + 1;
            }
            alternateLiteralsUtf8 = result; construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryUnwrapLiteralAlternationWithBoundaries(
        Utf8TreeNode node,
        out string[] literals,
        out Utf8BoundaryRequirement leadingBoundary,
        out Utf8BoundaryRequirement trailingBoundary)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        owner.ChargeWork(1);
        literals = [];
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;

        if (TryUnwrapLiteralAlternation(node, out literals))
        {
            construction.Complete();
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
                construction.Complete();
                return true;
            }

            if (TryUnwrapLiteralAlternation(node.Child(0), out literals) &&
                TryGetBoundaryRequirement(node.Child(1).Kind, out trailingBoundary))
            {
                construction.Complete();
                return true;
            }
        }
        else if (node.ChildCount == 3 &&
            TryGetBoundaryRequirement(node.Child(0).Kind, out leadingBoundary) &&
            TryUnwrapLiteralAlternation(node.Child(1), out literals) &&
            TryGetBoundaryRequirement(node.Child(2).Kind, out trailingBoundary))
        {
            construction.Complete();
            return true;
        }

        literals = [];
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;
        return false;
    }

    private static bool TryUnwrapLiteralAlternation(Utf8TreeNode node, out string[] literals)
    {
        literals = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group && node.ChildCount == 1)
        {
            owner.ChargeWork(1); node = node.Child(0); frames.Push(new(node, 0, false));
        }
        owner.ChargeWork(1);
        if (node.Kind != Utf8TreeNodeKind.Alternate) return false;
        var result = owner.AllocateArray<string>(node.ChildCount, false);
        for (var index = 0; index < result.Length; index++)
        {
            owner.ChargeWork(1);
            if (!TryGetLiteralBranchText(node.Child(index), out result[index])) return false;
        }
        literals = result; construction.Complete(); return true;
    }

    private static bool TryGetLiteralBranchText(Utf8TreeNode node, out string literal)
    {
        literal = string.Empty;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group && node.ChildCount == 1)
        {
            owner.ChargeWork(1); node = node.Child(0); frames.Push(new(node, 0, false));
        }
        owner.ChargeWork(1);
        if (node.Kind is not Utf8TreeNodeKind.One and not Utf8TreeNodeKind.Multi || !TryGetLiteralText(node, out literal)) return false;
        construction.Complete(); return true;
    }

    private static bool TryExtractBoundaryWrappedLiteralTree(
        Utf8SemanticRegex semanticRegex,
        out string literal,
        out Utf8BoundaryRequirement leadingBoundary,
        out Utf8BoundaryRequirement trailingBoundary)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        literal = string.Empty;
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;

        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != Utf8TreeNodeKind.Capture ||
            root.ChildCount != 1)
        {
            return false;
        }

        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(root, 0, false));
        owner.ChargeWork(1);
        var body = root.Child(0);
        frames.Push(new(body, 0, false));
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

        var accepted = literal.Length > 0 && IsNativeLiteralExecutionPattern(semanticRegex.ExecutionPattern, semanticRegex.ExecutionOptions);
        if (accepted) construction.Complete();
        return accepted;
    }

    private static bool TryExtractBoundaryWrappedLiteral(
        Utf8TreeNode body,
        out string literal,
        out Utf8BoundaryRequirement leadingBoundary,
        out Utf8BoundaryRequirement trailingBoundary)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(body, 0, false));
        owner.ChargeWork(1);
        literal = string.Empty;
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;

        if (body.ChildCount == 3)
        {
            var accepted = TryGetBoundaryRequirement(body.Child(0).Kind, out leadingBoundary) &&
                TryGetLiteralText(body.Child(1), out literal) &&
                TryGetBoundaryRequirement(body.Child(2).Kind, out trailingBoundary);
            if (accepted) construction.Complete();
            return accepted;
        }

        if (body.ChildCount != 2)
        {
            return false;
        }

        if (TryGetBoundaryRequirement(body.Child(0).Kind, out leadingBoundary) &&
            TryGetLiteralText(body.Child(1), out literal))
        {
            construction.Complete();
            return true;
        }

        if (TryGetLiteralText(body.Child(0), out literal) &&
            TryGetBoundaryRequirement(body.Child(1).Kind, out trailingBoundary))
        {
            construction.Complete();
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
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
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

            // This verifier consumes the separator greedily without rewinding it.
            // A suffix beginning in that class needs the owned backtracking path.
            var firstSuffix = suffixParts.Length > 0 && suffixParts[0].IsLiteral
                ? suffixParts[0].LiteralUtf8 : suffixUtf8;
            if (identifierStartSet is null && separatorSet is { } separator &&
                firstSuffix is { Length: > 0 } && separator.Contains(firstSuffix[0]))
                return false;

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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractAsciiStructuralIdentifierFamily(
        Utf8TreeNode node,
        out byte[][] prefixes,
        out AsciiCharClass? separatorSet,
        out int separatorMinCount,
        out AsciiCharClass? identifierStartSet,
        out AsciiCharClass? identifierTailSet,
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
        identifierStartSet = null;
        identifierTailSet = null;
        identifierTailMinCount = 0;
        identifierTailMaxCount = 0;
        suffixUtf8 = null;
        suffixParts = [];
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;

        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        while (true)
        {
            owner.ChargeWork(1);
            if (node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || node.ChildCount != 1) break;
            node = node.Child(0); frames.Push(new(node, 0, false));
        }
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
            var accepted = (leadingBoundary != Utf8BoundaryRequirement.None ||
                    trailingBoundary != Utf8BoundaryRequirement.None) &&
                   GetCommonPrefixLength(prefixes) >= 3;
            if (accepted) construction.Complete();
            return accepted;
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

                construction.Complete(); return true;
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

        construction.Complete(); return true;
    }

    private static bool TryAnalyzeAsciiStructuralTokenWindowTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static void ResetAsciiStructuralIdentifierFamilyExtraction(
        out byte[][] prefixes,
        out AsciiCharClass? separatorSet,
        out int separatorMinCount,
        out AsciiCharClass? identifierStartSet,
        out AsciiCharClass? identifierTailSet,
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
        identifierStartSet = null;
        identifierTailSet = null;
        identifierTailMinCount = 0;
        identifierTailMaxCount = 0;
        suffixUtf8 = null;
        suffixParts = [];
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;
    }

    private static int GetCommonPrefixLength(byte[][] prefixes)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.ChargeWork(1);
        if (prefixes.Length == 0)
        {
            return 0;
        }

        var commonPrefixLength = prefixes[0].Length;
        for (var i = 1; i < prefixes.Length && commonPrefixLength > 0; i++)
        {
            commonPrefixLength = Math.Min(commonPrefixLength, prefixes[i].Length);
            owner.ChargeWork((ulong)commonPrefixLength + 1);
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
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
            plan = default;
            node = UnwrapRuntimeNode(node, frames);
            if (node.Kind != Utf8TreeNodeKind.Concatenate ||
                node.ChildCount != 7 ||
                !TryGetExactAsciiSetLoop(node.Child(0), out var leadingCharClass, out var leadingLength) ||
                !TryGetIdentifierSeparatorLoop(node.Child(1), out var separatorSet, out var separatorMinCount) ||
                !TryGetBoundedAnyTextLoop(node.Child(2), out var leadingGapMax, out _) ||
                !TryGetAsciiLiteral(node.Child(3), out var anchorLiteralUtf8) ||
                !TryGetBoundedAnyTextLoop(node.Child(4), out var trailingGapMax, out _) ||
                !TryGetIdentifierSeparatorLoop(node.Child(5), out var trailingSeparatorSet, out var trailingSeparatorMinCount) ||
                !TryGetExactAsciiSetLoop(node.Child(6), out var trailingCharClass, out var trailingLength) ||
                separatorSet != trailingSeparatorSet ||
                separatorMinCount != trailingSeparatorMinCount)
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryAnalyzeAsciiOrderedLiteralWindowTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractOrderedLiteralWindow(
        Utf8TreeNode node,
        out AsciiOrderedLiteralWindowPlan plan)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
            plan = default;
            node = UnwrapRuntimeNode(node, frames);
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
                leadingLiteral = GetShortestLiteral(alternates);
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractOrderedLiteralLookaheadWindow(
        Utf8TreeNode node,
        out AsciiOrderedLiteralWindowPlan plan)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
            plan = default;
            node = UnwrapRuntimeNode(node, frames);
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractSeparatorLiteralLookahead(
        Utf8TreeNode node,
        out AsciiOrderedWindowSeparatorPlan separator,
        out byte[] trailingLiteralUtf8)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
            separator = default;
            trailingLiteralUtf8 = [];
            node = UnwrapRuntimeNode(node, frames);
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
            var accepted = separatorMinCount > 0;
            if (accepted) construction.Complete();
            return accepted;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractSymmetricAlternateOrderedLiteralWindow(
        Utf8TreeNode node,
        out AsciiOrderedLiteralWindowPlan plan)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
            plan = default;
            node = UnwrapRuntimeNode(node, frames);
            if (node.Kind != Utf8TreeNodeKind.Alternate || node.ChildCount < 2)
            {
                return false;
            }

            var leadingLiterals = owner.AllocateArray<byte[]>(node.ChildCount, false);
            var trailingLiterals = owner.AllocateArray<byte[]>(node.ChildCount, false);
            var separators = owner.AllocateArray<AsciiOrderedWindowSeparatorPlan>(node.ChildCount, false);
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
                owner.ChargeWork(1);
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
                    owner.ChargeWork((ulong)Math.Min(leadingLiterals[i].Length, leadingLiterals[j].Length) + 1);
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

            var shortestLeading = GetShortestLiteral(leadingLiterals);
            var shortestTrailing = GetShortestLiteral(trailingLiterals);
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
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
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
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

            node = UnwrapRuntimeNode(node, frames);
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

            var accepted = index == childCount;
            if (accepted) construction.Complete();
            return accepted;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryAnalyzeAsciiStructuralRepeatedSegmentTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryAnalyzeAsciiStructuralQuotedRelationTree(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractAsciiStructuralRepeatedSegment(
        Utf8TreeNode node,
        out AsciiStructuralRepeatedSegmentPlan plan)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
            plan = default;
            node = UnwrapRuntimeNode(node, frames);
            if (node.Kind is not Utf8TreeNodeKind.Loop and not Utf8TreeNodeKind.Lazyloop ||
                node.ChildCount != 1 ||
                node.M <= 0 ||
                node.N < node.M ||
                node.N == int.MaxValue)
            {
                return false;
            }

            var segment = UnwrapRuntimeNode(node.Child(0), frames);
            if (segment.Kind != Utf8TreeNodeKind.Concatenate ||
                segment.ChildCount != 3 ||
                !TryGetExactAsciiSetAtom(segment.Child(0), out var leadingCharClass, out var leadingLength) ||
                leadingLength != 1 ||
                !TryGetSetLoop(segment.Child(1), out var trailingSetNode, out var trailingMin, out var trailingMax) ||
                trailingMin <= 0 ||
                trailingMax != int.MaxValue ||
                !Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(trailingSetNode.ScalarClass, out var trailingCharClass) ||
                !TryGetSetLoop(segment.Child(2), out var separatorSetNode, out var separatorMin, out var separatorMax) ||
                separatorMin < 0 ||
                separatorMax != int.MaxValue ||
                !TryGetAsciiSeparatorSet(separatorSetNode.ScalarClass, out var separatorSet))
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractAsciiStructuralQuotedRelation(
        Utf8TreeNode node,
        out AsciiStructuralQuotedRelationPlan plan)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
            plan = default;
            node = UnwrapRuntimeNode(node, frames);
            if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop &&
                node.ChildCount == 1 &&
                node.M >= 1 &&
                node.N == int.MaxValue)
            {
                node = UnwrapRuntimeNode(node.Child(0), frames);
            }

            if (node.Kind != Utf8TreeNodeKind.Alternate ||
                node.ChildCount != 2 ||
                !TryExtractAsciiStructuralQuotedRelationBranch(node.Child(0), out var firstBranch, out var firstPrefixes, out var prefixedTailClass, out var firstTailLength, out var quotedRunClass, out var firstRunLength) ||
                !TryExtractAsciiStructuralQuotedRelationBranch(node.Child(1), out var secondBranch, out var secondPrefixes, out var secondTailSet, out var secondTailLength, out var secondRunSet, out var secondRunLength) ||
                !PrefixesEqual(firstPrefixes, secondPrefixes) ||
                prefixedTailClass != secondTailSet ||
                firstTailLength != secondTailLength ||
                quotedRunClass != secondRunSet ||
                firstRunLength != secondRunLength)
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractAsciiStructuralQuotedRelationBranch(
        Utf8TreeNode node,
        out AsciiStructuralQuotedRelationBranchPlan branch,
        out byte[][] prefixesUtf8,
        out AsciiCharClass prefixedTailSet,
        out int prefixedTailLength,
        out AsciiCharClass quotedRunSet,
        out int quotedRunLength)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
            branch = default;
            prefixesUtf8 = [];
            prefixedTailSet = default;
            prefixedTailLength = 0;
            quotedRunSet = default;
            quotedRunLength = 0;

            node = UnwrapRuntimeNode(node, frames);
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
            construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractQuotedRelationOperandAtEdge(
        Utf8TreeNode concatenateNode,
        bool fromStart,
        out int consumeCount,
        out AsciiStructuralQuotedOperandKind kind,
        out bool allowRepeat,
        out byte[][] prefixesUtf8,
        out AsciiCharClass prefixedTailSet,
        out int prefixedTailLength,
        out AsciiCharClass quotedRunSet,
        out int quotedRunLength)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(concatenateNode, 0, false));
            consumeCount = 0; kind = AsciiStructuralQuotedOperandKind.None;
            allowRepeat = false; prefixesUtf8 = []; prefixedTailSet = default;
            prefixedTailLength = 0; quotedRunSet = default; quotedRunLength = 0;

            var singleIndex = fromStart ? 0 : concatenateNode.ChildCount - 1;
            if (TryExtractQuotedRelationOperand(concatenateNode.Child(singleIndex),
                out kind, out allowRepeat, out prefixesUtf8, out prefixedTailSet,
                out prefixedTailLength, out quotedRunSet, out quotedRunLength))
            {
                consumeCount = 1; construction.Complete(); return true;
            }
            if (concatenateNode.ChildCount < 3) return false;

            // Borrow the original three children. A synthetic concatenation
            // would acquire IR/list state and reparent the caller's grammar.
            var spanStart = fromStart ? 0 : concatenateNode.ChildCount - 3;
            if (!IsQuoteNode(concatenateNode.Child(spanStart)) ||
                !IsQuoteNode(concatenateNode.Child(spanStart + 2))) return false;
            var body = concatenateNode.Child(spanStart + 1);
            if (TryExtractPrefixedAsciiRunBody(body, out prefixesUtf8, out prefixedTailSet, out prefixedTailLength))
                kind = AsciiStructuralQuotedOperandKind.QuotedPrefixedRun;
            else if (TryGetExactAsciiSetLoop(body, out quotedRunSet, out quotedRunLength))
                kind = AsciiStructuralQuotedOperandKind.QuotedAsciiRun;
            else return false;
            consumeCount = 3; construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryGetFiniteLineBreakCount(
        Utf8TreeNode node,
        int startChildIndex,
        int endChildIndex,
        out int maxLineBreaks)
    {
        maxLineBreaks = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        while (true)
        {
            owner.ChargeWork(1);
            if (node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || node.ChildCount != 1) break;
            node = node.Child(0); frames.Push(new(node, 0, false));
        }
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
            owner.ChargeWork(1);
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
        out AsciiCharClass prefixedTailSet,
        out int prefixedTailLength,
        out AsciiCharClass quotedRunSet,
        out int quotedRunLength)
    {
        kind = AsciiStructuralQuotedOperandKind.None;
        allowRepeat = false;
        prefixesUtf8 = [];
        prefixedTailSet = default;
        prefixedTailLength = 0;
        quotedRunSet = default;
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
        out AsciiCharClass runtimeSet,
        out int runLength,
        out bool allowRepeat)
    {
        runtimeSet = default;
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
        out AsciiCharClass runtimeSet,
        out int runLength)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = default;
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
        out AsciiCharClass prefixedTailSet,
        out int prefixedTailLength)
    {
        prefixesUtf8 = [];
        prefixedTailSet = default;
        prefixedTailLength = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        while (true)
        {
            owner.ChargeWork(1);
            if (node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || node.ChildCount != 1) break;
            node = node.Child(0); frames.Push(new(node, 0, false));
        }
        if (node.Kind != Utf8TreeNodeKind.Concatenate ||
            node.ChildCount != 3 ||
            !IsQuoteNode(node.Child(0)) ||
            !IsQuoteNode(node.Child(2)))
        {
            return false;
        }

        var accepted = TryExtractPrefixedAsciiRunBody(node.Child(1), out prefixesUtf8, out prefixedTailSet, out prefixedTailLength);
        if (accepted) construction.Complete();
        return accepted;
    }

    private static bool TryExtractPrefixedAsciiRunBody(Utf8TreeNode body,
        out byte[][] prefixesUtf8, out AsciiCharClass prefixedTailSet, out int prefixedTailLength)
    {
        prefixesUtf8 = []; prefixedTailSet = default; prefixedTailLength = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(body, 0, false));
        while (true)
        {
            owner.ChargeWork(1);
            if (body.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || body.ChildCount != 1) break;
            body = body.Child(0); frames.Push(new(body, 0, false));
        }
        if (body.Kind != Utf8TreeNodeKind.Concatenate ||
            body.ChildCount != 2 ||
            !TryGetAsciiLiteralAlternation(body.Child(0), out prefixesUtf8) ||
            !TryGetExactAsciiSetLoop(body.Child(1), out prefixedTailSet, out prefixedTailLength))
        {
            prefixesUtf8 = [];
            prefixedTailSet = default;
            prefixedTailLength = 0;
            return false;
        }

        var accepted = prefixesUtf8.Length > 0 && prefixedTailLength > 0;
        if (accepted) construction.Complete();
        return accepted;
    }

    private static bool TryGetAsciiLiteralAlternation(Utf8TreeNode node, out byte[][] alternatesUtf8)
    {
        alternatesUtf8 = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        while (true)
        {
            owner.ChargeWork(1);
            if (node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || node.ChildCount != 1) break;
            node = node.Child(0); frames.Push(new(node, 0, false));
        }
        if (TryGetAsciiLiteral(node, out var single))
        {
            var result = owner.AllocateArray<byte[]>(1, false);
            owner.ChargeWork(1); result[0] = single;
            alternatesUtf8 = result; construction.Complete(); return true;
        }
        if (node.Kind != Utf8TreeNodeKind.Alternate || node.ChildCount <= 1) return false;
        var alternatives = owner.AllocateArray<byte[]>(node.ChildCount, false);
        for (var index = 0; index < alternatives.Length; index++)
        {
            owner.ChargeWork(1);
            if (!TryGetAsciiLiteral(node.Child(index), out alternatives[index])) return false;
        }
        alternatesUtf8 = alternatives; construction.Complete(); return true;
    }

    private static bool IsQuoteNode(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        while (true)
        {
            owner.ChargeWork(1);
            if (node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || node.ChildCount != 1) break;
            node = node.Child(0); frames.Push(new(node, 0, false));
        }
        if (IsSingleQuote(node)) return true;
        if (node.Kind != Utf8TreeNodeKind.Alternate || node.ChildCount == 0) return false;
        var parentDepth = frames.Count;
        for (var index = 0; index < node.ChildCount; index++)
        {
            var child = node.Child(index); frames.Push(new(child, 0, false));
            while (true)
            {
                owner.ChargeWork(1);
                if (child.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || child.ChildCount != 1) break;
                child = child.Child(0); frames.Push(new(child, 0, false));
            }
            if (!IsSingleQuote(child)) return false;
            while (frames.Count > parentDepth) frames.Pop();
        }
        return true;

        static bool IsSingleQuote(Utf8TreeNode value) => value.Kind switch
        {
            Utf8TreeNodeKind.One => value.Ch is '\'' or '"',
            Utf8TreeNodeKind.Multi => value.Str is "\"" or "'",
            _ => false,
        };
    }

    private static bool PrefixesEqual(byte[][] left, byte[][] right)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.ChargeWork(1);
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            owner.ChargeWork((ulong)Math.Min(left[i].Length, right[i].Length) + 1);
            if (!left[i].AsSpan().SequenceEqual(right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetAsciiSeparatorSet(Utf8ScalarClass? scalarClass, out AsciiCharClass separatorSet)
    {
        return Utf8ScalarAsciiProjector.TryProjectStructuralClass(scalarClass, out separatorSet) &&
            IsAsciiWhitespaceOnlyMask(separatorSet);
    }

    private static bool IsAsciiWhitespaceOnlyMask(AsciiCharClass charClass)
    {
        var sawMatch = false;
        for (var value = 0; value < 128; value++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!charClass.Contains((byte)value)) continue;
            sawMatch = true;
            if (!Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value)) return false;
        }
        return sawMatch;
    }

    private static bool TryExtractAsciiStructuralSuffixParts(
        Utf8TreeNode node,
        int startChildIndex,
        int endChildIndex,
        out AsciiStructuralSuffixPart[] suffixParts)
    {
        suffixParts = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        owner.ChargeWork(1);
        if (startChildIndex < 0 || endChildIndex >= node.ChildCount || endChildIndex < startChildIndex) return false;
        using var parts = Utf8ConstructionBuffer<AsciiStructuralSuffixPart>.Create(owner);
        for (var index = startChildIndex; index <= endChildIndex; index++)
        {
            owner.ChargeWork(1);
            var child = node.Child(index);
            if (TryGetIdentifierSeparatorLoop(child, out var separatorSet, out var separatorMinCount))
                parts.Add(AsciiStructuralSuffixPart.CreateSeparator(separatorSet, separatorMinCount));
            else if (TryGetAsciiLiteral(child, out var literalUtf8))
                parts.Add(AsciiStructuralSuffixPart.CreateLiteral(literalUtf8));
            else return false;
        }
        suffixParts = parts.Take(); construction.Complete(); return true;
    }

    private static bool TryGetAsciiSet(Utf8TreeNode node, out AsciiCharClass? runtimeSet)
    {
        runtimeSet = null;
        if (node.Kind != Utf8TreeNodeKind.Set ||
            !Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(node.ScalarClass, out var charClass)) return false;
        runtimeSet = charClass;
        return true;
    }

    private static bool TryGetAsciiBoundedSetLoop(Utf8TreeNode node, out AsciiCharClass? runtimeSet, out int minCount, out int maxCount)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = null; minCount = 0; maxCount = 0;
        if (node.Kind != Utf8TreeNodeKind.Setloop ||
            !Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(node.ScalarClass, out var charClass)) return false;
        runtimeSet = charClass;
        minCount = Math.Max(node.M - 1, 0);
        maxCount = Math.Max(node.N - 1, 0);
        return true;
    }

    private static bool TryGetIdentifierSeparatorLoop(Utf8TreeNode node, out AsciiCharClass runtimeSet, out int minCount)
    {
        runtimeSet = default; minCount = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        while (true)
        {
            owner.ChargeWork(1);
            if (node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || node.ChildCount != 1) break;
            node = node.Child(0); frames.Push(new(node, 0, false));
        }
        var min = node.M; var max = node.N;
        if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop && node.ChildCount == 1)
        {
            node = node.Child(0); frames.Push(new(node, 0, false));
            while (true)
            {
                owner.ChargeWork(1);
                if (node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || node.ChildCount != 1) break;
                node = node.Child(0); frames.Push(new(node, 0, false));
            }
            if (node.Kind != Utf8TreeNodeKind.Set) return false;
        }
        else if (node.Kind is not (Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Setloopatomic)) return false;
        if (min < 0 || max != int.MaxValue ||
            !TryGetAsciiSeparatorSet(node.ScalarClass, out var separatorSet)) return false;
        runtimeSet = separatorSet; minCount = min; return true;
    }



    // Membership must honor negation: the raw test matches the exclusion
    // range of a negated set (dot and negated-newline loops passed as
    // whitespace-only because their lone excluded newline is whitespace),
    // misrouting dot gaps into separator-window plans.
    private static bool IsAsciiWhitespaceOnlySet(Utf8ScalarClass scalarClass)
    {
        if (!Utf8ScalarAsciiProjector.TryProjectAsciiQuery(scalarClass, out var mask) || mask.IsEmpty) return false;
        for (var value = 0; value < 128; value++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (mask.Contains((byte)value) && !Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value)) return false;
        }
        return true;
    }

    private static bool TryGetAsciiLiteral(Utf8TreeNode node, out byte[] literalUtf8)
    {
        literalUtf8 = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(owner, new(node, 0, false));
        while (true)
        {
            owner.ChargeWork(1);
            if (node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || node.ChildCount != 1) break;
            node = node.Child(0); frames.Push(new(node, 0, false));
        }
        if (node.Kind == Utf8TreeNodeKind.One && node.Ch <= 0x7f)
        {
            var result = owner.AllocateArray<byte>(1, false);
            owner.ChargeWork(1); result[0] = (byte)node.Ch;
            literalUtf8 = result; construction.Complete(); return true;
        }
        if (node.Kind != Utf8TreeNodeKind.Multi || node.Str is not { Length: > 0 } text) return false;
        owner.ChargeWork((ulong)text.Length);
        if (!IsAsciiLiteralText(text)) return false;
        var bytes = owner.AllocateArray<byte>(text.Length, false);
        owner.ChargeWork((ulong)text.Length);
        for (var index = 0; index < bytes.Length; index++) bytes[index] = (byte)text[index];
        literalUtf8 = bytes; construction.Complete(); return true;
    }

    private static bool TryGetExactAsciiSetLoop(Utf8TreeNode node, out AsciiCharClass runtimeSet, out int length)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = default; length = 0;
        if (!TryGetSetLoop(node, out var setNode, out var min, out var max) || min <= 0 || min != max ||
            !Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(setNode.ScalarClass, out runtimeSet)) return false;
        length = min;
        return true;
    }

    private static bool TryGetExactAsciiSetAtom(Utf8TreeNode node, out AsciiCharClass runtimeSet, out int length)
    {
        node = UnwrapRuntimeNode(node);
        runtimeSet = default; length = 0;
        if (node.Kind == Utf8TreeNodeKind.Set &&
            Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(node.ScalarClass, out runtimeSet))
        {
            length = 1;
            return true;
        }
        return TryGetExactAsciiSetLoop(node, out runtimeSet, out length);
    }

    private struct NativeLineBreakFrame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int NextChild;
        internal int Total;
        internal bool Entered;
    }

    // The quoted-relation consumer bounds LF-separated lines. This differs
    // deliberately from the front-end's conservative CR-or-LF line proof.
    private static bool TryGetFiniteLineBreakCount(Utf8TreeNode node, out int maxLineBreaks)
    {
        maxLineBreaks = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<NativeLineBreakFrame>.Create(owner, new(node));
        var value = 0;
        while (frames.Count != 0)
        {
            owner.ChargeWork(1);
            var index = frames.Count - 1; var frame = frames[index]; node = frame.Node;
            if (!frame.Entered)
            {
                frame.Entered = true;
                if (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && node.ChildCount == 1)
                {
                    frames[index] = frame; frames.Push(new(node.Child(0))); continue;
                }
                switch (node.Kind)
                {
                    case Utf8TreeNodeKind.One:
                        value = node.Ch == '\n' ? 1 : 0; frames.Pop(); continue;
                    case Utf8TreeNodeKind.Multi when node.Str is { } text:
                        owner.ChargeWork((ulong)text.Length); value = 0;
                        foreach (var ch in text) if (ch == '\n') value++;
                        frames.Pop(); continue;
                    case Utf8TreeNodeKind.Set when node.ScalarClass is { } set:
                        value = HasNewline(set) ? 1 : 0; frames.Pop(); continue;
                    case Utf8TreeNodeKind.Loop:
                    case Utf8TreeNodeKind.Lazyloop:
                    case Utf8TreeNodeKind.Setloop:
                    case Utf8TreeNodeKind.Setlazy:
                    case Utf8TreeNodeKind.Setloopatomic:
                        if (node.Kind is Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Setloopatomic &&
                            node.ScalarClass is { } loopSet && !HasNewline(loopSet))
                        { value = 0; frames.Pop(); continue; }
                        if (node.ChildCount != 1) return false;
                        frames[index] = frame; frames.Push(new(node.Child(0))); continue;
                    case Utf8TreeNodeKind.Concatenate:
                    case Utf8TreeNodeKind.Alternate:
                        if (node.ChildCount == 0) { value = 0; frames.Pop(); continue; }
                        frame.NextChild = 1; frames[index] = frame; frames.Push(new(node.Child(0))); continue;
                    case Utf8TreeNodeKind.Bol:
                    case Utf8TreeNodeKind.Beginning:
                    case Utf8TreeNodeKind.Start:
                    case Utf8TreeNodeKind.Eol:
                    case Utf8TreeNodeKind.End:
                    case Utf8TreeNodeKind.EndZ:
                    case Utf8TreeNodeKind.Boundary:
                    case Utf8TreeNodeKind.NonBoundary:
                        value = 0; frames.Pop(); continue;
                    default: return false;
                }
            }
            if (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic)
            { frames.Pop(); continue; }
            if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop or Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Setloopatomic)
            {
                if (node.N == int.MaxValue && value != 0) return false;
                value = checked(value * node.N); frames.Pop(); continue;
            }
            frame.Total = node.Kind == Utf8TreeNodeKind.Alternate ? Math.Max(frame.Total, value) : checked(frame.Total + value);
            if (frame.NextChild == node.ChildCount) { value = frame.Total; frames.Pop(); continue; }
            var child = node.Child(frame.NextChild++); frames[index] = frame; frames.Push(new(child));
        }
        maxLineBreaks = value; return true;

        static bool HasNewline(Utf8ScalarClass scalarClass) =>
            !Utf8ScalarAsciiProjector.TryProjectAsciiQuery(scalarClass, out var mask) || mask.Contains((byte)'\n');
    }



    // The direct window executor selects the trailing literal greedily
    // (LastIndexOf), so lazy ranges decline window plans while greedy and
    // fixed gaps keep them; declined shapes use the required scalar route.
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
            setNode.ScalarClass is null ||
            min < 0 ||
            max < min ||
            max == int.MaxValue ||
            !IsAsciiAnyTextSet(setNode.ScalarClass))
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
            setNode.ScalarClass is null ||
            min < 0 ||
            max < min ||
            max == int.MaxValue ||
            !IsAsciiDotSet(setNode.ScalarClass))
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
    private static bool IsAsciiDotSet(Utf8ScalarClass scalarClass) =>
        Utf8ScalarAsciiProjector.TryProjectAsciiQuery(scalarClass, out var mask) &&
        mask.ActualLowMask == (ulong.MaxValue & ~(1UL << '\n')) && mask.ActualHighMask == ulong.MaxValue;

    private static bool IsAsciiAnyTextSet(Utf8ScalarClass scalarClass) =>
        Utf8ScalarAsciiProjector.TryProjectAsciiQuery(scalarClass, out var mask) &&
        mask.ActualLowMask == ulong.MaxValue && mask.ActualHighMask == ulong.MaxValue;

    private static byte[] GetShortestLiteral(byte[][] literals)
    {
        var owner = Utf8ConstructionScope.CurrentBudget;
        owner.ChargeWork(1); var shortest = literals[0];
        for (var index = 1; index < literals.Length; index++)
        {
            owner.ChargeWork(1);
            if (literals[index].Length < shortest.Length) shortest = literals[index];
        }
        return shortest;
    }

    private static Utf8TreeNode UnwrapRuntimeNode(Utf8TreeNode node, Utf8ConstructionFrames<ScanFrame> frames)
    {
        var owner = Utf8ConstructionScope.CurrentBudget;
        if (!ReferenceEquals(frames[frames.Count - 1].Node, node)) frames.Push(new(node, 0, false));
        while (true)
        {
            owner.ChargeWork(1);
            if (node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || node.ChildCount != 1)
                return node;
            node = node.Child(0); frames.Push(new(node, 0, false));
        }
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
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        if (!TryExtractLiteralFamilyStrings(node, out var literals) || literals.Length == 0) return false;
        using var arena = Utf8ConstructionArena.Create(owner);
        var result = arena.Allocate<byte[]>(literals.Length, true);
        for (var index = 0; index < literals.Length; index++)
        {
            var literal = literals[index]; owner.ChargeWork((ulong)literal.Length);
            if (!IsPlainAsciiLiteral(literal)) return false;
            var bytes = arena.Allocate<byte>(literal.Length, true);
            owner.ChargeWork((ulong)literal.Length);
            for (var position = 0; position < bytes.Length; position++) bytes[position] = (byte)literal[position];
            result[index] = bytes;
        }
        owner.ReleaseArray(literals, false);
        literalsUtf8 = result; arena.Complete(); construction.Complete(); return true;
    }

    private static bool TryExtractLiteralFamilyStrings(Utf8TreeNode node, out string[] literals)
        => TryExtractLiteralFamilyStringsCore(node, false, out literals);

    private static bool TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings(Utf8TreeNode node, out string[] literals)
        => TryExtractLiteralFamilyStringsCore(node, true, out literals);

    private struct LiteralFamilyFrame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int NextChild;
        internal bool Entered;
        internal bool PassThrough;
        internal string[] Values = [];
    }

    // Preserve branch reservations, concatenation products and their order.
    // Ancestors retain depth while child results resume them. Scratch arrays
    // remain in the arena; only the final reference array transfers its loan.
    private static bool TryExtractLiteralFamilyStringsCore(Utf8TreeNode node, bool foldCase, out string[] literals)
    {
        literals = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<LiteralFamilyFrame>.Create(owner, new(node));
        using var arena = Utf8ConstructionArena.Create(owner);
        var familyBudget = owner.AllocateObject((4096, 256), static limits => new Utf8PreparationBudget(limits.Item1, limits.Item2));
        string[] values = [];
        while (frames.Count != 0)
        {
            owner.ChargeWork(1);
            var index = frames.Count - 1; var frame = frames[index]; node = frame.Node;
            if (!frame.Entered)
            {
                frame.Entered = true;
                if (node.ChildCount == 1 && (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group ||
                    foldCase && node.Kind == Utf8TreeNodeKind.Atomic))
                {
                    frame.PassThrough = true; frames[index] = frame;
                    frames.Push(new(node.Child(0))); continue;
                }
                if (node.Kind is Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Alternate)
                {
                    var alternate = node.Kind == Utf8TreeNodeKind.Alternate;
                    if (alternate && !familyBudget.TryReserve(node.ChildCount, node.ChildCount)) return false;
                    frame.Values = arena.Allocate<string>(alternate ? node.ChildCount : 1, false);
                    if (!alternate) frame.Values[0] = string.Empty;
                    if (node.ChildCount == 0) { values = frame.Values; frames.Pop(); continue; }
                    frame.NextChild = 1; frames[index] = frame;
                    frames.Push(new(node.Child(0))); continue;
                }
                string literal;
                if (foldCase && node.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Multi or Utf8TreeNodeKind.Set &&
                    TryGetInvariantIgnoreCaseAsciiLiteralText(node, out literal)) { }
                else if (!foldCase && node.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Multi &&
                    TryGetLiteralText(node, out literal)) { }
                else if (!foldCase && node.Kind is Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setloopatomic or Utf8TreeNodeKind.Setlazy &&
                    node.ScalarClass is { } set && node.M >= 1 && node.N == int.MaxValue)
                {
                    if (!IsAsciiWhitespaceOnlySet(set)) return false;
                    literal = " ";
                }
                else return false;
                values = arena.Allocate<string>(1, false); values[0] = literal;
                frames.Pop(); continue;
            }
            if (frame.PassThrough) { frames.Pop(); continue; }
            if (node.Kind == Utf8TreeNodeKind.Alternate)
            {
                if (values.Length != 1) return false;
                frame.Values[frame.NextChild - 1] = values[0];
            }
            else
            {
                if (!familyBudget.TryReserveProduct(frame.Values.Length, values.Length, 256, out var product)) return false;
                var next = arena.Allocate<string>(product, false); var output = 0;
                foreach (var left in frame.Values)
                foreach (var right in values)
                {
                    owner.ChargeWork(1);
                    if (left.Length == 0) next[output++] = right;
                    else if (right.Length == 0) next[output++] = left;
                    else
                    {
                        using var text = Utf8ConstructionText.Create();
                        text.Append(left); text.Append(right); next[output++] = text.ToText();
                    }
                }
                frame.Values = next;
            }
            if (frame.NextChild == node.ChildCount) { values = frame.Values; frames.Pop(); continue; }
            var child = node.Child(frame.NextChild++); frames[index] = frame; frames.Push(new(child));
        }
        var result = arena.Allocate<string>(values.Length, true);
        owner.ChargeWork((ulong)values.Length); values.CopyTo(result, 0);
        literals = result; arena.Complete(); construction.Complete();
        return true;
    }

    private struct FoldedLiteralFrame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int NextChild;
        internal int Start;
        internal bool Entered;
    }

    private static bool TryGetInvariantIgnoreCaseAsciiLiteralText(Utf8TreeNode node, out string literal)
    {
        literal = string.Empty;
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        using var frames = Utf8ConstructionFrames<FoldedLiteralFrame>.Create(owner, new(node));
        using var text = Utf8ConstructionText.Create();
        while (frames.Count != 0)
        {
            owner.ChargeWork(1);
            var index = frames.Count - 1; var frame = frames[index]; node = frame.Node;
            if (!frame.Entered)
            {
                frame.Entered = true; frame.Start = text.Length;
                if (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && node.ChildCount == 1)
                {
                    frame.NextChild = 1; frames[index] = frame; frames.Push(new(node.Child(0))); continue;
                }
                switch (node.Kind)
                {
                    case Utf8TreeNodeKind.One when node.Ch <= 0x7f:
                        text.Append((char)Internal.Search.AsciiSearch.FoldCase((byte)node.Ch)); break;
                    case Utf8TreeNodeKind.Multi when node.Str is { } borrowed:
                        foreach (var value in borrowed)
                        {
                            owner.ChargeWork(1);
                            if (value > 0x7f) return false;
                            text.Append((char)Internal.Search.AsciiSearch.FoldCase((byte)value));
                        }
                        break;
                    case Utf8TreeNodeKind.Set when node.ScalarClass is { } set:
                        if (!TryGetInvariantIgnoreCaseAsciiSetLiteral(set, out var folded)) return false;
                        text.Append((char)folded); break;
                    case Utf8TreeNodeKind.Concatenate:
                        if (node.ChildCount == 0) return false;
                        frame.NextChild = 1; frames[index] = frame; frames.Push(new(node.Child(0))); continue;
                    default: return false;
                }
                frames.Pop(); continue;
            }
            if (node.Kind == Utf8TreeNodeKind.Concatenate)
            {
                if (frame.NextChild < node.ChildCount)
                {
                    var child = node.Child(frame.NextChild++); frames[index] = frame; frames.Push(new(child)); continue;
                }
                if (text.Length == frame.Start) return false;
            }
            frames.Pop();
        }
        literal = text.ToText(); construction.Complete(); return true;
    }

    private static bool TryGetInvariantIgnoreCaseAsciiSetLiteral(Utf8ScalarClass scalarClass, out byte literal)
    {
        literal = 0;
        var owner = Utf8ConstructionScope.CurrentBudget;
        // An ASCII intersection alone does not prove the whole class is this literal.
        if (!Utf8ScalarAsciiProjector.TryProjectPositiveAsciiDefinition(scalarClass, out var mask))
        {
            return false;
        }

        var seen = false;
        var folded = byte.MaxValue;
        for (var value = 0; value < 128; value++)
        {
            owner.ChargeWork(1);
            if (!mask.Contains((byte)value))
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
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.ChargeWork(1);
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.One:
                    literal = owner.CopyText([node.Ch]); construction.Complete(); return true;
                case Utf8TreeNodeKind.Multi when node.Str is not null:
                    literal = node.Str; return true;
                default:
                    literal = string.Empty; return false;
            }
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool IsNativeLiteralExecutionPattern(string executionPattern, RegexOptions executionOptions)
    {
        Utf8ConstructionScope.ChargeWork((ulong)executionPattern.Length);
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
