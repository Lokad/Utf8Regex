using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed partial class Utf8VerifierRuntime
{
    private readonly record struct ConstructionState(
        Utf8StructuralVerifierRuntime StructuralVerifierRuntime);

    private Utf8VerifierRuntime(ConstructionState state)
    {
        StructuralVerifierRuntime = state.StructuralVerifierRuntime;
    }

    public Utf8StructuralVerifierPlan StructuralVerifierPlan => StructuralVerifierRuntime.Plan;

    public Utf8StructuralVerifierRuntime StructuralVerifierRuntime { get; }

    public static Utf8VerifierRuntime Create(Utf8PreparedRegex regexPlan)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var structural = Utf8StructuralVerifierRuntimeFactory.Create(regexPlan);
            var result = budget.AllocateObject(new ConstructionState(structural),
                static state => new Utf8VerifierRuntime(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
