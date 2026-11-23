using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
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

    private static (Utf8SearchAsciiSet Repeated, Utf8SearchAsciiSet Follower) GetExactPrefixRepeatHead(
        Utf8SliceProgram program, int index)
    {
        var code = program.Instructions.AsSpan();
        if (index == 0 || code.Length - index < 5 || !IsSingleClassRepeat(code, index) ||
            code[index] is not { RepeatMinimum: 1, RepeatMaximum: long.MaxValue } ||
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
            ? (repeated, follower) : default;
    }

    // The candidate step covers this bounded scan. Only a complete ASCII
    // run can prove the following scalar's unique start. Wider or longer runs
    // leave the existing scalar attempt in charge.
    private static int ScanExactPrefixRepeatHead(ReadOnlySpan<byte> input, Utf8SearchAsciiSet ascii,
        Utf8SearchAsciiSet follower)
    {
        var length = Math.Min(16, input.Length);
        for (var offset = 0; offset < length; offset++)
        {
            var next = input[offset];
            if (next >= 128) return 0;
            if (!ascii.Contains(next)) return offset > 0 && follower.Contains(next) ? offset + 1 : -1;
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

    private static int GetUnfoldedAsciiRange(Utf8SliceInstruction instruction, out byte last)
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

    private static bool IsNonDecimalWideStart(ReadOnlySpan<byte> input, int position)
        => input[position] >= 128 && TryReadScalar(input, position, false, out var scalar, out _) && !Rune.IsDigit(scalar);

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
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            var relative = exactPrefix.IsEmpty ? IndexOfLeadingByte(input[position..], first, last, decimalSet, sparsePair, includeKelvin)
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
                ? IndexOfLeadingByte(block, first, last, decimalSet, sparsePair, includeKelvin) : block.IndexOf(exactPrefix);
            if (relative >= 0) { position += relative; return Utf8ExecutionLimitKind.None; }
            position += length - overlap;
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    private static int IndexOfLeadingByte(ReadOnlySpan<byte> input, byte first, byte last, bool decimalSet, bool sparsePair,
        bool includeKelvin)
        => decimalSet ? Utf8AsciiBytePredicates.DecimalOrScalarLead.IndexOf(input)
            : includeKelvin ? input.IndexOfAny(first, last, "K"u8[0])
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
            program.Instructions[index - 1] is { Kind: Utf8SliceInstructionKind.Fork, B: 1, C: > 1, D: 1 } &&
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

    private static byte[][]? GetAlignedDelimiterAlternatives(Utf8SliceProgram program, int index,
        in PreparedSearcher search)
    {
        if (program.Instructions[index].Kind != Utf8SliceInstructionKind.Fork ||
            search.ExactMultiLiterals is not { Length: >= 2 and <= 8 } literals) return null;
        foreach (var literal in literals)
            if (literal.Length is 0 or > 64) return null;

        // Each arm must begin with a prepared needle. Required literals farther
        // into a branch do not identify the end of the leading class window.
        var remaining = 16;
        return CoversAllStarts(index) ? literals : null;

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

    private static PreparedSubstringSearch? GetRepeatPrefixSearch(Utf8SliceProgram program,
        in Utf8SearchPlan candidatePlan, out (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd) prefix)
    {
        prefix = default;
        if (candidatePlan.LiteralSearch is not { IgnoreCase: false, Length: > 1 } search) return null;
        var code = program.Instructions.AsSpan();
        ulong low = 0, high = 0;
        var last = default(Utf8SearchAsciiSet);
        var previousCategories = 0u;
        var disjoint = true;
        var repeats = 0;
        var index = 0;
        while (index < code.Length && index < 64)
        {
            // The verifier still checks boundaries on the original subject.
            if (code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or
                Utf8SliceInstructionKind.Boundary)
            {
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
                if (!program.TryGetUnfoldedAsciiSet(atom, out ascii, out var asciiOnly)) return null;
                disjoint &= asciiOnly;
            }
            var required = code[index].RepeatMinimum > 0;
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
            !LiteralStartsAtInstruction(program, index, search.Needle)) return null;
        prefix = (Utf8SearchAsciiSet.FromMasks(low, high), last, disjoint && repeats >= 2 ? index : 0);
        return search;
    }

    private static Utf8ExecutionLimitKind TryProveSingleWindowAttempt(Utf8SliceProgram program, int leadingClass,
        byte delimiter, ref Utf8SliceInvocation invocation, out bool singleAttempt)
    {
        singleAttempt = false;
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

    private static bool CanReuseLeadingClassWindow(Utf8SliceProgram program, int leadingClass, int literalIndex)
        => leadingClass == 1 && literalIndex == 4 && program.GroupCount == 0 &&
            program.Instructions[0] is { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 1 };

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
            if (!TryReadScalar(input, position, true, out var scalar, out var width) || !Rune.IsWhiteSpace(scalar)) break;
            position -= width;
        }
        var classStart = position;
        var classMaximum = leadingClass >= 0 ? program.Instructions[leadingClass - 1].RepeatMaximum : 0;
        var ascii = default(Utf8SearchAsciiSet);
        var hasAsciiMask = false;
        if (directClassLiteral && (classMaximum == long.MaxValue || classMaximum <= 4))
        {
            if (program.LeadingScalarIsDecimal && leadingClass == program.LeadingByteCandidate.InstructionIndex)
            {
                ascii = Utf8SearchAsciiSet.FromMasks(Utf8AsciiBytePredicates.DecimalMask, 0);
                hasAsciiMask = true;
            }
            else hasAsciiMask = program.TryGetUnfoldedAsciiSet(code[leadingClass], out ascii, out _);
        }
        for (long remaining = classMaximum; remaining > 0 && position > floor; remaining--)
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
            breach = TryMatchScalarClass(program, program.Instructions[leadingClass], scalar, ref invocation, out var hit);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!hit) break;
            position -= width;
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
        int verifiedNextDelimiter)
    {
        // The caller proved a boundary, decimal run and adjacent one-byte delimiter.
        var floor = position;
        if (verifiedNextDelimiter >= floor)
        {
            // The preceding window already classified the ASCII digits
            // from this floor through the next delimiter. Keep the search
            // and rewind checkpoints under the original invocation.
            windowEnd = verifiedNextDelimiter;
            if (!invocation.IsUnmetered || invocation.CheckpointHook is not null)
            {
                var searchBreach = invocation.TryChargeInputStep();
                if (searchBreach != Utf8ExecutionLimitKind.None) return searchBreach;
            }
            var rewindBreach = invocation.TryChargeInputStep();
            if (rewindBreach == Utf8ExecutionLimitKind.None)
                position = Math.Max(floor, windowEnd - (int)program.Instructions[1].RepeatMaximum);
            return rewindBreach;
        }
        var breach = TrySeekAsciiCandidate(input, ref position, literal, literal, ref invocation);
        windowEnd = position;
        return breach != Utf8ExecutionLimitKind.None || position < 0 ? breach :
            TryRewindClassWhitespace(program, input, floor, leadingClass: 2, gapMaximum: 0, directClassLiteral: true,
                ref position, ref invocation);
    }

    internal static Utf8ExecutionLimitKind TrySeekLeadingClassLiteralWindow(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int leadingClass, byte literal, in PreparedSubstringSearch? search,
        long gapMaximum, bool direct, ref int position, ref Utf8SliceInvocation invocation, out int windowEnd,
        in (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd) repeatPrefix = default,
        byte[][]? delimiterAlternatives = null, (int First, int Second) requiredSuffix = default,
        int screenedDelimiter = -1)
    {
        if (leadingClass >= 2 && !repeatPrefix.Any.HasValue && delimiterAlternatives is null)
        {
            var code = program.Instructions;
            var preceding = leadingClass - 2;
            while (preceding >= 0 && code[preceding].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                preceding--;
            if (preceding >= 0 && IsUnicodeSpace(program, code[preceding]))
                return TrySeekWhitespaceClassStart(program, input, leadingClass, literal, search, ref position, ref invocation, out windowEnd);
        }
        var floor = position;
        var earliest = -1;
        Utf8ExecutionLimitKind breach;
        while (true)
        {
            if (screenedDelimiter >= floor)
            {
                position = screenedDelimiter;
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
                var lastStart = input.Length - prepared.Length;
                while (true)
                {
                    breach = Utf8SearchExecutor.TryFindLiteralCandidate(prepared, input, position, lastStart,
                        false, ref invocation, out position);
                    if (breach != Utf8ExecutionLimitKind.None || position < 0) break;
                    var rejected = repeatPrefix.Last.HasValue &&
                        (position == floor || input[position - 1] < 128 && !repeatPrefix.Last.Contains(input[position - 1]));
                    if (!rejected && repeatPrefix.DisjointEnd != 0)
                    {
                        breach = TryNarrowDisjointRepeatPrefix(program, input, floor, position,
                            repeatPrefix.DisjointEnd, ref invocation, out earliest, out rejected);
                        if (breach != Utf8ExecutionLimitKind.None) break;
                    }
                    if (!rejected) break;

                    // This literal cannot follow the mandatory final repeat.
                    // A later delimiter may still match from before this hit.
                    if (position == lastStart) { position = -1; break; }
                    position++;
                }
            }
            else
                breach = TrySeekAsciiCandidate(input, ref position, literal, literal, ref invocation);
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
        if (repeatPrefix.Any.HasValue)
        {
            if (earliest >= 0)
            {
                position = earliest;
                return Utf8ExecutionLimitKind.None;
            }
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
        return TryRewindClassWhitespace(program, input, floor, leadingClass, gapMaximum, direct, ref position, ref invocation);
    }

    private static Utf8ExecutionLimitKind TryNarrowDisjointRepeatPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int floor, int position, int instruction, ref Utf8SliceInvocation invocation,
        out int earliest, out bool rejected)
    {
        earliest = -1;
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
        }

        // Disjoint adjacent classes force every internal split. The start of
        // each maximal backward run is monotone in its end, even for a later
        // delimiter. The VM still checks minima, captures and assertions.
        earliest = position;
        return Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind TrySeekWhitespaceClassStart(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int leadingClass, byte literal, in PreparedSubstringSearch? search,
        ref int position, ref Utf8SliceInvocation invocation, out int windowEnd)
    {
        var repeat = program.Instructions[leadingClass - 1];
        program.TryGetUnfoldedAsciiSet(program.Instructions[leadingClass], out var ascii, out _);
        var floor = position;
        var last = input.Length - (search?.Length ?? 1);
        windowEnd = -1;
        while (position <= last)
        {
            var breach = search is { } prepared
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

        // A required ASCII repeat immediately after a verified byte cannot
        // match without its first byte. A greedy repeat with an empty tail
        // then has an accepting path, but still runs for extent and captures.
        var next = probe + 1;
        if (verified == 1 && next < code.Length &&
            code[next] is { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 1 } repeat &&
            IsSingleAtomRepeat(code, next, repeat) &&
            GetUnfoldedAsciiRange(code[next + 1], out var end) is >= 0 and var start)
        {
            var edge = position + 1;
            if (edge == input.Length || (uint)(input[edge] - start) > (uint)(end - start)) return -1;
            provesAccept = allowAcceptProof && CanAcceptEmptyTail(code, repeat.A + 1);
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

    internal static (int Instruction, byte Min, byte Max, byte Value) PrepareRequiredBranchLiteral(Utf8SliceProgram program)
    {
        var end = Math.Min(32, program.Instructions.Length);
        Utf8ConstructionScope.ChargeWork((ulong)end);
        var window = FindRequiredAsciiWindow(program, 0, end, out var instruction);
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
        ReadOnlySpan<byte> input, int position, int index, ref int remaining, bool proveMatch)
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
                    continue;
                case Utf8SliceInstructionKind.Set:
                    break;
                case Utf8SliceInstructionKind.Accept:
                    return proveMatch ? position : BranchedPrefixUndecided;
                case Utf8SliceInstructionKind.CaptureStart:
                case Utf8SliceInstructionKind.CaptureEnd:
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
                        if (tail != 0 && remaining >= 2 && input.Length - position >= 2 &&
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
                            while (true)
                            {
                                var edge = position + 1;
                                var possible = true;
                                if (remaining != 0 && GetUnfoldedAsciiRange(code[next], out var last) is >= 0 and var first)
                                {
                                    remaining--;
                                    possible = edge < input.Length && (uint)(input[edge] - first) <= (uint)(last - first);
                                    next++;
                                    edge++;
                                }
                                if (possible)
                                {
                                    var end = ProbeBranchedAsciiPrefix(program, input, edge, next, ref remaining, proveMatch);
                                    if (end != BranchedPrefixRejected) return end;
                                }
                                if (afterSibling == 0 || remaining < 2) break;

                                // Preparation proved that this sibling starts with
                                // the same matching byte. Keep its fork and literal
                                // visits while entering directly at the next atom.
                                remaining -= 2;
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
                        var end = ProbeBranchedAsciiPrefix(program, input, position, index, ref remaining, proveMatch);
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
                    else
                    {
                        if (!Utf8ScalarWordBoundary.TryGetAsciiBoundary(input, position, out var boundary)) return BranchedPrefixUndecided;
                        if (boundary != (entry.A != 0)) return BranchedPrefixRejected;
                    }
                    continue;
                case Utf8SliceInstructionKind.RepeatGreedy:
                case Utf8SliceInstructionKind.RepeatLazy:
                    var minimum = entry.RepeatMinimum;
                    var singleAtom = entry.HasSingleAtomBody;
                    if (singleAtom && minimum == 0 && entry.RepeatMaximum == 1 && code[index] is var optionalAtom &&
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
                            continue;
                        }
                        if (value >= 128 && optionalAtom.Kind == Utf8SliceInstructionKind.Hybrid)
                            return BranchedPrefixUndecided;
                        var optionalFirst = GetUnfoldedAsciiRange(optionalAtom, out var optionalLast);
                        if (optionalFirst >= 0
                            ? (uint)(value - optionalFirst) > (uint)(optionalLast - optionalFirst)
                            : value >= 128 || (program.SetMasks[optionalAtom.A][value >> 3] & (1 << (value & 7))) == 0)
                            continue;
                        if (proveMatch && program.SkipsTerminalRewind(repeatIndex))
                        {
                            position++;
                            continue;
                        }
                        var lazy = proveMatch && entry.Kind == Utf8SliceInstructionKind.RepeatLazy;
                        var preferred = lazy ? position : position + 1;
                        var end = ProbeBranchedAsciiPrefix(program, input, preferred, index, ref remaining, proveMatch);
                        if (end != BranchedPrefixRejected) return end;
                        if (lazy) position++;
                        continue;
                    }
                    // A fixed ASCII set repeat has one endpoint. Verify its
                    // byte mask below without trying competing repeat lengths.
                    if (proveMatch && singleAtom && (minimum != entry.RepeatMaximum ||
                        code[index] is not { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 }))
                    {
                        var end = TryMatchPrefixRepeat(program, input, position, index, in entry, ref remaining,
                            out var continueProof);
                        if (continueProof)
                        {
                            position = end;
                            index = entry.A + 1;
                            continue;
                        }
                        if (end != BranchedPrefixUnhandledRepeat) return end;
                    }
                    if (minimum == 0) return BranchedPrefixUndecided;
                    // A required body always starts here. After a nontrivial loop,
                    // its end stays unknown. Skip only a proved fixed-width atom.
                    if (minimum != entry.RepeatMaximum || minimum > remaining || !singleAtom)
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

    // Keep complete repeat verification out of the rejection walk used
    // for every viable Count prefix. Every continuation shares its visit allowance.
    // The caller resumes at a returned byte position when continueProof is set.
    // Competing endpoints can already have proved an outcome for the caller.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TryMatchPrefixRepeat(Utf8SliceProgram program, ReadOnlySpan<byte> input, int position,
        int atomIndex, in Utf8SliceInstruction repeat, ref int remaining, out bool continueProof)
    {
        continueProof = false;
        var atom = program.Instructions[atomIndex];
        var continuation = repeat.A + 1;
        var repeatMaximum = repeat.RepeatMaximum;
        if (atom.IsDecimalDigitSet && repeatMaximum == long.MaxValue && repeat.Kind == Utf8SliceInstructionKind.RepeatGreedy &&
            input.Length - position <= Math.Min(32, remaining) && program.SkipsTerminalRewind(atomIndex - 1))
        {
            // Only a proved greedy endpoint can continue. A wider scalar
            // may still be a decimal digit and needs the original scalar VM.
            var suffix = input[position..];
            var digits = suffix.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
            if (digits < 0) digits = suffix.Length;
            else if (suffix[digits] >= 128) return BranchedPrefixUndecided;
            if (digits < repeat.RepeatMinimum) return BranchedPrefixRejected;
            remaining -= Math.Min(suffix.Length, digits + 1);
            continueProof = true;
            return position + digits;
        }

        if (repeatMaximum > 4)
        {
            var available = Math.Min(input.Length - position, repeatMaximum);
            var exit = program.Instructions[repeat.A];
            // A greedy run with an unconditional empty tail can keep its
            // longest endpoint. Verify the tail in order for the final range.
            if (repeat.Kind != Utf8SliceInstructionKind.RepeatGreedy || available > remaining ||
                !(exit.RepeatExitRejectsShorterEndpoints ||
                    repeatMaximum == long.MaxValue && exit.RepeatExitAcceptsEmptyTail ||
                    program.SkipsTerminalRewind(atomIndex - 1)))
                return BranchedPrefixUnhandledRepeat;

            var suffix = input.Slice(position, (int)available);
            int stop;
            if (atom.Kind == Utf8SliceInstructionKind.Dot && repeat.RepeatMinimum <= 1 && repeatMaximum == long.MaxValue)
                stop = atom.A == 0 ? suffix.IndexOf((byte)'\n') : -1;
            else if (GetUnfoldedAsciiRange(atom, out var last) is >= 0 and var first)
                stop = suffix.IndexOfAnyExceptInRange((byte)first, last);
            else if (atom is { Kind: Utf8SliceInstructionKind.Set, B: 0 } or
                { Kind: Utf8SliceInstructionKind.Hybrid, B: 0, D: 0 })
            {
                var mask = program.SetMasks[atom.A];
                stop = ScanAsciiMask(suffix, mask, category: false);
                if ((uint)stop < (uint)suffix.Length)
                {
                    var value = suffix[stop];
                    if (atom.Kind == Utf8SliceInstructionKind.Hybrid)
                    {
                        if (value >= 128) return BranchedPrefixUnhandledRepeat;
                    }
                    else if (atom.C != 0 && (value >= 128 ||
                        Utf8SliceScalarClasses.MatchesMask(new Rune(value), mask, true, program.CaseBehavior)))
                    {
                        // The complete suffix is already within the proof's byte
                        // allowance. With no upper count and at most one required
                        // scalar, these byte endpoints suffice after folding.
                        if (repeatMaximum != long.MaxValue || repeat.RepeatMinimum > 1) return BranchedPrefixUnhandledRepeat;
                        stop = ContinueFoldedAsciiMask(suffix, stop, mask, program.CaseBehavior);
                    }
                }
            }
            else return BranchedPrefixUnhandledRepeat;

            // The dot proof needs at most one scalar. A LF stop is aligned in
            // UTF-8, and a nonempty run suffices for that minimum.
            var length = stop < 0 ? suffix.Length : stop;
            if (length < repeat.RepeatMinimum) return BranchedPrefixRejected;
            remaining -= Math.Min(suffix.Length, length + 1);
            continueProof = true;
            return position + length;
        }
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
        var maximum = Math.Min(input.Length - position, (int)repeatMaximum);
        if (maximum > remaining) return BranchedPrefixUndecided;
        remaining -= maximum;
        var consumed = 0;
        if (decimalDigits)
        {
            var stop = input.Slice(position, maximum).IndexOfAnyExceptInRange((byte)'0', (byte)'9');
            consumed = stop < 0 ? maximum : stop;
        }
        while (consumed < maximum)
        {
            var value = input[position + consumed];
            if (value >= 128 && !asciiOnly)
            {
                if (minimum != repeatMaximum || !decimalDigits)
                    return BranchedPrefixUndecided;

                // A fixed decimal repeat has one end. Decode the remaining scalars under the same small bound.
                var end = position + consumed;
                for (var digit = consumed; digit < minimum; digit++)
                {
                    if (!TryReadScalar(input, end, false, out var scalar, out var scalarWidth) ||
                        !Rune.IsDigit(scalar)) return BranchedPrefixRejected;
                    end += scalarWidth;
                }
                continueProof = true;
                return end;
            }
            if (decimalDigits || !ascii.Contains(value) && additionalAsciiMember?.Invoke(value) != true) break;
            consumed++;
        }
        if (consumed < minimum) return BranchedPrefixRejected;
        if (consumed != minimum && !program.SkipsTerminalRewind(atomIndex - 1))
        {
            // Keep the first viable endpoint in the program's repeat order.
            var lazy = repeat.Kind == Utf8SliceInstructionKind.RepeatLazy;
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

    private const int BranchedPrefixInstructionLimit = 96;
    private const int SingleOriginPrefixInstructionLimit = 256;

    internal static bool MayMatchBranchedAsciiPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, int index, out int acceptedEnd, bool proveMatch = false,
        int instructionLimit = BranchedPrefixInstructionLimit)
    {
        // Share one instruction and scalar allowance across the arms. The
        // caller meters this bounded check with each candidate search step.
        var remaining = instructionLimit;
        var end = ProbeBranchedAsciiPrefix(program, input, position, index, ref remaining, proveMatch);
        acceptedEnd = Math.Max(BranchedPrefixUndecided, end);
        return end != BranchedPrefixRejected;
    }
}
