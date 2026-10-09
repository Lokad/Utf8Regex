using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8StructuralVerifierKind : byte
{
    None = 0,
    AsciiStructuralProgram = 1,
    ByteSafeLinearProgram = 2,
    ByteSafeLazyDfaProgram = 3,
}

internal readonly struct Utf8StructuralVerifierPlan
{
    public Utf8StructuralVerifierPlan(
        Utf8StructuralVerifierKind kind,
        AsciiStructuralVerifierProgram asciiProgram,
        Utf8ByteSafeLinearCompileOutcome linearCompileOutcome,
        Utf8ByteSafeLazyDfaCompileOutcome lazyDfaCompileOutcome,
        Utf8ExecutionProgram? byteSafeProgram,
        Utf8DeterministicVerifierGuards byteSafeGuards)
    {
        Kind = kind;
        AsciiProgram = asciiProgram;
        LinearCompileOutcome = linearCompileOutcome;
        LazyDfaCompileOutcome = lazyDfaCompileOutcome;
        ByteSafeProgram = byteSafeProgram;
        ByteSafeGuards = byteSafeGuards;
    }

    public Utf8StructuralVerifierKind Kind { get; }

    public AsciiStructuralVerifierProgram AsciiProgram { get; }

    public Utf8ByteSafeLinearCompileOutcome LinearCompileOutcome { get; }

    public Utf8ByteSafeLazyDfaCompileOutcome LazyDfaCompileOutcome { get; }

    public Utf8ByteSafeLazyDfaVerifierProgram ByteSafeLazyDfaProgram => LazyDfaCompileOutcome.Program;

    public Utf8ByteSafeLinearVerifierProgram ByteSafeLinearProgram => LinearCompileOutcome.Program;

    public Utf8ExecutionProgram? ByteSafeProgram { get; }

    public Utf8DeterministicVerifierGuards ByteSafeGuards { get; }

    public bool HasValue => Kind != Utf8StructuralVerifierKind.None;

}

internal static class Utf8StructuralVerifierRuntimeFactory
{
    public static Utf8StructuralVerifierRuntime Create(Utf8PreparedRegex regexPlan)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var result = budget.AllocateObject<Utf8StructuralVerifierRuntime.ConstructionState, Utf8StructuralVerifierRuntime>(
                new(regexPlan), static state => state.RegexPlan.StructuralVerifier.Kind switch
                {
                    Utf8StructuralVerifierKind.AsciiStructuralProgram => new Utf8AsciiStructuralVerifierRuntime(state),
                    Utf8StructuralVerifierKind.ByteSafeLinearProgram => new Utf8ByteSafeLinearVerifierRuntime(state),
                    Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram => new Utf8ByteSafeLazyDfaVerifierRuntime(state),
                    _ => new Utf8NoStructuralVerifierRuntime(state),
                });
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
