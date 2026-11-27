using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Posix;

internal partial struct PosixWorkspace
{
    private bool TryMatchReference(ReadOnlySpan<byte> input, int start, int end, bool foldCase,
        ref int at, ref Utf8SliceInvocation invocation)
    {
        var capture = input[start..end];
        var remaining = input[at..];
        if (!foldCase)
        {
            if (remaining.Length < capture.Length) return false;
            Check(Utf8SearchExecutor.TryCompareLiteral(remaining[..capture.Length], capture, false,
                ref invocation, out var equal), ref invocation);
            if (equal) at += capture.Length;
            return equal;
        }

        var consumed = 0;
        while (!capture.IsEmpty)
        {
            Charge(ref invocation);
            if (Rune.DecodeFromUtf8(capture, out var left, out var leftWidth) != OperationStatus.Done ||
                Rune.DecodeFromUtf8(remaining[consumed..], out var right, out var rightWidth) != OperationStatus.Done ||
                !Utf8SliceCaseEquivalences.ScalarsEquivalent(left, right, _program.Scalar.CaseBehavior))
                return false;
            capture = capture[leftWidth..];
            consumed += rightWidth;
        }
        at += consumed;
        return true;
    }

    // A reference depends on its path's captures. Keep the winning trace
    // while the admitted retry stack continues looking for a longer match.
    private void SaveWinningHistory(ref Utf8SliceInvocation invocation)
    {
        Clear(ref _winnerTrace, ref invocation);
        var trace = _trace.WrittenItems;
        for (var index = 0; index < trace.Length; index++)
        {
            if ((index & 63) == 0) Charge(ref invocation);
            Check(_winnerTrace.TryPush(trace[index], ref invocation), ref invocation);
        }
    }

    private bool BetterHistory(ref Utf8SliceInvocation invocation)
    {
        var current = _trace.WrittenItems;
        var best = _winnerTrace.WrittenItems;
        for (var tag = 1; tag < _program.Parents.Length; tag++)
        {
            Charge(ref invocation);
            var parent = _program.Parents[tag];
            if (parent == 0)
            {
                // The root has one occurrence enclosing the entire match.
                var order = CompareOccurrences(current, best, tag, 0, current.Length - 1,
                    0, best.Length - 1, ref invocation);
                if (order != 0) return order < 0;
                continue;
            }

            var a = 0;
            var b = 0;
            while (true)
            {
                var aEnd = NextClose(current, parent, a, current.Length, ref invocation);
                var bEnd = NextClose(best, parent, b, best.Length, ref invocation);
                if (aEnd < 0 || bEnd < 0)
                {
                    if (aEnd != bEnd) throw new InvalidOperationException("Previously compared POSIX parent histories disagree.");
                    break;
                }
                var order = CompareOccurrences(current, best, tag, current[aEnd].Opening, aEnd,
                    best[bEnd].Opening, bEnd, ref invocation);
                if (order != 0) return order < 0;
                a = aEnd + 1;
                b = bEnd + 1;
            }
        }
        return false;
    }

    private static int CompareOccurrences(ReadOnlySpan<TagEvent> current, ReadOnlySpan<TagEvent> best,
        int tag, int a, int aEnd, int b, int bEnd, ref Utf8SliceInvocation invocation)
    {
        var first = true;
        while (true)
        {
            a = NextClose(current, tag, a + 1, aEnd, ref invocation);
            b = NextClose(best, tag, b + 1, bEnd, ref invocation);
            if (a < 0 || b < 0)
            {
                if (a == b) return 0;
                // A first occurrence wins over absence. After equal
                // occurrences, prefer the history without a redundant tail.
                if (first) return a < 0 ? 1 : -1;
                return a < 0 ? -1 : 1;
            }
            var startOrder = current[current[a].Opening].Position.CompareTo(best[best[b].Opening].Position);
            if (startOrder != 0) return startOrder;
            var endOrder = best[b].Position.CompareTo(current[a].Position);
            if (endOrder != 0) return endOrder;
            first = false;
        }
    }

    private static int NextClose(ReadOnlySpan<TagEvent> trace, int tag, int start, int end,
        ref Utf8SliceInvocation invocation)
    {
        for (var index = start; index < end; index++)
        {
            if (((index - start) & 63) == 0) Charge(ref invocation);
            if (trace[index].Tag == tag && trace[index].Opening >= 0) return index;
        }
        return -1;
    }
}
