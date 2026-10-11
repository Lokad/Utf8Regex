namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Owned scalar class data. Each subtraction segment has a negation,
/// range-endpoint count and predicate count followed by half-open scalar ranges
/// and predicates. Counts and endpoints are full integers; U+110000 is the
/// exclusive upper bound. No subject is interpreted as UTF-16 units.
/// </summary>
internal sealed class Utf8ScalarClass
{
    internal const int RangePredicateCode = 101;

    private readonly record struct ConstructionState(int[] Data);
    internal static ulong ObjectBytes => Utf8ConstructionBudget.ObjectBytes<ConstructionState>();
    private Utf8ScalarClass(ConstructionState state)
    {
        Data = state.Data;
    }

    internal int[] Data { get; }

    internal static Utf8ScalarClass Create(Utf8ConstructionBuffer<int> data)
    {
        // Compaction transfers a borrowed buffer loan and may release its old
        // capacity. Finish that transfer before taking the nested checkpoint;
        // object-admission failure must not roll back to a larger released loan.
        var payload = data.Take();
        using var scope = Utf8ConstructionScope.Enter();
        var result = scope.Budget.AllocateObject(new ConstructionState(payload),
            static state => new Utf8ScalarClass(state));
        scope.Complete();
        return result;
    }

}
