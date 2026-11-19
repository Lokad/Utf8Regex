using System.Numerics;
using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    // Each nonzero seven-bit tag holds an instruction index plus 64. Nine
    // tags fit in a word, and each index has its own bit for deduplication.
    private const int OrderedLeafLimit = 9;
    private const int OrderedTagBits = 7;
    private const ulong OrderedTagMask = (1UL << OrderedTagBits) - 1;

    internal static ulong[] BuildOrderedRegularClosures(
        Utf8SliceInstruction[] instructions, ReadOnlySpan<int> epsilonOrder, Utf8ConstructionBudget budget)
    {
        if (instructions.Length > 64) return [];
        budget.ChargeWork((ulong)instructions.Length);
        var leaves = 0;
        foreach (var instruction in instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Anchor or Utf8SliceInstructionKind.Boundary:
                    return [];
                case Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Jump or
                    Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or Utf8SliceInstructionKind.Fail:
                    break;
                default:
                    if (++leaves > OrderedLeafLimit) return [];
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
                    budget.ChargeWork(2 * OrderedLeafLimit);
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
                    closures[pc] = (ulong)(pc + 64);
                    break;
            }
        }
        return closures;
    }

    internal static (byte First, byte Second, byte Third, bool Present) GetOrderedRegularStartBytes(
        Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        Span<byte> starts = stackalloc byte[3];
        var count = 0;
        var behavior = program.CaseBehavior;
        for (var tags = program.NonBacktrackingOrderedClosures[0]; tags != 0; tags >>= OrderedTagBits)
        {
            budget.ChargeWork(1);
            var instruction = program.Instructions[(int)tags & 63];
            if (instruction.Kind == Utf8SliceInstructionKind.Literal)
            {
                if (!Rune.TryCreate(instruction.A, out var literal) ||
                    !TryAddOrderedRegularStarts(literal, instruction.B != 0, behavior, starts, ref count)) return default;
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
                    var scalar = new Rune((index << 3) + BitOperations.TrailingZeroCount(bits));
                    if (!TryAddOrderedRegularStarts(scalar, instruction.C != 0, behavior, starts, ref count)) return default;
                }
        }
        return count == 0 ? default : (starts[0], starts[Math.Min(1, count - 1)], starts[count - 1], true);
    }

    private static bool TryAddOrderedRegularStarts(Rune scalar, bool folded, Utf8ScalarCaseBehavior behavior,
        Span<byte> starts, ref int count)
    {
        if (!TryAddOrderedRegularStartByte(scalar, starts, ref count)) return false;
        if (!folded) return true;
        if (!scalar.IsBmp)
        {
            // Supplementary equivalence uses simple lowercase. Its table
            // consists of reversible pairs contained in the supplementary planes.
            var lower = Utf8SupplementaryCase.Lower(scalar.Value);
            return TryAddOrderedRegularStartByte(new Rune(lower), starts, ref count) &&
                TryAddOrderedRegularStartByte(new Rune(Utf8SupplementaryCase.Upper(lower)), starts, ref count);
        }
        foreach (var equivalent in Utf8SliceCaseEquivalences.GetEquivalents((char)scalar.Value, behavior))
            if (!TryAddOrderedRegularStartByte(new Rune(equivalent), starts, ref count)) return false;
        return true;
    }

    private static bool TryAddOrderedRegularStartByte(Rune scalar, Span<byte> starts, ref int count)
    {
        Span<byte> encoded = stackalloc byte[4];
        _ = scalar.EncodeToUtf8(encoded);
        var first = encoded[0];
        if (starts[..count].Contains(first)) return true;
        if (count == starts.Length) return false;
        starts[count++] = first;
        return true;
    }

    internal static Utf8SliceSearchOutcome FindOrderedRegularRange(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int fromByte, ref Utf8SliceInvocation invocation)
    {
        const int capacity = 16;
        var originBytes = (ulong)(2 * OrderedLeafLimit * Unsafe.SizeOf<int>());
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
            Span<int> origins = stackalloc int[OrderedLeafLimit];
            Span<int> nextOrigins = stackalloc int[OrderedLeafLimit];
            Span<OrderedRegularTransition> table = stackalloc OrderedRegularTransition[cache ? capacity : 0];
            table.Clear();
            var closures = program.NonBacktrackingOrderedClosures;
            var starts = input.Length - fromByte >= 64 ? program.NonBacktrackingOrderedStartBytes : default;
            var current = new OrderedRegularFrontier();
            var bestStart = -1;
            var bestEnd = -1;
            var position = fromByte;
            while (true)
            {
                if (current.Count == 0 && bestStart < 0 && starts.Present && position < input.Length &&
                    !IsRegularStartByte(input[position], starts))
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
                        var pc = (int)tags & 63;
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

    internal static bool IsRegularStartByte(byte value, (byte First, byte Second, byte Third, bool Present) starts)
        => value == starts.First || value == starts.Second || value == starts.Third;

    internal static Utf8ExecutionLimitKind TrySeekRegularStart(ReadOnlySpan<byte> input, ref int position,
        (byte First, byte Second, byte Third, bool Present) starts, ref Utf8SliceInvocation invocation)
    {
        // A proved start byte cannot occur inside a scalar. Each caller has
        // established that there is no older path to advance over this gap.
        while (position < input.Length)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var block = input.Slice(position, Math.Min(2048, input.Length - position));
            var candidate = block.IndexOfAny(starts.First, starts.Second, starts.Third);
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

        // Unconditional edges make replay independent of the suffix. Clipping
        // after the winning end removes only continuations that lost to it.
        var replay = TryMatchAt(program, input[..selected.EndByte], selected.StartByte,
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
        internal ulong Seen;
        internal int Count;

        internal bool TryAppend(ulong tag)
        {
            var bit = 1UL << ((int)tag & 63);
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
