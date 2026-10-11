using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

internal static class PythonReAsciiPrefixDigitMatcher
{
    public static int MatchUnchecked(ReadOnlySpan<byte> input, ReadOnlySpan<byte> prefix,
        ref Utf8SliceInvocation invocation)
    {
        if (!MatchesPrefix(input, prefix, ref invocation))
        {
            return 0;
        }

        var end = prefix.Length;
        var digitStart = end;
        var control = new Utf8InvocationBufferControl(ref invocation);
        while ((uint)end < (uint)input.Length)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var limit = end + Math.Min(256, input.Length - end);
            for (; end < limit; end++)
                if (input[end] is < (byte)'0' or > (byte)'9')
                {
                    Utf8InvocationOutput.Checkpoint(control);
                    return end == digitStart ? 0 : end;
                }
        }
        Utf8InvocationOutput.Checkpoint(control);
        return end == digitStart ? 0 : end;
    }

    // Literal fragments are proven ASCII by the classifier. The original
    // subject is validated by the owning caller; each comparison is bounded.
    internal static bool MatchesPrefix(ReadOnlySpan<byte> input, ReadOnlySpan<byte> prefix,
        ref Utf8SliceInvocation invocation)
    {
        var control = new Utf8InvocationBufferControl(ref invocation);
        Utf8InvocationOutput.Checkpoint(control);
        if (input.Length < prefix.Length) return false;
        for (var offset = 0; offset < prefix.Length;)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var count = Math.Min(256, prefix.Length - offset);
            if (!input.Slice(offset, count).SequenceEqual(prefix.Slice(offset, count))) return false;
            offset += count;
        }
        Utf8InvocationOutput.Checkpoint(control);
        return true;
    }
}
