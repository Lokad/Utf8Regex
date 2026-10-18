namespace Lokad.Utf8Regex.Internal.Execution;

// Exact output extents remain working storage through filling and publication.
// Returned arrays then transfer to the caller; retained-result policies are a
// separate gate. Views borrow the live operation authority without copying it.
internal static class Utf8InvocationOutput
{
    internal static void Checkpoint<TControl>(scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        var breach = control.TryChargeWorkspaceStep();
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Owned output exceeded {breach}.");
    }

    internal static Utf8ExecutionLimitKind TryAllocateBytes<TControl>(int count, scoped TControl control,
        out byte[] result, out ulong reservation)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        result = [];
        reservation = 0;
        var bytes = Utf8ConstructionBudget.ArrayBytes<byte>(count);
        var breach = control.TryChargeResultBytes(bytes);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        breach = control.TryReserveWorking(bytes);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        reservation = bytes;
        try
        {
            Checkpoint(control);
            result = count == 0 ? [] : new byte[count];
            return Utf8ExecutionLimitKind.None;
        }
        catch { control.ReleaseWorking(reservation); reservation = 0; throw; }
    }

    internal static byte[] AllocateBytes<TControl>(int count, scoped TControl control, out ulong reservation)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        var breach = TryAllocateBytes(count, control, out var result, out reservation);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Owned output exceeded {breach}.");
        return result;
    }

    internal static void Copy<TControl>(ReadOnlySpan<byte> source, Span<byte> destination, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        if (source.Length > destination.Length) throw new ArgumentException("The output destination is too small.", nameof(destination));
        destination = destination[..source.Length];
        // Preserve Span.CopyTo's memmove semantics across bounded blocks.
        // The loop admits its first block too; the terminal checkpoint also
        // observes the original authority when there are no bytes to copy.
        var backward = source.Overlaps(destination, out var displacement) && displacement > 0;
        for (var copied = 0; copied < source.Length;)
        {
            Checkpoint(control);
            var count = Math.Min(4096, source.Length - copied);
            var offset = backward ? source.Length - copied - count : copied;
            source.Slice(offset, count).CopyTo(destination.Slice(offset, count));
            copied += count;
        }
        Checkpoint(control);
    }
}
