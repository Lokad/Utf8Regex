namespace Lokad.Utf8Regex.Internal.Execution;

// R05: fixed-span capture map for single exact literals whose captures partition the
// literal (concatenated runs wrapped in capturing or non-capturing groups), plus at most
// one optional single-iteration loop over a literal run and branch-invariant alternations
// whose branches agree group for group on spans and end at the same position. Each
// recorded group spans a fixed UTF-16 range of the discovered literal, so detailed shaping
// projects every group from the owned discovery hit without executing the fallback engine;
// the hit length selects the present or absent projection for optional shapes. Other
// quantifiers, divergent alternations, loops inside alternations, sets, anchors,
// lookarounds, backreferences, and empty nodes decline; a null table means the shape keeps
// the existing BCL routes. Construction is eager and pure over the owned tree: no fallback
// engine is built here.
internal sealed class Utf8GroupedLiteralSpanTable
{
    private Utf8GroupedLiteralSpanTable(
        int totalLengthInUtf16,
        int[] groups,
        int[] startsInLiteral,
        int[] lengthsInLiteral,
        bool hasOptionalLoop,
        int totalLengthWithoutLoopInUtf16,
        int[] absentStartsInLiteral,
        int[] absentLengthsInLiteral)
    {
        TotalLengthInUtf16 = totalLengthInUtf16;
        Groups = groups;
        StartsInLiteral = startsInLiteral;
        LengthsInLiteral = lengthsInLiteral;
        HasOptionalLoop = hasOptionalLoop;
        TotalLengthWithoutLoopInUtf16 = totalLengthWithoutLoopInUtf16;
        AbsentStartsInLiteral = absentStartsInLiteral;
        AbsentLengthsInLiteral = absentLengthsInLiteral;
    }

    // Total literal length in UTF-16 code units; shaping verifies the discovery hit length.
    public int TotalLengthInUtf16 { get; }

    // Parallel arrays: Groups[i] spans (StartsInLiteral[i], LengthsInLiteral[i]) in the literal.
    public int[] Groups { get; }

    public int[] StartsInLiteral { get; }

    public int[] LengthsInLiteral { get; }

    public bool HasOptionalLoop { get; }

    // Hit length when the optional loop does not participate; the absent projection applies.
    public int TotalLengthWithoutLoopInUtf16 { get; }

    // Parallel to Groups; AbsentStartsInLiteral[i] below zero marks the group unmatched
    // when the optional loop does not participate. Empty when no optional loop applies.
    public int[] AbsentStartsInLiteral { get; }

    public int[] AbsentLengthsInLiteral { get; }

    public static Utf8GroupedLiteralSpanTable? TryCreate(Utf8ExecutionTree? tree)
    {
        if (tree?.Root is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 0 } root ||
            root.Children.Count != 1)
        {
            return null;
        }

        var walker = new SpanWalker();
        if (!walker.TryWalk(root.Children[0], inLoop: false, inAlternate: false))
        {
            return null;
        }

        if (walker.Position == 0 || walker.Groups.Count == 0)
        {
            return null;
        }

        // Owned-tree capture numbers diverge from BCL group numbers for nested named
        // captures; translate every recorded group up front so shaping projects BCL
        // slots, declining on any numbering surprise.
        var slots = Utf8BclSlotTranslator.TryCreateMap(root.Children[0]);
        if (slots is null)
        {
            return null;
        }

        for (var i = 0; i < walker.Groups.Count; i++)
        {
            if (!slots.TryGetValue(walker.Groups[i], out var slot))
            {
                return null;
            }

            walker.Groups[i] = slot;
        }

        if (!walker.LoopSeen)
        {
            return new Utf8GroupedLiteralSpanTable(
                walker.Position,
                walker.Groups.ToArray(),
                walker.Starts.ToArray(),
                walker.Lengths.ToArray(),
                hasOptionalLoop: false,
                walker.Position,
                [],
                []);
        }

        var absentStarts = new int[walker.Groups.Count];
        var absentLengths = new int[walker.Groups.Count];
        var loopEnd = walker.LoopStart + walker.LoopLength;
        for (var i = 0; i < walker.Groups.Count; i++)
        {
            if (walker.InLoop[i])
            {
                absentStarts[i] = -1;
                absentLengths[i] = 0;
                continue;
            }

            var start = walker.Starts[i];
            var length = walker.Lengths[i];
            if (start <= walker.LoopStart && start + length >= loopEnd)
            {
                // The group contains the loop: its absent span is the hit minus the loop.
                absentStarts[i] = start;
                absentLengths[i] = length - walker.LoopLength;
            }
            else if (start >= loopEnd)
            {
                // The group follows the loop: its absent span shifts left by the loop.
                absentStarts[i] = start - walker.LoopLength;
                absentLengths[i] = length;
            }
            else if (start + length <= walker.LoopStart)
            {
                absentStarts[i] = start;
                absentLengths[i] = length;
            }
            else
            {
                // Partial loop overlap cannot arise from nested captures; decline.
                return null;
            }
        }

        return new Utf8GroupedLiteralSpanTable(
            walker.Position,
            walker.Groups.ToArray(),
            walker.Starts.ToArray(),
            walker.Lengths.ToArray(),
            hasOptionalLoop: true,
            walker.Position - walker.LoopLength,
            absentStarts,
            absentLengths);
    }

    private sealed class SpanWalker
    {
        internal int Position;
        internal readonly List<int> Groups = new();
        internal readonly List<int> Starts = new();
        internal readonly List<int> Lengths = new();
        internal readonly List<bool> InLoop = new();
        internal bool LoopSeen;
        internal int LoopStart = -1;
        internal int LoopLength;

        internal bool TryWalk(Utf8ExecutionNode node, bool inLoop, bool inAlternate)
        {
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.One:
                    Position += 1;
                    return true;
                case Utf8ExecutionNodeKind.Multi:
                    if (node.Text is null)
                    {
                        return false;
                    }

                    Position += node.Text.Length;
                    return true;
                case Utf8ExecutionNodeKind.Concatenate:
                    for (var i = 0; i < node.Children.Count; i++)
                    {
                        if (!TryWalk(node.Children[i], inLoop, inAlternate))
                        {
                            return false;
                        }
                    }

                    return true;
                case Utf8ExecutionNodeKind.Group:
                    if (node.Children.Count != 1)
                    {
                        return false;
                    }

                    return TryWalk(node.Children[0], inLoop, inAlternate);
                case Utf8ExecutionNodeKind.Capture:
                    if (node.CaptureNumber <= 0 || node.Children.Count != 1 || Groups.Contains(node.CaptureNumber))
                    {
                        return false;
                    }

                    var start = Position;
                    if (!TryWalk(node.Children[0], inLoop, inAlternate))
                    {
                        return false;
                    }

                    Groups.Add(node.CaptureNumber);
                    Starts.Add(start);
                    Lengths.Add(Position - start);
                    InLoop.Add(inLoop);
                    return true;
                case Utf8ExecutionNodeKind.Alternate:
                    if (node.Children.Count == 0)
                    {
                        return false;
                    }

                    // Branch-invariant alternations: the first branch walks into the
                    // shared lists while the rest walk scratch copies that must agree
                    // group for group and end at the same position. Divergence keeps
                    // the BCL routes.
                    var branchBase = Position;
                    var branchCountBase = Groups.Count;
                    if (!TryWalk(node.Children[0], inLoop, inAlternate: true))
                    {
                        return false;
                    }

                    var branchEnd = Position;
                    for (var branch = 1; branch < node.Children.Count; branch++)
                    {
                        var scratch = new SpanWalker { Position = branchBase };
                        if (!scratch.TryWalk(node.Children[branch], inLoop, inAlternate: true))
                        {
                            return false;
                        }

                        if (scratch.Position != branchEnd || !BranchMatches(scratch, branchCountBase))
                        {
                            return false;
                        }
                    }

                    return true;
                case Utf8ExecutionNodeKind.Loop:
                case Utf8ExecutionNodeKind.LazyLoop:
                    if (LoopSeen || inLoop || inAlternate || node.Min != 0 || node.Max != 1 || node.Children.Count != 1)
                    {
                        return false;
                    }

                    LoopSeen = true;
                    LoopStart = Position;
                    if (!TryWalk(node.Children[0], inLoop: true, inAlternate: false))
                    {
                        return false;
                    }

                    LoopLength = Position - LoopStart;
                    if (LoopLength == 0)
                    {
                        return false;
                    }

                    return true;
                default:
                    return false;
            }
        }

        private bool BranchMatches(SpanWalker scratch, int baseCount)
        {
            if (scratch.Groups.Count != Groups.Count - baseCount)
            {
                return false;
            }

            for (var i = 0; i < scratch.Groups.Count; i++)
            {
                if (scratch.Groups[i] != Groups[baseCount + i] ||
                    scratch.Starts[i] != Starts[baseCount + i] ||
                    scratch.Lengths[i] != Lengths[baseCount + i] ||
                    scratch.InLoop[i] != InLoop[baseCount + i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
