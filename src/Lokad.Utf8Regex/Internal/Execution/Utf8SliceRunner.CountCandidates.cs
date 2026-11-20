using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    private static int FindExactFamilyContinuation(ReadOnlySpan<Utf8SliceInstruction> code, byte[][] literals)
    {
        // Prove that the discovery plan covers every arm of one leading fork
        // family. Only one exact arm may match at the same start.
        // The caller checks a leading boundary before entering the shared tail.
        var index = code[0].Kind == Utf8SliceInstructionKind.Boundary ? 1 : 0;
        var continuation = 0;
        var heads = default(Utf8SearchAsciiSet);
        for (var arm = 0; arm < literals.Length; arm++)
        {
            var literal = literals[arm];
            if (literal.Length is 0 or > 16 || literal[0] >= 128) return 0;
            if (heads.Contains(literal[0]))
            {
                for (var earlier = 0; earlier < arm; earlier++)
                {
                    var alternate = literals[earlier];
                    var commonLength = Math.Min(literal.Length, alternate.Length);
                    if (literal.AsSpan(0, commonLength).SequenceEqual(alternate.AsSpan(0, commonLength)))
                        return 0;
                }
            }
            heads = heads.With(literal[0]);

            var last = arm == literals.Length - 1;
            var next = continuation;
            if (!last)
            {
                if ((uint)index >= (uint)code.Length ||
                    code[index++] is not { Kind: Utf8SliceInstructionKind.Fork, B: 0, C: 0, D: 0 } fork)
                    return 0;
                next = fork.A;
            }
            if ((uint)next >= (uint)code.Length || next - index != literal.Length + (last ? 0 : 1)) return 0;

            foreach (var value in literal)
                if (value >= 128 || code[index++] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0 } atom || atom.A != value)
                    return 0;

            if (last) break;
            if (code[index] is not { Kind: Utf8SliceInstructionKind.Jump, B: 0 } jump ||
                jump.A <= next || jump.A >= code.Length || continuation != 0 && continuation != jump.A)
                return 0;
            continuation = jump.A;
            index = next;
        }
        return continuation;
    }

    // These entries leave the input edge unchanged when they succeed. The
    // scanner can reject an impossible consuming byte before the VM runs them.
    private static bool PrecedesRequiredScalar(Utf8SliceInstruction entry) =>
        entry.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or
            Utf8SliceInstructionKind.Anchor or Utf8SliceInstructionKind.Boundary or
            Utf8SliceInstructionKind.Lookahead or Utf8SliceInstructionKind.Lookbehind ||
        entry.Kind == Utf8SliceInstructionKind.RepeatGreedy && entry.RepeatMinimum > 0;

    // An unfolded ASCII literal or positive range proves possible starts.
    // This bounded instruction proof is independent of the subject.
    internal static (int, byte, int, byte, int, bool) FindLeadingAsciiCandidate(
        ReadOnlySpan<Utf8SliceInstruction> code, Utf8ConstructionBudget budget)
    {
        var index = 0;
        while (index < code.Length && index < 64)
        {
            budget.ChargeWork(1);
            if (!PrecedesRequiredScalar(code[index])) break;
            index++;
        }
        if (index >= code.Length || index >= 64) return (-1, 0, -1, 0, -1, false);
        var firstScalarIndex = index;
        var first = GetUnfoldedAsciiRange(code[index], out var last);
        if (first < 0)
        {
            if (code[index].Kind is not (Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.RepeatGreedy or
                Utf8SliceInstructionKind.RepeatLazy)) return (-1, 0, -1, 0, firstScalarIndex, false);
            var remaining = 64;
            first = GetBranchedAsciiStart(code, index, budget, ref remaining, out last);
            return (first, last, -1, 0, firstScalarIndex, true);
        }
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
        return (first, last, second, secondLast, firstScalarIndex, false);
    }

    // Every reachable arm must prove its first consuming byte. Stop on
    // unknown effects, an empty completion or backwards control flow. The
    // two arms share a fixed visit budget, including when paths reconverge.
    private static int GetBranchedAsciiStart(ReadOnlySpan<Utf8SliceInstruction> code, int index, Utf8ConstructionBudget budget,
        ref int remaining, out byte last)
    {
        last = 0;
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
                first = GetBranchedAsciiStart(code, index + 1, budget, ref remaining, out last);
                next = entry.A;
            }
            else if (entry.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy &&
                entry.RepeatMinimum == 0 && entry.RepeatMaximum == 1 && entry.A == index + 3 && entry.A < code.Length)
            {
                // The one-atom optional can consume its byte or skip directly
                // through the zero-width repeat exit into the continuation.
                first = GetUnfoldedAsciiRange(code[index + 1], out last);
                next = entry.A + 1;
            }
            else return GetUnfoldedAsciiRange(entry, out last);

            if (first < 0) return -1;
            var other = GetBranchedAsciiStart(code, next, budget, ref remaining, out var otherLast);
            if (other < 0) return -1;
            last = Math.Max(last, otherLast);
            return Math.Min(first, other);
        }
        return -1;
    }

    private static int GetUnfoldedAsciiRange(Utf8SliceInstruction instruction, out byte last)
    {
        last = 0;
        if (instruction is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } literal && (uint)literal.A < 128)
        {
            last = (byte)literal.A;
            return literal.A;
        }
        if (instruction is not { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } set) return -1;
        // Common lowering proves the immutable mask once. The existing operand
        // stores this fact without growing instructions or per-cursor state.
        last = (byte)(set.D >> 8);
        return (set.D & 255) - 1;
    }

    private static bool IsLeadingDecimalSet(Utf8SliceProgram program, int index)
        => index >= 0 && IsDecimalDigitSet(program, program.Instructions[index]);

    private static bool IsNonDecimalWideStart(ReadOnlySpan<byte> input, int position)
        => input[position] >= 128 && TryReadScalar(input, position, false, out var scalar, out _) && !Rune.IsDigit(scalar);

    private static bool IsDecimalDigitSet(Utf8SliceProgram program, in Utf8SliceInstruction atom)
        => atom.Kind == Utf8SliceInstructionKind.CategorySet &&
            TryGetPositiveCategorySet(program, atom, out var categories, out var low, out var high) &&
            categories == 1u << (int)UnicodeCategory.DecimalDigitNumber &&
            low == Utf8AsciiBytePredicates.DecimalMask && high == 0;

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
        byte first, byte last, bool decimalSet, ref Utf8SliceInvocation invocation, scoped ReadOnlySpan<byte> exactPrefix = default)
    {
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            var relative = exactPrefix.IsEmpty ? IndexOfLeadingByte(input[position..], first, last, decimalSet)
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
            var relative = exactPrefix.IsEmpty ? IndexOfLeadingByte(block, first, last, decimalSet) : block.IndexOf(exactPrefix);
            if (relative >= 0) { position += relative; return Utf8ExecutionLimitKind.None; }
            position += length - overlap;
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    private static int IndexOfLeadingByte(ReadOnlySpan<byte> input, byte first, byte last, bool decimalSet)
        => decimalSet ? Utf8AsciiBytePredicates.DecimalOrScalarLead.IndexOf(input)
            : first == last ? input.IndexOf(first) : input.IndexOfAnyInRange(first, last);

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

    private static PreparedSubstringSearch? GetAlignedDelimiterSearch(Utf8SliceProgram program, int index,
        in Utf8SearchPlan candidatePlan)
        => candidatePlan.LiteralSearch is { IgnoreCase: false } search &&
            LiteralStartsAtInstruction(program, index, search.Needle) ? search : null;

    private static int GetRequiredHybridSuffix(ReadOnlySpan<Utf8SliceInstruction> code, int index)
    {
        if (!IsSingleClassRepeat(code, index) || !IsSingleClassRepeat(code, index + 4) ||
            code[index].RepeatMinimum == 0 || code[index + 4].RepeatMinimum == 0 ||
            code[index + 1] is not { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 } ||
            code[index + 5] is not { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 }) return -1;
        return index;
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
        var hasAsciiMask = directClassLiteral && classMaximum == long.MaxValue &&
            program.TryGetUnfoldedAsciiSet(code[leadingClass], out ascii, out _);
        for (long remaining = classMaximum; remaining > 0 && position > floor; remaining--)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (hasAsciiMask && input[position - 1] < 128)
            {
                // The exact ASCII predicate can rewind one charged block.
                // Stop at the first wider scalar and decode it at its right edge.
                var length = Math.Min(64, position - floor);
                var block = input.Slice(position - length, length);
                var index = length - 1;
                while (index >= 0 && ascii.Contains(block[index])) index--;
                position -= length - 1 - index;
                if (index < 0) continue;
                if (block[index] < 128) break;
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

    internal static Utf8ExecutionLimitKind TrySeekLeadingClassLiteralWindow(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int leadingClass, byte literal, in PreparedSubstringSearch? search,
        long gapMaximum, bool direct, ref int position, ref Utf8SliceInvocation invocation, out int windowEnd,
        in (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd) repeatPrefix = default,
        byte[][]? delimiterAlternatives = null, int requiredSuffix = -1, int screenedDelimiter = -1)
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
        if (requiredSuffix >= 0 && search is { } aligned)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var code = program.Instructions;
            if (RejectsAdjacentHybridRuns(program, code[requiredSuffix + 1], code[requiredSuffix + 5],
                input, position + aligned.Length))
            {
                // The leading class cannot consume this ASCII delimiter byte.
                // Search from its successor, retaining overlapping delimiters.
                position++;
                return Utf8ExecutionLimitKind.None;
            }
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
    private static int GetForkPrefixLength(Utf8SliceProgram program, ReadOnlySpan<byte> input, int position, int index)
    {
        var code = program.Instructions.AsSpan();
        var probe = Utf8SliceInstruction.SkipLeadingCaptureMarkers(code, index);
        if ((uint)probe >= (uint)code.Length) return 0;
        var entry = code[probe];
        // A lowered delimited repeat cannot complete another iteration without
        // its exact delimiter. The fork step covers this bounded byte search.
        if (entry is { Kind: Utf8SliceInstructionKind.Jump, B: > 0 } && input.Length - position <= 32)
            return input[position..].Contains((byte)(entry.B - 1)) ? 0 : -1;
        // Nested forks prove viability without selecting an inner path.
        if (entry is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } && entry.A > probe + 1)
            return MatchesStraightAsciiPrefix(program, input, position, entry.A, 2) ||
                MatchesStraightAsciiPrefix(program, input, position, probe + 1, 2) ? 0 : -1;
        if (entry.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy)
        {
            var minimum = entry.RepeatMinimum;
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
        return probe == index ? verified : 0;
    }

    private static (int Min, int Max, byte Literal) FindBranchedAsciiLiteralWindow(
        ReadOnlySpan<Utf8SliceInstruction> code, int start)
    {
        Span<(int Min, int Max)> widths = stackalloc (int, int)[32];
        widths = widths[..Math.Min(widths.Length, code.Length - start)];
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
            // Keep at most two adjacent byte offsets for the hot check.
            if (offset == furthest && min >= 2 && max - min <= 1 &&
                entry is { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 })
                return (min, max, (byte)entry.A);

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
                if (entry.RepeatMaximum != 1 || !IsSingleAtomRepeat(code, index, entry) ||
                    GetUnfoldedAsciiRange(code[index + 1], out _) < 0) return default;
                min += (int)entry.RepeatMinimum;
                max++;
                next = entry.A + 1 - start;
            }
            else if (GetUnfoldedAsciiRange(entry, out _) >= 0)
            {
                min++;
                max++;
            }
            else if (entry.Kind is not (Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd))
                return default;

            if (next >= widths.Length || max > 16) return default;
            JoinAsciiPrefixWidths(widths, next, min, max);
            furthest = Math.Max(furthest, next);
        }
        return default;
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

    private static bool IsSingleAtomRepeat(ReadOnlySpan<Utf8SliceInstruction> code, int index, Utf8SliceInstruction entry)
        => entry.A == index + 3 && entry.A < code.Length &&
            code[index + 2] is { Kind: Utf8SliceInstructionKind.RepeatEnd } loop && loop.A == index &&
            code[entry.A] is { Kind: Utf8SliceInstructionKind.RepeatExit } exit && exit.A == entry.B;

    // This check eliminates impossible starts. A Boolean caller at the root
    // can also reuse an Accept reached through completely verified atoms.
    private static bool MayMatchBranchedAsciiPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, int index, ref int remaining, bool proveMatch, out int acceptedEnd)
    {
        acceptedEnd = -1;
        var code = program.Instructions;
        while (remaining > 0 && (uint)index < (uint)code.Length)
        {
            remaining--;
            var entry = code[index++];
            if (entry.Kind == Utf8SliceInstructionKind.Accept)
            {
                if (proveMatch) acceptedEnd = position;
                return true;
            }
            if (proveMatch && entry is { Kind: Utf8SliceInstructionKind.Anchor,
                A: (int)Utf8SliceAnchorKind.End or (int)Utf8SliceAnchorKind.VeryEnd })
            {
                if (!MatchesEndAnchor(input, position, entry)) return false;
                continue;
            }
            if (proveMatch && entry.Kind == Utf8SliceInstructionKind.Boundary)
            {
                if (!Utf8ScalarWordBoundary.TryGetAsciiBoundary(input, position, out var boundary)) return true;
                if (boundary != (entry.A != 0)) return false;
                continue;
            }
            if (entry.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) continue;
            if (entry is { Kind: Utf8SliceInstructionKind.Jump } && entry.A >= index)
            {
                index = entry.A;
                continue;
            }
            if (entry is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } && entry.A > index)
            {
                if (MayMatchBranchedAsciiPrefix(program, input, position, index, ref remaining, proveMatch, out acceptedEnd)) return true;
                index = entry.A;
                continue;
            }

            var width = 1;
            if (entry.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy)
            {
                var minimum = entry.RepeatMinimum;
                var singleAtom = IsSingleAtomRepeat(code, index - 1, entry);
                if (proveMatch && singleAtom && entry.RepeatMaximum <= 4 &&
                    program.TryGetUnfoldedAsciiSet(code[index], out var ascii, out var asciiOnly))
                {
                    var maximum = Math.Min(input.Length - position, (int)entry.RepeatMaximum);
                    if (maximum > remaining) return true;
                    remaining -= maximum;
                    var consumed = 0;
                    while (consumed < maximum)
                    {
                        var value = input[position + consumed];
                        if (value >= 128 && !asciiOnly)
                        {
                            if (minimum != entry.RepeatMaximum || !IsDecimalDigitSet(program, code[index])) return true;

                            // A fixed decimal repeat has one end. Decode the
                            // remaining scalars under the same small visit bound.
                            var end = position + consumed;
                            for (var digit = consumed; digit < minimum; digit++)
                            {
                                if (!TryReadScalar(input, end, false, out var scalar, out var scalarWidth) ||
                                    !Rune.IsDigit(scalar)) return false;
                                end += scalarWidth;
                            }
                            return MayMatchBranchedAsciiPrefix(program, input, end, entry.A + 1,
                                ref remaining, proveMatch, out acceptedEnd);
                        }
                        if (!ascii.Contains(value)) break;
                        consumed++;
                    }
                    if (consumed < minimum) return false;
                    var rewindFree = program.SkipsTerminalRewind(index - 1);
                    index = entry.A + 1;
                    // The Boolean witness only needs one valid endpoint.
                    // Share the visit bound across the remaining alternatives.
                    while (!rewindFree && consumed > minimum)
                    {
                        if (MayMatchBranchedAsciiPrefix(program, input, position + consumed, index,
                            ref remaining, proveMatch, out acceptedEnd))
                            return true;
                        consumed--;
                    }
                    position += consumed;
                    continue;
                }
                if (minimum == 0)
                {
                    if (!singleAtom || entry.RepeatMaximum != 1) return true;
                    var first = GetUnfoldedAsciiRange(code[index], out var last);
                    if (first < 0) return true;
                    index = entry.A + 1;
                    // An optional ASCII atom can consume one byte or leave the
                    // start unchanged. Reject only if neither continuation fits.
                    if (position < input.Length && (uint)(input[position] - first) <= (uint)(last - first) &&
                        MayMatchBranchedAsciiPrefix(program, input, position + 1, index, ref remaining, proveMatch, out acceptedEnd))
                        return true;
                    continue;
                }
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
                entry = code[atomIndex];
            }
            if (entry is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } && (uint)entry.A < 128)
            {
                if (width > input.Length - position ||
                    (width == 1 ? input[position] != entry.A : input.Slice(position, width).ContainsAnyExcept((byte)entry.A)))
                    return false;
            }
            else if (entry is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } && program.SetMasks[entry.A].Length == 16)
            {
                if (width > input.Length - position) return false;
                var mask = program.SetMasks[entry.A];
                foreach (var value in input.Slice(position, width))
                    if (value >= 128 || (mask[value >> 3] & (1 << (value & 7))) == 0) return false;
            }
            else return true;
            position += width;
        }
        return true;
    }

    private static bool MayMatchBranchedAsciiPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, int index, out int acceptedEnd, bool proveMatch = false, int instructionLimit = 96)
    {
        // Share one instruction and scalar allowance across the arms. The
        // caller meters this bounded check with each candidate search step.
        var remaining = instructionLimit;
        return MayMatchBranchedAsciiPrefix(program, input, position, index, ref remaining, proveMatch, out acceptedEnd);
    }
}
