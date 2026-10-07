namespace Lokad.Utf8Regex.Internal.FrontEnd;

using System.Text.RegularExpressions;
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

    public static ByteWidth ComputeByteWidth(Utf8TreeNode node) => MeasurePostOrder<ByteWidth>(node, MeasureByteWidth, false);

    // Bounds concern consumed UTF-8 bytes. Assertions are zero-width; conditions
    // select their result branches, and references have no independent maximum.
    // Complex classes/case fibers may have conservative bounds. Equality is a
    // fixed-byte certificate only when the upper bound is known and unsaturated.
    private static ByteWidth MeasureByteWidth(Utf8TreeNode node, Utf8ConstructionMap<Utf8TreeNode, ByteWidth> values)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.One:
                return Scalar(node.Ch, node.MatchCaseInsensitive, node.Options);
            case Utf8TreeNodeKind.Multi:
                if (node.Str is not { } text) return new(0, null);
                long low = 0, high = 0;
                for (var offset = 0; offset < text.Length;)
                {
                    budget.ChargeWork(1);
                    if (Rune.DecodeFromUtf16(text.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done)
                        return new(0, null);
                    var atom = Scalar(scalar.Value, node.MatchCaseInsensitive, node.Options);
                    low += atom.Minimum; high += atom.Maximum.GetValueOrDefault(); offset += consumed;
                }
                return Bounds(low, high);
            case Utf8TreeNodeKind.Notone:
                return new(1, 4);
            case Utf8TreeNodeKind.Set:
                return Class(node);
            case Utf8TreeNodeKind.Oneloop:
            case Utf8TreeNodeKind.Onelazy:
            case Utf8TreeNodeKind.Oneloopatomic:
                return Repeat(Scalar(node.Ch, node.MatchCaseInsensitive, node.Options), node.M, node.N);
            case Utf8TreeNodeKind.Notoneloop:
            case Utf8TreeNodeKind.Notonelazy:
            case Utf8TreeNodeKind.Notoneloopatomic:
                return Repeat(new(1, 4), node.M, node.N);
            case Utf8TreeNodeKind.Setloop:
            case Utf8TreeNodeKind.Setlazy:
            case Utf8TreeNodeKind.Setloopatomic:
                return Repeat(Class(node), node.M, node.N);
            case Utf8TreeNodeKind.Loop:
            case Utf8TreeNodeKind.Lazyloop:
                return node.ChildCount == 1 ? Repeat(values[node.Child(0)], node.M, node.N) : new(0, null);
            case Utf8TreeNodeKind.Capture:
            case Utf8TreeNodeKind.Group:
            case Utf8TreeNodeKind.Atomic:
            case Utf8TreeNodeKind.Concatenate:
                long minimum = 0, maximum = 0; var unbounded = false;
                for (var i = 0; i < node.ChildCount; i++)
                {
                    budget.ChargeWork(1); var child = values[node.Child(i)];
                    minimum += child.Minimum;
                    if (child.Maximum is int bound) maximum += bound; else unbounded = true;
                }
                return new(CapMinimum(minimum), unbounded ? null : CapMaximum(maximum));
            case Utf8TreeNodeKind.Alternate:
            case Utf8TreeNodeKind.BackreferenceConditional:
            case Utf8TreeNodeKind.ExpressionConditional:
                var first = node.Kind == Utf8TreeNodeKind.ExpressionConditional ? 1 : 0;
                var conditional = node.Kind != Utf8TreeNodeKind.Alternate;
                var branchMin = int.MaxValue; int? branchMax = 0;
                for (var i = first; i < node.ChildCount; i++)
                {
                    budget.ChargeWork(1); var child = values[node.Child(i)];
                    branchMin = Math.Min(branchMin, child.Minimum);
                    branchMax = branchMax is int current && child.Maximum is int bound ? Math.Max(current, bound) : null;
                }
                if (node.ChildCount == first || conditional && node.ChildCount - first < 2) branchMin = 0;
                return new(branchMin, branchMax);
            case Utf8TreeNodeKind.Backreference:
            case Utf8TreeNodeKind.Unknown:
                return new(0, null);
            default:
                return new(0, 0);
        }

        static int CapMinimum(long value) => value > int.MaxValue ? int.MaxValue : (int)value;
        static int? CapMaximum(long value) => value > int.MaxValue ? null : (int)value;
        static ByteWidth Bounds(long low, long high) => new(CapMinimum(low), CapMaximum(high));
        static ByteWidth Repeat(ByteWidth body, int minimum, int maximum)
        {
            if (minimum < 0 || maximum < minimum) return new(0, null);
            return new(CapMinimum((long)body.Minimum * minimum), maximum == 0 || body.Maximum == 0 ? 0 :
                maximum == int.MaxValue || body.Maximum is null ? null : CapMaximum((long)body.Maximum.Value * maximum));
        }
        static ByteWidth Scalar(int scalar, bool folded, RegexOptions options)
        {
            var owner = Utf8ConstructionScope.CurrentBudget; owner.ChargeWork(1);
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
                owner.ChargeWork(1);
                foreach (var equivalent in Internal.Execution.Utf8SliceCaseEquivalences.GetEquivalents((char)scalar,
                    (Internal.Execution.Utf8ScalarCaseBehavior)behavior))
                {
                    owner.ChargeWork(1); var bytes = new Rune(equivalent).Utf8SequenceLength;
                    low = Math.Min(low, bytes); high = Math.Max(high, bytes);
                }
            }
            return new(low, high);
        }
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
            if (node.Str is not { Length: >= 3 } wire) return new(0, null);
            var ranges = (int)wire[1]; var categories = (int)wire[2];
            if (wire.Length != 3 + ranges + categories || wire[0] != 0) return new(1, 4);
            var minimum = 4; var maximum = 0;
            for (var index = 0; index < ranges; index += 2)
            {
                owner.ChargeWork(1);
                var low = (int)wire[3 + index];
                var high = index + 1 == ranges || wire[4 + index] == 0 ? 65536 : wire[4 + index];
                var range = Range(low, high);
                if (range.Maximum is null) return new(0, null);
                if (node.MatchCaseInsensitive)
                {
                    if (high - low > 128) return new(1, 4);
                    for (var value = low; value < high; value++)
                    {
                        var scalar = Scalar(value, true, node.Options);
                        minimum = Math.Min(minimum, scalar.Minimum); maximum = Math.Max(maximum, scalar.Maximum.GetValueOrDefault());
                    }
                }
                else { minimum = Math.Min(minimum, range.Minimum); maximum = Math.Max(maximum, range.Maximum.Value); }
            }
            var position = 3 + ranges;
            while (position < wire.Length)
            {
                owner.ChargeWork(1);
                if (wire[position++] != Utf8OwnedCharClass.ScalarRangePredicateCode || position == wire.Length) return new(1, 4);
                var endpoints = wire[position++];
                if ((endpoints & 1) != 0 || endpoints > (wire.Length - position) / 2) return new(0, null);
                if (node.MatchCaseInsensitive) return new(1, 4);
                for (var index = 0; index < endpoints; index += 2)
                {
                    owner.ChargeWork(1);
                    var low = wire[position] | wire[position + 1] << 16;
                    var high = wire[position + 2] | wire[position + 3] << 16; position += 4;
                    var range = Range(low, high);
                    if (range.Maximum is null) return new(0, null);
                    minimum = Math.Min(minimum, range.Minimum); maximum = Math.Max(maximum, range.Maximum.Value);
                }
            }
            return maximum == 0 ? new(0, null) : new(minimum, maximum);
        }
    }

    public static Utf8TreeNodeKind FindTrailingAnchor(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<Utf8TreeNode>.Create(budget, node);
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic or Utf8TreeNodeKind.Concatenate)
        {
            budget.ChargeWork(1);
            if (node.ChildCount == 0) return Utf8TreeNodeKind.Unknown;
            node = node.Child(node.ChildCount - 1);
            frames.Push(node);
        }
        budget.ChargeWork(1);
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
    public static string? TryExtractLeadingLiteral(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<Utf8TreeNode>.Create(budget, node);
        while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group)
        {
            budget.ChargeWork(1);
            if (node.ChildCount != 1) return null;
            node = node.Child(0);
            frames.Push(node);
        }
        budget.ChargeWork(1);
        var result = node.Kind switch
        {
            Utf8TreeNodeKind.One => budget.CopyText([node.Ch]),
            Utf8TreeNodeKind.Multi => string.IsNullOrEmpty(node.Str) ? null : node.Str,
            Utf8TreeNodeKind.Concatenate => LeadingRun(node),
            _ => null,
        };
        if (result is not null) construction.Complete();
        return result;

        static string? LeadingRun(Utf8TreeNode parent)
        {
            var budget = Utf8ConstructionScope.CurrentBudget;
            using var run = Utf8ConstructionText.Create();
            for (var i = 0; i < parent.ChildCount; i++)
            {
                budget.ChargeWork(1);
                var child = parent.Child(i);
                if (child.Kind == Utf8TreeNodeKind.One)
                {
                    run.Append(child.Ch);
                }
                else if (child.Kind == Utf8TreeNodeKind.Multi && !string.IsNullOrEmpty(child.Str))
                {
                    run.Append(child.Str);
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
        return MeasurePostOrder<string?>(node, Measure, true);

        static string? Measure(Utf8TreeNode node, Utf8ConstructionMap<Utf8TreeNode, string?> values)
        {
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.One:
                    return Utf8ConstructionScope.CurrentBudget.CopyText([node.Ch]);
                case Utf8TreeNodeKind.Multi:
                    return string.IsNullOrEmpty(node.Str) ? null : node.Str;
                case Utf8TreeNodeKind.Capture:
                case Utf8TreeNodeKind.Group:
                    return node.ChildCount == 1 ? values[node.Child(0)] : null;
                case Utf8TreeNodeKind.Concatenate:
                    return LongestRun(node);
                case Utf8TreeNodeKind.Alternate:
                    return LongestBranch(node);
                default:
                    return null;
            }

            static string? LongestRun(Utf8TreeNode parent)
            {
                string? best = null;
                var budget = Utf8ConstructionScope.CurrentBudget;
                using var run = Utf8ConstructionText.Create();
                for (var i = 0; i < parent.ChildCount; i++)
                {
                    budget.ChargeWork(1);
                    var child = parent.Child(i);
                    if (child.Kind == Utf8TreeNodeKind.One)
                    {
                        run.Append(child.Ch);
                    }
                    else if (child.Kind == Utf8TreeNodeKind.Multi && !string.IsNullOrEmpty(child.Str))
                    {
                        run.Append(child.Str);
                    }
                    else if (child.Kind == Utf8TreeNodeKind.Empty)
                    {
                        continue;
                    }
                    else
                    {
                        best = Longer(best, run.ToText());
                        run.Clear();
                    }
                }

                return Longer(best, run.ToText());
            }

            string? LongestBranch(Utf8TreeNode parent)
            {
                string? best = null;
                for (var i = 0; i < parent.ChildCount; i++)
                {
                    Utf8ConstructionScope.CurrentBudget.ChargeWork(1);
                    best = Longer(best, values[parent.Child(i)]);
                }

                return best;
            }

            static string? Longer(string? left, string? right)
            {
                if (string.IsNullOrEmpty(left))
                {
                    return string.IsNullOrEmpty(right) ? null : right;
                }

                if (string.IsNullOrEmpty(right) || right.Length <= left.Length)
                {
                    return left;
                }

                return right;
            }

        }
    }

    private sealed class PrefixWalker
    {
        public List<string> Families = [""];
        public bool Failed;
        public bool Stopped;
    }

    // Owned alternate prefixes, pinned against legacy instances over the probe
    // catalog. The single path walk covers literal runs, class products, exact
    // loops and transparent groups, gated on RightToLeft and validated to
    // length two or more. The alternation walk covers unions and concat
    // products with branch level rules: empty match branches and nested
    // alternation branches reject the family, stopped branches discard their
    // partial work and halt later branches, anchored families require every
    // member to reach length two. Concat products reject past sixteen members
    // while unions stay unbounded, and single path products stop adding
    // factors past sixteen instead. Loop factors stop the walk once eight
    // characters accumulate. Case folded sets contribute their invariant
    // lowercase head under top level IgnoreCase, read from the explicit options
    // because the tree root strips that flag, and expand wire order otherwise.
    // Every rule above was read off probe behavior.
    public static string[]? FindAlternatePrefixes(Utf8TreeNode node, RegexOptions options)
    {
        var topIgnoreCase = (options & RegexOptions.IgnoreCase) != 0;
        var unwrapped = node;
        while ((unwrapped.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) && unwrapped.ChildCount == 1)
        {
            unwrapped = unwrapped.Child(0);
        }

        if (unwrapped.Kind == Utf8TreeNodeKind.Alternate)
        {
            var union = ExpandFallbackAlternation(unwrapped, topIgnoreCase);
            if (union is null)
            {
                return null;
            }

            return union.ToArray();
        }

        if (HasRegexOption(node, RegexOptions.RightToLeft))
        {
            return null;
        }

        var walker = new PrefixWalker();
        WalkPrimary(node, walker, topIgnoreCase);
        if (walker.Failed)
        {
            var general = ExpandFallbackRoot(node, topIgnoreCase);
            if (general is null)
            {
                return null;
            }

            return general.ToArray();
        }

        if (walker.Families.Count == 0)
        {
            return null;
        }

        foreach (var family in walker.Families)
        {
            if (family.Length < 2)
            {
                return null;
            }
        }

        return walker.Families.ToArray();

        static bool HasRegexOption(Utf8TreeNode target, RegexOptions option)
        {
            return (target.Options & option) != 0;
        }

        static bool UnwrapsToAlternate(Utf8TreeNode target)
        {
            var current = target;
            while ((current.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) && current.ChildCount == 1)
            {
                current = current.Child(0);
            }

            return current.Kind == Utf8TreeNodeKind.Alternate;
        }

        static void WalkPrimary(Utf8TreeNode node, PrefixWalker walker, bool topIgnoreCase)
        {
            var pending = new Stack<Utf8TreeNode>();
            pending.Push(node);
            while (!walker.Failed && !walker.Stopped && pending.TryPop(out node))
            {
                switch (node.Kind)
                {
                    case Utf8TreeNodeKind.Capture:
                    case Utf8TreeNodeKind.Group:
                    case Utf8TreeNodeKind.Atomic:
                    case Utf8TreeNodeKind.Concatenate:
                        for (var index = node.ChildCount - 1; index >= 0; index--)
                            pending.Push(node.Child(index));
                        break;
                    case Utf8TreeNodeKind.Alternate:
                        walker.Failed = true;
                        break;
                    case Utf8TreeNodeKind.One:
                        AppendPrefixText(walker, node.Ch.ToString());
                        break;
                    case Utf8TreeNodeKind.Multi:
                        if (string.IsNullOrEmpty(node.Str))
                        {
                            walker.Stopped = true;
                            break;
                        }

                        AppendPrefixText(walker, node.Str);
                        break;
                    case Utf8TreeNodeKind.Set:
                        ApplyPrimarySet(walker, node, topIgnoreCase);
                        break;
                    case Utf8TreeNodeKind.Oneloop:
                    case Utf8TreeNodeKind.Onelazy:
                    case Utf8TreeNodeKind.Oneloopatomic:
                        ApplyPrimarySingleLoop(walker, node.Ch, node.M, node.N);
                        break;
                    case Utf8TreeNodeKind.Setloop:
                    case Utf8TreeNodeKind.Setlazy:
                    case Utf8TreeNodeKind.Setloopatomic:
                        ApplyPrimarySetLoop(walker, node, topIgnoreCase);
                        break;
                    case Utf8TreeNodeKind.Loop:
                    case Utf8TreeNodeKind.Lazyloop:
                    case Utf8TreeNodeKind.Notone:
                    case Utf8TreeNodeKind.Notoneloop:
                    case Utf8TreeNodeKind.Notonelazy:
                    case Utf8TreeNodeKind.Notoneloopatomic:
                    case Utf8TreeNodeKind.Backreference:
                    case Utf8TreeNodeKind.BackreferenceConditional:
                    case Utf8TreeNodeKind.ExpressionConditional:
                        walker.Stopped = true;
                        break;
                    default:
                        break;
                }
            }
        }

        static void AppendPrefixText(PrefixWalker walker, string text)
        {
            for (var i = 0; i < walker.Families.Count; i++)
            {
                walker.Families[i] = walker.Families[i] + text;
            }
        }

        static int MaxPrefixLength(List<string> families)
        {
            var longest = 0;
            foreach (var family in families)
            {
                if (family.Length > longest)
                {
                    longest = family.Length;
                }
            }

            return longest;
        }

        static List<string> ProductPrefix(List<string> families, List<string> parts)
        {
            var result = new List<string>(families.Count * parts.Count);
            foreach (var part in parts)
            {
                foreach (var family in families)
                {
                    result.Add(family + part);
                }
            }

            return result;
        }

        static bool SetIsOpaque(string? set)
        {
            if (string.IsNullOrEmpty(set) || set.Length < 4)
            {
                return true;
            }

            if (Utf8OwnedCharClass.IsNegated(set) || Utf8OwnedCharClass.IsSubtraction(set))
            {
                return true;
            }

            var setLength = set[Utf8OwnedCharClass.SetLengthIndex];
            var categoryLength = set[Utf8OwnedCharClass.CategoryLengthIndex];
            if (setLength < 2 || categoryLength != 0 || setLength % 2 != 0 || Utf8OwnedCharClass.SetStartIndex + setLength > set.Length)
            {
                return true;
            }

            return false;
        }

        static List<string>? ExpandSetChars(string? set)
        {
            if (set is null || SetIsOpaque(set))
            {
                return null;
            }

            var setLength = set[Utf8OwnedCharClass.SetLengthIndex];
            var chars = new List<string>();
            var total = 0;
            for (var i = 0; i < setLength; i += 2)
            {
                var low = set[Utf8OwnedCharClass.SetStartIndex + i];
                var high = set[Utf8OwnedCharClass.SetStartIndex + i + 1];
                if (high <= low)
                {
                    continue;
                }

                if (high - low + total > 256)
                {
                    return null;
                }

                for (var c = low; c < high; c++)
                {
                    chars.Add(c.ToString());
                }

                total += high - low;
            }

            return chars;
        }

        static bool TryOriginalSetChar(string? set, out char original)
        {
            original = (char)0;
            if (set is null || SetIsOpaque(set))
            {
                return false;
            }

            var setLength = set[Utf8OwnedCharClass.SetLengthIndex];
            var minimum = (char)65535;
            var found = false;
            for (var i = 0; i < setLength; i += 2)
            {
                var low = set[Utf8OwnedCharClass.SetStartIndex + i];
                var high = set[Utf8OwnedCharClass.SetStartIndex + i + 1];
                if (high <= low)
                {
                    continue;
                }

                if (!found || low < minimum)
                {
                    minimum = low;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            var head = char.ToLowerInvariant(minimum);
            if (head > 127)
            {
                return false;
            }

            original = head;
            return true;
        }
        static void ApplyPrimarySet(PrefixWalker walker, Utf8TreeNode node, bool topIgnoreCase)
        {
            if (SetIsOpaque(node.Str))
            {
                walker.Stopped = true;
                return;
            }

            if (topIgnoreCase)
            {
                if (TryOriginalSetChar(node.Str, out var original))
                {
                    AppendPrefixText(walker, original.ToString());
                    return;
                }

                walker.Stopped = true;
                return;
            }

            var chars = ExpandSetChars(node.Str);
            if (chars is null || chars.Count == 0)
            {
                walker.Stopped = true;
                return;
            }

            if (walker.Families.Count * chars.Count > 16)
            {
                walker.Stopped = true;
                return;
            }

            walker.Families = ProductPrefix(walker.Families, chars);
        }

        static void ApplyPrimarySingleLoop(PrefixWalker walker, char ch, int m, int n)
        {
            var room = 8 - MaxPrefixLength(walker.Families);
            if (room < 0)
            {
                room = 0;
            }

            var take = m < room ? m : room;
            AppendPrefixText(walker, new string(ch, take));
            if (m != n || MaxPrefixLength(walker.Families) >= 8)
            {
                walker.Stopped = true;
            }
        }

        static void ApplyPrimarySetLoop(PrefixWalker walker, Utf8TreeNode node, bool topIgnoreCase)
        {
            if (SetIsOpaque(node.Str))
            {
                walker.Stopped = true;
                return;
            }

            if (topIgnoreCase)
            {
                if (TryOriginalSetChar(node.Str, out var original))
                {
                    ApplyPrimarySingleLoop(walker, original, node.M, node.N);
                    return;
                }

                walker.Stopped = true;
                return;
            }

            var chars = ExpandSetChars(node.Str);
            if (chars is null || chars.Count == 0)
            {
                walker.Stopped = true;
                return;
            }

            for (var k = 0; k < node.M; k++)
            {
                if (walker.Families.Count * chars.Count > 16 || MaxPrefixLength(walker.Families) >= 8)
                {
                    walker.Stopped = true;
                    return;
                }

                walker.Families = ProductPrefix(walker.Families, chars);
            }

            if (node.M != node.N)
            {
                walker.Stopped = true;
            }
        }

        static List<string>? ExpandFallbackRoot(Utf8TreeNode node, bool topIgnoreCase)
        {
            var result = ExpandFallback(node, topIgnoreCase);
            if (result.Families is null || result.Families.Count == 0)
            {
                return null;
            }

            foreach (var family in result.Families)
            {
                if (family.Length < 2)
                {
                    return null;
                }
            }

            return result.Families;
        }

        static List<string>? ExpandFallbackAlternation(Utf8TreeNode node, bool topIgnoreCase)
        {
            var result = ExpandFallback(node, topIgnoreCase);
            if (result.Families is null)
            {
                return null;
            }

            return result.Families;
        }

        static (List<string>? Families, bool Done, bool Anchored) ExpandFallback(Utf8TreeNode node, bool topIgnoreCase)
        {
            var frames = new Stack<(Utf8TreeNode Node, int Child, List<string> Families, bool Anchored)>();
            (List<string>? Families, bool Done, bool Anchored) result;
            while (true)
            {
                while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic && node.ChildCount == 1)
                    node = node.Child(0);
                if (node.Kind is Utf8TreeNodeKind.Alternate or Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic)
                {
                    var union = node.Kind == Utf8TreeNodeKind.Alternate;
                    var families = union ? new List<string>() : new List<string> { "" };
                    if (node.ChildCount == 0)
                        result = (families, true, false);
                    else if (union && UnwrapsToAlternate(node.Child(0)))
                        result = (null, true, false);
                    else
                    {
                        frames.Push((node, 0, families, false));
                        node = node.Child(0);
                        continue;
                    }
                }
                else
                    result = ExpandFallbackSingle(node, topIgnoreCase);

                while (frames.TryPop(out var frame))
                {
                    if (result.Families is null)
                    {
                        result = (null, true, false);
                        continue;
                    }
                    var union = frame.Node.Kind == Utf8TreeNodeKind.Alternate;
                    if (!result.Done)
                    {
                        result = union ? FinishUnion(frame.Families, frame.Anchored)
                            : (frame.Families, false, frame.Anchored);
                        continue;
                    }
                    List<string> families;
                    if (union)
                    {
                        if (result.Families.Any(static family => family.Length == 0))
                        {
                            result = (null, true, false);
                            continue;
                        }
                        families = frame.Families;
                        families.AddRange(result.Families);
                    }
                    else
                    {
                        if (frame.Families.Count * result.Families.Count > 16)
                        {
                            result = (null, true, false);
                            continue;
                        }
                        families = ProductPrefix(frame.Families, result.Families);
                    }
                    var anchored = frame.Anchored || result.Anchored;
                    var next = frame.Child + 1;
                    if (next == frame.Node.ChildCount)
                    {
                        result = union ? FinishUnion(families, anchored) : (families, true, anchored);
                        continue;
                    }
                    if (union && UnwrapsToAlternate(frame.Node.Child(next)))
                    {
                        result = (null, true, false);
                        continue;
                    }
                    frames.Push((frame.Node, next, families, anchored));
                    node = frame.Node.Child(next);
                    break;
                }
                if (frames.Count == 0) return result;
            }

            static (List<string>? Families, bool Done, bool Anchored) FinishUnion(List<string> families, bool anchored)
            {
                if (anchored && families.Any(static family => family.Length < 2)) return (null, true, false);
                return (families, true, anchored);
            }
        }

        static (List<string>? Families, bool Done, bool Anchored) ExpandFallbackSingle(Utf8TreeNode node, bool topIgnoreCase)
        {
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.One:
                    return (new List<string> { node.Ch.ToString() }, true, false);
                case Utf8TreeNodeKind.Multi:
                    if (string.IsNullOrEmpty(node.Str))
                    {
                        return (new List<string> { "" }, true, false);
                    }

                    return (new List<string> { node.Str }, true, false);
                case Utf8TreeNodeKind.Set:
                    if (SetIsOpaque(node.Str))
                    {
                        return (new List<string> { "" }, false, false);
                    }

                    if (topIgnoreCase)
                    {
                        if (TryOriginalSetChar(node.Str, out var original))
                        {
                            return (new List<string> { original.ToString() }, true, false);
                        }

                        return (new List<string> { "" }, false, false);
                    }

                    var chars = ExpandSetChars(node.Str);
                    if (chars is null || chars.Count == 0)
                    {
                        return (new List<string> { "" }, false, false);
                    }

                    if (chars.Count > 16)
                    {
                        return (null, true, false);
                    }

                    return (chars, true, false);
                case Utf8TreeNodeKind.Oneloop:
                case Utf8TreeNodeKind.Onelazy:
                case Utf8TreeNodeKind.Oneloopatomic:
                    return ExpandFallbackSingleLoop(node.Ch, node.M, node.N);
                case Utf8TreeNodeKind.Setloop:
                case Utf8TreeNodeKind.Setlazy:
                case Utf8TreeNodeKind.Setloopatomic:
                    return ExpandFallbackSetLoop(node, topIgnoreCase);
                case Utf8TreeNodeKind.Beginning:
                case Utf8TreeNodeKind.Bol:
                case Utf8TreeNodeKind.Start:
                case Utf8TreeNodeKind.End:
                case Utf8TreeNodeKind.EndZ:
                case Utf8TreeNodeKind.Eol:
                    return (new List<string> { "" }, true, true);
                case Utf8TreeNodeKind.Loop:
                case Utf8TreeNodeKind.Lazyloop:
                case Utf8TreeNodeKind.Notone:
                case Utf8TreeNodeKind.Notoneloop:
                case Utf8TreeNodeKind.Notonelazy:
                case Utf8TreeNodeKind.Notoneloopatomic:
                case Utf8TreeNodeKind.Backreference:
                case Utf8TreeNodeKind.BackreferenceConditional:
                case Utf8TreeNodeKind.ExpressionConditional:
                    return (new List<string> { "" }, false, false);
                default:
                    return (new List<string> { "" }, true, false);
            }
        }

        static (List<string>? Families, bool Done, bool Anchored) ExpandFallbackSingleLoop(char ch, int m, int n)
        {
            var take = m < 8 ? m : 8;
            var families = new List<string> { new string(ch, take) };
            if (m != n || take >= 8)
            {
                return (families, false, false);
            }

            return (families, true, false);
        }

        static (List<string>? Families, bool Done, bool Anchored) ExpandFallbackSetLoop(Utf8TreeNode node, bool topIgnoreCase)
        {
            if (SetIsOpaque(node.Str))
            {
                return (new List<string> { "" }, false, false);
            }

            if (topIgnoreCase)
            {
                if (TryOriginalSetChar(node.Str, out var original))
                {
                    return ExpandFallbackSingleLoop(original, node.M, node.N);
                }

                return (new List<string> { "" }, false, false);
            }

            var chars = ExpandSetChars(node.Str);
            if (chars is null || chars.Count == 0)
            {
                return (new List<string> { "" }, false, false);
            }

            var families = new List<string> { "" };
            for (var k = 0; k < node.M; k++)
            {
                if (families.Count * chars.Count > 16 || MaxPrefixLength(families) >= 8)
                {
                    return (families, false, false);
                }

                families = ProductPrefix(families, chars);
            }

            if (node.M != node.N)
            {
                return (families, false, false);
            }

            return (families, true, false);
        }
    }


    // A candidate is mandatory at this byte offset only while its entire
    // preceding prefix has one proven byte width. An opaque variable node ends
    // the proof; a longer literal after it must never displace a safe prefix.
    public static (string Literal, int Distance)? TryExtractFixedDistanceLiteral(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<FactFrame>.Create(budget, new(node));
        using var widths = Utf8ConstructionMap<Utf8TreeNode, ByteWidth>.Create(budget, ReferenceEqualityComparer.Instance);
        PopulateFacts(frames, widths, MeasureByteWidth);
        string? best = null; var bestDistance = 0; var distance = 0;
        frames.Push(new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1; var frame = frames[index]; var current = frame.Node;
            if (current.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic or Utf8TreeNodeKind.Concatenate)
            {
                if (frame.NextChild < current.ChildCount)
                {
                    var child = current.Child(frame.NextChild++); frames[index] = frame; frames.Push(new(child));
                }
                else frames.Pop();
                continue;
            }
            var width = widths[current];
            if (!width.IsFixed || width.Minimum > int.MaxValue - distance) break;
            if (current.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Multi && IsExactLiteral(current))
            {
                var text = current.Kind == Utf8TreeNodeKind.One ? budget.CopyText([current.Ch]) : current.Str;
                if (!string.IsNullOrEmpty(text) && (best is null || text.Length >= best.Length))
                    { best = text; bestDistance = distance; }
            }
            distance += width.Minimum; frames.Pop();
        }
        if (best is null) return null;
        construction.Complete();
        return (best, bestDistance);

        static bool IsExactLiteral(Utf8TreeNode node)
        {
            if (!node.MatchCaseInsensitive) return true;
            if (node.Kind == Utf8TreeNodeKind.One) return Identity(node.Ch, node.Options);
            if (node.Str is not { } text) return false;
            for (var offset = 0; offset < text.Length;)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (Rune.DecodeFromUtf16(text.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done ||
                    !scalar.IsBmp || !Identity((char)scalar.Value, node.Options)) return false;
                offset += consumed;
            }
            return true;

            static bool Identity(char value, RegexOptions options)
            {
                var lastBehavior = (options & RegexOptions.CultureInvariant) != 0 ? 0 : 2;
                for (var behavior = 0; behavior <= lastBehavior; behavior++)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    foreach (var equivalent in Internal.Execution.Utf8SliceCaseEquivalences.GetEquivalents(value,
                        (Internal.Execution.Utf8ScalarCaseBehavior)behavior))
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        if (equivalent != value) return false;
                    }
                }
                return true;
            }
        }
    }

    public readonly record struct OwnedCharRange(char LowInclusive, char HighInclusive);

    public readonly record struct OwnedSet(string Set, int Distance, bool Negated, char[]? Chars, OwnedCharRange? Range);

    // Owned fixed-distance sets, pinned against legacy instances over the probe
    // catalog. The walk admits fixed-byte literals, clean ASCII sets, transparent
    // groups and line anchors only, accumulating proven byte distances. Every
    // other shape poisons the whole result: alternations, all loop kinds,
    // lookarounds, backreferences, boundaries, opaque widths, categorized or
    // negated or subtracted or non ascii classes, and conditionals. Single wire
    // ranges project to the range form while wider sets project to distinct
    // ascending characters with the wire payload reused verbatim. No options
    // take part: folded payloads already carry their bytes. Every rule above
    // was retained only where it supplies a sound scalar-byte search fact.
    public static OwnedSet[]? FindFixedDistanceSets(Utf8TreeNode node)
    {
        var sets = new List<OwnedSet>();
        var result = WalkSets(node, 0, sets);
        if (!result.Ok || sets.Count == 0)
        {
            return null;
        }

        return sets.ToArray();

        static (int Distance, bool Ok) WalkSets(Utf8TreeNode current, int distance, List<OwnedSet> sets)
        {
            var pending = new Stack<Utf8TreeNode>();
            pending.Push(current);
            while (pending.TryPop(out current))
            {
                switch (current.Kind)
                {
                    case Utf8TreeNodeKind.One:
                    case Utf8TreeNodeKind.Multi:
                        var width = ComputeByteWidth(current);
                        if (!width.IsFixed || width.Minimum > int.MaxValue - distance) return (distance, false);
                        distance += width.Minimum;
                        break;
                    case Utf8TreeNodeKind.Set:
                        var setWidth = ComputeByteWidth(current);
                        if (!setWidth.IsFixed || setWidth.Minimum != 1 || distance == int.MaxValue ||
                            !TryAddSet(current.Str, distance, sets)) return (distance, false);
                        distance++;
                        break;
                    case Utf8TreeNodeKind.Capture:
                    case Utf8TreeNodeKind.Group:
                    case Utf8TreeNodeKind.Atomic:
                    case Utf8TreeNodeKind.Concatenate:
                        for (var index = current.ChildCount - 1; index >= 0; index--)
                            pending.Push(current.Child(index));
                        break;
                    case Utf8TreeNodeKind.Beginning:
                    case Utf8TreeNodeKind.Bol:
                    case Utf8TreeNodeKind.Start:
                    case Utf8TreeNodeKind.End:
                    case Utf8TreeNodeKind.EndZ:
                    case Utf8TreeNodeKind.Eol:
                    case Utf8TreeNodeKind.Empty:
                        break;
                    default:
                        return (distance, false);
                }
            }
            return (distance, true);
        }

        static bool TryAddSet(string? wire, int distance, List<OwnedSet> sets)
        {
            if (string.IsNullOrEmpty(wire) || wire.Length < 4)
            {
                return false;
            }

            if (Utf8OwnedCharClass.IsNegated(wire) || Utf8OwnedCharClass.IsSubtraction(wire))
            {
                return false;
            }

            var setLength = wire[Utf8OwnedCharClass.SetLengthIndex];
            var categoryLength = wire[Utf8OwnedCharClass.CategoryLengthIndex];
            if (setLength < 2 || categoryLength != 0 || setLength % 2 != 0 || Utf8OwnedCharClass.SetStartIndex + setLength > wire.Length)
            {
                return false;
            }

            if (setLength == 2)
            {
                var low = wire[Utf8OwnedCharClass.SetStartIndex];
                var high = wire[Utf8OwnedCharClass.SetStartIndex + 1];
                if (high <= low || high > 128)
                {
                    return false;
                }

                sets.Add(new OwnedSet(wire, distance, false, null, new OwnedCharRange(low, (char)(high - 1))));
                return true;
            }

            var chars = new SortedSet<char>();
            for (var i = 0; i < setLength; i += 2)
            {
                var low = wire[Utf8OwnedCharClass.SetStartIndex + i];
                var high = wire[Utf8OwnedCharClass.SetStartIndex + i + 1];
                if (high <= low)
                {
                    continue;
                }

                if (high > 128)
                {
                    return false;
                }

                for (var c = low; c < high; c++)
                {
                    chars.Add(c);
                }
            }

            if (chars.Count == 0)
            {
                return false;
            }

            var array = new char[chars.Count];
            chars.CopyTo(array);
            sets.Add(new OwnedSet(wire, distance, false, array, null));
            return true;
        }
    }

    private struct FactFrame(Utf8TreeNode node)
    {
        internal readonly Utf8TreeNode Node = node;
        internal int NextChild;
    }

    // Static measure delegates borrow their inputs. Root frame admission
    // precedes map acquisition; frames, map arrays and depth unwind together.
    // Only materialized text results transfer their acquired loans/descriptors.
    private static T MeasurePostOrder<T>(Utf8TreeNode node,
        Func<Utf8TreeNode, Utf8ConstructionMap<Utf8TreeNode, T>, T> measure, bool transferResult)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<FactFrame>.Create(budget, new(node));
        using var values = Utf8ConstructionMap<Utf8TreeNode, T>.Create(budget, ReferenceEqualityComparer.Instance);
        PopulateFacts(frames, values, measure);
        var result = values[node];
        if (transferResult && result is not null) construction.Complete();
        return result;
    }

    private static void PopulateFacts<T>(Utf8ConstructionFrames<FactFrame> frames,
        Utf8ConstructionMap<Utf8TreeNode, T> values,
        Func<Utf8TreeNode, Utf8ConstructionMap<Utf8TreeNode, T>, T> measure)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (frame.NextChild < frame.Node.ChildCount)
            {
                var child = frame.Node.Child(frame.NextChild++);
                frames[index] = frame;
                frames.Push(new(child));
            }
            else
            {
                values[frame.Node] = measure(frame.Node, values);
                frames.Pop();
            }
        }
    }
}
