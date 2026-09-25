namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

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
    public static string[]? FindBestRequiredLiteralFamily(RuntimeFrontEnd.RegexNode node)
    {
        var top = node;
        while ((top.Kind is RuntimeFrontEnd.RegexNodeKind.Capture or RuntimeFrontEnd.RegexNodeKind.Group or RuntimeFrontEnd.RegexNodeKind.Atomic) && top.ChildCount == 1)
        {
            top = top.Child(0);
        }

        if (top.Kind == RuntimeFrontEnd.RegexNodeKind.Alternate)
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

        static void CollectInto(RuntimeFrontEnd.RegexNode node, FamilyCollector collector, bool branch)
        {
            switch (node.Kind)
            {
                case RuntimeFrontEnd.RegexNodeKind.One:
                    collector.Families.Add(new string[] { node.Ch.ToString() });
                    return;
                case RuntimeFrontEnd.RegexNodeKind.Multi:
                    if (!string.IsNullOrEmpty(node.Str))
                    {
                        collector.Families.Add(new string[] { node.Str });
                    }

                    return;
                case RuntimeFrontEnd.RegexNodeKind.Capture:
                case RuntimeFrontEnd.RegexNodeKind.Group:
                case RuntimeFrontEnd.RegexNodeKind.Atomic:
                case RuntimeFrontEnd.RegexNodeKind.Concatenate:
                    for (var i = 0; i < node.ChildCount; i++)
                    {
                        CollectInto(node.Child(i), collector, branch);
                    }

                    return;
                case RuntimeFrontEnd.RegexNodeKind.Alternate:
                    var union = UnionCleanBranches(node);
                    if (union is not null)
                    {
                        collector.Families.Add(union.ToArray());
                    }

                    return;
                case RuntimeFrontEnd.RegexNodeKind.Loop:
                case RuntimeFrontEnd.RegexNodeKind.Lazyloop:
                    if (node.M > 0 && node.ChildCount == 1)
                    {
                        CollectInto(node.Child(0), collector, branch);
                    }

                    return;
                case RuntimeFrontEnd.RegexNodeKind.Oneloop:
                case RuntimeFrontEnd.RegexNodeKind.Onelazy:
                case RuntimeFrontEnd.RegexNodeKind.Oneloopatomic:
                case RuntimeFrontEnd.RegexNodeKind.Notoneloop:
                case RuntimeFrontEnd.RegexNodeKind.Notonelazy:
                case RuntimeFrontEnd.RegexNodeKind.Notoneloopatomic:
                case RuntimeFrontEnd.RegexNodeKind.Setloop:
                case RuntimeFrontEnd.RegexNodeKind.Setlazy:
                case RuntimeFrontEnd.RegexNodeKind.Setloopatomic:
                    if (!branch && node.N > 1 && (node.N != int.MaxValue || node.M > 1))
                    {
                        collector.Families.Clear();
                    }

                    return;
                default:
                    return;
            }
        }

        static List<string>? UnionCleanBranches(RuntimeFrontEnd.RegexNode node)
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

        static string[]? BestCommonFamily(RuntimeFrontEnd.RegexNode node)
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
        static string? BranchText(RuntimeFrontEnd.RegexNode node)
        {
            switch (node.Kind)
            {
                case RuntimeFrontEnd.RegexNodeKind.One:
                    return node.Ch.ToString();
                case RuntimeFrontEnd.RegexNodeKind.Multi:
                    return string.IsNullOrEmpty(node.Str) ? null : node.Str;
                case RuntimeFrontEnd.RegexNodeKind.Capture:
                case RuntimeFrontEnd.RegexNodeKind.Group:
                case RuntimeFrontEnd.RegexNodeKind.Atomic:
                    return node.ChildCount == 1 ? BranchText(node.Child(0)) : null;
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
