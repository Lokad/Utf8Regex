using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Owns immutable literal search plans referenced by operation cursors.</summary>
internal sealed class Utf8OperationMatchCursorPlans
{
    private readonly Utf8SearchPlan _searchPlan;

    private Utf8OperationMatchCursorPlans(Utf8SearchPlan searchPlan) => _searchPlan = searchPlan;

    internal static Utf8OperationMatchCursorPlans Create(Utf8SearchPlan searchPlan)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var result = scope.Budget.AllocateObject(searchPlan,
            static plan => new Utf8OperationMatchCursorPlans(plan));
        scope.Complete();
        return result;
    }

    // Shared empty plans are borrowed, not acquired per invocation.
    public static Utf8OperationMatchCursorPlans Empty { get; } = new(default);

    public ref readonly Utf8SearchPlan SearchPlan => ref _searchPlan;
}
