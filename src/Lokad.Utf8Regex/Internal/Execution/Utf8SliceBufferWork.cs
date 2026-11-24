using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Shared bounded-buffer checkpoint and copy mechanics for owned shaping.</summary>
internal static class Utf8SliceBufferWork
{
    // Discovery supplies scalar-aligned endpoints. Publication still observes
    // the original invocation after matching, without a coordinate scan.
    internal static Utf8ExecutionLimitKind TryCreateMatchRange(int start, int end, ref Utf8SliceInvocation invocation, out Utf8CaptureRange match)
    {
        match = default;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        match = new Utf8CaptureRange(start, end);
        return Utf8ExecutionLimitKind.None;
    }

    internal static Utf8CaptureRange CreateMatchRange(int start, int end, ref Utf8SliceInvocation invocation)
    {
        var breach = TryCreateMatchRange(start, end, ref invocation, out var match);
        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Whole-match publication exceeded {breach}.");
        return match;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ChargeResultBytes(ulong bytes, ref Utf8SliceInvocation invocation)
    {
        var breach = invocation.Budget.TryChargeResultBytes(bytes);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Owned result materialization exceeded {breach}.");
    }

    internal static void Checkpoint(ref Utf8SliceInvocation invocation)
    {
        if (invocation.IsUnmetered && invocation.CheckpointHook is null) return;
        Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
    }

    internal static void Copy(ReadOnlySpan<byte> source, Span<byte> destination, ref Utf8SliceInvocation invocation)
        => Utf8InvocationOutput.Copy(source, destination, new Utf8InvocationBufferControl(ref invocation));

    internal static int Utf16BlockLength(ReadOnlySpan<char> remaining)
    {
        var count = Math.Min(2048, remaining.Length);
        if (count < remaining.Length && char.IsHighSurrogate(remaining[count - 1]) && char.IsLowSurrogate(remaining[count])) count--;
        return count;
    }

    // Fragments may be malformed before the completed byte stream is assembled.
    // Validate only that stream, in bounded blocks, retaining absolute error offsets.
    internal static void ValidateUtf8(ReadOnlySpan<byte> bytes, ref Utf8SliceInvocation invocation)
    {
        for (var offset = 0; offset < bytes.Length;)
        {
            Checkpoint(ref invocation);
            var count = Utf8BlockLength(bytes[offset..]);
            Input.Utf8Validation.ThrowIfInvalidOnly(bytes.Slice(offset, count), offset);
            offset += count;
        }
    }

    internal static int Utf8BlockLength(ReadOnlySpan<byte> remaining, int maxBlockLength = 4096)
    {
        var count = Math.Min(maxBlockLength, remaining.Length);
        if (count == remaining.Length) return count;
        var cut = count;
        while (count - cut < 3 && (remaining[cut] & 0xc0) == 0x80) cut--;
        // Four or more consecutive continuations are malformed. Retaining the
        // block preserves its first invalid offset rather than truncating a lead.
        return (remaining[cut] & 0xc0) == 0x80 ? count : cut;
    }
}
