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
        // Candidate blocks own search work. A prior denial must still stop
        // before even the unmetered finder; empty plans retain their checkpoint.
        if (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None ||
            plan.LiteralSearch is not { Length: > 0 } search)
            return invocation.TryChargeInputStep();

        var breach = Utf8ExecutionLimitKind.None;
        var low = start;
        var high = end - search.Length;
        while (low <= high)
        {
            breach = TryFindLiteralCandidate(search, input, low, high, reverse, ref invocation, out var candidate);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (candidate < 0) break;

            var accepted = true;
            if (plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement)
            {
                breach = TryConfirmLiteral(plan.LeadingBoundary, plan.TrailingBoundary, plan.TrailingLiteralUtf8,
                    input, candidate, search.Length, ref invocation, out accepted);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
            }
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
        in PreparedSubstringSearch search, ReadOnlySpan<byte> input, int low, int high,
        bool reverse, ref Utf8SliceInvocation invocation, out int candidate, bool stopAtKelvin = false)
    {
        candidate = -1;
        if (!stopAtKelvin && invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            var slice = input.Slice(low, high - low + search.Length);
            var relative = reverse ? search.LastIndexOf(slice) : search.IndexOf(slice);
            candidate = relative < 0 ? -1 : low + relative;
            return Utf8ExecutionLimitKind.None;
        }

        var first = search.Needle[0];
        var other = search.IgnoreCase && (uint)((first | 0x20) - 'a') <= 'z' - 'a'
            ? (byte)(first ^ 0x20) : first;
        // A short exact needle has bounded verification cost. Include its
        // lookahead in the 2,048-byte window. Medium forward needles use a
        // complete sequence search within that same charged window.
        var preparedWindow = !search.IgnoreCase && search.Length <= 32;
        var sequenceWindow = preparedWindow && !reverse && search.Length is >= 5 and <= 16;
        var lookahead = preparedWindow ? search.Length - 1 : 0;
        if (stopAtKelvin)
        {
            // Still look for an alias beyond the last possible ASCII start.
            // It can complete a candidate whose ASCII comparison failed.
            lookahead = "K"u8.Length - 1;
        }
        var last = stopAtKelvin ? input.Length - 1 : high;
        while (low <= last)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var count = Math.Min(2048 - lookahead, last - low + 1);
            var blockStart = reverse ? last - count + 1 : low;
            var blockLength = stopAtKelvin ? Math.Min(count + lookahead, input.Length - blockStart) : count + lookahead;
            var block = input.Slice(blockStart, blockLength);
            int relative;
            if (stopAtKelvin) relative = IndexOfFoldedCandidateOrKelvin(block, count, first, other);
            else if (sequenceWindow) relative = block.IndexOf(search.Needle);
            else if (preparedWindow) relative = reverse ? search.LastIndexOf(block) : search.IndexOf(block);
            else relative = reverse ? block.LastIndexOfAny(first, other) : block.IndexOfAny(first, other);
            if (relative < 0)
            {
                if (reverse) last -= count;
                else low += count;
                continue;
            }

            var found = blockStart + relative;
            if (stopAtKelvin && input[found] == "K"u8[0])
            {
                candidate = found;
                return Utf8ExecutionLimitKind.None;
            }
            if (stopAtKelvin && found > high)
            {
                low = found + 1;
                continue;
            }
            var equal = true;
            if (!preparedWindow)
            {
                breach = TryCompareLiteral(input.Slice(found, search.Length), search.Needle,
                    search.IgnoreCase, ref invocation, out equal);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
            }
            if (equal)
            {
                candidate = found;
                return Utf8ExecutionLimitKind.None;
            }
            if (reverse) last = found - 1;
            else low = found + 1;
        }
        return Utf8ExecutionLimitKind.None;
    }

    private static int IndexOfFoldedCandidateOrKelvin(ReadOnlySpan<byte> block, int count, byte first, byte other)
    {
        var starts = block[..count];
        var relative = starts.IndexOfAny(first, other, "K"u8[0]);
        if (relative < 0 || starts[relative] != "K"u8[0]) return relative;

        if (block[relative..].StartsWith("K"u8)) return relative;
        // An unrelated scalar shares this lead. Search for a real alias only
        // up to the next ASCII candidate so dense matches do not rescan a block.
        var next = relative + 1;
        var ascii = starts[next..].IndexOfAny(first, other);
        var end = ascii >= 0 ? next + ascii : block.Length;
        var alias = block[next..end].IndexOf("K"u8);
        if (alias >= 0) return next + alias;
        return ascii >= 0 ? next + ascii : -1;
    }

    internal static Utf8ExecutionLimitKind TryCompareLiteral(
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal, bool ignoreCase,
        ref Utf8SliceInvocation invocation, out bool equal, bool reverse = false)
    {
        equal = false;
        if (input.Length < literal.Length) return Utf8ExecutionLimitKind.None;
        for (var offset = 0; offset < literal.Length;)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var count = Math.Min(2048, literal.Length - offset);
            var start = reverse ? literal.Length - offset - count : offset;
            var actual = input.Slice(start, count);
            var expected = literal.Slice(start, count);
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
        Utf8BoundaryRequirement leadingBoundary, Utf8BoundaryRequirement trailingBoundary,
        byte[]? trailingLiteral, ReadOnlySpan<byte> input, int candidate, int length,
        ref Utf8SliceInvocation invocation, out bool accepted)
    {
        accepted = false;
        // At most four scalar neighbors. Search windows, a trailing literal
        // comparison and the final outcome retain their direct clock checks.
        var breach = invocation.TryChargeBoundedCheck();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        // Inspect neighbors on the original subject, including after a reverse
        // cursor's end floor. Enumeration bounds do not invent word boundaries.
        if (!Utf8ScalarWordBoundary.MatchesRequirement(leadingBoundary, input, candidate) ||
            !Utf8ScalarWordBoundary.MatchesRequirement(trailingBoundary, input, candidate + length))
            return Utf8ExecutionLimitKind.None;
        if (trailingLiteral is { } trailing)
        {
            breach = TryCompareLiteral(input[(candidate + length)..], trailing, false, ref invocation, out var equal);
            if (breach != Utf8ExecutionLimitKind.None || !equal) return breach;
        }
        accepted = true;
        return Utf8ExecutionLimitKind.None;
    }
}
