using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    private static bool TryFindExactFamilyContinuations(ReadOnlySpan<Utf8SliceInstruction> code, byte[][] literals,
        Span<int> continuations, out int commonContinuation)
    {
        // Prove that the discovery plan covers every arm of one leading fork
        // family. Only one exact arm may match at the same start.
        // The caller checks a leading boundary before entering the selected tail.
        var index = code[0].Kind == Utf8SliceInstructionKind.Boundary ? 1 : 0;
        commonContinuation = 0;
        var common = 0;
        var heads = default(Utf8SearchAsciiSet);
        for (var arm = 0; arm < literals.Length; arm++)
        {
            var literal = literals[arm];
            if (literal.Length is 0 or > 16 || literal[0] >= 128) return false;
            if (heads.Contains(literal[0]))
            {
                for (var earlier = 0; earlier < arm; earlier++)
                {
                    var alternate = literals[earlier];
                    var commonLength = Math.Min(literal.Length, alternate.Length);
                    if (literal.AsSpan(0, commonLength).SequenceEqual(alternate.AsSpan(0, commonLength)))
                        return false;
                }
            }
            heads = heads.With(literal[0]);

            var last = arm == literals.Length - 1;
            var next = code.Length;
            if (!last)
            {
                if ((uint)index >= (uint)code.Length ||
                    code[index++] is not { Kind: Utf8SliceInstructionKind.Fork, B: 0, C: 0, D: 0 } fork)
                    return false;
                next = fork.A;
            }
            if ((uint)next > (uint)code.Length || next - index <= literal.Length) return false;

            foreach (var value in literal)
                if (value >= 128 || code[index++] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0 } atom || atom.A != value)
                    return false;

            // Preserve the existing shared-tail proof for bare literal arms.
            var tail = !last && index == next - 1 &&
                code[index] is { Kind: Utf8SliceInstructionKind.Jump, B: 0, C: 0, D: 0 } jump &&
                jump.A > next && jump.A < code.Length ? jump.A : index;
            continuations[arm] = tail;
            if (arm == 0) common = tail;
            else if (common != tail) common = 0;
            index = next;
        }
        commonContinuation = common;
        return true;
    }

    private static (Utf8SearchAsciiSet Repeated, Utf8SearchAsciiSet Follower, int Minimum) GetExactPrefixRepeatHead(
        Utf8SliceProgram program, int index)
    {
        var code = program.Instructions.AsSpan();
        if (index == 0 || code.Length - index < 5 || !IsSingleClassRepeat(code, index) ||
            code[index] is not { RepeatMinimum: 0 or 1, RepeatMaximum: long.MaxValue } ||
            !program.TryGetUnfoldedAsciiSet(code[index + 4], out var follower, out var asciiOnly) || !asciiOnly)
            return default;

        var atom = code[index + 1];
        Utf8SearchAsciiSet repeated;
        if (atom is { Kind: Utf8SliceInstructionKind.Hybrid, B: 0, D: 0 })
        {
            // The unfolded hybrid mask includes all of its ASCII members.
            var mask = program.SetMasks[atom.A].AsSpan();
            repeated = Utf8SearchAsciiSet.FromMasks(BinaryPrimitives.ReadUInt64LittleEndian(mask),
                BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]));
        }
        else if (!program.TryGetUnfoldedAsciiSet(atom, out repeated, out _))
            return default;

        return ((repeated.Low & follower.Low) | (repeated.High & follower.High)) == 0
            ? (repeated, follower, (int)code[index].RepeatMinimum) : default;
    }

    // Keep the family proof's stack storage out of general first-match discovery.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8ExecutionLimitKind ScreenFirstExactFamilyFollower(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, byte[][] prefixes, ref int position, ref Utf8SliceInvocation invocation)
    {
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        // A Boolean search can finish immediately at a plausible first byte.
        // Keep its forward search before inspecting any later follower.
        var head = input[position];
        foreach (var prefix in prefixes)
            if (prefix is [var first, ..] && first == head) return Utf8ExecutionLimitKind.None;
        Span<int> continuations = stackalloc int[prefixes.Length];
        return TryFindExactFamilyContinuations(program.Instructions, prefixes, continuations, out var continuation) &&
            continuation != 0
            ? ScreenExactFamilyFollower(program, input, prefixes, continuation, ref position, ref invocation)
            : Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind ScreenExactFamilyFollower(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, byte[][] prefixes, int continuation,
        ref int position, ref Utf8SliceInvocation invocation)
    {
        var code = program.Instructions.AsSpan();
        var followerIndex = continuation + 4;
        if (followerIndex >= code.Length || !IsSingleClassRepeat(code, continuation) ||
            code[continuation].RepeatMinimum == 0 || !IsUnicodeSpace(program, code[continuation + 1]))
            return Utf8ExecutionLimitKind.None;

        Span<byte> storage = stackalloc byte[16];
        var follower = GetExactLeadingLiteral(program, followerIndex, storage);
        if (follower.IsEmpty || Utf8AsciiBytePredicates.IsSixByteWhitespace(follower[0]))
            return Utf8ExecutionLimitKind.None;
        var longest = 0;
        foreach (var prefix in prefixes)
        {
            if (Utf8AsciiBytePredicates.IsSixByteWhitespace(prefix[^1]))
                return Utf8ExecutionLimitKind.None;
            longest = Math.Max(longest, prefix.Length);
        }

        var floor = position;
        var suffix = floor;
        ref readonly var first = ref code[0];
        while (true)
        {
            var breach = TrySeekLeadingByte(input, ref suffix, follower[0], follower[0], false, ref invocation, follower);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (suffix < 0) { position = -1; return Utf8ExecutionLimitKind.None; }
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;

            // A bare prefix ends outside whitespace. Any such match must
            // end its prefix at the left edge of this run. Wider scalars and
            // runs that reach the probe bound leave the result undecided.
            var edge = suffix;
            var stop = Math.Max(floor, suffix - 64);
            while (edge > stop && Utf8AsciiBytePredicates.IsSixByteWhitespace(input[edge - 1])) edge--;
            if (edge > floor)
            {
                if (edge == stop || input[edge - 1] >= 128) return Utf8ExecutionLimitKind.None;
                if (edge != suffix)
                {
                    var beforeRun = input[floor..edge];
                    foreach (var prefix in prefixes)
                    {
                        if (!beforeRun.EndsWith(prefix) || first.Kind == Utf8SliceInstructionKind.Boundary &&
                            !MatchesBoundary(program, first, input, edge - prefix.Length)) continue;
                        // Later followers cannot rewind through this nonspace
                        // byte. Even their longest prefix cannot start earlier
                        // than this floor. Verify in the original forward order.
                        position = Math.Max(floor, edge - longest);
                        while (position > floor && (input[position] & 0xc0) == 0x80) position--;
                        return Utf8ExecutionLimitKind.None;
                    }
                }
            }
            suffix++;
        }
    }

    // The candidate step covers this bounded scan. Only a complete ASCII
    // run can prove the following scalar's unique start. Wider or longer runs
    // leave the existing scalar attempt in charge.
    private static int ScanExactPrefixRepeatHead(ReadOnlySpan<byte> input, Utf8SearchAsciiSet ascii,
        Utf8SearchAsciiSet follower, int minimum)
    {
        var length = Math.Min(16, input.Length);
        for (var offset = 0; offset < length; offset++)
        {
            var next = input[offset];
            if (next >= 128) return 0;
            if (!ascii.Contains(next)) return offset >= minimum && follower.Contains(next) ? offset + 1 : -1;
        }
        return length == input.Length ? -1 : 0;
    }

    // These entries leave the input edge unchanged when they succeed. The
    // scanner can reject an impossible consuming byte before the VM runs them.
    private static bool PrecedesRequiredScalar(Utf8SliceInstruction entry) =>
        entry.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or
            Utf8SliceInstructionKind.Anchor or Utf8SliceInstructionKind.Boundary or
            Utf8SliceInstructionKind.Lookahead or Utf8SliceInstructionKind.Lookbehind ||
        entry.Kind == Utf8SliceInstructionKind.RepeatGreedy && entry.RepeatMinimum > 0;

    // An unfolded literal or positive range can prove possible first bytes.
    // This bounded instruction proof is independent of the subject.
    internal static (int, byte, int, byte, int, bool, bool) FindLeadingByteCandidate(
        Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        var code = program.Instructions.AsSpan();
        var index = 0;
        while (index < code.Length && index < 64)
        {
            budget.ChargeWork(1);
            if (!PrecedesRequiredScalar(code[index])) break;
            index++;
        }
        if (index >= code.Length || index >= 64) return (-1, 0, -1, 0, -1, false, false);
        var firstScalarIndex = index;
        var first = GetAsciiStartEnvelope(program, code[index], budget, out var last, out var onlyEndpoints);
        if (first < 0)
        {
            if (GetUnfoldedUnicodeLeadRange(program, code[index], budget, out last) is >= 0 and var wide)
                return (wide, last, -1, 0, firstScalarIndex, false, false);
            if (code[index].Kind is not (Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.RepeatGreedy or
                Utf8SliceInstructionKind.RepeatLazy)) return (-1, 0, -1, 0, firstScalarIndex, false, false);
            var remaining = 64;
            first = GetBranchedStartBytes(program, index, budget, ref remaining, out last, out onlyEndpoints);
            return (first, last, -1, 0, firstScalarIndex, true, onlyEndpoints && last - first > 1);
        }
        var sparsePair = onlyEndpoints && last - first > 1;
        if (code[index].IsLiteralChoice) return (first, last, -1, 0, firstScalarIndex, true, sparsePair);
        // Only straight-line mandatory entries can precede the next scalar.
        // Repeat-end/exit, optional entries and forks stay opaque: the next
        // instruction in storage need not be the next consumed scalar.
        index++;
        while (index < code.Length && index < 64)
        {
            budget.ChargeWork(1);
            if (!PrecedesRequiredScalar(code[index])) break;
            index++;
        }
        var second = -1;
        byte secondLast = 0;
        if (index < code.Length && index < 64)
            second = GetUnfoldedAsciiRange(code[index], out secondLast);
        return (first, last, second, secondLast, firstScalarIndex, false, sparsePair);
    }

    private static int GetUnfoldedUnicodeLeadRange(Utf8SliceProgram program, Utf8SliceInstruction atom,
        Utf8ConstructionBudget budget, out byte last)
    {
        last = 0;
        if (atom is { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 128 } && Rune.IsValid(atom.A))
            return last = Utf8StarterByte(atom.A);
        if (atom is not { Kind: Utf8SliceInstructionKind.CategorySet, B: 0, C: 0 } ||
            program.CategorySetMembers[atom.D] is not [{ Negated: false, Table: { Mask: 0, ClassProgram: null,
                ScalarPredicate: null, Ranges: { Length: >= 2 and <= 32 } ranges } }] ||
            program.SetMasks[atom.A].AsSpan().ContainsAnyExcept((byte)0)) return -1;

        var first = byte.MaxValue;
        for (var index = 0; index < ranges.Length; index += 2)
        {
            budget.ChargeWork(1);
            var low = ranges[index];
            var high = ranges[index + 1] - 1;
            if (low < 128 || !Rune.IsValid(low) || !Rune.IsValid(high)) return -1;
            first = Math.Min(first, Utf8StarterByte(low));
            last = Math.Max(last, Utf8StarterByte(high));
        }
        // Every value in this interval is a UTF-8 starter. Holes in the
        // original scalar ranges still need the normal VM class check.
        return first;
    }

    private static byte Utf8StarterByte(int scalar) => (byte)(scalar < 0x80 ? scalar :
        scalar < 0x800 ? 0xc0 | scalar >> 6 : scalar < 0x10000 ? 0xe0 | scalar >> 12 : 0xf0 | scalar >> 18);

    // Every reachable arm must prove its first consuming byte. Stop on
    // unknown effects, an empty completion or backwards control flow. The
    // two arms share a fixed visit budget, including when paths reconverge.
    private static int GetBranchedStartBytes(Utf8SliceProgram program, int index, Utf8ConstructionBudget budget,
        ref int remaining, out byte last, out bool onlyEndpoints)
    {
        var code = program.Instructions.AsSpan();
        last = 0;
        onlyEndpoints = false;
        while ((uint)index < (uint)code.Length && remaining > 0)
        {
            budget.ChargeWork(1);
            --remaining;
            var entry = code[index];
            if (PrecedesRequiredScalar(entry)) { index++; continue; }
            if (entry.Kind == Utf8SliceInstructionKind.Jump && entry.A > index) { index = entry.A; continue; }

            int first, next;
            if (entry is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } && entry.A > index + 1)
            {
                first = GetBranchedStartBytes(program, index + 1, budget, ref remaining, out last, out onlyEndpoints);
                next = entry.A;
            }
            else if (entry.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy &&
                entry.RepeatMinimum == 0 && entry.RepeatMaximum == 1 && entry.A == index + 3 && entry.A < code.Length)
            {
                // The one-atom optional can consume its byte or skip directly
                // through the zero-width repeat exit into the continuation.
                first = GetStartRange(program, code[index + 1], budget, out last, out onlyEndpoints);
                next = entry.A + 1;
            }
            else return GetStartRange(program, entry, budget, out last, out onlyEndpoints);

            if (first < 0) return -1;
            var other = GetBranchedStartBytes(program, next, budget, ref remaining, out var otherLast, out var otherOnlyEndpoints);
            // A single interval joining ASCII and wide starters would also
            // include UTF-8 continuation bytes.
            if (other < 0 || (first < 128) != (other < 128)) { onlyEndpoints = false; return -1; }
            var low = Math.Min(first, other);
            var high = Math.Max(last, otherLast);
            // Two arm endpoints can be interior bytes of the combined interval.
            onlyEndpoints = onlyEndpoints && otherOnlyEndpoints &&
                (first == low || first == high) && (last == low || last == high) &&
                (other == low || other == high) && (otherLast == low || otherLast == high);
            last = high;
            return low;
        }
        return -1;
    }

    private static int GetStartRange(Utf8SliceProgram program, Utf8SliceInstruction atom,
        Utf8ConstructionBudget budget, out byte last, out bool onlyEndpoints)
        => GetAsciiStartEnvelope(program, atom, budget, out last, out onlyEndpoints) is >= 0 and var ascii ? ascii :
            GetUnfoldedUnicodeLeadRange(program, atom, budget, out last);

    private static int GetAsciiStartEnvelope(Utf8SliceProgram program, Utf8SliceInstruction atom,
        Utf8ConstructionBudget budget, out byte last, out bool onlyEndpoints)
    {
        var first = GetUnfoldedAsciiRange(atom, out last);
        onlyEndpoints = first >= 0 && last - first <= 1;
        if (first >= 0 || atom is not { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 }) return first;

        budget.ChargeWork(1);
        if (!program.TryGetUnfoldedAsciiSet(atom, out var set, out _) || !set.HasValue) return -1;
        onlyEndpoints = BitOperations.PopCount(set.Low) + BitOperations.PopCount(set.High) == 2;
        // The exact mask still decides whether a candidate in this interval matches.
        last = (byte)(set.High != 0 ? 127 - BitOperations.LeadingZeroCount(set.High)
            : 63 - BitOperations.LeadingZeroCount(set.Low));
        return set.Low != 0 ? BitOperations.TrailingZeroCount(set.Low)
            : 64 + BitOperations.TrailingZeroCount(set.High);
    }

    internal static int GetUnfoldedAsciiRange(Utf8SliceInstruction instruction, out byte last)
    {
        last = 0;
        if (instruction is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } literal && (uint)literal.A < 128)
        {
            last = (byte)literal.A;
            return literal.A;
        }
        if (instruction is not { Kind: Utf8SliceInstructionKind.Set, D: not 0 } set) return -1;
        // Common lowering proves the immutable mask once. The existing operand
        // stores this fact without growing instructions or per-cursor state.
        var first = set.AsciiIntervalStart;
        last = (byte)(first + set.AsciiIntervalWidth - 1);
        return first;
    }

    private static bool CanSkipShortRepeatChain(Utf8SliceProgram program)
    {
        var code = program.Instructions;
        if (code.Length is < 9 or > 64 || (code.Length - 5) % 4 != 0 ||
            code[0] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: >= 2 and <= 64 } outer ||
            outer.A != code.Length - 2 || code[^3] is not { Kind: Utf8SliceInstructionKind.RepeatEnd, A: 0 } ||
            code[^2] is not { Kind: Utf8SliceInstructionKind.RepeatExit } outerExit || outerExit.A != outer.B ||
            code[^1].Kind != Utf8SliceInstructionKind.Accept) return false;

        // The search already proved one required ASCII starter. Each other
        // atom excludes it and every other run's alphabet. Thus each complete
        // body has a unique usable boundary and contains no later body start.
        ulong seenLow = 0, seenHigh = 0;
        uint seenCategories = 0;
        if (!TryAddDisjointRepeatAtom(program, code[1], ref seenLow, ref seenHigh, ref seenCategories)) return false;
        for (var index = 2; index < code.Length - 3; index += 4)
        {
            if (code[index] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy } repeat || repeat.A != index + 3 ||
                code[index + 2] is not { Kind: Utf8SliceInstructionKind.RepeatEnd } loop || loop.A != index ||
                code[index + 3] is not { Kind: Utf8SliceInstructionKind.RepeatExit } exit || exit.A != repeat.B ||
                !TryAddDisjointRepeatAtom(program, code[index + 1], ref seenLow, ref seenHigh, ref seenCategories)) return false;
        }
        return true;
    }

    private static bool TryAddDisjointRepeatAtom(Utf8SliceProgram program, Utf8SliceInstruction atom,
        ref ulong seenLow, ref ulong seenHigh, ref uint seenCategories)
    {
        ulong low, high;
        uint categories = 0;
        if (atom is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } && (uint)atom.A < 128)
        {
            low = atom.A < 64 ? 1UL << atom.A : 0;
            high = atom.A >= 64 ? 1UL << (atom.A - 64) : 0;
        }
        else if (atom is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } && program.SetMasks[atom.A] is { Length: 16 } mask)
        {
            low = BinaryPrimitives.ReadUInt64LittleEndian(mask);
            high = BinaryPrimitives.ReadUInt64LittleEndian(mask.AsSpan(8));
        }
        else if (!TryGetPositiveCategorySet(program, atom, out categories, out low, out high)) return false;

        if ((low & seenLow) != 0 || (high & seenHigh) != 0 || (categories & seenCategories) != 0) return false;
        seenLow |= low;
        seenHigh |= high;
        seenCategories |= categories;
        return true;
    }

    private static Utf8ExecutionLimitKind CheckRepeatChain(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        byte first, byte last, ref int position, ref Utf8SliceInvocation invocation, out bool possible)
    {
        possible = false;
        var code = program.Instructions;
        var minimum = code[0].RepeatMinimum;
        var bodyEnd = code.Length - 3;
        var next = position;
        for (var piece = 0; piece < minimum; piece++)
        {
            // The starter inspects one byte. The runs still poll ahead of
            // every buffered scan, and the Count loop polls before verifying
            // a survivor or publishing an exhausted search.
            var breach = ChargeRepeatBoundary(ref invocation, checkDeadline: false);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (next == input.Length || (uint)(input[next] - first) > (uint)(last - first))
            {
                position = next;
                return Utf8ExecutionLimitKind.None;
            }

            var afterStarter = ++next;
            for (var index = 2; index < bodyEnd; index += 4)
            {
                var run = RunClassRepeat(program, index, input, next, ref invocation, checkExitDeadline: false);
                if (run.Breach != Utf8ExecutionLimitKind.None) return run.Breach;
                if (run.EndIndex < 0)
                {
                    // This body also fails when entered on its own. Later
                    // starters remain available to the ordinary byte search.
                    position = afterStarter;
                    return Utf8ExecutionLimitKind.None;
                }
                next = run.EndIndex;
            }
        }
        possible = true;
        return Utf8ExecutionLimitKind.None;
    }

    internal static Utf8ExecutionLimitKind TrySeekAsciiCandidate(ReadOnlySpan<byte> input, ref int position,
        byte first, byte last, ref Utf8SliceInvocation invocation)
        => TrySeekLeadingByte(input, ref position, first, last, false, ref invocation);

    private static ReadOnlySpan<byte> GetExactLeadingLiteral(Utf8SliceProgram program, int index, Span<byte> storage)
    {
        // The start proof already skipped the mandatory zero-width prefix.
        // Only contiguous exact ASCII literals certify a fixed byte prefix.
        var code = program.Instructions.AsSpan(index);
        var length = 0;
        foreach (var atom in code[..Math.Min(code.Length, storage.Length)])
        {
            if (atom is not { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 }) break;
            storage[length++] = (byte)atom.A;
        }
        return length >= 3 ? storage[..length] : default;
    }

    private static Utf8ExecutionLimitKind TrySeekLeadingByte(ReadOnlySpan<byte> input, ref int position,
        byte first, byte last, bool decimalSet, ref Utf8SliceInvocation invocation,
        scoped ReadOnlySpan<byte> exactPrefix = default, bool sparsePair = false, bool includeKelvin = false)
    {
        if (decimalSet && exactPrefix.IsEmpty)
            return TrySeekDecimalStart(input, ref position, ref invocation);

        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            var relative = exactPrefix.IsEmpty ? IndexOfLeadingByte(input[position..], first, last, sparsePair, includeKelvin)
                : input[position..].IndexOf(exactPrefix);
            position = relative < 0 ? -1 : position + relative;
            return Utf8ExecutionLimitKind.None;
        }
        // Include literal lookahead inside each charged block. The next
        // block starts at the first byte that could not start a full prefix.
        var overlap = exactPrefix.IsEmpty ? 0 : exactPrefix.Length - 1;
        while (position < input.Length - overlap)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var length = Math.Min(2048, input.Length - position);
            var block = input.Slice(position, length);
            var relative = exactPrefix.IsEmpty
                ? IndexOfLeadingByte(block, first, last, sparsePair, includeKelvin) : block.IndexOf(exactPrefix);
            if (relative >= 0) { position += relative; return Utf8ExecutionLimitKind.None; }
            position += length - overlap;
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind TrySeekDecimalStart(ReadOnlySpan<byte> input, ref int position,
        ref Utf8SliceInvocation invocation)
    {
        var metered = !invocation.IsUnmetered || invocation.CheckpointHook is not null;
        while (position < input.Length)
        {
            if (metered)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
            }
            var end = metered ? position + Math.Min(2048, input.Length - position) : input.Length;
            while (position < end)
            {
                var next = Utf8AsciiBytePredicates.DecimalOrScalarLead.IndexOf(input[position..end]);
                if (next < 0) { position = end; break; }
                position += next;
                if (input[position] < 128) return Utf8ExecutionLimitKind.None;
                // A lead inside this block admits one scalar. Its last bytes
                // may lie in the following block of the same subject.
                if (TryReadScalar(input, position, false, out var scalar, out var width) && Rune.IsDigit(scalar))
                    return Utf8ExecutionLimitKind.None;
                position += Math.Max(1, width);
                if (metered && position < end)
                {
                    // The current search step already covers checking its
                    // candidate. Charge again only before continuing.
                    var breach = invocation.TryChargeStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                }
            }
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    private static int IndexOfLeadingByte(ReadOnlySpan<byte> input, byte first, byte last, bool sparsePair,
        bool includeKelvin)
        => includeKelvin ? input.IndexOfAny(first, last, "K"u8[0])
            : sparsePair ? input.IndexOfAny(first, last)
            : first == last ? input.IndexOf(first) : input.IndexOfAnyInRange(first, last);

    private static bool MayStartAtByte(byte value, int first, byte last, bool sparsePair, bool includeKelvin)
        => sparsePair ? value == first || value == last || includeKelvin && value == "K"u8[0]
            : (uint)(value - first) <= (uint)(last - first);

    // Locate the tail after a proved whitespace repeat, optional initial class
    // run and bounded gap. Return the direct ASCII delimiter when present.
    // Verify every start in that window with the unchanged VM: captures,
    // references and assertions may make a later start win after an earlier
    // one fails. No candidate is itself accepted by this necessary condition.
    internal static int FindLeadingClassDelimiter(Utf8SliceProgram program,
        out int leadingClass, out int literalIndex, out long gapMaximum, out bool directClassLiteral)
    {
        leadingClass = -1;
        literalIndex = -1;
        gapMaximum = 0;
        directClassLiteral = false;
        var code = program.Instructions.AsSpan();
        var leadingBoundary = code is [{ Kind: Utf8SliceInstructionKind.Boundary, A: 1 }, ..];
        var index = leadingBoundary ? 1 : 0;
        while (index < code.Length && index < 64 && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            index++;
        // A leading boundary leaves the class at the original start. Only
        // short direct windows use this proof, and the VM still tests every
        // candidate's boundary in the full subject.
        if (leadingBoundary && (index >= 64 || !IsSingleClassRepeat(code, index) || code[index].RepeatMaximum > 3))
            return -1;
        var singleLeadingSpace = !leadingBoundary && index < code.Length && IsUnicodeSpace(program, code[index]);
        if (singleLeadingSpace)
        {
            index++;
            while (index < code.Length && index < 64 && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                index++;
            // Disjointness makes the whitespace before a bounded ASCII
            // run the unique possible start for a given literal position.
            const ulong asciiWhitespace = (0x1FUL << 9) | (1UL << ' ');
            if (index >= 64 || !IsSingleClassRepeat(code, index) || code[index].RepeatMaximum > 64 ||
                !program.TryGetUnfoldedAsciiSet(code[index + 1], out var ascii, out var asciiOnly) ||
                !asciiOnly || (ascii.Low & asciiWhitespace) != 0)
                return -1;
        }
        // One leading scalar-class run may precede the whitespace run. Reuse
        // the actual VM predicate when rewinding: ASCII intersections are not
        // a proof of Unicode word/category membership.
        var lazyDot = !singleLeadingSpace && index < 64 && index < code.Length &&
            code[index] is { Kind: Utf8SliceInstructionKind.RepeatLazy, RepeatMinimum: 0,
                RepeatMaximum: long.MaxValue } lazy && IsSingleAtomRepeat(code, index, lazy) &&
            code[index + 1].Kind == Utf8SliceInstructionKind.Dot;
        if (lazyDot || IsSingleClassRepeat(code, index) && (singleLeadingSpace || code[index].RepeatMinimum > 0))
        {
            leadingClass = index + 1;
            index += 4;
            while (index < code.Length && index < 64 && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                index++;
        }
        // Capture markers do not consume input. The scalar-class run can
        // still meet its delimiter directly when they sit between the two.
        // A positive inline assertion also requires its exact first byte
        // at this edge. Keep its literal index inside the assertion so the
        // window cannot verify a consuming prefix through the fork.
        if (leadingClass >= 0 && index < 63 && index < code.Length - 1 &&
            code[index] is { Kind: Utf8SliceInstructionKind.Fork, B: 1, C: > 1, D: 1 })
            index++;
        if (leadingClass >= 0 && index < 64 && index < code.Length &&
            code[index] is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } direct &&
            (uint)direct.A < 128)
        {
            literalIndex = index;
            directClassLiteral = true;
            return direct.A;
        }
        if (leadingBoundary || singleLeadingSpace || lazyDot) return -1;
        if (index >= 64 || !IsSingleClassRepeat(code, index)) return -1;
        var repeat = code[index];
        if (repeat.RepeatMaximum != long.MaxValue ||
            repeat.RepeatMinimum > (leadingClass >= 0 ? 1 : 0)) return -1;
        if (!IsUnicodeSpace(program, code[index + 1])) return -1;
        index += 4;
        while (index < code.Length && index < 64 && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            index++;
        if (leadingClass >= 0 && index < 64 && IsSingleClassRepeat(code, index) &&
            code[index].RepeatMaximum != long.MaxValue &&
            code[index + 1] is { Kind: Utf8SliceInstructionKind.Dot, A: 1 })
        {
            gapMaximum = code[index].RepeatMaximum;
            index += 4;
            while (index < code.Length && index < 64 && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                index++;
        }
        if (index >= code.Length || index >= 64) return -1;
        literalIndex = index;
        if (code[index].Kind != Utf8SliceInstructionKind.Literal || code[index].B != 0 ||
            (uint)code[index].A >= 128 || Rune.IsWhiteSpace(new Rune(code[index].A)))
            return -1;
        return code[index].A;
    }

    private static bool IsUnicodeSpace(Utf8SliceProgram program, in Utf8SliceInstruction instruction) =>
        instruction is { Kind: Utf8SliceInstructionKind.Shorthand, A: (int)Utf8SliceShorthand.Space } ||
        instruction is { Kind: Utf8SliceInstructionKind.Hybrid, B: 0, C: 1 << (int)Utf8SliceShorthand.Space, D: 0 } &&
        ContainsOnlyAsciiWhitespace(program.SetMasks[instruction.A]);

    private static bool IsSingleClassRepeat(ReadOnlySpan<Utf8SliceInstruction> code, int index)
        => code.Length - index >= 4 && code[index].Kind == Utf8SliceInstructionKind.RepeatGreedy &&
            code[index].A == index + 3 &&
            code[index + 1].Kind is (Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.Set or
                Utf8SliceInstructionKind.Shorthand or Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.CategorySet) &&
            code[index + 2].Kind == Utf8SliceInstructionKind.RepeatEnd && code[index + 2].A == index &&
            code[index + 3].Kind == Utf8SliceInstructionKind.RepeatExit && code[index + 3].A == code[index].B;

    private static bool LiteralStartsAtInstruction(Utf8SliceProgram program, int index, ReadOnlySpan<byte> needle)
    {
        // A required fragment elsewhere in the tail is not the literal at the
        // end of these runs. Check alignment before borrowing the prepared search.
        if (needle.IsEmpty || needle.Length > 64 || program.Instructions.Length - index < needle.Length) return false;
        for (var i = 0; i < needle.Length; i++)
        {
            var instruction = program.Instructions[index + i];
            if (needle[i] >= 128 || instruction.Kind != Utf8SliceInstructionKind.Literal ||
                instruction.B != 0 || instruction.A != needle[i]) return false;
        }
        return true;
    }

    private static PreparedSubstringSearch? GetAlignedDelimiterSearch(Utf8SliceProgram program, int index, int literal,
        in Utf8SearchPlan candidatePlan)
    {
        if (candidatePlan.LiteralSearch is { IgnoreCase: false } search &&
            (search.Length == 1 ? search.Needle[0] == literal : LiteralStartsAtInstruction(program, index, search.Needle)))
            return search;

        ref readonly var required = ref candidatePlan.RequiredPrefilterSearcher;
        return index > 0 && required.Kind == PreparedSearcherKind.ExactLiteral &&
            (program.LeadingClassWindowProof is { CanFinishWhitespace: true } window &&
                window.LiteralLength == required.LiteralSearch.Length ||
             program.Instructions[index - 1] is { Kind: Utf8SliceInstructionKind.Fork, B: 1, C: > 1, D: 1 }) &&
            LiteralStartsAtInstruction(program, index, required.LiteralSearch.Needle)
            ? required.LiteralSearch : null;
    }

    private static (int First, int Second) GetRequiredHybridSuffix(ReadOnlySpan<Utf8SliceInstruction> code, int index)
    {
        // The first class may already have been reduced to one scalar.
        // Either layout requires two scalars before the suffix can finish.
        var repeated = IsSingleClassRepeat(code, index) && code[index].RepeatMinimum != 0;
        var first = repeated ? index + 1 : index;
        var next = repeated ? index + 4 : index + 1;
        if (!IsSingleClassRepeat(code, next) || code[next].RepeatMinimum == 0 ||
            code[first] is not { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 } ||
            code[next + 1] is not { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 }) return default;
        return (first, next + 1);
    }

    internal static Utf8SliceClassWindowProof PrepareDirectClassWindow(Utf8SliceProgram program, int leadingClass,
        int literalIndex, byte literal, Utf8ConstructionBudget budget)
    {
        var code = program.Instructions;
        var capturedWhitespaceRun = leadingClass == 3 && literalIndex == 6 && program.GroupCount == 1 &&
            code.Length >= 10 && code[1] is { Kind: Utf8SliceInstructionKind.CaptureStart, A: 0, B: 0 } &&
            code[^3] is { Kind: Utf8SliceInstructionKind.CaptureEnd, A: 0, B: 0 };
        var captures = capturedWhitespaceRun ? 1 : 0;
        var literalLength = code.Length - literalIndex - 2 - captures;
        if (leadingClass == 2 + captures && literalIndex == 5 + captures && program.GroupCount == captures &&
            literalLength is >= 1 and <= 64 && IsUnicodeSpace(program, code[0]) &&
            code[^1].Kind == Utf8SliceInstructionKind.Accept &&
            code[^2] is { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 } or
                { Kind: Utf8SliceInstructionKind.Set, C: 0 } or { Kind: Utf8SliceInstructionKind.Shorthand } &&
            program.TryGetUnfoldedAsciiSet(code[leadingClass], out var repeated, out var asciiOnly) && asciiOnly)
        {
            budget.ChargeWork((ulong)(code.Length - literalIndex));
            foreach (var literalInstruction in code.AsSpan(literalIndex, literalLength))
                if (literalInstruction is not { Kind: Utf8SliceInstructionKind.Literal, A: >= 0 and < 128, B: 0 } ||
                    !repeated.Contains((byte)literalInstruction.A))
                    return default;

            // A follower outside this alphabet also excludes every byte of
            // the literal. No later greedy endpoint can reach across it.
            // Callers that observe the capture still verify the found start.
            return new((byte)literalLength, 0, 0, false, false, false)
                { Completion = Utf8SliceClassWindowCompletion.Whitespace };
        }
        if (program.Assertions.Length != 0 ||
            code[leadingClass - 1].RepeatMaximum != long.MaxValue)
            return default;

        var capturesLeadingRun = leadingClass == 2 && literalIndex == 6 &&
            code[0] is { Kind: Utf8SliceInstructionKind.CaptureStart } open &&
            code[5] is { Kind: Utf8SliceInstructionKind.CaptureEnd } close &&
            open.A == close.A && open.B == close.B;
        var requiredRun = code[leadingClass - 1] is { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 1 };
        if (program.ReadsCaptures && (!capturesLeadingRun || !requiredRun)) return default;

        budget.ChargeWork(1);
        ref readonly var atom = ref code[leadingClass];
        // These masks include the ASCII members of unfolded shorthands.
        // Category tables and folded classes retain their invocation path.
        bool? excludes = atom switch
        {
            { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 } or { Kind: Utf8SliceInstructionKind.Set, C: 0 }
                => ((program.SetMasks[atom.A][literal >> 3] & (1 << (literal & 7))) != 0) == (atom.B != 0),
            { Kind: Utf8SliceInstructionKind.CategorySet } when program.GetCompleteAsciiCategoryMask(atom) != 0
                => !MatchesAsciiHybridClass(program.SetMasks[atom.A], false, literal),
            { Kind: Utf8SliceInstructionKind.Dot } => atom.A == 0 && literal == '\n',
            { Kind: Utf8SliceInstructionKind.Shorthand } =>
                !Utf8SliceScalarClasses.MatchesShorthand(new Rune(literal), (Utf8SliceShorthand)atom.A),
            _ => null,
        };
        if (program.ReadsCaptures && excludes != true) return default;
        if (excludes == false) return new(0, 0, 0, false, false, false);

        // The complete literal width locates the suffix even when the
        // aligned search needle covers only part of the delimiter.
        var index = literalIndex + 1;
        for (; index < code.Length && index - literalIndex < 64; index++)
        {
            budget.ChargeWork(1);
            if (code[index] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 })
                break;
        }
        if (program.ReadsCaptures)
        {
            // The excluded delimiter fixes where this nonempty capture ends.
            // Compare the first byte of its ordinal reference before trying
            // starts in the run. Keep single-attempt and suffix reuse disabled.
            return index < code.Length && code[index] is { Kind: Utf8SliceInstructionKind.Backref, B: 0 } reference &&
                reference.A == code[0].A
                ? new((byte)(index - literalIndex), 0, 0, false, false, null)
                : default;
        }

        budget.ChargeWork(12);
        var (first, second) = GetRequiredHybridSuffix(code, index);
        // A value-only call can also skip the capture markers around the
        // proved run. The initial guard excludes programs that read them.
        var canReuse = (leadingClass == 1 && literalIndex == 4 && program.GroupCount == 0 || capturesLeadingRun) &&
            requiredRun;
        var canAccept = false;
        if (canReuse && !capturesLeadingRun && second != 0 && second == first + 2 && code[second - 1].RepeatMinimum == 1)
        {
            budget.ChargeWork(1);
            canAccept = code[second + 2].RepeatExitAcceptsEmptyTail;
            if (!canAccept)
            {
                budget.ChargeWork((ulong)Math.Min(code.Length - second - 3, 32));
                canAccept = CanAcceptEmptyTail(code, second + 3);
            }
        }
        var completion = Utf8SliceClassWindowCompletion.None;
        if (canReuse && excludes == true && capturesLeadingRun && program.HasSingleCapturePerGroup &&
            program.GroupCount == 2 && code.Length - index == 7)
        {
            budget.ChargeWork(7);
            // The full delimiter leaves exactly one capture around a greedy class
            // repeat. Accept observes only its maximal extent and both captures.
            if (code[index] is { Kind: Utf8SliceInstructionKind.CaptureStart, A: var slot } &&
                code[^2] is { Kind: Utf8SliceInstructionKind.CaptureEnd, A: var endSlot } &&
                slot == endSlot && slot != code[0].A && code[^1].Kind == Utf8SliceInstructionKind.Accept &&
                IsSingleClassRepeat(code, index + 1))
                completion = Utf8SliceClassWindowCompletion.CapturedRepeat;
        }
        else if (canReuse && excludes == true && capturesLeadingRun && program.HasSingleCapturePerGroup &&
            program.GroupCount == 3 && code.Length - index == 14)
        {
            budget.ChargeWork(14);
            if (code.AsSpan(index) is
                [
                    { Kind: Utf8SliceInstructionKind.CaptureStart, A: var openingSlot },
                    _, _, _, { RepeatExitSharesTerminalRun: true },
                    { Kind: Utf8SliceInstructionKind.CaptureEnd, A: var closingSlot },
                    { Kind: Utf8SliceInstructionKind.Literal, A: >= 0 and < 128, B: 0 },
                    { Kind: Utf8SliceInstructionKind.CaptureStart, A: var suffixOpening },
                    _, _, _, _,
                    { Kind: Utf8SliceInstructionKind.CaptureEnd, A: var suffixClosing },
                    { Kind: Utf8SliceInstructionKind.Accept }
                ] && openingSlot == closingSlot && suffixOpening == suffixClosing &&
                IsSingleClassRepeat(code, index + 1) && IsSingleClassRepeat(code, index + 8))
                completion = Utf8SliceClassWindowCompletion.CapturedSharedRun;
        }
        else if (canReuse && !capturesLeadingRun && excludes == true && code.Length - index == 10)
        {
            budget.ChargeWork(10);
            if (code.AsSpan(index) is
            [
                { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 1, RepeatMaximum: long.MaxValue, HasSingleAtomBody: true },
                var firstAtom, _, { RepeatExitRejectsShorterEndpoints: true } or { RepeatExitSharesTerminalRun: true },
                { Kind: Utf8SliceInstructionKind.Literal, A: >= 0 and < 128, B: 0 },
                { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 1, RepeatMaximum: long.MaxValue, HasSingleAtomBody: true },
                var secondAtom, _, _, { Kind: Utf8SliceInstructionKind.Accept }
            ] && program.GetCompleteAsciiCategoryMask(firstAtom) != 0 && program.GetCompleteAsciiCategoryMask(secondAtom) != 0)
                completion = Utf8SliceClassWindowCompletion.TerminalRuns;
        }
        // The delimiter starts within 64 instructions. The exact needle and
        // these two single-class nodes keep both offsets within a byte.
        return new((byte)(index - literalIndex), checked((byte)first), checked((byte)second),
            canReuse, canAccept, excludes) { Completion = completion };
    }

    private static Utf8SliceMatchOutcome MatchTerminalCapturedRepeat(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int start, int captureInstruction, scoped Span<int> captureStarts,
        scoped Span<int> captureEnds, ref Utf8SliceInvocation invocation, bool sharesRun)
    {
        var repeated = RunClassRepeat(program, captureInstruction + 1, input, start, ref invocation);
        if (repeated.EndIndex < 0 || repeated.Breach != Utf8ExecutionLimitKind.None)
            return new(false, -1, repeated.Breach);

        var firstEnd = repeated.EndIndex;
        var code = program.Instructions;
        if (sharesRun)
        {
            // An ASCII separator is itself a scalar. Interior byte positions
            // leave a full scalar on both sides even when either edge is wide.
            var seekBreach = FindPreviousRepeatStart(input, start + 1, repeated.EndIndex - 1,
                code[captureInstruction + 4].RepeatExitRewindStarts, ref invocation, out firstEnd);
            if (firstEnd < 0 || seekBreach != Utf8ExecutionLimitKind.None)
                return new(false, -1, seekBreach);
        }
        var breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, breach);

        // A truncated caller buffer can omit a capture. The leading
        // capture is published by candidate search after this tail succeeds.
        var slotCount = Math.Min(captureStarts.Length, captureEnds.Length);
        var slot = code[captureInstruction].A;
        if ((uint)slot < (uint)slotCount)
        {
            captureStarts[slot] = start;
            captureEnds[slot] = firstEnd;
        }
        if (sharesRun && (uint)code[captureInstruction + 7].A < (uint)slotCount)
        {
            var suffixSlot = code[captureInstruction + 7].A;
            captureStarts[suffixSlot] = firstEnd + 1;
            captureEnds[suffixSlot] = repeated.EndIndex;
        }
        return new(true, repeated.EndIndex, Utf8ExecutionLimitKind.None);
    }

    internal static int PrepareRequiredLiteralContinuation(Utf8SliceProgram program, ReadOnlySpan<byte> needle,
        Utf8ConstructionBudget budget)
    {
        if (program.IsNonBacktracking || program.IgnoreCase || needle.Length is < 1 or > 64) return 0;
        budget.ChargeWork(1);
        var proof = program.LeadingClassWindowProof;
        var alignedWindow = proof is { CanReusePrefix: true, ExcludesDelimiter: true } &&
            needle.Length == proof.LiteralLength;
        var literal = alignedWindow ? program.LeadingClassDelimiter.LiteralIndex : 0;
        var continuation = literal + needle.Length;
        if (alignedWindow && !proof.CanAcceptAfterPair)
        {
            // The prepared window cannot read captures. Look through its
            // markers for the suffix shape; the verifier still meters them.
            var code = program.Instructions;
            var visits = Math.Clamp(code.Length - continuation, 0, Utf8SliceInstruction.LeadingCaptureMarkerLimit);
            budget.ChargeWork((ulong)visits);
            if (!CanProveRepeatPrefix(program, Utf8SliceInstruction.SkipLeadingCaptureMarkers(code, continuation))) return 0;
        }
        else if (!alignedWindow &&
            (program.GroupCount != 0 || !CanProveRepeatPrefix(program, continuation) &&
                !CanProveOptionalLiteralThenHybrid(program.Instructions, continuation, budget))) return 0;
        budget.ChargeWork((ulong)needle.Length);
        return LiteralStartsAtInstruction(program, literal, needle) ? continuation : 0;
    }

    internal static byte[]? PrepareRequiredLiteralTerminalSuffix(Utf8SliceProgram program, int index,
        Utf8ConstructionBudget budget)
    {
        var code = program.Instructions;
        var suffixStart = index + 4;
        var length = code.Length - suffixStart - 1;
        if (length is < 1 or > 16 || program.GroupCount != 0 || code[^1].Kind != Utf8SliceInstructionKind.Accept)
            return null;
        budget.ChargeWork((ulong)(length * 2 + 3));
        if (code[index] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: > 0,
                RepeatMaximum: long.MaxValue, HasSingleAtomBody: true } repeat ||
            repeat.A != suffixStart - 1 || !code[repeat.A].RepeatExitRejectsShorterEndpoints)
            return null;
        var literals = code.AsSpan(suffixStart, length);
        foreach (var literal in literals)
            if (literal is not { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 })
                return null;
        var suffix = budget.AllocateArray<byte>(length, false);
        for (var offset = 0; offset < suffix.Length; offset++)
            suffix[offset] = (byte)literals[offset].A;
        return suffix;
    }

    private static bool CanProveOptionalLiteralThenHybrid(ReadOnlySpan<Utf8SliceInstruction> code, int index,
        Utf8ConstructionBudget? budget = null)
    {
        if ((uint)index >= (uint)code.Length ||
            code[index] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 0, RepeatMaximum: 1 } optional ||
            !IsSingleAtomRepeat(code, index, optional) ||
            code[index + 1] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 }) return false;

        for (var next = optional.A + 1; next < code.Length && next - optional.A <= 8; next++)
        {
            budget?.ChargeWork(1);
            var entry = code[next];
            if (entry is { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 }) continue;
            return entry is { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 1 } &&
                IsSingleAtomRepeat(code, next, entry) && code[entry.A].RepeatExitAcceptsEmptyTail &&
                code[next + 1] is { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 };
        }
        return false;
    }

    private static bool CanProveRepeatPrefix(Utf8SliceProgram program, int index)
        => (uint)index < (uint)program.Instructions.Length &&
            program.Instructions[index] is
                { Kind: Utf8SliceInstructionKind.RepeatGreedy, HasSingleAtomBody: true, RepeatMaximum: > 4 } repeat &&
            (program.Instructions[repeat.A].RepeatExitRejectsShorterEndpoints ||
                program.Instructions[repeat.A].RepeatExitSharesTerminalRun ||
                program.Instructions[repeat.A].RepeatExitAcceptsEmptyTail) &&
            (program.Instructions[index + 1] is
                { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } or { Kind: Utf8SliceInstructionKind.Hybrid, B: 0, D: 0 } ||
                program.GetCompleteAsciiCategoryMask(program.Instructions[index + 1]) != 0);

    private static byte[]? GetGuardedRepeatLookbehind(Utf8SliceProgram program, in Utf8SliceInstruction first)
        => !program.RightToLeft && first is { Kind: Utf8SliceInstructionKind.Lookbehind, B: 1 } &&
            program.Assertions[first.A] is { ForwardScalarCount: null,
                InlineLiteralPrefixLength: Utf8SliceInstruction.InlineClassAssertion, Body.RightToLeft: true } assertion &&
            assertion.Body.Instructions[0] is { Kind: Utf8SliceInstructionKind.Hybrid, B: 0, D: 0 } guard &&
            CanProveRepeatPrefix(program, 2)
                ? assertion.Body.SetMasks[guard.A] : null;

    private static bool TryGetGuardedRepeatLookahead(Utf8SliceProgram program, out Utf8SliceInstruction follower)
    {
        follower = default;
        var code = program.Instructions;
        ref readonly var repeat = ref code[2];
        if (code[1] is not { Kind: Utf8SliceInstructionKind.Set, D: not 0 } ||
            !code[repeat.A].RepeatExitRejectsShorterEndpoints ||
            code[repeat.A + 1] is not { Kind: Utf8SliceInstructionKind.Fork, B: 1,
                C: Utf8SliceInstruction.InlineClassAssertion } lookahead ||
            code[lookahead.A + lookahead.D].Kind != Utf8SliceInstructionKind.Accept ||
            code[repeat.A + 2] is not { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 })
            return false;
        follower = lookahead;
        return true;
    }

    private static bool MayMatchGuardedRepeat(Utf8SliceProgram program, ReadOnlySpan<byte> input, int position,
        out int acceptedEnd)
    {
        acceptedEnd = -1;
        return !TryGetGuardedRepeatLookahead(program, out var lookahead) ||
            MayMatchGuardedRepeat(program, input, position, lookahead, out acceptedEnd);
    }

    private static bool MayMatchGuardedRepeat(Utf8SliceProgram program, ReadOnlySpan<byte> input, int position,
        in Utf8SliceInstruction lookahead, out int acceptedEnd)
    {
        acceptedEnd = -1;
        var code = program.Instructions;
        ref readonly var repeat = ref code[2];
        ref readonly var head = ref code[1];
        ref readonly var guard = ref code[repeat.A + 2];
        if (position == input.Length || !head.MatchesAsciiInterval(input[position])) return false;

        var remaining = BranchedPrefixInstructionLimit - 5;
        var end = TryMatchLongPrefixRepeat(program, input, position + 1, 3, repeat, ref remaining);
        if (end < 0) return end != BranchedPrefixRejected;
        if (end < input.Length && input[end] >= 128) return true;

        var matched = end < input.Length && MatchesAsciiHybridClass(program.SetMasks[guard.A], guard.B != 0, input[end]);
        if (matched != (lookahead.D != 0)) return false;
        acceptedEnd = end;
        return true;
    }

    private static bool TryFindGuardedRepeat(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        ref int position, ref Utf8SliceInvocation invocation, out Utf8SliceSearchOutcome outcome)
    {
        outcome = default;
        if (invocation.Budget.Limits.MaxDepth is > 0 and < BranchedPrefixInstructionLimit ||
            !Utf8BytePosition.IsScalarBoundary(input, position) ||
            GetGuardedRepeatLookbehind(program, program.Instructions[0]) is not { } lookbehind ||
            !TryGetGuardedRepeatLookahead(program, out var lookahead))
            return false;

        ref readonly var head = ref program.Instructions[1];
        var first = (byte)head.AsciiIntervalStart;
        var last = (byte)(first + head.AsciiIntervalWidth - 1);
        while (position < input.Length)
        {
            if (!head.MatchesAsciiInterval(input[position]))
            {
                var search = TrySeekAsciiCandidate(input, ref position, first, last, ref invocation);
                if (search != Utf8ExecutionLimitKind.None)
                {
                    outcome = new(false, -1, -1, search);
                    return true;
                }
                if (position < 0) break;
            }

            // Retain the original subject and this exact candidate for the
            // general verifier when the preceding scalar is wider than ASCII.
            if (position > 0 && input[position - 1] >= 128) return false;
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None)
            {
                outcome = new(false, -1, -1, breach);
                return true;
            }
            if (position > 0 && MatchesAsciiHybridClass(lookbehind, false, input[position - 1]) &&
                MayMatchGuardedRepeat(program, input, position, lookahead, out var end))
            {
                if (end < 0) return false;
                breach = invocation.TryChargeInputStep();
                invocation.Budget.ThrowIfCancellationRequested();
                outcome = breach == Utf8ExecutionLimitKind.None
                    ? new(true, position, end, breach) : new(false, -1, -1, breach);
                return true;
            }
            position++;
        }
        outcome = new(false, -1, -1, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
        invocation.Budget.ThrowIfCancellationRequested();
        return true;
    }

    internal static bool PrepareAlignedDelimiterAlternatives(Utf8SliceProgram program,
        in PreparedSearcher search, Utf8ConstructionBudget budget)
    {
        if (program.LeadingClassDelimiter is not { LeadingClass: >= 0, Literal: < 0, LiteralIndex: >= 0 and var index } ||
            search.ExactMultiLiterals is not { Length: >= 2 and <= 8 } literals ||
            program.Instructions[index].Kind != Utf8SliceInstructionKind.Fork) return false;
        budget.ChargeWork(1);
        foreach (var literal in literals)
            if (literal.Length is 0 or > 64) return false;

        // Each arm must begin with a prepared needle. Required literals farther
        // into a branch do not identify the end of the leading class window.
        var remaining = 16;
        return CoversAllStarts(index);

        bool CoversAllStarts(int start)
        {
            var code = program.Instructions;
            while (remaining-- > 0 && (uint)start < (uint)code.Length)
            {
                var instruction = code[start];
                if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                {
                    start++;
                    continue;
                }
                if (instruction is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } && instruction.A > start + 1)
                    return CoversAllStarts(start + 1) && CoversAllStarts(instruction.A);
                foreach (var literal in literals)
                    if (LiteralStartsAtInstruction(program, start, literal)) return true;
                return false;
            }
            return false;
        }
    }

    private static Utf8ExecutionLimitKind TrySeedOrderedClassFamily(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, in Utf8SearchPlan candidatePlan, ref int position, ref Utf8SliceInvocation invocation,
        out int acceptedEnd, out (int SpaceInstruction, int FirstEnd, int LiteralStart) provedPrefix)
    {
        acceptedEnd = -1;
        provedPrefix = default;
        if (!HasOrderedClassFamilyPrefix(program, candidatePlan))
            return Utf8ExecutionLimitKind.None;

        var prefix = program.LeadingClassDelimiter;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;

        var floor = position;
        var scan = position;
        while (true)
        {
            breach = candidatePlan.RequiredPrefilterSearcher.TryFindMatch(input, scan, input.Length, false,
                ref invocation, out var hit, out var found);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            position = found ? hit.Index : -1;
            if (!found) return Utf8ExecutionLimitKind.None;

            breach = TryProveOrderedClassFamily(program, input, floor, position, ref invocation,
                out var earliest, out acceptedEnd, out provedPrefix, out var rejected);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (rejected)
            {
                // Every arm needs the missing run at this ASCII delimiter.
                // Later arms may overlap it or rewind through it to the floor.
                scan = position + 1;
                continue;
            }
            if (earliest >= 0)
            {
                position = earliest;
                return Utf8ExecutionLimitKind.None;
            }
            // An unresolved prefix needs the existing conservative position.
            return TryRewindClassWhitespace(program, input, floor, prefix.LeadingClass, 0, false,
                ref position, ref invocation);
        }
    }

    private static bool HasOrderedClassFamilyPrefix(Utf8SliceProgram program, in Utf8SearchPlan candidatePlan)
        => ReferenceEquals(candidatePlan.AlignedClassDelimiterProgram, program) &&
            program.LeadingClassDelimiter is { Literal: < 0, LeadingClass: > 0, GapMaximum: 0, LiteralIndex: >= 0 } prefix &&
            program.Instructions[prefix.LeadingClass].Kind != Utf8SliceInstructionKind.Dot;

    private static Utf8SliceMatchOutcome ReplayOrderedFamilyPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int start, scoped Span<int> captureStarts, scoped Span<int> captureEnds,
        (int SpaceInstruction, int FirstEnd, int LiteralStart) provedPrefix, ref AttemptWorkspace workspace,
        ref Utf8SliceInvocation invocation)
    {
        // A single opening before the first four-instruction run closes
        // immediately before the second. Resume at the first tail instruction,
        // including capture markers. The tail cannot observe the omitted slot.
        var code = program.Instructions;
        if (program.LeadingClassDelimiter.LeadingClass != 2 || provedPrefix.SpaceInstruction != 6 ||
            code[0] is not { Kind: Utf8SliceInstructionKind.CaptureStart, A: var slot } ||
            code[5] is not { Kind: Utf8SliceInstructionKind.CaptureEnd, A: var close } || close != slot)
            return TryMatchAt(program, input, start, captureStarts, captureEnds, false, false, true,
                ref workspace, ref invocation);

        var replay = TryMatchAt(program, input, start, captureStarts, captureEnds, false, false, true,
            ref workspace, ref invocation, verifiedPrefix: (provedPrefix.SpaceInstruction + 4, provedPrefix.LiteralStart - start));
        if (replay.IsMatch && (uint)slot < (uint)captureStarts.Length)
        {
            captureStarts[slot] = start;
            captureEnds[slot] = provedPrefix.FirstEnd;
        }
        return replay;
    }

    internal static Utf8ExecutionLimitKind TryProveScreenedOrderedClassFamily(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, in Utf8SearchPlan candidatePlan, int floor, int screenedLiteral,
        ref Utf8SliceInvocation invocation, out bool found)
    {
        found = false;
        if (screenedLiteral < floor || invocation.Budget.Limits.MaxDepth is > 0 and < BranchedPrefixInstructionLimit ||
            !HasOrderedClassFamilyPrefix(program, candidatePlan))
            return Utf8ExecutionLimitKind.None;

        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;

        breach = TryProveOrderedClassFamily(program, input, floor, screenedLiteral, ref invocation, out _, out var end, out _, out _);
        if (breach != Utf8ExecutionLimitKind.None || end < 0) return breach;
        breach = invocation.TryReserveWorking(0);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        invocation.Budget.CheckDeadline();
        found = true;
        return Utf8ExecutionLimitKind.None;
    }

    internal static Utf8ExecutionLimitKind TryProveScreenedTerminalRepeatPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, in Utf8SearchPlan candidatePlan, int floor, int screenedLiteral,
        ref Utf8SliceInvocation invocation, out bool found)
    {
        found = false;
        if (screenedLiteral <= floor || invocation.Budget.Limits.MaxDepth is > 0 and < BranchedPrefixInstructionLimit ||
            candidatePlan.GetRepeatPrefixSearch(program, out var prefix) is not { } search ||
            !ReferenceEquals(search.Needle, candidatePlan.RequiredPrefilterSearcher.LiteralSearch.Needle) ||
            !Utf8BytePosition.IsScalarBoundary(input, floor) ||
            input[screenedLiteral - 1] is < 128 and var previous && !prefix.Last.Contains(previous))
            return Utf8ExecutionLimitKind.None;

        // The screened literal is the complete tail. Only the disjoint
        // repeats to its left need verification for a Boolean result.
        var breach = TryNarrowDisjointRepeatPrefix(program, input, floor, screenedLiteral, prefix.DisjointEnd,
            ref invocation, out var start, out _, out _);
        if (breach != Utf8ExecutionLimitKind.None || start < 0) return breach;
        breach = invocation.TryReserveWorking(0);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        invocation.Budget.CheckDeadline();
        found = true;
        return Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind TryProveOrderedClassFamily(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int floor, int position, ref Utf8SliceInvocation invocation,
        out int earliest, out int acceptedEnd, out (int SpaceInstruction, int FirstEnd, int LiteralStart) provedPrefix,
        out bool rejected)
    {
        earliest = acceptedEnd = -1;
        provedPrefix = default;
        rejected = false;
        var prefix = program.LeadingClassDelimiter;
        var code = program.Instructions.AsSpan();
        if (program.NonBacktrackingOrderedConditions != 0 ||
            invocation.Budget.Limits.MaxDepth is > 0 and < BranchedPrefixInstructionLimit ||
            code[prefix.LeadingClass - 1] is not { RepeatMinimum: 1, RepeatMaximum: long.MaxValue } ||
            input[position] >= 128 || Rune.IsWhiteSpace(new Rune(input[position])))
            return Utf8ExecutionLimitKind.None;

        var space = Utf8SliceInstruction.SkipLeadingCaptureMarkers(code, prefix.LeadingClass + 3);
        if (space > prefix.LiteralIndex - 4 ||
            code[space] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 1, RepeatMaximum: long.MaxValue } ||
            Utf8SliceInstruction.SkipLeadingCaptureMarkers(code, space + 4) != prefix.LiteralIndex ||
            !AreDisjointPositiveCategorySets(program.SetMasks, program.CategorySetMembers,
                code[prefix.LeadingClass], code[space + 1]))
            return Utf8ExecutionLimitKind.None;

        var breach = TryNarrowDisjointRepeatPrefix(program, input, floor, position, prefix.LiteralIndex,
            ref invocation, out earliest, out var firstEnd, out rejected);
        if (breach != Utf8ExecutionLimitKind.None || earliest < 0) return breach;

        // Both nonempty runs end at the screened literal. The suffix must
        // also succeed before this proof can replace state traversal.
        MayMatchBranchedAsciiPrefix(program, input, position, prefix.LiteralIndex, out acceptedEnd,
            proveMatch: true, BranchedPrefixInstructionLimit - prefix.LiteralIndex);
        if (acceptedEnd < 0) return Utf8ExecutionLimitKind.None;
        provedPrefix = (space, firstEnd, position);
        return invocation.TryChargeInputStep();
    }

    internal static (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd, bool Terminal)
        PrepareRepeatPrefix(Utf8SliceProgram program, ReadOnlySpan<byte> needle, Utf8ConstructionBudget budget)
    {
        var code = program.Instructions.AsSpan();
        budget.ChargeWork((ulong)(Math.Min(code.Length, 64) + needle.Length));
        ulong low = 0, high = 0;
        var last = default(Utf8SearchAsciiSet);
        var previousCategories = 0u;
        var disjoint = true;
        var terminal = program.GroupCount == 0;
        var repeats = 0;
        var index = 0;
        while (index < code.Length && index < 64)
        {
            // The verifier still checks boundaries on the original subject.
            if (code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or
                Utf8SliceInstructionKind.Boundary)
            {
                terminal = false;
                index++;
                continue;
            }
            if (!IsSingleClassRepeat(code, index)) break;
            var atom = code[index + 1];
            var categories = 0u;
            Utf8SearchAsciiSet ascii;
            if (atom.Kind != Utf8SliceInstructionKind.Set &&
                TryGetPositiveCategorySet(program, atom, out categories, out var atomLow, out var atomHigh))
                ascii = Utf8SearchAsciiSet.FromMasks(atomLow, atomHigh);
            else
            {
                if (!program.TryGetUnfoldedAsciiSet(atom, out ascii, out var asciiOnly)) return default;
                disjoint &= asciiOnly;
            }
            var required = code[index].RepeatMinimum > 0;
            terminal &= code[index].RepeatMinimum == 1;
            disjoint &= required && code[index].RepeatMaximum == long.MaxValue &&
                (last.Low & ascii.Low) == 0 && (last.High & ascii.High) == 0 &&
                (previousCategories & categories) == 0;
            low |= ascii.Low;
            high |= ascii.High;
            last = required ? ascii : default;
            previousCategories = categories;
            repeats++;
            index += 4;
        }

        if ((low | high) == 0 || (low & high) == ulong.MaxValue || index >= 64 ||
            !LiteralStartsAtInstruction(program, index, needle)) return default;
        var disjointEnd = disjoint && repeats >= 2 ? index : 0;
        // The completed rewind proves every minimum. An excluded literal
        // fixes the final greedy split, and Accept leaves no tail to replay.
        terminal &= disjointEnd != 0 && !last.Contains(needle[0]) &&
            code.Length - index == needle.Length + 1 && code[^1].Kind == Utf8SliceInstructionKind.Accept;
        return (Utf8SearchAsciiSet.FromMasks(low, high), last, disjointEnd, terminal);
    }

    private static Utf8ExecutionLimitKind TryProveSingleWindowAttempt(Utf8SliceProgram program, int leadingClass,
        byte delimiter, ref Utf8SliceInvocation invocation, out bool singleAttempt)
    {
        singleAttempt = false;
        if (program.LeadingClassWindowProof.ExcludesDelimiter is { } excluded)
        {
            var admission = invocation.TryChargeStep();
            singleAttempt = admission == Utf8ExecutionLimitKind.None && excluded;
            return admission;
        }
        if (program.ReadsCaptures || program.Assertions.Length != 0 ||
            program.Instructions[leadingClass - 1].RepeatMaximum != long.MaxValue)
            return Utf8ExecutionLimitKind.None;

        // An excluded delimiter fixes the suffix entry for every start in the
        // run. When matching cannot read captures, later starts have the
        // same suffix decisions and fewer scalars available for the minimum.
        var breach = invocation.TryChargeStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        breach = TryMatchScalarClass(program, program.Instructions[leadingClass], new Rune(delimiter),
            ref invocation, out var includesDelimiter);
        singleAttempt = breach == Utf8ExecutionLimitKind.None && !includesDelimiter;
        return breach;
    }

    private static Utf8ExecutionLimitKind TryRewindClassWhitespace(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int floor, int leadingClass, long gapMaximum, bool directClassLiteral,
        ref int position, ref Utf8SliceInvocation invocation)
    {
        var windowEnd = position;
        var code = program.Instructions;
        if (directClassLiteral &&
            code[leadingClass] is { Kind: Utf8SliceInstructionKind.Dot, A: not 0 } &&
            code[leadingClass - 1].RepeatMaximum == long.MaxValue)
        {
            position = floor;
            return invocation.TryChargeInputStep();
        }
        if (directClassLiteral && code[leadingClass - 1].RepeatMaximum == long.MaxValue &&
            (code[leadingClass - 1].RepeatMinimum == 1 || code[leadingClass].Kind == Utf8SliceInstructionKind.Dot) &&
            TryGetAsciiRepeatStops(program, code[leadingClass], out var first, out var second))
        {
            // Direct unbounded class runs start after the last excluded byte.
            // ASCII cannot occur inside a wider scalar in caller-valid UTF-8.
            // Keep literal discovery and VM verification: this is only a window.
            while (position > floor)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var length = Math.Min(2048, position - floor);
                var start = position - length;
                var block = input.Slice(start, length);
                var stop = first == second ? block.LastIndexOf(first) : block.LastIndexOfAny(first, second);
                if (stop >= 0) { position = start + stop + 1; break; }
                position = start;
            }
            return invocation.TryChargeInputStep();
        }
        // Move by scalar count, not bytes. If this lands inside a valid prefix,
        // it lies in its class/whitespace runs; their conservative rewind then
        // covers the earliest start. Landing before the prefix only adds starts.
        for (long remaining = gapMaximum; remaining > 0 && position > floor; remaining--)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            // This gap needs only scalar width. Membership reads below still
            // decode values.
            if (!TryGetScalarWidthEndingAt(input, position, out var width)) break;
            position -= width;
        }
        // Explicit whitespace can overlap the class: cover their complete union.
        // A direct class/literal prefix has no intervening whitespace. Its actual
        // class predicate below already includes any whitespace it can consume.
        while (!directClassLiteral && position > floor)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            // Admit a bounded block and finish any scalar crossing its edge.
            var blockFloor = Math.Max(floor, position - 64);
            while (position > blockFloor &&
                TryReadScalar(input, position, true, out var scalar, out var width) && Rune.IsWhiteSpace(scalar))
                position -= width;
            if (position > blockFloor) break;
        }
        var classStart = position;
        var classMaximum = leadingClass >= 0 ? program.Instructions[leadingClass - 1].RepeatMaximum : 0;
        var ascii = default(Utf8SearchAsciiSet);
        var hasAsciiMask = false;
        if (leadingClass >= 0 && (classMaximum == long.MaxValue || classMaximum <= 4))
        {
            if (program.LeadingScalarIsDecimal && leadingClass == program.LeadingByteCandidate.InstructionIndex)
            {
                if (directClassLiteral && gapMaximum == 0 && classMaximum <= 4 && code[leadingClass - 1].RepeatMinimum > 0 &&
                    windowEnd < input.Length && input[windowEnd] is < 128 and var delimiter &&
                    !Utf8AsciiBytePredicates.IsDigit(delimiter) &&
                    FindShortAsciiDecimalStart(input, floor, windowEnd, (int)classMaximum) is >= 0 and var asciiStart)
                {
                    var breach = invocation.TryChargeInputStep();
                    if (breach == Utf8ExecutionLimitKind.None)
                        position = asciiStart < windowEnd ? asciiStart : windowEnd + 1;
                    return breach;
                }
                ascii = Utf8SearchAsciiSet.FromMasks(Utf8AsciiBytePredicates.DecimalMask, 0);
                hasAsciiMask = true;
            }
            else hasAsciiMask = program.TryGetUnfoldedAsciiSet(code[leadingClass], out ascii, out _);
        }
        var categories = hasAsciiMask && classMaximum == long.MaxValue && position - floor >= 64
            ? program.GetCompleteAsciiCategoryMask(code[leadingClass]) : 0;
        if (categories != 0)
        {
            var breach = TryRewindCategoryRun(input, floor, categories, ascii, ref position, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
        }
        else
        {
            for (var remaining = classMaximum; remaining > 0 && position > floor; remaining--)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (hasAsciiMask && input[position - 1] < 128)
                {
                    // The exact ASCII predicate can rewind one charged block
                    // within the remaining scalar bound.
                    // Stop at the first wider scalar and decode it at its right edge.
                    var length = (int)Math.Min(remaining, Math.Min(64, position - floor));
                    var block = input.Slice(position - length, length);
                    var index = length - 1;
                    while (index >= 0 && ascii.Contains(block[index])) index--;
                    var consumed = length - 1 - index;
                    position -= consumed;
                    if (index < 0)
                    {
                        remaining -= consumed - 1;
                        continue;
                    }
                    if (block[index] < 128) break;
                    remaining -= consumed;
                }
                if (!TryReadScalar(input, position, true, out var scalar, out var width)) break;
                breach = TryMatchScalarClass(program, code[leadingClass], scalar, ref invocation, out var hit);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!hit) break;
                position -= width;
            }
        }
        // A backwards class step already proved membership at the new start.
        // Otherwise discard only the unproved prefix once per window; every
        // remaining viable start still enters complete VM verification.
        if (leadingClass >= 0 && position == classStart &&
            code[leadingClass].Kind != Utf8SliceInstructionKind.Dot)
        {
            while (position <= windowEnd)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!TryReadScalar(input, position, false, out var scalar, out var width)) break;
                breach = TryMatchScalarClass(program, code[leadingClass], scalar, ref invocation, out var hit);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (hit) break;
                position += width;
            }
        }
        return Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind TryRewindCategoryRun(ReadOnlySpan<byte> input, int floor, uint categories,
        Utf8SearchAsciiSet ascii, ref int position, ref Utf8SliceInvocation invocation)
    {
        while (position > floor)
        {
            // One charge admits 64 bytes and enough lookbehind to finish the
            // last scalar. Decoding never crosses the actual search floor.
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var blockFloor = Math.Max(floor, position - 64);
            var edge = position;
            do
            {
                var previous = input[edge - 1];
                if (previous < 128)
                {
                    if (!ascii.Contains(previous)) break;
                    edge--;
                    continue;
                }
                if (Rune.DecodeLastFromUtf8(input[floor..edge], out var scalar, out var width) != OperationStatus.Done ||
                    (categories & (1u << (int)Rune.GetUnicodeCategory(scalar))) == 0)
                    break;
                edge -= width;
            } while (edge > blockFloor);

            position = edge;
            if (edge > blockFloor) break;
        }
        return Utf8ExecutionLimitKind.None;
    }

    private static bool ContainsOnlyAsciiWhitespace(ReadOnlySpan<byte> mask)
    {
        for (var index = 0; index < mask.Length; index++)
        {
            // ASCII whitespace is 09..0D and 20. A subset is sufficient:
            // rewinding a wider whitespace run merely adds VM candidates.
            var allowed = index == 1 ? 0x3E : index == 4 ? 1 : 0;
            if ((mask[index] & ~allowed) != 0) return false;
        }
        return true;
    }

    private static Utf8ExecutionLimitKind TrySeekLeadingWhitespaceLiteral(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, ref int position, int literalIndex, ref Utf8SliceInvocation invocation, out int windowEnd)
    {
        var floor = position;
        var literal = (byte)program.Instructions[literalIndex].A;
        windowEnd = -1;
        while (position < input.Length)
        {
            var breach = TrySeekAsciiCandidate(input, ref position, literal, literal, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None || position < 0) return breach;
            if (MatchesStraightAsciiPrefix(program, input, position, literalIndex))
            {
                windowEnd = position;
                return TryRewindClassWhitespace(program, input, floor, -1, 0, false, ref position, ref invocation);
            }
            // The admitted first byte is ASCII, so the next byte is a scalar boundary.
            position++;
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind TrySeekDecimalLiteralWindow(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, byte literal, ref int position, ref Utf8SliceInvocation invocation, out int windowEnd,
        int verifiedNextDelimiter, bool verifyFollowingWindow, out int followingWindowEnd)
    {
        // The caller proved a boundary, decimal run and adjacent one-byte delimiter.
        var floor = position;
        var reuseDelimiter = verifiedNextDelimiter >= floor;
        followingWindowEnd = 0;
        if (!reuseDelimiter)
        {
            var breach = TrySeekAsciiCandidate(input, ref position, literal, literal, ref invocation);
            windowEnd = position;
            if (breach != Utf8ExecutionLimitKind.None || position < 0) return breach;
        }
        else
        {
            windowEnd = verifiedNextDelimiter;
            if (!invocation.IsUnmetered || invocation.CheckpointHook is not null)
            {
                var searchBreach = invocation.TryChargeInputStep();
                if (searchBreach != Utf8ExecutionLimitKind.None) return searchBreach;
            }
        }

        var asciiStart = -1;
        if (verifyFollowingWindow)
        {
            if (!reuseDelimiter && program.Instructions[1].RepeatMaximum is >= 1 and <= 4 and var maximum)
            {
                asciiStart = FindShortAsciiDecimalStart(input, floor, windowEnd, (int)maximum);
                if (asciiStart == windowEnd)
                {
                    // No decimal scalar can end at this delimiter within the search floor.
                    position = windowEnd + 1;
                    return Utf8ExecutionLimitKind.None;
                }
            }

            // Every start in this window must pass the same following run.
            followingWindowEnd = FindShortAsciiDecimalLiteralEnd(input, windowEnd + 1, program.FollowingDecimalLiteralWindow);
            if (followingWindowEnd < 0)
            {
                position = windowEnd + 1;
                return Utf8ExecutionLimitKind.None;
            }
        }

        if (!reuseDelimiter && asciiStart < 0)
            return TryRewindClassWhitespace(program, input, floor, leadingClass: 2, gapMaximum: 0, directClassLiteral: true,
                ref position, ref invocation);

        // A reused delimiter already has its decimal run classified.
        var rewindBreach = invocation.TryChargeInputStep();
        if (rewindBreach == Utf8ExecutionLimitKind.None)
            position = reuseDelimiter ? Math.Max(floor, windowEnd - (int)program.Instructions[1].RepeatMaximum) : asciiStart;
        return rewindBreach;
    }

    private static Utf8SliceCountOutcome CountAlignedDecimalWindows(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, byte delimiter, int delimiterInstruction,
        ref Utf8SliceInvocation invocation)
    {
        var workspace = new AttemptWorkspace();
        try
        {
            var outcome = ScanAlignedDecimalWindows(program, input, position, delimiter, delimiterInstruction,
                ref workspace, ref invocation, countAll: true, existenceOnly: false, out var count);
            return new(count, outcome.Breach);
        }
        finally { workspace.Release(ref invocation); }
    }

    private static Utf8SliceSearchOutcome ScanAlignedDecimalWindows(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, byte delimiter, int delimiterInstruction,
        ref AttemptWorkspace workspace, ref Utf8SliceInvocation invocation,
        bool countAll, bool existenceOnly, out int count)
    {
        count = 0;
        var nextDelimiter = -1;
        var continuation = program.FollowingDecimalContinuation;
        while (true)
        {
            var breach = TrySeekDecimalLiteralWindow(program, input, delimiter, ref position, ref invocation,
                out var windowEnd, nextDelimiter, verifyFollowingWindow: true, out var followingEnd);
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
            if (position < 0)
                return new(false, -1, -1, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
            nextDelimiter = -1;
            if (position >= windowEnd)
            {
                position = windowEnd + 1;
                continue;
            }

            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
            invocation.Budget.ThrowIfCancellationRequested();
            // Rewind proved a decimal scalar at the start. Its right side
            // is a Unicode word character, so only the preceding scalar matters.
            if (position != 0 && (input[position - 1] is < 128 and var previous
                ? Utf8AsciiBytePredicates.IsWord(previous)
                : TryReadScalar(input, position, true, out var scalar, out _) &&
                  Utf8SliceScalarClasses.IsWord(scalar, includeJoiners: true, ecma: false)))
            {
                // No interior start has a word boundary within a decimal run.
                position = windowEnd + 1;
                continue;
            }

            int provedEnd;
            bool possible;
            if (followingEnd > 0 && continuation.TerminalMax != 0)
                possible = (provedEnd = MatchTerminalDecimalBoundary(input, followingEnd,
                    continuation.TerminalMin, continuation.TerminalMax)) >= 0;
            else
            {
                var proofPosition = followingEnd > 0 ? followingEnd : windowEnd;
                var proofStart = followingEnd > 0 ? continuation.Index : delimiterInstruction;
                var proofLimit = followingEnd > 0
                    ? BranchedPrefixInstructionLimit - program.FollowingDecimalLiteralWindow.Max - 3
                    : BranchedPrefixInstructionLimit;
                possible = MayMatchBranchedAsciiPrefix(program, input, proofPosition, proofStart, out provedEnd,
                    proveMatch: true, instructionLimit: proofLimit);
            }
            if (!possible)
            {
                if (!countAll && followingEnd > 0 && program.FollowingDecimalLiteralWindow.Literal == delimiter)
                    nextDelimiter = followingEnd - 1;
                position = windowEnd + 1;
                continue;
            }

            var outcome = provedEnd >= 0
                ? new Utf8SliceMatchOutcome(true, provedEnd,
                    countAll ? invocation.TryReserveWorking(0) : invocation.TryChargeInputStep())
                : TryMatchAt(program, input, position, [], [], false, false, true, ref workspace, ref invocation, existenceOnly);
            if (outcome.Breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, outcome.Breach);
            invocation.Budget.ThrowIfCancellationRequested();
            if (!outcome.IsMatch) { position = windowEnd + 1; continue; }
            if (!countAll) return new(true, position, outcome.EndIndex, Utf8ExecutionLimitKind.None);
            count++;
            position = outcome.EndIndex;
            invocation.AnchorOriginByte = position;
        }
    }

    internal static int MatchBoundedDecimalRun(ReadOnlySpan<byte> input, int start, int minimum, int maximum)
    {
        var digits = input.Slice(start, Math.Min(maximum, input.Length - start));
        var stop = digits.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
        var count = stop < 0 ? digits.Length : stop;
        var end = start + count;
        if (stop >= 0 && digits[stop] >= 128)
            return MatchWideDecimalPrefix(input, end, Math.Max(0, minimum - count), maximum - count);
        return count < minimum ? BranchedPrefixRejected : end;
    }

    internal static bool TryGetFixedDecimalFollower(Utf8SliceProgram program, out int digits, out byte first, out byte last)
    {
        digits = first = last = 0;
        var code = program.Instructions.AsSpan();
        var atomIndex = program.LeadingByteCandidate.InstructionIndex;
        var repeatIndex = atomIndex - 1;
        if (Utf8SliceInstruction.SkipLeadingCaptureMarkers(code, 0) != repeatIndex ||
            !code[atomIndex].IsDecimalDigitSet || !IsSingleClassRepeat(code, repeatIndex)) return false;

        ref readonly var repeat = ref code[repeatIndex];
        if (repeat.RepeatMinimum != repeat.RepeatMaximum || repeat.RepeatMinimum is < 1 or > 4 || repeat.A + 1 >= code.Length ||
            !program.TryGetUnfoldedAsciiSet(code[repeat.A + 1], out var follower, out var asciiOnly) || !asciiOnly ||
            !TryGetAsciiBytePair(follower, out first, out last) ||
            Utf8AsciiBytePredicates.IsDigit(first) || Utf8AsciiBytePredicates.IsDigit(last)) return false;

        digits = (int)repeat.RepeatMinimum;
        return true;
    }

    private static Utf8SliceCountOutcome CountFromFixedDecimalFollowers(ReadOnlySpan<byte> input, int floor,
        int digits, byte first, byte last, Utf8BoundedProgramEmitter.Match emitted, ref Utf8SliceInvocation invocation)
    {
        var count = 0;
        while (TryFindFixedDecimalMatch(input, floor, digits, first, last, emitted, out _, out var end))
        {
            var breach = invocation.TryReserveWorking(0);
            if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
            count++;
            floor = end;
            invocation.AnchorOriginByte = floor;
        }
        return new(count, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
    }

    internal static bool TryFindFixedDecimalMatch(ReadOnlySpan<byte> input, int floor,
        int digits, byte first, byte last, Utf8BoundedProgramEmitter.Match emitted, out int start, out int end,
        Span<Utf8CaptureRange> captures = default)
    {
        while (input.Length - floor > digits)
        {
            // Fixed scalar width gives one increasing candidate start per follower.
            var search = floor + digits;
            var relative = IndexOfLeadingByte(input[search..], first, last,
                sparsePair: first != last, includeKelvin: false);
            if (relative < 0) break;
            var follower = search + relative;
            var candidate = follower - digits;
            var prefix = input.Slice(candidate, digits);
            var nonDigit = prefix.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
            if (nonDigit >= 0)
                candidate = prefix[nonDigit] < 128 ? -1 : FindFixedDecimalStart(input, floor, follower, digits);

            // No decimal prefix can cross this follower. After a false prefix,
            // skip runs of non-starters before seeking the next follower.
            floor = follower + 1;
            if (candidate < 0)
            {
                var next = Utf8AsciiBytePredicates.DecimalOrScalarLead.IndexOf(input[floor..]);
                if (next < 0) break;
                floor += next;
                continue;
            }
            var relativeEnd = emitted(input[candidate..], captures);
            if (relativeEnd < 0) continue;
            start = candidate;
            end = candidate + relativeEnd;
            return true;
        }
        start = end = -1;
        return false;
    }

    private static int FindFixedDecimalStart(ReadOnlySpan<byte> input, int floor, int edge, int digits)
    {
        for (var i = 0; i < digits; i++)
        {
            if (edge <= floor || !TryReadScalar(input, edge, true, out var scalar, out var width) || !Rune.IsDigit(scalar))
                return -1;
            edge -= width;
        }
        return edge >= floor ? edge : -1;
    }

    private static int MatchTerminalDecimalBoundary(ReadOnlySpan<byte> input, int start, int minimum, int maximum)
    {
        // The admitted continuation has at most four digits and one boundary.
        var end = MatchBoundedDecimalRun(input, start, minimum, maximum);
        if (end < 0) return end;
        // The required digit before the boundary is a Unicode word scalar.
        if (end == input.Length) return end;
        var next = input[end];
        var word = next < 128 ? Utf8AsciiBytePredicates.IsWord(next)
            : TryReadScalar(input, end, false, out var scalar, out _) &&
              Utf8SliceScalarClasses.IsWord(scalar, includeJoiners: true, ecma: false);
        return word ? BranchedPrefixRejected : end;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindShortAsciiDecimalStart(ReadOnlySpan<byte> input, int floor, int end, int maximum)
    {
        var start = end;
        var limit = Math.Max(floor, end - maximum);
        while (start > limit && Utf8AsciiBytePredicates.IsDigit(input[start - 1])) start--;
        // A possible wider digit at the left edge needs scalar rewind.
        return start == limit || input[start - 1] < 128 ? start : -1;
    }

    internal static bool TryFindPreparedLiteralContinuation(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        ref int position, in PreparedSubstringSearch search, int screenedLiteral, int continuation, ref Utf8SliceInvocation invocation,
        out Utf8SliceSearchOutcome outcome, bool existenceOnly = true, byte[]? terminalSuffix = null)
    {
        outcome = default;
        var classWindow = continuation != search.Length;
        var proof = program.LeadingClassWindowProof;
        var pair = classWindow && proof.CanAcceptAfterPair;
        if (!pair && invocation.Budget.Limits.MaxDepth is > 0 and < BranchedPrefixInstructionLimit)
            return false;
        invocation.Budget.ThrowIfCancellationRequested();
        if ((uint)position > (uint)input.Length || position < input.Length && (input[position] & 0xC0) == 0x80)
        {
            outcome = new(false, -1, -1, Utf8ExecutionLimitKind.None);
            return true;
        }

        var breach = pair ? invocation.TryChargeStep() : invocation.TryChargeInputStep();
        if (breach == Utf8ExecutionLimitKind.None && pair)
        {
            var (literal, leadingClass, _, _, _) = program.LeadingClassDelimiter;
            outcome = FindAlignedClassPair(program, input, position, leadingClass, (byte)literal, search,
                proof.SuffixAfter(search.Length), screenedLiteral, ref invocation);
            return true;
        }
        if (breach == Utf8ExecutionLimitKind.None && invocation.Ledger.ReservedBytes != 0)
            breach = invocation.TryReserveWorking(0);
        var start = screenedLiteral;
        if (breach == Utf8ExecutionLimitKind.None && classWindow && existenceOnly)
        {
            // An excluded delimiter fixes the end of the leading run.
            // Its last scalar suffices for this Boolean result.
            start = -1;
            if (screenedLiteral > position && TryReadScalar(input, screenedLiteral, true, out var scalar, out var width))
            {
                var leadingClass = program.LeadingClassDelimiter.LeadingClass;
                breach = TryMatchScalarClass(program, program.Instructions[leadingClass], scalar, ref invocation, out var member);
                if (member) start = screenedLiteral - width;
            }
        }
        int end;
        if (breach == Utf8ExecutionLimitKind.None && start >= 0 &&
            (terminalSuffix is not null
                ? (end = MatchPreparedLiteralTail(program, input, screenedLiteral + search.Length, continuation,
                    in program.Instructions[continuation], terminalSuffix)) != BranchedPrefixRejected
                : classWindow && proof.CanFinishTerminalRuns
                ? MayFinishTerminalClassRuns(program, input, screenedLiteral + search.Length, continuation, out end,
                    existenceOnly: existenceOnly)
                : MayMatchBranchedAsciiPrefix(program, input, screenedLiteral + search.Length, continuation,
                    out end, proveMatch: true, BranchedPrefixInstructionLimit - continuation, existenceOnly)))
        {
            if (end < 0)
            {
                // An exact leading literal ruled out all earlier starts.
                // A delimiter can still belong to a run starting before it.
                if (!existenceOnly && !classWindow) position = screenedLiteral;
                return false;
            }
            if (!existenceOnly && classWindow)
            {
                var leadingClass = program.LeadingClassDelimiter.LeadingClass;
                ref readonly var atom = ref program.Instructions[leadingClass];
                var categories = program.GetCompleteAsciiCategoryMask(atom);
                // The aligned delimiter is excluded by this complete, nonempty,
                // unbounded prefix. A rewind that consumes nothing rejects it.
                breach = categories != 0
                    ? TryRewindCategoryRun(input, position, categories, program.ReadAsciiSetMask(atom), ref start, ref invocation)
                    : TryRewindClassWhitespace(program, input, position, leadingClass, 0, true, ref start, ref invocation);
            }
            if (breach == Utf8ExecutionLimitKind.None && (!classWindow || start < screenedLiteral))
                breach = invocation.TryChargeInputStep();
            if (breach == Utf8ExecutionLimitKind.None)
            {
                invocation.Budget.ThrowIfCancellationRequested();
                if (!classWindow || start < screenedLiteral)
                {
                    outcome = new(true, start, end, breach);
                    return true;
                }
            }
        }
        if (breach == Utf8ExecutionLimitKind.None)
        {
            // The first hit rules out only its own prefix or delimiter.
            // Retain the next byte for overlapping needles.
            position = screenedLiteral + 1;
            return false;
        }
        outcome = new(false, -1, -1, breach);
        return true;
    }

    private static bool MayFinishTerminalClassRuns(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int position, int index, out int acceptedEnd, bool capturedRepeat = false, bool existenceOnly = false)
    {
        // A value-only call can skip the opening capture marker. The repeat
        // is followed only by its closing marker and Accept.
        if (capturedRepeat) index++;
        // A shared run already verifies the interior separator and both
        // operands. Otherwise the separator must follow the first run.
        var remaining = BranchedPrefixInstructionLimit - index - 4;
        var code = program.Instructions;
        var end = TryMatchLongPrefixRepeat(program, input, position, index + 1, in code[index], ref remaining);
        if (end >= 0 && !capturedRepeat && !code[index + 3].RepeatExitSharesTerminalRun)
        {
            if (end == input.Length || input[end] != code[index + 4].A)
                end = BranchedPrefixRejected;
            else if (existenceOnly)
                end = MatchFirstTerminalCategoryScalar(program, input, end + 1, in code[index + 6], remaining);
            else
                end = TryMatchLongPrefixRepeat(program, input, end + 1, index + 6, in code[index + 5], ref remaining);
        }
        acceptedEnd = end < 0 ? BranchedPrefixUndecided : end;
        return end != BranchedPrefixRejected;
    }

    private static int MatchFirstTerminalCategoryScalar(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int position, in Utf8SliceInstruction atom, int remaining)
    {
        // The terminal repeat can accept after its one required scalar.
        if (position == input.Length) return BranchedPrefixRejected;
        if (remaining < Math.Min(4, input.Length - position)) return BranchedPrefixUndecided;
        var value = input[position];
        if (value < 128)
            return MatchesAsciiHybridClass(program.SetMasks[atom.A], false, value)
                ? position + 1 : BranchedPrefixRejected;
        if (!TryReadScalar(input, position, false, out var scalar, out var width)) return BranchedPrefixUndecided;
        var categories = program.GetCompleteAsciiCategoryMask(atom);
        return (categories & (1u << (int)Rune.GetUnicodeCategory(scalar))) != 0
            ? position + width : BranchedPrefixRejected;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Utf8SliceCountOutcome CountTerminalClassWindows(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> delimiter, ref int position, int firstDelimiter, ref Utf8SliceInvocation invocation)
    {
        var (_, leadingClass, literalIndex, _, _) = program.LeadingClassDelimiter;
        var continuation = literalIndex + delimiter.Length;
        var capturedRepeat = program.LeadingClassWindowProof.Completion == Utf8SliceClassWindowCompletion.CapturedRepeat;
        var firstByte = delimiter[0];
        ref readonly var atom = ref program.Instructions[leadingClass];
        var count = 0;
        var scan = Math.Max(position, firstDelimiter);
        while (scan < input.Length)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
            var limit = scan + Math.Min(2048, input.Length - scan);
            var windowEnd = limit + Math.Min(delimiter.Length - 1, input.Length - limit);
            while (scan < limit)
            {
                var window = input[scan..windowEnd];
                var relative = delimiter.Length == 1 ? window.IndexOf(firstByte) : window.IndexOf(delimiter);
                if (relative < 0) break;
                var at = scan + relative;
                // An unsuccessful delimiter can overlap the next one, and a
                // later byte can belong to its leading class.
                scan = at + 1;

                // An excluded delimiter fixes the end of the leading run.
                // One preceding member suffices for Count, including at a block seam.
                breach = invocation.TryChargeBoundedCheck();
                if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
                if (at > position && TryReadScalar(input, at, true, out var scalar, out _))
                {
                    breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out var member);
                    if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
                    if (member && MayFinishTerminalClassRuns(program, input, at + delimiter.Length,
                        continuation, out var end, capturedRepeat))
                    {
                        // Replay from the previous proved floor if a run exceeds
                        // the bounded suffix proof's allowance.
                        if (end < 0) return new(count, Utf8ExecutionLimitKind.None);
                        if (invocation.Ledger.ReservedBytes != 0)
                        {
                            breach = invocation.TryReserveWorking(0);
                            if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
                        }
                        count++;
                        scan = end;
                    }
                }
                position = scan;
            }
            scan = Math.Max(scan, limit);
        }
        position = input.Length;
        var outcome = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return new(outcome == Utf8ExecutionLimitKind.None ? count : 0, outcome);
    }

    internal static bool TryFindPreparedLiteralRange(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        ref int position, in PreparedSubstringSearch search, ref int screenedLiteral, int continuation,
        ref Utf8SliceInvocation invocation, out Utf8SliceSearchOutcome outcome, byte[]? terminalSuffix = null)
    {
        outcome = default;
        var classWindow = continuation != search.Length;
        // Eligible class windows can recover their full start from one
        // excluded delimiter. The adjacent-class proof is existence-only.
        if (program.GroupCount != 0 ||
            classWindow && (search.Length != 1 || program.LeadingClassWindowProof.CanAcceptAfterPair) ||
            !Utf8BytePosition.IsScalarBoundary(input, position) ||
            invocation.Budget.Limits.MaxDepth is > 0 and < BranchedPrefixInstructionLimit)
            return false;

        if (screenedLiteral < position)
        {
            Utf8ExecutionLimitKind breach;
            if (search.Length == 1)
            {
                screenedLiteral = position;
                var literal = search.Needle[0];
                breach = TrySeekAsciiCandidate(input, ref screenedLiteral, literal, literal, ref invocation);
            }
            else
            {
                breach = Utf8SearchExecutor.TryFindLiteralCandidate(search, input, position, input.Length - search.Length,
                    false, ref invocation, out screenedLiteral);
            }
            if (breach != Utf8ExecutionLimitKind.None || screenedLiteral < 0)
            {
                invocation.Budget.ThrowIfCancellationRequested();
                invocation.Budget.CheckDeadline();
                outcome = new(false, -1, -1, breach);
                return true;
            }
        }
        return TryFindPreparedLiteralContinuation(program, input, ref position, search, screenedLiteral,
            continuation, ref invocation, out outcome, existenceOnly: false, terminalSuffix: terminalSuffix);
    }

    private static int MatchPreparedLiteralTail(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int start, int continuation, in Utf8SliceInstruction repeat, ReadOnlySpan<byte> suffix)
    {
        var remaining = BranchedPrefixInstructionLimit - continuation - 1;
        var end = TryMatchLongPrefixRepeat(program, input, start, continuation + 1, in repeat, ref remaining);
        if (end < 0) return end;
        if (remaining <= suffix.Length) return BranchedPrefixUndecided;
        return input[end..].StartsWith(suffix) ? end + suffix.Length : BranchedPrefixRejected;
    }

    internal static Utf8SliceCountOutcome CountPreparedLiteralRanges(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> suffix,
        ref int position, ref int screenedLiteral, ref Utf8SliceInvocation invocation)
    {
        var count = 0;
        var branched = suffix.IsEmpty;
        ref readonly var repeat = ref program.Instructions[prefix.Length];
        var last = input.Length - prefix.Length;
        position = Math.Max(position, screenedLiteral);
        screenedLiteral = -1;
        while (position <= last)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
            // Each window admits every prefix start once, including the
            // lookahead needed to recognize a literal across its right edge.
            var limit = position + Math.Min(2049 - prefix.Length, last - position + 1);
            var windowEnd = limit + (prefix.Length - 1);
            while (position < limit)
            {
                var relative = input[position..windowEnd].IndexOf(prefix);
                if (relative < 0)
                {
                    position = limit;
                    break;
                }

                position += relative;
                breach = invocation.TryChargeBoundedCheck();
                if (breach == Utf8ExecutionLimitKind.None && invocation.Ledger.ReservedBytes != 0)
                    breach = invocation.TryReserveWorking(0);
                if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);

                var remaining = BranchedPrefixInstructionLimit - prefix.Length - 1;
                var start = position + prefix.Length;
                var end = branched
                    ? ProbeBranchedAsciiPrefix(program, input, start, prefix.Length, ref remaining, proveMatch: true)
                    : MatchPreparedLiteralTail(program, input, start, prefix.Length, in repeat, suffix);
                if (end == BranchedPrefixRejected)
                {
                    position++;
                    continue;
                }
                if (end < 0)
                {
                    // The ordinary verifier must begin at the first unproved
                    // prefix. A later candidate could consume this match.
                    screenedLiteral = position;
                    break;
                }
                count++;
                position = end;
            }
            if (screenedLiteral >= 0) break;
        }
        if (screenedLiteral < 0) position = input.Length;
        var outcome = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return new(outcome == Utf8ExecutionLimitKind.None ? count : 0, outcome);
    }

    private static Utf8SliceSearchOutcome FindAlignedClassPair(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int position, int leadingClass, byte delimiter, in PreparedSubstringSearch search,
        (int First, int Second) suffixClasses, int screenedDelimiter, ref Utf8SliceInvocation invocation)
    {
        ref readonly var first = ref program.Instructions[suffixClasses.First];
        ref readonly var second = ref program.Instructions[suffixClasses.Second];
        while (true)
        {
            var breach = TrySeekLeadingClassLiteralWindow(program, input, leadingClass, delimiter, search,
                0, true, ref position, ref invocation, out var delimiterStart, out _, requiredSuffix: suffixClasses,
                screenedDelimiter: screenedDelimiter, useNearestViableStart: true);
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
            if (position < 0)
                return new(false, -1, -1, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());

            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
            invocation.Budget.ThrowIfCancellationRequested();
            if (position < delimiterStart)
            {
                // The leading run needs one scalar. The first suffix class
                // has width one scalar, so the second class must follow it.
                var suffix = delimiterStart + search.Length;
                var tail = input[suffix..];
                var width = MatchesAdjacentAsciiHybridClasses(program, first, second, tail) ? 2 : 0;
                if (width == 0 && tail.Length >= 2 && (tail[0] | tail[1]) >= 128)
                {
                    breach = TryMatchAdjacentHybridScalars(program, first, second, tail, ref invocation, out width);
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
                }
                if (width != 0)
                {
                    breach = invocation.TryChargeInputStep();
                    invocation.Budget.ThrowIfCancellationRequested();
                    return breach == Utf8ExecutionLimitKind.None
                        ? new(true, position, suffix + width, breach)
                        : new(false, -1, -1, breach);
                }
            }
            position = delimiterStart + 1;
            screenedDelimiter = -1;
        }
    }

    internal static Utf8ExecutionLimitKind TrySeekLeadingClassLiteralWindow(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int leadingClass, byte literal, in PreparedSubstringSearch? search,
        long gapMaximum, bool direct, ref int position, ref Utf8SliceInvocation invocation, out int windowEnd,
        out int provedMatchEnd,
        in (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd, bool Terminal) repeatPrefix = default,
        byte[][]? delimiterAlternatives = null, (int First, int Second) requiredSuffix = default,
        int screenedDelimiter = -1, bool useNearestViableStart = false)
    {
        provedMatchEnd = -1;
        if (leadingClass >= 2 && !repeatPrefix.Any.HasValue && delimiterAlternatives is null)
        {
            var code = program.Instructions;
            var preceding = leadingClass - 2;
            while (preceding >= 0 && code[preceding].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                preceding--;
            if (preceding >= 0 && IsUnicodeSpace(program, code[preceding]))
                return TrySeekWhitespaceClassStart(program, input, leadingClass, literal, search, ref position,
                    ref invocation, out windowEnd, out provedMatchEnd, screenedDelimiter);
        }
        var floor = position;
        var earliest = -1;
        // Two mandatory suffix atoms require at least two bytes. Limit only
        // delimiter discovery so the class checks still see the whole input.
        var searchEnd = requiredSuffix.Second != 0 ? Math.Max(0, input.Length - 2) : input.Length;
        Utf8ExecutionLimitKind breach;
        while (true)
        {
            if (screenedDelimiter >= floor && !repeatPrefix.Any.HasValue)
            {
                position = screenedDelimiter;
                if (requiredSuffix.Second != 0 && position > searchEnd - search.GetValueOrDefault().Length)
                    position = -1;
                breach = Utf8ExecutionLimitKind.None;
            }
            else if (delimiterAlternatives is not null)
            {
                breach = Utf8SearchExecutor.TryFindLiteralFamilyCandidate(delimiterAlternatives, false,
                    input, position, input.Length, false, ref invocation, out var candidate, out var found);
                position = found ? candidate.Index : -1;
            }
            else if (search is { } prepared && (prepared.Length > 1 || !direct))
            {
                var lastStart = searchEnd - prepared.Length;
                breach = Utf8ExecutionLimitKind.None;
                while (position <= lastStart)
                {
                    if (screenedDelimiter >= position)
                    {
                        // The same first hit still needs the repeat-prefix checks.
                        position = screenedDelimiter;
                        screenedDelimiter = -1;
                        if (position > lastStart) break;
                    }
                    else
                    {
                        breach = Utf8SearchExecutor.TryFindLiteralCandidate(prepared, input, position, lastStart,
                            false, ref invocation, out position);
                        if (breach != Utf8ExecutionLimitKind.None || position < 0) break;
                    }
                    var rejected = repeatPrefix.Last.HasValue &&
                        (position == floor || input[position - 1] < 128 && !repeatPrefix.Last.Contains(input[position - 1]));
                    if (!rejected && repeatPrefix.DisjointEnd != 0)
                    {
                        breach = TryNarrowDisjointRepeatPrefix(program, input, floor, position,
                            repeatPrefix.DisjointEnd, ref invocation, out earliest, out _, out rejected);
                        if (breach != Utf8ExecutionLimitKind.None) break;
                    }
                    if (!rejected) break;

                    // This literal cannot follow the mandatory final repeat.
                    // A later delimiter may still match from before this hit.
                    if (position == lastStart) { position = -1; break; }
                    position++;
                }
                if (position > lastStart) position = -1;
            }
            else
                breach = TrySeekAsciiCandidate(input[..searchEnd], ref position, literal, literal, ref invocation);
            windowEnd = position;
            if (breach != Utf8ExecutionLimitKind.None || position < 0) return breach;
            if (requiredSuffix.Second == 0 || search is not { } aligned) break;

            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var code = program.Instructions;
            if (!RejectsAdjacentHybridRuns(program, code[requiredSuffix.First], code[requiredSuffix.Second],
                input, position + aligned.Length))
                break;

            // The leading class cannot consume this ASCII delimiter byte.
            // Advance the floor by one to retain overlapping delimiters.
            floor = ++position;
            screenedDelimiter = -1;
            earliest = -1;
        }
        if (earliest >= 0)
        {
            position = earliest;
            if (repeatPrefix.Terminal) provedMatchEnd = windowEnd + search.GetValueOrDefault().Length;
            return Utf8ExecutionLimitKind.None;
        }
        // The bounded proof can run out of input allowance inside a long
        // run. Keep the existing whitespace rewind at its own delimiter.
        if (repeatPrefix.Any.HasValue &&
            !(repeatPrefix.Terminal && !direct && leadingClass >= 0 &&
                program.LeadingClassDelimiter.Literal == literal &&
                program.LeadingClassDelimiter.LiteralIndex == repeatPrefix.DisjointEnd))
        {
            // Even if this hit is inside a repeat and the actual delimiter is
            // later, every byte from its viable start to here can occur in the
            // prefix. Only an excluded ASCII byte proves a barrier. Rewind no
            // earlier than the last verified floor and keep all wider scalars.
            while (position > floor)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var length = Math.Min(256, position - floor);
                position -= length;
                var block = input.Slice(position, length);
                for (var offset = block.Length - 1; offset >= 0; offset--)
                {
                    var value = block[offset];
                    if (value >= 128 || repeatPrefix.Any.Contains(value)) continue;
                    position += offset + 1;
                    return invocation.TryChargeInputStep();
                }
            }
            return invocation.TryChargeInputStep();
        }
        if (!useNearestViableStart)
        {
            breach = TryRewindClassWhitespace(program, input, floor, leadingClass, gapMaximum, direct, ref position, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None || position >= windowEnd || !direct || !program.ReadsCaptures ||
                program.LeadingClassWindowProof.LiteralLength is not (> 0 and var referenceOffset) ||
                program.LeadingClassDelimiter.LeadingClass != leadingClass || program.LeadingClassDelimiter.Literal != literal)
                return breach;

            if (referenceOffset < input.Length - windowEnd)
            {
                // An ASCII follower requires the same first byte at the
                // capture start. Keep wide followers on the scalar path.
                var first = input[windowEnd + referenceOffset];
                if (first >= 128 || input[position] == first) return breach;
                breach = TrySeekAsciiCandidate(input[..windowEnd], ref position, first, first, ref invocation);
                if (breach != Utf8ExecutionLimitKind.None || position >= 0) return breach;
            }
            position = windowEnd + 1;
            return invocation.TryChargeInputStep();
        }

        // Boolean and Count callers can use the nearest valid start. The
        // excluded delimiter and the minimum of one make this scalar sufficient.
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (position > floor && TryReadScalar(input, position, true, out var scalar, out var width))
        {
            breach = TryMatchScalarClass(program, program.Instructions[leadingClass], scalar, ref invocation, out var member);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (member)
            {
                position -= width;
                return Utf8ExecutionLimitKind.None;
            }
        }
        // The final scalar cannot belong to the run, so this delimiter has no start.
        position++;
        return Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind TryNarrowDisjointRepeatPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int floor, int position, int instruction, ref Utf8SliceInvocation invocation,
        out int earliest, out int lastRunStart, out bool rejected)
    {
        earliest = lastRunStart = -1;
        rejected = false;
        // One bounded probe, plus at most the remainder of a scalar at its
        // lower edge. Leave longer runs to the existing conservative search.
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var stop = Math.Max(floor, position - 64);
        var code = program.Instructions.AsSpan();
        while (instruction > 0)
        {
            if (code[instruction - 1].Kind is Utf8SliceInstructionKind.CaptureStart or
                Utf8SliceInstructionKind.CaptureEnd or Utf8SliceInstructionKind.Boundary)
            {
                instruction--;
                continue;
            }
            instruction -= 4;
            var atom = code[instruction + 1];
            var ascii = program.SetMasks[atom.A].AsSpan();
            var end = position;
            while (position > stop)
            {
                var previous = input[position - 1];
                if (previous < 128)
                {
                    if ((ascii[previous >> 3] & (1 << (previous & 7))) == 0) break;
                    position--;
                    continue;
                }
                if (!TryReadScalar(input, position, true, out var scalar, out var width))
                    return Utf8ExecutionLimitKind.None;
                breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out var member);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!member) break;
                position -= width;
            }
            if (position <= stop && stop > floor) return Utf8ExecutionLimitKind.None;
            if (position == end)
            {
                rejected = true;
                return Utf8ExecutionLimitKind.None;
            }
            if (lastRunStart < 0) lastRunStart = position;
        }

        // Disjoint adjacent classes force every internal split. The start of
        // each maximal backward run is monotone in its end, even for a later
        // delimiter. The VM still checks minima, captures and assertions.
        earliest = position;
        return Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind TrySeekWhitespaceClassStart(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int leadingClass, byte literal, in PreparedSubstringSearch? search,
        ref int position, ref Utf8SliceInvocation invocation, out int windowEnd, out int provedMatchEnd,
        int screenedDelimiter)
    {
        var repeat = program.Instructions[leadingClass - 1];
        program.TryGetUnfoldedAsciiSet(program.Instructions[leadingClass], out var ascii, out _);
        var floor = position;
        var last = input.Length - (search?.Length ?? 1);
        windowEnd = -1;
        provedMatchEnd = -1;
        var proof = program.LeadingClassWindowProof;
        var canFinish = proof.CanFinishWhitespace && search?.Length == proof.LiteralLength &&
            invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit;
        while (position <= last)
        {
            Utf8ExecutionLimitKind breach;
            if (search.HasValue && screenedDelimiter >= position && screenedDelimiter <= last)
            {
                position = screenedDelimiter;
                screenedDelimiter = -1;
                breach = Utf8ExecutionLimitKind.None;
            }
            else
                breach = search is { } prepared
                    ? Utf8SearchExecutor.TryFindLiteralCandidate(prepared, input, position, last, false, ref invocation, out position)
                    : TrySeekAsciiCandidate(input, ref position, literal, literal, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None || position < 0) return breach;
            if (search.HasValue || MatchesStraightAsciiPrefix(program, input, position, leadingClass + 3))
            {
                var head = position;
                var lower = Math.Max(floor, position - (int)repeat.RepeatMaximum);
                while (head > lower && ascii.Contains(input[head - 1])) head--;
                // The class excludes whitespace. The next literal hit has the
                // same head or a later one, so verification stays leftmost.
                if (head > floor && position - head >= repeat.RepeatMinimum &&
                    TryReadScalar(input, head, true, out var scalar, out var width) &&
                    head - width >= floor && Rune.IsWhiteSpace(scalar))
                {
                    if (canFinish)
                    {
                        var after = position + proof.LiteralLength;
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        if (!TryReadScalar(input, after, false, out var follower, out var followerWidth))
                            break;
                        breach = TryMatchScalarClass(program, program.Instructions[^2], follower, ref invocation, out var accepted);
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        if (!accepted) { position++; continue; }
                        // An admitted follower within the repeat still
                        // leaves the greedy endpoint to the full verifier.
                        if (!ascii.Contains(input[after])) provedMatchEnd = after + followerWidth;
                    }
                    windowEnd = position = head - width;
                    return Utf8ExecutionLimitKind.None;
                }
            }
            position++;
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    private static bool MatchesStraightAsciiPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, int instructionIndex, int instructionLimit = 64) =>
        MatchesStraightAsciiPrefix(program, input, position, instructionIndex, instructionLimit, out _, out _);

    private static bool MatchesStraightAsciiPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, int instructionIndex, int instructionLimit,
        out int verifiedLength, out bool completes)
    {
        // Branches and wider classes stop the necessary-prefix check.
        // Only a nonempty run of ASCII consumers may prove completion.
        var code = program.Instructions.AsSpan();
        var prefixStart = instructionIndex;
        var inputStart = position;
        verifiedLength = 0;
        completes = false;
        var end = instructionIndex + Math.Min(code.Length - instructionIndex, instructionLimit);
        for (; instructionIndex < end; instructionIndex++)
        {
            var entry = code[instructionIndex];
            if (entry.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) continue;
            if (entry is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } && (uint)entry.A < 128)
            {
                if (position >= input.Length || input[position++] != entry.A) return false;
            }
            else if (entry is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } && program.SetMasks[entry.A].Length == 16)
            {
                if (position >= input.Length || input[position] >= 128) return false;
                var value = input[position++];
                if ((program.SetMasks[entry.A][value >> 3] & (1 << (value & 7))) == 0) return false;
            }
            else
            {
                // A negated ASCII mask can disprove its excluded bytes.
                // Its successful path still needs the scalar VM.
                if (!program.RightToLeft && entry is { Kind: Utf8SliceInstructionKind.Set, B: 1, C: 0 } && position < input.Length &&
                    input[position] is < 128 and var value && program.SetMasks[entry.A] is { Length: 16 } excluded &&
                    (excluded[value >> 3] & (1 << (value & 7))) != 0)
                    return false;
                if (!program.RightToLeft && entry is
                    { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.End or (int)Utf8SliceAnchorKind.VeryEnd } &&
                    !MatchesEndAnchor(input, position, entry))
                    return false;
                break;
            }
        }
        // A capture marker can help disprove the suffix, but the VM must
        // execute it. Only publish a pure run of one-byte consumers.
        var consumed = position - inputStart;
        if (consumed == instructionIndex - prefixStart) verifiedLength = consumed;
        completes = verifiedLength > 0 && instructionIndex < end &&
            code[instructionIndex].Kind == Utf8SliceInstructionKind.Accept;
        return true;
    }

    // Return -1 for a disproved arm or the number of consecutive pure
    // one-byte atoms verified at the arm's original entry.
    private static int GetForkPrefixLength(Utf8SliceProgram program, ReadOnlySpan<byte> input, int position, int index,
        bool allowAcceptProof, out bool provesAccept, bool allowEmptyAccept = false)
    {
        provesAccept = false;
        var code = program.Instructions.AsSpan();
        var probe = Utf8SliceInstruction.SkipLeadingCaptureMarkers(code, index);
        if ((uint)probe >= (uint)code.Length) return 0;
        var entry = code[probe];
        if (entry.Kind == Utf8SliceInstructionKind.Jump)
        {
            if (entry.C < 0)
            {
                if (input.Length - position < 2) return -1;
                var pair = BinaryPrimitives.ReadUInt16LittleEndian(input[position..]);
                var packed = ~entry.C;
                if (pair != (packed & 0xFFFF) && pair != (packed >> 16)) return -1;

                // This complete iteration can reach final acceptance again.
                // An explicit endpoint can still need an older iteration.
                provesAccept = allowAcceptProof;
                return 0;
            }
            if (entry.C > 0 && position < input.Length && input[position] == entry.C - 1) return -1;
            // A lowered delimited repeat cannot complete another iteration without
            // its exact delimiter. The fork step covers this bounded byte search.
            return entry.B > 0 && input.Length - position <= 32 && !input[position..].Contains((byte)(entry.B - 1))
                ? -1 : 0;
        }
        // Nested forks prove viability without selecting an inner path.
        if (entry is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } && entry.A > probe + 1)
            return MatchesStraightAsciiPrefix(program, input, position, entry.A, 2) ||
                MatchesStraightAsciiPrefix(program, input, position, probe + 1, 2) ? 0 : -1;
        if (entry.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy)
        {
            var minimum = entry.RepeatMinimum;
            if (minimum == 0 && entry.Kind == Utf8SliceInstructionKind.RepeatGreedy &&
                allowEmptyAccept && probe == index && entry.HasSingleAtomBody &&
                IsScalarRepeatAtom(code[probe + 1]) && entry.A < code.Length - 1 &&
                code[entry.A + 1] is { Kind: Utf8SliceInstructionKind.Jump, A: var join } &&
                join == code.Length - 1 && code[join].Kind == Utf8SliceInstructionKind.Accept)
            {
                // This terminal arm can always finish at the zero endpoint.
                // The repeat still executes to select its normal extent.
                provesAccept = true;
                return 0;
            }
            if (minimum == 0 && entry.RepeatMaximum == 1)
                return MayMatchBranchedAsciiPrefix(program, input, position, probe, out _, instructionLimit: 4) ? 0 : -1;
            if (minimum > 0 && IsSingleAtomRepeat(code, probe, entry))
            {
                var atom = code[probe + 1];
                var remaining = input.Length - position;
                // A required pure atom consumes at least one byte. An exact
                // ASCII interval also proves whether its first byte can fit.
                if (minimum > remaining && atom.Kind is Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set or
                    Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.Shorthand or
                    Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid)
                    return -1;
                var first = GetUnfoldedAsciiRange(atom, out var last);
                if (first >= 0 && (remaining == 0 || (uint)(input[position] - first) > (uint)(last - first)))
                    return -1;
            }
        }
        if (!MatchesStraightAsciiPrefix(program, input, position, probe, 2, out var verified, out _))
            return -1;

        // A required repeat immediately after a verified byte cannot match
        // without its first scalar. Prove membership for an ASCII interval
        // or decimal set. The repeat still runs for extent and captures.
        var next = probe + 1;
        if (verified == 1 && next < code.Length &&
            code[next] is { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 1 } repeat &&
            IsSingleAtomRepeat(code, next, repeat))
        {
            var atom = code[next + 1];
            var first = GetUnfoldedAsciiRange(atom, out var last);
            if (first >= 0 || atom.IsDecimalDigitSet)
            {
                var edge = position + 1;
                if (edge == input.Length) return -1;
                var value = input[edge];
                var matches = first >= 0
                    ? (uint)(value - first) <= (uint)(last - first)
                    : Utf8AsciiBytePredicates.IsDigit(value) || value >= 128 &&
                        TryReadScalar(input, edge, false, out var scalar, out _) && Rune.IsDigit(scalar);
                if (!matches) return -1;
                provesAccept = allowAcceptProof && (code[repeat.A].RepeatExitAcceptsEmptyTail ||
                    CanAcceptEmptyTail(code, repeat.A + 1));
            }
        }
        return probe == index ? verified : 0;
    }

    private static (int Min, int Max, Utf8SearchAsciiSet Bytes) FindBranchedAsciiWindow(
        Utf8SliceProgram program, int start)
    {
        var code = program.Instructions;
        if (code[start] is { Kind: Utf8SliceInstructionKind.Fork, B: 0, A: var other } &&
            other - start >= 32)
        {
            // The two arms do not join within the common window. Each arm
            // can still supply one required byte at a fixed, different offset.
            var first = FindRequiredAsciiWindow(program, start + 1, other, out _);
            if (first.Min == 0 || first.Min != first.Max) return default;
            var second = FindRequiredAsciiWindow(program, other, code.Length, out _);
            if (second.Min == 0 || second.Min != second.Max) return default;
            return (Math.Min(first.Min, second.Min), Math.Max(first.Min, second.Min),
                Utf8SearchAsciiSet.FromMasks(first.Bytes.Low | second.Bytes.Low, first.Bytes.High | second.Bytes.High));
        }
        return FindRequiredAsciiWindow(program, start, Math.Min(start + 32, code.Length), out _);
    }

    internal static (int Instruction, byte Min, byte Max, byte Value) PrepareRequiredBranchLiteral(
        Utf8SliceProgram program, int start = 0)
    {
        var end = Math.Min(start + 32, program.Instructions.Length);
        Utf8ConstructionScope.ChargeWork((ulong)(end - start));
        var window = FindRequiredAsciiWindow(program, start, end, out var instruction);
        if (window.Min == 0 || program.Instructions[instruction] is not
            { Kind: Utf8SliceInstructionKind.Literal, A: >= 0 and < 128 and var value, B: 0 })
            return default;
        return (instruction, (byte)window.Min, (byte)window.Max, (byte)value);
    }

    private static (int Min, int Max, Utf8SearchAsciiSet Bytes) FindRequiredAsciiWindow(
        Utf8SliceProgram program, int start, int end, out int instruction)
    {
        instruction = -1;
        var code = program.Instructions.AsSpan();
        Span<(int Min, int Max)> widths = stackalloc (int, int)[64];
        widths = widths[..Math.Min(widths.Length, end - start)];
        widths.Fill((-1, 0));
        widths[0] = (0, 0);
        var furthest = 0;
        for (var offset = 0; offset <= furthest; offset++)
        {
            var (min, max) = widths[offset];
            if (min < 0) continue;
            var index = start + offset;
            var entry = code[index];
            // Every live path joins here when the furthest edge is reached.
            // Keep at most two byte offsets and a small exact ASCII set.
            if (offset == furthest && min >= 2 && max - min <= 1 &&
                program.TryGetUnfoldedAsciiSet(entry, out var bytes, out var asciiOnly) && asciiOnly &&
                BitOperations.PopCount(bytes.Low) + BitOperations.PopCount(bytes.High) is > 0 and <= 4)
            {
                instruction = index;
                return (min, max, bytes);
            }

            var next = offset + 1;
            if (entry is { Kind: Utf8SliceInstructionKind.Jump } && entry.A > index)
                next = entry.A - start;
            else if (entry is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } && entry.A > index + 1)
            {
                var other = entry.A - start;
                if (other >= widths.Length) return default;
                JoinAsciiPrefixWidths(widths, other, min, max);
                furthest = Math.Max(furthest, other);
            }
            else if (entry.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy)
            {
                if (entry.RepeatMaximum > 64 ||
                    entry.RepeatMaximum != 1 && entry.RepeatMinimum != entry.RepeatMaximum ||
                    !IsSingleAtomRepeat(code, index, entry) ||
                    !program.TryGetUnfoldedAsciiSet(code[index + 1], out _, out var repeatAsciiOnly) ||
                    !repeatAsciiOnly) return default;
                min += (int)entry.RepeatMinimum;
                max += (int)entry.RepeatMaximum;
                next = entry.A + 1 - start;
            }
            else if (program.TryGetUnfoldedAsciiSet(entry, out _, out var atomAsciiOnly) && atomAsciiOnly)
            {
                min++;
                max++;
            }
            else if (entry.Kind is not (Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd))
                return default;

            if (next >= widths.Length || max > 64) return default;
            JoinAsciiPrefixWidths(widths, next, min, max);
            furthest = Math.Max(furthest, next);
        }
        return default;
    }

    private static int GetFirstPossibleBranchStart(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int fromByte, int screenedLiteral, in Utf8SearchPlan candidatePlan)
    {
        var required = program.RequiredBranchLiteral;
        if (required.Min == 0 ||
            candidatePlan.RequiredPrefilterSearcher.LiteralSearch.Needle is not [var screenedByte] || screenedByte != required.Value)
            return fromByte;

        // Every viable start must reach this byte within the proved window.
        // The prefilter already found its first occurrence.
        var start = screenedLiteral - required.Max;
        while ((input[start] & 0xC0) == 0x80) start++;
        return start;
    }

    private const int NoBranchSuffixCheck = -2;

    private static bool MayMatchCountBranch(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int position, int instruction, bool proveMatch, ref int rejectedSuffix, out int acceptedEnd,
        Utf8BoundedProgramEmitter.Match? emitted, ref int interpretedVisits)
    {
        var limit = BranchedPrefixInstructionLimit;
        if (rejectedSuffix != NoBranchSuffixCheck &&
            RejectRequiredBranchLiteralSuffix(program, input, position, ref rejectedSuffix, ref limit))
        {
            acceptedEnd = -1;
            return false;
        }
        if (MayMatchAdmittedCandidate(program, input, position, instruction, out acceptedEnd, emitted,
            ref interpretedVisits, proveMatch, limit))
            return true;

        if (rejectedSuffix == NoBranchSuffixCheck && proveMatch && emitted is null &&
            program.RequiredBranchLiteral.Min != 0 &&
            input.Length - position <= 64)
            rejectedSuffix = -1;
        return false;
    }

    private static bool RejectRequiredBranchLiteralSuffix(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int start, ref int rejectedSuffix, ref int proofLimit)
    {
        var (instruction, min, max, value) = program.RequiredBranchLiteral;
        var length = input.Length - start;
        var first = min < length && input[start + min] == value;
        var second = max != min && max < length && input[start + max] == value;
        proofLimit -= 2;
        // When both positions can reach the literal, keep the ordered proof.
        if (first == second) return !first;
        var suffix = start + (first ? min : max) + 1;
        if (suffix == rejectedSuffix) return true;

        // Admit the suffix check from the same candidate step as the ordinary
        // proof. Leave time for the whole proof if the suffix is inconclusive.
        const int suffixLimit = 40;
        var remaining = suffixLimit;
        var end = ProbeBranchedAsciiPrefix(program, input, suffix, instruction + 1, ref remaining, proveMatch: true);
        proofLimit -= suffixLimit - remaining;
        if (end != BranchedPrefixRejected) return false;
        rejectedSuffix = suffix;
        return true;
    }

    private static bool HasAsciiByteInWindow(ReadOnlySpan<byte> input, int start,
        (int Min, int Max, Utf8SearchAsciiSet Bytes) window)
    {
        var remaining = input.Length - start;
        return (window.Min < remaining && window.Bytes.Contains(input[start + window.Min])) ||
            (window.Max != window.Min && window.Max < remaining && window.Bytes.Contains(input[start + window.Max]));
    }

    private static bool TryGetAsciiBytePair(Utf8SearchAsciiSet bytes, out byte first, out byte last)
    {
        first = last = 0;
        if (BitOperations.PopCount(bytes.Low) + BitOperations.PopCount(bytes.High) is not (1 or 2)) return false;
        first = (byte)(bytes.Low != 0 ? BitOperations.TrailingZeroCount(bytes.Low) : 64 + BitOperations.TrailingZeroCount(bytes.High));
        last = (byte)(bytes.High != 0 ? 127 - BitOperations.LeadingZeroCount(bytes.High) : 63 - BitOperations.LeadingZeroCount(bytes.Low));
        return true;
    }

    private static Utf8ExecutionLimitKind TrySeekAsciiBranchWindow(ReadOnlySpan<byte> input, ref int position,
        byte first, byte last, int firstOffset, int lastOffset, byte tailFirst, byte tailLast, ref Utf8SliceInvocation invocation)
    {
        var head0 = Vector512.Create(first);
        var head1 = Vector512.Create(last);
        var tail0 = Vector512.Create(tailFirst);
        var tail1 = Vector512.Create(tailLast);
        var width = Vector512<byte>.Count;
        var vectorEnd = input.Length - lastOffset - width;
        var metered = !invocation.IsUnmetered || invocation.CheckpointHook is not null;
        while (position <= vectorEnd)
        {
            if (metered)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
            }
            // Keep both proved offsets inside the same charged input block.
            var blockEnd = position + Math.Min(2048 - lastOffset - width, vectorEnd - position);
            do
            {
                var heads = MemoryMarshal.Read<Vector512<byte>>(input[position..]);
                var early = MemoryMarshal.Read<Vector512<byte>>(input[(position + firstOffset)..]);
                var late = MemoryMarshal.Read<Vector512<byte>>(input[(position + lastOffset)..]);
                var starts = Vector512.Equals(heads, head0) | Vector512.Equals(heads, head1);
                var tails = Vector512.Equals(early, tail0) | Vector512.Equals(early, tail1) |
                    Vector512.Equals(late, tail0) | Vector512.Equals(late, tail1);
                var hits = (starts & tails).ExtractMostSignificantBits();
                if (hits != 0)
                {
                    position += BitOperations.TrailingZeroCount(hits);
                    return Utf8ExecutionLimitKind.None;
                }
                position += width;
            } while (position <= blockEnd);
        }
        // A last block can contain starts with the first offset still in bounds.
        // The caller applies the scalar proof and verifier to these candidates.
        return TrySeekLeadingByte(input, ref position, first, last, false, ref invocation, sparsePair: first != last);
    }

    private static void JoinAsciiPrefixWidths(Span<(int Min, int Max)> widths, int offset, int min, int max)
    {
        ref var destination = ref widths[offset];
        destination = destination.Min < 0 ? (min, max) : (Math.Min(destination.Min, min), Math.Max(destination.Max, max));
    }

    private static bool HasAsciiLiteralInWindow(ReadOnlySpan<byte> input, int start, (int Min, int Max, byte Literal) window)
    {
        var remaining = input.Length - start;
        return (window.Min < remaining && input[start + window.Min] == window.Literal) ||
            (window.Max != window.Min && window.Max < remaining && input[start + window.Max] == window.Literal);
    }

    internal static (int Min, int Max, byte Literal) GetShortRepeatLiteralWindow(Utf8SliceProgram program, int atomIndex)
    {
        var code = program.Instructions.AsSpan();
        if (atomIndex <= 0 || code.Length - atomIndex < 4) return default;
        var repeat = code[atomIndex - 1];
        if (repeat.RepeatMinimum == 0 || repeat.RepeatMaximum > 3 ||
            !IsSingleClassRepeat(code, atomIndex - 1) ||
            code[atomIndex + 3] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 } literal)
            return default;
        return ((int)repeat.RepeatMinimum, (int)repeat.RepeatMaximum, (byte)literal.A);
    }

    internal static bool MayReachShortRepeatLiteral(ReadOnlySpan<byte> input, int start,
        (int Min, int Max, byte Literal) window, bool admitsWideScalars)
    {
        var remaining = input.Length - start;
        if (remaining <= window.Min) return false;
        // The byte window covers every repeat length only when the consumed
        // scalars are all ASCII. Leave any possible wider scalar to the VM.
        return admitsWideScalars && (input[start] >= 128 ||
                window.Max >= 2 && remaining > 1 && input[start + 1] >= 128 ||
                window.Max == 3 && remaining > 2 && input[start + 2] >= 128) ||
            HasAsciiLiteralInWindow(input, start, window) ||
            window.Min == 1 && window.Max == 3 && remaining > 2 && input[start + 2] == window.Literal;
    }

    // A delimiter outside the decimal set can have only one matching endpoint.
    // Leave a possible wider decimal to the scalar proof.
    private static int FindShortAsciiDecimalLiteralEnd(ReadOnlySpan<byte> input, int start,
        (int Min, int Max, byte Literal) window)
    {
        var suffix = input[start..];
        var limit = Math.Min(suffix.Length, window.Max);
        var digits = 0;
        while (digits < limit && Utf8AsciiBytePredicates.IsDigit(suffix[digits])) digits++;
        if (digits == suffix.Length) return -1;
        var next = suffix[digits];
        if (digits >= window.Min && next == window.Literal) return start + digits + 1;
        return digits < limit && next >= 128 ? 0 : -1;
    }

    private static int CountStartsBelowRepeatMinimum(ReadOnlySpan<byte> prefix, Utf8SearchAsciiSet ascii)
    {
        // The input step covers this short probe. An excluded ASCII scalar
        // prevents reaching the minimum from every start through that byte.
        for (var offset = 0; offset < prefix.Length; offset++)
        {
            var value = prefix[offset];
            if (value >= 128) break;
            if (!ascii.Contains(value)) return offset + 1;
        }
        return 0;
    }

    internal static bool IsSingleAtomRepeat(ReadOnlySpan<Utf8SliceInstruction> code, int index, Utf8SliceInstruction entry)
        => entry.A == index + 3 && entry.A < code.Length &&
            code[index + 2] is { Kind: Utf8SliceInstructionKind.RepeatEnd } loop && loop.A == index &&
            code[entry.A] is { Kind: Utf8SliceInstructionKind.RepeatExit } exit && exit.A == entry.B;

    internal static int PrepareAnchoredRepeatProof(Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        var code = program.Instructions;
        // Admit the bounded marker walk before inspecting the instructions.
        budget.ChargeWork(program.GroupCount == 0 || program.ReadsCaptures ? 1UL :
            (ulong)Math.Min(code.Length, 1 + Utf8SliceInstruction.LeadingCaptureMarkerLimit));
        if (program.GroupCount == 0 && program.SkipsTerminalRewind(1) && code[1].RepeatMaximum <= 4)
            return 1;
        if (program.ReadsCaptures) return 0;

        // An unobserved opening capture does not change the repeat.
        var proofStart = Utf8SliceInstruction.SkipLeadingCaptureMarkers(code, 1);
        var index = proofStart;
        if (code.Length <= 32 && (uint)index < (uint)code.Length &&
            code[index] is { Kind: Utf8SliceInstructionKind.CategorySet, B: 0 or 2, C: 0 } or
                           { Kind: Utf8SliceInstructionKind.Set, B: 0 })
        {
            budget.ChargeWork((ulong)Math.Min(code.Length - index, 2 + Utf8SliceInstruction.LeadingCaptureMarkerLimit));
            var afterClass = Utf8SliceInstruction.SkipLeadingCaptureMarkers(code, index + 1);
            if (afterClass < code.Length && code[afterClass] is { Kind: Utf8SliceInstructionKind.RepeatGreedy } captured &&
                code[captured.A].RepeatExitUnobservedDecimal)
                return 1;
        }
        if (index < code.Length && code[index] is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: not 0 })
        {
            budget.ChargeWork(1);
            index++;
        }
        if ((uint)index >= (uint)code.Length || code[index] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy } repeat)
            return 0;
        if (index == proofStart && index != 1 && code.Length <= 32 && repeat.RepeatMinimum is >= 1 and <= 4 &&
            repeat.RepeatMinimum == repeat.RepeatMaximum && IsSingleAtomRepeat(code, index, repeat) &&
            code[index + 1] is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 })
            return 1;

        // The lowered delimiter proof prevents an unbounded run from rewinding.
        return repeat.RepeatMaximum == long.MaxValue &&
            code[repeat.A].RepeatExitRejectsShorterEndpoints &&
            (code[index + 1] is { Kind: Utf8SliceInstructionKind.Set, B: 0 } or
                { Kind: Utf8SliceInstructionKind.Hybrid, B: 0, D: 0 } ||
             GetUnfoldedAsciiRange(code[index + 1], out _) >= 0) ? -proofStart : 0;
    }

    // This check eliminates impossible starts. A caller without capture output
    // can also reuse an Accept reached through completely verified, ordered atoms.
    private const int BranchedPrefixRejected = -2;
    private const int BranchedPrefixUndecided = -1;
    private const int BranchedPrefixUnhandledRepeat = -3;

    private static int ProbeBranchedAsciiPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, int index, ref int remaining, bool proveMatch, bool existenceOnly = false)
    {
        var code = program.Instructions;
        while (remaining > 0 && (uint)index < (uint)code.Length)
        {
            remaining--;
            ref readonly var entry = ref code[index++];
            var width = 1;
            switch (entry.Kind)
            {
                case Utf8SliceInstructionKind.Literal:
                    if (entry.B != 0 || (uint)entry.A >= 128) return BranchedPrefixUndecided;
                    if ((uint)position >= (uint)input.Length || input[position] != entry.A) return BranchedPrefixRejected;
                    position++;
                    continue;
                case Utf8SliceInstructionKind.Set when entry.D != 0:
                    if ((uint)position >= (uint)input.Length || !entry.MatchesAsciiInterval(input[position]))
                        return BranchedPrefixRejected;
                    position++;
                    if (entry.HasAdjacentAsciiInterval && remaining != 0)
                    {
                        remaining--;
                        if (position == input.Length || !entry.MatchesAsciiInterval(input[position]))
                            return BranchedPrefixRejected;
                        position++;
                        index++;
                    }
                    continue;
                case Utf8SliceInstructionKind.Set:
                    break;
                case Utf8SliceInstructionKind.CategorySet when proveMatch:
                    // An explicit ASCII member is sufficient when no class
                    // program overrides the mask. Other scalars need the VM.
                    if (position < input.Length && input[position] is < 128 and var ascii &&
                        entry is { B: 0 or 2, C: 0 } &&
                        (program.SetMasks[entry.A][ascii >> 3] & (1 << (ascii & 7))) != 0 &&
                        program.CategorySetMembers[entry.D] is [{ Table.ClassProgram: null }])
                    {
                        position++;
                        continue;
                    }
                    return BranchedPrefixUndecided;
                case Utf8SliceInstructionKind.Accept:
                    return proveMatch ? position : BranchedPrefixUndecided;
                case Utf8SliceInstructionKind.CaptureStart:
                case Utf8SliceInstructionKind.CaptureEnd:
                    if (entry.C > index)
                    {
                        // Every skipped marker still consumes its proof visit.
                        var skipped = Math.Min(remaining, entry.C - index);
                        index += skipped;
                        remaining -= skipped;
                    }
                    continue;
                case Utf8SliceInstructionKind.Jump:
                    // A jump only changes the instruction cursor. The shared
                    // allowance above also counts backward edges.
                    index = entry.A;
                    continue;
                case Utf8SliceInstructionKind.Fork when entry.B == 0 && entry.A > index:
                    if (remaining == 0) return BranchedPrefixUndecided;
                    if (entry is { C: < 0, D: > 0 } && remaining >= 3 &&
                        position < input.Length && input[position] == ~entry.C)
                    {
                        // The target byte excludes the prepared captured prefix.
                        // Keep the visits for its open, its rejected first literal,
                        // and the matching target literal.
                        remaining -= 3;
                        position++;
                        index = entry.A + 1;
                        // A hit covers the same two literal visits. Leave a
                        // partial or failed comparison to the ordinary walk.
                        var tail = code[entry.A].C;
                        if (tail > 0 && remaining >= 2 && input.Length - position >= 2 &&
                            BinaryPrimitives.ReadUInt16LittleEndian(input[position..]) == tail - 1)
                        {
                            remaining -= 2;
                            position += 2;
                            index += 2;
                        }
                        continue;
                    }
                    // Many arms start with a fixed byte and an exact ASCII
                    // interval. Reject those mismatches before recursion.
                    if (code[index] is { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 } literal)
                    {
                        remaining--;
                        if (position < input.Length && input[position] == literal.A)
                        {
                            var next = index + 1;
                            var fallback = entry.A;
                            var afterSibling = literal.D;
                            var armJump = ~literal.C;
                            while (true)
                            {
                                var edge = position + 1;
                                var possible = true;
                                if (remaining != 0)
                                {
                                    ref readonly var follower = ref code[next];
                                    var hasInterval = follower is { Kind: Utf8SliceInstructionKind.Set, D: not 0 };
                                    if (hasInterval || follower is { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 })
                                    {
                                        remaining--;
                                        possible = edge < input.Length && (hasInterval
                                            ? follower.MatchesAsciiInterval(input[edge])
                                            : input[edge] == follower.A);
                                        next++;
                                        edge++;
                                    }
                                }
                                if (possible && proveMatch && armJump >= 0 && remaining >= armJump - next + 2)
                                {
                                    while (next < armJump)
                                    {
                                        ref readonly var atom = ref code[next++];
                                        remaining--;
                                        if (edge == input.Length || (atom.Kind == Utf8SliceInstructionKind.Set
                                            ? !atom.MatchesAsciiInterval(input[edge]) : atom.A != input[edge]))
                                        {
                                            possible = false;
                                            break;
                                        }
                                        edge++;
                                    }
                                    if (possible)
                                    {
                                        var join = code[armJump].A;
                                        remaining -= 2;
                                        if (edge < input.Length && input[edge] == code[join].A)
                                        {
                                            position = edge + 1;
                                            fallback = join + 1;
                                            break;
                                        }
                                        possible = false;
                                    }
                                }
                                if (possible)
                                {
                                    var end = ProbeBranchedAsciiPrefix(program, input, edge, next, ref remaining, proveMatch, existenceOnly);
                                    if (end != BranchedPrefixRejected) return end;
                                }
                                if (afterSibling == 0 || remaining < 2) break;

                                // Preparation proved that this sibling starts with
                                // the same matching byte. Keep its fork and literal
                                // visits while entering directly at the next atom.
                                remaining -= 2;
                                armJump = ~code[fallback + 1].C;
                                next = fallback + 2;
                                fallback = afterSibling;
                                afterSibling = 0;
                            }
                            index = fallback;
                            continue;
                        }
                        else if (literal.D != 0 && remaining >= 2)
                        {
                            // Both fallthrough arms reject this byte. Keep the
                            // visit charges for the second fork and its literal.
                            remaining -= 2;
                            index = literal.D;
                            continue;
                        }
                    }
                    else if (remaining >= 2 && index + 1 < entry.A &&
                        code[index].Kind == Utf8SliceInstructionKind.CaptureStart &&
                        code[index + 1] is { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 } captured &&
                        (position >= input.Length || input[position] != captured.A))
                    {
                        // The proof ignores the capture itself. Debit both
                        // visits and reject the arm without entering it.
                        remaining -= 2;
                    }
                    else
                    {
                        if (proveMatch && entry.A == index + 1 &&
                            input.Length - position is <= sizeof(ulong) and var tailLength && tailLength < remaining &&
                            code[index] is { Kind: Utf8SliceInstructionKind.Jump, A: var body, B: > 0 and <= 128 } jump &&
                            body < index)
                        {
                            // Each further iteration needs the delimiter recorded on its back edge.
                            // Only screen short tails to avoid rescanning a long suffix at each fork.
                            var suffix = input[position..];
                            remaining -= tailLength;
                            if (!suffix.Contains((byte)(jump.B - 1)))
                            {
                                remaining--;
                                index = entry.A;
                                continue;
                            }
                        }
                        var end = ProbeBranchedAsciiPrefix(program, input, position, index, ref remaining, proveMatch, existenceOnly);
                        if (end != BranchedPrefixRejected) return end;
                    }
                    index = entry.A;
                    continue;
                case Utf8SliceInstructionKind.Anchor:
                case Utf8SliceInstructionKind.Boundary:
                    if (!proveMatch) return BranchedPrefixUndecided;
                    if (entry.Kind == Utf8SliceInstructionKind.Anchor)
                    {
                        if (entry.A is not ((int)Utf8SliceAnchorKind.End or (int)Utf8SliceAnchorKind.VeryEnd)) return BranchedPrefixUndecided;
                        if (!MatchesEndAnchor(input, position, entry)) return BranchedPrefixRejected;
                    }
                    else if (!MatchesBoundary(program, entry, input, position)) return BranchedPrefixRejected;
                    continue;
                case Utf8SliceInstructionKind.RepeatGreedy:
                case Utf8SliceInstructionKind.RepeatLazy:
                    var minimum = entry.RepeatMinimum;
                    var maximum = entry.RepeatMaximum;
                    var singleAtom = entry.HasSingleAtomBody;
                    if (existenceOnly && proveMatch && singleAtom && minimum == 1 &&
                        code[entry.A].RepeatExitAcceptsEmptyTail &&
                        code[index] is { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 } required)
                    {
                        // One admitted member reaches the proved empty tail.
                        // The scalar verifier handles non-ASCII here.
                        if (position == input.Length) return BranchedPrefixRejected;
                        if (remaining == 0 || input[position] >= 128) return BranchedPrefixUndecided;
                        remaining--;
                        return MatchesAsciiHybridClass(program.SetMasks[required.A], required.B != 0, input[position])
                            ? position + 1 : BranchedPrefixRejected;
                    }
                    if (proveMatch && code[entry.A].RepeatExitCoversOptionalTail)
                        return MatchCoveredOptionalClassTail(program, input, position, index, ref remaining);
                    if (!singleAtom && proveMatch && entry.Kind == Utf8SliceInstructionKind.RepeatGreedy &&
                        code[entry.A].RepeatExitUnobservedDecimal)
                    {
                        var end = TryMatchLongPrefixRepeat(program, input, position, index + 1, in entry, ref remaining);
                        if (end < 0) return end == BranchedPrefixUnhandledRepeat ? BranchedPrefixUndecided : end;
                        position = end;
                        index = entry.A + 1;
                        continue;
                    }
                    if (singleAtom && minimum == 0 && maximum == 1 && code[index] is var optionalAtom &&
                        optionalAtom is ({ Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 } or
                            { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } or
                            { Kind: Utf8SliceInstructionKind.Hybrid, B: 0, D: 0 }))
                    {
                        var repeatIndex = index - 1;
                        index = entry.A + 1;
                        if (position == input.Length) continue;
                        if (proveMatch)
                        {
                            // The complete repeat proof admits one byte before testing it.
                            if (remaining == 0) return BranchedPrefixUndecided;
                            remaining--;
                        }
                        var value = input[position];
                        if (value < 128 && value != '\n' && proveMatch && code[entry.A].RepeatExitBeforeTerminalDot)
                        {
                            // The prepared terminal dot can consume this byte
                            // whenever the optional atom leaves it. Only the
                            // final endpoint is observed by this proof.
                            position++;
                            // Preparation proves the remaining instructions are
                            // the dot, the absolute end and plain capture markers.
                            // The dot's three body/loop/exit instructions are
                            // already skipped by the ordinary repeat proof.
                            var visits = code.Length - index - 3;
                            if (input.Length - position <= remaining - visits)
                            {
                                var lf = input[position..].IndexOf((byte)'\n');
                                if (lf < 0)
                                {
                                    remaining -= visits + input.Length - position;
                                    return input.Length;
                                }
                                var anchor = program.TerminalRepeatAnchor;
                                var end = position + lf;
                                if (MatchesEndAnchor(input, end, code[anchor]))
                                {
                                    remaining -= visits + lf + 1;
                                    return end;
                                }
                                remaining -= anchor - index - 2 + lf + 1;
                                return BranchedPrefixRejected;
                            }
                            continue;
                        }
                        if (value >= 128 && optionalAtom.Kind == Utf8SliceInstructionKind.Hybrid)
                            return BranchedPrefixUndecided;
                        var matchesOptional = optionalAtom.Kind switch
                        {
                            Utf8SliceInstructionKind.Literal => value == optionalAtom.A,
                            Utf8SliceInstructionKind.Set when optionalAtom.D != 0 => optionalAtom.MatchesAsciiInterval(value),
                            _ => value < 128 && (program.SetMasks[optionalAtom.A][value >> 3] & (1 << (value & 7))) != 0,
                        };
                        if (!matchesOptional) continue;
                        if (proveMatch && program.SkipsTerminalRewind(repeatIndex))
                        {
                            position++;
                            continue;
                        }
                        if (proveMatch && code[entry.A].RepeatExitOptionalAsciiDelimiter is > 0 and var delimiter)
                        {
                            if (remaining == 0) return BranchedPrefixUndecided;
                            remaining--;
                            var offset = delimiter >> 8;
                            if (offset >= input.Length - position) return BranchedPrefixRejected;
                            var delimiterByte = (byte)delimiter;
                            var shortWidth = input[position + offset] == delimiterByte;
                            if (!shortWidth) position++;
                            if (offset == 2 && remaining >= 3 && code[index].HasAdjacentAsciiInterval)
                            {
                                // The width check leaves room for both interval
                                // bytes, even after shifting to the longer arm.
                                ref readonly var pair = ref code[index];
                                remaining--;
                                if (!pair.MatchesAsciiInterval(input[position])) return BranchedPrefixRejected;
                                remaining--;
                                if (!pair.MatchesAsciiInterval(input[position + 1])) return BranchedPrefixRejected;
                                remaining--;
                                position += 2;
                                if (!shortWidth && (position == input.Length || input[position] != delimiterByte))
                                    return BranchedPrefixRejected;
                                position++;
                                index += 3;
                            }
                            continue;
                        }
                        var lazy = proveMatch && entry.Kind == Utf8SliceInstructionKind.RepeatLazy;
                        var preferred = lazy ? position : position + 1;
                        if (proveMatch && remaining >= 3 && input.Length - preferred >= 2 && code.Length - index >= 3 &&
                            code[index] is { Kind: Utf8SliceInstructionKind.Set, HasAdjacentAsciiInterval: true } terminalPair &&
                            code[index + 2].Kind == Utf8SliceInstructionKind.Accept)
                        {
                            // A terminal pair needs only its two visits and Accept.
                            // Keep the ordinary retry when the preferred endpoint fails.
                            remaining--;
                            if (terminalPair.MatchesAsciiInterval(input[preferred]))
                            {
                                remaining--;
                                if (terminalPair.MatchesAsciiInterval(input[preferred + 1]))
                                {
                                    remaining--;
                                    return preferred + 2;
                                }
                            }
                        }
                        else
                        {
                            var end = ProbeBranchedAsciiPrefix(program, input, preferred, index, ref remaining, proveMatch, existenceOnly);
                            if (end != BranchedPrefixRejected) return end;
                        }
                        if (lazy) position++;
                        continue;
                    }
                    // A fixed ASCII set repeat has one endpoint. Verify its
                    // byte mask below without trying competing repeat lengths.
                    if (proveMatch && singleAtom && (minimum != maximum ||
                        code[index] is not { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 }))
                    {
                        if (maximum <= 4 && code[index].IsDecimalDigitSet &&
                            (minimum == maximum || entry.Kind == Utf8SliceInstructionKind.RepeatGreedy &&
                                program.SkipsTerminalRewind(index - 1)))
                        {
                            // Fixed counts and proved greedy runs have one viable endpoint.
                            var available = Math.Min(input.Length - position, (int)maximum);
                            if (available > remaining) return BranchedPrefixUndecided;
                            remaining -= available;
                            var digits = input.Slice(position, available);
                            var stop = digits.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
                            if (stop >= 0 && digits[stop] >= 128)
                            {
                                var limit = minimum == maximum ? (int)maximum : available;
                                position = MatchWideDecimalPrefix(input, position + stop,
                                    Math.Max(0, (int)minimum - stop), limit - stop);
                                if (position < 0) return BranchedPrefixRejected;
                            }
                            else
                            {
                                var consumed = stop < 0 ? available : stop;
                                if (consumed < minimum) return BranchedPrefixRejected;
                                position += consumed;
                            }
                            index = entry.A + 1;
                            continue;
                        }
                        if (maximum <= 4 && entry.Kind == Utf8SliceInstructionKind.RepeatGreedy &&
                            code[index] is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } set &&
                            program.SkipsTerminalRewind(index - 1))
                        {
                            // This continuation excludes every shorter end of the ASCII run.
                            var available = Math.Min(input.Length - position, (int)maximum);
                            if (available > remaining) return BranchedPrefixUndecided;
                            remaining -= available;
                            var consumed = ScanScalarAsciiMask(input.Slice(position, available), program.SetMasks[set.A], category: false);
                            if (consumed < minimum) return BranchedPrefixRejected;
                            position += consumed;
                            index = entry.A + 1;
                            continue;
                        }
                        var continueProof = true;
                        var end = maximum > 4
                            ? TryMatchLongPrefixRepeat(program, input, position, index, in entry, ref remaining)
                            : TryMatchSmallPrefixRepeat(program, input, position, index, in entry, ref remaining, out continueProof);
                        if (end >= 0 && continueProof)
                        {
                            if (code[entry.A].RepeatExitSharesTerminalRun) return end;
                            position = end;
                            index = entry.A + 1;
                            continue;
                        }
                        if (end != BranchedPrefixUnhandledRepeat) return end;
                    }
                    if (minimum == 0) return BranchedPrefixUndecided;
                    // A required body always starts here. After a nontrivial loop,
                    // its end stays unknown. Skip only a proved fixed-width atom.
                    if (minimum != maximum || minimum > remaining || !singleAtom)
                    {
                        proveMatch = false;
                        continue;
                    }
                    width = (int)minimum;
                    remaining -= width;
                    var atomIndex = index;
                    index = entry.A + 1;
                    entry = ref code[atomIndex];
                    break;
                default:
                    return BranchedPrefixUndecided;
            }
            if (entry is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } && (uint)entry.A < 128)
            {
                if (width > input.Length - position ||
                    (width == 1 ? input[position] != entry.A : input.Slice(position, width).ContainsAnyExcept((byte)entry.A)))
                    return BranchedPrefixRejected;
            }
            else if (entry is { Kind: Utf8SliceInstructionKind.Set, B: 0 } && program.SetMasks[entry.A].Length == 16)
            {
                if (width > input.Length - position) return BranchedPrefixRejected;
                var mask = program.SetMasks[entry.A];
                foreach (var value in input.Slice(position, width))
                {
                    if (value >= 128)
                    {
                        if (entry.C == 0) return BranchedPrefixRejected;
                        if (width != 1) return BranchedPrefixUndecided;
                        width = GetFoldedMaskScalarWidth(input, position, mask, program.CaseBehavior);
                        if (width == 0) return BranchedPrefixRejected;
                        break;
                    }
                    if ((mask[value >> 3] & (1 << (value & 7))) == 0 &&
                        (entry.C == 0 || !Utf8SliceScalarClasses.MatchesMask(new Rune(value), mask, true, program.CaseBehavior)))
                        return BranchedPrefixRejected;
                }
            }
            else return BranchedPrefixUndecided;
            position += width;
        }
        return BranchedPrefixUndecided;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int MatchWideDecimalPrefix(ReadOnlySpan<byte> input, int position, int minimum, int maximum)
    {
        for (var digit = 0; digit < maximum; digit++)
        {
            if (!TryReadScalar(input, position, false, out var scalar, out var width) ||
                !Rune.IsDigit(scalar)) return digit < minimum ? BranchedPrefixRejected : position;
            position += width;
        }
        return position;
    }

    // Keep complete repeat verification out of the rejection walk used
    // for every viable Count prefix. Every continuation shares its visit allowance.
    // The caller resumes at a returned byte position when continueProof is set.
    // Competing endpoints can already have proved an outcome for the caller.
    [MethodImpl(MethodImplOptions.NoInlining)]
    // A nonnegative result is the endpoint of the run. A prepared terminal
    // pair proves both runs here. Other continuations are verified by the caller.
    private static int TryMatchLongPrefixRepeat(Utf8SliceProgram program, ReadOnlySpan<byte> input, int position,
        int atomIndex, in Utf8SliceInstruction repeat, ref int remaining)
    {
        ref readonly var atom = ref program.Instructions[atomIndex];
        var repeatMaximum = repeat.RepeatMaximum;
        if (atom.IsDecimalDigitSet && repeatMaximum == long.MaxValue && repeat.Kind == Utf8SliceInstructionKind.RepeatGreedy &&
            input.Length - position <= Math.Min(32, remaining) &&
            (program.SkipsTerminalRewind(atomIndex - 1) || program.Instructions[repeat.A].RepeatExitUnobservedDecimal))
        {
            // Only a proved greedy endpoint can continue. A wider scalar
            // may still be a decimal digit and needs the original scalar VM.
            var suffix = input[position..];
            var digits = suffix.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
            if (digits < 0) digits = suffix.Length;
            else if (suffix[digits] >= 128) return BranchedPrefixUndecided;
            if (digits < repeat.RepeatMinimum) return BranchedPrefixRejected;
            remaining -= Math.Min(suffix.Length, digits + 1);
            return position + digits;
        }

        var available = Math.Min(input.Length - position, repeatMaximum);
        var categories = program.GetCompleteAsciiCategoryMask(atom);
        var completeCategory = categories != 0;
        var negatedHybrid = atom is { Kind: Utf8SliceInstructionKind.Hybrid, B: not 0, D: 0 };
        ref readonly var exit = ref program.Instructions[repeat.A];
        // A greedy run with an unconditional empty tail can keep its
        // longest endpoint. Verify the tail in order for the final range.
        if (repeat.Kind != Utf8SliceInstructionKind.RepeatGreedy ||
            available > remaining && (remaining < repeat.RepeatMinimum || !completeCategory &&
                atom is not ({ Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } or
                    { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 })) ||
            !(exit.RepeatExitRejectsShorterEndpoints || exit.RepeatExitSharesTerminalRun ||
                repeatMaximum == long.MaxValue && (exit.RepeatExitAcceptsEmptyTail ||
                    completeCategory && repeat.A + 2 == program.Instructions.Length &&
                    program.Instructions[^1].Kind == Utf8SliceInstructionKind.Accept) ||
                program.SkipsTerminalRewind(atomIndex - 1)))
            return BranchedPrefixUnhandledRepeat;

        var candidate = input.Slice(position, (int)Math.Min(remaining, available));
        int stop;
        if (atom.Kind == Utf8SliceInstructionKind.Dot && repeat.RepeatMinimum <= 1 && repeatMaximum == long.MaxValue)
            stop = atom.A == 0 ? candidate.IndexOf((byte)'\n') : -1;
        else if (GetUnfoldedAsciiRange(atom, out var last) is >= 0 and var first)
            stop = candidate.IndexOfAnyExceptInRange((byte)first, last);
        else if (completeCategory || atom is { Kind: Utf8SliceInstructionKind.Set, B: 0 } or
            { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 })
        {
            var mask = program.SetMasks[atom.A];
            stop = ScanAsciiMask(candidate, mask, category: false, negated: negatedHybrid);
            if ((uint)stop < (uint)candidate.Length)
            {
                var value = candidate[stop];
                if (completeCategory || atom.Kind == Utf8SliceInstructionKind.Hybrid)
                {
                    if (value >= 128)
                    {
                        if (negatedHybrid) return BranchedPrefixUndecided;
                        // A byte offset suffices for the minimum only when
                        // the unbounded repeat needs at most one scalar.
                        if (repeatMaximum != long.MaxValue || repeat.RepeatMinimum > 1 ||
                            !completeCategory && atom.C != 1 << (int)Utf8SliceShorthand.Space)
                            return BranchedPrefixUnhandledRepeat;
                        stop = ContinueUnfoldedClassPrefix(candidate, stop, mask, categories);
                        if (stop < 0) return BranchedPrefixUndecided;
                    }
                }
                else if (atom.C != 0 && (value >= 128 ||
                    Utf8SliceScalarClasses.MatchesMask(new Rune(value), mask, true, program.CaseBehavior)))
                {
                    // The complete suffix is already within the proof's byte
                    // allowance. With no upper count and at most one required
                    // scalar, these byte endpoints suffice after folding.
                    if (repeatMaximum != long.MaxValue || repeat.RepeatMinimum > 1) return BranchedPrefixUnhandledRepeat;
                    stop = ContinueFoldedAsciiMask(candidate, stop, mask, program.CaseBehavior);
                }
            }
        }
        else return BranchedPrefixUnhandledRepeat;

        // The dot proof needs at most one scalar. A LF stop is aligned in
        // UTF-8, and a nonempty run suffices for that minimum.
        var length = stop < 0 ? candidate.Length : stop;
        // A short run can end within this window even on a long subject.
        // If every admitted byte matched, let the VM finish the run.
        if (length == candidate.Length && length < available)
        {
            remaining = 0;
            return BranchedPrefixUndecided;
        }
        if (length < repeat.RepeatMinimum) return BranchedPrefixRejected;
        remaining -= Math.Min(candidate.Length, length + 1);
        if (exit.RepeatExitSharesTerminalRun)
        {
            // An ASCII delimiter cannot start inside a wider scalar. Keep
            // at least one scalar on each side, even at the UTF-8 edges.
            var inner = length - 2;
            if (inner <= 0) return BranchedPrefixRejected;
            if (inner >= remaining) return BranchedPrefixUndecided;
            remaining -= inner + 1;
            if (!candidate.Slice(1, inner).Contains((byte)exit.RepeatExitRewindStarts))
                return BranchedPrefixRejected;
        }
        return position + length;
    }

    private static int MatchCoveredOptionalClassTail(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int position, int firstAtom, ref int remaining)
    {
        if (position == input.Length) return BranchedPrefixRejected;
        if (remaining < 2 || input[position] >= 128) return BranchedPrefixUndecided;
        remaining--;
        var code = program.Instructions;
        if (!MatchesAsciiHybridClass(program.SetMasks[code[firstAtom].A], true, input[position]))
            return BranchedPrefixRejected;

        var suffix = input[(position + 1)..];
        var window = suffix[..Math.Min(suffix.Length, remaining)];
        // The prepared final atom is followed by the repeat loop, exit and Accept.
        var consumed = ScanAsciiMask(window, program.SetMasks[code[^4].A], category: false, negated: true);
        remaining -= Math.Min(window.Length, consumed + 1);
        // A wide scalar or the end of the proof's allowance is unresolved.
        if (consumed == window.Length ? window.Length < suffix.Length : window[consumed] >= 128)
            return BranchedPrefixUndecided;
        return position + 1 + consumed;
    }

    private static int ContinueUnfoldedClassPrefix(ReadOnlySpan<byte> input, int offset,
        ReadOnlySpan<byte> asciiMask, uint categories)
    {
        while (offset < input.Length)
        {
            var value = input[offset];
            if (value < 128)
            {
                if ((asciiMask[value >> 3] & (1 << (value & 7))) == 0) break;
                offset++;
                continue;
            }
            if (Rune.DecodeFromUtf8(input[offset..], out var scalar, out var width) != OperationStatus.Done)
                return -1;
            if (categories == 0 ? !Rune.IsWhiteSpace(scalar) : (categories & (1u << (int)Rune.GetUnicodeCategory(scalar))) == 0)
                break;
            offset += width;
        }
        return offset;
    }

    private static int TryMatchSmallPrefixRepeat(Utf8SliceProgram program, ReadOnlySpan<byte> input, int position,
        int atomIndex, in Utf8SliceInstruction repeat, ref int remaining, out bool continueProof)
    {
        continueProof = false;
        ref readonly var atom = ref program.Instructions[atomIndex];
        var decimalDigits = atom.IsDecimalDigitSet;
        var ascii = default(Utf8SearchAsciiSet);
        var asciiOnly = false;
        Func<int, bool>? additionalAsciiMember = null;
        if (!decimalDigits && !program.TryGetUnfoldedAsciiSet(atom, out ascii, out asciiOnly))
        {
            if (atom is not { Kind: Utf8SliceInstructionKind.CategorySet, B: 0, C: 0 } ||
                program.CategorySetMembers[atom.D] is not
                    [{ Negated: false, Table: {
                        Mask: 0, Ranges.Length: 0, ClassProgram: null, ScalarPredicate: { } predicate } }])
                return BranchedPrefixUnhandledRepeat;

            // The mask immediately accepts its members. Consult the same pure
            // predicate as the VM for other ASCII bytes. Wide bytes still
            // leave the repeat undecided for profile-specific classification.
            var mask = program.SetMasks[atom.A].AsSpan();
            ascii = Utf8SearchAsciiSet.FromMasks(BinaryPrimitives.ReadUInt64LittleEndian(mask),
                BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]));
            additionalAsciiMember = predicate;
        }

        var minimum = (int)repeat.RepeatMinimum;
        var maximum = Math.Min(input.Length - position, (int)repeat.RepeatMaximum);
        if (maximum > remaining) return BranchedPrefixUndecided;
        remaining -= maximum;
        var consumed = 0;
        if (decimalDigits)
        {
            var suffix = input.Slice(position, maximum);
            var stop = suffix.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
            if (stop >= 0 && suffix[stop] >= 128)
            {
                if (repeat.Kind != Utf8SliceInstructionKind.RepeatGreedy || !program.SkipsTerminalRewind(atomIndex - 1))
                    return BranchedPrefixUndecided;
                // Each admitted repeat visit covers one scalar. The certificate
                // excludes shorter endpoints even when a digit spans several bytes.
                var end = MatchWideDecimalPrefix(input, position + stop, Math.Max(0, minimum - stop), maximum - stop);
                continueProof = end >= 0;
                return end;
            }
            consumed = stop < 0 ? maximum : stop;
        }
        else
        {
            while (consumed < maximum)
            {
                var value = input[position + consumed];
                if (value >= 128 && !asciiOnly) return BranchedPrefixUndecided;
                if (!ascii.Contains(value) && additionalAsciiMember?.Invoke(value) != true) break;
                consumed++;
            }
        }
        if (consumed < minimum) return BranchedPrefixRejected;
        if (consumed != minimum && !program.SkipsTerminalRewind(atomIndex - 1))
        {
            // Keep the first viable endpoint in the program's repeat order.
            var lazy = repeat.Kind == Utf8SliceInstructionKind.RepeatLazy;
            var continuation = repeat.A + 1;
            var last = lazy ? consumed : minimum;
            var step = lazy ? 1 : -1;
            for (var count = lazy ? minimum : consumed; count != last; count += step)
            {
                var end = ProbeBranchedAsciiPrefix(program, input, position + count, continuation, ref remaining, true);
                if (end == BranchedPrefixRejected) continue;
                return end;
            }
            consumed = last;
        }
        continueProof = true;
        return position + consumed;
    }

    private static int ContinueFoldedAsciiMask(ReadOnlySpan<byte> input, int position,
        byte[] mask, Utf8ScalarCaseBehavior behavior)
    {
        int width;
        while ((width = GetFoldedMaskScalarWidth(input, position, mask, behavior)) != 0)
        {
            position += width;
            position += ScanAsciiMask(input[position..], mask, category: false);
        }
        return position;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetFoldedMaskScalarWidth(ReadOnlySpan<byte> input, int position,
        byte[] mask, Utf8ScalarCaseBehavior behavior)
        => TryReadScalar(input, position, false, out var scalar, out var width) &&
            Utf8SliceScalarClasses.MatchesMask(scalar, mask, true, behavior) ? width : 0;

    internal const int BranchedPrefixInstructionLimit = 96;
    internal const int ShortSingleOriginProofInputLimit = 32;
    private const int SingleOriginPrefixInstructionLimit = 256;

    internal static int GetLongSingleOriginProofLimit(int length)
        => length > BranchedPrefixInstructionLimit ? SingleOriginPrefixInstructionLimit : BranchedPrefixInstructionLimit;

    private static int GetAnchoredLineProofStart(bool seekBeginning, in Utf8SliceInstruction anchor,
        ref Utf8SliceInvocation invocation)
        => seekBeginning && anchor is { B: 1, D: < 0 } &&
            invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit ? -anchor.D : 0;

    private static Utf8BoundedProgramEmitter.Match? GetCandidateProof(Utf8SliceProgram program, bool allowTier,
        ref Utf8SliceInvocation invocation, out int interpretedVisits)
    {
        interpretedVisits = -1;
        var emitted = program.EmittedMatch;
        if (emitted is null && allowTier && (program.ObservesProofWork || program.TieredProof is not null) &&
            invocation.CheckpointHook is null && invocation.IsUnmetered)
        {
            emitted = program.TieredProof;
            if (emitted is null) interpretedVisits = 0;
        }
        return emitted;
    }

    private static Utf8SliceCountOutcome CompleteCandidateCount(Utf8SliceProgram program, int count,
        int interpretedVisits, Utf8ExecutionLimitKind breach)
    {
        if (interpretedVisits > 0 && breach == Utf8ExecutionLimitKind.None)
            program.ObserveProofWork(interpretedVisits);
        return new(count, breach);
    }

    private static bool MayMatchAdmittedCandidate(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int position, int index, out int acceptedEnd, Utf8BoundedProgramEmitter.Match? emitted, ref int interpretedVisits,
        bool proveMatch, int instructionLimit = BranchedPrefixInstructionLimit)
    {
        // A compiled candidate executes the complete proof for the admitted
        // suffix. The general probe also handles partial instruction windows.
        if (index == 0 && proveMatch &&
            instructionLimit >= BranchedPrefixInstructionLimit && program.Instructions[0].Kind != Utf8SliceInstructionKind.Anchor)
        {
            if (emitted is not null)
            {
                var end = emitted(input[position..]);
                acceptedEnd = end < 0 ? -1 : position + end;
                return end >= 0;
            }
            if (interpretedVisits >= 0)
            {
                var remaining = instructionLimit;
                var end = ProbeBranchedAsciiPrefix(program, input, position, index, ref remaining, proveMatch: true);
                // The emitted proof can replace this walk even if the
                // interpreter leaves its outcome to the general matcher.
                interpretedVisits = Math.Min(Utf8SliceProgram.ProofTierThreshold,
                    interpretedVisits + instructionLimit - remaining);
                acceptedEnd = Math.Max(BranchedPrefixUndecided, end);
                return end != BranchedPrefixRejected;
            }
        }
        return MayMatchBranchedAsciiPrefix(program, input, position, index, out acceptedEnd, proveMatch, instructionLimit);
    }

    internal static bool MayMatchBranchedAsciiPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, int index, out int acceptedEnd, bool proveMatch = false,
        int instructionLimit = BranchedPrefixInstructionLimit, bool existenceOnly = false)
    {
        // Share one instruction and scalar allowance across the arms. The
        // caller meters this bounded check with each candidate search step.
        var remaining = instructionLimit;
        var end = ProbeBranchedAsciiPrefix(program, input, position, index, ref remaining, proveMatch, existenceOnly);
        acceptedEnd = Math.Max(BranchedPrefixUndecided, end);
        return end != BranchedPrefixRejected;
    }
}
