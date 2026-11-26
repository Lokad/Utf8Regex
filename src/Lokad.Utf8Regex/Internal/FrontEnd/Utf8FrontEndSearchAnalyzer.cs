using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEndSearchAnalyzer
{
    private const int MaxDeterministicPrefixSets = 32;

    public static Utf8SearchFacts Analyze(Utf8SemanticRegex semanticRegex)
    {
        if (semanticRegex.RuntimeTree is not { } tree)
            return Utf8SearchFacts.Create(Utf8SearchKind.None);

        using var construction = Utf8ConstructionScope.Enter();
        var result = AnalyzeTree(tree, semanticRegex.ExecutionOptions);
        construction.Complete();
        return result;
    }

    private static Utf8SearchFacts AnalyzeTree(Utf8OwnedRegexTree tree, RegexOptions options)
    {
        var root = tree.Root;
        var width = Utf8OwnedFindOptimizations.ComputeByteWidth(root);
        TrySelectRequiredLiteralPrefilter(root, out var requiredLiteral, out var requiredAlternatives);
        TrySelectFallbackStartTransform(root, out var fallbackStartTransform);
        TrySelectBoundaryRequirements(root, out var leadingBoundary, out var trailingBoundary);
        var facts = new Utf8SearchFactData
        {
            RequiredPrefilterLiteralUtf8 = requiredLiteral ?? [],
            RequiredPrefilterAlternateLiteralsUtf8 = requiredAlternatives ?? [],
            FallbackStartTransform = fallbackStartTransform,
            ExactRequiredLength = width.IsFixed ? width.Minimum : null,
            MaxPossibleLength = width.IsFixed ? null : width.Maximum,
            LeadingBoundary = leadingBoundary,
            TrailingBoundary = trailingBoundary,
        };

        // Keep search priority and prepare each candidate only when it can be used.
        if (TrySelectTrailingFixedLengthAnchor(root, width, out var trailingKind))
            return Utf8SearchFacts.Create(trailingKind, facts with { MinRequiredLength = width.Minimum });

        var invariantIgnoreCase = UsesInvariantIgnoreCase(options);
        if (TrySelectFixedDistanceSets(root, invariantIgnoreCase, out var fixedSets))
            return Utf8SearchFacts.Create(Utf8SearchKind.FixedDistanceAsciiSets,
                facts with { FixedDistanceSets = fixedSets, MinRequiredLength = width.Minimum });

        if (TrySelectDeterministicPrefixSets(root, width.Minimum, fallbackStartTransform,
            out var deterministicSets, out var deterministicMinimum))
            return Utf8SearchFacts.Create(Utf8SearchKind.FixedDistanceAsciiSets,
                facts with { FixedDistanceSets = deterministicSets, MinRequiredLength = deterministicMinimum });

        if (TrySelectFixedDistanceLiteral(root, invariantIgnoreCase, out var fixedLiteral, out var distance))
            return Utf8SearchFacts.Create(Utf8SearchKind.FixedDistanceAsciiLiteral,
                facts with { LiteralUtf8 = fixedLiteral, Distance = distance, MinRequiredLength = width.Minimum });

        var prefixes = Utf8OwnedFindOptimizations.FindAlternatePrefixes(root, tree.Options);
        if (!invariantIgnoreCase && prefixes is { Length: > 1 })
        {
            var kind = prefixes.Any(static literal => literal.Any(static b => b > 0x7F))
                ? Utf8SearchKind.ExactUtf8Literals
                : Utf8SearchKind.ExactAsciiLiterals;
            return Utf8SearchFacts.Create(kind,
                facts with { AlternateLiteralsUtf8 = prefixes, CanGuideFallbackStarts = CanGuideFallbackStarts(options) });
        }

        var leadingLiteral = Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(root);
        var hasAsciiLeadingLiteral = leadingLiteral is { Length: > 0 } && IsAscii(leadingLiteral);
        var candidate = hasAsciiLeadingLiteral ? EncodeLiteral(leadingLiteral!) : SelectAsciiCandidateLiteral(root, prefixes);
        if (candidate.Length != 0)
        {
            var kind = invariantIgnoreCase ? Utf8SearchKind.AsciiFoldedByteLiteral : Utf8SearchKind.ExactAsciiLiteral;
            return Utf8SearchFacts.Create(kind, facts with
            {
                LiteralUtf8 = candidate,
                CanGuideFallbackStarts = hasAsciiLeadingLiteral && CanGuideFallbackStarts(options),
            });
        }

        return Utf8SearchFacts.Create(Utf8SearchKind.None, facts with { MinRequiredLength = width.Minimum });
    }

    private struct ScanFrame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int Index;
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
                       // This is the complete leading literal, not an interior anchor.
                       CanGuideFallbackStarts = literalUtf8.Length > 0 &&
                           executionKind != NativeExecutionKind.AsciiLiteralIgnoreCase,
                       LiteralUtf8 = literalUtf8,
                       TrailingLiteralUtf8 = trailingLiteralUtf8,
                       LeadingBoundary = leadingBoundary,
                       TrailingBoundary = trailingBoundary
                   });
    }

    private static byte[] SelectAsciiCandidateLiteral(Utf8TreeNode root, byte[][]? alternatePrefixes)
    {
        if (alternatePrefixes is [{ Length: > 0 } prefix])
            return IsAscii(prefix) ? prefix : [];

        // Leading literals were already screened. Check the interior fragment
        // before allocating UTF-8 for an ASCII-only candidate.
        return Utf8OwnedFindOptimizations.TryExtractLongestLiteral(root) is { Length: > 0 } literal && IsAscii(literal)
            ? EncodeLiteral(literal)
            : [];
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
        var leading = GetLeadingContentNode(root);
        if (leading is not null && IsLeadingAsciiWhitespaceNode(leading))
        {
            transform = new Utf8FallbackStartTransform(0, Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace);
            return true;
        }

        transform = default;
        return false;
    }

    private static Utf8TreeNode? GetLeadingContentNode(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        node = UnwrapStructuralNode(node);

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

    private static bool IsWhitespaceLoop(Utf8TreeNode node)
    {
        if (node.Kind is not (Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Set))
        {
            return false;
        }

        return Utf8ScalarAsciiProjector.IsPositiveWhitespace(node.ScalarClass);
    }

    private static bool IsLeadingAsciiWhitespaceNode(Utf8TreeNode node)
    {
        // The inverse candidate transform only walks six ASCII whitespace
        // bytes. Unicode \s also includes wider scalars; that definition cannot
        // certify distances relative to this transform's requirement base.
        node = UnwrapStructuralNode(node);
        return node.Kind is Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Set &&
            Utf8ScalarAsciiProjector.TryProjectPositiveAsciiDefinition(node.ScalarClass, out var mask) &&
            mask.ActualLowMask == 0x100003e00UL && mask.ActualHighMask == 0;
    }

    private static bool TrySelectTrailingFixedLengthAnchor(Utf8TreeNode root,
        Utf8OwnedFindOptimizations.ByteWidth width, out Utf8SearchKind kind)
    {
        kind = Utf8SearchKind.None;
        if (!width.IsFixed || width.Minimum == 0) return false;

        kind = Utf8OwnedFindOptimizations.FindTrailingAnchor(root) switch
        {
            Utf8TreeNodeKind.End => Utf8SearchKind.TrailingAnchorFixedLengthEnd,
            Utf8TreeNodeKind.EndZ => Utf8SearchKind.TrailingAnchorFixedLengthEndZ,
            _ => Utf8SearchKind.None,
        };
        return kind != Utf8SearchKind.None;
    }

    private static bool TrySelectFixedDistanceLiteral(Utf8TreeNode root, bool ignoreCase,
        out byte[] literalUtf8, out int distance)
    {
        literalUtf8 = [];
        distance = 0;
        if (ignoreCase ||
            Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(root) is not { } fixedDistance ||
            !IsAscii(fixedDistance.Literal) ||
            fixedDistance.Literal.Length <= 1)
            return false;

        literalUtf8 = EncodeLiteral(fixedDistance.Literal);
        distance = fixedDistance.Distance;
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
        // Its admitted UTF-8 payloads retain that proof without re-encoding.
        if (requiredFamily.Length == 1)
        {
            literalUtf8 = requiredFamily[0];
            return literalUtf8.Length > 0;
        }

        alternateLiteralsUtf8 = requiredFamily;
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

        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;

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
        Utf8TreeNode root, bool ignoreCase, out Utf8FixedDistanceSet[] fixedDistanceSets)
    {
        fixedDistanceSets = [];
        if (ignoreCase ||
            Utf8OwnedFindOptimizations.FindFixedDistanceSets(root) is not { Length: > 0 } runtimeSets)
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

        return true;
    }

    private static (Utf8TreeNode? Node, int StartIndex) SkipLeadingAsciiWhitespacePrefix(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var unwrapped = UnwrapStructuralNode(node);
        if (unwrapped.Kind != Utf8TreeNodeKind.Concatenate)
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
            firstContent = UnwrapStructuralNode(node.Child(startIndex));
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

    private static Utf8TreeNode UnwrapStructuralNode(Utf8TreeNode node)
    {
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic &&
               node.ChildCount == 1)
        {
            Utf8ConstructionScope.ChargeWork(1);
            node = node.Child(0);
        }

        return node;
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

    private static bool IsAscii(byte[] value)
    {
        foreach (var item in value)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (item > 0x7f) return false;
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

}
