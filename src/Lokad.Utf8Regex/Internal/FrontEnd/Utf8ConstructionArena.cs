namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Owns arrays acquired during tree conversion. Temporary IR arrays
/// release after lowering; completed program payloads keep their byte loans.
/// The tracking vector itself uses admitted capacity. Node/string/iterator
/// objects remain separate construction admission work.</summary>
internal sealed class Utf8ConstructionArena : IDisposable
{
    private readonly record struct Allocation(Array Array, ulong Bytes, bool Retained);
    private readonly Utf8ConstructionBudget budget;
    private readonly Utf8ConstructionBuffer<Allocation> _allocations;
    private bool _completed;
    internal Utf8ConstructionBudget Budget => budget;

    internal Utf8ConstructionArena(Utf8ConstructionBudget owner)
    {
        budget = owner;
        _allocations = new(owner);
    }

    private Utf8ConstructionArena(Utf8ConstructionBudget owner, Utf8ConstructionBuffer<Allocation> allocations)
    {
        budget = owner;
        _allocations = allocations;
    }

    internal static Utf8ConstructionArena Create(Utf8ConstructionBudget owner)
    {
        var allocations = Utf8ConstructionBuffer<Allocation>.Create(owner);
        return owner.AllocateObject((owner, allocations, false), static state => new Utf8ConstructionArena(state.owner, state.allocations));
    }

    internal T[] Allocate<T>(int count, bool retained) => Track(budget.AllocateArray<T>(count, false), retained);
    internal T[] Take<T>(Utf8ConstructionBuffer<T> buffer, bool retained) => Track(buffer.Take(), retained);
    internal T[] Adopt<T>(T[] array, bool retained) => Track(array, retained);

    private T[] Track<T>(T[] array, bool retained)
    {
        if (array.Length == 0) return array;
        try { _allocations.Add(new(array, Utf8ConstructionBudget.ArrayBytes<T>(array.Length), retained)); }
        catch { budget.ReleaseArray(array, false); throw; }
        return array;
    }

    internal void Complete() => _completed = true;

    /// <summary>Releases temporary arrays, and all payloads if conversion did not complete.</summary>
    public void Dispose()
    {
        for (var index = 0; index < _allocations.Count; index++)
        {
            var allocation = _allocations[index];
            if (!_completed || !allocation.Retained) budget.ReleaseBytes(allocation.Bytes);
        }
        _allocations.Dispose();
    }
}
