using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Lowers the authoritative owned tree to the shared UTF-8 VM.
/// Capture identities and scoped options are already resolved by the front end;
/// this pass translates execution order and decoded class unions without
/// lexing the pattern again.</summary>
internal static class Utf8OwnedTreeSliceLowerer
{
    internal static Utf8SliceProgram? TryLower(Utf8OwnedRegexTree tree, RegexOptions options)
    {
        if ((options & RegexOptions.NonBacktracking) != 0 ||
            tree.CaptureCatalog is not { } catalog)
            return null;

        var hasBalancing = false;
        // Both VM lanes retain interpreted rollback for the documented .NET
        // 10.0.12 capture-writing finite-lazy-loop defect. Lowering does not
        // reproduce compiled-oracle phantom matches or abandoned captures.
        // The same shared assertion trail retains interpreted positions for
        // .NET 10.0.12's compiled RTL conditional/capture-reference defect:
        // (?x:(?(?=(?<x>a+)) \k<x> | b)+) can return a different earlier
        // range on aaabab after a later match. Regression oracles use the
        // ordinary engine for this diagnosed case, per the user's guidance.
        var hasCaptureWritingLazyRange = false;
        var root = TryConvertRoot(tree.Root, catalog, ref hasBalancing, ref hasCaptureWritingLazyRange);
        return root is null ? null : Utf8SliceLowerer.Lower(root, catalog.GroupNames,
            hasBalancing, tree.UsesIgnoreCase || (options & RegexOptions.IgnoreCase) != 0,
            (options & RegexOptions.RightToLeft) != 0, catalog,
            (options & RegexOptions.Compiled) != 0 && hasCaptureWritingLazyRange,
            tree.UsesLiteralClassBracketSyntax);

    }

    internal static Utf8SliceNode? TryConvertRoot(Utf8TreeNode root, Utf8CaptureCatalog catalog,
        ref bool hasBalancing, ref bool hasCaptureWritingLazyRange)
    {
        var nodes = new Dictionary<Utf8TreeNode, Utf8SliceNode?>();
        var writes = new Dictionary<Utf8TreeNode, bool>();
        var frames = new Stack<(Utf8TreeNode Node, int Depth, int NextChild)>();
        frames.Push((root, 0, 0));
        while (frames.TryPop(out var frame))
        {
            // Retain the existing structural admission while other native
            // preparation/runtime consumers are still being converted.
            if (frame.Depth > 2 * Utf8SliceParser.MaxNestingDepth + 2) return null;
            var node = frame.Node;
            if (frame.NextChild < node.ChildCount)
            {
                frames.Push((node, frame.Depth, frame.NextChild + 1));
                frames.Push((node.Child(frame.NextChild), frame.Depth + 1, 0));
                continue;
            }
            var captures = node.Kind == Utf8TreeNodeKind.Capture && (node.M > 0 || node.N >= 0);
            for (var index = 0; index < node.ChildCount; index++) captures |= writes[node.Child(index)];
            writes[node] = captures;
            nodes[node] = ConvertNode(node, catalog, frame.Depth, nodes, writes, ref hasBalancing, ref hasCaptureWritingLazyRange);
        }
        return nodes[root];

        static Utf8SliceNode? ConvertNode(Utf8TreeNode node, Utf8CaptureCatalog catalog, int depth,
            Dictionary<Utf8TreeNode, Utf8SliceNode?> nodes, Dictionary<Utf8TreeNode, bool> writes,
            ref bool hasBalancing, ref bool hasCaptureWritingLazyRange)
        {
            var foldCase = node.MatchCaseInsensitive;
            var rightToLeft = (node.Options & RegexOptions.RightToLeft) != 0;
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.Empty:
                case Utf8TreeNodeKind.UpdateBumpalong:
                    return new Utf8SliceNode.EmptyNode();
                case Utf8TreeNodeKind.Nothing:
                    return new Utf8SliceNode.FailNode();
                case Utf8TreeNodeKind.One:
                    return new Utf8SliceNode.LiteralNode(node.Ch, foldCase);
                case Utf8TreeNodeKind.Notone:
                    return NotOne(node.Ch, foldCase);
                case Utf8TreeNodeKind.Set:
                    return node.Str is { } set ? DecodeClass(set, foldCase) : null;
                case Utf8TreeNodeKind.Multi:
                    if (node.Str is not { } text) return null;
                    var literals = new List<Utf8SliceNode>();
                    for (var index = 0; index < text.Length;)
                    {
                        var width = char.IsSurrogatePair(text, index) ? 2 : 1;
                        var value = width == 2 ? char.ConvertToUtf32(text, index) : text[index];
                        literals.Add(new Utf8SliceNode.LiteralNode(value, foldCase));
                        index += width;
                    }
                    if (rightToLeft) literals.Reverse();
                    return new Utf8SliceNode.ConcatNode(literals.ToArray());
                case Utf8TreeNodeKind.Concatenate:
                    var children = new Utf8SliceNode[node.ChildCount];
                    for (var index = 0; index < children.Length; index++)
                    {
                        var child = nodes[node.Child(rightToLeft ? children.Length - 1 - index : index)];
                        if (child is null) return null;
                        children[index] = child;
                    }
                    return new Utf8SliceNode.ConcatNode(children);
                case Utf8TreeNodeKind.Alternate:
                    var branches = new Utf8SliceNode[node.ChildCount][];
                    for (var index = 0; index < branches.Length; index++)
                    {
                        var branch = nodes[node.Child(index)];
                        if (branch is null) return null;
                        branches[index] = [branch];
                    }
                    return new Utf8SliceNode.AlternationNode(branches);
                case Utf8TreeNodeKind.Oneloop:
                case Utf8TreeNodeKind.Onelazy:
                case Utf8TreeNodeKind.Oneloopatomic:
                case Utf8TreeNodeKind.Notoneloop:
                case Utf8TreeNodeKind.Notonelazy:
                case Utf8TreeNodeKind.Notoneloopatomic:
                case Utf8TreeNodeKind.Setloop:
                case Utf8TreeNodeKind.Setlazy:
                case Utf8TreeNodeKind.Setloopatomic:
                    var atom = node.Kind switch
                    {
                        Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Onelazy or Utf8TreeNodeKind.Oneloopatomic =>
                            new Utf8SliceNode.LiteralNode(node.Ch, foldCase),
                        Utf8TreeNodeKind.Notoneloop or Utf8TreeNodeKind.Notonelazy or Utf8TreeNodeKind.Notoneloopatomic =>
                            NotOne(node.Ch, foldCase),
                        _ => node.Str is { } wire ? DecodeClass(wire, foldCase) : null,
                    };
                    if (atom is null) return null;
                    var compactRepeat = new Utf8SliceNode.RepeatNode(atom, node.M, node.N,
                        node.Kind is Utf8TreeNodeKind.Onelazy or Utf8TreeNodeKind.Notonelazy or Utf8TreeNodeKind.Setlazy);
                    return node.Kind is Utf8TreeNodeKind.Oneloopatomic or Utf8TreeNodeKind.Notoneloopatomic or Utf8TreeNodeKind.Setloopatomic
                        ? new Utf8SliceNode.AtomicNode(compactRepeat) : compactRepeat;
                case Utf8TreeNodeKind.Loop:
                case Utf8TreeNodeKind.Lazyloop:
                    if (node.ChildCount != 1) return null;
                    hasCaptureWritingLazyRange |= node.Kind == Utf8TreeNodeKind.Lazyloop &&
                        node.N != int.MaxValue && node.N > 1 && node.N > node.M && writes[node.Child(0)];
                    var repeatBody = nodes[node.Child(0)];
                    return repeatBody is null ? null : new Utf8SliceNode.RepeatNode(repeatBody, node.M, node.N,
                        node.Kind == Utf8TreeNodeKind.Lazyloop);
                case Utf8TreeNodeKind.Group:
                case Utf8TreeNodeKind.Capture:
                case Utf8TreeNodeKind.Atomic:
                case Utf8TreeNodeKind.PositiveLookaround:
                case Utf8TreeNodeKind.NegativeLookaround:
                    if (node.ChildCount != 1) return null;
                    var body = nodes[node.Child(0)];
                    if (body is null) return null;
                    if (node.Kind == Utf8TreeNodeKind.Group) return body;
                    if (node.Kind == Utf8TreeNodeKind.Atomic) return new Utf8SliceNode.AtomicNode(body);
                    if (node.Kind is Utf8TreeNodeKind.PositiveLookaround or Utf8TreeNodeKind.NegativeLookaround)
                        return rightToLeft
                            ? new Utf8SliceNode.LookbehindNode(body, node.Kind == Utf8TreeNodeKind.PositiveLookaround)
                            : new Utf8SliceNode.LookaheadNode(body, node.Kind == Utf8TreeNodeKind.PositiveLookaround);
                    if (node.N >= 0)
                    {
                        hasBalancing = true;
                        var popOrdinal = catalog.OrdinalFromNumber(node.N);
                        if (popOrdinal == 0) return new Utf8SliceNode.FailNode();
                        if (popOrdinal < 0) return null;
                        return new Utf8SliceNode.BalanceNode(popOrdinal - 1,
                            node.M < 0 ? -1 : catalog.OrdinalFromNumber(node.M) - 1, body);
                    }
                    if (node.M == 0) return depth == 0 ? body : null;
                    var ordinal = catalog.OrdinalFromNumber(node.M);
                    return ordinal <= 0 ? null : new Utf8SliceNode.CaptureNode(ordinal - 1, body);
                case Utf8TreeNodeKind.Backreference:
                    var reference = catalog.OrdinalFromNumber(node.M);
                    var ecmaReference = (node.Options & RegexOptions.ECMAScript) != 0;
                    return reference < 0 ? null : reference == 0
                        ? ecmaReference ? new Utf8SliceNode.EmptyNode() : new Utf8SliceNode.FailNode()
                        : new Utf8SliceNode.BackrefNode(reference - 1, foldCase, ecmaReference);
                case Utf8TreeNodeKind.BackreferenceConditional:
                case Utf8TreeNodeKind.ExpressionConditional:
                    var expression = node.Kind == Utf8TreeNodeKind.ExpressionConditional;
                    var firstBranch = expression ? 1 : 0;
                    if (node.ChildCount < firstBranch + 1 || node.ChildCount > firstBranch + 2) return null;
                    var yes = nodes[node.Child(firstBranch)];
                    var no = node.ChildCount == firstBranch + 2
                        ? nodes[node.Child(firstBranch + 1)]
                        : new Utf8SliceNode.EmptyNode();
                    var test = expression ? nodes[node.Child(0)] : null;
                    return yes is null || no is null || expression && test is null ? null
                        : new Utf8SliceNode.ConditionalNode(expression ? -1 : catalog.OrdinalFromNumber(node.M) - 1, yes, no, test);
                case Utf8TreeNodeKind.Bol:
                    return new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.Beginning, true);
                case Utf8TreeNodeKind.Beginning:
                    return new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.Beginning, false);
                case Utf8TreeNodeKind.Eol:
                    return new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.End, true);
                case Utf8TreeNodeKind.EndZ:
                    return new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.End, false);
                case Utf8TreeNodeKind.End:
                    return new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.VeryEnd, false);
                case Utf8TreeNodeKind.Start:
                    return new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.Contiguous, false);
                case Utf8TreeNodeKind.Boundary:
                case Utf8TreeNodeKind.NonBoundary:
                    return new Utf8SliceNode.BoundaryNode(node.Kind == Utf8TreeNodeKind.Boundary);
                case Utf8TreeNodeKind.ECMABoundary:
                case Utf8TreeNodeKind.NonECMABoundary:
                    return new Utf8SliceNode.BoundaryNode(node.Kind == Utf8TreeNodeKind.ECMABoundary, true);
                default:
                    return null;
            }
        }

        static Utf8SliceNode NotOne(char unit, bool foldCase)
        {
            if (unit < 128)
            {
                var mask = new byte[16];
                mask[unit >> 3] = (byte)(1 << (unit & 7));
                return new Utf8SliceNode.SetNode(mask, true, [], foldCase);
            }
            return new Utf8SliceNode.CategorySetNode(new byte[16], true, [],
                [new Utf8SliceNode.CategorySetMember(new Utf8SliceCategoryTable(0,
                    unit == char.MaxValue ? [unit] : [unit, (ushort)(unit + 1)]), false)], foldCase);
        }

        static Utf8SliceNode? DecodeClass(string wire, bool foldCase)
        {
            if (wire == Utf8OwnedCharClass.AnyScalarClass) return new Utf8SliceNode.DotNode(true);
            if (wire == "\u0001\u0002\u0000\n\v") return new Utf8SliceNode.DotNode(false);
            var position = 0;
            var segments = new List<Utf8SliceNode>();
            while (position < wire.Length)
            {
                if (wire.Length - position < 3) return null;
                var negated = wire[position] != 0;
                var rangeLength = (int)wire[position + 1];
                var categoryLength = (int)wire[position + 2];
                position += 3;
                if (rangeLength + categoryLength > wire.Length - position) return null;
                var ranges = new List<(int Low, int High)>();
                for (var index = 0; index < rangeLength; index += 2)
                {
                    var low = (int)wire[position + index];
                    var high = index + 1 == rangeLength || wire[position + index + 1] == 0
                        ? 65536 : wire[position + index + 1];
                    if (high <= low) return null;
                    ranges.Add((low, high));
                }
                position += rangeLength;
                var categories = new List<Utf8SliceNode.CategorySetMember>();
                var members = new List<Utf8SliceShorthand>();
                if (!DecodeCategories(wire.AsSpan(position, categoryLength), categories, members)) return null;
                position += categoryLength;
                ranges.Sort(static (left, right) => left.Low.CompareTo(right.Low));
                var mask = new byte[16];
                var unicode = new List<ushort>();
                var pendingLow = -1;
                var pendingHigh = -1;
                foreach (var range in ranges)
                {
                    for (var value = range.Low; value < Math.Min(range.High, 128); value++)
                        mask[value >> 3] |= (byte)(1 << (value & 7));
                    var low = Math.Max(range.Low, 128);
                    if (low >= range.High) continue;
                    if (pendingLow >= 0 && low <= pendingHigh) pendingHigh = Math.Max(pendingHigh, range.High);
                    else
                    {
                        if (pendingLow >= 0) AppendRange(unicode, pendingLow, pendingHigh);
                        pendingLow = low;
                        pendingHigh = range.High;
                    }
                }
                if (pendingLow >= 0) AppendRange(unicode, pendingLow, pendingHigh);
                if (unicode.Count != 0)
                    categories.Add(new Utf8SliceNode.CategorySetMember(new Utf8SliceCategoryTable(0, unicode.ToArray()), false));
                // Hybrid instructions consult the precomputed mask on ASCII;
                // their shorthand bits classify only the Unicode remainder.
                foreach (var member in members)
                    for (var value = 0; value < 128; value++)
                    {
                        var hit = ((int)member >> 1) switch
                        {
                            0 => char.IsDigit((char)value),
                            1 => Utf8OwnedCharClass.IsWordChar((char)value),
                            _ => char.IsWhiteSpace((char)value),
                        };
                        if (hit != (((int)member & 1) != 0)) mask[value >> 3] |= (byte)(1 << (value & 7));
                    }
                segments.Add(categories.Count == 0
                    ? new Utf8SliceNode.SetNode(mask, negated, members.ToArray(), foldCase)
                    : new Utf8SliceNode.CategorySetNode(mask, negated, members.ToArray(), categories.ToArray(), foldCase));
            }
            if (segments.Count == 0) return null;
            return segments.Count == 1 ? segments[0] : Utf8SliceClassProgram.Subtract(segments);
        }

        static void AppendRange(List<ushort> ranges, int low, int high)
        {
            ranges.Add((ushort)low);
            if (high < 65536) ranges.Add((ushort)high);
        }

        static bool DecodeCategories(ReadOnlySpan<char> encoded,
            List<Utf8SliceNode.CategorySetMember> categories, List<Utf8SliceShorthand> members)
        {
            for (var index = 0; index < encoded.Length; index++)
            {
                if (encoded[index] == Utf8OwnedCharClass.ScalarRangePredicateCode)
                {
                    if (++index >= encoded.Length) return false;
                    var length = encoded[index];
                    if ((length & 1) != 0 || length > (encoded.Length - index - 1) / 2) return false;
                    var ranges = new int[length];
                    for (var range = 0; range < length; range++)
                    {
                        var low = encoded[++index];
                        ranges[range] = low | encoded[++index] << 16;
                    }
                    for (var range = 0; range < length; range += 2)
                        if (ranges[range] < 0 || ranges[range] >= ranges[range + 1] || ranges[range + 1] > 0x110000) return false;
                    categories.Add(new Utf8SliceNode.CategorySetMember(Utf8SliceCategoryTable.ForScalarRanges(ranges), false));
                    continue;
                }
                if (encoded[index] == Utf8OwnedCharClass.RangePredicateCode)
                {
                    if (index + 2 >= encoded.Length) return false;
                    var negated = encoded[++index] != 0;
                    var length = encoded[++index];
                    if (length > encoded.Length - index - 1) return false;
                    var ranges = new ushort[length];
                    for (var range = 0; range < length; range++) ranges[range] = encoded[++index];
                    categories.Add(new Utf8SliceNode.CategorySetMember(new Utf8SliceCategoryTable(0, ranges), negated));
                    continue;
                }
                var code = (short)encoded[index];
                if (code is 100 or -100)
                {
                    members.Add(code > 0 ? Utf8SliceShorthand.Space : Utf8SliceShorthand.NonSpace);
                    continue;
                }
                if (code == 0)
                {
                    var close = index + 1;
                    while (close < encoded.Length && encoded[close] != 0) close++;
                    if (close >= encoded.Length || close == index + 1) return false;
                    uint positive = 0, negative = 0;
                    for (var member = index + 1; member < close; member++)
                    {
                        var value = (short)encoded[member];
                        var number = Math.Abs((int)value);
                        if (number is < 1 or > 30) return false;
                        if (value > 0) positive |= 1u << (number - 1);
                        else negative |= 1u << (number - 1);
                    }
                    var mask = positive == 0 ? negative : positive & ~negative;
                    categories.Add(new Utf8SliceNode.CategorySetMember(new Utf8SliceCategoryTable(mask, []),
                        positive == 0));
                    index = close;
                    continue;
                }
                var category = Math.Abs((int)code);
                if (category is < 1 or > 30) return false;
                categories.Add(new Utf8SliceNode.CategorySetMember(new Utf8SliceCategoryTable(1u << (category - 1), []), code < 0));
            }
            return true;
        }
    }
}
