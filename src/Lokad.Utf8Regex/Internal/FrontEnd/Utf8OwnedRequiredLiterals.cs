namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Required literal families guide native search. Clean alternatives keep their
// ordinally sorted branches; dirty top-level alternatives keep common members.
// Nested dirty alternatives and opaque nodes contribute nothing. Selection uses
// pattern-text length as a heuristic, never as a scalar matching width.
internal static class Utf8OwnedRequiredLiterals
{
    private readonly record struct Frame(Utf8TreeNode Node, int NextChild, bool Entered);

    public static string[]? FindBestRequiredLiteralFamily(Utf8TreeNode node)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        var top = node;
        // Root admission precedes all auxiliary descriptors and payloads. Finish
        // this scan before collecting so its depth does not overlap a second walk.
        using (var wrappers = Utf8ConstructionFrames<Utf8TreeNode>.Create(budget, top))
        {
            while (true)
            {
                budget.ChargeWork(1);
                if (top.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic) || top.ChildCount != 1) break;
                top = top.Child(0);
                wrappers.Push(top);
            }
        }

        using var arena = Utf8ConstructionArena.Create(budget);
        string[]? selected;
        if (top.Kind == Utf8TreeNodeKind.Alternate)
            selected = UnionCleanBranches(top, budget, arena) ?? BestCommonFamily(top, budget, arena);
        else
        {
            using var families = Utf8ConstructionBuffer<string[]>.Create(budget);
            CollectInto(node, families, false, budget, arena);
            selected = families.Count == 0 ? null : BestByTotalLength(families, budget);
        }
        if (selected is null) return null;
        // Candidate arrays remain scratch. Copy only the selected family into a
        // transferred payload, admitting overlap with the still-live candidates.
        var result = budget.AllocateArray<string>(selected.Length, false);
        budget.ChargeWork((ulong)selected.Length);
        selected.AsSpan().CopyTo(result);
        scope.Complete();
        return result;

        static string[] BestByTotalLength(Utf8ConstructionBuffer<string[]> families, Utf8ConstructionBudget budget)
        {
            var best = families[0];
            var bestScore = TotalLength(best, budget);
            for (var i = 1; i < families.Count; i++)
            {
                budget.ChargeWork(1);
                var score = TotalLength(families[i], budget);
                if (score > bestScore) { best = families[i]; bestScore = score; }
            }
            return best;
        }

        static int TotalLength(string[] family, Utf8ConstructionBudget budget)
        {
            var total = 0;
            foreach (var member in family) { budget.ChargeWork(1); total += member.Length; }
            return total;
        }

        static void CollectInto(Utf8TreeNode node, Utf8ConstructionBuffer<string[]> families,
            bool branch, Utf8ConstructionBudget budget, Utf8ConstructionArena arena)
        {
            using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(node, 0, false));
            while (frames.Count != 0)
            {
                budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                node = frame.Node;
                if (!frame.Entered)
                {
                    frame = frame with { Entered = true };
                    switch (node.Kind)
                    {
                        case Utf8TreeNodeKind.One:
                            var one = arena.Allocate<string>(1, false);
                            one[0] = budget.CopyText([node.Ch]);
                            families.Add(one);
                            break;
                        case Utf8TreeNodeKind.Multi:
                            if (!string.IsNullOrEmpty(node.Str))
                            {
                                var multi = arena.Allocate<string>(1, false);
                                multi[0] = node.Str;
                                families.Add(multi);
                            }
                            break;
                        case Utf8TreeNodeKind.Alternate:
                            var union = UnionCleanBranches(node, budget, arena);
                            if (union is not null) families.Add(union);
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
                            if (!branch && node.N > 1 && (node.N != int.MaxValue || node.M > 1)) families.Clear();
                            break;
                    }
                }
                var childCount = node.Kind switch
                {
                    Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic or Utf8TreeNodeKind.Concatenate => node.ChildCount,
                    Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop when node.M > 0 && node.ChildCount == 1 => 1,
                    _ => 0,
                };
                if (frame.NextChild == childCount) { frames.Pop(); continue; }
                frames[index] = frame with { NextChild = frame.NextChild + 1 };
                frames.Push(new(node.Child(frame.NextChild), 0, false));
            }
        }

        static string[]? UnionCleanBranches(Utf8TreeNode node, Utf8ConstructionBudget budget, Utf8ConstructionArena arena)
        {
            using var result = Utf8ConstructionBuffer<string>.Create(budget);
            for (var i = 0; i < node.ChildCount; i++)
            {
                budget.ChargeWork(1);
                var text = BranchText(node.Child(i), budget);
                if (text is null) return null;
                result.Add(text);
            }
            result.Sort(CompareOrdinal);
            return arena.Take(result, false);
        }

        static int CompareOrdinal(string left, string right)
        {
            Utf8ConstructionScope.ChargeWork((ulong)Math.Min(left.Length, right.Length));
            return string.CompareOrdinal(left, right);
        }

        static string[]? BestCommonFamily(Utf8TreeNode node, Utf8ConstructionBudget budget, Utf8ConstructionArena arena)
        {
            using var members = Utf8ConstructionBuffer<Utf8ConstructionMap<string, bool>>.Create(budget);
            using var candidates = Utf8ConstructionBuffer<string>.Create(budget);
            using var common = Utf8ConstructionBuffer<string>.Create(budget);
            try
            {
                for (var i = 0; i < node.ChildCount; i++)
                {
                    budget.ChargeWork(1);
                    var set = Utf8ConstructionMap<string, bool>.Create(budget, StringComparer.Ordinal);
                    // Publish before populating so every acquired table is released
                    // by the unmetered finally path, including partial branches.
                    members.Add(set);
                    using var families = Utf8ConstructionBuffer<string[]>.Create(budget);
                    CollectInto(node.Child(i), families, true, budget, arena);
                    foreach (var family in families)
                        foreach (var member in family)
                        {
                            budget.ChargeWork(1);
                            if (set.ContainsKey(member)) continue;
                            set[member] = true;
                            if (i == 0) candidates.Add(member);
                        }
                }
                if (members.Count == 0) return null;
                foreach (var candidate in candidates)
                {
                    budget.ChargeWork(1);
                    var shared = true;
                    for (var i = 1; i < members.Count; i++)
                    {
                        budget.ChargeWork(1);
                        if (!members[i].ContainsKey(candidate)) { shared = false; break; }
                    }
                    if (shared) common.Add(candidate);
                }
                if (common.Count == 0) return null;
                common.Sort(CompareOrdinal);
                return arena.Take(common, false);
            }
            finally { foreach (var set in members) set.Dispose(); }
        }

        static string? BranchText(Utf8TreeNode node, Utf8ConstructionBudget budget)
        {
            using var frames = Utf8ConstructionFrames<Utf8TreeNode>.Create(budget, node);
            while (true)
            {
                budget.ChargeWork(1);
                if (node.Kind is not (Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic)) break;
                if (node.ChildCount != 1) return null;
                node = node.Child(0);
                frames.Push(node);
            }
            return node.Kind switch
            {
                Utf8TreeNodeKind.One => budget.CopyText([node.Ch]),
                Utf8TreeNodeKind.Multi => string.IsNullOrEmpty(node.Str) ? null : node.Str,
                _ => null,
            };
        }
    }
}
