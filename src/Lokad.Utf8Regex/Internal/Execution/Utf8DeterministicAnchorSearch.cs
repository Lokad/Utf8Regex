using System.Text;
using Lokad.Utf8Regex.Internal.FrontEnd.Runtime;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8DeterministicAnchorSearch(PreparedSearcher Searcher, int Offset)
{
    private const int MaxDeterministicAnchorBranches = 32;
    private const int MaxDeterministicSetChoices = 4;

    public bool HasValue => Searcher.HasValue;

    public static bool CanUseForByteSafeLinear(
        Utf8ExecutionTree? tree,
        Utf8DeterministicAnchorSearch anchor,
        Utf8StructuralSearchPlan structuralSearchPlan = default)
    {
        return tree is not null &&
            IsByteSafeNode(tree.Root) &&
            ((anchor.HasValue &&
              anchor.Offset == 0 &&
              anchor.Searcher.Kind is not PreparedSearcherKind.ByteSet) ||
             HasSelectiveStructuralStartPlan(structuralSearchPlan));
    }

    public static Utf8DeterministicAnchorSearch Create(Utf8ExecutionTree? tree, Utf8SearchPlan searchPlan)
    {
        if (tree is null)
        {
            return default;
        }

        if (searchPlan.Kind == Utf8SearchKind.FixedDistanceAsciiLiteral &&
            searchPlan.LiteralUtf8 is { Length: > 0 } fixedLiteral &&
            HasDeterministicLeadingAnchor(tree.Root, fixedLiteral, searchPlan.Distance))
        {
            return new Utf8DeterministicAnchorSearch(
                new PreparedSearcher(new Internal.Utilities.PreparedSubstringSearch(fixedLiteral, ignoreCase: false), ignoreCase: false),
                searchPlan.Distance);
        }

        if (TryExtractDeterministicLeadingAsciiLiteralFamily(tree.Root, out var leadingLiterals))
        {
            if (TryCreateAsciiIgnoreCaseAnchorLiteral(leadingLiterals, out var ignoreCaseLiteral))
            {
                return new Utf8DeterministicAnchorSearch(
                    new PreparedSearcher(new Internal.Utilities.PreparedSubstringSearch(ignoreCaseLiteral, ignoreCase: true), ignoreCase: true),
                    0);
            }

            var literalFamilyUtf8 = leadingLiterals.Select(Encoding.UTF8.GetBytes).ToArray();
            return new Utf8DeterministicAnchorSearch(
                new PreparedSearcher(new PreparedMultiLiteralSearch(literalFamilyUtf8, ignoreCase: false)),
                0);
        }

        if (TryExtractDeterministicLeadingAsciiByteSet(tree.Root, out var leadingBytes))
        {
            return new Utf8DeterministicAnchorSearch(
                new PreparedSearcher(PreparedByteSearch.Create(leadingBytes)),
                0);
        }

        return default;
    }

    private static bool HasDeterministicLeadingAnchor(Utf8ExecutionNode root, ReadOnlySpan<byte> literal, int distance)
    {
        var prefix = new StringBuilder();
        AppendDeterministicLeadingAscii(root, prefix);
        if (distance < 0 || distance + literal.Length > prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < literal.Length; i++)
        {
            if (prefix[distance + i] != (char)literal[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void AppendDeterministicLeadingAscii(Utf8ExecutionNode node, StringBuilder prefix)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
                if (node.Children.Count == 1)
                {
                    AppendDeterministicLeadingAscii(node.Children[0], prefix);
                }

                return;

            case Utf8ExecutionNodeKind.Concatenate:
                foreach (var child in node.Children)
                {
                    var before = prefix.Length;
                    AppendDeterministicLeadingAscii(child, prefix);
                    if (prefix.Length == before)
                    {
                        return;
                    }
                }

                return;

            case Utf8ExecutionNodeKind.Empty:
            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
                return;

            case Utf8ExecutionNodeKind.One:
                if (node.Ch <= 0x7F)
                {
                    prefix.Append(node.Ch);
                }

                return;

            case Utf8ExecutionNodeKind.Multi:
                if (node.Text is not null)
                {
                    foreach (var ch in node.Text)
                    {
                        if (ch > 0x7F)
                        {
                            return;
                        }
                    }

                    prefix.Append(node.Text);
                }

                return;

            default:
                return;
        }
    }

    private static bool TryExtractDeterministicLeadingAsciiLiteralFamily(Utf8ExecutionNode node, out string[] literals)
    {
        var results = TryExtractLeadingAsciiLiteralsCore(node);
        if (results is not { Count: > 0 })
        {
            literals = [];
            return false;
        }

        literals = [.. results
            .Where(static value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)];
        return literals.Length > 1 && literals[0].Length > 0;
    }

    private static bool TryExtractDeterministicLeadingAsciiByteSet(Utf8ExecutionNode node, out byte[] values)
    {
        values = [];
        if (!TryGetLeadingAsciiByteMask(node, out var lowMask, out var highMask))
        {
            return false;
        }

        var bytes = new List<byte>(128);
        for (var i = 0; i < 64; i++)
        {
            if (((lowMask >> i) & 1UL) != 0)
            {
                bytes.Add((byte)i);
            }
        }

        for (var i = 0; i < 64; i++)
        {
            if (((highMask >> i) & 1UL) != 0)
            {
                bytes.Add((byte)(i + 64));
            }
        }

        values = [.. bytes];
        return values.Length > 0;
    }

    private static bool TryGetLeadingAsciiByteMask(Utf8ExecutionNode node, out ulong lowMask, out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;

        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Empty:
            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
                return false;

            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
                return node.Children.Count == 1 &&
                    TryGetLeadingAsciiByteMask(Unwrap(node.Children[0]), out lowMask, out highMask);

            case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                AddByteToMask((byte)node.Ch, ref lowMask, ref highMask);
                return true;

            case Utf8ExecutionNodeKind.Multi when TryGetAsciiBytes(node.Text, out var text) && text.Length > 0:
                AddByteToMask(text[0], ref lowMask, ref highMask);
                return true;

            case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set && RegexCharClass.IsAscii(set):
                return TryBuildAsciiSetMask(set, out lowMask, out highMask);

            case Utf8ExecutionNodeKind.Concatenate:
                foreach (var child in node.Children)
                {
                    var unwrapped = Unwrap(child);
                    if (IsZeroWidthNode(unwrapped))
                    {
                        continue;
                    }

                    return TryGetLeadingAsciiByteMask(unwrapped, out lowMask, out highMask);
                }

                return false;

            case Utf8ExecutionNodeKind.Alternate:
                foreach (var child in node.Children)
                {
                    if (!TryGetLeadingAsciiByteMask(Unwrap(child), out var childLowMask, out var childHighMask))
                    {
                        return false;
                    }

                    lowMask |= childLowMask;
                    highMask |= childHighMask;
                }

                return (lowMask | highMask) != 0;

            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                if (node.Children.Count == 1)
                {
                    return TryGetLeadingAsciiByteMask(Unwrap(node.Children[0]), out lowMask, out highMask);
                }

                if (node.Ch is > (char)0 and <= (char)0x7F)
                {
                    AddByteToMask((byte)node.Ch, ref lowMask, ref highMask);
                    return true;
                }

                if (node.Text is { Length: > 0 } loopSet && RegexCharClass.IsAscii(loopSet))
                {
                    return TryBuildAsciiSetMask(loopSet, out lowMask, out highMask);
                }

                if (TryGetAsciiBytes(node.Text, out var loopText) && loopText.Length > 0)
                {
                    AddByteToMask(loopText[0], ref lowMask, ref highMask);
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private static bool TryBuildAsciiSetMask(string set, out ulong lowMask, out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;

        for (var i = 0; i < 128; i++)
        {
            if (RegexCharClass.CharInClass((char)i, set))
            {
                AddByteToMask((byte)i, ref lowMask, ref highMask);
            }
        }

        return (lowMask | highMask) != 0;
    }

    private static void AddByteToMask(byte value, ref ulong lowMask, ref ulong highMask)
    {
        if (value < 64)
        {
            lowMask |= 1UL << value;
        }
        else
        {
            highMask |= 1UL << (value - 64);
        }
    }

    private static bool IsZeroWidthNode(Utf8ExecutionNode node)
    {
        return node.Kind is Utf8ExecutionNodeKind.Empty
            or Utf8ExecutionNodeKind.Bol
            or Utf8ExecutionNodeKind.Beginning
            or Utf8ExecutionNodeKind.Start
            or Utf8ExecutionNodeKind.Eol
            or Utf8ExecutionNodeKind.EndZ
            or Utf8ExecutionNodeKind.End
            or Utf8ExecutionNodeKind.Boundary
            or Utf8ExecutionNodeKind.NonBoundary;
    }

    private static bool IsByteSafeNode(Utf8ExecutionNode node)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Empty:
            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Concatenate:
            case Utf8ExecutionNodeKind.Alternate:
            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
            case Utf8ExecutionNodeKind.Eol:
            case Utf8ExecutionNodeKind.EndZ:
            case Utf8ExecutionNodeKind.End:
                break;

            case Utf8ExecutionNodeKind.One:
                if (node.Ch > 0x7F)
                {
                    return false;
                }

                break;

            case Utf8ExecutionNodeKind.Multi:
                if (node.Text is null || node.Text.Any(static ch => ch > 0x7F))
                {
                    return false;
                }

                break;

            case Utf8ExecutionNodeKind.Set:
                if (node.Text is null || !RegexCharClass.IsAscii(node.Text))
                {
                    return false;
                }

                break;

            default:
                return false;
        }

        foreach (var child in node.Children)
        {
            if (!IsByteSafeNode(child))
            {
                return false;
            }
        }

        return true;
    }

    private static Utf8ExecutionNode Unwrap(Utf8ExecutionNode node)
    {
        while (node.Kind is Utf8ExecutionNodeKind.Capture or Utf8ExecutionNodeKind.Group &&
               node.Children.Count == 1)
        {
            node = node.Children[0];
        }

        return node;
    }

    private static bool TryGetAsciiBytes(string? text, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] > 0x7F)
            {
                return false;
            }
        }

        bytes = text.Select(static ch => (byte)ch).ToArray();
        return true;
    }

    private static List<string>? TryExtractLeadingAsciiLiteralsCore(Utf8ExecutionNode node)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Empty:
            case Utf8ExecutionNodeKind.Bol:
            case Utf8ExecutionNodeKind.Beginning:
            case Utf8ExecutionNodeKind.Start:
                return [string.Empty];

            case Utf8ExecutionNodeKind.Capture:
            case Utf8ExecutionNodeKind.Group:
                return node.Children.Count == 1 ? TryExtractLeadingAsciiLiteralsCore(node.Children[0]) : null;

            case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                return [node.Ch.ToString()];

            case Utf8ExecutionNodeKind.Multi when node.Text is { Length: > 0 } text && text.All(static ch => ch <= 0x7F):
                return [text];

            case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set:
                return TryExtractSetChoices(set);

            case Utf8ExecutionNodeKind.Concatenate:
                return TryExtractConcatenatedLeadingLiterals(node.Children);

            case Utf8ExecutionNodeKind.Alternate:
                return TryExtractAlternatedLeadingLiterals(node.Children);

            default:
                return null;
        }
    }

    private static List<string>? TryExtractConcatenatedLeadingLiterals(IReadOnlyList<Utf8ExecutionNode> children)
    {
        List<string>? prefixes = null;
        foreach (var child in children)
        {
            var childLiterals = TryExtractLeadingAsciiLiteralsCore(child);
            if (childLiterals is not { Count: > 0 })
            {
                return prefixes is { Count: > 0 } ? prefixes : null;
            }

            prefixes = prefixes is null
                ? childLiterals
                : CombineLeadingLiteralFamilies(prefixes, childLiterals);

            if (prefixes is null)
            {
                return null;
            }
        }

        return prefixes;
    }

    private static List<string>? TryExtractAlternatedLeadingLiterals(IReadOnlyList<Utf8ExecutionNode> children)
    {
        List<string>? combined = null;
        foreach (var child in children)
        {
            var childLiterals = TryExtractLeadingAsciiLiteralsCore(child);
            if (childLiterals is not { Count: > 0 })
            {
                return null;
            }

            combined ??= [];
            foreach (var literal in childLiterals)
            {
                if (!combined.Contains(literal, StringComparer.Ordinal))
                {
                    combined.Add(literal);
                    if (combined.Count > MaxDeterministicAnchorBranches)
                    {
                        return null;
                    }
                }
            }
        }

        return combined;
    }

    private static List<string>? CombineLeadingLiteralFamilies(List<string> left, List<string> right)
    {
        var combined = new List<string>(left.Count * right.Count);
        foreach (var leftLiteral in left)
        {
            foreach (var rightLiteral in right)
            {
                combined.Add(leftLiteral + rightLiteral);
                if (combined.Count > MaxDeterministicAnchorBranches)
                {
                    return null;
                }
            }
        }

        return combined;
    }

    private static List<string>? TryExtractSetChoices(string set)
    {
        if (!RegexCharClass.CanEasilyEnumerateSetContents(set))
        {
            return null;
        }

        var chars = RegexCharClass.GetSetChars(set);
        if (chars.Length == 0 || chars.Length > MaxDeterministicSetChoices || chars.Any(static ch => ch > 0x7F))
        {
            return null;
        }

        return [.. chars.Select(static ch => ch.ToString())];
    }

    private static bool TryCreateAsciiIgnoreCaseAnchorLiteral(string[] literals, out byte[] literalUtf8)
    {
        literalUtf8 = [];
        if (literals.Length <= 1)
        {
            return false;
        }

        var width = literals[0].Length;
        if (width == 0 || literals.Any(literal => literal.Length != width))
        {
            return false;
        }

        Span<int> variantCounts = stackalloc int[width];
        Span<byte> foldedBytes = stackalloc byte[width];

        for (var offset = 0; offset < width; offset++)
        {
            var first = (byte)literals[0][offset];
            var folded = AsciiSearch.FoldCase(first);
            var sawLower = first == folded;
            var sawUpper = first != folded;

            for (var i = 1; i < literals.Length; i++)
            {
                var value = (byte)literals[i][offset];
                if (AsciiSearch.FoldCase(value) != folded)
                {
                    return false;
                }

                sawLower |= value == folded;
                sawUpper |= value != folded;
            }

            if (IsAsciiLetter(folded))
            {
                if (!(sawLower && sawUpper))
                {
                    return false;
                }

                variantCounts[offset] = 2;
            }
            else
            {
                variantCounts[offset] = 1;
            }

            foldedBytes[offset] = folded;
        }

        var expectedCount = 1;
        for (var i = 0; i < variantCounts.Length; i++)
        {
            expectedCount *= variantCounts[i];
            if (expectedCount > literals.Length)
            {
                return false;
            }
        }

        if (expectedCount != literals.Length)
        {
            return false;
        }

        literalUtf8 = foldedBytes.ToArray();
        return true;
    }

    private static bool IsAsciiLetter(byte value)
    {
        return (uint)(AsciiSearch.FoldCase(value) - (byte)'a') <= (byte)('z' - 'a');
    }

    private static bool HasSelectiveStructuralStartPlan(Utf8StructuralSearchPlan plan)
    {
        if (!(plan.HasValue && plan.YieldKind == Utf8StructuralSearchYieldKind.Start) ||
            plan.Stages is not { Length: > 0 } stages)
        {
            return false;
        }

        foreach (var stage in stages)
        {
            if (stage.Kind == Utf8StructuralSearchStageKind.TransformCandidateStart)
            {
                return true;
            }
        }

        return false;
    }
}
