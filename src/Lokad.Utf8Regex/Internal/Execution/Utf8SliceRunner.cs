using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Resume actions for explicit slice backtrack frames.</summary>
internal enum Utf8SliceResumeKind : byte
{
    Alternation = 0,
    RepeatUnwind = 1,
    RepeatContinue = 2,
    Assertion = 3,
    Atomic = 4,
}

internal struct Utf8SliceFrame
{
    internal Utf8SliceResumeKind ResumeKind;
    internal int Target;
    internal int InputIndex;
    internal int TrailCount;
    internal int Aux;
    internal int Count;
    internal int JournalCount;
}

internal struct Utf8SliceTrailEntry
{
    internal int Slot;
    internal int OldValue;
    internal int NewValue;
    // Keep the journal entry at sixteen bytes as capture markers are added.
    private byte _flags;
    internal bool IsStart { readonly get => (_flags & 1) != 0; set => _flags = (byte)(value ? _flags | 1 : _flags & ~1); }
    internal bool IsHead { readonly get => (_flags & 2) != 0; set => _flags = (byte)(value ? _flags | 2 : _flags & ~2); }
    internal bool IsRestore { readonly get => (_flags & 4) != 0; set => _flags = (byte)(value ? _flags | 4 : _flags & ~4); }
    internal bool IsOpening { readonly get => (_flags & 8) != 0; set => _flags = (byte)(value ? _flags | 8 : _flags & ~8); }
    internal bool IsClosing { readonly get => (_flags & 16) != 0; set => _flags = (byte)(value ? _flags | 16 : _flags & ~16); }
}

internal struct Utf8SliceBalanceEntry
{
    internal int Slot;
    internal int Start;
    internal int End;
    internal int Prev;
}

internal struct Utf8SliceCountMutation
{
    internal int RepeatId;
    internal int OldCount;
    internal int OldPosition;
}

internal readonly struct Utf8SliceMatchOutcome
{
    internal Utf8SliceMatchOutcome(bool isMatch, int endIndex, Utf8ExecutionLimitKind breach, int[][]? captureStarts, int[][]? captureLengths)
    {
        IsMatch = isMatch;
        EndIndex = endIndex;
        Breach = breach;
        CaptureStarts = captureStarts;
        CaptureLengths = captureLengths;
    }

    internal bool IsMatch { get; }
    internal int EndIndex { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
    internal int[][]? CaptureStarts { get; }
    internal int[][]? CaptureLengths { get; }
}

internal readonly struct Utf8SliceUnitOutcome
{
    internal Utf8SliceUnitOutcome(bool found, int startUnit, int endUnit, int startByte, int endByte, Utf8ExecutionLimitKind breach)
    {
        Found = found;
        StartUnit = startUnit;
        EndUnit = endUnit;
        StartByte = startByte;
        EndByte = endByte;
        Breach = breach;
    }

    internal bool Found { get; }
    internal int StartUnit { get; }
    internal int EndUnit { get; }
    internal int StartByte { get; }
    internal int EndByte { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
}

internal readonly struct Utf8SliceSlots
{
    internal Utf8SliceSlots(int[] starts, int[] ends)
    {
        Starts = starts;
        Ends = ends;
    }

    internal int[] Starts { get; }
    internal int[] Ends { get; }
}

internal readonly struct Utf8SliceDetailedOutcome
{
    internal Utf8SliceDetailedOutcome(
        bool found, int startUnit, int endUnit, int startByte, int endByte,
        int[][]? captureStarts, int[][]? captureLengths, Utf8ExecutionLimitKind breach)
    {
        Found = found;
        StartUnit = startUnit;
        EndUnit = endUnit;
        StartByte = startByte;
        EndByte = endByte;
        CaptureStarts = captureStarts;
        CaptureLengths = captureLengths;
        Breach = breach;
    }

    internal bool Found { get; }
    internal int StartUnit { get; }
    internal int EndUnit { get; }
    internal int StartByte { get; }
    internal int EndByte { get; }
    internal int[][]? CaptureStarts { get; }
    internal int[][]? CaptureLengths { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
}

internal readonly struct Utf8SliceCountOutcome
{
    internal Utf8SliceCountOutcome(int count, Utf8ExecutionLimitKind breach)
    {
        Count = count;
        Breach = breach;
    }

    internal int Count { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
}

/// <summary>Explicit-frame backtracking runner over immutable slice programs.
/// One budgeted step per visited instruction; depth is the live frame count.
/// Repeat count/position writes go through a mutation journal so backtracking
/// past one repeat restores sibling repeats to their push-time state; frames,
/// trail chunks, journal chunks, tables, counts, and capture slots are
/// ledger-admitted before acquisition and released on every return path,
/// including breach, cancellation, and disposal. Capture slot pairs hold
/// start/end input indexes, -1 when unset; the trail journal restores them.</summary>
internal static partial class Utf8SliceRunner
{
    internal const int FrameChunkCapacity = 64;
    internal const int TrailChunkCapacity = 128;
    internal const int JournalChunkCapacity = 128;
    internal const int BalanceChunkCapacity = 128;
    internal const int ChunkTableCapacity = 4;

    internal static int FrameReserveBytes => Unsafe.SizeOf<Utf8SliceFrame>() * FrameChunkCapacity;
    internal static int TrailReserveBytes => Unsafe.SizeOf<Utf8SliceTrailEntry>() * TrailChunkCapacity;
    internal static int JournalReserveBytes => Unsafe.SizeOf<Utf8SliceCountMutation>() * JournalChunkCapacity;
    internal static int BalanceReserveBytes => Unsafe.SizeOf<Utf8SliceBalanceEntry>() * BalanceChunkCapacity;

    internal static Utf8SliceSlots RentSlots(int groups, ref Utf8SliceInvocation invocation)
    {
        if (groups == 0)
        {
            return new Utf8SliceSlots(Array.Empty<int>(), Array.Empty<int>());
        }

        if (!invocation.Ledger.TryReserve((ulong)groups * 8))
        {
            throw new InvalidOperationException("Owned slice capture slots breached an unlimited invocation budget.");
        }

        var starts = new int[groups];
        var ends = new int[groups];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        return new Utf8SliceSlots(starts, ends);
    }

    internal static void ReleaseSlots(Utf8SliceSlots slots, ref Utf8SliceInvocation invocation)
    {
        if (slots.Starts.Length != 0)
        {
            invocation.Ledger.Release((ulong)slots.Starts.Length * 8);
        }
    }

    internal static void ReleaseHistory(int[][]? starts, int[][]? lengths, ref Utf8SliceInvocation invocation)
    {
        if (starts is null || lengths is null)
        {
            return;
        }

        invocation.Ledger.Release((ulong)starts.Length * 8);
        invocation.Ledger.Release((ulong)lengths.Length * 8);
        for (var group = 0; group < starts.Length && group < lengths.Length; group++)
        {
            if (starts[group].Length != 0)
            {
                invocation.Ledger.Release((ulong)starts[group].Length * 4);
            }

            if (lengths[group].Length != 0)
            {
                invocation.Ledger.Release((ulong)lengths[group].Length * 4);
            }
        }
    }

    // BMP folding remains shared by ASCII set and reference interoperability.
    private static bool TryFoldBmpScalarForCaseInsensitiveMatch(char scalar, out char folded)
    {
        if (scalar == '\u0130')
        {
            folded = scalar;
            return false;
        }

        folded = char.ToLowerInvariant(scalar);
        return true;
    }

    // Callers pass validated scalar starts; the guards keep byte-stepping
    // attempts fail-closed without revalidating the input.
    private static bool TryDecodeBmpScalar(ReadOnlySpan<byte> input, int index, out char scalar)
    {
        scalar = default;
        if ((uint)index >= (uint)input.Length)
        {
            return false;
        }

        var lead = input[index];
        if (lead < 0x80 || (lead & 0xC0) == 0x80 || lead < 0xC2 || lead >= 0xF0)
        {
            return false;
        }

        if (lead < 0xE0)
        {
            if (index + 1 >= input.Length)
            {
                return false;
            }

            scalar = (char)(((lead & 0x1F) << 6) | (input[index + 1] & 0x3F));
        }
        else
        {
            if (index + 2 >= input.Length)
            {
                return false;
            }

            scalar = (char)(((lead & 0x0F) << 12) | ((input[index + 1] & 0x3F) << 6) | (input[index + 2] & 0x3F));
        }

        return true;
    }

    // Width of the scalar ending at rightEdge (validated input): ASCII is
    // one byte, longer scalars measure back from their lead. Mid-scalar
    // edges and out-of-range positions fail closed so mirrored right-to-left
    // attempts cannot report unaligned starts. Shared by every mirrored
    // consuming arm (six callers, so the single-caller debt ceiling does
    // not apply).
    private static bool TryGetScalarWidthEndingAt(ReadOnlySpan<byte> input, int rightEdge, out int width)
    {
        width = 0;
        if (rightEdge <= 0 || rightEdge > input.Length)
        {
            return false;
        }

        var leadIndex = rightEdge - 1;
        if (input[leadIndex] < 0x80)
        {
            width = 1;
            return true;
        }

        var steps = 0;
        while ((input[leadIndex] & 0xC0) == 0x80)
        {
            leadIndex--;
            steps++;
            if (leadIndex < 0 || steps > 3)
            {
                return false;
            }
        }

        var lead = input[leadIndex];
        if (lead < 0xC2 || lead >= 0xF5)
        {
            return false;
        }

        width = lead < 0xE0 ? 2 : lead < 0xF0 ? 3 : 4;
        return leadIndex + width == rightEdge;
    }

    private static bool TryReadScalar(ReadOnlySpan<byte> input, int edge, bool rightToLeft, out Rune scalar, out int width)
    {
        scalar = default;
        width = 0;
        if (rightToLeft)
        {
            if (!TryGetScalarWidthEndingAt(input, edge, out width)) return false;
            edge -= width;
        }
        if ((uint)edge >= (uint)input.Length) return false;
        if (Rune.DecodeFromUtf8(input.Slice(edge), out scalar, out var decodedWidth) != OperationStatus.Done) return false;
        if (rightToLeft && decodedWidth != width) return false;
        width = decodedWidth;
        return true;
    }

    private static bool IsWordScalarStartingAt(ReadOnlySpan<byte> input, int index, bool includeJoiners, bool ecma)
        => TryReadScalar(input, index, false, out var scalar, out _) &&
            Utf8SliceScalarClasses.IsWord(scalar, includeJoiners, ecma);

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        bool startIsScalarBoundary,
        ref Utf8SliceInvocation invocation)
    {
        return TryMatchAt(program, input, start, captureStarts, captureEnds, false, startIsScalarBoundary, ref invocation);
    }

    internal static Utf8SliceUnitOutcome TryMatchFromUnitNonEmpty(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int unit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        if (unit < 0 || unit > map.Utf16Length)
        {
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
        }

        var startBoundary = map.Resolve(unit);
        if (!startBoundary.IsScalarBoundary)
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
        var startByte = startBoundary.ByteOffset;
        var outcome = TryMatchAt(program, input, startByte, captureStarts, captureEnds, false, true, startBoundary.IsScalarBoundary, ref invocation);
        if (outcome.Breach != Utf8ExecutionLimitKind.None || !outcome.IsMatch)
        {
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, outcome.Breach);
        }

        var endUnit = map.GetUtf16OffsetForByteOffset(outcome.EndIndex);
        return new Utf8SliceUnitOutcome(true, unit, endUnit, startByte, outcome.EndIndex, Utf8ExecutionLimitKind.None);
    }

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        bool collectHistory,
        bool startIsScalarBoundary,
        ref Utf8SliceInvocation invocation)
    {
        return TryMatchAt(program, input, start, captureStarts, captureEnds, collectHistory, false, startIsScalarBoundary, ref invocation);
    }

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        bool collectHistory,
        bool requireNonEmpty,
        bool startIsScalarBoundary,
        ref Utf8SliceInvocation invocation)
    {
        if (!startIsScalarBoundary || (uint)start > (uint)input.Length ||
            start < input.Length && (input[start] & 0xC0) == 0x80)
            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None, null, null);

        var counts = RentIntArray(program.RepeatCount, ref invocation, out var breach);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            return new Utf8SliceMatchOutcome(false, -1, breach, null, null);
        }

        var positions = RentIntArray(program.RepeatCount, ref invocation, out breach);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            ReleaseIntArray(counts, ref invocation);
            return new Utf8SliceMatchOutcome(false, -1, breach, null, null);
        }

        Array.Fill(counts, -1);
        Array.Fill(captureStarts, -1);
        Array.Fill(captureEnds, -1);
        var heads = RentIntArray(program.GroupCount, ref invocation, out breach);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            ReleaseIntArray(positions, ref invocation);
            ReleaseIntArray(counts, ref invocation);
            return new Utf8SliceMatchOutcome(false, -1, breach, null, null);
        }

        Array.Fill(heads, -1);
        var stacks = new ChunkedStacks();
        var completed = false;
        try
        {
            var attempt = RunFrom(program, input, start, captureStarts, captureEnds, counts, positions, heads, collectHistory, requireNonEmpty, startIsScalarBoundary, ref stacks, ref invocation);
            if (attempt.IsMatch && program.RightToLeft)
            {
                // Mirrored runs record right edges at capture starts and left
                // edges at capture ends; projection and backreferences read
                // normalized spans while the trail stays raw and consistent.
                for (var slot = 0; slot < captureStarts.Length; slot++)
                {
                    if (captureStarts[slot] >= 0 && captureEnds[slot] >= 0 && captureEnds[slot] < captureStarts[slot])
                    {
                        var normalizedStart = captureEnds[slot];
                        captureEnds[slot] = captureStarts[slot];
                        captureStarts[slot] = normalizedStart;
                    }
                }
            }

            completed = attempt.IsMatch;
            return attempt;
        }
        finally
        {
            if (!completed)
            {
                // A committed positive assertion still belongs to this attempt;
                // failure or cancellation must not publish its capture slots.
                Array.Fill(captureStarts, -1);
                Array.Fill(captureEnds, -1);
            }
            stacks.ReleaseAll(ref invocation);
            ReleaseIntArray(heads, ref invocation);
            ReleaseIntArray(positions, ref invocation);
            ReleaseIntArray(counts, ref invocation);
        }
    }

    internal static Utf8SliceUnitOutcome TryMatchFromUnit(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int unit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        if (unit < 0 || unit > map.Utf16Length)
        {
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
        }

        var startBoundary = map.Resolve(unit);
        if (!startBoundary.IsScalarBoundary)
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
        var startByte = startBoundary.ByteOffset;
        var outcome = TryMatchAt(program, input, startByte, captureStarts, captureEnds, startBoundary.IsScalarBoundary, ref invocation);
        if (outcome.Breach != Utf8ExecutionLimitKind.None || !outcome.IsMatch)
        {
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, outcome.Breach);
        }

        if (outcome.EndIndex == startByte)
        {
            return new Utf8SliceUnitOutcome(true, unit, unit, startByte, startByte, Utf8ExecutionLimitKind.None);
        }

        var endUnit = map.GetUtf16OffsetForByteOffset(outcome.EndIndex);
        return new Utf8SliceUnitOutcome(true, unit, endUnit, startByte, outcome.EndIndex, Utf8ExecutionLimitKind.None);
    }

    internal static Utf8ValueMatch ProjectMatch(Utf8BoundaryMap map, Utf8SliceUnitOutcome found)
    {
        var startBoundary = map.Resolve(found.StartUnit);
        var endBoundary = map.Resolve(found.EndUnit);
        var aligned = startBoundary.IsScalarBoundary && endBoundary.IsScalarBoundary;
        return new Utf8ValueMatch(
            true,
            aligned,
            found.StartUnit,
            found.EndUnit - found.StartUnit,
            startBoundary.ByteOffset,
            endBoundary.ByteOffset - startBoundary.ByteOffset);
    }

    // Right-edge attempt for right-to-left scans: runs the mirrored program
    // from the byte edge closing endUnit and reports the hit in unit space.
    // Zero-width hits report (endUnit, endUnit) without byte mapping so
    // unaligned edges stay exact; other hits map their left edge, which mirrored
    // consumption leaves on scalar starts for served programs. Shared by the
    // three scan drivers plus the operation cursor (four callers, so the
    // single-caller debt ceiling does not apply).
    internal static Utf8SliceUnitOutcome TryMatchAtRightEdgeUnit(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int endUnit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        if (endUnit < 0 || endUnit > map.Utf16Length)
        {
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
        }

        var edgeByte = endUnit >= map.Utf16Length ? input.Length : map.Resolve(endUnit).ByteOffset;
        var edgeClean = endUnit >= map.Utf16Length || endUnit <= 0 || map.Resolve(endUnit).IsScalarBoundary;
        if (!edgeClean)
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
        var outcome = TryMatchAt(program, input, edgeByte, captureStarts, captureEnds, edgeClean, ref invocation);
        if (outcome.Breach != Utf8ExecutionLimitKind.None || !outcome.IsMatch)
        {
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, outcome.Breach);
        }

        if (outcome.EndIndex == edgeByte)
        {
            return new Utf8SliceUnitOutcome(true, endUnit, endUnit, edgeByte, edgeByte, Utf8ExecutionLimitKind.None);
        }

        var startUnit = map.GetUtf16OffsetForByteOffset(outcome.EndIndex);
        var closeUnit = map.GetUtf16OffsetForByteOffset(edgeByte);
        return new Utf8SliceUnitOutcome(true, startUnit, closeUnit, outcome.EndIndex, edgeByte, Utf8ExecutionLimitKind.None);
    }

    internal static Utf8SliceUnitOutcome TryFindFirst(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int fromUnit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        invocation.Budget.ThrowIfCancellationRequested();

        // Right-to-left scans enumerate closing edges from the search bound
        // down (BCL runtextstart), so the rightmost match wins like the oracle.
        if (program.RightToLeft)
        {
            for (var unit = Math.Min(fromUnit, map.Utf16Length); unit >= 0; unit--)
            {
                var outcome = TryMatchAtRightEdgeUnit(program, input, map, unit, captureStarts, captureEnds, ref invocation);
                if (outcome.Breach != Utf8ExecutionLimitKind.None || outcome.Found)
                {
                    return outcome;
                }
            }

            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
        }

        for (var unit = fromUnit; unit <= map.Utf16Length; unit++)
        {
            var outcome = TryMatchFromUnit(program, input, map, unit, captureStarts, captureEnds, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None || outcome.Found)
            {
                return outcome;
            }
        }

        return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
    }

    internal static Utf8SliceMatchOutcome IsMatch(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        var outcome = TryFindFirst(program, input, map, 0, captureStarts, captureEnds, ref invocation);
        if (outcome.Breach != Utf8ExecutionLimitKind.None)
        {
            return new Utf8SliceMatchOutcome(false, -1, outcome.Breach, null, null);
        }

        return outcome.Found
            ? new Utf8SliceMatchOutcome(true, outcome.EndByte, Utf8ExecutionLimitKind.None, null, null)
            : new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None, null, null);
    }

    internal static Utf8SliceCountOutcome CountFrom(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int fromUnit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        // Right-to-left counting chains closing edges down from the search
        // bound; contiguous matches pin \\G to each scan start, stop at the
        // first scan miss, and step past empty matches by one unit (later edges
        // all miss the pin, so an empty match ends the chain).
        if (program.RightToLeft)
        {
            if (program.HasContiguousAnchor)
            {
                var contiguousCount = 0;
                var contiguousUnit = Math.Min(fromUnit, map.Utf16Length);
                var contiguousPin = contiguousUnit >= 0 && contiguousUnit <= map.Utf16Length ? map.Resolve(contiguousUnit).ByteOffset : -1;
                while (contiguousUnit >= 0)
                {
                    invocation.AnchorOriginByte = contiguousPin;
                    var contiguousScan = TryMatchAtRightEdgeUnit(program, input, map, contiguousUnit, captureStarts, captureEnds, ref invocation);
                    if (contiguousScan.Breach != Utf8ExecutionLimitKind.None)
                    {
                        return new Utf8SliceCountOutcome(contiguousCount, contiguousScan.Breach);
                    }

                    if (!contiguousScan.Found)
                    {
                        break;
                    }

                    contiguousCount++;
                    contiguousPin = contiguousScan.StartByte;
                    contiguousUnit = contiguousScan.EndUnit > contiguousScan.StartUnit ? contiguousScan.StartUnit : contiguousScan.StartUnit - 1;
                }

                return new Utf8SliceCountOutcome(contiguousCount, Utf8ExecutionLimitKind.None);
            }

            if (fromUnit >= 0 && fromUnit <= map.Utf16Length)
            {
                invocation.AnchorOriginByte = map.Resolve(Math.Min(fromUnit, map.Utf16Length)).ByteOffset;
            }

            var rtlCount = 0;
            var rtlUnit = Math.Min(fromUnit, map.Utf16Length);
            while (rtlUnit >= 0)
            {
                var rtlOutcome = TryMatchAtRightEdgeUnit(program, input, map, rtlUnit, captureStarts, captureEnds, ref invocation);
                if (rtlOutcome.Breach != Utf8ExecutionLimitKind.None)
                {
                    return new Utf8SliceCountOutcome(rtlCount, rtlOutcome.Breach);
                }

                if (!rtlOutcome.Found)
                {
                    rtlUnit--;
                    continue;
                }

                rtlCount++;
                rtlUnit = rtlOutcome.EndUnit > rtlOutcome.StartUnit ? rtlOutcome.StartUnit : rtlOutcome.EndUnit - 1;
                // Contiguous chains read the advanced origin on nested runs;
                // misses keep their scan start.
                if (rtlOutcome.StartUnit >= 0 && rtlOutcome.StartUnit <= map.Utf16Length)
                {
                    invocation.AnchorOriginByte = map.Resolve(rtlOutcome.StartUnit).ByteOffset;
                }
            }

            return new Utf8SliceCountOutcome(rtlCount, Utf8ExecutionLimitKind.None);
        }

        if (program.HasContiguousAnchor)
        {
            // Contiguous matches chain from the search start: each scan pins
            // \\G to its own start (BCL startat), stops at the first scan miss, and
            // advances past empty matches by one unit (later positions all miss
            // the pin, so an empty match ends the chain).
            var scanCount = 0;
            var scanUnit = fromUnit;
            var pinByte = fromUnit >= 0 && fromUnit <= map.Utf16Length ? map.Resolve(fromUnit).ByteOffset : -1;
            while (true)
            {
                invocation.AnchorOriginByte = pinByte;
                var scan = TryFindFirst(program, input, map, scanUnit, captureStarts, captureEnds, ref invocation);
                if (scan.Breach != Utf8ExecutionLimitKind.None)
                {
                    return new Utf8SliceCountOutcome(scanCount, scan.Breach);
                }

                if (!scan.Found)
                {
                    break;
                }

                scanCount++;
                pinByte = scan.EndByte;
                scanUnit = scan.EndUnit > scan.StartUnit ? scan.EndUnit : scan.StartUnit + 1;
            }

            return new Utf8SliceCountOutcome(scanCount, Utf8ExecutionLimitKind.None);
        }

        if (fromUnit >= 0 && fromUnit <= map.Utf16Length)
        {
            invocation.AnchorOriginByte = map.Resolve(fromUnit).ByteOffset;
        }

        var count = 0;
        var unit = fromUnit;
        while (unit <= map.Utf16Length)
        {
            var outcome = TryMatchFromUnit(program, input, map, unit, captureStarts, captureEnds, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                return new Utf8SliceCountOutcome(count, outcome.Breach);
            }

            if (!outcome.Found)
            {
                unit++;
                continue;
            }

            count++;
            unit = outcome.EndUnit > outcome.StartUnit ? outcome.EndUnit : outcome.StartUnit + 1;
            // Contiguous matches chain BCL startat (nested lookbehind \\G reads
            // the advanced origin); misses keep their scan start.
            if (outcome.EndUnit >= 0 && outcome.EndUnit <= map.Utf16Length)
            {
                invocation.AnchorOriginByte = map.Resolve(outcome.EndUnit).ByteOffset;
            }
        }

        return new Utf8SliceCountOutcome(count, Utf8ExecutionLimitKind.None);
    }

    internal static Utf8SliceCountOutcome Count(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        return CountFrom(program, input, map, 0, captureStarts, captureEnds, ref invocation);
    }

    internal static Utf8SliceDetailedOutcome TryFindFirstDetailed(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int fromUnit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        invocation.Budget.ThrowIfCancellationRequested();

        // Right-to-left detailed search mirrors the unit scan above through
        // closing edges; histories materialize normalized at Accept.
        if (program.RightToLeft)
        {
            return TryFindFirstDetailedRtl(program, input, map, fromUnit, captureStarts, captureEnds, ref invocation);

            static Utf8SliceDetailedOutcome TryFindFirstDetailedRtl(
                Utf8SliceProgram rtlProgram,
                ReadOnlySpan<byte> rtlInput,
                Utf8BoundaryMap rtlMap,
                int rtlFromUnit,
                int[] rtlStarts,
                int[] rtlEnds,
                ref Utf8SliceInvocation rtlInvocation)
            {
                for (var rtlUnit = Math.Min(rtlFromUnit, rtlMap.Utf16Length); rtlUnit >= 0; rtlUnit--)
                {
                    var rtlEdgeByte = rtlUnit >= rtlMap.Utf16Length ? rtlInput.Length : rtlMap.Resolve(rtlUnit).ByteOffset;
                    var rtlEdgeClean = rtlUnit >= rtlMap.Utf16Length || rtlUnit <= 0 || rtlMap.Resolve(rtlUnit).IsScalarBoundary;
                    if (!rtlEdgeClean) continue;
                    var rtlOutcome = TryMatchAt(rtlProgram, rtlInput, rtlEdgeByte, rtlStarts, rtlEnds, true, rtlEdgeClean, ref rtlInvocation);
                    if (rtlOutcome.Breach != Utf8ExecutionLimitKind.None)
                    {
                        return new Utf8SliceDetailedOutcome(false, -1, -1, -1, -1, null, null, rtlOutcome.Breach);
                    }

                    if (!rtlOutcome.IsMatch)
                    {
                        continue;
                    }

                    if (rtlOutcome.EndIndex == rtlEdgeByte)
                    {
                        return new Utf8SliceDetailedOutcome(
                            true, rtlUnit, rtlUnit, rtlEdgeByte, rtlEdgeByte,
                            rtlOutcome.CaptureStarts, rtlOutcome.CaptureLengths, Utf8ExecutionLimitKind.None);
                    }

                    var rtlStartUnit = rtlMap.GetUtf16OffsetForByteOffset(rtlOutcome.EndIndex);
                    var rtlCloseUnit = rtlMap.GetUtf16OffsetForByteOffset(rtlEdgeByte);
                    return new Utf8SliceDetailedOutcome(
                        true, rtlStartUnit, rtlCloseUnit, rtlOutcome.EndIndex, rtlEdgeByte,
                        rtlOutcome.CaptureStarts, rtlOutcome.CaptureLengths, Utf8ExecutionLimitKind.None);
                }

                return new Utf8SliceDetailedOutcome(false, -1, -1, -1, -1, null, null, Utf8ExecutionLimitKind.None);
            }
        }

        for (var unit = fromUnit; unit <= map.Utf16Length; unit++)
        {
            var startBoundary = map.Resolve(unit);
            if (!startBoundary.IsScalarBoundary) continue;
            var startByte = startBoundary.ByteOffset;
            var outcome = TryMatchAt(program, input, startByte, captureStarts, captureEnds, true, startBoundary.IsScalarBoundary, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                return new Utf8SliceDetailedOutcome(false, -1, -1, -1, -1, null, null, outcome.Breach);
            }

            if (!outcome.IsMatch)
            {
                continue;
            }

            if (outcome.EndIndex == startByte)
            {
                return new Utf8SliceDetailedOutcome(
                    true, unit, unit, startByte, startByte,
                    outcome.CaptureStarts, outcome.CaptureLengths, Utf8ExecutionLimitKind.None);
            }

            var endUnit = map.GetUtf16OffsetForByteOffset(outcome.EndIndex);
            return new Utf8SliceDetailedOutcome(
                true, unit, endUnit, startByte, outcome.EndIndex,
                outcome.CaptureStarts, outcome.CaptureLengths, Utf8ExecutionLimitKind.None);
        }

        return new Utf8SliceDetailedOutcome(false, -1, -1, -1, -1, null, null, Utf8ExecutionLimitKind.None);
    }

    private static Utf8SliceMatchOutcome RunSegment(
        ReadOnlySpan<byte> input, int[] captureStarts, int[] captureEnds, int[] balanceHeads,
        ref AssertionCursor cursor, bool resumeFailure,
        ref ChunkedStacks stacks, ref Utf8SliceInvocation invocation, out bool suspended)
    {
        suspended = false;
        var program = cursor.Program;
        var start = cursor.Start;
        var counts = cursor.Counts;
        var positions = cursor.Positions;
        var collectHistory = cursor.CollectHistory;
        var requireNonEmpty = cursor.RequireNonEmpty;
        var startIsScalarBoundary = cursor.Aligned;
        var frameBase = cursor.FrameBase;
        var instructions = program.Instructions;
        var programIndex = cursor.ProgramIndex;
        var inputIndex = cursor.InputIndex;
        while (true)
        {
            var failed = resumeFailure;
            if (resumeFailure)
            {
                resumeFailure = false;
                goto Backtrack;
            }
            var stepBreach = invocation.TryChargeStep();
            if (stepBreach != Utf8ExecutionLimitKind.None)
            {
                return new Utf8SliceMatchOutcome(false, -1, stepBreach, null, null);
            }

            var instruction = instructions[programIndex];
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Literal:
                    // Literal atoms consume one complete scalar in either direction.
                    var literalIndex = inputIndex;
                    var literalWidth = 0;
                    if (program.RightToLeft)
                    {
                        if (TryGetScalarWidthEndingAt(input, inputIndex, out literalWidth))
                            literalIndex -= literalWidth;
                    }
                    else if (inputIndex < input.Length && (input[inputIndex] & 0xC0) != 0x80)
                    {
                        var lead = input[inputIndex];
                        literalWidth = lead < 0x80 ? 1 : lead < 0xE0 ? 2 : lead < 0xF0 ? 3 : 4;
                    }

                    if (literalWidth == 0)
                    {
                        failed = true;
                        break;
                    }

                    var literalLead = input[literalIndex];
                    bool literalMatches;
                    if (literalLead < 0x80 && instruction.A < 0x80)
                    {
                        // Retain the byte-only ASCII fast path.
                        literalMatches = instruction.B == 0
                            ? literalLead == instruction.A
                            : Internal.Search.AsciiSearch.FoldCase(literalLead) ==
                                Internal.Search.AsciiSearch.FoldCase((byte)instruction.A);
                    }
                    else
                    {
                        if (Rune.DecodeFromUtf8(input.Slice(literalIndex), out var literalScalar, out _) != OperationStatus.Done)
                        {
                            failed = true;
                            break;
                        }

                        literalMatches = literalScalar.Value == instruction.A;
                        if (!literalMatches && instruction.B != 0)
                        {
                            literalMatches = literalScalar.IsBmp && instruction.A <= char.MaxValue
                                ? Utf8SliceCaseEquivalences.GetInvariantEquivalents((char)literalScalar.Value)
                                    .Contains((char)instruction.A)
                                : Rune.IsValid(instruction.A) && Rune.ToUpperInvariant(literalScalar) ==
                                    Rune.ToUpperInvariant(new Rune(instruction.A));
                        }
                    }

                    if (literalMatches)
                    {
                        inputIndex += program.RightToLeft ? -literalWidth : literalWidth;
                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }
                    break;
                case Utf8SliceInstructionKind.Set:
                    // Mirrored classes test the scalar ending at the attempt edge with
                    // the same mask and fold rules as the forward arm.
                    if (program.RightToLeft)
                    {
                        if (TryGetScalarWidthEndingAt(input, inputIndex, out var rtlSetWidth))
                        {
                            var setCandidate = input[inputIndex - rtlSetWidth];
                            var setInMask = false;
                            if (setCandidate < 0x80)
                            {
                                var setTest = instruction.C != 0 ? Internal.Search.AsciiSearch.FoldCase(setCandidate) : setCandidate;
                                setInMask = (program.SetMasks[instruction.A][setTest >> 3] & (byte)(1 << (setTest & 7))) != 0;
                            }
                            else if (instruction.C != 0 &&
                                setCandidate < 0xF0 &&
                                TryDecodeBmpScalar(input, inputIndex - rtlSetWidth, out var rtlSetScalar) &&
                                TryFoldBmpScalarForCaseInsensitiveMatch(rtlSetScalar, out var rtlSetFolded) &&
                                rtlSetFolded < 0x80)
                            {
                                setInMask = (program.SetMasks[instruction.A][rtlSetFolded >> 3] & (byte)(1 << (rtlSetFolded & 7))) != 0;
                            }

                            if (setInMask != (instruction.B != 0))
                            {
                                inputIndex -= rtlSetWidth;
                                programIndex++;
                            }
                            else
                            {
                                failed = true;
                            }
                        }
                        else
                        {
                            failed = true;
                        }

                        break;
                    }

                    // Positive classes match one ASCII member byte. Negated
                    // classes match one ASCII non-member byte or one whole
                    // non-ASCII scalar (BMP units agree; astral inputs are
                    // declined at routing). Continuation bytes fail, so
                    // byte-stepping drivers cannot report unaligned starts.
                    // Inputs are validated before execution, hence lead
                    // widths stay in bounds without revalidation here.
                    if (inputIndex < input.Length &&
                        (input[inputIndex] & 0xC0) != 0x80)
                    {
                        var candidate = input[inputIndex];
                        // Case-insensitive classes fold the candidate before the mask
                        // test, so non-ASCII scalars with ASCII folds (U+212A) hit exactly
                        // like the BCL oracle; anything else keeps the raw membership.
                        var inMask = false;
                        if (candidate < 0x80)
                        {
                            var test = instruction.C != 0 ? Internal.Search.AsciiSearch.FoldCase(candidate) : candidate;
                            inMask = (program.SetMasks[instruction.A][test >> 3] & (byte)(1 << (test & 7))) != 0;
                        }
                        else if (instruction.C != 0 &&
                            candidate < 0xF0 &&
                            TryDecodeBmpScalar(input, inputIndex, out var setScalar) &&
                            TryFoldBmpScalarForCaseInsensitiveMatch(setScalar, out var foldedSet) &&
                            foldedSet < 0x80)
                        {
                            inMask = (program.SetMasks[instruction.A][foldedSet >> 3] & (byte)(1 << (foldedSet & 7))) != 0;
                        }
                        if (inMask != (instruction.B != 0))
                        {
                            if (candidate < 0x80)
                            {
                                inputIndex++;
                            }
                            else if (candidate < 0xE0)
                            {
                                inputIndex += 2;
                            }
                            else if (candidate < 0xF0)
                            {
                                inputIndex += 3;
                            }
                            else
                            {
                                inputIndex += 4;
                            }

                            programIndex++;
                        }
                        else
                        {
                            failed = true;
                        }
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Fork:
                    if (!stacks.PushFrame(
                        new Utf8SliceFrame
                        {
                            ResumeKind = instruction.B == 0 ? Utf8SliceResumeKind.Alternation : instruction.B == 1 ? Utf8SliceResumeKind.Assertion : Utf8SliceResumeKind.Atomic,
                            Target = instruction.A,
                            InputIndex = inputIndex,
                            TrailCount = stacks.TrailCount,
                            JournalCount = stacks.JournalCount,
                        },
                        ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                    }

                    if (invocation.Budget.TryNoteDepth((uint)stacks.FrameCount) != Utf8ExecutionLimitKind.None)
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.Depth, null, null);
                    }

                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.Jump:
                    programIndex = instruction.A;
                    break;
                case Utf8SliceInstructionKind.RepeatGreedy:
                case Utf8SliceInstructionKind.RepeatLazy:
                    {
                        var repeatId = instruction.B;
                        var count = counts[repeatId];
                        if (count < 0)
                        {
                            if (!SetRepeat(counts, positions, repeatId, 0, -1, ref stacks, ref invocation))
                            {
                                return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                            }

                            count = 0;
                        }

                        var madeNoProgress = count > 0 && positions[repeatId] == inputIndex;
                        var minimum = instruction.C;
                        var maximum = instruction.D;
                        var greedy = instruction.Kind != Utf8SliceInstructionKind.RepeatLazy;
                        // Repetition counts atom iterations, independently of UTF-8 or UTF-16 width.
                        if (count < minimum)
                        {
                            if (!SetRepeat(counts, positions, repeatId, count + 1, inputIndex, ref stacks, ref invocation))
                            {
                                return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                            }

                            programIndex++;
                            break;
                        }

                        if (count >= maximum || madeNoProgress)
                        {
                            programIndex = instruction.A;
                            break;
                        }

                        var frame = new Utf8SliceFrame
                        {
                            InputIndex = inputIndex,
                            TrailCount = stacks.TrailCount,
                            JournalCount = stacks.JournalCount,
                            Aux = repeatId,
                        };
                        if (greedy)
                        {
                            frame.ResumeKind = Utf8SliceResumeKind.RepeatUnwind;
                            frame.Target = instruction.A;
                            frame.Count = count;
                        }
                        else
                        {
                            frame.ResumeKind = Utf8SliceResumeKind.RepeatContinue;
                            frame.Target = programIndex + 1;
                            frame.Count = count + 1;
                        }

                        if (!stacks.PushFrame(frame, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        }

                        if (invocation.Budget.TryNoteDepth((uint)stacks.FrameCount) != Utf8ExecutionLimitKind.None)
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.Depth, null, null);
                        }

                        if (greedy)
                        {
                            if (!SetRepeat(counts, positions, repeatId, count + 1, inputIndex, ref stacks, ref invocation))
                            {
                                return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                            }

                            programIndex++;
                        }
                        else
                        {
                            programIndex = instruction.A;
                        }

                        break;
                    }

                case Utf8SliceInstructionKind.RepeatEnd:
                    programIndex = instruction.A;
                    break;
                case Utf8SliceInstructionKind.RepeatExit:
                    if (!SetRepeat(counts, positions, instruction.A, -1, -1, ref stacks, ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                    }

                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.CaptureStart:
                    if (program.UsesCaptureOpeningJournal)
                    {
                        if (!stacks.PushTrailEntry(new Utf8SliceTrailEntry { Slot = instruction.A, NewValue = inputIndex, IsOpening = true }, ref invocation))
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        programIndex++;
                        break;
                    }
                    if (!stacks.PushTrail(instruction.A, captureStarts[instruction.A], inputIndex, true, ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                    }

                    captureStarts[instruction.A] = inputIndex;
                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.CaptureEnd:
                    if (program.UsesCaptureOpeningJournal)
                    {
                        // An opening capture does not overwrite the last completed one.
                        // This preserves nested aliases and references inside their bodies.
                        if (!stacks.TryFindCaptureOpening(instruction.A, ref invocation, out var opening, out var openingBreach))
                            return new Utf8SliceMatchOutcome(false, -1, openingBreach, null, null);
                        if (!stacks.PushRestoreTrail(instruction.A, captureStarts[instruction.A], opening, captureEnds[instruction.A], inputIndex, ref invocation) ||
                            !stacks.PushTrailEntry(new Utf8SliceTrailEntry { Slot = instruction.A, OldValue = opening, NewValue = inputIndex, IsClosing = true }, ref invocation))
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        captureStarts[instruction.A] = opening;
                        captureEnds[instruction.A] = inputIndex;
                    }
                    else
                    {
                        if (!stacks.PushTrail(instruction.A, captureEnds[instruction.A], inputIndex, false, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        }
                        captureEnds[instruction.A] = inputIndex;
                    }
                    if (program.HasBalancing)
                    {
                        // Balancing stacks mirror every capture so pops can
                        // restore previous tops; the head journal makes the
                        // push vanish on backtrack like any trail write.
                        if (!stacks.PushBalance(instruction.A, captureStarts[instruction.A], inputIndex, balanceHeads[instruction.A], ref invocation) ||
                            !stacks.PushHeadTrail(instruction.A, balanceHeads[instruction.A], stacks.BalanceCount - 1, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        }

                        balanceHeads[instruction.A] = stacks.BalanceCount - 1;
                    }

                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.Dot:
                    // Mirrored dots consume the scalar ending at the attempt edge.
                    if (program.RightToLeft)
                    {
                        if (TryGetScalarWidthEndingAt(input, inputIndex, out var rtlDotWidth) &&
                            (instruction.A != 0 || input[inputIndex - 1] != (byte)'\n'))
                        {
                            inputIndex -= rtlDotWidth;
                            programIndex++;
                        }
                        else
                        {
                            failed = true;
                        }

                        break;
                    }

                    // Dots consume one scalar: ASCII and BMP leads take
                    // their scalar, astral leads take all four bytes.
                    // Continuation bytes fail the attempt, so byte-stepping
                    // drivers cannot report unaligned starts. Inputs are
                    // validated before execution, hence lead widths stay
                    // in bounds without revalidation here. Unit-exact
                    // consumers decline astral inputs at routing, where
                    // scalar and unit semantics diverge.
                    if (inputIndex < input.Length &&
                        (instruction.A != 0 || input[inputIndex] != (byte)'\n') &&
                        (input[inputIndex] & 0xC0) != 0x80)
                    {
                        var lead = input[inputIndex];
                        if (lead < 0x80)
                        {
                            inputIndex++;
                        }
                        else if (lead < 0xE0)
                        {
                            inputIndex += 2;
                        }
                        else if (lead < 0xF0)
                        {
                            inputIndex += 3;
                        }
                        else
                        {
                            inputIndex += 4;
                        }

                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Anchor:
                    // Zero-width edges: beginning holds at the input origin
                    // or (multiline `^`) just after a line feed; lax end
                    // (`$`, `\Z`) holds at the input end, before a trailing
                    // line feed, or (multiline `$`) just before any line
                    // feed; strict end (`\z`) holds only at the input end.
                    // Line feeds are single bytes, so these checks are
                    // encoding-indifferent and need no astral routing.
                    // Guards order short-circuit before any indexing.
                    var anchorMultiline = instruction.B != 0;
                    bool anchorHolds;
                    if (instruction.A == 0)
                    {
                        // Origin assertions additionally require a
                        // scalar-aligned attempt: a low-surrogate unit maps
                        // to byte zero, where the oracle never matches `^`.
                        anchorHolds = (inputIndex == 0 && startIsScalarBoundary) ||
                            (anchorMultiline && inputIndex > 0 && input[inputIndex - 1] == (byte)'\n');
                    }
                    else if (instruction.A == 2)
                    {
                        anchorHolds = inputIndex == input.Length;
                    }
                    else if (instruction.A == 3)
                    {
                        // Contiguous matches pin to the scan start (BCL startat),
                        // defaulting to the attempt start when no driver set an origin.
                        var origin = invocation.AnchorOriginByte;
                        anchorHolds = inputIndex == (origin < 0 ? start : origin);
                    }
                    else if (inputIndex == input.Length)
                    {
                        anchorHolds = true;
                    }
                    else if (anchorMultiline)
                    {
                        anchorHolds = input[inputIndex] == (byte)'\n';
                    }
                    else
                    {
                        anchorHolds = inputIndex + 1 == input.Length && input[inputIndex] == (byte)'\n';
                    }

                    if (anchorHolds)
                    {
                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Boundary:
                    // Zero-width word edges: `\b` holds on a word/non-word
                    // transition, `\B` where both sides agree. Both require
                    // scalar-aligned positions and classify complete neighbors.
                    if (!startIsScalarBoundary)
                    {
                        failed = true;
                    }
                    else if (inputIndex <= input.Length &&
                        (inputIndex == input.Length || (input[inputIndex] & 0xC0) != 0x80))
                    {
                        var beforeWord = false;
                        if (inputIndex > 0)
                        {
                            var previous = inputIndex - 1;
                            while (previous > 0 && (input[previous] & 0xC0) == 0x80)
                            {
                                previous--;
                            }

                            beforeWord = IsWordScalarStartingAt(input, previous, true, instruction.B != 0);
                        }

                        var afterWord = inputIndex < input.Length && IsWordScalarStartingAt(input, inputIndex, true, instruction.B != 0);
                        if ((beforeWord != afterWord) == (instruction.A != 0))
                        {
                            programIndex++;
                        }
                        else
                        {
                            failed = true;
                        }
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Shorthand:
                case Utf8SliceInstructionKind.Category:
                case Utf8SliceInstructionKind.CategorySet:
                case Utf8SliceInstructionKind.Hybrid:
                    {
                        if (!TryReadScalar(input, inputIndex, program.RightToLeft, out var classScalar, out var classWidth))
                        {
                            failed = true;
                            break;
                        }

                        bool classHit;
                        if (instruction.Kind == Utf8SliceInstructionKind.Shorthand)
                        {
                            classHit = Utf8SliceScalarClasses.MatchesShorthand(classScalar, (Utf8SliceShorthand)instruction.A);
                        }
                        else if (instruction.Kind == Utf8SliceInstructionKind.Category)
                        {
                            classHit = Utf8SliceScalarClasses.MatchesTable(classScalar, program.CategoryTables[instruction.A],
                                (instruction.B & 2) != 0, (instruction.B & 1) != 0);
                        }
                        else
                        {
                            var categorySet = instruction.Kind == Utf8SliceInstructionKind.CategorySet;
                            var classMembers = categorySet ? program.CategorySetMembers[instruction.D] : null;
                            if (classMembers is { Length: 1 } && classMembers[0].Table.ClassProgram is { } classProgram)
                            {
                                classHit = classProgram.TryMatch(classScalar, ref invocation, out var classBreach);
                                if (classBreach != Utf8ExecutionLimitKind.None)
                                    return new Utf8SliceMatchOutcome(false, -1, classBreach, null, null);
                            }
                            else
                            {
                                var fold = categorySet ? (instruction.B & 2) != 0 : instruction.D != 0;
                                var memberHit = Utf8SliceScalarClasses.MatchesMask(classScalar, program.SetMasks[instruction.A], fold) ||
                                    Utf8SliceScalarClasses.MatchesMembers(classScalar, instruction.C);
                                if (!memberHit && classMembers is not null)
                                {
                                    foreach (var member in classMembers)
                                    {
                                        if (Utf8SliceScalarClasses.MatchesTable(classScalar, member.Table, fold, member.Negated))
                                        {
                                            memberHit = true;
                                            break;
                                        }
                                    }
                                }
                                classHit = memberHit != ((instruction.B & 1) != 0);
                            }
                        }

                        if (classHit)
                        {
                            inputIndex += program.RightToLeft ? -classWidth : classWidth;
                            programIndex++;
                        }
                        else failed = true;
                        break;
                    }

                case Utf8SliceInstructionKind.Backref:
                    // ECMAScript permits a defined group that has not completed
                    // a capture yet to match an empty reference.
                    if (instruction.C != 0 && (captureStarts[instruction.A] < 0 || captureEnds[instruction.A] < 0))
                    {
                        programIndex++;
                        break;
                    }
                    // Mirrored backreferences compare the captured span backward: the
                    // run records right edges at capture starts and left edges at
                    // capture ends, so an unmatched (or in-progress) group fails on
                    // the mirrored guard and surviving spans compare from their ends.
                    // Byte comparison stays encoding-indifferent; the
                    // case-insensitive walk folds both sides with backward scalar
                    // widths like the forward arm.
                    if (program.RightToLeft)
                    {
                        if (captureEnds[instruction.A] < 0 ||
                            captureStarts[instruction.A] < 0)
                        {
                            failed = true;
                            break;
                        }

                        var rtlReferStart = Math.Min(captureStarts[instruction.A], captureEnds[instruction.A]);
                        var rtlReferEnd = Math.Max(captureStarts[instruction.A], captureEnds[instruction.A]);
                        var rtlReferLength = rtlReferEnd - rtlReferStart;
                        if (instruction.B != 0)
                        {
                            var rtlCapIndex = rtlReferEnd;
                            var rtlBackIndex = inputIndex;
                            var rtlBackFailed = false;
                            while (rtlCapIndex > rtlReferStart)
                            {
                                var rtlBackCharge = invocation.TryChargeStep();
                                if (rtlBackCharge != Utf8ExecutionLimitKind.None)
                                {
                                    return new Utf8SliceMatchOutcome(false, -1, rtlBackCharge, null, null);
                                }

                                if (rtlBackIndex <= 0)
                                {
                                    rtlBackFailed = true;
                                    break;
                                }

                                var rtlCapEndByte = input[rtlCapIndex - 1];
                                var rtlBackEndByte = input[rtlBackIndex - 1];
                                int rtlCapStep;
                                int rtlInStep;
                                bool rtlBackEqual;
                                if (rtlCapEndByte < 0x80 && rtlBackEndByte < 0x80)
                                {
                                    rtlBackEqual = Internal.Search.AsciiSearch.FoldCase(rtlCapEndByte) == Internal.Search.AsciiSearch.FoldCase(rtlBackEndByte);
                                    rtlCapStep = 1;
                                    rtlInStep = 1;
                                }
                                else if (!TryGetScalarWidthEndingAt(input, rtlCapIndex, out var rtlCapWidth) ||
                                    !TryGetScalarWidthEndingAt(input, rtlBackIndex, out var rtlInWidth))
                                {
                                    rtlBackFailed = true;
                                    break;
                                }
                                else if ((input[rtlCapIndex - rtlCapWidth] & 0xC0) == 0x80 || (input[rtlBackIndex - rtlInWidth] & 0xC0) == 0x80 || input[rtlCapIndex - rtlCapWidth] >= 0xF0 || input[rtlBackIndex - rtlInWidth] >= 0xF0)
                                {
                                    // Continuation bytes and astral leads compare raw
                                    // byte-by-byte exactly like the forward arm.
                                    rtlBackEqual = rtlCapEndByte == rtlBackEndByte;
                                    rtlCapStep = 1;
                                    rtlInStep = 1;
                                }
                                else
                                {
                                    var rtlCaptureLead = input[rtlCapIndex - rtlCapWidth];
                                    var rtlInputLead = input[rtlBackIndex - rtlInWidth];
                                    char rtlCaptureScalar;
                                    if (rtlCaptureLead < 0x80)
                                    {
                                        rtlCaptureScalar = (char)Internal.Search.AsciiSearch.FoldCase(rtlCaptureLead);
                                    }
                                    else if (!TryDecodeBmpScalar(input, rtlCapIndex - rtlCapWidth, out var rtlDecodedCapture))
                                    {
                                        rtlBackFailed = true;
                                        break;
                                    }
                                    else if (!TryFoldBmpScalarForCaseInsensitiveMatch(rtlDecodedCapture, out var rtlFoldedCapture))
                                    {
                                        rtlCaptureScalar = rtlDecodedCapture;
                                    }
                                    else
                                    {
                                        rtlCaptureScalar = rtlFoldedCapture;
                                    }

                                    char rtlInputScalar;
                                    if (rtlInputLead < 0x80)
                                    {
                                        rtlInputScalar = (char)Internal.Search.AsciiSearch.FoldCase(rtlInputLead);
                                    }
                                    else if (!TryDecodeBmpScalar(input, rtlBackIndex - rtlInWidth, out var rtlDecodedInput))
                                    {
                                        rtlBackFailed = true;
                                        break;
                                    }
                                    else if (!TryFoldBmpScalarForCaseInsensitiveMatch(rtlDecodedInput, out var rtlFoldedInput))
                                    {
                                        rtlInputScalar = rtlDecodedInput;
                                    }
                                    else
                                    {
                                        rtlInputScalar = rtlFoldedInput;
                                    }

                                    rtlBackEqual = rtlCaptureScalar == rtlInputScalar;
                                    rtlCapStep = rtlCapWidth;
                                    rtlInStep = rtlInWidth;
                                }

                                if (!rtlBackEqual)
                                {
                                    rtlBackFailed = true;
                                    break;
                                }

                                rtlCapIndex -= rtlCapStep;
                                rtlBackIndex -= rtlInStep;
                            }

                            if (rtlBackFailed)
                            {
                                failed = true;
                            }
                            else
                            {
                                inputIndex = rtlBackIndex;
                                programIndex++;
                            }

                            break;
                        }

                        if (rtlReferLength > inputIndex)
                        {
                            failed = true;
                            break;
                        }

                        var rtlCompared = 0;
                        while (rtlCompared < rtlReferLength)
                        {
                            var rtlCharge = invocation.TryChargeStep();
                            if (rtlCharge != Utf8ExecutionLimitKind.None)
                            {
                                return new Utf8SliceMatchOutcome(false, -1, rtlCharge, null, null);
                            }

                            if (input[rtlReferEnd - 1 - rtlCompared] != input[inputIndex - 1 - rtlCompared])
                            {
                                break;
                            }

                            rtlCompared++;
                        }

                        if (rtlCompared < rtlReferLength)
                        {
                            failed = true;
                        }
                        else
                        {
                            inputIndex -= rtlReferLength;
                            programIndex++;
                        }

                        break;
                    }

                    // Ordinal backreference: an unmatched (or in-progress)
                    // group fails, otherwise the captured bytes must repeat
                    // verbatim at the current position. Byte comparison is
                    // encoding-indifferent (captures never hold astral
                    // scalars on served programs). Each compared byte
                    // charges work so long captures stay bounded and
                    // cancellable like the rest of the engine.
                    if (captureStarts[instruction.A] < 0 ||
                        captureEnds[instruction.A] < 0)
                    {
                        failed = true;
                        break;
                    }

                    {
                        var referStart = Math.Min(captureStarts[instruction.A], captureEnds[instruction.A]);
                        var referEnd = Math.Max(captureStarts[instruction.A], captureEnds[instruction.A]);
                        var referLength = referEnd - referStart;
                        if (instruction.B != 0)
                        {
                            // Case-insensitive backreferences compare per-scalar BCL-invariant
                            // folds (ASCII pairs plus single-char ToLowerInvariant; U+0130 and
                            // astral halves compare raw and only ever equal themselves). The input
                            // advances by its own scalar widths, which may differ from the
                            // captured widths on fold hits (k versus U+212A).
                            var capIndex = referStart;
                            var backIndex = inputIndex;
                            var backFailed = false;
                            while (capIndex < referEnd)
                            {
                                var backCharge = invocation.TryChargeStep();
                                if (backCharge != Utf8ExecutionLimitKind.None)
                                {
                                    return new Utf8SliceMatchOutcome(false, -1, backCharge, null, null);
                                }

                                if (backIndex >= input.Length)
                                {
                                    backFailed = true;
                                    break;
                                }

                                var captureLead = input[capIndex];
                                var inputLead = input[backIndex];
                                int capWidth;
                                int inWidth;
                                bool backEqual;
                                if (captureLead < 0x80 && inputLead < 0x80)
                                {
                                    backEqual = Internal.Search.AsciiSearch.FoldCase(captureLead) == Internal.Search.AsciiSearch.FoldCase(inputLead);
                                    capWidth = 1;
                                    inWidth = 1;
                                }
                                else if ((captureLead & 0xC0) == 0x80 || (inputLead & 0xC0) == 0x80 || captureLead >= 0xF0 || inputLead >= 0xF0)
                                {
                                    // Continuation bytes (mid-astral walks) and astral
                                    // leads (halves never fold) compare raw.
                                    backEqual = captureLead == inputLead;
                                    capWidth = 1;
                                    inWidth = 1;
                                }
                                else
                                {
                                    // Both sides fold (ASCII pairs inline, BMP via
                                    // ToLowerInvariant with U+0130 comparing raw);
                                    // undecodable leads fail closed.
                                    char captureScalar;
                                    if (captureLead < 0x80)
                                    {
                                        captureScalar = (char)Internal.Search.AsciiSearch.FoldCase(captureLead);
                                    }
                                    else if (!TryDecodeBmpScalar(input, capIndex, out var decodedCapture))
                                    {
                                        backFailed = true;
                                        break;
                                    }
                                    else if (!TryFoldBmpScalarForCaseInsensitiveMatch(decodedCapture, out var foldedCapture))
                                    {
                                        captureScalar = decodedCapture;
                                    }
                                    else
                                    {
                                        captureScalar = foldedCapture;
                                    }

                                    char inputScalar;
                                    if (inputLead < 0x80)
                                    {
                                        inputScalar = (char)Internal.Search.AsciiSearch.FoldCase(inputLead);
                                    }
                                    else if (!TryDecodeBmpScalar(input, backIndex, out var decodedInput))
                                    {
                                        backFailed = true;
                                        break;
                                    }
                                    else if (!TryFoldBmpScalarForCaseInsensitiveMatch(decodedInput, out var foldedInput))
                                    {
                                        inputScalar = decodedInput;
                                    }
                                    else
                                    {
                                        inputScalar = foldedInput;
                                    }

                                    backEqual = captureScalar == inputScalar;
                                    capWidth = captureLead < 0x80 ? 1 : captureLead < 0xE0 ? 2 : 3;
                                    inWidth = inputLead < 0x80 ? 1 : inputLead < 0xE0 ? 2 : 3;
                                }

                                if (!backEqual)
                                {
                                    backFailed = true;
                                    break;
                                }

                                capIndex += capWidth;
                                backIndex += inWidth;
                            }

                            if (backFailed)
                            {
                                failed = true;
                            }
                            else
                            {
                                inputIndex = backIndex;
                                programIndex++;
                            }

                            break;
                        }

                        if (referLength > input.Length - inputIndex)
                        {
                            failed = true;
                            break;
                        }

                        var compared = 0;
                        while (compared < referLength)
                        {
                            var charge = invocation.TryChargeStep();
                            if (charge != Utf8ExecutionLimitKind.None)
                            {
                                return new Utf8SliceMatchOutcome(false, -1, charge, null, null);
                            }

                            if (input[referStart + compared] != input[inputIndex + compared])
                            {
                                break;
                            }

                            compared++;
                        }

                        if (compared < referLength)
                        {
                            failed = true;
                        }
                        else
                        {
                            inputIndex += referLength;
                            programIndex++;
                        }
                    }

                    break;
                case Utf8SliceInstructionKind.TestGroup:
                    // One-shot group test (BCL IsMatched: participated, even empty;
                    // in-progress and unset groups test false, group zero never matches):
                    // jump to the no-branch on false, fall into yes on true. Downstream
                    // failure never retries the other branch (BCL commits to the test).
                    // Direction-free: mirrored runs record inverted spans, but both ends
                    // stay non-negative exactly when the group participated.
                    if (instruction.A < 0 || captureStarts[instruction.A] < 0 || captureEnds[instruction.A] < 0)
                    {
                        programIndex = instruction.B;
                    }
                    else
                    {
                        programIndex++;
                    }

                    break;
                case Utf8SliceInstructionKind.Fail:
                    failed = true;
                    break;
                case Utf8SliceInstructionKind.AtomicCommit:
                    {
                        // Possessive commit: discard every frame up to and
                        // including the atomic fork (a surviving body must not
                        // resume its alternatives), keeping input, captures,
                        // and repeat state.
                        var foundAtomic = false;
                        while (stacks.FrameCount > frameBase && stacks.PopFrame(out var committed))
                        {
                            if (committed.ResumeKind == Utf8SliceResumeKind.Atomic)
                            {
                                foundAtomic = true;
                                break;
                            }
                        }

                        if (!foundAtomic)
                        {
                            failed = true;
                            break;
                        }

                        programIndex++;
                        break;
                    }
                case Utf8SliceInstructionKind.Commit:
                    {
                        // Assertion commit: discard every frame up to and
                        // including the assertion fork (a surviving body must
                        // not resume its alternatives), rewind to the
                        // assertion position (zero width), restore repeat
                        // state to the fork mark, and keep capture writes.
                        var journalMark = -1;
                        var rewindTo = -1;
                        while (stacks.FrameCount > frameBase && stacks.PopFrame(out var committed))
                        {
                            if (committed.ResumeKind == Utf8SliceResumeKind.Assertion)
                            {
                                journalMark = committed.JournalCount;
                                rewindTo = committed.InputIndex;
                                break;
                            }
                        }

                        if (journalMark < 0)
                        {
                            failed = true;
                            break;
                        }

                        inputIndex = rewindTo;
                        stacks.TruncateJournal(journalMark, counts, positions);
                        programIndex++;
                        break;
                    }
                case Utf8SliceInstructionKind.Lookbehind:
                case Utf8SliceInstructionKind.Lookahead:
                    // The driver owns child arrays and saves this exact edge;
                    // returning here suspends, rather than recursively entering,
                    // the assertion's independent instruction stream.
                    cursor.ProgramIndex = programIndex;
                    cursor.InputIndex = inputIndex;
                    suspended = true;
                    return default;
                case Utf8SliceInstructionKind.BalanceStart:
                    // Retain the body's opening without testing its source yet:
                    // the body can establish the capture popped at successful close.
                    if (!stacks.PushTrailEntry(new Utf8SliceTrailEntry {
                            Slot = instruction.B, NewValue = inputIndex, IsOpening = true }, ref invocation))
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                    programIndex++;
                    break;

                case Utf8SliceInstructionKind.BalancePop:
                    {
                        // Check at successful close: the body may have consumed
                        // the source through a nested balancing group. Pop its
                        // current completed capture, then project the interval
                        // against the completed body independently of direction.
                        var popSlot = instruction.A;
                        var captureSlot = instruction.B;
                        if (balanceHeads[popSlot] < 0)
                        {
                            failed = true;
                            break;
                        }
                        var top = stacks.BalanceEntryAt(balanceHeads[popSlot]);
                        var previousStart = -1;
                        var previousEnd = -1;
                        if (top.Prev >= 0)
                        {
                            var below = stacks.BalanceEntryAt(top.Prev);
                            previousStart = below.Start;
                            previousEnd = below.End;
                        }
                        if (!stacks.PushRestoreTrail(popSlot, captureStarts[popSlot], previousStart,
                                captureEnds[popSlot], previousEnd, ref invocation) ||
                            !stacks.PushHeadTrail(popSlot, balanceHeads[popSlot], top.Prev, ref invocation))
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        captureStarts[popSlot] = previousStart;
                        captureEnds[popSlot] = previousEnd;
                        balanceHeads[popSlot] = top.Prev;
                        if (captureSlot >= 0)
                        {
                            if (!stacks.TryFindCaptureOpening(captureSlot, ref invocation,
                                    out var opening, out var openingBreach))
                                return new Utf8SliceMatchOutcome(false, -1, openingBreach, null, null);
                            var sourceStart = Math.Min(top.Start, top.End);
                            var sourceEnd = Math.Max(top.Start, top.End);
                            var gapStart = Math.Min(opening, inputIndex);
                            var gapEnd = Math.Max(opening, inputIndex);
                            // Capture the gap for disjoint spans or their
                            // intersection when the body overlaps its source.
                            if (gapStart >= sourceEnd)
                            {
                                gapEnd = gapStart;
                                gapStart = sourceEnd;
                            }
                            else if (gapEnd <= sourceStart)
                            {
                                gapStart = gapEnd;
                                gapEnd = sourceStart;
                            }
                            else
                            {
                                gapStart = Math.Max(gapStart, sourceStart);
                                gapEnd = Math.Min(gapEnd, sourceEnd);
                            }
                            if (!stacks.PushRestoreTrail(captureSlot, captureStarts[captureSlot], gapStart,
                                    captureEnds[captureSlot], gapEnd, ref invocation) ||
                                !stacks.PushTrailEntry(new Utf8SliceTrailEntry {
                                    Slot = captureSlot, OldValue = gapStart, NewValue = gapEnd, IsClosing = true }, ref invocation) ||
                                !stacks.PushBalance(captureSlot, gapStart, gapEnd, balanceHeads[captureSlot], ref invocation) ||
                                !stacks.PushHeadTrail(captureSlot, balanceHeads[captureSlot], stacks.BalanceCount - 1, ref invocation))
                                return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                            captureStarts[captureSlot] = gapStart;
                            captureEnds[captureSlot] = gapEnd;
                            balanceHeads[captureSlot] = stacks.BalanceCount - 1;
                        }
                        programIndex++;
                        break;
                    }

                case Utf8SliceInstructionKind.Accept:
                    if (requireNonEmpty && inputIndex == start)
                    {
                        failed = true;
                        break;
                    }

                    if (!collectHistory)
                    {
                        return new Utf8SliceMatchOutcome(true, inputIndex, Utf8ExecutionLimitKind.None, null, null);
                    }

                    if (!stacks.MaterializeHistory(program.GroupCount, ref invocation, out var historyStarts, out var historyLengths))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                    }

                    return new Utf8SliceMatchOutcome(true, inputIndex, Utf8ExecutionLimitKind.None, historyStarts, historyLengths);
                default:
                    throw new InvalidOperationException($"Unsupported slice instruction {instruction.Kind}.");
            }

        Backtrack:
            if (failed)
            {
                if (stacks.FrameCount == frameBase || !stacks.PopFrame(out var popped))
                {
                    return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None, null, null);
                }

                stacks.TruncateJournal(popped.JournalCount, counts, positions);
                stacks.TruncateTrail(popped.TrailCount, captureStarts, captureEnds, balanceHeads);
                switch (popped.ResumeKind)
                {
                    case Utf8SliceResumeKind.Alternation:
                    case Utf8SliceResumeKind.Assertion:
                    case Utf8SliceResumeKind.Atomic:
                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                    case Utf8SliceResumeKind.RepeatUnwind:
                        if (!SetRepeat(counts, positions, popped.Aux, popped.Count, popped.InputIndex, ref stacks, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        }

                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                    default:
                        if (!SetRepeat(counts, positions, popped.Aux, popped.Count, popped.InputIndex, ref stacks, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        }

                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                }
            }
        }
    }

    private static bool SetRepeat(
        int[] counts,
        int[] positions,
        int repeatId,
        int count,
        int position,
        ref ChunkedStacks stacks,
        ref Utf8SliceInvocation invocation)
    {
        if (!stacks.PushJournal(repeatId, counts[repeatId], positions[repeatId], ref invocation))
        {
            return false;
        }

        counts[repeatId] = count;
        positions[repeatId] = position;
        return true;
    }

    private static int[] RentIntArray(int length, ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        breach = Utf8ExecutionLimitKind.None;
        if (length == 0)
        {
            return Array.Empty<int>();
        }

        if (!invocation.Ledger.TryReserve((ulong)length * 4))
        {
            breach = Utf8ExecutionLimitKind.WorkingBytes;
            return Array.Empty<int>();
        }

        try
        {
            breach = invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes);
            if (breach != Utf8ExecutionLimitKind.None)
            {
                invocation.Ledger.Release((ulong)length * 4);
                return Array.Empty<int>();
            }
            return new int[length];
        }
        catch
        {
            invocation.Ledger.Release((ulong)length * 4);
            throw;
        }
    }

    private static void ReleaseIntArray(int[] array, ref Utf8SliceInvocation invocation)
    {
        if (array.Length != 0)
        {
            invocation.Ledger.Release((ulong)array.Length * 4);
        }
    }

    private struct ChunkedStacks
    {
        private Utf8SliceFrame[][] _frameTables;
        private Utf8SliceTrailEntry[][] _trailTables;
        private Utf8SliceCountMutation[][] _journalTables;
        private Utf8SliceBalanceEntry[][] _balanceTables;
        private int _frameCount;
        private int _trailCount;
        private int _journalCount;
        private int _balanceCount;
        private ulong _reservedBytes;

        internal int FrameCount => _frameCount;
        internal int TrailCount => _trailCount;
        internal int JournalCount => _journalCount;
        internal int BalanceCount => _balanceCount;

        internal bool PushFrame(Utf8SliceFrame frame, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _frameTables, ref _frameCount, FrameChunkCapacity, FrameReserveBytes, frame, ref invocation, ref _reservedBytes);
        }

        internal bool PushTrail(int slot, int oldValue, int newValue, bool isStart, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity, TrailReserveBytes,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldValue, NewValue = newValue, IsStart = isStart }, ref invocation, ref _reservedBytes);
        }

        internal bool PushTrailEntry(Utf8SliceTrailEntry entry, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity, TrailReserveBytes,
                entry, ref invocation, ref _reservedBytes);
        }

        internal bool TryFindCaptureOpening(int slot, ref Utf8SliceInvocation invocation, out int opening, out Utf8ExecutionLimitKind breach)
        {
            var closed = 0;
            opening = -1;
            breach = Utf8ExecutionLimitKind.None;
            for (var index = _trailCount - 1; index >= 0; index--)
            {
                breach = invocation.TryChargeStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                var entry = TrailEntryAt(index);
                if (entry.Slot != slot) continue;
                if (entry.IsClosing) closed++;
                else if (entry.IsOpening)
                {
                    if (closed != 0) closed--;
                    else
                    {
                        opening = entry.NewValue;
                        return true;
                    }
                }
            }
            throw new InvalidOperationException("Capture close has no surviving opening marker.");
        }

        internal bool PushRestoreTrail(int slot, int oldStart, int newStart, int oldEnd, int newEnd, ref Utf8SliceInvocation invocation)
        {
            // Balance-pop slot restores: live slot state for backtrack
            // truncation, but no capture record (the popped capture
            // drains through its head entry; the restored top stays
            // recorded from its own capture time).
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity, TrailReserveBytes,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldStart, NewValue = newStart, IsStart = true, IsRestore = true }, ref invocation, ref _reservedBytes) &&
                Push(ref _trailTables, ref _trailCount, TrailChunkCapacity, TrailReserveBytes,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldEnd, NewValue = newEnd, IsStart = false, IsRestore = true }, ref invocation, ref _reservedBytes);
        }

        internal bool PushJournal(int repeatId, int oldCount, int oldPosition, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _journalTables, ref _journalCount, JournalChunkCapacity, JournalReserveBytes,
                new Utf8SliceCountMutation { RepeatId = repeatId, OldCount = oldCount, OldPosition = oldPosition }, ref invocation, ref _reservedBytes);
        }

        internal bool PushBalance(int slot, int start, int end, int prev, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _balanceTables, ref _balanceCount, BalanceChunkCapacity, BalanceReserveBytes,
                new Utf8SliceBalanceEntry { Slot = slot, Start = start, End = end, Prev = prev }, ref invocation, ref _reservedBytes);
        }

        internal bool PushHeadTrail(int slot, int oldHead, int newHead, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity, TrailReserveBytes,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldHead, NewValue = newHead, IsStart = false, IsHead = true }, ref invocation, ref _reservedBytes);
        }

        internal Utf8SliceBalanceEntry BalanceEntryAt(int index)
        {
            return _balanceTables[index / BalanceChunkCapacity][index % BalanceChunkCapacity];
        }

        internal bool PopFrame(out Utf8SliceFrame frame)
        {
            if (_frameCount == 0)
            {
                frame = default;
                return false;
            }

            _frameCount--;
            frame = _frameTables[_frameCount / FrameChunkCapacity][_frameCount % FrameChunkCapacity];
            return true;
        }

        internal Utf8SliceTrailEntry TrailEntryAt(int index)
        {
            return _trailTables[index / TrailChunkCapacity][index % TrailChunkCapacity];
        }

        internal void TruncateTrail(int count, int[] captureStarts, int[] captureEnds, int[] balanceHeads)
        {
            while (_trailCount > count)
            {
                _trailCount--;
                var entry = _trailTables[_trailCount / TrailChunkCapacity][_trailCount % TrailChunkCapacity];
                if (entry.IsOpening || entry.IsClosing) continue;
                if (entry.IsHead)
                {
                    balanceHeads[entry.Slot] = entry.OldValue;
                }
                else if (entry.IsStart)
                {
                    captureStarts[entry.Slot] = entry.OldValue;
                }
                else
                {
                    captureEnds[entry.Slot] = entry.OldValue;
                }
            }
        }

        internal void TruncateJournal(int count, int[] counts, int[] positions)
        {
            while (_journalCount > count)
            {
                _journalCount--;
                var entry = _journalTables[_journalCount / JournalChunkCapacity][_journalCount % JournalChunkCapacity];
                counts[entry.RepeatId] = entry.OldCount;
                positions[entry.RepeatId] = entry.OldPosition;
            }
        }

        internal bool MaterializeHistory(
            int groups,
            ref Utf8SliceInvocation invocation,
            out int[][] captureStarts,
            out int[][] captureLengths)
        {
            if (!invocation.Ledger.TryReserve((ulong)groups * 16))
            {
                captureStarts = Array.Empty<int[]>();
                captureLengths = Array.Empty<int[]>();
                return false;
            }

            captureStarts = new int[groups][];
            captureLengths = new int[groups][];
            if (groups == 0)
            {
                return true;
            }

            if (!invocation.Ledger.TryReserve((ulong)groups * 4))
            {
                return false;
            }

            var reservedBytes = (ulong)groups * 4;
            var counts = new int[groups];
            var sawBalanceDrain = false;
            try
            {
                for (var entry = 0; entry < _trailCount; entry++)
                {
                    var item = _trailTables[entry / TrailChunkCapacity][entry % TrailChunkCapacity];
                    if (item.IsRestore || item.IsOpening)
                    {
                        continue;
                    }
                    if (item.IsHead)
                    {
                        // Balance pops drain the stack: chunk indexes grow
                        // monotonically, so a head entry moving to a lower
                        // index (or -1) is a pop that removes the most
                        // recent capture; anything else is a push. Sizing
                        // counts every capture write: the fill cursor drains
                        // through the same head entries below and the arrays
                        // shrink to the drained length afterwards.
                        if (item.NewValue < item.OldValue)
                        {
                            sawBalanceDrain = true;
                        }
                    }
                    else if (!item.IsStart)
                    {
                        counts[item.Slot]++;
                    }
                }

                for (var group = 0; group < groups; group++)
                {
                    if (counts[group] == 0)
                    {
                        captureStarts[group] = Array.Empty<int>();
                        captureLengths[group] = Array.Empty<int>();
                        continue;
                    }

                    if (!invocation.Ledger.TryReserve((ulong)counts[group] * 8))
                    {
                        for (var created = 0; created < group; created++)
                        {
                            invocation.Ledger.Release((ulong)captureStarts[created].Length * 8);
                        }

                        invocation.Ledger.Release(reservedBytes);
                        return false;
                    }

                    reservedBytes += (ulong)counts[group] * 8;
                    captureStarts[group] = new int[counts[group]];
                    captureLengths[group] = new int[counts[group]];
                    counts[group] = 0;
                }

                var open = new int[groups];
                for (var group = 0; group < groups; group++)
                {
                    open[group] = -1;
                }

                for (var entry = 0; entry < _trailCount; entry++)
                {
                    var item = _trailTables[entry / TrailChunkCapacity][entry % TrailChunkCapacity];
                    if (item.IsRestore || item.IsOpening)
                    {
                        continue;
                    }
                    if (item.IsHead)
                    {
                        if (item.NewValue < item.OldValue)
                        {
                            counts[item.Slot]--;
                        }
                    }
                    else if (item.IsStart)
                    {
                        open[item.Slot] = item.NewValue;
                    }
                    else
                    {
                        // Mirrored runs record the right edge at the start entry and
                        // the left edge at the end entry; histories always carry
                        // normalized spans (forward runs already satisfy the order).
                        var historyOpen = item.IsClosing ? item.OldValue : open[item.Slot];
                        var historyClose = item.NewValue;
                        captureStarts[item.Slot][counts[item.Slot]] = historyOpen < historyClose ? historyOpen : historyClose;
                        captureLengths[item.Slot][counts[item.Slot]] = historyOpen < historyClose ? historyClose - historyOpen : historyOpen - historyClose;
                        counts[item.Slot]++;
                    }
                }

                if (sawBalanceDrain)
                {
                    // Drained pops were recorded transiently above while
                    // their head entries rewound the per-group cursors:
                    // shrink every drained array and hand the slack back.
                    for (var group = 0; group < groups; group++)
                    {
                        if (counts[group] < captureStarts[group].Length)
                        {
                            var slack = (ulong)(captureStarts[group].Length - counts[group]) * 8;
                            if (counts[group] == 0)
                            {
                                captureStarts[group] = Array.Empty<int>();
                                captureLengths[group] = Array.Empty<int>();
                            }
                            else
                            {
                                Array.Resize(ref captureStarts[group], counts[group]);
                                Array.Resize(ref captureLengths[group], counts[group]);
                            }

                            invocation.Ledger.Release(slack);
                        }
                    }
                }

                return true;
            }
            finally
            {
                invocation.Ledger.Release((ulong)groups * 4);
            }
        }

        internal void ReleaseAll(ref Utf8SliceInvocation invocation)
        {
            if (_reservedBytes != 0)
            {
                invocation.Ledger.Release(_reservedBytes);
                _reservedBytes = 0;
            }

            _frameTables = Array.Empty<Utf8SliceFrame[]>();
            _trailTables = Array.Empty<Utf8SliceTrailEntry[]>();
            _journalTables = Array.Empty<Utf8SliceCountMutation[]>();
            _balanceTables = Array.Empty<Utf8SliceBalanceEntry[]>();
            _frameCount = 0;
            _trailCount = 0;
            _journalCount = 0;
            _balanceCount = 0;
        }

        private static bool Push<T>(
            ref T[][] tables,
            ref int count,
            int chunkCapacity,
            int chunkBytes,
            T value,
            ref Utf8SliceInvocation invocation,
            ref ulong reservedBytes)
            where T : struct
        {
            var chunk = count / chunkCapacity;
            var slot = count % chunkCapacity;
            if (slot == 0)
            {
                if (tables is null || tables.Length == 0)
                {
                    if (!invocation.Ledger.TryReserve((ulong)ChunkTableCapacity * 8))
                    {
                        return false;
                    }

                    reservedBytes += (ulong)ChunkTableCapacity * 8;
                    if (invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes) != Utf8ExecutionLimitKind.None)
                        return false;
                    tables = new T[ChunkTableCapacity][];
                }
                else if (chunk >= tables.Length)
                {
                    var capacity = tables.Length <= Array.MaxLength / 2 ? tables.Length * 2 : Array.MaxLength;
                    if (capacity <= chunk) return false;
                    var bytes = (ulong)capacity * 8;
                    if (!invocation.Ledger.TryReserve(bytes)) return false;
                    reservedBytes += bytes;
                    var adopted = false;
                    try
                    {
                        if (invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes) != Utf8ExecutionLimitKind.None)
                            return false;
                        var grown = new T[capacity][];
                        Array.Copy(tables, grown, tables.Length);
                        var oldBytes = (ulong)tables.Length * 8;
                        invocation.Ledger.Release(oldBytes);
                        reservedBytes -= oldBytes;
                        tables = grown;
                        adopted = true;
                    }
                    finally
                    {
                        if (!adopted)
                        {
                            invocation.Ledger.Release(bytes);
                            reservedBytes -= bytes;
                        }
                    }
                }

                if (tables[chunk] is null)
                {
                    if (!invocation.Ledger.TryReserve((ulong)chunkBytes))
                    {
                        return false;
                    }

                    reservedBytes += (ulong)chunkBytes;
                    if (invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes) != Utf8ExecutionLimitKind.None)
                        return false;
                    tables[chunk] = new T[chunkCapacity];
                }
            }

            tables[chunk][slot] = value;
            count++;
            return true;
        }
    }
}
