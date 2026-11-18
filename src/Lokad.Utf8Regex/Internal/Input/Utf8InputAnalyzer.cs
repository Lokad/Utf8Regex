using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8InputAnalyzer
{
    public static bool IsAscii(ReadOnlySpan<byte> input)
    {
        return System.Text.Ascii.IsValid(input);
    }

    internal static bool IsAscii(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        if (invocation.IsUnmetered && invocation.CheckpointHook is null) return IsAscii(input);
        for (var offset = 0; offset < input.Length;)
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            var count = Math.Min(2048, input.Length - offset);
            if (!IsAscii(input.Slice(offset, count))) return false;
            offset += count;
        }
        return true;
    }
}
