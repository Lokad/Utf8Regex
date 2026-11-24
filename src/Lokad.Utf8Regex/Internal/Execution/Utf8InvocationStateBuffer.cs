using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

// Exact extents make aggregate admission authoritative before allocation,
// including the old/new overlap during growth. Reference-bearing frames borrow
// immutable programs; popped and retained slots release their graph references.
internal struct Utf8InvocationStateBuffer<T>
{
    private T[]? _items;
    private int _count;
    private ulong _reservedBytes;

    internal readonly int Count => _count;

    // Result staging can fund the next acquisition without a second growth
    // policy or a specialized VM control. Reading this never reserves storage.
    internal readonly ulong PendingGrowthBytes =>
        (_items is null || _count == _items.Length) && GrowthCapacity > _count
            ? Utf8ConstructionBudget.ArrayBytes<T>(GrowthCapacity) : 0;

    private readonly int GrowthCapacity => _items is null ? 16
        : _items.Length <= Array.MaxLength / 2 ? _items.Length * 2 : Array.MaxLength;

    internal readonly ReadOnlySpan<T> WrittenItems => _items is null ? [] : _items.AsSpan(0, _count);

    internal readonly T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            return GetItems()[index];
        }
    }

    internal Utf8ExecutionLimitKind TryPush(T value, ref Utf8SliceInvocation invocation)
        => TryPush(value, new Utf8InvocationBufferControl(ref invocation));

    internal Utf8ExecutionLimitKind TryPush<TControl>(T value, scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        if (_items is null || _count == _items.Length)
        {
            var breach = TryGrow(ref _items, _count, GrowthCapacity, ref _reservedBytes, ref control, out _);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
        }
        GetItems()[_count++] = value;
        return Utf8ExecutionLimitKind.None;
    }

    // PCRE2-INTEGRATION-POINT: VM stacks share the old/new overlap admission
    // and transfer, with their existing checkpoints and exception mapping.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Utf8ExecutionLimitKind TryGrow<TControl>(ref T[]? items, int count, int capacity,
        ref ulong reservedBytes, scoped ref TControl control, out bool rented)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        rented = false;
        if (capacity <= count) return Utf8ExecutionLimitKind.WorkingBytes;
        var old = items;
        var bytes = Utf8ConstructionBudget.ArrayBytes<T>(capacity);
        var breach = control.TryReserveWorking(bytes);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var adopted = false;
        try
        {
            breach = control.TryChargeWorkspaceStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var grown = Utf8ExactStatePool<T>.Rent(capacity, control.CanRetainWorkspace);
            rented = true;
            try
            {
                for (var offset = 0; offset < count;)
                {
                    breach = control.TryChargeWorkspaceStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var block = Math.Min(2048, count - offset);
                    old.AsSpan(offset, block).CopyTo(grown.AsSpan(offset, block));
                    offset += block;
                }
                items = grown;
                control.ReleaseWorking(reservedBytes);
                reservedBytes = bytes;
                adopted = true;
                if (old is not null) Utf8ExactStatePool<T>.Return(old, control.CanRetainWorkspace);
                return Utf8ExecutionLimitKind.None;
            }
            finally { if (!adopted) Utf8ExactStatePool<T>.Return(grown, control.CanRetainWorkspace); }
        }
        finally { if (!adopted) control.ReleaseWorking(bytes); }
    }

    internal void Pop()
    {
        if (_count == 0) throw new InvalidOperationException("The invocation state buffer is empty.");
        _count--;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) GetItems()[_count] = default!;
    }

    // Callers retain their bounded interruption checkpoints around each chunk.
    // Value-only records need no per-item write; borrowed references still clear.
    internal void Pop(int count)
    {
        if ((uint)count > (uint)_count) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) GetItems().AsSpan(_count - count, count).Clear();
        _count -= count;
    }

    // A completed result can own an exactly filled array. Relinquish both the
    // leaf and its existing working reservation without returning it to the pool.
    internal T[]? TakeFullArray(out ulong reservedBytes)
    {
        reservedBytes = 0;
        if (_items is not { } items || _count != items.Length) return null;
        reservedBytes = _reservedBytes;
        this = default;
        return items;
    }

    internal void Release(ref Utf8SliceInvocation invocation)
        => Release(new Utf8InvocationBufferControl(ref invocation));

    internal void Release<TControl>(scoped TControl control)
        where TControl : IUtf8InvocationBufferControl, allows ref struct
    {
        control.ReleaseWorking(_reservedBytes);
        _reservedBytes = 0;
        if (_items is { } items) Utf8ExactStatePool<T>.Return(items, control.CanRetainWorkspace);
        _items = null;
        _count = 0;
    }

    private readonly T[] GetItems() => _items ?? throw new InvalidOperationException("The invocation state buffer has no storage.");
}
