using Lokad.Utf8Regex.Internal.Diagnostics;

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

    // Only the whole-pattern word proof and a fully validated ASCII subject
    // admit this counter. It keeps the existing run algorithm while carrying
    // work/interruption through bounded blocks instead of a dormant raw loop.
    internal static Utf8ExecutionLimitKind TryCountWordRunsValidatedAscii(ReadOnlySpan<byte> input, int minCount,
        ref Utf8SliceInvocation invocation, out int count)
    {
        var lookup = Utf8SliceScalarClasses.WordCharAsciiLookup;
        count = 0;
        var runLength = 0;
        for (var offset = 0; offset < input.Length;)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            invocation.Budget.ThrowIfCancellationRequested();
            var block = input.Slice(offset, Math.Min(32, input.Length - offset));
            foreach (var value in block)
            {
                if (lookup[value] != 0) runLength++;
                else
                {
                    if (runLength >= minCount) count++;
                    runLength = 0;
                }
            }
            offset += block.Length;
        }
        if (runLength >= minCount) count++;
        var finalBreach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return finalBreach;
    }
}
