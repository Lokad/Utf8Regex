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

    internal static void Copy(ReadOnlySpan<byte> source, Span<byte> destination, ref Utf8SliceInvocation invocation)
    {
        for (var offset = 0; offset < source.Length;)
        {
            Checkpoint(ref invocation);
            var count = Math.Min(4096, source.Length - offset);
            source.Slice(offset, count).CopyTo(destination.Slice(offset, count));
            offset += count;
        }
    }

    // Fragments may be malformed before the completed byte stream is assembled.
    // Validate only that stream, in bounded blocks, retaining absolute error offsets.
    internal static void ValidateUtf8(ReadOnlySpan<byte> bytes, ref Utf8SliceInvocation invocation)
    {
        for (var offset = 0; offset < bytes.Length;)
        {
            Checkpoint(ref invocation);
            var count = Utf8BlockLength(bytes[offset..]);
            if (!Input.Utf8ValidationCore.TryValidate(bytes.Slice(offset, count), false, out _, out var error))
                throw Input.Utf8Validation.CreateInvalidUtf8Exception(offset + error);
            offset += count;
        }
    }

    internal static int Utf8BlockLength(ReadOnlySpan<byte> remaining)
    {
        var count = Math.Min(4096, remaining.Length);
        if (count == remaining.Length) return count;
        var cut = count;
        while (count - cut < 3 && (remaining[cut] & 0xc0) == 0x80) cut--;
        // Four or more consecutive continuations are malformed. Retaining the
        // block preserves its first invalid offset rather than truncating a lead.
        return (remaining[cut] & 0xc0) == 0x80 ? count : cut;
    }

    // The caller has validated the bytes and admitted the resulting char payload.
    internal static string DecodeUtf8(ReadOnlySpan<byte> bytes, int charCount, ref Utf8SliceInvocation invocation)
    {
        var state = new DecodeState(bytes, ref invocation);
        return string.Create(charCount, state, static (destination, decode) =>
        {
            var written = 0;
            for (var offset = 0; offset < decode.Bytes.Length;)
            {
                Checkpoint(ref decode.Invocation);
                var count = Utf8BlockLength(decode.Bytes[offset..]);
                written += System.Text.Encoding.UTF8.GetChars(decode.Bytes.Slice(offset, count), destination[written..]);
                offset += count;
            }
        });
    }

    private ref struct DecodeState(ReadOnlySpan<byte> bytes, ref Utf8SliceInvocation invocation)
    {
        internal ReadOnlySpan<byte> Bytes = bytes;
        internal ref Utf8SliceInvocation Invocation = ref invocation;
    }
}
