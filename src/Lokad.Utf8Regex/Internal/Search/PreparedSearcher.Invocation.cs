using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedSearcher
{
    // Searches borrow the existing immutable payload. Neither long needles nor
    // quoted runs enter an opaque unbounded comparison in a metered operation.
    internal Utf8ExecutionLimitKind TryFindMatch(ReadOnlySpan<byte> input, int start, int end,
        bool reverse, ref Utf8SliceInvocation invocation, out PreparedSearchMatch match, out bool found)
    {
        match = default;
        found = false;
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var candidate = -1;
        var length = 1;
        if (Kind is PreparedSearcherKind.ExactLiteral or PreparedSearcherKind.IgnoreCaseLiteral)
        {
            length = LiteralSearch.Length;
            if (length == 0) candidate = reverse ? end : start;
            else if (length == 1 && !LiteralSearch.IgnoreCase && end - start <= 2048)
            {
                // One exact byte needs no literal-window setup. Keep the
                // same admission between the entry and publication checks.
                if (start != end && (!invocation.IsUnmetered || invocation.CheckpointHook is not null))
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                }
                var window = input[start..end];
                var value = LiteralSearch.Needle[0];
                var relative = reverse ? window.LastIndexOf(value) : window.IndexOf(value);
                if (relative >= 0) candidate = start + relative;
            }
            else
            {
                breach = Utf8SearchExecutor.TryFindLiteralCandidate(LiteralSearch, input, start, end - length,
                    reverse, ref invocation, out candidate);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
            }
        }
        else if (Kind == PreparedSearcherKind.MultiLiteral)
        {
            var literals = MultiLiteralSearch.Literals;
            var hasEmpty = false;
            foreach (var literal in literals)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                hasEmpty |= literal.Length == 0;
            }
            var folded = MultiLiteralSearch.Kind == PreparedMultiLiteralKind.AsciiIgnoreCase;
            if (!hasEmpty)
                return Utf8SearchExecutor.TryFindLiteralFamilyCandidate(literals, folded, input, start, end,
                    reverse, ref invocation, out match, out found);
            // Empty search anchors are candidate facts, not global regex matches.
            // Preserve their branch priority and terminal position without forcing
            // them into the nonempty literal-family execution kernel.
            for (var position = reverse ? end : start; position >= start && position <= end; position += reverse ? -1 : 1)
            {
                for (var id = 0; id < literals.Length; id++)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var literal = literals[id];
                    var index = position;
                    if (index < start || index > end - literal.Length) continue;
                    breach = Utf8SearchExecutor.TryCompareLiteral(input.Slice(index, literal.Length), literal,
                        folded, ref invocation, out var equal);
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    if (!equal) continue;
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    invocation.Budget.ThrowIfCancellationRequested();
                    match = new PreparedSearchMatch(index, literal.Length, id);
                    found = true;
                    return Utf8ExecutionLimitKind.None;
                }
                if (reverse ? position == start : position == end) break;
            }
        }
        else if (Kind == PreparedSearcherKind.ByteSet)
        {
            length = 1;
            var low = start;
            var high = end - length;
            while (low <= high)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var count = Math.Min(2048, high - low + 1);
                var blockStart = reverse ? high - count + 1 : low;
                var block = input.Slice(blockStart, count);
                var relative = reverse ? ByteSearch.LastIndexOf(block) : ByteSearch.IndexOf(block);
                if (relative < 0)
                {
                    if (reverse) high -= count;
                    else low += count;
                    continue;
                }
                candidate = blockStart + relative;
                break;
            }
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        if (candidate >= 0) { match = new PreparedSearchMatch(candidate, length, 0); found = true; }
        return Utf8ExecutionLimitKind.None;
    }
}
