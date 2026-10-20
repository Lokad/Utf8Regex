using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed partial class Utf8VerifierRuntime
{
    internal static ulong ObjectBytes => 128;

    private Utf8VerifierRuntime(Utf8StructuralVerifierRuntime structural)
    {
        StructuralVerifierRuntime = structural;
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
            budget.CheckBytes(ObjectBytes);
            budget.ChargeWork(2);
            budget.ReserveBytes(ObjectBytes);
            var result = new Utf8VerifierRuntime(structural);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
