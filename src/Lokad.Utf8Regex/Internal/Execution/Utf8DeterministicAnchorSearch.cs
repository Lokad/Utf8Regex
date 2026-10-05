using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8DeterministicAnchorSearch(PreparedSearcher Searcher, int Offset)
{
    private const int MaxDeterministicAnchorBranches = 32;
    private const int MaxDeterministicSetChoices = 4;

    public bool HasValue => Searcher.HasValue;

    public static bool CanUseForByteSafeLinear(
        Utf8ExecutionTree? tree,
        Utf8DeterministicAnchorSearch anchor,
        Utf8StructuralSearchPlan structuralSearchPlan)
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
                new PreparedSearcher(new Internal.Search.PreparedSubstringSearch(fixedLiteral, ignoreCase: false), ignoreCase: false),
                searchPlan.Distance);
        }

        if (TryExtractDeterministicLeadingAsciiLiteralFamily(tree.Root, out var leadingLiterals))
        {
            if (TryCreateAsciiIgnoreCaseAnchorLiteral(leadingLiterals, out var ignoreCaseLiteral))
            {
                return new Utf8DeterministicAnchorSearch(
                    new PreparedSearcher(new Internal.Search.PreparedSubstringSearch(ignoreCaseLiteral, ignoreCase: true), ignoreCase: true),
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
        var frames = new Stack<(Utf8ExecutionNode Node, int NextChild, int Before)>();
        while (true)
        {
            node = Unwrap(node);
            if (node.Kind == Utf8ExecutionNodeKind.Concatenate && node.Children.Count > 0)
            {
                frames.Push((node, 1, prefix.Length));
                node = node.Children[0];
                continue;
            }
            AppendScalar(node, prefix);
            while (frames.TryPop(out var frame))
            {
                if (prefix.Length == frame.Before || frame.NextChild == frame.Node.Children.Count) continue;
                frames.Push((frame.Node, frame.NextChild + 1, prefix.Length));
                node = frame.Node.Children[frame.NextChild];
                break;
            }
            if (frames.Count == 0) return;
        }

        static void AppendScalar(Utf8ExecutionNode node, StringBuilder prefix)
        {
            switch (node.Kind)
            {
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
        var frames = new Stack<(Utf8ExecutionNode Node, int NextChild, ulong Low, ulong High)>();
        while (true)
        {
            node = Unwrap(node);
            if (node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop && node.Children.Count == 1)
            {
                node = node.Children[0];
                continue;
            }
            if (node.Kind == Utf8ExecutionNodeKind.Concatenate)
            {
                var first = node.Children.Select(Unwrap).FirstOrDefault(static child => !IsZeroWidthNode(child));
                if (first is not null)
                {
                    node = first;
                    continue;
                }
            }
            if (node.Kind == Utf8ExecutionNodeKind.Alternate && node.Children.Count > 0)
            {
                frames.Push((node, 1, 0, 0));
                node = node.Children[0];
                continue;
            }
            var result = ReadScalar(node, out lowMask, out highMask);
            while (frames.TryPop(out var frame))
            {
                if (!result)
                {
                    // A failed union keeps only its previously completed masks.
                    lowMask = frame.Low;
                    highMask = frame.High;
                    continue;
                }
                lowMask |= frame.Low;
                highMask |= frame.High;
                if (frame.NextChild == frame.Node.Children.Count)
                {
                    result = (lowMask | highMask) != 0;
                    continue;
                }
                frames.Push((frame.Node, frame.NextChild + 1, lowMask, highMask));
                node = frame.Node.Children[frame.NextChild];
                break;
            }
            if (frames.Count == 0) return result;
        }

        static bool ReadScalar(Utf8ExecutionNode node, out ulong lowMask, out ulong highMask)
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

                case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                    AddByteToMask((byte)node.Ch, ref lowMask, ref highMask);
                    return true;

                case Utf8ExecutionNodeKind.Multi when TryGetAsciiBytes(node.Text, out var text) && text.Length > 0:
                    AddByteToMask(text[0], ref lowMask, ref highMask);
                    return true;

                case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set && Internal.FrontEnd.Utf8OwnedCharClass.IsAscii(set):
                    return TryBuildAsciiSetMask(set, out lowMask, out highMask);

                case Utf8ExecutionNodeKind.Loop:
                case Utf8ExecutionNodeKind.LazyLoop:
                    if (node.Ch is > (char)0 and <= (char)0x7F)
                    {
                        AddByteToMask((byte)node.Ch, ref lowMask, ref highMask);
                        return true;
                    }

                    if (node.Text is { Length: > 0 } loopSet && Internal.FrontEnd.Utf8OwnedCharClass.IsAscii(loopSet))
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
    }

    private static bool TryBuildAsciiSetMask(string set, out ulong lowMask, out ulong highMask)
    {
        lowMask = 0;
        highMask = 0;

        for (var i = 0; i < 128; i++)
        {
            if (Internal.FrontEnd.Utf8OwnedCharClass.CharInClass((char)i, set))
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

    // PLAN.md D06: single owner for execution-tree analysis primitives shared with the byte-safe linear verifier.
    internal static bool IsZeroWidthNode(Utf8ExecutionNode node)
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
        var pending = new Stack<Utf8ExecutionNode>();
        pending.Push(node);
        while (pending.TryPop(out node))
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
                    if (node.Text is null || !Internal.FrontEnd.Utf8OwnedCharClass.IsAscii(node.Text))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }
            for (var i = node.Children.Count - 1; i >= 0; i--) pending.Push(node.Children[i]);
        }
        return true;
    }

    internal static Utf8ExecutionNode Unwrap(Utf8ExecutionNode node)
    {
        while (node.Kind is Utf8ExecutionNodeKind.Capture or Utf8ExecutionNodeKind.Group &&
               node.Children.Count == 1)
        {
            node = node.Children[0];
        }

        return node;
    }

    internal static bool TryGetAsciiBytes(string? text, out byte[] bytes)
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
        var frames = new Stack<(Utf8ExecutionNode Node, int NextChild, List<string>? Families)>();
        while (true)
        {
            node = Unwrap(node);
            if (node.Kind is Utf8ExecutionNodeKind.Concatenate or Utf8ExecutionNodeKind.Alternate && node.Children.Count > 0)
            {
                frames.Push((node, 1, null));
                node = node.Children[0];
                continue;
            }
            var result = ReadScalar(node);
            while (frames.TryPop(out var frame))
            {
                var union = frame.Node.Kind == Utf8ExecutionNodeKind.Alternate;
                if (result is not { Count: > 0 })
                {
                    result = union ? null : frame.Families is { Count: > 0 } ? frame.Families : null;
                    continue;
                }
                List<string>? families;
                if (union)
                {
                    families = frame.Families ?? [];
                    foreach (var literal in result)
                    {
                        if (families.Contains(literal, StringComparer.Ordinal)) continue;
                        families.Add(literal);
                        if (families.Count <= MaxDeterministicAnchorBranches) continue;
                        families = null;
                        break;
                    }
                }
                else
                    families = frame.Families is null ? result : CombineLeadingLiteralFamilies(frame.Families, result);
                result = families;
                if (families is null || frame.NextChild == frame.Node.Children.Count) continue;
                frames.Push((frame.Node, frame.NextChild + 1, families));
                node = frame.Node.Children[frame.NextChild];
                break;
            }
            if (frames.Count == 0) return result;
        }

        static List<string>? ReadScalar(Utf8ExecutionNode node)
        {
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.Empty:
                case Utf8ExecutionNodeKind.Bol:
                case Utf8ExecutionNodeKind.Beginning:
                case Utf8ExecutionNodeKind.Start:
                    return [string.Empty];

                case Utf8ExecutionNodeKind.One when node.Ch <= 0x7F:
                    return [node.Ch.ToString()];

                case Utf8ExecutionNodeKind.Multi when node.Text is { Length: > 0 } text && text.All(static ch => ch <= 0x7F):
                    return [text];

                case Utf8ExecutionNodeKind.Set when node.Text is { Length: > 0 } set:
                    return TryExtractSetChoices(set);

                default:
                    return null;
            }
        }
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
        if (!Internal.FrontEnd.Utf8OwnedCharClass.CanEasilyEnumerateSetContents(set))
        {
            return null;
        }

        var chars = Internal.FrontEnd.Utf8OwnedCharClass.GetSetChars(set);
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
