using Lokad.Utf8Regex.Internal.Planning;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEndSearchAnalyzer
{
    public static Utf8SearchFacts Analyze(Utf8SemanticRegex semanticRegex)
    {
        if (semanticRegex.RuntimeTree is not { } tree)
            return default;

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
        TrySelectBoundaryRequirements(root, out var leadingBoundary, out var trailingBoundary);
        var facts = new Utf8SearchFacts
        {
            RequiredPrefilterLiteralUtf8 = requiredLiteral,
            RequiredPrefilterAlternateLiteralsUtf8 = requiredAlternatives,
            MinRequiredLength = width.Minimum,
            ExactRequiredLength = width.IsFixed ? width.Minimum : null,
            MaxPossibleLength = width.IsFixed ? null : width.Maximum,
            LeadingBoundary = leadingBoundary,
            TrailingBoundary = trailingBoundary,
        };

        // Keep search priority and prepare each candidate only when it can be used.
        if (TrySelectTrailingFixedLengthAnchor(root, width, out var trailingKind))
            return facts with { Kind = trailingKind };

        var invariantIgnoreCase = UsesInvariantIgnoreCase(options);
        if (!invariantIgnoreCase && TrySelectFixedDistanceLiteral(root) is { } fixedLiteral)
            return facts with { Kind = Utf8SearchKind.FixedDistanceAsciiLiteral, LiteralUtf8 = fixedLiteral };

        var prefixes = Utf8OwnedFindOptimizations.FindAlternatePrefixes(root, tree.Options);
        if (!invariantIgnoreCase && prefixes is { Length: > 1 })
        {
            var kind = prefixes.Any(static literal => literal.Any(static b => b > 0x7F))
                ? Utf8SearchKind.ExactUtf8Literals
                : Utf8SearchKind.ExactAsciiLiterals;
            return facts with { Kind = kind, AlternateLiteralsUtf8 = prefixes, CanGuideFallbackStarts = CanGuideFallbackStarts(options) };
        }

        var leadingLiteral = Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(root);
        var hasAsciiLeadingLiteral = leadingLiteral is { Length: > 0 } && IsAscii(leadingLiteral);
        var candidate = hasAsciiLeadingLiteral ? EncodeLiteral(leadingLiteral!) : SelectAsciiCandidateLiteral(root, prefixes);
        if (candidate.Length != 0)
        {
            var kind = invariantIgnoreCase ? Utf8SearchKind.AsciiFoldedByteLiteral : Utf8SearchKind.ExactAsciiLiteral;
            return facts with
            {
                Kind = kind,
                LiteralUtf8 = candidate,
                CanGuideFallbackStarts = hasAsciiLeadingLiteral && CanGuideFallbackStarts(options),
            };
        }

        return facts;
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
        byte[]? trailingLiteralUtf8,
        Utf8BoundaryRequirement leadingBoundary,
        Utf8BoundaryRequirement trailingBoundary)
    {
        var searchKind = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCase
            ? Utf8SearchKind.AsciiFoldedByteLiteral
            : Utf8SearchKind.ExactAsciiLiteral;

        return new(searchKind)
        {
            // This is the complete leading literal, not an interior anchor.
            CanGuideFallbackStarts = literalUtf8.Length > 0 && executionKind != NativeExecutionKind.AsciiLiteralIgnoreCase,
            LiteralUtf8 = literalUtf8,
            TrailingLiteralUtf8 = trailingLiteralUtf8,
            LeadingBoundary = leadingBoundary,
            TrailingBoundary = trailingBoundary,
        };
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

    private static byte[]? TrySelectFixedDistanceLiteral(Utf8TreeNode root)
    {
        // An exact literal cannot follow an uncertified byte width. Check the
        // first class or scalar repeat before allocating the per-node widths.
        var leading = GetLeadingContentNode(root);
        if (leading is { Kind: Utf8TreeNodeKind.Set } && !Utf8OwnedFindOptimizations.ComputeByteWidth(leading).IsFixed ||
            leading is { Kind: Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Onelazy or
                Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy } && (leading.M != leading.N || leading.IsUnboundedRepeat) ||
            Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(root) is not { } fixedDistance ||
            !IsAscii(fixedDistance.Literal) ||
            fixedDistance.Literal.Length <= 1)
            return null;

        return EncodeLiteral(fixedDistance.Literal);
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
