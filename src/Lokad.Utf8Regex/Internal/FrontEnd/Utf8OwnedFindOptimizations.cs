namespace Lokad.Utf8Regex.Internal.FrontEnd;

using System.Text.RegularExpressions;
// Owned find-optimization facts: lengths, anchors, alternate prefixes and
// fixed-distance literals. Pure functions over runtime trees of either
// provenance, differentially pinned against legacy instances. Fixed-distance
// sets arrive in the next slice; consumers migrate hub by hub. Length
// arithmetic saturates exactly like the pinned formulas: minimums cap at
// int.MaxValue - 1 past int.MaxValue, maximums turn null, lone bounds stay put.
internal static class Utf8OwnedFindOptimizations
{
    public static int ComputeMinLength(Utf8TreeNode node)
    {
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.One:
            case Utf8TreeNodeKind.Notone:
            case Utf8TreeNodeKind.Set:
                return 1;
            case Utf8TreeNodeKind.Multi:
                return node.Str?.Length ?? 1;
            case Utf8TreeNodeKind.Oneloop:
            case Utf8TreeNodeKind.Notoneloop:
            case Utf8TreeNodeKind.Setloop:
            case Utf8TreeNodeKind.Onelazy:
            case Utf8TreeNodeKind.Notonelazy:
            case Utf8TreeNodeKind.Setlazy:
            case Utf8TreeNodeKind.Oneloopatomic:
            case Utf8TreeNodeKind.Notoneloopatomic:
            case Utf8TreeNodeKind.Setloopatomic:
                return node.M;
            case Utf8TreeNodeKind.Loop:
            case Utf8TreeNodeKind.Lazyloop:
                return CapMin((long)node.M * ChildMinLength(node));
            case Utf8TreeNodeKind.Capture:
            case Utf8TreeNodeKind.Group:
            case Utf8TreeNodeKind.Atomic:
            case Utf8TreeNodeKind.Concatenate:
                return SumMinLength(node);
            case Utf8TreeNodeKind.Alternate:
            case Utf8TreeNodeKind.BackreferenceConditional:
            case Utf8TreeNodeKind.ExpressionConditional:
                return AlternateMinLength(node);
            default:
                return 0;
        }

        static int ChildMinLength(Utf8TreeNode parent)
        {
            return parent.ChildCount == 0 ? 0 : ComputeMinLength(parent.Child(0));
        }

        static int SumMinLength(Utf8TreeNode parent)
        {
            long total = 0;
            for (var i = 0; i < parent.ChildCount; i++)
            {
                total += ComputeMinLength(parent.Child(i));
                if (total > int.MaxValue)
                {
                    return int.MaxValue - 1;
                }
            }

            return CapMin(total);
        }

        static int AlternateMinLength(Utf8TreeNode parent)
        {
            var minimum = int.MaxValue;
            for (var i = 0; i < parent.ChildCount; i++)
            {
                var branch = ComputeMinLength(parent.Child(i));
                if (branch < minimum)
                {
                    minimum = branch;
                }
            }

            return minimum == int.MaxValue ? 0 : minimum;
        }

        static int CapMin(long value)
        {
            return value > int.MaxValue ? int.MaxValue - 1 : (int)value;
        }
    }

    public static int? ComputeMaxLength(Utf8TreeNode node)
    {
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.One:
            case Utf8TreeNodeKind.Notone:
            case Utf8TreeNodeKind.Set:
                return 1;
            case Utf8TreeNodeKind.Multi:
                return node.Str?.Length ?? 1;
            case Utf8TreeNodeKind.Oneloop:
            case Utf8TreeNodeKind.Notoneloop:
            case Utf8TreeNodeKind.Setloop:
            case Utf8TreeNodeKind.Onelazy:
            case Utf8TreeNodeKind.Notonelazy:
            case Utf8TreeNodeKind.Setlazy:
            case Utf8TreeNodeKind.Oneloopatomic:
            case Utf8TreeNodeKind.Notoneloopatomic:
            case Utf8TreeNodeKind.Setloopatomic:
                return node.N == int.MaxValue ? null : node.N;
            case Utf8TreeNodeKind.Loop:
            case Utf8TreeNodeKind.Lazyloop:
                return RepeatMaxLength(node);
            case Utf8TreeNodeKind.Capture:
            case Utf8TreeNodeKind.Group:
            case Utf8TreeNodeKind.Atomic:
            case Utf8TreeNodeKind.Concatenate:
                return SumMaxLength(node);
            case Utf8TreeNodeKind.Alternate:
            case Utf8TreeNodeKind.BackreferenceConditional:
            case Utf8TreeNodeKind.ExpressionConditional:
                return AlternateMaxLength(node);
            default:
                return 0;
        }

        static int? RepeatMaxLength(Utf8TreeNode parent)
        {
            var child = parent.ChildCount == 0 ? 0 : ComputeMaxLength(parent.Child(0));
            if (parent.N == int.MaxValue || child is null)
            {
                return null;
            }

            return CapMax((long)parent.N * child.Value);
        }

        static int? SumMaxLength(Utf8TreeNode parent)
        {
            long total = 0;
            for (var i = 0; i < parent.ChildCount; i++)
            {
                var child = ComputeMaxLength(parent.Child(i));
                if (child is null)
                {
                    return null;
                }

                total += child.Value;
                if (total > int.MaxValue)
                {
                    return null;
                }
            }

            return CapMax(total);
        }

        static int? AlternateMaxLength(Utf8TreeNode parent)
        {
            int? maximum = 0;
            for (var i = 0; i < parent.ChildCount; i++)
            {
                var branch = ComputeMaxLength(parent.Child(i));
                if (branch is null)
                {
                    return null;
                }

                if (branch > maximum)
                {
                    maximum = branch;
                }
            }

            return maximum;
        }

        static int? CapMax(long value)
        {
            return value > int.MaxValue ? null : (int?)value;
        }
    }

    public static Utf8TreeNodeKind FindLeadingAnchor(Utf8TreeNode node)
    {
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.Capture:
            case Utf8TreeNodeKind.Group:
            case Utf8TreeNodeKind.Atomic:
            case Utf8TreeNodeKind.Concatenate:
                return node.ChildCount == 0 ? Utf8TreeNodeKind.Unknown : FindLeadingAnchor(node.Child(0));
            case Utf8TreeNodeKind.Beginning:
            case Utf8TreeNodeKind.Bol:
            case Utf8TreeNodeKind.Start:
            case Utf8TreeNodeKind.EndZ:
            case Utf8TreeNodeKind.End:
                return node.Kind;
            default:
                return Utf8TreeNodeKind.Unknown;
        }
    }

    public static Utf8TreeNodeKind FindTrailingAnchor(Utf8TreeNode node)
    {
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.Capture:
            case Utf8TreeNodeKind.Group:
            case Utf8TreeNodeKind.Atomic:
            case Utf8TreeNodeKind.Concatenate:
                return node.ChildCount == 0 ? Utf8TreeNodeKind.Unknown : FindTrailingAnchor(node.Child(node.ChildCount - 1));
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
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.One:
                return node.Ch.ToString();
            case Utf8TreeNodeKind.Multi:
                return string.IsNullOrEmpty(node.Str) ? null : node.Str;
            case Utf8TreeNodeKind.Capture:
            case Utf8TreeNodeKind.Group:
                return node.ChildCount == 1 ? TryExtractLeadingLiteral(node.Child(0)) : null;
            case Utf8TreeNodeKind.Concatenate:
                return LeadingRun(node);
            default:
                return null;
        }

        static string? LeadingRun(Utf8TreeNode parent)
        {
            var run = new StringBuilder();
            for (var i = 0; i < parent.ChildCount; i++)
            {
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

            return run.Length == 0 ? null : run.ToString();
        }
    }

    public static string? TryExtractLongestLiteral(Utf8TreeNode node)
    {
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.One:
                return node.Ch.ToString();
            case Utf8TreeNodeKind.Multi:
                return string.IsNullOrEmpty(node.Str) ? null : node.Str;
            case Utf8TreeNodeKind.Capture:
            case Utf8TreeNodeKind.Group:
                return node.ChildCount == 1 ? TryExtractLongestLiteral(node.Child(0)) : null;
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
            var run = new StringBuilder();
            for (var i = 0; i < parent.ChildCount; i++)
            {
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
                    best = Longer(best, run.ToString());
                    run.Clear();
                }
            }

            return Longer(best, run.ToString());
        }

        static string? LongestBranch(Utf8TreeNode parent)
        {
            string? best = null;
            for (var i = 0; i < parent.ChildCount; i++)
            {
                best = Longer(best, TryExtractLongestLiteral(parent.Child(i)));
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

    private sealed class PrefixWalker
    {
        public List<string> Families = [""];
        public bool Failed;
        public bool Stopped;
    }

    private sealed class LiteralHolder
    {
        public string? Text;
        public int Length = -1;
        public int Distance;
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
            if (walker.Failed || walker.Stopped)
            {
                return;
            }

            switch (node.Kind)
            {
                case Utf8TreeNodeKind.Capture:
                case Utf8TreeNodeKind.Group:
                case Utf8TreeNodeKind.Atomic:
                    if (node.ChildCount == 1)
                    {
                        WalkPrimary(node.Child(0), walker, topIgnoreCase);
                        return;
                    }

                    WalkPrimaryChildren(node, walker, topIgnoreCase);
                    return;
                case Utf8TreeNodeKind.Concatenate:
                    WalkPrimaryChildren(node, walker, topIgnoreCase);
                    return;
                case Utf8TreeNodeKind.Alternate:
                    walker.Failed = true;
                    return;
                case Utf8TreeNodeKind.One:
                    AppendPrefixText(walker, node.Ch.ToString());
                    return;
                case Utf8TreeNodeKind.Multi:
                    if (string.IsNullOrEmpty(node.Str))
                    {
                        walker.Stopped = true;
                        return;
                    }

                    AppendPrefixText(walker, node.Str);
                    return;
                case Utf8TreeNodeKind.Set:
                    ApplyPrimarySet(walker, node, topIgnoreCase);
                    return;
                case Utf8TreeNodeKind.Oneloop:
                case Utf8TreeNodeKind.Onelazy:
                case Utf8TreeNodeKind.Oneloopatomic:
                    ApplyPrimarySingleLoop(walker, node.Ch, node.M, node.N);
                    return;
                case Utf8TreeNodeKind.Setloop:
                case Utf8TreeNodeKind.Setlazy:
                case Utf8TreeNodeKind.Setloopatomic:
                    ApplyPrimarySetLoop(walker, node, topIgnoreCase);
                    return;
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
                    return;
                default:
                    return;
            }
        }

        static void WalkPrimaryChildren(Utf8TreeNode parent, PrefixWalker walker, bool topIgnoreCase)
        {
            for (var i = 0; i < parent.ChildCount; i++)
            {
                WalkPrimary(parent.Child(i), walker, topIgnoreCase);
                if (walker.Failed || walker.Stopped)
                {
                    return;
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
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.Alternate:
                    return ExpandFallbackUnion(node, topIgnoreCase);
                case Utf8TreeNodeKind.Concatenate:
                    return ExpandFallbackSequence(node, topIgnoreCase);
                case Utf8TreeNodeKind.Capture:
                case Utf8TreeNodeKind.Group:
                case Utf8TreeNodeKind.Atomic:
                    if (node.ChildCount == 1)
                    {
                        return ExpandFallback(node.Child(0), topIgnoreCase);
                    }

                    return ExpandFallbackSequence(node, topIgnoreCase);
                default:
                    return ExpandFallbackSingle(node, topIgnoreCase);
            }
        }

        static (List<string>? Families, bool Done, bool Anchored) ExpandFallbackSequence(Utf8TreeNode node, bool topIgnoreCase)
        {
            var families = new List<string> { "" };
            var anchored = false;
            for (var i = 0; i < node.ChildCount; i++)
            {
                var child = ExpandFallback(node.Child(i), topIgnoreCase);
                if (child.Families is null)
                {
                    return (null, true, false);
                }

                if (!child.Done)
                {
                    return (families, false, anchored);
                }

                if (families.Count * child.Families.Count > 16)
                {
                    return (null, true, false);
                }

                families = ProductPrefix(families, child.Families);
                anchored = anchored || child.Anchored;
            }

            return (families, true, anchored);
        }

        static (List<string>? Families, bool Done, bool Anchored) ExpandFallbackUnion(Utf8TreeNode node, bool topIgnoreCase)
        {
            var result = new List<string>();
            var anchored = false;
            for (var i = 0; i < node.ChildCount; i++)
            {
                var branch = node.Child(i);
                if (UnwrapsToAlternate(branch))
                {
                    return (null, true, false);
                }

                var expanded = ExpandFallback(branch, topIgnoreCase);
                if (expanded.Families is null)
                {
                    return (null, true, false);
                }

                if (!expanded.Done)
                {
                    break;
                }

                foreach (var family in expanded.Families)
                {
                    if (family.Length == 0)
                    {
                        return (null, true, false);
                    }
                }

                result.AddRange(expanded.Families);
                anchored = anchored || expanded.Anchored;
            }

            if (anchored)
            {
                foreach (var family in result)
                {
                    if (family.Length < 2)
                    {
                        return (null, true, false);
                    }
                }
            }

            return (result, true, anchored);
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


    // Owned fixed-distance literal, pinned against legacy instances over the
    // probe catalog. Literal runs record their text with the minimum distance
    // accumulated ahead of them while sets, dots, shorthands and opaque loops
    // only advance that distance. Groups and atomic wrappers stay transparent,
    // anchors, lookarounds and backreferences cost nothing, alternations skip
    // their branches at the shortest branch length, and loops advance their
    // minimum length. The longest run wins with ties going to the last run.
    // Case folded trees carry sets instead of literals and yield nothing, and
    // RightToLeft needs no special handling. Distances use unchecked integer
    // arithmetic exactly like the surrounding runtime formulas.
    public static (string Literal, int Distance)? TryExtractFixedDistanceLiteral(Utf8TreeNode node)
    {
        var holder = new LiteralHolder();
        WalkLiteral(node, 0, holder);
        var text = holder.Text;
        if (text is null)
        {
            return null;
        }

        return (text, holder.Distance);

        static void ConsiderLiteral(LiteralHolder holder, string text, int distance)
        {
            if (!string.IsNullOrEmpty(text) && text.Length >= holder.Length)
            {
                holder.Text = text;
                holder.Length = text.Length;
                holder.Distance = distance;
            }
        }

        static int WalkLiteral(Utf8TreeNode node, int distance, LiteralHolder holder)
        {
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.One:
                    ConsiderLiteral(holder, node.Ch.ToString(), distance);
                    return distance + 1;
                case Utf8TreeNodeKind.Multi:
                    if (!string.IsNullOrEmpty(node.Str))
                    {
                        ConsiderLiteral(holder, node.Str, distance);
                    }

                    return distance + (node.Str is null ? 1 : node.Str.Length);
                case Utf8TreeNodeKind.Notone:
                case Utf8TreeNodeKind.Set:
                    return distance + 1;
                case Utf8TreeNodeKind.Capture:
                case Utf8TreeNodeKind.Group:
                case Utf8TreeNodeKind.Atomic:
                case Utf8TreeNodeKind.Concatenate:
                    var running = distance;
                    for (var i = 0; i < node.ChildCount; i++)
                    {
                        running = WalkLiteral(node.Child(i), running, holder);
                    }

                    return running;
                case Utf8TreeNodeKind.Alternate:
                    return distance + ComputeMinLength(node);
                case Utf8TreeNodeKind.Oneloop:
                case Utf8TreeNodeKind.Onelazy:
                case Utf8TreeNodeKind.Oneloopatomic:
                case Utf8TreeNodeKind.Notoneloop:
                case Utf8TreeNodeKind.Notonelazy:
                case Utf8TreeNodeKind.Notoneloopatomic:
                case Utf8TreeNodeKind.Setloop:
                case Utf8TreeNodeKind.Setlazy:
                case Utf8TreeNodeKind.Setloopatomic:
                    return distance + node.M;
                case Utf8TreeNodeKind.Loop:
                case Utf8TreeNodeKind.Lazyloop:
                    var body = node.ChildCount == 0 ? 0 : ComputeMinLength(node.Child(0));
                    return unchecked(distance + node.M * body);
                default:
                    return distance + ComputeMinLength(node);
            }
        }
    }

    public readonly record struct OwnedCharRange(char LowInclusive, char HighInclusive);

    public readonly record struct OwnedSet(string Set, int Distance, bool Negated, char[]? Chars, OwnedCharRange? Range);

    // Owned fixed-distance sets, pinned against legacy instances over the probe
    // catalog. The walk admits literals, clean ascii sets, transparent groups
    // and line anchors only, accumulating minimum distances as it goes. Every
    // other shape poisons the whole result: alternations, all loop kinds,
    // lookarounds, backreferences, boundaries, opaque widths, categorized or
    // negated or subtracted or non ascii classes, and conditionals. Single wire
    // ranges project to the range form while wider sets project to distinct
    // ascending characters with the wire payload reused verbatim. No options
    // take part: folded payloads already carry their bytes. Every rule above
    // was read off probe behavior.
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
            switch (current.Kind)
            {
                case Utf8TreeNodeKind.One:
                    return (distance + 1, true);
                case Utf8TreeNodeKind.Multi:
                    return (distance + (current.Str is null ? 1 : current.Str.Length), true);
                case Utf8TreeNodeKind.Set:
                    if (!TryAddSet(current.Str, distance, sets))
                    {
                        return (distance, false);
                    }

                    return (distance + 1, true);
                case Utf8TreeNodeKind.Capture:
                case Utf8TreeNodeKind.Group:
                case Utf8TreeNodeKind.Atomic:
                case Utf8TreeNodeKind.Concatenate:
                    var running = distance;
                    for (var i = 0; i < current.ChildCount; i++)
                    {
                        var child = WalkSets(current.Child(i), running, sets);
                        if (!child.Ok)
                        {
                            return (running, false);
                        }

                        running = child.Distance;
                    }

                    return (running, true);
                case Utf8TreeNodeKind.Beginning:
                case Utf8TreeNodeKind.Bol:
                case Utf8TreeNodeKind.Start:
                case Utf8TreeNodeKind.End:
                case Utf8TreeNodeKind.EndZ:
                case Utf8TreeNodeKind.Eol:
                case Utf8TreeNodeKind.Empty:
                    return (distance, true);
                default:
                    return (distance, false);
            }
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
}
