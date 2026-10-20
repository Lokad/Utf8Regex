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
            budget.CheckBytes(Utf8StructuralVerifierRuntime.ObjectBytes);
            budget.ChargeWork(2);
            budget.ReserveBytes(Utf8StructuralVerifierRuntime.ObjectBytes);
            Utf8StructuralVerifierRuntime result = regexPlan.StructuralVerifier.Kind switch
            {
                Utf8StructuralVerifierKind.AsciiStructuralProgram => new Utf8AsciiStructuralVerifierRuntime(regexPlan),
                _ => new Utf8NoStructuralVerifierRuntime(regexPlan),
            };
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
