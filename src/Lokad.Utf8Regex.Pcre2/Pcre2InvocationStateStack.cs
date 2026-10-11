using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

// Single lexical owner. Read-only borrowed copies may inspect values; only the
// original may mutate or release storage, through its invocation's budget.
internal ref struct Pcre2InvocationStateStack<T> where T : struct
{
    private readonly int _maximumCount;
    private readonly bool _trackRents;
    private T[]? _items;
    private int _count;
    private int _rentCount;
    private ulong _reservedBytes;

    internal Pcre2InvocationStateStack(int maximumCount, bool trackRents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCount);
        _maximumCount = maximumCount;
        _trackRents = trackRents;
    }

    internal readonly int Count => _count;
    internal readonly int RentCount => _rentCount;
    internal readonly T this[int index] => (uint)index < (uint)_count
        ? GetItems()[index] : throw new ArgumentOutOfRangeException(nameof(index));

    internal bool TryPush(T value, ref Pcre2ResourceBudget budget)
    {
        if (_count >= _maximumCount) return false;
        if (_items is null || _count == _items.Length)
        {
            var old = _items;
            var capacity = old is null ? Math.Min(16, _maximumCount)
                : Math.Min(old.Length <= Array.MaxLength / 2 ? old.Length * 2 : Array.MaxLength, _maximumCount);
            if (capacity <= _count) budget.ThrowWorkingLimit();
            var bytes = Utf8ConstructionBudget.ArrayBytes<T>(capacity);
            budget.ReserveWorking(bytes);
            var adopted = false;
            try
            {
                budget.ChargeWorkspaceStep();
                var grown = Utf8ExactStatePool<T>.Rent(capacity, budget.CanRetainWorkspace);
                if (_trackRents) _rentCount++;
                try
                {
                    for (var offset = 0; offset < _count;)
                    {
                        budget.ChargeWorkspaceStep();
                        var count = Math.Min(2048, _count - offset);
                        if (old is not null) old.AsSpan(offset, count).CopyTo(grown.AsSpan(offset, count));
                        offset += count;
                    }
                    _items = grown;
                    budget.ReleaseWorking(_reservedBytes);
                    _reservedBytes = bytes;
                    adopted = true;
                    if (old is not null) Utf8ExactStatePool<T>.Return(old, budget.CanRetainWorkspace);
                }
                finally { if (!adopted) Utf8ExactStatePool<T>.Return(grown, budget.CanRetainWorkspace); }
            }
            finally { if (!adopted) budget.ReleaseWorking(bytes); }
        }
        GetItems()[_count++] = value;
        return true;
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
        if (_items is { } items) Utf8ExactStatePool<T>.Return(items, budget.CanRetainWorkspace);
        _items = null;
        _count = 0;
        budget.ReleaseWorking(_reservedBytes);
        _reservedBytes = 0;
    }

    private readonly T[] GetItems() => _items ?? throw new InvalidOperationException("The PCRE2 state stack has no storage.");
}
