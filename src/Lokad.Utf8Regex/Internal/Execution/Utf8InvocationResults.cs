namespace Lokad.Utf8Regex.Internal.Execution;

// Result graphs remain working storage until the operation publishes them.
// Borrowed canonical values are excluded; temporary roots release their own
// extent at the last read. Publication transfers ownership to the caller, and
// does not implement a retained-result policy. Covered capacity traffic is
// charged cumulatively before acquisition and is independent of working release.
internal struct Utf8InvocationResultLedger
{
    private ulong _reservedBytes;

    internal readonly ulong ReservedBytes => _reservedBytes;

    internal T[] AllocateArray<T>(int count, ref Utf8SliceInvocation invocation)
    {
        var bytes = Utf8ConstructionBudget.ArrayBytes<T>(count);
        Reserve(bytes, ref invocation);
        var control = new Utf8InvocationBufferControl(ref invocation);
        try
        {
            Utf8InvocationOutput.Checkpoint(control);
            var result = count == 0 ? [] : new T[count];
            Utf8InvocationOutput.Checkpoint(control);
            _reservedBytes += bytes;
            return result;
        }
        catch { control.ReleaseWorking(bytes); throw; }
    }

    internal string DecodeUtf8(ReadOnlySpan<byte> bytes, int charCount, ref Utf8SliceInvocation invocation)
    {
        var control = new Utf8InvocationBufferControl(ref invocation);
        var extent = Utf8ConstructionBudget.StringBytes(charCount);
        Reserve(extent, ref invocation);
        try
        {
            var result = Utf8SliceBufferWork.DecodeUtf8(bytes, charCount, ref invocation);
            _reservedBytes += extent;
            return result;
        }
        catch { control.ReleaseWorking(extent); throw; }
    }

    internal static void Copy<T>(ReadOnlySpan<T> source, Span<T> destination, ref Utf8SliceInvocation invocation)
    {
        if (source.Length > destination.Length) throw new ArgumentException("The result destination is too small.", nameof(destination));
        var control = new Utf8InvocationBufferControl(ref invocation);
        Utf8InvocationOutput.Checkpoint(control);
        for (var offset = 0; offset < source.Length;)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var count = Math.Min(256, source.Length - offset);
            source.Slice(offset, count).CopyTo(destination.Slice(offset, count));
            offset += count;
        }
        Utf8InvocationOutput.Checkpoint(control);
    }

    internal void ReleaseArray<T>(T[] array, ref Utf8SliceInvocation invocation)
    {
        var extent = Utf8ConstructionBudget.ArrayBytes<T>(array.Length);
        ReleaseSince(_reservedBytes - extent, ref invocation);
    }

    internal void ReleaseSince(ulong marker, ref Utf8SliceInvocation invocation)
    {
        if (marker > _reservedBytes) throw new InvalidOperationException("The result reservation marker is no longer live.");
        invocation.Ledger.Release(_reservedBytes - marker);
        _reservedBytes = marker;
    }

    internal void Release(ref Utf8SliceInvocation invocation) => ReleaseSince(0, ref invocation);

    private static void Reserve(ulong bytes, ref Utf8SliceInvocation invocation)
    {
        Utf8SliceBufferWork.ChargeResultBytes(bytes, ref invocation);
        var breach = new Utf8InvocationBufferControl(ref invocation).TryReserveWorking(bytes);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Owned result exceeded {breach}.");
    }
}

// Exact staging extents reuse the invocation buffer's preallocation admission,
// overlap accounting and reference clearing. Final roots overlap staging.
internal struct Utf8InvocationResultBuffer<T>
{
    private Utf8InvocationStateBuffer<T> _items;

    internal readonly int Count => _items.Count;
    internal readonly T this[int index] => _items[index];

    internal void Add(T value, ref Utf8SliceInvocation invocation)
    {
        var control = new Utf8InvocationBufferControl(ref invocation);
        Utf8InvocationOutput.Checkpoint(control);
        Utf8SliceBufferWork.ChargeResultBytes(_items.PendingGrowthBytes, ref invocation);
        var breach = _items.TryPush(value, control);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Owned result staging exceeded {breach}.");
    }

    internal T[] ToArray(ref Utf8InvocationResultLedger ledger, ref Utf8SliceInvocation invocation)
    {
        var result = ledger.AllocateArray<T>(Count, ref invocation);
        var control = new Utf8InvocationBufferControl(ref invocation);
        for (var offset = 0; offset < result.Length;)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var count = Math.Min(256, result.Length - offset);
            _items.WrittenItems.Slice(offset, count).CopyTo(result.AsSpan(offset, count));
            offset += count;
        }
        Utf8InvocationOutput.Checkpoint(control);
        return result;
    }

    internal void Release(ref Utf8SliceInvocation invocation) => _items.Release(ref invocation);
}
