using System.Diagnostics.CodeAnalysis;

namespace Lokad.Utf8Regex.Internal;

/// <summary>Exact array-backed construction vector/stack. Admission covers both
/// extents during growth and final copying; Take transfers the final array loan.
/// Elements borrowed from callers retain their independent ownership.</summary>
internal sealed class Utf8ConstructionBuffer<T>(Utf8ConstructionBudget budget, bool instructions) : IDisposable
{
    private T[] _items = [];
    // Opt-in descriptor acquisition for construction-owned parser collections.
    // Existing callers may still borrow a separately owned descriptor.
    internal static Utf8ConstructionBuffer<T> Create(Utf8ConstructionBudget owner) => Create(owner, false);
    internal static Utf8ConstructionBuffer<T> Create(Utf8ConstructionBudget owner, bool instructions) =>
        owner.AllocateObject((owner, instructions, Array.Empty<T>(), 0), static state => new Utf8ConstructionBuffer<T>(state.owner, state.instructions));
    internal Utf8ConstructionBuffer(Utf8ConstructionBudget budget) : this(budget, false) { }
    internal int Count { get; private set; }
    internal int Capacity => _items.Length;
    internal ReadOnlySpan<T> Span => _items.AsSpan(0, Count);
    internal T this[int index] { get => _items[index]; set => _items[index] = value; }

    internal void Add(T value)
    {
        EnsureCapacity(checked(Count + 1));
        _items[Count++] = value;
    }

    internal void AddRange(ReadOnlySpan<T> values)
    {
        var count = checked(Count + values.Length);
        EnsureCapacity(count);
        budget.ChargeWork((ulong)values.Length);
        values.CopyTo(_items.AsSpan(Count));
        Count = count;
    }

    internal void Clear()
    {
        budget.ChargeWork((ulong)Count);
        _items.AsSpan(0, Count).Clear();
        Count = 0;
    }

    internal void Truncate(int count)
    {
        if ((uint)count > (uint)Count) throw new ArgumentOutOfRangeException(nameof(count));
        budget.ChargeWork((ulong)(Count - count));
        _items.AsSpan(count, Count - count).Clear();
        Count = count;
    }

    internal void RemoveAt(int index)
    {
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        budget.ChargeWork((ulong)(Count - index));
        _items.AsSpan(index + 1, Count - index - 1).CopyTo(_items.AsSpan(index));
        _items.AsSpan(--Count, 1).Clear();
    }

    internal void Insert(int index, T value)
    {
        if ((uint)index > (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        EnsureCapacity(checked(Count + 1));
        budget.ChargeWork((ulong)(Count - index) + 1);
        _items.AsSpan(index, Count - index).CopyTo(_items.AsSpan(index + 1));
        _items[index] = value;
        Count++;
    }

    // Parser fixup sequences borrow a distinct scratch vector. Admit the growth
    // overlap and both shifted/source ranges before rewriting the destination.
    internal void InsertRange(int index, ReadOnlySpan<T> values)
    {
        if ((uint)index > (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (values.IsEmpty) return;
        if (values.Overlaps(_items)) throw new ArgumentException("Insertion requires a distinct source vector.", nameof(values));
        var count = checked(Count + values.Length);
        EnsureCapacity(count);
        budget.ChargeWork((ulong)(Count - index) + (ulong)values.Length);
        _items.AsSpan(index, Count - index).CopyTo(_items.AsSpan(index + values.Length));
        values.CopyTo(_items.AsSpan(index));
        Count = count;
    }

    private void EnsureCapacity(int required)
    {
        if (required > _items.Length)
        {
            var desired = Math.Max(required, _items.Length == 0 ? 1L : (long)_items.Length * 2);
            var capacity = (int)Math.Min(desired, budget.AvailableArrayCapacity<T>());
            if (instructions) capacity = (int)Math.Min((ulong)capacity, budget.Maximum - budget.Reserved);
            if (capacity < required)
            {
                if (instructions) budget.CheckSlots((ulong)required);
                budget.CheckBytes(Utf8ConstructionBudget.ArrayBytes<T>(required));
                throw new InvalidOperationException("The construction buffer cannot represent its next extent.");
            }
            var grown = budget.AllocateArray<T>(capacity, instructions);
            try
            {
                budget.ChargeWork((ulong)Count);
                _items.AsSpan(0, Count).CopyTo(grown);
            }
            catch { budget.ReleaseArray(grown, instructions); throw; }
            budget.ReleaseArray(_items, instructions);
            _items = grown;
        }
    }

    internal void Push(T value) => Add(value);
    internal T Peek() => _items[Count - 1];
    internal T Pop()
    {
        var value = _items[--Count];
        _items.AsSpan(Count, 1).Clear();
        return value;
    }
    internal bool TryPop([MaybeNullWhen(false)] out T value)
    {
        if (Count > 0) { value = Pop(); return true; }
        value = default;
        return false;
    }

    internal int IndexOf(T value) => Array.IndexOf(_items, value, 0, Count);
    internal void Sort(Comparison<T> comparison) => Utf8ConstructionSort.Sort(_items.AsSpan(0, Count), comparison, budget);

    internal T[] Take()
    {
        var result = _items;
        if (Count != result.Length)
        {
            result = budget.AllocateArray<T>(Count, instructions);
            try
            {
                budget.ChargeWork((ulong)Count);
                _items.AsSpan(0, Count).CopyTo(result);
            }
            catch { budget.ReleaseArray(result, instructions); throw; }
            budget.ReleaseArray(_items, instructions);
        }
        _items = [];
        Count = 0;
        return result;
    }

    /// <summary>Enumerates the initialized extent during construction.</summary>
    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator(Utf8ConstructionBuffer<T> buffer) : IDisposable
    {
        private int _index = -1;
        public readonly T Current => buffer[_index];
        public bool MoveNext() => ++_index < buffer.Count;
        public readonly void Dispose() { }
    }

    /// <summary>Releases only the temporary array still owned by this buffer.</summary>
    public void Dispose()
    {
        budget.ReleaseArray(_items, instructions);
        _items = [];
        Count = 0;
    }
}
