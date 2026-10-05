namespace Lokad.Utf8Regex.Internal.FrontEnd;


// Owned required-literal families, pinned against legacy instances over the probe
// catalog. Concatenations collect one family per literal position while
// alternations union clean single run branches, sorted ordinally.
// Dirty top level branches fall back to members common across every branch,
// sorted ordinally. Nested dirty alternations contribute nothing.
// Group loops enter when their minimum is positive and single char loops
// continue only for the bare optional, star and plus shapes while every other
// counted loop clears prior families outside branches and skips inside them.
// Opaque widths, anchors, boundaries, lookarounds, backreferences and empty
// nodes contribute nothing. Scoring prefers the longest total length with
// ties going to the first family. Every rule above was read off probe behavior.
internal static class Utf8OwnedRequiredLiterals
{
    public static string[]? FindBestRequiredLiteralFamily(Utf8TreeNode node)
    {
        var top = node;
        while ((top.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) && top.ChildCount == 1)
        {
            top = top.Child(0);
        }

        if (top.Kind == Utf8TreeNodeKind.Alternate)
        {
            var clean = UnionCleanBranches(top);
            if (clean is not null)
            {
                return clean.ToArray();
            }

            return BestCommonFamily(top);
        }

        var collector = new FamilyCollector();
        CollectInto(node, collector, false);
        if (collector.Families.Count == 0)
        {
            return null;
        }

        return BestByTotalLength(collector.Families);

        static string[] BestByTotalLength(List<string[]> families)
        {
            var best = families[0];
            var bestScore = TotalLength(best);
            for (var i = 1; i < families.Count; i++)
            {
                var score = TotalLength(families[i]);
                if (score > bestScore)
                {
                    best = families[i];
                    bestScore = score;
                }
            }

            return best;
        }

        static int TotalLength(string[] family)
        {
            var total = 0;
            foreach (var member in family)
            {
                total += member.Length;
            }

            return total;
        }

        static void CollectInto(Utf8TreeNode node, FamilyCollector collector, bool branch)
        {
            var pending = new Stack<Utf8TreeNode>();
            pending.Push(node);
            while (pending.TryPop(out node))
            {
                switch (node.Kind)
                {
                    case Utf8TreeNodeKind.One:
                        collector.Families.Add(new string[] { node.Ch.ToString() });
                        break;
                    case Utf8TreeNodeKind.Multi:
                        if (!string.IsNullOrEmpty(node.Str))
                        {
                            collector.Families.Add(new string[] { node.Str });
                        }

                        break;
                    case Utf8TreeNodeKind.Capture:
                    case Utf8TreeNodeKind.Group:
                    case Utf8TreeNodeKind.Atomic:
                    case Utf8TreeNodeKind.Concatenate:
                        for (var i = node.ChildCount - 1; i >= 0; i--) pending.Push(node.Child(i));
                        break;
                    case Utf8TreeNodeKind.Alternate:
                        var union = UnionCleanBranches(node);
                        if (union is not null)
                        {
                            collector.Families.Add(union.ToArray());
                        }

                        break;
                    case Utf8TreeNodeKind.Loop:
                    case Utf8TreeNodeKind.Lazyloop:
                        if (node.M > 0 && node.ChildCount == 1)
                        {
                            pending.Push(node.Child(0));
                        }

                        break;
                    case Utf8TreeNodeKind.Oneloop:
                    case Utf8TreeNodeKind.Onelazy:
                    case Utf8TreeNodeKind.Oneloopatomic:
                    case Utf8TreeNodeKind.Notoneloop:
                    case Utf8TreeNodeKind.Notonelazy:
                    case Utf8TreeNodeKind.Notoneloopatomic:
                    case Utf8TreeNodeKind.Setloop:
                    case Utf8TreeNodeKind.Setlazy:
                    case Utf8TreeNodeKind.Setloopatomic:
                        if (!branch && node.N > 1 && (node.N != int.MaxValue || node.M > 1))
                        {
                            collector.Families.Clear();
                        }

                        break;
                    default:
                        break;
                }
            }
        }

        static List<string>? UnionCleanBranches(Utf8TreeNode node)
        {
            var result = new List<string>();
            for (var i = 0; i < node.ChildCount; i++)
            {
                var text = BranchText(node.Child(i));
                if (text is null)
                {
                    return null;
                }

                result.Add(text);
            }

            result.Sort(static (left, right) => string.CompareOrdinal(left, right));
            return result;
        }

        static string[]? BestCommonFamily(Utf8TreeNode node)
        {
            var members = new List<HashSet<string>>();
            for (var i = 0; i < node.ChildCount; i++)
            {
                var collector = new FamilyCollector();
                CollectInto(node.Child(i), collector, true);
                var branch = new HashSet<string>(StringComparer.Ordinal);
                foreach (var family in collector.Families)
                {
                    foreach (var member in family)
                    {
                        branch.Add(member);
                    }
                }

                members.Add(branch);
            }

            if (members.Count == 0)
            {
                return null;
            }

            var common = new List<string>();
            foreach (var candidate in members[0])
            {
                var shared = true;
                for (var i = 1; i < members.Count; i++)
                {
                    if (!members[i].Contains(candidate))
                    {
                        shared = false;
                        break;
                    }
                }

                if (shared && !common.Contains(candidate))
                {
                    common.Add(candidate);
                }
            }

            if (common.Count == 0)
            {
                return null;
            }

            common.Sort(static (left, right) => string.CompareOrdinal(left, right));
            return common.ToArray();
        }
        static string? BranchText(Utf8TreeNode node)
        {
            while (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic)
            {
                if (node.ChildCount != 1) return null;
                node = node.Child(0);
            }
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.One:
                    return node.Ch.ToString();
                case Utf8TreeNodeKind.Multi:
                    return string.IsNullOrEmpty(node.Str) ? null : node.Str;
                default:
                    return null;
            }
        }
    }

    private sealed class FamilyCollector
    {
        public readonly List<string[]> Families = new();
    }
}
