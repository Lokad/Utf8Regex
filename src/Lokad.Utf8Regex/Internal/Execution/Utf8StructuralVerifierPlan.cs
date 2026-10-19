using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8StructuralVerifierKind : byte
{
    None = 0,
    AsciiStructuralProgram = 1,
}

internal readonly struct Utf8StructuralVerifierPlan
{
    public Utf8StructuralVerifierPlan(
        Utf8StructuralVerifierKind kind,
        AsciiStructuralVerifierProgram asciiProgram)
    {
        Kind = kind;
        AsciiProgram = asciiProgram;
    }

    public Utf8StructuralVerifierKind Kind { get; }

    public AsciiStructuralVerifierProgram AsciiProgram { get; }

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
                    _ => new Utf8NoStructuralVerifierRuntime(state),
                });
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
