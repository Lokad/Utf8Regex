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

        // Keep more selective prepared forward starts. Tight depth limits retain
        // the scalar path, which can admit less than a complete frontier.
        return (program.RightToLeft || search.Kind == Utf8SearchKind.None) &&
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
        for (var candidates = program.NonBacktrackingConsumerMask; candidates != 0; candidates &= candidates - 1)
        {
            budget.ChargeWork(1);
            var pc = BitOperations.TrailingZeroCount(candidates);
            if ((program.NonBacktrackingEpsilonAncestors[pc] & reachable) == 0) continue;
            if (!TryAddRegularStartBytes(program, program.Instructions[pc], starts, budget)) return default;
        }
        return BuildRegularStartSearch(starts);
    }

    internal static PreparedByteSearch GetWideRegularStartBytes(Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        const int visitLimit = 64;
        Span<int> branches = stackalloc int[visitLimit];
        Span<ulong> starts = stackalloc ulong[4];
        starts.Clear();
        var branchCount = 0;
        var pc = 0;

        // Inspect only the epsilon paths from the root. Conditions can hold
        // and cannot provide a byte. Abandon open paths at the visit limit.
        for (var visit = 0; visit < visitLimit; visit++)
        {
            budget.ChargeWork(1);
            ref readonly var instruction = ref program.Instructions[pc];
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Fork:
                    branches[branchCount++] = instruction.A;
                    pc++;
                    continue;
                case Utf8SliceInstructionKind.Jump:
                    pc = instruction.A;
                    continue;
                case Utf8SliceInstructionKind.CaptureStart:
                case Utf8SliceInstructionKind.CaptureEnd:
                case Utf8SliceInstructionKind.Anchor:
                case Utf8SliceInstructionKind.Boundary:
                    pc++;
                    continue;
                case Utf8SliceInstructionKind.Accept:
                    return default;
                case Utf8SliceInstructionKind.Fail:
                    break;
                default:
                    if (!TryAddRegularStartBytes(program, instruction, starts, budget)) return default;
                    break;
            }
            if (branchCount == 0) return BuildRegularStartSearch(starts);
            pc = branches[--branchCount];
        }
        return default;
    }

    private static bool TryAddRegularStartBytes(Utf8SliceProgram program, in Utf8SliceInstruction instruction,
        Span<ulong> starts, Utf8ConstructionBudget budget)
    {
        var behavior = program.CaseBehavior;
        if (instruction.Kind == Utf8SliceInstructionKind.Literal)
        {
            if (!Rune.TryCreate(instruction.A, out var literal)) return false;
            AddOrderedRegularStarts(literal, instruction.B != 0, behavior, starts);
            return true;
        }

        if (GetUnfoldedUnicodeLeadRange(program, instruction, budget, out var last) is >= 0 and var first)
        {
            for (var starter = first; starter <= last; starter++)
                AddStartByte((byte)starter, starts);
            return true;
        }

        if (TryGetPositiveCategorySet(program, instruction, out _, out var low, out var high))
        {
            // Keep all multibyte leads for scalar category verification.
            starts[0] |= low;
            starts[1] |= high;
            starts[3] |= Utf8AsciiBytePredicates.DecimalOrScalarLead.Mask3;
            return true;
        }

        if (instruction is not { Kind: Utf8SliceInstructionKind.Set, B: 0 }) return false;
        var mask = program.SetMasks[instruction.A];
        budget.ChargeWork((ulong)mask.Length);
        for (var index = 0; index < mask.Length; index++)
            for (var bits = (uint)mask[index]; bits != 0; bits &= bits - 1)
            {
                budget.ChargeWork(1);
                var scalar = new Rune((index << 3) + BitOperations.TrailingZeroCount(bits));
                AddOrderedRegularStarts(scalar, instruction.C != 0, behavior, starts);
            }
        return true;
    }

    private static PreparedByteSearch BuildRegularStartSearch(ReadOnlySpan<ulong> starts)
    {
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

    private static int GetOrderedRegularRowCapacity(Utf8SliceProgram program)
        => Math.Max(9, program.NonBacktrackingOrderedLeaves.Length - 1);

    private static ulong GetOrderedRegularRowBytes(int capacity)
        => (ulong)(2 * capacity * Unsafe.SizeOf<int>());

    private static bool CanAdmitOrderedRows(Utf8SliceProgram program, ref Utf8SliceInvocation invocation)
    {
        ref var ledger = ref invocation.Ledger;
        if (ledger.HasMemoryAllowance && !program.IsNonBacktracking && program.RepeatCount == 0) return false;
        var cap = invocation.Budget.Limits.MaxWorkingBytes;
        if (cap == 0) return true;
        // Both rows are required. The range transition table is optional.
        // Retain the caller's existing route under a tighter numeric cap.
        var bytes = GetOrderedRegularRowBytes(GetOrderedRegularRowCapacity(program));
        return bytes <= cap && ledger.ReservedBytes <= cap - bytes;
    }

    private const int OrderedCountFail = -1;
    private const int OrderedCountEmpty = -2;

    private static Utf8SliceCountOutcome CountOrderedRegular(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int fromByte, ref Utf8SliceInvocation invocation)
    {
        var seek = TrySeekInitialRegularStart(program, input, ref fromByte, ref invocation);
        if (seek != Utf8ExecutionLimitKind.None || fromByte < 0) return new(0, seek);
        var reverse = program.RightToLeft;
        var leaves = program.NonBacktrackingOrderedLeaves;
        if (program.RepeatCount != 0 && !program.IsNonBacktracking)
        {
            var depth = invocation.Budget.TryNoteDepth((uint)(leaves.Length - 1));
            if (depth != Utf8ExecutionLimitKind.None) return new(0, depth);
        }
        var capacity = GetOrderedRegularRowCapacity(program);
        var bytes = GetOrderedRegularRowBytes(capacity);
        var breach = invocation.TryReserveWorking(bytes);
        if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
        try
        {
            Span<int> row = stackalloc int[capacity];
            Span<int> following = stackalloc int[capacity];
            var closures = program.NonBacktrackingOrderedClosures;
            var ancestors = program.NonBacktrackingEpsilonAncestors;
            uint acceptLeaves = 0;
            for (var tag = 1; tag < leaves.Length; tag++)
            {
                if (program.Instructions[leaves[tag]].Kind != Utf8SliceInstructionKind.Accept) continue;
                acceptLeaves |= 1U << tag;
                row[tag - 1] = following[tag - 1] = OrderedCountEmpty;
            }
            ulong followingViable = 0;
            uint followingLeaves = 0;
            var edge = reverse ? 0 : input.Length;
            var followingCount = 0;
            var scalar = default(Rune);
            while (true)
            {
                // One quantum covers a row of at most fifteen leaves. Large
                // class tables keep their own predicate work and checkpoints.
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
                var viable = program.NonBacktrackingAcceptClosure;
                var viableLeaves = acceptLeaves;
                // The following row's viable instructions admit only atoms
                // whose continuations can finish. Leaf bits mark valid counts.
                var candidates = (followingViable >> 1) & program.NonBacktrackingConsumerMask;
                for (; candidates != 0; candidates &= candidates - 1)
                {
                    var pc = BitOperations.TrailingZeroCount(candidates);
                    var instruction = program.Instructions[pc];
                    var hit = MatchesRegularAtom(program, instruction, scalar, ref invocation, out breach);
                    if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
                    if (!hit) continue;
                    var tag = (int)closures[pc];
                    var outcome = FirstOrderedCount(closures[pc + 1], followingLeaves, following);
                    // A consuming edge ending at an empty acceptance uses
                    // the search count at that endpoint.
                    row[tag - 1] = outcome == OrderedCountEmpty ? followingCount : outcome;
                    viableLeaves |= 1U << tag;
                    viable |= ancestors[pc];
                }
                // Conditions appear after their epsilon dependencies.
                foreach (var pc in program.NonBacktrackingEpsilonOrder)
                {
                    if ((viable & (1UL << (pc + 1))) == 0) continue;
                    var instruction = program.Instructions[pc];
                    if (!(instruction.Kind == Utf8SliceInstructionKind.Anchor
                        ? HoldsNonBacktrackingAnchor(instruction, input, edge)
                        : MatchesBoundary(program, instruction, input, edge)))
                        continue;
                    var tag = (int)closures[pc];
                    row[tag - 1] = FirstOrderedCount(closures[pc + 1], viableLeaves, row);
                    viableLeaves |= 1U << tag;
                    viable |= ancestors[pc];
                }
                var match = FirstOrderedCount(closures[0], viableLeaves, row);
                var count = CountFromOrderedOutcome(match, followingCount);
                if (edge == fromByte)
                {
                    invocation.Budget.ThrowIfCancellationRequested();
                    invocation.Budget.CheckDeadline();
                    return new(count, Utf8ExecutionLimitKind.None);
                }
                if (!TryReadScalar(input, edge, !reverse, out scalar, out var width))
                    throw new InvalidOperationException("Ordered traversal requires a complete scalar.");
                edge += reverse ? width : -width;
                followingViable = viable;
                followingLeaves = viableLeaves;
                followingCount = count;
                var previous = following;
                following = row;
                row = previous;
            }
        }
        finally { invocation.Ledger.Release(bytes); }
    }

    private static Utf8SliceCountOutcome CountWideNonBacktracking(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int fromByte, ref Utf8SliceInvocation invocation)
    {
        var seek = TrySeekInitialRegularStart(program, input, ref fromByte, ref invocation);
        if (seek != Utf8ExecutionLimitKind.None || fromByte < 0) return new(0, seek);
        var length = program.Instructions.Length;
        var epsilon = GetWideRegularEpsilonOrder(program);
        var maskLength = program.NonBacktrackingWideMasks.Length;
        var wordCount = maskLength / 2;
        var useStack = length <= 256;
        var bytes = useStack
            ? GetOrderedRegularRowBytes(length) + (ulong)(maskLength * Unsafe.SizeOf<ulong>())
            : checked(2 * Utf8ConstructionBudget.ArrayBytes<int>(length)
                + Utf8ConstructionBudget.ArrayBytes<ulong>(maskLength));
        var admission = invocation.TryReserveWorking(bytes);
        if (admission != Utf8ExecutionLimitKind.None) return new(0, admission);

        try
        {
            Span<int> row = useStack ? stackalloc int[length] : new int[length];
            Span<int> following = useStack ? stackalloc int[length] : new int[length];
            Span<ulong> masks = useStack ? stackalloc ulong[maskLength] : new ulong[maskLength];
            var viable = masks[..wordCount];
            var followingViable = masks[wordCount..];
            var accepts = program.NonBacktrackingWideMasks.AsSpan(wordCount);
            for (var word = 0; word < accepts.Length; word++)
            {
                var step = invocation.TryChargeInputStep();
                if (step != Utf8ExecutionLimitKind.None) return new(0, step);
                for (var bits = accepts[word]; bits != 0; bits &= bits - 1)
                {
                    var pc = (word << 6) + BitOperations.TrailingZeroCount(bits);
                    row[pc] = following[pc] = OrderedCountEmpty;
                }
            }

            var edge = input.Length;
            var scalar = default(Rune);
            var followingCount = 0;
            while (true)
            {
                var hasBefore = TryReadScalar(input, edge, true, out var before, out var width);
                var breach = TryBuildWideRegularRow(program, epsilon, input, edge, scalar, before,
                    edge == input.Length ? default : followingViable, viable, ref invocation, row, following, followingCount);
                if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);

                var count = CountFromOrderedOutcome((viable[0] & 1) == 0 ? OrderedCountFail : row[0], followingCount);
                if (edge == fromByte)
                {
                    invocation.Budget.ThrowIfCancellationRequested();
                    invocation.Budget.CheckDeadline();
                    return new(count, Utf8ExecutionLimitKind.None);
                }
                if (!hasBefore) throw new InvalidOperationException("Ordered traversal requires a complete scalar.");
                edge -= width;
                scalar = before;
                followingCount = count;
                var previous = following;
                following = row;
                row = previous;
                var previousViable = followingViable;
                followingViable = viable;
                viable = previousViable;
            }
        }
        finally { invocation.Ledger.Release(bytes); }
    }

    internal static ulong[] PrepareWideRegularMasks(Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        var instructions = program.Instructions.AsSpan();
        var order = program.NonBacktrackingEpsilonOrder.AsSpan();
        var leafCount = FindWideRegularLeafCount(instructions, order);
        var wordCount = (instructions.Length - 1) / 64 + 1;
        var masks = budget.AllocateArray<ulong>(2 * wordCount, false);
        // Each word records the consuming leaves and the unconditional accepts.
        for (var index = 0; index < leafCount; index++)
        {
            if ((index & 63) == 0) budget.ChargeWork((ulong)Math.Min(64, leafCount - index));
            var pc = order[index];
            var kind = instructions[pc].Kind;
            if (kind == Utf8SliceInstructionKind.Accept)
                masks[wordCount + (pc >> 6)] |= 1UL << (pc & 63);
            else if (kind != Utf8SliceInstructionKind.Fail)
                masks[pc >> 6] |= 1UL << (pc & 63);
        }
        return masks;
    }

    internal static ReadOnlySpan<int> GetWideRegularEpsilonOrder(Utf8SliceProgram program)
    {
        var order = program.NonBacktrackingEpsilonOrder.AsSpan();
        return order[FindWideRegularLeafCount(program.Instructions, order)..];
    }

    // Capture tables keep every viability row. Count also keeps suffix counts
    // for the chosen successors, using the same viability and priority rules.
    internal static Utf8ExecutionLimitKind TryBuildWideRegularRow(Utf8SliceProgram program,
        ReadOnlySpan<int> epsilon, ReadOnlySpan<byte> input, int edge, Rune scalar, Rune before,
        ReadOnlySpan<ulong> followingViable, Span<ulong> viable, ref Utf8SliceInvocation invocation,
        Span<int> row = default, ReadOnlySpan<int> following = default, int followingCount = 0)
    {
        var instructions = program.Instructions;
        var wordCount = viable.Length;
        var masks = program.NonBacktrackingWideMasks.AsSpan();
        var consumers = masks[..wordCount];
        var accepts = masks[wordCount..];
        var cellsLeft = 0;
        for (var word = 0; word < wordCount; word++)
        {
            ulong successors = 0;
            if (!followingViable.IsEmpty)
            {
                successors = followingViable[word] >> 1;
                if (word + 1 < wordCount) successors |= followingViable[word + 1] << 63;
            }
            var candidates = successors & consumers[word];
            // Charge a group of at most 64 feasible atoms, mask
            // words or epsilon operations. Tables meter their scans.
            var cells = Math.Max(1, BitOperations.PopCount(candidates));
            if (cells > cellsLeft)
            {
                var step = invocation.TryChargeInputStep();
                if (step != Utf8ExecutionLimitKind.None) return step;
                cellsLeft = 64;
            }
            cellsLeft -= cells;
            var live = accepts[word];
            for (; candidates != 0; candidates &= candidates - 1)
            {
                var bit = BitOperations.TrailingZeroCount(candidates);
                var pc = (word << 6) + bit;
                var hit = MatchesRegularAtom(program, instructions[pc], scalar, ref invocation, out var breach);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!hit) continue;
                if (!row.IsEmpty)
                {
                    var outcome = following[pc + 1];
                    row[pc] = outcome == OrderedCountEmpty ? followingCount : outcome;
                }
                live |= 1UL << bit;
            }
            viable[word] = live;
        }

        for (var index = 0; index < epsilon.Length; index++)
        {
            if (cellsLeft == 0)
            {
                var step = invocation.TryChargeInputStep();
                if (step != Utf8ExecutionLimitKind.None) return step;
                cellsLeft = 64;
            }
            cellsLeft--;
            var pc = epsilon[index];
            ref readonly var instruction = ref instructions[pc];
            var successor = pc + 1;
            if (instruction.Kind == Utf8SliceInstructionKind.Fork)
            {
                var preferred = instruction.B == 0 ? pc + 1 : instruction.A;
                var other = instruction.B == 0 ? instruction.A : pc + 1;
                successor = IsWideRegularViable(viable, preferred) ? preferred : other;
            }
            else if (instruction.Kind == Utf8SliceInstructionKind.Jump) successor = instruction.A;
            if (!IsWideRegularViable(viable, successor)) continue;
            if (instruction.Kind == Utf8SliceInstructionKind.Anchor && !HoldsNonBacktrackingAnchor(instruction, input, edge))
                continue;
            if (instruction.Kind == Utf8SliceInstructionKind.Boundary)
            {
                var beforeWord = edge > 0 && Utf8SliceScalarClasses.IsBoundaryWord(before, program, instruction);
                var afterWord = edge < input.Length && Utf8SliceScalarClasses.IsBoundaryWord(scalar, program, instruction);
                if ((beforeWord != afterWord) != (instruction.A != 0)) continue;
            }
            if (!row.IsEmpty) row[pc] = row[successor];
            viable[pc >> 6] |= 1UL << (pc & 63);
        }
        return Utf8ExecutionLimitKind.None;
    }

    private static int FindWideRegularLeafCount(ReadOnlySpan<Utf8SliceInstruction> instructions, ReadOnlySpan<int> order)
    {
        var start = 0;
        var end = order.Length;
        while (start < end)
        {
            var middle = start + (end - start) / 2;
            if (Utf8NonBacktrackingSearchState.IsEpsilonInstruction(instructions[order[middle]].Kind)) end = middle;
            else start = middle + 1;
        }
        return start;
    }

    private static bool IsWideRegularViable(ReadOnlySpan<ulong> viable, int pc)
        => (viable[pc >> 6] & (1UL << (pc & 63))) != 0;

    internal static Utf8ExecutionLimitKind TrySeekInitialRegularStart(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, ref int fromByte, ref Utf8SliceInvocation invocation)
    {
        var starts = program.NonBacktrackingOrderedStartBytes;
        var reverse = program.RightToLeft;
        if (starts.Count == 0 || IsPossibleRegularStart(input, fromByte, starts, reverse))
            return Utf8ExecutionLimitKind.None;
        var breach = reverse
            ? TrySeekReverseRegularStart(input, ref fromByte, starts, ref invocation)
            : TrySeekRegularStart(input, ref fromByte, starts, ref invocation);
        if (breach == Utf8ExecutionLimitKind.None && fromByte == (reverse ? 0 : input.Length))
        {
            invocation.Budget.ThrowIfCancellationRequested();
            invocation.Budget.CheckDeadline();
            fromByte = -1;
            return invocation.Budget.LimitBreach;
        }
        return breach;
    }

    private static int CountFromOrderedOutcome(int outcome, int followingCount) => outcome switch
    {
        OrderedCountFail => followingCount,
        OrderedCountEmpty => followingCount + 1,
        _ => outcome + 1,
    };

    private static int FirstOrderedCount(ulong tags, uint viableLeaves, ReadOnlySpan<int> row)
    {
        for (; tags != 0; tags >>= OrderedTagBits)
        {
            var tag = (int)(tags & OrderedTagMask);
            if ((viableLeaves & (1U << tag)) != 0) return row[tag - 1];
        }
        return OrderedCountFail;
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
        var originCapacity = GetOrderedRegularRowCapacity(program);
        var originBytes = GetOrderedRegularRowBytes(originCapacity);
        var cacheBytes = (ulong)(capacity * Unsafe.SizeOf<OrderedRegularTransition>());
        var limit = invocation.Budget.Limits.MaxWorkingBytes;
        var total = originBytes + cacheBytes;
        var reverse = program.RightToLeft;
        var remaining = reverse ? fromByte : input.Length - fromByte;
        var cache = remaining >= 64 &&
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
            var starts = remaining >= 64 ? program.NonBacktrackingOrderedStartBytes : default;
            var finalEdge = reverse ? 0 : input.Length;
            var conditionMask = program.NonBacktrackingOrderedConditions;
            var current = new OrderedRegularFrontier();
            var bestStart = -1;
            var bestEnd = -1;
            var position = fromByte;
            while (true)
            {
                if (current.Count == 0 && bestStart < 0 && starts.Count != 0 && position != finalEdge &&
                    !IsPossibleRegularStart(input, position, starts, reverse))
                {
                    breach = reverse
                        ? TrySeekReverseRegularStart(input, ref position, starts, ref invocation)
                        : TrySeekRegularStart(input, ref position, starts, ref invocation);
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

                var hasScalar = TryReadScalar(input, position, reverse, out var scalar, out var width);
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
                    return reverse
                        ? new(bestStart >= 0, bestEnd, bestStart, null, null, Utf8ExecutionLimitKind.None)
                        : new(bestStart >= 0, bestStart, bestEnd, null, null, Utf8ExecutionLimitKind.None);
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
                position += reverse ? -width : width;
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

    private static bool IsPossibleRegularStart(ReadOnlySpan<byte> input, int edge, in PreparedByteSearch starts, bool reverse)
    {
        if (!reverse) return edge < input.Length && starts.Contains(input[edge]);
        return TryGetScalarWidthEndingAt(input, edge, out var width) && starts.Contains(input[edge - width]);
    }

    private static Utf8ExecutionLimitKind TrySeekReverseRegularStart(ReadOnlySpan<byte> input, ref int edge,
        in PreparedByteSearch starts, ref Utf8SliceInvocation invocation)
    {
        while (edge > 0)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var blockStart = Math.Max(0, edge - 2048);
            var candidate = starts.LastIndexOf(input[blockStart..edge]);
            if (candidate >= 0)
            {
                var lead = blockStart + candidate;
                // A block can cut through the candidate scalar. Decode against
                // the subject and return its right edge for mirrored execution.
                if (!TryReadScalar(input, lead, false, out _, out var width))
                    throw new InvalidOperationException("Ordered traversal requires a complete scalar.");
                edge = lead + width;
                return Utf8ExecutionLimitKind.None;
            }
            edge = blockStart;
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
