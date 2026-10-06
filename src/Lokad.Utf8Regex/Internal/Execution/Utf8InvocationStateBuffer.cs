using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

// Exact extents make aggregate admission authoritative before allocation,
// including the old/new overlap during growth. Reference-bearing frames borrow
// immutable programs; popped and retained slots release their graph references.
internal struct Utf8InvocationStateBuffer<T> where T : struct
{
    private T[]? _items;
    private int _count;
    private ulong _reservedBytes;

    internal readonly int Count => _count;

    internal readonly T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            return GetItems()[index];
        }
    }

    internal Utf8ExecutionLimitKind TryPush(T value, ref Utf8SliceInvocation invocation)
    {
        if (_items is null || _count == _items.Length)
        {
            var old = _items;
            var capacity = old is null ? 16 : old.Length <= Array.MaxLength / 2 ? old.Length * 2 : Array.MaxLength;
            if (capacity <= _count) return Utf8ExecutionLimitKind.WorkingBytes;
            var bytes = (ulong)(uint)capacity * (ulong)(uint)Unsafe.SizeOf<T>();
            if (!invocation.Ledger.TryReserve(bytes)) return Utf8ExecutionLimitKind.WorkingBytes;
            var adopted = false;
            try
            {
                var breach = invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var grown = Utf8ExactStatePool<T>.Rent(capacity, invocation.Ledger.IsUnlimited);
                try
                {
                    for (var offset = 0; offset < _count;)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        var count = Math.Min(2048, _count - offset);
                        if (old is not null) old.AsSpan(offset, count).CopyTo(grown.AsSpan(offset, count));
                        offset += count;
                    }
                    _items = grown;
                    invocation.Ledger.Release(_reservedBytes);
                    _reservedBytes = bytes;
                    adopted = true;
                    if (old is not null) Utf8ExactStatePool<T>.Return(old, invocation.Ledger.IsUnlimited);
                }
                finally { if (!adopted) Utf8ExactStatePool<T>.Return(grown, invocation.Ledger.IsUnlimited); }
            }
            finally
            {
                if (!adopted) invocation.Ledger.Release(bytes);
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
    {
        invocation.Ledger.Release(_reservedBytes);
        _reservedBytes = 0;
        if (_items is { } items) Utf8ExactStatePool<T>.Return(items, invocation.Ledger.IsUnlimited);
        _items = null;
        _count = 0;
    }

    private readonly T[] GetItems() => _items ?? throw new InvalidOperationException("The invocation state buffer has no storage.");
}
