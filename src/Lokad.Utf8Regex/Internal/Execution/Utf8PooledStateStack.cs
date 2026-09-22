using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

// PCRE2-INTEGRATION-POINT: flavor-neutral bounded invocation storage used by both managed matchers.
internal ref struct Utf8PooledStateStack<T> where T : struct
{
    private readonly int _maximumCount;
    private readonly bool _trackRents;
    private T[]? _items;
    private int _count;
    private int _rentCount;

    public Utf8PooledStateStack(int maximumCount)
        : this(maximumCount, trackRents: false)
    {
    }

    public Utf8PooledStateStack(int maximumCount, bool trackRents)
    {
        if (maximumCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        _maximumCount = maximumCount;
        _trackRents = trackRents;
        _items = null;
        _count = 0;
        _rentCount = 0;
    }

    public readonly int Count => _count;

    public readonly int MaximumCount => _maximumCount;

    public readonly int RentCount => _rentCount;

    public readonly T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetItems()[index];
        }
    }

    public bool TryPush(T value)
    {
        if (_count >= _maximumCount)
        {
            return false;
        }

        EnsureCapacity(_count + 1);
        GetItems()[_count++] = value;
        return true;
    }

    public bool TryPush(T value, ref Utf8ExecutionBudget budget)
    {
        if (_count >= _maximumCount)
        {
            return false;
        }

        if (!TryEnsureCapacity(_count + 1, ref budget))
        {
            return false;
        }

        GetItems()[_count++] = value;
        return true;
    }

    public T Pop()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("The state stack is empty.");
        }

        var items = GetItems();
        var index = --_count;
        var value = items[index];
        return value;
    }

    public void Truncate(int count)
    {
        if ((uint)count > (uint)_count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        _count = count;
    }

    public void Dispose()
    {
        var items = _items;
        _items = null;
        _count = 0;
        if (items is not null)
        {
            ArrayPool<T>.Shared.Return(items);
        }
    }

    private void EnsureCapacity(int requiredCount)
    {
        var unlimited = new Utf8ExecutionBudget(default, Regex.InfiniteMatchTimeout, CancellationToken.None);
        if (!TryEnsureCapacity(requiredCount, ref unlimited))
        {
            throw new InvalidOperationException("The state stack could not reserve execution storage.");
        }
    }

    private bool TryEnsureCapacity(int requiredCount, ref Utf8ExecutionBudget budget)
    {
        var items = _items;
        if (items is not null && requiredCount <= items.Length)
        {
            return true;
        }

        var requestedCount = items is null
            ? Math.Min(Math.Max(4, requiredCount), _maximumCount)
            : Math.Min(checked(items.Length * 2), _maximumCount);
        var elementSize = Unsafe.SizeOf<T>();
        var liveBytes =
            checked((ulong)(uint)requestedCount * (ulong)(uint)elementSize) +
            (items is null ? 0UL : checked((ulong)(uint)items.Length * (ulong)(uint)elementSize));
        if (budget.TryNoteWorkingBytes(liveBytes) != Utf8ExecutionLimitKind.None)
        {
            return false;
        }

        var grown = ArrayPool<T>.Shared.Rent(requestedCount);
        if (_trackRents)
        {
            _rentCount++;
        }

        var actualBytes =
            checked((ulong)(uint)grown.Length * (ulong)(uint)elementSize) +
            (items is null ? 0UL : checked((ulong)(uint)items.Length * (ulong)(uint)elementSize));
        if (budget.TryNoteWorkingBytes(actualBytes) != Utf8ExecutionLimitKind.None)
        {
            ArrayPool<T>.Shared.Return(grown);
            return false;
        }

        if (items is not null)
        {
            items.AsSpan(0, _count).CopyTo(grown);
            ArrayPool<T>.Shared.Return(items);
        }

        _items = grown;
        return true;
    }

    private readonly T[] GetItems()
    {
        return _items ?? throw new InvalidOperationException("The state stack has no rented storage.");
    }
}
