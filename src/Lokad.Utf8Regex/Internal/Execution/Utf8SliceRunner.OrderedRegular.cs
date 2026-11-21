using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    // Tags index the prepared leaf table. Zero terminates the packed order.
    private const int OrderedTagBits = 4;
    private const ulong OrderedTagMask = (1UL << OrderedTagBits) - 1;
    private const int OrderedLeafLimit = (int)OrderedTagMask;

    internal static bool CanPrepareOrdinaryOrderedSearch(
        ReadOnlySpan<Utf8SliceInstruction> instructions, Utf8ConstructionBudget budget)
    {
        if (instructions.Length > 64 ||
            instructions[0] is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Beginning, B: 0 })
            return false;
        budget.ChargeWork((ulong)instructions.Length);
        var leaves = 0;
        foreach (var instruction in instructions)
        {
            switch (instruction.Kind)
            {
                // The ordinary lowerer emits these backward edges only for
                // greedy loops with a body that consumes on every completion.
                case Utf8SliceInstructionKind.Jump when instruction.B == 0:
                case Utf8SliceInstructionKind.Fork when instruction.B == 0:
                case Utf8SliceInstructionKind.CaptureStart:
                case Utf8SliceInstructionKind.CaptureEnd:
                case Utf8SliceInstructionKind.Fail:
                    break;
                case Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set or
                     Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.Shorthand or
                     Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid or
                     Utf8SliceInstructionKind.Accept or Utf8SliceInstructionKind.Boundary:
                case Utf8SliceInstructionKind.Anchor when instruction.A != (int)Utf8SliceAnchorKind.Contiguous:
                    if (++leaves > OrderedLeafLimit) return false;
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    internal static int[] BuildOrdinaryRepeatClosures(ReadOnlySpan<Utf8SliceInstruction> instructions,
        Utf8ConstructionBudget budget, out ulong[] ancestors, out (ulong[] Closures, byte[] Leaves, uint Conditions) ordered)
    {
        ancestors = [];
        ordered = ([], [], 0);
        if (instructions.Length > 64) return [];

        // The projected graph keeps the original atom indexes. Only the
        // closures survive, so capture replay still uses the original VM.
        Span<Utf8SliceInstruction> projected = stackalloc Utf8SliceInstruction[instructions.Length];
        budget.ChargeWork(2UL * (ulong)instructions.Length);
        instructions.CopyTo(projected);
        for (var pc = 0; pc < instructions.Length; pc++)
        {
            var repeat = instructions[pc];
            if (repeat.Kind != Utf8SliceInstructionKind.RepeatGreedy) continue;
            if (instructions.Length - pc < 5 || repeat.RepeatMaximum != long.MaxValue || repeat.RepeatMinimum > 1 ||
                repeat.A != pc + 3 || instructions[pc + 1].Kind is not (Utf8SliceInstructionKind.Literal or
                    Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid or
                    Utf8SliceInstructionKind.Shorthand or Utf8SliceInstructionKind.Dot) ||
                instructions[pc + 2].Kind != Utf8SliceInstructionKind.RepeatEnd || instructions[pc + 2].A != pc ||
                instructions[pc + 3].Kind != Utf8SliceInstructionKind.RepeatExit || instructions[pc + 3].A != repeat.B)
                return [];

            if (repeat.RepeatMinimum == 0)
            {
                projected[pc] = new(Utf8SliceInstructionKind.Fork, pc + 3, 0, 0, 0);
                projected[pc + 2] = new(Utf8SliceInstructionKind.Jump, pc, 0, 0, 0);
                projected[pc + 3] = new(Utf8SliceInstructionKind.Jump, pc + 4, 0, 0, 0);
            }
            else
            {
                // Consume the first atom, then give the repeat edge priority
                // over leaving. The original exit slot is local to the loop.
                projected[pc] = new(Utf8SliceInstructionKind.Jump, pc + 1, 0, 0, 0);
                projected[pc + 2] = new(Utf8SliceInstructionKind.Fork, pc + 4, 0, 0, 0);
                projected[pc + 3] = new(Utf8SliceInstructionKind.Jump, pc + 1, 0, 0, 0);
            }
            pc += 3;
        }
        return CanPrepareOrdinaryOrderedSearch(projected, budget)
            ? Utf8NonBacktrackingSearchState.BuildEpsilonOrder(projected, out ancestors, out ordered) : [];
    }

    internal static bool CanSearchOrdinaryOrdered(Utf8SliceProgram program, int remaining,
        in Utf8SearchPlan search, ref Utf8SliceInvocation invocation)
    {
        if (remaining < 256 || program.NonBacktrackingOrderedClosures.Length == 0) return false;
        var depth = invocation.Budget.Limits.MaxDepth;
        if (program.RepeatCount == 0) return depth == 0;

        // Keep more selective prepared starts. Tight depth limits retain
        // the scalar path, which can admit less than a complete frontier.
        return search.Kind == Utf8SearchKind.None &&
            (depth == 0 || depth >= (uint)(program.NonBacktrackingOrderedLeaves.Length - 1));
    }

    internal static (ulong[] Closures, byte[] Leaves, uint Conditions) BuildOrderedRegularClosures(
        ReadOnlySpan<Utf8SliceInstruction> instructions, ReadOnlySpan<int> epsilonOrder, Utf8ConstructionBudget budget)
    {
        if (instructions.Length > 64) return ([], [], 0);
        budget.ChargeWork((ulong)instructions.Length);
        Span<byte> leaves = stackalloc byte[OrderedLeafLimit + 1];
        Span<byte> tagsByInstruction = stackalloc byte[instructions.Length];
        leaves[0] = 0;
        var leafCount = 0;
        uint conditions = 0;
        for (var pc = 0; pc < instructions.Length; pc++)
        {
            var kind = instructions[pc].Kind;
            switch (kind)
            {
                case Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Jump or
                    Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or Utf8SliceInstructionKind.Fail:
                    break;
                default:
                    if (++leafCount > OrderedLeafLimit) return ([], [], 0);
                    leaves[leafCount] = (byte)pc;
                    tagsByInstruction[pc] = (byte)leafCount;
                    if (kind is Utf8SliceInstructionKind.Anchor or Utf8SliceInstructionKind.Boundary)
                        conditions |= 1U << leafCount;
                    break;
            }
        }

        var closures = budget.AllocateArray<ulong>(instructions.Length, false);
        // The viability order already visits epsilon children before parents.
        foreach (var pc in epsilonOrder)
        {
            budget.ChargeWork(1);
            var instruction = instructions[pc];
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Fork:
                    budget.ChargeWork(2UL * (ulong)leafCount);
                    var first = instruction.B == 0 ? pc + 1 : instruction.A;
                    var second = instruction.B == 0 ? instruction.A : pc + 1;
                    var joined = new OrderedRegularFrontier();
                    for (var tags = closures[first]; tags != 0; tags >>= OrderedTagBits)
                        joined.TryAppend(tags & OrderedTagMask);
                    for (var tags = closures[second]; tags != 0; tags >>= OrderedTagBits)
                        joined.TryAppend(tags & OrderedTagMask);
                    closures[pc] = joined.Order;
                    break;
                case Utf8SliceInstructionKind.Jump:
                    closures[pc] = closures[instruction.A];
                    break;
                case Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd:
                    closures[pc] = closures[pc + 1];
                    break;
                case Utf8SliceInstructionKind.Fail:
                    break;
                default:
                    closures[pc] = tagsByInstruction[pc];
                    break;
            }
        }
        var leafInstructions = budget.AllocateArray<byte>(leafCount + 1, false);
        leaves[..(leafCount + 1)].CopyTo(leafInstructions);
        return (closures, leafInstructions, conditions);
    }

    internal static PreparedByteSearch GetOrderedRegularStartBytes(
        Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        // For a necessary start set, assume every zero-width condition holds.
        // An empty acceptance disqualifies skipping bytes altogether.
        ulong reachable = 1;
        var order = program.NonBacktrackingEpsilonOrder;
        for (var index = order.Length - 1; index >= 0; index--)
        {
            budget.ChargeWork(1);
            var pc = order[index];
            if ((program.NonBacktrackingEpsilonAncestors[pc] & reachable) != 0)
                reachable |= 1UL << (pc + 1);
        }
        if ((program.NonBacktrackingAcceptClosure & reachable) != 0) return default;

        Span<ulong> starts = stackalloc ulong[4];
        starts.Clear();
        var behavior = program.CaseBehavior;
        for (var candidates = program.NonBacktrackingConsumerMask; candidates != 0; candidates &= candidates - 1)
        {
            budget.ChargeWork(1);
            var pc = BitOperations.TrailingZeroCount(candidates);
            if ((program.NonBacktrackingEpsilonAncestors[pc] & reachable) == 0) continue;
            var instruction = program.Instructions[pc];
            if (instruction.Kind == Utf8SliceInstructionKind.Literal)
            {
                if (!Rune.TryCreate(instruction.A, out var literal)) return default;
                AddOrderedRegularStarts(literal, instruction.B != 0, behavior, starts);
                continue;
            }

            if (GetUnfoldedUnicodeLeadRange(program, instruction, budget, out var last) is >= 0 and var first)
            {
                for (var starter = first; starter <= last; starter++)
                    AddStartByte((byte)starter, starts);
                continue;
            }

            if (TryGetPositiveCategorySet(program, instruction, out _, out var low, out var high))
            {
                // ASCII membership is known. Keep every multibyte lead as a
                // candidate and leave scalar category membership to execution.
                starts[0] |= low;
                starts[1] |= high;
                starts[3] |= Utf8AsciiBytePredicates.DecimalOrScalarLead.Mask3;
                continue;
            }

            // Acceptance, complements and general class predicates lack a
            // complete list of scalar starts here.
            if (instruction is not { Kind: Utf8SliceInstructionKind.Set, B: 0 }) return default;
            var mask = program.SetMasks[instruction.A];
            budget.ChargeWork((ulong)mask.Length);
            for (var index = 0; index < mask.Length; index++)
                for (var bits = (uint)mask[index]; bits != 0; bits &= bits - 1)
                {
                    budget.ChargeWork(1);
                    var scalar = new Rune((index << 3) + BitOperations.TrailingZeroCount(bits));
                    AddOrderedRegularStarts(scalar, instruction.C != 0, behavior, starts);
                }
        }
        Span<byte> values = stackalloc byte[256];
        var count = 0;
        for (var block = 0; block < starts.Length; block++)
            for (var bits = starts[block]; bits != 0; bits &= bits - 1)
                values[count++] = (byte)(64 * block + BitOperations.TrailingZeroCount(bits));
        return PreparedByteSearch.Create(values[..count]);
    }

    private static void AddOrderedRegularStarts(Rune scalar, bool folded, Utf8ScalarCaseBehavior behavior,
        Span<ulong> starts)
    {
        AddStartByte(Utf8StarterByte(scalar.Value), starts);
        if (!folded) return;
        if (!scalar.IsBmp)
        {
            // Supplementary equivalence uses simple lowercase. Its table
            // consists of reversible pairs contained in the supplementary planes.
            var lower = Utf8SupplementaryCase.Lower(scalar.Value);
            AddStartByte(Utf8StarterByte(lower), starts);
            AddStartByte(Utf8StarterByte(Utf8SupplementaryCase.Upper(lower)), starts);
            return;
        }
        foreach (var equivalent in Utf8SliceCaseEquivalences.GetEquivalents((char)scalar.Value, behavior))
            AddStartByte(Utf8StarterByte(equivalent), starts);
    }

    private static void AddStartByte(byte first, Span<ulong> starts)
        => starts[first >> 6] |= 1UL << (first & 63);

    private static int GetOrderedRangeOriginCapacity(Utf8SliceProgram program)
        => Math.Max(9, program.NonBacktrackingOrderedLeaves.Length - 1);

    private static ulong GetOrderedRangeOriginBytes(int capacity)
        => (ulong)(2 * capacity * Unsafe.SizeOf<int>());

    private static bool CanAdmitOrdinaryOrderedRange(Utf8SliceProgram program, ref Utf8SliceInvocation invocation)
    {
        ref var ledger = ref invocation.Ledger;
        if (ledger.HasMemoryAllowance && program.RepeatCount == 0) return false;
        var cap = invocation.Budget.Limits.MaxWorkingBytes;
        if (cap == 0) return true;
        // The origin vectors are required. The transition table is optional.
        // Keep ordinary verification when the numeric cap cannot admit both vectors.
        var bytes = GetOrderedRangeOriginBytes(GetOrderedRangeOriginCapacity(program));
        return bytes <= cap && ledger.ReservedBytes <= cap - bytes;
    }

    internal static Utf8SliceSearchOutcome FindOrderedRegularRange(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int fromByte, ref Utf8SliceInvocation invocation)
    {
        const int capacity = 16;
        var leaves = program.NonBacktrackingOrderedLeaves;
        if (program.RepeatCount != 0 && !program.IsNonBacktracking)
        {
            var depth = invocation.Budget.TryNoteDepth((uint)(leaves.Length - 1));
            if (depth != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, depth);
        }
        var originCapacity = GetOrderedRangeOriginCapacity(program);
        var originBytes = GetOrderedRangeOriginBytes(originCapacity);
        var cacheBytes = (ulong)(capacity * Unsafe.SizeOf<OrderedRegularTransition>());
        var limit = invocation.Budget.Limits.MaxWorkingBytes;
        var total = originBytes + cacheBytes;
        var cache = input.Length - fromByte >= 64 &&
            (limit == 0 || total <= limit && invocation.Ledger.ReservedBytes <= limit - total);
        var reserved = cache ? total : originBytes;
        var breach = invocation.TryReserveWorking(reserved);
        if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
        try
        {
            Span<int> origins = stackalloc int[originCapacity];
            Span<int> nextOrigins = stackalloc int[originCapacity];
            Span<OrderedRegularTransition> table = stackalloc OrderedRegularTransition[cache ? capacity : 0];
            table.Clear();
            var closures = program.NonBacktrackingOrderedClosures;
            var starts = input.Length - fromByte >= 64 ? program.NonBacktrackingOrderedStartBytes : default;
            var conditionMask = program.NonBacktrackingOrderedConditions;
            var current = new OrderedRegularFrontier();
            var bestStart = -1;
            var bestEnd = -1;
            var position = fromByte;
            while (true)
            {
                if (current.Count == 0 && bestStart < 0 && starts.Count != 0 && position < input.Length &&
                    !starts.Contains(input[position]))
                {
                    breach = TrySeekRegularStart(input, ref position, starts, ref invocation);
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                }
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                // Active starts precede the new start. A saved acceptance
                // eliminates future starts, while earlier threads can extend it.
                if (bestStart < 0)
                    for (var tags = closures[0]; tags != 0; tags >>= OrderedTagBits)
                        if (current.TryAppend(tags & OrderedTagMask)) origins[current.Count - 1] = position;

                if ((current.Seen & conditionMask) != 0)
                {
                    // Resolve conditions before using the cache. Consumption
                    // then depends only on the expanded order and the scalar.
                    var expanded = new OrderedRegularFrontier();
                    uint seenConditions = 0;
                    var source = 0;
                    for (var tags = current.Order; tags != 0; tags >>= OrderedTagBits, source++)
                    {
                        breach = AddOrderedConditionLeaves(program, input, position, tags & OrderedTagMask,
                            origins[source], nextOrigins, ref expanded, ref seenConditions, ref invocation);
                        if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                    }
                    var unexpanded = origins;
                    origins = nextOrigins;
                    nextOrigins = unexpanded;
                    current = expanded;
                }

                var hasScalar = TryReadScalar(input, position, false, out var scalar, out var width);
                var value = hasScalar ? scalar.Value : -1;
                var index = (int)((current.Order ^ (current.Order >> 32) ^ (uint)(value * 13)) & (capacity - 1));
                var canCache = cache && current.Order != 0;
                OrderedRegularTransition next;
                if (canCache && table[index].Key == current.Order && table[index].Scalar == value)
                    next = table[index];
                else
                {
                    next = new() { Key = current.Order, Scalar = value, AcceptAt = -1 };
                    var source = 0;
                    for (var tags = current.Order; tags != 0; tags >>= OrderedTagBits, source++)
                    {
                        var pc = leaves[(int)(tags & OrderedTagMask)];
                        var instruction = program.Instructions[pc];
                        if (instruction.Kind == Utf8SliceInstructionKind.Accept)
                        {
                            // This frontier is in priority order. Lower-priority
                            // paths cannot affect the saved match or its extension.
                            next.AcceptAt = source;
                            break;
                        }
                        if (!hasScalar) continue;
                        var hit = MatchesNonBacktrackingAtom(program, instruction, scalar, ref invocation, out breach);
                        if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                        if (!hit) continue;
                        for (var closure = closures[pc + 1]; closure != 0; closure >>= OrderedTagBits)
                            if (next.Frontier.TryAppend(closure & OrderedTagMask))
                                next.SourceMap |= (ulong)source << (4 * (next.Frontier.Count - 1));
                    }
                    if (canCache) table[index] = next;
                }

                if (next.AcceptAt >= 0)
                {
                    bestStart = origins[next.AcceptAt];
                    bestEnd = position;
                }
                if (!hasScalar || bestStart >= 0 && next.Frontier.Count == 0)
                {
                    invocation.Budget.ThrowIfCancellationRequested();
                    invocation.Budget.CheckDeadline();
                    return new(bestStart >= 0, bestStart, bestEnd, null, null, Utf8ExecutionLimitKind.None);
                }

                // The cache stores origin indexes. Every call's raw byte
                // offsets stay in its two ordered frontiers.
                var sourceMap = next.SourceMap;
                for (var lane = 0; lane < next.Frontier.Count; lane++, sourceMap >>= 4)
                    nextOrigins[lane] = origins[(int)sourceMap & 15];
                var previous = origins;
                origins = nextOrigins;
                nextOrigins = previous;
                current = next.Frontier;
                position += width;
            }
        }
        finally { invocation.Ledger.Release(reserved); }
    }

    private static Utf8ExecutionLimitKind AddOrderedConditionLeaves(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, ulong tag, int start, Span<int> origins,
        ref OrderedRegularFrontier frontier, ref uint seenConditions, ref Utf8SliceInvocation invocation)
    {
        var bit = 1U << (int)tag;
        if ((program.NonBacktrackingOrderedConditions & bit) == 0)
        {
            if (frontier.TryAppend(tag)) origins[frontier.Count - 1] = start;
            return Utf8ExecutionLimitKind.None;
        }
        if ((seenConditions & bit) != 0) return Utf8ExecutionLimitKind.None;
        seenConditions |= bit;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var pc = program.NonBacktrackingOrderedLeaves[(int)tag];
        ref readonly var instruction = ref program.Instructions[pc];
        var holds = instruction.Kind == Utf8SliceInstructionKind.Anchor
            ? HoldsNonBacktrackingAnchor(instruction, input, position)
            : MatchesBoundary(program, instruction, input, position);
        if (!holds) return Utf8ExecutionLimitKind.None;

        // Epsilon lowering excludes cycles. The first arrival has priority
        // even when another start reaches the same condition at this edge.
        for (var tags = program.NonBacktrackingOrderedClosures[pc + 1]; tags != 0; tags >>= OrderedTagBits)
        {
            breach = AddOrderedConditionLeaves(program, input, position, tags & OrderedTagMask,
                start, origins, ref frontier, ref seenConditions, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
        }
        return Utf8ExecutionLimitKind.None;
    }

    internal static Utf8ExecutionLimitKind TrySeekRegularStart(ReadOnlySpan<byte> input, ref int position,
        in PreparedByteSearch starts, ref Utf8SliceInvocation invocation)
    {
        // A proved start byte cannot occur inside a scalar. Each caller has
        // established that there is no older path to advance over this gap.
        while (position < input.Length)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var block = input.Slice(position, Math.Min(2048, input.Length - position));
            var candidate = starts.IndexOf(block);
            if (candidate >= 0) { position += candidate; break; }
            position += block.Length;
        }
        return Utf8ExecutionLimitKind.None;
    }

    internal static Utf8SliceSearchOutcome ReplayOrderedRegularRange(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, Utf8SliceSearchOutcome selected, Utf8SliceSlots slots,
        ref Utf8SliceInvocation invocation)
    {
        if (selected.EndByte - selected.StartByte <= 32)
            return Utf8NonBacktrackingSearchState.ReplayCompactRange(program, input, selected, slots, ref invocation);

        // Anchors and boundaries can read beyond the winning end.
        var subject = program.NonBacktrackingEpsilonOrder.Length == 0 ? input[..selected.EndByte] : input;
        var replay = TryMatchAt(program, subject, selected.StartByte,
            slots.Starts, slots.Ends, true, ref invocation);
        if (replay.Breach != Utf8ExecutionLimitKind.None)
            return new(false, -1, -1, null, null, replay.Breach);
        if (!replay.IsMatch || replay.EndIndex != selected.EndByte)
            throw new InvalidOperationException("NonBacktracking capture replay changed the selected range.");
        return selected;
    }

    private struct OrderedRegularFrontier
    {
        internal ulong Order;
        internal uint Seen;
        internal int Count;

        internal bool TryAppend(ulong tag)
        {
            var bit = 1U << (int)tag;
            if ((Seen & bit) != 0) return false;
            Seen |= bit;
            Order |= tag << (OrderedTagBits * Count++);
            return true;
        }
    }

    private struct OrderedRegularTransition
    {
        internal ulong Key;
        internal ulong SourceMap;
        internal int Scalar;
        internal int AcceptAt;
        internal OrderedRegularFrontier Frontier;
    }
}
