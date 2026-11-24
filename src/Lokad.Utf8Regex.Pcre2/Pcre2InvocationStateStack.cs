using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

// Single lexical owner. Read-only borrowed copies may inspect values; only the
// original may mutate or release storage, through its invocation's budget.
internal ref struct Pcre2InvocationStateStack<T> where T : struct
{
    private readonly int _maximumCount;
    private readonly int _initialCapacity;
    private readonly bool _trackRents;
    private T[]? _items;
    private int _count;
    private int _rentCount;
    private ulong _reservedBytes;

    internal Pcre2InvocationStateStack(int maximumCount, bool trackRents, int initialCapacity = 16)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCount);
        _maximumCount = maximumCount;
        _initialCapacity = initialCapacity;
        _trackRents = trackRents;
    }

    internal readonly int Count => _count;
    internal readonly int RentCount => _rentCount;
    internal readonly T this[int index] => (uint)index < (uint)_count
        ? GetItems()[index] : throw new ArgumentOutOfRangeException(nameof(index));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryPush(T value, ref Pcre2ResourceBudget budget)
    {
        if (_count >= _maximumCount) return false;
        var items = _items;
        if (items is null || _count == items.Length) items = Grow(ref budget);
        items[_count++] = value;
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private T[] Grow(ref Pcre2ResourceBudget budget)
    {
        var capacity = _items is null ? Math.Min(_initialCapacity, _maximumCount)
            : Math.Min(_items.Length <= Array.MaxLength / 2 ? _items.Length * 2 : Array.MaxLength, _maximumCount);
        var rented = false;
        try
        {
            if (Utf8InvocationStateBuffer<T>.TryGrow(ref _items, _count, capacity, ref _reservedBytes,
                ref budget, out rented) != Utf8ExecutionLimitKind.None)
                budget.ThrowWorkingLimit();
            return GetItems();
        }
        finally { if (_trackRents && rented) _rentCount++; }
    }

    internal T Pop()
    {
        if (_count == 0) throw new InvalidOperationException("The PCRE2 state stack is empty.");
        var items = GetItems();
        var index = --_count;
        var value = items[index];
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) items[index] = default;
        return value;
    }

    internal void Truncate(int count)
    {
        if ((uint)count > (uint)_count) throw new ArgumentOutOfRangeException(nameof(count));
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() && count < _count)
            GetItems().AsSpan(count, _count - count).Clear();
        _count = count;
    }

    internal void Dispose(ref Pcre2ResourceBudget budget)
    {
        // Failed growth refunds its new reservation before returning control.
        // Without an acquired array there is no stack loan to release.
        if (_items is not { } items) return;
        Utf8ExactStatePool<T>.Return(items, budget.CanRetainWorkspace);
        _items = null;
        _count = 0;
        budget.ReleaseWorking(_reservedBytes);
        _reservedBytes = 0;
    }

    private readonly T[] GetItems() => _items ?? throw new InvalidOperationException("The PCRE2 state stack has no storage.");
}
