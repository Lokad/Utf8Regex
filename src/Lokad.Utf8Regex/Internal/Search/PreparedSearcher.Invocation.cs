using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedSearcher
{
    internal Utf8ExecutionLimitKind TryFindNextOverlappingMatch(ReadOnlySpan<byte> input,
        ref PreparedSearchScanState state, ref Utf8SliceInvocation invocation,
        out PreparedSearchMatch match, out bool found)
    {
        match = default;
        found = false;
        var pending = state;
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
            found = TryFindNextOverlappingMatch(input, ref pending, out match);
        else
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if ((uint)state.NextStart <= (uint)input.Length)
            {
                breach = TryFindMatch(input, state.NextStart, input.Length, false,
                    ref invocation, out match, out found);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
            }
            pending = new PreparedSearchScanState(found ? match.Index + 1 : input.Length, default);
        }
        state = pending;
        return Utf8ExecutionLimitKind.None;
    }

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
        else if (Kind is PreparedSearcherKind.ByteSet or PreparedSearcherKind.QuotedAsciiRun)
        {
            length = Kind == PreparedSearcherKind.ByteSet ? 1 : QuotedAsciiRunSearch.MatchLength;
            var low = start;
            var high = end - length;
            while (low <= high)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var count = Math.Min(2048, high - low + 1);
                var blockStart = reverse ? high - count + 1 : low;
                var block = input.Slice(blockStart, count);
                var relative = Kind == PreparedSearcherKind.ByteSet
                    ? (reverse ? ByteSearch.LastIndexOf(block) : ByteSearch.IndexOf(block))
                    : (reverse ? block.LastIndexOfAny((byte)'\'', (byte)'"') : block.IndexOfAny((byte)'\'', (byte)'"'));
                if (relative < 0)
                {
                    if (reverse) high -= count;
                    else low += count;
                    continue;
                }
                var index = blockStart + relative;
                var equal = true;
                if (Kind == PreparedSearcherKind.QuotedAsciiRun)
                {
                    equal = input[index] == input[index + length - 1];
                    for (var offset = 0; equal && offset < QuotedAsciiRunSearch.RunLength;)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        var size = Math.Min(2048, QuotedAsciiRunSearch.RunLength - offset);
                        for (var i = 0; i < size; i++)
                            if (!QuotedAsciiRunSearch.AsciiSet.Contains(input[index + 1 + offset + i])) { equal = false; break; }
                        offset += size;
                    }
                }
                if (equal) { candidate = index; break; }
                if (reverse) high = index - 1;
                else low = index + 1;
            }
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        if (candidate >= 0) { match = new PreparedSearchMatch(candidate, length, 0); found = true; }
        return Utf8ExecutionLimitKind.None;
    }
}
