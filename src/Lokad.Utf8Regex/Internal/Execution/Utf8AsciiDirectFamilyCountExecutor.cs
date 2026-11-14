using Lokad.Utf8Regex.Internal.Diagnostics;
using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiDirectFamilyCountExecutor
{
    public static bool TryCount(
        ReadOnlySpan<byte> input,
        bool isAscii,
        in Utf8FallbackDirectFamilyPlan plan,
        Utf8EmittedTokenFamilyMatcher? emittedTokenFamilyMatcher,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out int count,
        out Utf8ExecutionRoute diagnosticsRoute)
    {
        if (emittedTokenFamilyMatcher is not null && isAscii)
        {
            count = emittedTokenFamilyMatcher.Count(input);
            diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiEmittedTokenFamily;
            return true;
        }

        return TryCount(input, isAscii, plan, delimitedTokenSearch, literalStructuredTokenSearch, out count, out diagnosticsRoute);
    }

    public static bool TryCount(
        ReadOnlySpan<byte> input,
        bool isAscii,
        in Utf8FallbackDirectFamilyPlan plan,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out int count,
        out Utf8ExecutionRoute diagnosticsRoute)
    {
        count = 0;
        diagnosticsRoute = Utf8ExecutionRoute.None;

        switch (plan.Kind)
        {
            case Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount:
                diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectUtf8WordDelimitedToken;
                count = Utf8WordDelimitedTokenExecutor.CountWellFormed(input);
                return true;

            case Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount:
                diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiDottedDecimalQuad;
                count = Utf8AsciiIpv4TokenExecutor.CountAsciiIpv4Tokens(input);
                return true;

            case Utf8FallbackDirectFamilyKind.AsciiUntilByteStarCount:
                diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiUntilByteStar;
                count = Utf8AsciiUntilByteStarExecutor.Count(input, plan.TerminatorByte);
                return true;

            case Utf8FallbackDirectFamilyKind.Utf8BalancedBeginEndCount:
                if (Utf8BalancedBeginEndExecutor.TryCount(input, out count))
                {
                    diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectUtf8BalancedBeginEnd;
                    return true;
                }

                return false;
        }

        if (Utf8FallbackDirectFamilyCategories.IsPreparedTokenCountFamily(plan.Kind))
        {
            if (!isAscii)
            {
                return false;
            }

            diagnosticsRoute = plan.Kind == Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount
                ? Utf8ExecutionRoute.FallbackDirectAsciiDelimitedToken
                : Utf8ExecutionRoute.FallbackDirectAsciiLiteralStructuredToken;
            count = Utf8AsciiPreparedTokenFamilyExecutor.CountTokens(input, plan, delimitedTokenSearch, literalStructuredTokenSearch);
            return true;
        }

        if (Utf8FallbackDirectFamilyCategories.IsTokenCountFamily(plan.Kind))
        {
            if (!Utf8AsciiTokenFamilyExecutor.TryCountTokens(input, isAscii, plan, out count, out diagnosticsRoute))
            {
                count = 0;
                diagnosticsRoute = Utf8ExecutionRoute.None;
                return false;
            }

            return true;
        }

        return false;
    }

    // The complete greedy word proof needs no captures or VM scratch. Keep
    // the ASCII lookup/run algorithm, decoding only encountered wider scalars.
    internal static Utf8ExecutionLimitKind TryCountWordRunsValidated(ReadOnlySpan<byte> input, int minCount,
        bool requireBoundaries, ref Utf8SliceInvocation invocation, out int count)
    {
        var lookup = Utf8SliceScalarClasses.WordCharAsciiLookup;
        count = 0;
        var runLength = 0;
        var startsAfterJoiner = false;
        for (var offset = 0; offset < input.Length;)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            invocation.Budget.ThrowIfCancellationRequested();
            var end = offset + Math.Min(32, input.Length - offset);
            if (end < input.Length)
            {
                // Valid UTF-8 has at most three continuations at this cut.
                // Admit complete scalars within the same <=32-byte work unit.
                for (var adjustment = 0; adjustment < 3 && (input[end] & 0xc0) == 0x80; adjustment++) end--;
            }
            var block = input[offset..end];
            for (var index = 0; index < block.Length;)
            {
                var value = block[index];
                bool member;
                var joiner = false;
                if (value < 128)
                {
                    member = lookup[value] != 0;
                    index++;
                }
                else
                {
                    if (Rune.DecodeFromUtf8(block[index..], out var scalar, out var width) != OperationStatus.Done)
                        throw Input.Utf8Validation.CreateInvalidUtf8Exception(offset + index);
                    member = Utf8SliceScalarClasses.IsWord(scalar, false, false);
                    // Joiners participate in boundaries without being \w.
                    joiner = scalar.Value is 0x200c or 0x200d;
                    index += width;
                }
                if (member) runLength++;
                else
                {
                    if (runLength >= minCount && (!requireBoundaries || !startsAfterJoiner && !joiner)) count++;
                    runLength = 0;
                    // Every other nonmember supplies a boundary for the next run.
                    startsAfterJoiner = joiner;
                }
            }
            offset = end;
        }
        if (runLength >= minCount && (!requireBoundaries || !startsAfterJoiner)) count++;
        var finalBreach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return finalBreach;
    }
}
