using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Owns immutable compiled plans referenced by operation match cursors.</summary>
internal sealed class Utf8OperationMatchCursorPlans
{
    private readonly Utf8SearchPlan _searchPlan;
    private readonly Utf8StructuralLinearProgram _structuralLinearProgram;
    private readonly AsciiSimplePatternPlan _simplePatternPlan;
    private readonly Utf8StructuralSearchPlan _structuralSearchPlan;
    private readonly AsciiStructuralVerifierProgram _structuralVerifier;

    private readonly record struct ConstructionState(
        Utf8SearchPlan SearchPlan,
        Utf8StructuralLinearProgram StructuralLinearProgram,
        AsciiSimplePatternPlan SimplePatternPlan,
        Utf8StructuralSearchPlan StructuralSearchPlan,
        AsciiStructuralVerifierProgram StructuralVerifier);

    private Utf8OperationMatchCursorPlans(ConstructionState state)
    {
        _searchPlan = state.SearchPlan;
        _structuralLinearProgram = state.StructuralLinearProgram;
        _simplePatternPlan = state.SimplePatternPlan;
        _structuralSearchPlan = state.StructuralSearchPlan;
        _structuralVerifier = state.StructuralVerifier;
    }

    internal static Utf8OperationMatchCursorPlans Create(
        Utf8SearchPlan searchPlan,
        Utf8StructuralLinearProgram structuralLinearProgram,
        AsciiSimplePatternPlan simplePatternPlan,
        Utf8StructuralSearchPlan structuralSearchPlan,
        AsciiStructuralVerifierProgram structuralVerifier)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var result = scope.Budget.AllocateObject(new ConstructionState(searchPlan, structuralLinearProgram,
            simplePatternPlan, structuralSearchPlan, structuralVerifier),
            static state => new Utf8OperationMatchCursorPlans(state));
        scope.Complete();
        return result;
    }

    // Shared immutable empty plans are borrowed, not acquired per invocation.
    public static Utf8OperationMatchCursorPlans Empty { get; } = new(new ConstructionState(
        default, default, default, default, default));

    public ref readonly Utf8SearchPlan SearchPlan => ref _searchPlan;

    public ref readonly Utf8StructuralLinearProgram StructuralLinearProgram => ref _structuralLinearProgram;

    public ref readonly AsciiSimplePatternPlan SimplePatternPlan => ref _simplePatternPlan;

    public ref readonly Utf8StructuralSearchPlan StructuralSearchPlan => ref _structuralSearchPlan;

    public ref readonly AsciiStructuralVerifierProgram StructuralVerifier => ref _structuralVerifier;
}
