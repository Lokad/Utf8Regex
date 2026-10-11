namespace Lokad.Utf8Regex.Internal.Execution;

// Result graphs remain working storage until the operation publishes them.
// Borrowed canonical values are excluded; temporary roots release their own
// extent at the last read. Publication transfers ownership to the caller, and
// does not implement a retained-result policy.
internal struct Utf8InvocationResultLedger
{
    private ulong _reservedBytes;

    internal readonly ulong ReservedBytes => _reservedBytes;

    internal T[] AllocateArray<T, TControl>(int count, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        var bytes = Utf8ConstructionBudget.ArrayBytes<T>(count);
        Reserve(bytes, control);
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
        Reserve(extent, control);
        try
        {
            var result = Utf8SliceBufferWork.DecodeUtf8(bytes, charCount, ref invocation);
            _reservedBytes += extent;
            return result;
        }
        catch { control.ReleaseWorking(extent); throw; }
    }

    internal static void Copy<T, TControl>(ReadOnlySpan<T> source, Span<T> destination, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        if (source.Length > destination.Length) throw new ArgumentException("The result destination is too small.", nameof(destination));
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

    internal void ReleaseArray<T, TControl>(T[] array, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        var extent = Utf8ConstructionBudget.ArrayBytes<T>(array.Length);
        ReleaseSince(_reservedBytes - extent, control);
    }

    internal void ReleaseSince<TControl>(ulong marker, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        if (marker > _reservedBytes) throw new InvalidOperationException("The result reservation marker is no longer live.");
        control.ReleaseWorking(_reservedBytes - marker);
        _reservedBytes = marker;
    }

    internal void Release<TControl>(scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct => ReleaseSince(0, control);

    private static void Reserve<TControl>(ulong bytes, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        var breach = control.TryReserveWorking(bytes);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Owned result exceeded {breach}.");
    }
}

// Exact staging extents reuse the invocation buffer's preallocation admission,
// overlap accounting and reference clearing. Final roots overlap staging.
internal struct Utf8InvocationResultBuffer<T>
{
    private readonly record struct StoredResult(T Value);
    private Utf8InvocationStateBuffer<StoredResult> _items;

    internal readonly int Count => _items.Count;
    internal readonly T this[int index] => _items[index].Value;

    internal void Add<TControl>(T value, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        Utf8InvocationOutput.Checkpoint(control);
        var breach = _items.TryPush(new StoredResult(value), control);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Owned result staging exceeded {breach}.");
    }

    internal T[] ToArray<TControl>(ref Utf8InvocationResultLedger ledger, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        var result = ledger.AllocateArray<T, TControl>(Count, control);
        for (var offset = 0; offset < result.Length;)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var end = Math.Min(offset + 256, result.Length);
            for (; offset < end; offset++) result[offset] = _items[offset].Value;
        }
        Utf8InvocationOutput.Checkpoint(control);
        return result;
    }

    internal void Release<TControl>(scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct => _items.Release(control);
}
