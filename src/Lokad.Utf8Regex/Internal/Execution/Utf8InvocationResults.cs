namespace Lokad.Utf8Regex.Internal.Execution;

// Result graphs remain working storage until the operation publishes them.
// Borrowed canonical values are excluded; temporary roots release their own
// extent at the last read. Ordinary publication releases staging; explicit
// retained receipts can take over its already admitted loan. Covered traffic is
// charged cumulatively before acquisition and is independent of working release.
internal struct Utf8InvocationResultLedger
{
    private ulong _reservedBytes;

    internal readonly ulong ReservedBytes => _reservedBytes;

    // Shared immutable storage is conservatively counted again when a retained
    // result takes an independent lifetime. No copying or identity registry.
    internal void ReserveSharedStorage(ulong bytes, ref Utf8SliceInvocation invocation)
    {
        Reserve(bytes, ref invocation);
        _reservedBytes += bytes;
    }

    internal T AllocateObject<TState, T>(TState state, Func<TState, T> factory, ref Utf8SliceInvocation invocation)
        where T : class
    {
        var bytes = Math.Max(128UL, Utf8ConstructionBudget.ObjectBytes<TState>());
        Reserve(bytes, ref invocation);
        var control = new Utf8InvocationBufferControl(ref invocation);
        try
        {
            Utf8InvocationOutput.Checkpoint(control);
            var result = factory(state);
            Utf8InvocationOutput.Checkpoint(control);
            _reservedBytes += bytes;
            return result;
        }
        catch { control.ReleaseWorking(bytes); throw; }
    }

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

    internal T[] CompleteArray<T>(ref Utf8InvocationStateBuffer<T> staging, ref Utf8SliceInvocation invocation)
    {
        var control = new Utf8InvocationBufferControl(ref invocation);
        if (staging.TakeFullArray(out var reservation) is { } full)
        {
            // Acquisition was already admitted and charged during staging.
            // Adopt its live loan before any interruption can leave this method.
            _reservedBytes += reservation;
            Utf8InvocationOutput.Checkpoint(control);
            return full;
        }
        var result = AllocateArray<T>(staging.Count, ref invocation);
        for (var offset = 0; offset < result.Length;)
        {
            Utf8InvocationOutput.Checkpoint(control);
            var count = Math.Min(256, result.Length - offset);
            staging.WrittenItems.Slice(offset, count).CopyTo(result.AsSpan(offset, count));
            offset += count;
        }
        Utf8InvocationOutput.Checkpoint(control);
        return result;
    }

    internal string DecodeUtf8(ReadOnlySpan<byte> bytes, int? charCount, ref Utf8SliceInvocation invocation)
    {
        // Complete valid short ranges can be decoded directly after admitting
        // their byte-derived UTF-16 upper bound. Known lengths stay exact;
        // larger ranges retain bounded sizing and decoding checkpoints.
        if (charCount is null && bytes.Length > 4096)
            charCount = Utf8SliceBufferWork.CountUtf16(bytes, ref invocation);
        var control = new Utf8InvocationBufferControl(ref invocation);
        var extent = Utf8ConstructionBudget.StringBytes(charCount ?? bytes.Length);
        Reserve(extent, ref invocation);
        try
        {
            string result;
            if (charCount is { } exact)
                result = Utf8SliceBufferWork.DecodeUtf8(bytes, exact, ref invocation);
            else
            {
                Utf8InvocationOutput.Checkpoint(control);
                result = System.Text.Encoding.UTF8.GetString(bytes);
                Utf8InvocationOutput.Checkpoint(control);
            }
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

    internal ulong TransferSince(ulong marker, ref Utf8SliceInvocation invocation)
    {
        if (marker > _reservedBytes) throw new InvalidOperationException("The result reservation marker is no longer live.");
        var bytes = _reservedBytes - marker;
        invocation.Ledger.TransferToCaller(bytes);
        _reservedBytes = marker;
        return bytes;
    }

    private static void Reserve(ulong bytes, ref Utf8SliceInvocation invocation)
    {
        Utf8SliceBufferWork.ChargeResultBytes(bytes, ref invocation);
        var breach = new Utf8InvocationBufferControl(ref invocation).TryReserveWorking(bytes);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Owned result exceeded {breach}.");
    }
}

// Exact staging extents reuse the invocation buffer's preallocation admission,
// overlap accounting and reference clearing. Completion can transfer full arrays;
// partial arrays require a separate exact root overlapping staging.
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
        => ledger.CompleteArray(ref _items, ref invocation);

    internal void Release(ref Utf8SliceInvocation invocation) => _items.Release(ref invocation);
}
