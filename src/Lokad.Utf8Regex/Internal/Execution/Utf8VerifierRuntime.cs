using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8VerifierRuntime
{
    private readonly record struct ConstructionState(
        Utf8StructuralVerifierRuntime StructuralVerifierRuntime,
        Utf8FallbackCandidateVerifier FallbackCandidateVerifier);

    private Utf8VerifierRuntime(ConstructionState state)
    {
        StructuralVerifierRuntime = state.StructuralVerifierRuntime;
        FallbackCandidateVerifier = state.FallbackCandidateVerifier;
    }

    public Utf8StructuralVerifierPlan StructuralVerifierPlan => StructuralVerifierRuntime.Plan;

    public Utf8StructuralVerifierRuntime StructuralVerifierRuntime { get; }

    public Utf8FallbackCandidateVerifier FallbackCandidateVerifier { get; }

    public static Utf8VerifierRuntime Create(Utf8PreparedRegex regexPlan, string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var structural = Utf8StructuralVerifierRuntimeFactory.Create(regexPlan);
            var fallback = Utf8FallbackVerifierRuntimeFactory.Create(regexPlan.FallbackVerifier, pattern, options, matchTimeout);
            var result = budget.AllocateObject(new ConstructionState(structural, fallback),
                static state => new Utf8VerifierRuntime(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
