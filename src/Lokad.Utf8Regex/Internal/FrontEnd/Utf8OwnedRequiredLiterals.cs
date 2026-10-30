namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Required literal families guide native search. Clean alternatives keep their
// byte-sorted branches; dirty top-level alternatives keep a whole family
// that covers a mandatory family in every branch. Families are disjunctions:
// intersecting individual members cannot prove that a match contains one.
// Nested dirty alternatives and opaque nodes contribute nothing. Selection uses
// UTF-8 byte length as a heuristic, never as a scalar matching width.
internal static class Utf8OwnedRequiredLiterals
{
    private readonly record struct Frame(Utf8TreeNode Node, int NextChild, bool Entered);

    public static byte[][]? FindBestRequiredLiteralFamily(Utf8TreeNode node)
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
        byte[][]? selected;
        if (top.Kind == Utf8TreeNodeKind.Alternate)
            selected = UnionCleanBranches(top, budget, arena) ?? BestCommonFamily(top, budget, arena);
        else
        {
            using var families = Utf8ConstructionBuffer<byte[][]>.Create(budget);
            CollectInto(node, families, false, budget, arena);
            selected = families.Count == 0 ? null : BestByTotalLength(families, budget);
        }
        if (selected is null) return null;
        // Candidate arrays remain scratch. Copy only the selected family into a
        // transferred payload, admitting overlap with the still-live candidates.
        var result = budget.AllocateArray<byte[]>(selected.Length, false);
        budget.ChargeWork((ulong)selected.Length);
        selected.AsSpan().CopyTo(result);
        scope.Complete();
        return result;

        static byte[][] BestByTotalLength(Utf8ConstructionBuffer<byte[][]> families, Utf8ConstructionBudget budget)
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

        static long TotalLength(byte[][] family, Utf8ConstructionBudget budget)
        {
            long total = 0;
            foreach (var member in family) { budget.ChargeWork(1); total += member.Length; }
            return total;
        }

        static void CollectInto(Utf8TreeNode node, Utf8ConstructionBuffer<byte[][]> families,
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
                        case Utf8TreeNodeKind.One when Utf8OwnedFindOptimizations.IsExactLiteral(node):
                            var one = arena.Allocate<byte[]>(1, false);
                            one[0] = budget.EncodeUtf8([node.Ch]);
                            families.Add(one);
                            break;
                        case Utf8TreeNodeKind.Multi:
                            if (Utf8OwnedFindOptimizations.IsExactLiteral(node))
                            {
                                var multi = arena.Allocate<byte[]>(1, false);
                                multi[0] = node.LiteralUtf8 ?? budget.EncodeUtf8(node.Str);
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
                            if (!branch && node.N > 1 && (!node.IsUnboundedRepeat || node.M > 1)) families.Clear();
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

        static byte[][]? UnionCleanBranches(Utf8TreeNode node, Utf8ConstructionBudget budget, Utf8ConstructionArena arena)
        {
            if (node.ChildCount == 0) return null;
            using var result = Utf8ConstructionBuffer<byte[]>.Create(budget);
            for (var i = 0; i < node.ChildCount; i++)
            {
                budget.ChargeWork(1);
                var literal = BranchBytes(node.Child(i), budget);
                if (literal is null) return null;
                result.Add(literal);
            }
            result.Sort(CompareBytes);
            return arena.Take(result, false);
        }

        static int CompareBytes(byte[] left, byte[] right)
        {
            Utf8ConstructionScope.ChargeWork((ulong)Math.Min(left.Length, right.Length));
            return left.AsSpan().SequenceCompareTo(right);
        }

        static byte[][]? BestCommonFamily(Utf8TreeNode node, Utf8ConstructionBudget budget, Utf8ConstructionArena arena)
        {
            if (node.ChildCount == 0) return null;
            using var candidates = Utf8ConstructionBuffer<byte[][]>.Create(budget);
            CollectInto(node.Child(0), candidates, true, budget, arena);
            for (var branch = 1; branch < node.ChildCount && candidates.Count != 0; branch++)
            {
                budget.ChargeWork(1);
                using var families = Utf8ConstructionBuffer<byte[][]>.Create(budget);
                CollectInto(node.Child(branch), families, true, budget, arena);
                var kept = 0;
                for (var index = 0; index < candidates.Count; index++)
                {
                    budget.ChargeWork(1);
                    var candidate = candidates[index];
                    var covered = false;
                    foreach (var family in families)
                    {
                        budget.ChargeWork(1);
                        if (Covers(candidate, family)) { covered = true; break; }
                    }
                    if (covered) candidates[kept++] = candidate;
                }
                while (candidates.Count > kept) { budget.ChargeWork(1); candidates.Pop(); }
            }
            return candidates.Count == 0 ? null : BestByTotalLength(candidates, budget);

            // A candidate disjunction covers a branch when it contains every
            // member of at least one mandatory family of that branch. Sorted
            // borrowed arrays permit a metered merge without flattening maps.
            static bool Covers(byte[][] candidate, byte[][] required)
            {
                if (candidate.Length == 0 || required.Length == 0) return false;
                var index = 0;
                foreach (var member in required)
                {
                    while (index < candidate.Length)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        var order = CompareBytes(candidate[index], member);
                        if (order > 0) return false;
                        if (order == 0) break;
                        index++;
                    }
                    if (index == candidate.Length) return false;
                }
                return true;
            }
        }

        static byte[]? BranchBytes(Utf8TreeNode node, Utf8ConstructionBudget budget)
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
                Utf8TreeNodeKind.One when Utf8OwnedFindOptimizations.IsExactLiteral(node) => budget.EncodeUtf8([node.Ch]),
                Utf8TreeNodeKind.Multi when Utf8OwnedFindOptimizations.IsExactLiteral(node) => node.LiteralUtf8 ?? budget.EncodeUtf8(node.Str),
                _ => null,
            };
        }
    }
}
