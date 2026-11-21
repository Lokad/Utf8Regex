using System.Numerics;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Scalar-boundary viability for a NonBacktracking batch or an exclusive cursor window.
/// Each row records which instruction states can reach acceptance from that
/// boundary. Ordered replay then skips dead choices without rescanning suffixes.
/// Capture values never affect viability in this regular grammar. Preparation
/// takes O(scalars * states) work; stored offsets and packed rows cost
/// (scalars + 1) * (4 + 8 * ceiling(states / 64)) payload bytes, plus complete modeled array headers/alignment.</summary>
internal sealed class Utf8NonBacktrackingSearchState
{
    private static readonly Utf8NonBacktrackingSearchState NoMatches = new([], [], 0, 0);

    private readonly int[] _boundaries;
    private readonly ulong[] _viability;
    private readonly int _stride;
    private int _startLimit;
    private int _windowEnd = -1;
    private ulong _reservation;

    private Utf8NonBacktrackingSearchState(int[] boundaries, ulong[] viability, int stride, ulong reservation)
    {
        _boundaries = boundaries;
        _viability = viability;
        _stride = stride;
        _startLimit = boundaries.Length;
        _reservation = reservation;
    }

    internal bool IsWindow => _windowEnd >= 0;

    internal static bool IsEpsilonInstruction(Utf8SliceInstructionKind kind) => kind is
        Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Jump or
        Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or
        Utf8SliceInstructionKind.Anchor or Utf8SliceInstructionKind.Boundary;

    internal static int[] BuildEpsilonOrder(ReadOnlySpan<Utf8SliceInstruction> instructions, out ulong[] ancestors,
        out (ulong[] Closures, byte[] Leaves, uint Conditions) ordered)
    {
        ancestors = [];
        ordered = ([], [], 0);
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        var colors = budget.AllocateArray<byte>(instructions.Length, false);
        try
        {
            var order = budget.AllocateArray<int>(instructions.Length, false);
            var written = 0;
            var epsilonStart = instructions.Length;
            var wide = instructions.Length > 64;
            using var frames = new Utf8ConstructionBuffer<(int Pc, bool Complete)>(budget);
            for (var pc = 0; pc < instructions.Length; pc++)
            {
                if (colors[pc] == 2) continue;
                frames.Push((pc, false));
                while (frames.TryPop(out var frame))
                {
                    if (frame.Complete)
                    {
                        colors[frame.Pc] = 2;
                        if (wide && IsEpsilonInstruction(instructions[frame.Pc].Kind))
                            order[--epsilonStart] = frame.Pc;
                        else
                            order[written++] = frame.Pc;
                        continue;
                    }
                    if (colors[frame.Pc] == 2) continue;
                    if (colors[frame.Pc] == 1)
                        throw new InvalidOperationException("A nullable NonBacktracking cycle escaped scalar lowering.");
                    colors[frame.Pc] = 1;
                    frames.Push((frame.Pc, true));
                    var instruction = instructions[frame.Pc];
                    switch (instruction.Kind)
                    {
                        case Utf8SliceInstructionKind.Fork:
                            frames.Push((instruction.A, false));
                            frames.Push((frame.Pc + 1, false));
                            break;
                        case Utf8SliceInstructionKind.Jump:
                            frames.Push((instruction.A, false));
                            break;
                        case Utf8SliceInstructionKind.CaptureStart:
                        case Utf8SliceInstructionKind.CaptureEnd:
                        case Utf8SliceInstructionKind.Anchor:
                        case Utf8SliceInstructionKind.Boundary:
                            frames.Push((frame.Pc + 1, false));
                            break;
                    }
                }
            }
            // Fold unconditional packed dependencies once. A condition's edge
            // remains a runtime decision; its unconditional ancestors can fold.
            if (!wide)
            {
                ordered = Utf8SliceRunner.BuildOrderedRegularClosures(instructions, order, budget);
                ancestors = budget.AllocateArray<ulong>(instructions.Length, false);
                budget.ChargeWork(3UL * (ulong)instructions.Length);
                for (var pc = 0; pc < instructions.Length; pc++) ancestors[pc] = 1UL << pc;
                // Parents precede their children in this reverse order.
                for (var index = order.Length - 1; index >= 0; index--)
                {
                    var pc = order[index];
                    var instruction = instructions[pc];
                    switch (instruction.Kind)
                    {
                        case Utf8SliceInstructionKind.Fork:
                            ancestors[instruction.A] |= ancestors[pc];
                            ancestors[pc + 1] |= ancestors[pc];
                            break;
                        case Utf8SliceInstructionKind.Jump:
                            ancestors[instruction.A] |= ancestors[pc];
                            break;
                        case Utf8SliceInstructionKind.CaptureStart:
                        case Utf8SliceInstructionKind.CaptureEnd:
                            ancestors[pc + 1] |= ancestors[pc];
                            break;
                    }
                }
                budget.ChargeWork((ulong)order.Length);
                var kept = 0;
                foreach (var pc in order)
                    if (instructions[pc].Kind is Utf8SliceInstructionKind.Anchor or Utf8SliceInstructionKind.Boundary)
                        order[kept++] = pc;
                var compact = kept == 0 ? [] : budget.AllocateArray<int>(kept, false);
                order.AsSpan(0, kept).CopyTo(compact);
                budget.ReleaseArray(order, false);
                order = compact;
            }
            else
            {
                // Leaves have no same-row dependencies. Keep them together
                // and preserve the dependency order among epsilon operations.
                budget.ChargeWork((ulong)(order.Length - epsilonStart));
                order.AsSpan(epsilonStart).Reverse();
            }
            construction.Complete();
            return order;
        }
        finally
        {
            budget.ReleaseArray(colors, false);
        }
    }

    // Input is caller-valid UTF-8. A bounded size scan and scalar pass
    // admit exact offsets and bit rows; no UTF-16 subject or unit map is built.
    internal static Utf8ExecutionLimitKind TryCreate(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out Utf8NonBacktrackingSearchState? state)
    {
        state = null;
        invocation.Budget.ThrowIfCancellationRequested();
        var first = 0;
        var singleWord = program.Instructions.Length <= 64;
        if (!singleWord)
        {
            var seek = Utf8SliceRunner.TrySeekInitialRegularStart(program, input, ref first, ref invocation);
            if (seek != Utf8ExecutionLimitKind.None) return seek;
            if (first < 0)
            {
                state = NoMatches;
                return Utf8ExecutionLimitKind.None;
            }
        }
        var scalars = 0;
        for (var edge = first; edge < input.Length;)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var block = input.Slice(edge, Math.Min(64, input.Length - edge));
            var firstNonAscii = block.IndexOfAnyExceptInRange((byte)0, (byte)0x7f);
            if (firstNonAscii < 0) scalars += block.Length;
            else
            {
                scalars += firstNonAscii;
                for (var index = firstNonAscii; index < block.Length; index++)
                    if ((block[index] & 0xc0) != 0x80) scalars++;
            }
            edge += block.Length;
        }
        var count = (long)scalars + 1;
        var stride = (int)(((long)program.Instructions.Length + 63) / 64);
        var cells = count * stride;
        if (count > Array.MaxLength || cells > Array.MaxLength) return Utf8ExecutionLimitKind.WorkingBytes;
        var reservation = checked(Utf8ConstructionBudget.ArrayBytes<int>((int)count)
            + Utf8ConstructionBudget.ArrayBytes<ulong>((int)cells));
        var admission = invocation.TryReserveWorking(reservation);
        if (admission != Utf8ExecutionLimitKind.None) return admission;
        var published = false;
        try
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var boundaries = new int[(int)count];
            boundaries[0] = first;
            var bits = new ulong[(int)cells];
            for (var index = 1; index < boundaries.Length; index++)
            {
                if (((index - 1) & 63) == 0)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                }
                boundaries[index] = Utf8SliceRunner.AdvancePastScalar(input, boundaries[index - 1], false);
                if (boundaries[index] < 0) throw new InvalidOperationException("The NonBacktracking batch requires validated scalar input.");
            }
            var result = new Utf8NonBacktrackingSearchState(boundaries, bits, stride, reservation);
            var epsilon = singleWord ? default : Utf8SliceRunner.GetWideRegularEpsilonOrder(program);
            var scalar = default(Rune);
            for (var boundary = scalars; boundary >= 0; boundary--)
            {
                var edge = boundaries[boundary];
                var before = default(Rune);
                if (boundary > 0) _ = Rune.DecodeFromUtf8(input[boundaries[boundary - 1]..], out before, out _);
                else if (edge != 0) _ = Rune.DecodeLastFromUtf8(input[..edge], out before, out _);
                if (singleWord)
                {
                    var following = boundary < scalars ? bits[boundary + 1] : 0;
                    breach = TryBuildPackedRow(program, input, edge, scalar, before, following, ref invocation, out var current);
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    bits[boundary] = current;
                    scalar = before;
                    continue;
                }
                var currentRow = bits.AsSpan(boundary * stride, stride);
                var followingRow = boundary == scalars ? default : bits.AsSpan((boundary + 1) * stride, stride);
                breach = Utf8SliceRunner.TryBuildWideRegularRow(program, epsilon, input, edge, scalar, before,
                    followingRow, currentRow, ref invocation);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                // On the next reverse row this preceding scalar is current.
                // The end row has no current scalar; the physical start has no prior one.
                scalar = before;
            }
            invocation.Budget.ThrowIfCancellationRequested();
            invocation.Budget.CheckDeadline();
            state = result;
            published = true;
            return Utf8ExecutionLimitKind.None;
        }
        finally { if (!published) invocation.Ledger.Release(reservation); }
    }

    private static Utf8ExecutionLimitKind TryBuildPackedRow(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int edge, Rune scalar, Rune before, ulong following, ref Utf8SliceInvocation invocation, out ulong current)
    {
        current = program.NonBacktrackingAcceptClosure;
        // At most 64 distinct cells; atom predicates retain their own direct charges.
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var candidates = (following >> 1) & program.NonBacktrackingConsumerMask;
        while (candidates != 0)
        {
            var pc = BitOperations.TrailingZeroCount(candidates);
            candidates &= candidates - 1;
            ref readonly var instruction = ref program.Instructions[pc];
            // The row admission covers this constant rejection, at most 64
            // cells. Feasible/folded atoms retain their direct admission.
            if (instruction.Kind == Utf8SliceInstructionKind.Literal && instruction.B == 0 && instruction.A != scalar.Value)
                continue;
            if (Utf8SliceRunner.MatchesNonBacktrackingAtom(program, instruction, scalar, ref invocation, out breach))
            {
                current |= program.NonBacktrackingEpsilonAncestors[pc];
            }
            if (breach != Utf8ExecutionLimitKind.None) return breach;
        }
        foreach (var pc in program.NonBacktrackingEpsilonOrder)
        {
            ref readonly var instruction = ref program.Instructions[pc];
            bool viable;
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Anchor:
                    viable = (current & (1UL << (pc + 1))) != 0 &&
                        Utf8SliceRunner.HoldsNonBacktrackingAnchor(instruction, input, edge);
                    break;
                case Utf8SliceInstructionKind.Boundary:
                    var beforeWord = edge > 0 && Utf8SliceScalarClasses.IsBoundaryWord(before, program, instruction);
                    var afterWord = edge < input.Length && Utf8SliceScalarClasses.IsBoundaryWord(scalar, program, instruction);
                    viable = (current & (1UL << (pc + 1))) != 0 &&
                        (beforeWord != afterWord) == (instruction.A != 0);
                    break;
                default:
                    throw new InvalidOperationException("An unconditional cell entered the packed condition order.");
            }
            if (viable) current |= program.NonBacktrackingEpsilonAncestors[pc];
        }
        return Utf8ExecutionLimitKind.None;
    }

    internal bool CanMatch(int boundary, int pc)
        => (_viability[boundary * _stride + (pc >> 6)] & (1UL << (pc & 63))) != 0;

    internal int ResolveByteOffset(ReadOnlySpan<byte> input, int byteOffset)
    {
        if (_boundaries.Length != 0 && byteOffset >= _boundaries[0])
        {
            var index = Array.BinarySearch(_boundaries, byteOffset);
            if (index >= 0) return index;
        }
        else if (Utf8ValidatedInput.IsScalarBoundary(input, byteOffset))
        {
            // Every skipped start is impossible. The first retained row,
            // or the end of an empty table, represents any earlier floor.
            return 0;
        }
        throw new ArgumentOutOfRangeException(nameof(byteOffset), "The start must be a complete scalar boundary.");
    }

    // Exclusive batch owners and failed setup release synchronously. Published
    // value cursors leave this reservation charged for the shared invocation;
    // dropping one view cannot invalidate another copy's table or accounting.
    internal void Release(ref Utf8SliceInvocation invocation)
    {
        if (_reservation == 0) return;
        invocation.Ledger.Release(_reservation);
        _reservation = 0;
    }

    internal static Utf8SliceCountOutcome CountInWindows(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int position, Utf8SliceSlots slots, ref Utf8SliceInvocation invocation)
    {
        Utf8NonBacktrackingSearchState? window = null;
        var boundary = 0;
        var count = 0;
        try
        {
            while (position >= 0)
            {
                var found = FindInWindows(program, input, position, slots, ref window, ref boundary, ref invocation);
                if (!found.Found) return new(count, found.Breach);
                count++;
                position = found.StartByte != found.EndByte ? found.EndByte
                    : Utf8SliceRunner.AdvancePastScalar(input, found.EndByte, false);
            }
            invocation.Budget.ThrowIfCancellationRequested();
            invocation.Budget.CheckDeadline();
            return new(count, Utf8ExecutionLimitKind.None);
        }
        finally { window?.Release(ref invocation); }
    }

    internal static Utf8SliceSearchOutcome FindInWindows(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int position, Utf8SliceSlots slots, ref Utf8NonBacktrackingSearchState? window, ref int boundary,
        ref Utf8SliceInvocation invocation)
    {
        const int windowBytes = 64;
        while (true)
        {
            if (window is null || position >= window._windowEnd && window._windowEnd < input.Length)
            {
                var starts = program.NonBacktrackingOrderedStartBytes;
                if (starts.Count != 0)
                {
                    if (position < input.Length && !starts.Contains(input[position]))
                    {
                        var seek = Utf8SliceRunner.TrySeekRegularStart(input, ref position, starts, ref invocation);
                        if (seek != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, seek);
                    }
                    if (position == input.Length)
                    {
                        invocation.Budget.ThrowIfCancellationRequested();
                        invocation.Budget.CheckDeadline();
                        return new(false, -1, -1, null, null, Utf8ExecutionLimitKind.None);
                    }
                }
                var commit = AlignedEnd(input, position, windowBytes);
                var startEnd = commit;
                if (starts.Count != 0)
                {
                    var scan = invocation.TryChargeInputStep();
                    if (scan != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, scan);
                    // The first byte at position is a candidate. Stop admitting
                    // starts after the last candidate, but skip the whole window.
                    var last = starts.LastIndexOf(input[position..commit]);
                    startEnd = AlignedEnd(input, position + last, 1);
                }
                // A forward-only path can visit each consuming instruction at most once.
                // The commit boundary limits starts, while the remaining rows settle their ends.
                var lookahead = BitOperations.PopCount(program.NonBacktrackingConsumerMask) * 4;
                if (window is null)
                {
                    // Both ends can round up by at most three bytes. An ASCII
                    // window uses the most rows for a given byte capacity.
                    var capacity = windowBytes + lookahead + 2 * 3 + 1;
                    var bytes = Utf8ConstructionBudget.ArrayBytes<int>(capacity)
                        + Utf8ConstructionBudget.ArrayBytes<ulong>(capacity);
                    var admission = invocation.TryReserveWorking(bytes);
                    if (admission != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, admission);
                    try
                    {
                        admission = invocation.TryChargeInputStep();
                        if (admission != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, admission);
                        window = new(new int[capacity], new ulong[capacity], 1, bytes);
                    }
                    finally { if (window is null) invocation.Ledger.Release(bytes); }
                }
                var end = AlignedEnd(input, startEnd, lookahead);
                var build = BuildPackedRangeRows(program, input, position, end, window._boundaries,
                    window._viability, ref invocation, out boundary);
                if (build != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, build);
                window._windowEnd = commit;
                window._startLimit = startEnd == input.Length ? window._boundaries.Length :
                    Array.BinarySearch(window._boundaries, boundary, window._boundaries.Length - boundary, startEnd);
            }
            var found = window.FindFirst(program, ref boundary, slots.Starts, slots.Ends, ref invocation);
            if (found.Found || found.Breach != Utf8ExecutionLimitKind.None || window._windowEnd == input.Length) return found;
            position = window._windowEnd;
        }
    }

    private static int AlignedEnd(ReadOnlySpan<byte> input, int position, int bytes)
    {
        var end = position + Math.Min(bytes, input.Length - position);
        while (end < input.Length && (input[end] & 0xc0) == 0x80) end++;
        return end;
    }

    internal static Utf8SliceSearchOutcome ReplayCompactRange(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        Utf8SliceSearchOutcome selected, Utf8SliceSlots slots, ref Utf8SliceInvocation invocation)
    {
        // ReplayOrderedRegularRange admits at most 32 subject bytes here.
        var from = selected.StartByte;
        var end = selected.EndByte;
        var maxRows = end - from + 1;
        var openingCount = program.NonBacktrackingCaptureOpeningCount;
        var bytes = (ulong)(maxRows * (sizeof(int) + sizeof(ulong)) + openingCount * sizeof(int));
        Array.Fill(slots.Starts, -1);
        Array.Fill(slots.Ends, -1);
        var breach = invocation.TryReserveWorking(bytes);
        if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
        var completed = false;
        try
        {
            Span<int> boundaries = stackalloc int[maxRows];
            Span<ulong> rows = stackalloc ulong[maxRows];
            Span<int> openings = stackalloc int[openingCount];
            openings.Clear();

            breach = BuildPackedRangeRows(program, input, from, end, boundaries, rows, ref invocation,
                out var boundary);
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);

            if ((rows[boundary] & 1) == 0)
                throw new InvalidOperationException("The selected NonBacktracking range is not viable.");
            var outcome = Replay(program, boundaries, rows, 1, ref boundary, slots.Starts, slots.Ends,
                openings, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None) return outcome;
            if (outcome.EndByte != end)
                throw new InvalidOperationException("NonBacktracking capture replay changed the selected range.");
            completed = true;
            return outcome;
        }
        finally
        {
            invocation.Ledger.Release(bytes);
            if (!completed)
            {
                Array.Fill(slots.Starts, -1);
                Array.Fill(slots.Ends, -1);
            }
        }
    }

    private static Utf8ExecutionLimitKind BuildPackedRangeRows(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int from, int end, Span<int> boundaries, Span<ulong> rows, ref Utf8SliceInvocation invocation, out int boundary)
    {
        // Fill backwards from the end so occupied rows are contiguous even
        // when some positions within the byte capacity are not scalar edges.
        boundary = boundaries.Length - 1;
        var scalar = default(Rune);
        var hasConditions = program.NonBacktrackingEpsilonOrder.Length != 0;
        // Conditions use their neighbors in the original subject. The final
        // row still has no consuming continuation out of the selected range.
        if (hasConditions && end < input.Length)
            _ = Rune.DecodeFromUtf8(input[end..], out scalar, out _);
        var following = 0UL;
        for (var edge = end; ; boundary--)
        {
            boundaries[boundary] = edge;
            var width = 0;
            var before = default(Rune);
            if (edge > from || hasConditions && edge > 0)
                _ = Rune.DecodeLastFromUtf8(input[..edge], out before, out width);
            var breach = TryBuildPackedRow(program, input, edge, scalar, before, following, ref invocation, out var row);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            rows[boundary] = row;
            if (edge == from) return Utf8ExecutionLimitKind.None;
            edge -= width;
            scalar = before;
            following = row;
        }
    }

    internal Utf8SliceSearchOutcome FindFirst(Utf8SliceProgram program, ref int boundary,
        int[] captureStarts, int[] captureEnds, ref Utf8SliceInvocation invocation)
    {
        var completed = false;
        ulong reservation = 0;
        Array.Fill(captureStarts, -1);
        Array.Fill(captureEnds, -1);
        try
        {
            var visited = 0;
            for (; boundary < _startLimit; boundary++)
            {
                // A candidate lookup reads one already prepared viability bit.
                if ((visited++ & 63) == 0)
                {
                    var breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                }
                if (CanMatch(boundary, 0)) break;
            }
            if (boundary >= _startLimit)
            {
                invocation.Budget.ThrowIfCancellationRequested();
                invocation.Budget.CheckDeadline();
                return new(false, -1, -1, null, null, Utf8ExecutionLimitKind.None);
            }
            var recordCaptures = captureStarts.Length != 0;
            var openingCount = recordCaptures ? program.NonBacktrackingCaptureOpeningCount : 0;
            // Keep the existing working admission for either storage location.
            var bytes = Utf8ConstructionBudget.ArrayBytes<int>(openingCount);
            var admission = invocation.TryReserveWorking(bytes);
            if (admission != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, admission);
            reservation = bytes;
            admission = invocation.TryChargeInputStep();
            if (admission != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, admission);
            var useStack = openingCount is > 0 and <= 32;
            Span<int> openings = useStack ? stackalloc int[openingCount]
                : openingCount == 0 ? [] : new int[openingCount];
            if (useStack) openings.Clear();
            var outcome = Replay(program, _boundaries, _viability, _stride, ref boundary, captureStarts, captureEnds,
                openings, ref invocation);
            completed = outcome.Found;
            return outcome;
        }
        finally
        {
            invocation.Ledger.Release(reservation);
            if (!completed)
            {
                Array.Fill(captureStarts, -1);
                Array.Fill(captureEnds, -1);
            }
        }
    }

    private static Utf8SliceSearchOutcome Replay(Utf8SliceProgram program, ReadOnlySpan<int> boundaries,
        ReadOnlySpan<ulong> viability, int stride, ref int boundary, int[] captureStarts, int[] captureEnds,
        Span<int> openings, ref Utf8SliceInvocation invocation)
    {
        var recordCaptures = captureStarts.Length != 0;
        var start = boundary;
        var pc = 0;
        while (true)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
            var instruction = program.Instructions[pc];
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Fork:
                    var first = instruction.B == 0 ? pc + 1 : instruction.A;
                    var second = instruction.B == 0 ? instruction.A : pc + 1;
                    var viable = (viability[boundary * stride + (first >> 6)] & (1UL << (first & 63))) != 0;
                    pc = viable ? first : second;
                    break;
                case Utf8SliceInstructionKind.Jump:
                    pc = instruction.A;
                    break;
                case Utf8SliceInstructionKind.CaptureStart:
                    if (recordCaptures) openings[instruction.B] = boundaries[boundary];
                    pc++;
                    break;
                case Utf8SliceInstructionKind.CaptureEnd:
                    if (recordCaptures)
                    {
                        captureStarts[instruction.A] = openings[instruction.B];
                        captureEnds[instruction.A] = boundaries[boundary];
                    }
                    pc++;
                    break;
                case Utf8SliceInstructionKind.Anchor:
                case Utf8SliceInstructionKind.Boundary:
                    pc++;
                    break;
                case Utf8SliceInstructionKind.Accept:
                    invocation.Budget.ThrowIfCancellationRequested();
                    var startByte = boundaries[start];
                    var endByte = boundaries[boundary];
                    boundary += start == boundary ? 1 : 0;
                    return new(true, startByte, endByte, null, null, Utf8ExecutionLimitKind.None);
                default:
                    boundary++;
                    pc++;
                    break;
            }
        }
    }
}
