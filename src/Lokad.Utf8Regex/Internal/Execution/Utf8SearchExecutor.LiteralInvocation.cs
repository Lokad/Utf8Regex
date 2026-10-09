using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SearchExecutor
{
    internal static Utf8ExecutionLimitKind TryFindNativeLiteralMatch(
        in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int start, int end,
        bool reverse, ref Utf8SliceInvocation invocation, out PreparedSearchMatch match,
        out bool found)
    {
        if (plan.AlternateLiteralsUtf8 is { Length: > 0 })
            return TryFindLiteralFamily(plan, input, start, end, reverse, ref invocation, out match, out found);

        match = default;
        found = false;
        var breach = TryFindLiteral(plan, input, start, end, reverse, ref invocation, out var index);
        if (breach != Utf8ExecutionLimitKind.None || index < 0) return breach;
        var search = plan.LiteralSearch ?? throw new InvalidOperationException("An accepted native literal requires a prepared search.");
        match = new PreparedSearchMatch(index, search.Length, 0);
        found = true;
        return Utf8ExecutionLimitKind.None;
    }

    // A charged search or comparison examines at most 2,048 bytes. Filtering
    // first-byte candidates separately avoids multiplying an unbounded needle
    // by a search window inside an opaque prepared-search call.
    internal static Utf8ExecutionLimitKind TryFindLiteral(
        in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int start, int end,
        bool reverse, ref Utf8SliceInvocation invocation, out int index)
    {
        index = -1;
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (plan.LiteralSearch is not { Length: > 0 } search) return Utf8ExecutionLimitKind.None;

        var low = start;
        var high = end - search.Length;
        while (low <= high)
        {
            breach = TryFindLiteralCandidate(search, input, low, high, reverse, ref invocation, out var candidate);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (candidate < 0) break;

            breach = TryConfirmLiteral(plan, input, candidate, search.Length, ref invocation, out var accepted);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (accepted)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                invocation.Budget.ThrowIfCancellationRequested();
                index = candidate;
                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                return Utf8ExecutionLimitKind.None;
            }

            if (reverse) high = candidate - 1;
            else low = candidate + 1;
        }

        breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return breach;
    }

    internal static Utf8ExecutionLimitKind TryFindLiteralCandidate(
        PreparedSubstringSearch search, ReadOnlySpan<byte> input, int low, int high,
        bool reverse, ref Utf8SliceInvocation invocation, out int candidate)
    {
        candidate = -1;
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            var slice = input.Slice(low, high - low + search.Length);
            var relative = reverse ? search.LastIndexOf(slice) : search.IndexOf(slice);
            candidate = relative < 0 ? -1 : low + relative;
            return Utf8ExecutionLimitKind.None;
        }

        var first = search.Needle[0];
        var other = search.IgnoreCase && (uint)((first | 0x20) - 'a') <= 'z' - 'a'
            ? (byte)(first ^ 0x20) : first;
        while (low <= high)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var count = Math.Min(2048, high - low + 1);
            var blockStart = reverse ? high - count + 1 : low;
            var block = input.Slice(blockStart, count);
            var relative = reverse ? block.LastIndexOfAny(first, other) : block.IndexOfAny(first, other);
            if (relative < 0)
            {
                if (reverse) high -= count;
                else low += count;
                continue;
            }

            var found = blockStart + relative;
            breach = TryCompareLiteral(input.Slice(found, search.Length), search.Needle,
                search.IgnoreCase, ref invocation, out var equal);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (equal)
            {
                candidate = found;
                return Utf8ExecutionLimitKind.None;
            }
            if (reverse) high = found - 1;
            else low = found + 1;
        }
        return Utf8ExecutionLimitKind.None;
    }

    internal static Utf8ExecutionLimitKind TryCompareLiteral(
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal, bool ignoreCase,
        ref Utf8SliceInvocation invocation, out bool equal)
    {
        equal = false;
        if (input.Length < literal.Length) return Utf8ExecutionLimitKind.None;
        for (var offset = 0; offset < literal.Length;)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var count = Math.Min(2048, literal.Length - offset);
            var actual = input.Slice(offset, count);
            var expected = literal.Slice(offset, count);
            if (!ignoreCase)
            {
                if (!actual.SequenceEqual(expected)) return Utf8ExecutionLimitKind.None;
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    var a = actual[i];
                    var b = expected[i];
                    if (a != b && !((uint)((a | 0x20) - 'a') <= 'z' - 'a' && (a | 0x20) == (b | 0x20)))
                        return Utf8ExecutionLimitKind.None;
                }
            }
            offset += count;
        }
        equal = true;
        return Utf8ExecutionLimitKind.None;
    }

    internal static Utf8ExecutionLimitKind TryConfirmLiteral(
        in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int candidate, int length,
        ref Utf8SliceInvocation invocation, out bool accepted)
    {
        accepted = false;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        // Inspect neighbors on the original subject, including after a reverse
        // cursor's end floor. Enumeration bounds do not invent word boundaries.
        if (!Utf8ScalarWordBoundary.MatchesRequirement(plan.LeadingBoundary, input, candidate) ||
            !Utf8ScalarWordBoundary.MatchesRequirement(plan.TrailingBoundary, input, candidate + length))
            return Utf8ExecutionLimitKind.None;
        if (plan.TrailingLiteralUtf8 is { } trailing)
        {
            breach = TryCompareLiteral(input[(candidate + length)..], trailing, false, ref invocation, out var equal);
            if (breach != Utf8ExecutionLimitKind.None || !equal) return breach;
        }
        accepted = true;
        return Utf8ExecutionLimitKind.None;
    }
}
