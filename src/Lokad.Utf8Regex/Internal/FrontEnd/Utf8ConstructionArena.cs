namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Owns arrays acquired during tree conversion. Temporary IR arrays
/// release after lowering; completed program payloads keep their byte loans.
/// Numeric totals do not retain acquired arrays. Explicit retention adds a
/// conservative charge, including shared arrays more than once.</summary>
internal sealed class Utf8ConstructionArena : IDisposable
{
    private readonly Utf8ConstructionBudget budget;
    // Each increment was admitted into the shared non-wrapping ledger first;
    // either subtotal is therefore bounded by that ledger's reserved bytes.
    private ulong _temporaryBytes;
    private ulong _retainedBytes;
    private bool _completed;
    internal Utf8ConstructionBudget Budget => budget;

    internal Utf8ConstructionArena(Utf8ConstructionBudget owner)
    {
        budget = owner;
    }

    internal static Utf8ConstructionArena Create(Utf8ConstructionBudget owner)
    {
        return owner.AllocateObject((owner, 0UL, 0UL, false), static state => new Utf8ConstructionArena(state.owner));
    }

    internal T[] Allocate<T>(int count, bool retained) => Track(budget.AllocateArray<T>(count, false), retained);
    internal T[] Take<T>(Utf8ConstructionBuffer<T> buffer, bool retained) => Track(buffer.Take(), retained);
    internal T[] Adopt<T>(T[] array, bool retained) => Track(array, retained);

    // Explicit publication may borrow/share an array. Charge its extent again
    // instead of keeping identities or proving an earlier allocation transfer.
    internal void Retain<T>(T[] array)
    {
        if (array.Length == 0) return;
        var bytes = Utf8ConstructionBudget.ArrayBytes<T>(array.Length);
        budget.ChargeWork(1);
        budget.ReserveBytes(bytes);
        _retainedBytes += bytes;
    }

    private T[] Track<T>(T[] array, bool retained)
    {
        if (array.Length == 0) return array;
        var bytes = Utf8ConstructionBudget.ArrayBytes<T>(array.Length);
        if (retained) _retainedBytes += bytes;
        else _temporaryBytes += bytes;
        return array;
    }

    internal void Complete() => _completed = true;

    /// <summary>Releases temporary arrays, and all payloads if conversion did not complete.</summary>
    public void Dispose()
    {
        budget.ReleaseBytes(_temporaryBytes);
        if (!_completed) budget.ReleaseBytes(_retainedBytes);
        _temporaryBytes = _retainedBytes = 0;
    }
}
