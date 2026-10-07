using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Exact managed instruction storage admitted before acquisition.
/// Growing retains both extents until copying completes. Taking the final
/// array transfers its admission to the constructed graph; no pool can return
/// an unexpectedly larger buffer.</summary>
internal sealed class Utf8SliceInstructionBuilder(Utf8ConstructionInstructionBudget budget) : IDisposable
{
    private Utf8SliceInstruction[] _items = [];
    internal int Count { get; private set; }
    internal int Capacity => _items.Length;

    internal Utf8SliceInstruction this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    internal void Add(Utf8SliceInstruction instruction)
    {
        if (Count == _items.Length)
        {
            var desired = _items.Length == 0 ? 1L : (long)_items.Length * 2;
            var available = budget.Maximum - budget.Reserved;
            var capacity = (int)Math.Min((ulong)Math.Min(desired, int.MaxValue), available);
            // If geometric growth cannot fit, try the smallest useful extent.
            // Failure reports required capacity rather than an arbitrary growth factor.
            if (capacity <= _items.Length)
                budget.Reserve((ulong)_items.Length + 1);
            budget.Reserve((ulong)capacity);
            Utf8SliceInstruction[] grown;
            try
            {
                grown = new Utf8SliceInstruction[capacity];
                _items.AsSpan(0, Count).CopyTo(grown);
            }
            catch
            {
                budget.Release((ulong)capacity);
                throw;
            }
            budget.Release((ulong)_items.Length);
            _items = grown;
        }
        _items[Count++] = instruction;
    }

    internal Utf8SliceInstruction[] Take()
    {
        var result = _items;
        if (Count != result.Length)
        {
            budget.Reserve((ulong)Count);
            try
            {
                result = new Utf8SliceInstruction[Count];
                _items.AsSpan(0, Count).CopyTo(result);
            }
            catch
            {
                budget.Release((ulong)Count);
                throw;
            }
            budget.Release((ulong)_items.Length);
        }
        _items = [];
        Count = 0;
        return result;
    }

    /// <summary>Enumerates initialized instructions while metadata is prepared.</summary>
    public IEnumerator<Utf8SliceInstruction> GetEnumerator()
    {
        for (var index = 0; index < Count; index++) yield return _items[index];
    }

    /// <summary>Releases only the temporary extent still owned by this builder.</summary>
    public void Dispose()
    {
        budget.Release((ulong)_items.Length);
        _items = [];
        Count = 0;
    }
}
