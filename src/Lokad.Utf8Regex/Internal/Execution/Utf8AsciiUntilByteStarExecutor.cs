namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiUntilByteStarExecutor
{
    public static int Count(ReadOnlySpan<byte> input, byte terminatorByte)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        TryCountValidated(input, terminatorByte, ref invocation, out var count);
        return count;
    }

    // The whole-pattern proof excludes exactly one ASCII scalar. Once UTF-8 is
    // validated, its byte cannot occur inside a wider scalar. Chunk boundaries
    // may split a scalar, but must not split a nonempty run into extra matches.
    internal static Utf8ExecutionLimitKind TryCountValidated(ReadOnlySpan<byte> input, byte terminatorByte,
        ref Utf8SliceInvocation invocation, out int count)
    {
        count = 1; // Empty match at end of input.
        var inRun = false;
        for (var offset = 0; offset < input.Length;)
        {
            var breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var block = input.Slice(offset, Math.Min(256, input.Length - offset));
            offset += block.Length;
            while (!block.IsEmpty)
            {
                var relative = block.IndexOf(terminatorByte);
                if (relative < 0) { inRun = true; break; }
                if (relative > 0 || inRun) count++;
                count++; // Empty match at this excluded scalar, then advance it.
                inRun = false;
                block = block[(relative + 1)..];
            }
        }
        if (inRun) count++;
        var publication = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return publication;
    }
}
