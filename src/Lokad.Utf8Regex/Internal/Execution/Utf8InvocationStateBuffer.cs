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
            var old = _items;
            var capacity = GrowthCapacity;
            if (capacity <= _count) return Utf8ExecutionLimitKind.WorkingBytes;
            var bytes = Utf8ConstructionBudget.ArrayBytes<T>(capacity);
            var reservationBreach = control.TryReserveWorking(bytes);
            if (reservationBreach != Utf8ExecutionLimitKind.None) return reservationBreach;
            var adopted = false;
            try
            {
                var breach = control.TryChargeWorkspaceStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var grown = Utf8ExactStatePool<T>.Rent(capacity, control.CanRetainWorkspace);
                try
                {
                    for (var offset = 0; offset < _count;)
                    {
                        breach = control.TryChargeWorkspaceStep();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        var count = Math.Min(2048, _count - offset);
                        if (old is not null) old.AsSpan(offset, count).CopyTo(grown.AsSpan(offset, count));
                        offset += count;
                    }
                    _items = grown;
                    control.ReleaseWorking(_reservedBytes);
                    _reservedBytes = bytes;
                    adopted = true;
                    if (old is not null) Utf8ExactStatePool<T>.Return(old, control.CanRetainWorkspace);
                }
                finally { if (!adopted) Utf8ExactStatePool<T>.Return(grown, control.CanRetainWorkspace); }
            }
            finally
            {
                if (!adopted) control.ReleaseWorking(bytes);
            }
        }
        GetItems()[_count++] = value;
        return Utf8ExecutionLimitKind.None;
    }

    internal void Pop()
    {
        if (_count == 0) throw new InvalidOperationException("The invocation state buffer is empty.");
        _count--;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) GetItems()[_count] = default;
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
