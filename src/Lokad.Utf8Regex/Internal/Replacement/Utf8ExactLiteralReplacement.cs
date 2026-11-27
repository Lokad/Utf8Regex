using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8ExactLiteralReplacement
{
    // A miss lets the caller publish the unchanged subject with its own
    // output admission and lifetime.
    internal static byte[]? Replace(
        ReadOnlySpan<byte> input,
        in PreparedSubstringSearch search,
        ReadOnlySpan<byte> replacement,
        int maximumReplacementCount,
        int startOffsetInBytes,
        ref Utf8SliceInvocation invocation,
        ref Utf8InvocationResultLedger results,
        out int replacementCount)
    {
        var control = new Utf8InvocationBufferControl(ref invocation);
        replacementCount = 0;
        // Short substitutions already find these positions while sizing
        // the output. Keep them on the stack for the copy pass.
        Span<int> matchStarts = stackalloc int[8];
        var nextStart = startOffsetInBytes;
        var lastStart = input.Length - search.Length;
        while (replacementCount < maximumReplacementCount)
        {
            var index = FindNext(input, search, nextStart, lastStart, ref invocation);
            if (index < 0)
            {
                break;
            }

            if (replacementCount < matchStarts.Length)
                matchStarts[replacementCount] = index;
            replacementCount++;
            nextStart = index + search.Length;
        }

        if (replacementCount == 0) return null;

        var outputLength = checked(input.Length + replacementCount * (replacement.Length - search.Length));
        var output = results.AllocateArray<byte>(outputLength, ref invocation);
        // Copy from the start of the subject so the prefix before the search still reaches the result.
        var sourcePosition = 0;
        var destinationPosition = 0;
        var copied = 0;
        while (copied < replacementCount)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var matchStart = copied < matchStarts.Length
                ? matchStarts[copied]
                : FindNext(input, search, sourcePosition, lastStart, ref invocation);
            if (matchStart < sourcePosition)
            {
                throw new InvalidOperationException(
                    "The exact replacement copy pass lost a preflighted match.");
            }

            var gap = input[sourcePosition..matchStart];
            var destination = output.AsSpan(destinationPosition);
            if (gap.Length <= Utf8InvocationOutput.CopyBlockLength - replacement.Length)
            {
                // The iteration checkpoint admits one output block. Both
                // fragments fit in it and the destination is freshly owned.
                gap.CopyTo(destination);
                replacement.CopyTo(destination[gap.Length..]);
            }
            else
            {
                Utf8InvocationOutput.Copy(gap, destination, control);
                Utf8InvocationOutput.Copy(replacement, destination[gap.Length..], control);
            }
            destinationPosition += gap.Length + replacement.Length;
            sourcePosition = matchStart + search.Length;
            copied++;
        }

        Utf8InvocationOutput.Copy(input[sourcePosition..], output.AsSpan(destinationPosition), control);
        return output;
    }

    private static int FindNext(ReadOnlySpan<byte> input, in PreparedSubstringSearch search, int start, int last,
        ref Utf8SliceInvocation invocation)
    {
        if (start > last) return -1;
        var breach = Utf8SearchExecutor.TryFindLiteralCandidate(search, input, start, last, false, ref invocation, out var candidate);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Exact replacement search exceeded {breach}.");
        return candidate;
    }
}
