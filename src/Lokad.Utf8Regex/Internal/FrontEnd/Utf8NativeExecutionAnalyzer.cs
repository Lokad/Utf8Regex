using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;
namespace Lokad.Utf8Regex.Internal.FrontEnd;


internal static class Utf8NativeExecutionAnalyzer
{
    public static bool TryAnalyze(
        Utf8SemanticRegex semanticRegex, Utf8RegexFeatures features, out Utf8RegexAnalysis analyzedRegex)
    {
        var executionOptions = semanticRegex.ExecutionOptions;
        if (semanticRegex.RuntimeTree is { UsesIgnoreCase: true } foldedTree &&
            Utf8SliceCaseEquivalences.GetBehavior(foldedTree.Culture, executionOptions) != Utf8ScalarCaseBehavior.Invariant)
        {
            // Native invariant byte folds cannot stand in for construction-
            // culture scalar equivalence, including scoped inline options.
            analyzedRegex = default;
            return false;
        }
        var (hasImpossibleAtom, supportsFiniteLiteral) = semanticRegex.RuntimeTree?.LiteralEligibility ?? default;
        if (hasImpossibleAtom)
        {
            // Native byte plans must never encode an impossible literal as
            // U+FFFD. The owned scalar lowerer emits failure for these atoms,
            // including under zero repetition and alternative/assertion bodies.
            analyzedRegex = default;
            return false;
        }
        if (TryAnalyzeLiteralTree(semanticRegex, features, executionOptions, out analyzedRegex))
        {
            return true;
        }

        if (supportsFiniteLiteral && TryAnalyzeFiniteLiteralTree(semanticRegex, features, executionOptions, out analyzedRegex))
        {
            return true;
        }

        analyzedRegex = default;
        return false;
    }

    private static bool TryAnalyzeLiteralTree(
        Utf8SemanticRegex semanticRegex, Utf8RegexFeatures features, RegexOptions executionOptions, out Utf8RegexAnalysis analyzedRegex)
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

            var freeSpacing = (executionOptions & RegexOptions.IgnorePatternWhitespace) != 0;
            var hasExecutionPattern = semanticRegex.Pattern is not null;
            var executionPattern = semanticRegex.ExecutionPattern;
            if (hasExecutionPattern && IsPlainLiteral(executionPattern, freeSpacing))
            {
                var accepted = TryCreateLiteralAnalyzedRegex(semanticRegex, features, executionPattern, executionOptions, out analyzedRegex);
                if (accepted) construction.Complete();
                return accepted;
            }

            if (!hasExecutionPattern && semanticRegex.RuntimeTree is { Root.ChildCount: 1 } nativeTree &&
                TryGetNativeLiteralBranch(nativeTree.Root.Child(0), executionOptions, out var nativeLiteral))
            {
                var accepted = TryCreateLiteralAnalyzedRegex(semanticRegex, features, nativeLiteral, executionOptions, out analyzedRegex);
                if (accepted) construction.Complete();
                return accepted;
            }

            // Named capture wrappers retain the same literal discovery. The
            // NonBacktracking admission proof separately excludes shared declarations
            // and scoped folding; offsets/results retain the owned scalar program.
            if ((executionOptions & RegexOptions.NonBacktracking) != 0 &&
                (executionOptions & RegexOptions.IgnoreCase) == 0 &&
                semanticRegex.RuntimeTree is { Root.ChildCount: 1 } literalTree &&
                TryGetLiteralBranchUtf8(literalTree.Root.Child(0), out var wrappedLiteral))
            {
                var accepted = TryCreateLiteralAnalyzedRegex(semanticRegex, features, wrappedLiteral, executionOptions, out analyzedRegex);
                if (accepted) construction.Complete();
                return accepted;
            }

            if (TryExtractBoundaryWrappedLiteralTree(semanticRegex, out var boundaryLiteral, out var leadingBoundary, out var trailingBoundary))
            {
                var accepted = TryCreateLiteralAnalyzedRegex(
                    semanticRegex,
                    features,
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
                var accepted = TryCreateLiteralAnalyzedRegex(semanticRegex, features, lookaheadLiteral, executionOptions, out analyzedRegex, trailingLiteralUtf8: trailingLiteralUtf8);
                if (accepted) construction.Complete();
                return accepted;
            }

            if (hasExecutionPattern && TryExtractSingleCapturedLiteralPattern(executionPattern, freeSpacing, out var capturedLiteral))
            {
                var accepted = TryCreateLiteralAnalyzedRegex(semanticRegex, features, capturedLiteral, executionOptions, out analyzedRegex);
                if (accepted) construction.Complete();
                return accepted;
            }

            if ((executionOptions & (RegexOptions.RightToLeft | RegexOptions.IgnoreCase)) == 0 &&
                TryExtractLiteralAlternationWithPositiveLiteralLookaheadTree(semanticRegex, executionOptions, out var lookaheadAlternates, out var alternationTrailingLiteralUtf8, out var lookaheadAlternationExecutionKind, out var lookaheadAlternationSearchKind))
            {
                analyzedRegex = Utf8RegexAnalysis.Create(
                    semanticRegex,
                    features,
                    new(lookaheadAlternationSearchKind)
                    {
                        AlternateLiteralsUtf8 = lookaheadAlternates,
                        TrailingLiteralUtf8 = alternationTrailingLiteralUtf8,
                    },
                    lookaheadAlternationExecutionKind);
                construction.Complete();
                return true;
            }

            if ((executionOptions & RegexOptions.RightToLeft) == 0 &&
                (executionOptions & RegexOptions.IgnoreCase) != 0 &&
                hasExecutionPattern &&
                TryExtractPlainAsciiLiteralAlternationPattern(executionPattern, freeSpacing, out var ignoreCaseAlternateLiterals))
            {
                analyzedRegex = Utf8RegexAnalysis.Create(
                    semanticRegex,
                    features,
                    new(Utf8SearchKind.AsciiFoldedByteLiterals) { AlternateLiteralsUtf8 = ignoreCaseAlternateLiterals },
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
                    owner.Checkpoint();
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
                    features,
                    new(searchKind)
                    {
                        AlternateLiteralsUtf8 = alternateLiterals,
                        LeadingBoundary = alternationLeadingBoundary,
                        TrailingBoundary = alternationTrailingBoundary,
                    },
                    executionKind);
                construction.Complete();
                return true;
            }

            analyzedRegex = default;
            return false;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryGetNativeLiteralBranch(Utf8TreeNode node, RegexOptions options, out byte[] literal)
    {
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group && node.ChildCount == 1)
        {
            Utf8ConstructionScope.Checkpoint();
            node = node.Child(0);
        }
        if (node.Kind == Utf8TreeNodeKind.Empty || node.Kind == Utf8TreeNodeKind.Concatenate && node.ChildCount == 0)
        { literal = []; return true; }
        if (node.Kind == Utf8TreeNodeKind.Multi && node.LiteralUtf8 is { } bytes &&
            node.MatchCaseInsensitive == ((options & RegexOptions.IgnoreCase) != 0))
        {
            literal = bytes;
            return true;
        }
        literal = [];
        return false;
    }

    private static bool TryCreateLiteralAnalyzedRegex(
        Utf8SemanticRegex semanticRegex, Utf8RegexFeatures features, byte[] literalUtf8, RegexOptions options,
        out Utf8RegexAnalysis analyzedRegex, byte[]? trailingLiteralUtf8 = null)
    {
        var ascii = IsPlainAsciiLiteral(literalUtf8);
        var folded = (options & RegexOptions.IgnoreCase) != 0;
        if (folded && !ascii) { analyzedRegex = default; return false; }
        var kind = folded ? NativeExecutionKind.AsciiLiteralIgnoreCase :
            ascii ? NativeExecutionKind.ExactAsciiLiteral : NativeExecutionKind.ExactUtf8Literal;
        analyzedRegex = Utf8RegexAnalysis.CreateLiteral(semanticRegex,
            features, Utf8FrontEndSearchAnalyzer.AnalyzeLiteral(literalUtf8, kind, trailingLiteralUtf8 ?? [],
                Utf8BoundaryRequirement.None, Utf8BoundaryRequirement.None), kind, literalUtf8);
        return true;
    }

    private static bool IsPlainAsciiLiteral(ReadOnlySpan<byte> bytes)
    {
        Utf8ConstructionScope.Checkpoint();
        foreach (var value in bytes)
            if (value > 0x7f || Utf8RegexSyntax.IsRegexMetaCharacter((char)value)) return false;
        return true;
    }

    private static bool TryCreateLiteralAnalyzedRegex(
        Utf8SemanticRegex semanticRegex, Utf8RegexFeatures features, ReadOnlySpan<char> literalPattern, RegexOptions executionOptions,
        out Utf8RegexAnalysis analyzedRegex, byte[]? trailingLiteralUtf8 = null,
        Utf8BoundaryRequirement leadingBoundary = Utf8BoundaryRequirement.None,
        Utf8BoundaryRequirement trailingBoundary = Utf8BoundaryRequirement.None)
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
                features,
                Utf8FrontEndSearchAnalyzer.AnalyzeLiteral(literalUtf8, executionKind, trailingLiteralUtf8 ?? [], leadingBoundary, trailingBoundary),
                executionKind,
                literalUtf8);
            construction.Complete();
            return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryAnalyzeFiniteLiteralTree(
        Utf8SemanticRegex semanticRegex, Utf8RegexFeatures features, RegexOptions executionOptions, out Utf8RegexAnalysis analyzedRegex)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            if (!SupportsNativeOptions(executionOptions) || semanticRegex.Pattern is not null && IsPlainLiteral(semanticRegex.ExecutionPattern))
            {
                analyzedRegex = default;
                return false;
            }

            if (Utf8FiniteLiteralLowerer.TryCreateLiteralBranches(semanticRegex, executionOptions, out var literalBranches))
            {
                if (literalBranches.Length == 1)
                {
                    var bytes = literalBranches[0];
                    var plainAscii = IsPlainAsciiLiteral(bytes);
                    var folded = (executionOptions & RegexOptions.IgnoreCase) != 0;
                    if (folded && !plainAscii) { analyzedRegex = default; return false; }
                    var kind = folded ? NativeExecutionKind.AsciiLiteralIgnoreCase :
                        plainAscii ? NativeExecutionKind.ExactAsciiLiteral : NativeExecutionKind.ExactUtf8Literal;
                    analyzedRegex = Utf8RegexAnalysis.CreateLiteral(semanticRegex, features,
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
                        owner.Checkpoint();
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
                    features,
                    new(ignoreCase ? Utf8SearchKind.AsciiFoldedByteLiterals : Utf8SearchKind.ExactAsciiLiterals)
                    { AlternateLiteralsUtf8 = literalBranches },
                    ignoreCase ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals : NativeExecutionKind.ExactUtf8Literals);
                construction.Complete();
                return true;
            }

            analyzedRegex = default;
            return false;
        }
        finally { owner.LeaveDepth(); }
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
        Utf8SemanticRegex semanticRegex, out byte[] literal, out byte[] trailingLiteralUtf8)
    {
        literal = []; trailingLiteralUtf8 = [];
        if (GetCaptureBody(semanticRegex) is not { } body || GetPositiveTailLookahead(body) is not { } lookahead)
            return false;

        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        owner.EnterDepth();
        owner.Checkpoint();
        owner.EnterDepth();
        owner.Checkpoint();
        owner.EnterDepth();
        owner.Checkpoint();
        var executionOptions = semanticRegex.ExecutionOptions;
        if (!TryGetNativeLookaheadLiteralUtf8(lookahead, executionOptions, out var trailingLiteral)) return false;
        owner.LeaveDepth();
        using var bytes = Utf8ConstructionBuffer<byte>.Create(owner);
        for (var index = 0; index < body.ChildCount - 1; index++)
        {
            owner.Checkpoint();
            if (!TryGetNativeLookaheadLiteralUtf8(body.Child(index), executionOptions, out var part))
                return false;
            bytes.AddRange(part);
        }
        literal = bytes.Take();
        if (literal.Length == 0) return false;
        owner.LeaveDepth();
        if (!IsNativeLiteralExecutionPattern(semanticRegex) || trailingLiteral.Length == 0) return false;
        trailingLiteralUtf8 = trailingLiteral;
        owner.LeaveDepth();
        construction.Complete(); return true;
    }

    private static bool TryExtractLiteralAlternationWithPositiveLiteralLookaheadTree(
        Utf8SemanticRegex semanticRegex, RegexOptions executionOptions, out byte[][] alternateLiteralsUtf8, out byte[] trailingLiteralUtf8, out NativeExecutionKind executionKind, out Utf8SearchKind searchKind)
    {
        alternateLiteralsUtf8 = []; trailingLiteralUtf8 = []; executionKind = default; searchKind = default;
        if (GetCaptureBody(semanticRegex) is not { ChildCount: 2 } body || GetPositiveTailLookahead(body) is not { } lookahead)
            return false;

        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        owner.EnterDepth();
        owner.Checkpoint();
        owner.EnterDepth();
        owner.Checkpoint();
        owner.EnterDepth();
        owner.Checkpoint();
        if (!TryGetNativeLookaheadLiteralUtf8(lookahead, executionOptions, out var trailingLiteral)) return false;
        owner.LeaveDepth();
        owner.LeaveDepth();
        if (!TryGetNativeLookaheadAlternationUtf8(body.Child(0), executionOptions, out var literalTexts) ||
            literalTexts.Length <= 1 || !IsNativeLiteralExecutionPattern(semanticRegex)) return false;
        var hasNonAscii = false;
        for (var index = 0; index < literalTexts.Length; index++)
        {
            owner.Checkpoint();
            var bytes = literalTexts[index];
            if (bytes.Length == 0) return false;
            owner.Checkpoint();
            foreach (var value in bytes) if (value > 0x7f) { hasNonAscii = true; break; }
        }
        trailingLiteralUtf8 = trailingLiteral;
        if (trailingLiteralUtf8.Length == 0) return false;
        var ignoreCase = (executionOptions & RegexOptions.IgnoreCase) != 0;
        if (ignoreCase && hasNonAscii) return false;
        executionKind = ignoreCase ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals : NativeExecutionKind.ExactUtf8Literals;
        searchKind = ignoreCase ? Utf8SearchKind.AsciiFoldedByteLiterals :
            hasNonAscii ? Utf8SearchKind.ExactUtf8Literals : Utf8SearchKind.ExactAsciiLiterals;
        alternateLiteralsUtf8 = literalTexts;
        owner.LeaveDepth();
        construction.Complete(); return true;
    }

    private static Utf8TreeNode? GetCaptureBody(Utf8SemanticRegex semanticRegex) =>
        semanticRegex.RuntimeTree?.Root is { Kind: Utf8TreeNodeKind.Capture, ChildCount: 1 } root ? root.Child(0) : null;

    private static Utf8TreeNode? GetPositiveTailLookahead(Utf8TreeNode body)
    {
        if (body.Kind != Utf8TreeNodeKind.Concatenate || body.ChildCount < 2) return null;
        var lookahead = body.Child(body.ChildCount - 1);
        return lookahead is { Kind: Utf8TreeNodeKind.PositiveLookaround, ChildCount: 1 } &&
            (lookahead.Options & RegexOptions.RightToLeft) == 0 ? lookahead.Child(0) : null;
    }

    private static bool TryGetNativeLookaheadLiteralUtf8(Utf8TreeNode node, RegexOptions executionOptions, out byte[] literal)
    {
        if (node.Kind == Utf8TreeNodeKind.Multi && node.LiteralUtf8 is { } bytes)
        { literal = bytes; return true; }
        using var construction = Utf8ConstructionScope.Enter();
        if (!TryGetNativeLookaheadLiteralText(node, executionOptions, out var text))
        { literal = []; return false; }
        literal = construction.Budget.EncodeUtf8(text);
        construction.Complete(); return true;
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

    private static bool TryGetNativeLookaheadAlternationUtf8(Utf8TreeNode node, RegexOptions executionOptions, out byte[][] literals)
    {
        using var construction = Utf8ConstructionScope.Enter();
        if (TryUnwrapLiteralAlternation(node, out literals)) { construction.Complete(); return true; }
        if ((executionOptions & RegexOptions.IgnoreCase) == 0 || (executionOptions & RegexOptions.CultureInvariant) == 0 ||
            !TryExtractInvariantIgnoreCaseAsciiLiteralFamilyStrings(node, out var texts)) return false;
        literals = construction.Budget.AllocateArray<byte[]>(texts.Length, false);
        for (var index = 0; index < texts.Length; index++)
        { construction.Budget.Checkpoint(); literals[index] = construction.Budget.EncodeUtf8(texts[index]); }
        construction.Budget.ReleaseArray(texts, false);
        construction.Complete(); return true;
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
        Utf8ConstructionScope.Checkpoint();
        foreach (var ch in pattern)
        {
            if (ch > 0x7F || Utf8RegexSyntax.IsRegexMetaCharacter(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPlainLiteral(ReadOnlySpan<char> pattern, bool freeSpacing = false)
    {
        Utf8ConstructionScope.Checkpoint();
        foreach (var ch in pattern)
        {
            if (Utf8RegexSyntax.IsRegexMetaCharacter(ch) || freeSpacing && IsFreeSpacingSyntax(ch))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TryExtractSingleCapturedLiteralPattern(ReadOnlySpan<char> pattern, bool freeSpacing, out ReadOnlySpan<char> literal)
    {
        literal = default;
        var owner = Utf8ConstructionScope.CurrentBudget;
        owner.EnterDepth();
        try
        {
            owner.Checkpoint();
            if (pattern.Length < 3 || pattern[0] != '(' || pattern[^1] != ')') return false;
            var inner = pattern[1..^1];
            if (!IsPlainLiteral(inner, freeSpacing)) return false;
            literal = inner;
            return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryExtractUtf8LiteralAlternationTree(
        Utf8SemanticRegex semanticRegex, out byte[][] alternateLiteralsUtf8, out Utf8BoundaryRequirement leadingBoundary, out Utf8BoundaryRequirement trailingBoundary)
    {
        alternateLiteralsUtf8 = []; leadingBoundary = Utf8BoundaryRequirement.None; trailingBoundary = Utf8BoundaryRequirement.None;
        if (GetCaptureBody(semanticRegex) is not { } body) return false;

        var leading = Utf8BoundaryRequirement.None;
        var trailing = Utf8BoundaryRequirement.None;
        var atom = body.Kind == Utf8TreeNodeKind.Concatenate
            ? GetBoundaryWrappedAtom(body, out leading, out trailing)
            : body;
        if (atom is null || atom.Kind is not (Utf8TreeNodeKind.Alternate or Utf8TreeNodeKind.Set or Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group))
            return false;

        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        owner.EnterDepth();
        owner.Checkpoint();
        owner.EnterDepth();
        owner.Checkpoint();
        if (!TryUnwrapLiteralAlternation(atom, out var literals) || literals.Length <= 1) return false;
        owner.LeaveDepth();
        for (var index = 0; index < literals.Length; index++)
        {
            owner.Checkpoint();
            if (literals[index].Length == 0) return false;
        }
        alternateLiteralsUtf8 = literals;
        leadingBoundary = leading;
        trailingBoundary = trailing;
        owner.LeaveDepth();
        construction.Complete();
        return true;
    }

    internal static bool TryExtractPlainAsciiLiteralAlternationPattern(ReadOnlySpan<char> pattern, bool freeSpacing, out byte[][] alternateLiteralsUtf8)
    {
        alternateLiteralsUtf8 = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            owner.Checkpoint();
            if (pattern.Length == 0 || pattern[0] == '|' || pattern[^1] == '|') return false;
            var count = 1; var length = 0;
            foreach (var value in pattern)
            {
                if (value == '|')
                { if (length == 0) return false; count++; length = 0; }
                else
                {
                    if (value > 0x7f || Utf8RegexSyntax.IsRegexMetaCharacter(value) || freeSpacing && IsFreeSpacingSyntax(value))
                        return false;
                    length++;
                }
            }
            if (count <= 1) return false;
            var result = owner.AllocateArray<byte[]>(count, false);
            var start = 0; var index = 0;
            for (var offset = 0; offset <= pattern.Length; offset++)
            {
                owner.Checkpoint();
                if (offset < pattern.Length && pattern[offset] != '|') continue;
                result[index++] = owner.EncodeUtf8(pattern[start..offset]);
                start = offset + 1;
            }
            alternateLiteralsUtf8 = result; construction.Complete(); return true;
        }
        finally { owner.LeaveDepth(); }
    }

    private static bool TryUnwrapLiteralAlternation(Utf8TreeNode node, out byte[][] literals)
    {
        literals = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        var depth = owner.Depth;
        owner.EnterDepth();
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group && node.ChildCount == 1)
        {
            owner.Checkpoint();
            node = node.Child(0);
            owner.EnterDepth();
        }
        owner.Checkpoint();
        // Two unfolded scalar members are a complete UTF-8 literal pair.
        // Keep ASCII classes on their existing lowering and borrow the pair
        // search for mixed/wide classes without adding a matching primitive.
        if (!(node.Kind == Utf8TreeNodeKind.Set && !node.MatchCaseInsensitive &&
            TryGetScalarClassLiteralPair(node.ScalarClass, owner, out literals)))
        {
            if (node.Kind != Utf8TreeNodeKind.Alternate) return false;
            var result = owner.AllocateArray<byte[]>(node.ChildCount, false);
            for (var index = 0; index < result.Length; index++)
            {
                owner.Checkpoint();
                if (!TryGetLiteralBranchUtf8(node.Child(index), out result[index])) return false;
            }
            literals = result;
        }
        // Keep the result loans while releasing depth from the single-path walk.
        owner.Rollback(owner.Reserved, owner.ReservedBytes, depth);
        construction.Complete();
        return true;
    }

    private static bool TryGetScalarClassLiteralPair(Utf8ScalarClass? scalarClass, Utf8ConstructionBudget owner, out byte[][] literals)
    {
        literals = [];
        if (scalarClass is null) return false;
        var data = scalarClass.Data.AsSpan();
        // Canonical positive ranges only: no negation, predicates or subtraction.
        if (data.Length < 5 || data[0] != 0 || data[2] != 0 ||
            data[1] is not (2 or 4) || data.Length != 3 + data[1]) return false;
        Span<int> scalars = stackalloc int[2];
        var count = 0;
        for (var index = 3; index < data.Length; index += 2)
        {
            for (var scalar = data[index]; scalar < data[index + 1]; scalar++)
            {
                owner.Checkpoint();
                if (count == scalars.Length || !Rune.IsValid(scalar)) return false;
                scalars[count++] = scalar;
            }
        }
        if (count != 2 || scalars[0] < 128 && scalars[1] < 128) return false;
        var result = owner.AllocateArray<byte[]>(2, false);
        for (var index = 0; index < result.Length; index++)
        {
            var rune = new Rune(scalars[index]);
            var bytes = owner.AllocateArray<byte>(rune.Utf8SequenceLength, false);
            rune.EncodeToUtf8(bytes);
            result[index] = bytes;
        }
        literals = result;
        return true;
    }

    private static bool TryGetLiteralBranchUtf8(Utf8TreeNode node, out byte[] literal)
    {
        literal = [];
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        var depth = owner.Depth;
        owner.EnterDepth();
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group && node.ChildCount == 1)
        {
            owner.Checkpoint();
            node = node.Child(0);
            owner.EnterDepth();
        }
        owner.Checkpoint();
        if (node.Kind is not Utf8TreeNodeKind.One and not Utf8TreeNodeKind.Multi ||
            !TryGetNativeLookaheadLiteralUtf8(node, node.Options, out literal)) return false;
        owner.Rollback(owner.Reserved, owner.ReservedBytes, depth);
        construction.Complete();
        return true;
    }

    private static bool TryExtractBoundaryWrappedLiteralTree(
        Utf8SemanticRegex semanticRegex, out string literal, out Utf8BoundaryRequirement leadingBoundary, out Utf8BoundaryRequirement trailingBoundary)
    {
        literal = string.Empty;
        leadingBoundary = Utf8BoundaryRequirement.None;
        trailingBoundary = Utf8BoundaryRequirement.None;
        if (GetCaptureBody(semanticRegex) is not { Kind: Utf8TreeNodeKind.Concatenate } body ||
            GetBoundaryWrappedAtom(body, out var leading, out var trailing) is not { } atom)
            return false;

        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        owner.EnterDepth();
        owner.Checkpoint();
        owner.EnterDepth();
        owner.EnterDepth();
        owner.Checkpoint();
        if (!TryGetLiteralText(atom, out var matchedLiteral) || matchedLiteral.Length == 0) return false;
        owner.LeaveDepth();
        if (!IsNativeLiteralExecutionPattern(semanticRegex)) return false;
        owner.LeaveDepth();
        owner.LeaveDepth();
        literal = matchedLiteral;
        leadingBoundary = leading;
        trailingBoundary = trailing;
        construction.Complete();
        return true;
    }

    private static Utf8TreeNode? GetBoundaryWrappedAtom(
        Utf8TreeNode body, out Utf8BoundaryRequirement leading, out Utf8BoundaryRequirement trailing)
    {
        leading = trailing = Utf8BoundaryRequirement.None;
        if (body.ChildCount is not (2 or 3)) return null;

        var first = body.Child(0);
        if (TryGetBoundaryRequirement(first.Kind, out leading))
        {
            if (body.ChildCount == 3 && !TryGetBoundaryRequirement(body.Child(2).Kind, out trailing)) return null;
            return body.Child(1);
        }
        return body.ChildCount == 2 && TryGetBoundaryRequirement(body.Child(1).Kind, out trailing) ? first : null;
    }

    private static bool TryGetBoundaryRequirement(Utf8TreeNodeKind kind, out Utf8BoundaryRequirement requirement)
    {
        requirement = kind switch
        {
            Utf8TreeNodeKind.Boundary => Utf8BoundaryRequirement.Boundary,
            Utf8TreeNodeKind.NonBoundary => Utf8BoundaryRequirement.NonBoundary,
            _ => Utf8BoundaryRequirement.None
        };

        return requirement != Utf8BoundaryRequirement.None;
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
            Utf8ConstructionScope.Checkpoint();
            if (mask.Contains((byte)value) && !Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value)) return false;
        }
        return true;
    }

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
            owner.Checkpoint();
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
                else if (!foldCase && node.Kind is Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy &&
                    node.ScalarClass is { } set && node.M >= 1 && node.IsUnboundedRepeat)
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
                        owner.Checkpoint();
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
        owner.Checkpoint(); values.CopyTo(result, 0);
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
            owner.Checkpoint();
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
                            owner.Checkpoint();
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
            owner.Checkpoint();
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
            owner.Checkpoint();
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

    private static bool IsNativeLiteralExecutionPattern(Utf8SemanticRegex semanticRegex)
    {
        // Native literals have already been resolved by the authoritative tree;
        // no source text is needed for this conservative raw-text eligibility check.
        if (semanticRegex.Pattern is null) return true;
        var executionPattern = semanticRegex.ExecutionPattern;
        Utf8ConstructionScope.Checkpoint();
        if ((semanticRegex.ExecutionOptions & RegexOptions.IgnorePatternWhitespace) == 0)
        {
            return true;
        }

        foreach (var ch in executionPattern)
        {
            if (IsFreeSpacingSyntax(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFreeSpacingSyntax(char ch) => char.IsWhiteSpace(ch) || ch == '#';
}
