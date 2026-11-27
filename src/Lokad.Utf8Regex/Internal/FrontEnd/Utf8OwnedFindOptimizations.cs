namespace Lokad.Utf8Regex.Internal.FrontEnd;

using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
// Owned find-optimization facts: lengths, anchors, alternate prefixes and
// fixed-distance literals and sets. Iterative walks over owned runtime-shaped
// trees. Literal text is pattern interoperability text, not subject matching
// units. Width bounds describe scalar byte consumption; exact-distance facts
// require fixed byte widths rather than the minimum length of a variable prefix.
internal static class Utf8OwnedFindOptimizations
{
    public readonly record struct ByteWidth(int Minimum, int? Maximum)
    {
        internal bool IsFixed => Maximum is int maximum && maximum == Minimum;
    }

    public static (ByteWidth Width, (Utf8TreeNode Node, int Distance)? FixedLiteral) MeasureByteFacts(
        Utf8TreeNode node, bool selectFixedLiteral = true)
    {
        var leaf = node.ChildCount == 0;
        // A leaf has no continuation or retained storage to roll back.
        using var construction = leaf && Utf8ConstructionScope.IsActive ? null : Utf8ConstructionScope.Enter();
        var budget = Utf8ConstructionScope.CurrentBudget;
        if (leaf)
        {
            budget.EnterDepth();
            try
            {
                budget.Checkpoint();
                var width = new WidthFrame(node).Complete();
                return (width, selectFixedLiteral && width.IsFixed && ExactLiteralLength(node) > 0 ? (node, 0) : null);
            }
            finally { budget.LeaveDepth(); }
        }

        using var frames = Utf8ConstructionFrames<WidthFrame>.Create(budget, new(node));
        (Utf8TreeNode Node, int Distance)? fixedLiteral = null;
        var bestLength = 0;
        var distance = 0;
        // Only concatenation and its structural wrappers expose their children
        // as a sequence. Use one completed width for an alternative, assertion
        // or repeat, without treating its children as mandatory literals.
        var prefixAtom = selectFixedLiteral && IsPrefixAtom(node) ? 0 : -1;
        while (true)
        {
            budget.Checkpoint();
            var index = frames.Count - 1;
            var frame = frames[index];
            if (frame.NextChild < frame.Node.ChildCount)
            {
                var child = frame.Node.Child(frame.NextChild++);
                frames[index] = frame;
                if (selectFixedLiteral && prefixAtom < 0 && IsPrefixAtom(child))
                    prefixAtom = index + 1;
                frames.Push(new(child));
                continue;
            }

            var width = frame.Complete();
            if (index == prefixAtom)
            {
                prefixAtom = -1;
                // The first variable-width atom ends the fixed-distance
                // prefix. The rest of the tree still contributes to bounds.
                selectFixedLiteral = width.IsFixed && width.Minimum <= int.MaxValue - distance;
                if (selectFixedLiteral)
                {
                    var length = ExactLiteralLength(frame.Node);
                    if (length > 0 && length >= bestLength)
                    {
                        fixedLiteral = (frame.Node, distance);
                        bestLength = length;
                    }
                    distance += width.Minimum;
                }
            }
            frames.Pop();
            if (index == 0) return (width, fixedLiteral);

            var parent = frames[index - 1];
            parent.AddChild(width, budget);
            frames[index - 1] = parent;
        }
    }

    private static bool IsPrefixAtom(Utf8TreeNode node) =>
        node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic or Utf8TreeNodeKind.Concatenate);

    private static int ExactLiteralLength(Utf8TreeNode node)
    {
        var length = node.Kind == Utf8TreeNodeKind.One ? 1 : node.Kind == Utf8TreeNodeKind.Multi ? node.Str?.Length ?? 0 : 0;
        return length > 0 && IsExactLiteral(node) ? length : 0;
    }

    private struct WidthFrame(Utf8TreeNode node)
    {
        internal readonly Utf8TreeNode Node = node;
        internal int NextChild;
        private ByteWidth _width = node.Kind is Utf8TreeNodeKind.Alternate or Utf8TreeNodeKind.BackreferenceConditional or
            Utf8TreeNodeKind.ExpressionConditional ? new(int.MaxValue, 0) : new(0, 0);

        internal void AddChild(ByteWidth child, Utf8ConstructionBudget budget)
        {
            switch (Node.Kind)
            {
                case Utf8TreeNodeKind.Loop:
                case Utf8TreeNodeKind.Lazyloop:
                    _width = child;
                    break;
                case Utf8TreeNodeKind.Capture:
                case Utf8TreeNodeKind.Group:
                case Utf8TreeNodeKind.Atomic:
                case Utf8TreeNodeKind.Concatenate:
                    budget.Checkpoint();
                    _width = new(CapMinimum((long)_width.Minimum + child.Minimum),
                        _width.Maximum is int head && child.Maximum is int tail ? CapMaximum((long)head + tail) : null);
                    break;
                case Utf8TreeNodeKind.ExpressionConditional when NextChild == 1:
                    // The condition selects a branch without consuming input.
                    break;
                case Utf8TreeNodeKind.Alternate:
                case Utf8TreeNodeKind.BackreferenceConditional:
                case Utf8TreeNodeKind.ExpressionConditional:
                    budget.Checkpoint();
                    _width = new(Math.Min(_width.Minimum, child.Minimum),
                        _width.Maximum is int current && child.Maximum is int bound ? Math.Max(current, bound) : null);
                    break;
            }
        }

        internal readonly ByteWidth Complete() => Node.Kind switch
        {
            Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop => Node.ChildCount == 1
                ? Repeat(_width, Node.M, Node.N) : new(0, null),
            Utf8TreeNodeKind.Alternate when Node.ChildCount == 0 => new(0, 0),
            Utf8TreeNodeKind.BackreferenceConditional when Node.ChildCount < 2 => new(0, _width.Maximum),
            Utf8TreeNodeKind.ExpressionConditional when Node.ChildCount < 3 => new(0, _width.Maximum),
            Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic or Utf8TreeNodeKind.Concatenate or
                Utf8TreeNodeKind.Alternate or Utf8TreeNodeKind.BackreferenceConditional or Utf8TreeNodeKind.ExpressionConditional => _width,
            _ => MeasureAtomWidth(Node),
        };
    }

    // Bounds concern consumed UTF-8 bytes. Assertions are zero-width; conditions
    // select their result branches, and references have no independent maximum.
    // Complex classes/case fibers may have conservative bounds. Equality is a
    // fixed-byte certificate only when the upper bound is known and unsaturated.
    private static ByteWidth MeasureAtomWidth(Utf8TreeNode node)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.One:
                return ComputeScalarByteWidth(node.Ch, node.MatchCaseInsensitive, node.Options);
            case Utf8TreeNodeKind.Multi:
                if (node.LiteralUtf8 is { } bytes)
                {
                    long nativeLow = 0, nativeHigh = 0;
                    for (var offset = 0; offset < bytes.Length;)
                    {
                        budget.Checkpoint();
                        if (Rune.DecodeFromUtf8(bytes.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done)
                            return new(0, null);
                        var atom = ComputeScalarByteWidth(scalar.Value, node.MatchCaseInsensitive, node.Options);
                        nativeLow += atom.Minimum; nativeHigh += atom.Maximum.GetValueOrDefault(); offset += consumed;
                    }
                    return Bounds(nativeLow, nativeHigh);
                }
                if (node.Str is not { } text) return new(0, null);
                long low = 0, high = 0;
                for (var offset = 0; offset < text.Length;)
                {
                    budget.Checkpoint();
                    if (Rune.DecodeFromUtf16(text.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done)
                        return new(0, null);
                    var atom = ComputeScalarByteWidth(scalar.Value, node.MatchCaseInsensitive, node.Options);
                    low += atom.Minimum; high += atom.Maximum.GetValueOrDefault(); offset += consumed;
                }
                return Bounds(low, high);
            case Utf8TreeNodeKind.Set:
                return Class(node);
            case Utf8TreeNodeKind.Oneloop:
            case Utf8TreeNodeKind.Onelazy:
                return Repeat(ComputeScalarByteWidth(node.Ch, node.MatchCaseInsensitive, node.Options), node.M, node.N);
            case Utf8TreeNodeKind.Setloop:
            case Utf8TreeNodeKind.Setlazy:
                return Repeat(Class(node), node.M, node.N);
            case Utf8TreeNodeKind.Backreference:
            case Utf8TreeNodeKind.Unknown:
                return new(0, null);
            case Utf8TreeNodeKind.Bol:
            case Utf8TreeNodeKind.Eol:
            case Utf8TreeNodeKind.Boundary:
            case Utf8TreeNodeKind.NonBoundary:
            case Utf8TreeNodeKind.Beginning:
            case Utf8TreeNodeKind.Start:
            case Utf8TreeNodeKind.EndZ:
            case Utf8TreeNodeKind.End:
            case Utf8TreeNodeKind.Nothing:
            case Utf8TreeNodeKind.Empty:
            case Utf8TreeNodeKind.PositiveLookaround:
            case Utf8TreeNodeKind.NegativeLookaround:
            case Utf8TreeNodeKind.ECMABoundary:
            case Utf8TreeNodeKind.NonECMABoundary:
                return new(0, 0);
            default:
                return new(0, null);
        }

        static ByteWidth Bounds(long low, long high) => new(CapMinimum(low), CapMaximum(high));
        static ByteWidth Range(int low, int high)
        {
            var minimum = 4; var maximum = 0;
            if (low < 0 || high > 0x110000 || high <= low) return new(0, null);
            if (low < 0x80 && high > 0) { minimum = 1; maximum = 1; }
            if (low < 0x800 && high > 0x80) { minimum = Math.Min(minimum, 2); maximum = 2; }
            if (low < 0xd800 && high > 0x800 || low < 0x10000 && high > 0xe000)
                { minimum = Math.Min(minimum, 3); maximum = 3; }
            if (high > 0x10000) { minimum = Math.Min(minimum, 4); maximum = 4; }
            return maximum == 0 ? new(0, null) : new(minimum, maximum);
        }
        static ByteWidth Class(Utf8TreeNode node)
        {
            var owner = Utf8ConstructionScope.CurrentBudget;
            if (node.ScalarClass is not { } scalarClass) return new(0, null);
            var data = scalarClass.Data;
            var ranges = data[1]; var categories = data[2];
            if (data.Length != 3 + ranges + categories || data[0] != 0 || categories != 0) return new(1, 4);
            var minimum = 4; var maximum = 0;
            for (var index = 0; index < ranges; index += 2)
            {
                owner.Checkpoint();
                var low = data[3 + index]; var high = data[4 + index];
                var range = Range(low, high);
                if (range.Maximum is null) return new(0, null);
                if (node.MatchCaseInsensitive)
                {
                    if (high - low > 128) return new(1, 4);
                    for (var value = low; value < high; value++)
                    {
                        var scalar = ComputeScalarByteWidth(value, true, node.Options);
                        minimum = Math.Min(minimum, scalar.Minimum); maximum = Math.Max(maximum, scalar.Maximum.GetValueOrDefault());
                    }
                }
                else { minimum = Math.Min(minimum, range.Minimum); maximum = Math.Max(maximum, range.Maximum.Value); }
            }
            return maximum == 0 ? new(0, null) : new(minimum, maximum);
        }
    }

    private static int CapMinimum(long value) => value > int.MaxValue ? int.MaxValue : (int)value;
    private static int? CapMaximum(long value) => value > int.MaxValue ? null : (int)value;

    private static ByteWidth Repeat(ByteWidth body, int minimum, int maximum)
    {
        if (minimum < 0 || maximum < minimum) return new(0, null);
        return new(CapMinimum((long)body.Minimum * minimum), maximum == 0 || body.Maximum == 0 ? 0 :
            maximum == int.MaxValue || body.Maximum is null ? null : CapMaximum((long)body.Maximum.Value * maximum));
    }

    internal static ByteWidth ComputeScalarByteWidth(int scalar, bool folded, RegexOptions options)
    {
        var owner = Utf8ConstructionScope.CurrentBudget; owner.Checkpoint();
        if (!Rune.IsValid(scalar)) return new(0, null);
        var width = new Rune(scalar).Utf8SequenceLength;
        if (!folded) return new(width, width);
        // Supplementary equivalence uses runtime scalar casing, not this BMP
        // table. Keep sound bounds without assuming cross-plane closure.
        if (scalar > 0xffff) return new(1, 4);
        var low = width; var high = width;
        var lastBehavior = (options & RegexOptions.CultureInvariant) != 0 ? 0 : 2;
        for (var behavior = 0; behavior <= lastBehavior; behavior++)
        {
            owner.Checkpoint();
            foreach (var equivalent in Internal.Execution.Utf8SliceCaseEquivalences.GetEquivalents((char)scalar,
                (Internal.Execution.Utf8ScalarCaseBehavior)behavior))
            {
                owner.Checkpoint(); var bytes = new Rune(equivalent).Utf8SequenceLength;
                low = Math.Min(low, bytes); high = Math.Max(high, bytes);
            }
        }
        return new(low, high);
    }

    public static Utf8TreeNodeKind FindTrailingAnchor(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        budget.EnterDepth();
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic or Utf8TreeNodeKind.Concatenate)
        {
            budget.Checkpoint();
            if (node.ChildCount == 0) return Utf8TreeNodeKind.Unknown;
            node = node.Child(node.ChildCount - 1);
            budget.EnterDepth();
        }
        budget.Checkpoint();
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.EndZ:
            case Utf8TreeNodeKind.End:
            case Utf8TreeNodeKind.Eol:
                return node.Kind;
            default:
                return Utf8TreeNodeKind.Unknown;
        }
    }
    // These are exact mandatory fragments, not a representative spelling of a
    // branch or a case fiber. Their pattern text must encode complete scalars.
    // Trees keep logical subject order even when the VM executes right to left.
    internal static bool IsExactLiteral(Utf8TreeNode node)
    {
        if (node.Kind == Utf8TreeNodeKind.One) return ExactScalar(node.Ch, node);
        if (node.Kind != Utf8TreeNodeKind.Multi) return false;
        if (node.LiteralUtf8 is { Length: > 0 } native)
        {
            Utf8ConstructionScope.Checkpoint();
            for (var offset = 0; offset < native.Length;)
            {
                if (Rune.DecodeFromUtf8(native.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done ||
                    !ExactScalar(scalar.Value, node)) return false;
                offset += consumed;
            }
            return true;
        }
        if (node.Str is not { Length: > 0 } text) return false;
        // Admit the bounded pattern-text scan before it runs. Supplementary
        // pairs are text interoperability storage, not matching coordinates.
        Utf8ConstructionScope.Checkpoint();
        for (var offset = 0; offset < text.Length;)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done ||
                !ExactScalar(scalar.Value, node)) return false;
            offset += consumed;
        }
        return true;

        static bool ExactScalar(int value, Utf8TreeNode node)
        {
            Utf8ConstructionScope.Checkpoint();
            if (!Rune.IsValid(value)) return false;
            if (!node.MatchCaseInsensitive) return true;
            // The BMP table proves identity. Supplementary folded literals use
            // scalar runtime casing and cannot acquire an identity certificate
            // from this table, so decline this optional exact-search fact.
            if (value > 0xffff) return false;
            var last = (node.Options & RegexOptions.CultureInvariant) != 0 ? 0 : 2;
            for (var behavior = 0; behavior <= last; behavior++)
            {
                Utf8ConstructionScope.Checkpoint();
                foreach (var equivalent in Utf8SliceCaseEquivalences.GetEquivalents((char)value,
                    (Utf8ScalarCaseBehavior)behavior))
                {
                    Utf8ConstructionScope.Checkpoint();
                    if (equivalent != value) return false;
                }
            }
            return true;
        }
    }

    public static string? TryExtractLeadingLiteral(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        var depth = budget.Depth;
        budget.EnterDepth();
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group)
        {
            budget.Checkpoint();
            if (node.ChildCount != 1) return null;
            node = node.Child(0);
            budget.EnterDepth();
        }
        budget.Checkpoint();
        var result = node.Kind switch
        {
            Utf8TreeNodeKind.One when IsExactLiteral(node) => budget.CopyText([node.Ch]),
            Utf8TreeNodeKind.Multi when IsExactLiteral(node) => node.Str,
            Utf8TreeNodeKind.Concatenate => LeadingRun(node),
            _ => null,
        };
        if (result is not null)
        {
            // Keep the returned literal and release the depth of the linear walk.
            budget.Rollback(budget.Reserved, budget.ReservedBytes, depth);
            construction.Complete();
        }
        return result;

        static string? LeadingRun(Utf8TreeNode parent)
        {
            var budget = Utf8ConstructionScope.CurrentBudget;
            using var run = Utf8ConstructionText.Create();
            for (var i = 0; i < parent.ChildCount; i++)
            {
                budget.Checkpoint();
                var child = parent.Child(i);
                if (child.Kind == Utf8TreeNodeKind.One && IsExactLiteral(child))
                {
                    run.Append(child.Ch);
                }
                else if (child.Kind == Utf8TreeNodeKind.Multi && child.Str is { } text && IsExactLiteral(child))
                {
                    run.Append(text);
                }
                else if (child.Kind == Utf8TreeNodeKind.Empty)
                {
                    continue;
                }
                else if (run.Length == 0 &&
                    (child.Kind == Utf8TreeNodeKind.Bol || child.Kind == Utf8TreeNodeKind.Eol))
                {
                    continue;
                }
                else
                {
                    break;
                }
            }

            return run.Length == 0 ? null : run.ToText();
        }
    }

    public static string? TryExtractLongestLiteral(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<LongestLiteralFrame>.Create(budget, new(node));
        while (true)
        {
            budget.Checkpoint();
            ref var frame = ref frames.Peek();
            var current = frame.Node;
            var kind = current.Kind;
            // Concatenations inspect direct literal runs. Only wrappers and
            // alternatives consume the facts of their child subtrees.
            var usesChildFacts = kind == Utf8TreeNodeKind.Alternate ||
                (kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group && current.ChildCount == 1);
            if (usesChildFacts && frame.NextChild < current.ChildCount &&
                (frame.NextChild == 0 || frame.Literal is not null))
            {
                var child = current.Child(frame.NextChild++);
                frames.Push(new(child));
                continue;
            }

            var literal = kind switch
            {
                Utf8TreeNodeKind.One when IsExactLiteral(current) => budget.CopyText([current.Ch]),
                Utf8TreeNodeKind.Multi when IsExactLiteral(current) => current.Str,
                Utf8TreeNodeKind.Concatenate => LongestRun(current, budget),
                _ => frame.Literal,
            };
            frames.Pop();
            if (frames.Count == 0)
            {
                if (literal is not null) construction.Complete();
                return literal;
            }

            ref var parent = ref frames.Peek();
            if (parent.NextChild == 1)
            {
                parent.Literal = literal;
            }
            else if (literal is not null && parent.Literal is { } common && literal.Length == common.Length)
            {
                // An alternate contributes a literal only when every branch
                // proves the same spelling.
                budget.Checkpoint();
                if (!string.Equals(common, literal, StringComparison.Ordinal)) parent.Literal = null;
            }
            else
            {
                parent.Literal = null;
            }
        }

        static string? LongestRun(Utf8TreeNode parent, Utf8ConstructionBudget budget)
        {
            string? best = null;
            using var run = Utf8ConstructionText.Create();
            for (var i = 0; i < parent.ChildCount; i++)
            {
                budget.Checkpoint();
                var child = parent.Child(i);
                if (child.Kind == Utf8TreeNodeKind.One && IsExactLiteral(child))
                {
                    run.Append(child.Ch);
                }
                else if (child.Kind == Utf8TreeNodeKind.Multi && child.Str is { } text && IsExactLiteral(child))
                {
                    run.Append(text);
                }
                else if (child.Kind != Utf8TreeNodeKind.Empty)
                {
                    best = Longer(best, run.ToText());
                    run.Clear();
                }
            }
            return Longer(best, run.ToText());
        }

        static string? Longer(string? best, string next) => next.Length > (best?.Length ?? 0) ? next : best;
    }

    // A covering family is proved from the canonical logical tree. Partial
    // prefixes cover every alternative and never authorize a following suffix.
    public static byte[][]? FindAlternatePrefixes(Utf8TreeNode node, RegexOptions options)
        => Utf8OwnedPrefixFamilies.Find(node, options);

    private struct LongestLiteralFrame(Utf8TreeNode node)
    {
        internal readonly Utf8TreeNode Node = node;
        internal int NextChild;
        internal string? Literal;
    }

}
