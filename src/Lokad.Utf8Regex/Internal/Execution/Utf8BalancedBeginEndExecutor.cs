using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8BalancedBeginEndExecutor
{
    private const int MaxNativeNestingDepth = 128;

    public static bool TryCount(ReadOnlySpan<byte> input, out int count)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        return TryCountWellFormed(input, ref invocation, out count, out var handled) == Utf8ExecutionLimitKind.None && handled;
    }

    // The complete classifier proves both balancing effects and the guarded
    // scalar branch. ASCII delimiters cannot occur inside a validated scalar.
    // The bounded stack can decline without publishing its provisional count.
    internal static Utf8ExecutionLimitKind TryCountWellFormed(ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out int count, out bool handled)
    {
        count = 0;
        handled = false;
        var reservation = (ulong)MaxNativeNestingDepth * (ulong)Unsafe.SizeOf<int>();
        var breach = invocation.TryReserveWorking(reservation);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        try
        {
            Span<int> completedChildrenByDepth = stackalloc int[MaxNativeNestingDepth];
            var depth = 0;
            var rootCount = 0;
            var searchStart = 0;

            while (searchStart < input.Length)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var length = Math.Min(2048, input.Length - searchStart);
                var relativeIndex = input.Slice(searchStart, length).IndexOfAny((byte)'B', (byte)'E');
                if (relativeIndex < 0)
                {
                    searchStart += length;
                    continue;
                }

                var tokenIndex = searchStart + relativeIndex;
                if (input[tokenIndex] == (byte)'B' && input[tokenIndex..].StartsWith("BEGIN"u8))
                {
                    if (depth == completedChildrenByDepth.Length)
                    {
                        return Utf8ExecutionLimitKind.None;
                    }

                    breach = invocation.Budget.TryNoteDepth((uint)depth + 1);
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    completedChildrenByDepth[depth] = 0;
                    depth++;
                    searchStart = tokenIndex + "BEGIN"u8.Length;
                    continue;
                }

                if (input[tokenIndex] == (byte)'E' && input[tokenIndex..].StartsWith("END"u8))
                {
                    if (depth > 0)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            rootCount++;
                        }
                        else
                        {
                            completedChildrenByDepth[depth - 1]++;
                        }
                    }

                    searchStart = tokenIndex + "END"u8.Length;
                    continue;
                }

                searchStart = tokenIndex + 1;
            }

            // A failed outer BEGIN does not hide the complete matches nested within it:
            // Unanchored search retries at the next BEGIN, promoting completed children.
            while (depth > 0)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                depth--;
                var completedChildren = completedChildrenByDepth[depth];
                if (depth == 0)
                {
                    rootCount += completedChildren;
                }
                else
                {
                    completedChildrenByDepth[depth - 1] += completedChildren;
                }
            }

            breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach == Utf8ExecutionLimitKind.None)
            {
                count = rootCount;
                handled = true;
            }
            return breach;
        }
        finally { invocation.Ledger.Release(reservation); }
    }
}
