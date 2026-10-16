using Lokad.Utf8Regex.Internal.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiDeterministicFixedWidthCountExecutor
{
    private const int LaneCount = 32;
    private const int MinimumWidth = 3;
    private const int MaximumWidth = 8;
    private const int MinimumInitialLiteralCandidates = 2;
    private const int EmptyLiteralBlockFallbackThreshold = 4;

    public static bool TryCount(
        in Utf8StructuralLinearProgram program,
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget,
        out int count)
    {
        count = 0;
        var deterministicProgram = program.DeterministicProgram;
        var checks = deterministicProgram.FixedWidthChecks;
        var width = deterministicProgram.FixedWidthLength;
        if (!Avx2.IsSupported ||
            !budget.IsInfinite ||
            Utf8SearchDiagnosticsSession.Current is not null ||
            !program.AllowsUtf8ByteSafe ||
            !deterministicProgram.HasValue ||
            deterministicProgram.IgnoreCase ||
            deterministicProgram.IsEndAnchored ||
            program.InstructionProgram.IsStartAnchored ||
            width is < MinimumWidth or > MaximumWidth ||
            checks.Length != width ||
            !AreVectorizable(checks))
        {
            return false;
        }

        var maxStart = input.Length - width;
        if (maxStart < LaneCount - 1)
        {
            return false;
        }

        var blockLength = LaneCount - 1 + width;
        var vectorLimit = maxStart - (LaneCount - 1);
        var blockStart = 0;
        var nextStart = 0;
        var emptyLiteralBlocks = 0;
        while (blockStart <= vectorLimit)
        {
            var matchMask = CreateMatchMask(input.Slice(blockStart, blockLength), checks, out var literalMask);
            if (blockStart == 0 &&
                BitOperations.PopCount(literalMask) < MinimumInitialLiteralCandidates)
            {
                return false;
            }

            if (literalMask == 0)
            {
                emptyLiteralBlocks++;
                if (emptyLiteralBlocks >= EmptyLiteralBlockFallbackThreshold)
                {
                    count += CountScalarSuffix(program, input, Math.Max(blockStart, nextStart), budget);
                    return true;
                }
            }
            else
            {
                emptyLiteralBlocks = 0;
            }

            CountNonOverlappingMatches(matchMask, blockStart, blockStart, width, ref nextStart, ref count);
            blockStart += LaneCount;
        }

        if (blockStart <= maxStart)
        {
            var finalBlockStart = maxStart - (LaneCount - 1);
            var matchMask = CreateMatchMask(input.Slice(finalBlockStart, blockLength), checks, out _);
            CountNonOverlappingMatches(matchMask, finalBlockStart, blockStart, width, ref nextStart, ref count);
        }

        return true;

        static int CountScalarSuffix(
            in Utf8StructuralLinearProgram program,
            ReadOnlySpan<byte> input,
            int startIndex,
            Utf8ExecutionDeadline budget)
        {
            var count = 0;
            var state = new Utf8AsciiDeterministicScanState(
                startIndex,
                startIndex + program.DeterministicProgram.SearchLiteralOffset);
            while (Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicFixedWidthMatch(
                program,
                input,
                ref state,
                budget,
                out _))
            {
                count++;
            }

            return count;
        }
    }

    // The caller supplies the original invocation after complete ASCII validation.
    // Charge each vector check and its bounded mask reduction before a block.
    // A declined attempt keeps its spent work; the caller resumes its owned VM
    // under that same authority instead of rolling the budget back.
    internal static bool TryCountValidatedAscii(
        in Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out int count, out Utf8ExecutionLimitKind breach)
    {
        count = 0;
        breach = Utf8ExecutionLimitKind.None;
        var deterministic = program.DeterministicProgram;
        var checks = deterministic.FixedWidthChecks;
        var width = deterministic.FixedWidthLength;
        if (!Avx2.IsSupported || Utf8SearchDiagnosticsSession.Current is not null ||
            !program.AllowsUtf8ByteSafe || !deterministic.HasValue || deterministic.IgnoreCase ||
            deterministic.IsEndAnchored || program.InstructionProgram.IsStartAnchored ||
            width is < MinimumWidth or > MaximumWidth || checks.Length != width || !AreVectorizable(checks))
            return false;
        var maxStart = input.Length - width;
        if (maxStart < LaneCount - 1) return false;
        var blockLength = LaneCount - 1 + width;
        var vectorLimit = maxStart - (LaneCount - 1);
        var blockStart = 0;
        var nextStart = 0;
        var emptyLiteralBlocks = 0;
        while (blockStart <= vectorLimit)
        {
            breach = ChargeBlock(width, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None) return true;
            var mask = CreateMatchMask(input.Slice(blockStart, blockLength), checks, out var literalMask);
            if (blockStart == 0 && BitOperations.PopCount(literalMask) < MinimumInitialLiteralCandidates)
                return false;
            emptyLiteralBlocks = literalMask == 0 ? emptyLiteralBlocks + 1 : 0;
            if (emptyLiteralBlocks >= EmptyLiteralBlockFallbackThreshold)
            {
                // The existing scalar suffix scanner has no invocation meter.
                // Decline to the owned runner, retaining all attempted work.
                count = 0;
                return false;
            }
            CountNonOverlappingMatches(mask, blockStart, blockStart, width, ref nextStart, ref count);
            blockStart += LaneCount;
        }
        if (blockStart <= maxStart)
        {
            breach = ChargeBlock(width, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None) return true;
            var finalStart = maxStart - (LaneCount - 1);
            var mask = CreateMatchMask(input.Slice(finalStart, blockLength), checks, out _);
            CountNonOverlappingMatches(mask, finalStart, blockStart, width, ref nextStart, ref count);
        }
        invocation.Budget.ThrowIfCancellationRequested();
        return true;
    }

    private static Utf8ExecutionLimitKind ChargeBlock(int width, ref Utf8SliceInvocation invocation)
    {
        for (var i = 0; i <= width; i++)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
        }
        return Utf8ExecutionLimitKind.None;
    }

    private static bool AreVectorizable(Utf8AsciiDeterministicFixedWidthCheck[] checks)
    {
        var literals = 0;
        foreach (var check in checks)
        {
            if (check.Kind == Utf8AsciiDeterministicFixedWidthCheckKind.Literal) { literals++; continue; }
            if (check.Kind != Utf8AsciiDeterministicFixedWidthCheckKind.CharClass ||
                !check.CharClass.TryGetKnownPredicateKind(out _)) return false;
        }
        return literals >= 2;
    }

    private static uint CreateMatchMask(
        ReadOnlySpan<byte> block,
        Utf8AsciiDeterministicFixedWidthCheck[] checks,
        out uint literalMask)
    {
        var candidates = Vector256.Create(byte.MaxValue);
        var literalCandidates = candidates;
        for (var i = 0; i < checks.Length; i++)
        {
            var values = MemoryMarshal.Read<Vector256<byte>>(block);
            block = block[1..];
            ref readonly var check = ref checks[i];
            if (check.Kind == Utf8AsciiDeterministicFixedWidthCheckKind.Literal)
            {
                var matches = Avx2.CompareEqual(values, Vector256.Create(check.Literal));
                candidates = Avx2.And(candidates, matches);
                literalCandidates = Avx2.And(literalCandidates, matches);
            }
            else
            {
                var matches = check.Kind == Utf8AsciiDeterministicFixedWidthCheckKind.CharClass
                    ? MatchKnownClass(values, check.CharClass.KnownPredicateKind)
                    : Vector256<byte>.Zero;
                candidates = Avx2.And(candidates, matches);
            }
        }

        literalMask = unchecked((uint)Avx2.MoveMask(literalCandidates.AsSByte()));
        return unchecked((uint)Avx2.MoveMask(candidates.AsSByte()));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<byte> MatchKnownClass(
            Vector256<byte> values,
            AsciiCharClassPredicateKind predicateKind)
        {
            return predicateKind switch
            {
                AsciiCharClassPredicateKind.Digit
                    => MatchRange(values, (byte)'0', (byte)'9'),
                AsciiCharClassPredicateKind.AsciiLetter
                    => MatchAsciiLetter(values),
                AsciiCharClassPredicateKind.AsciiLetterOrDigit
                    => Avx2.Or(MatchAsciiLetter(values), MatchRange(values, (byte)'0', (byte)'9')),
                AsciiCharClassPredicateKind.AsciiLetterDigitUnderscore
                    => Avx2.Or(
                        Avx2.Or(MatchAsciiLetter(values), MatchRange(values, (byte)'0', (byte)'9')),
                        Avx2.CompareEqual(values, Vector256.Create((byte)'_'))),
                AsciiCharClassPredicateKind.AsciiHexDigit
                    => Avx2.Or(
                        MatchRange(values, (byte)'0', (byte)'9'),
                        Avx2.Or(
                            MatchRange(values, (byte)'A', (byte)'F'),
                            MatchRange(values, (byte)'a', (byte)'f'))),
                _ => Vector256<byte>.Zero,
            };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static Vector256<byte> MatchAsciiLetter(Vector256<byte> values)
            {
                return Avx2.Or(
                    MatchRange(values, (byte)'A', (byte)'Z'),
                    MatchRange(values, (byte)'a', (byte)'z'));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<byte> MatchRange(Vector256<byte> values, byte low, byte high)
        {
            var atLeastLow = Avx2.CompareEqual(Avx2.Max(values, Vector256.Create(low)), values);
            var atMostHigh = Avx2.CompareEqual(Avx2.Min(values, Vector256.Create(high)), values);
            return Avx2.And(atLeastLow, atMostHigh);
        }
    }

    private static void CountNonOverlappingMatches(
        uint matchMask,
        int blockStart,
        int minimumStart,
        int width,
        ref int nextStart,
        ref int count)
    {
        var firstAllowedLane = Math.Max(minimumStart, nextStart) - blockStart;
        if (firstAllowedLane >= LaneCount)
        {
            return;
        }

        if (firstAllowedLane > 0)
        {
            matchMask &= uint.MaxValue << firstAllowedLane;
        }

        while (matchMask != 0)
        {
            var lane = BitOperations.TrailingZeroCount(matchMask);
            var matchStart = blockStart + lane;
            count++;
            nextStart = matchStart + width;

            var nextAllowedLane = nextStart - blockStart;
            if (nextAllowedLane >= LaneCount)
            {
                return;
            }

            matchMask &= uint.MaxValue << nextAllowedLane;
        }
    }
}
