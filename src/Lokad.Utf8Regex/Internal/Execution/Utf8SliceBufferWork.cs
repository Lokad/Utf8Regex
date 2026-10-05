namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Shared bounded-buffer checkpoint and copy mechanics for owned shaping.</summary>
internal static class Utf8SliceBufferWork
{
    internal static void Checkpoint(ref Utf8SliceInvocation invocation)
    {
        if (invocation.IsUnmetered) return;
        if (invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Owned buffer work exceeded the invocation budget.");
    }

    internal static void Copy(ReadOnlySpan<char> source, Span<char> destination, ref Utf8SliceInvocation invocation)
    {
        for (var offset = 0; offset < source.Length;)
        {
            Checkpoint(ref invocation);
            var count = Math.Min(2048, source.Length - offset);
            source.Slice(offset, count).CopyTo(destination.Slice(offset, count));
            offset += count;
        }
    }
}
