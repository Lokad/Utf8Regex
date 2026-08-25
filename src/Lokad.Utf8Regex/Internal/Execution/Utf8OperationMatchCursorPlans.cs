using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Owns immutable compiled plans referenced by operation match cursors.</summary>
internal sealed class Utf8OperationMatchCursorPlans
{
    private readonly Utf8SearchPlan _searchPlan;
    private readonly Utf8StructuralLinearProgram _structuralLinearProgram;
    private readonly AsciiSimplePatternPlan _simplePatternPlan;

    public Utf8OperationMatchCursorPlans(
        Utf8ExecutionProgram? executionProgram,
        Utf8SearchPlan searchPlan,
        Utf8StructuralLinearProgram structuralLinearProgram,
        AsciiSimplePatternPlan simplePatternPlan)
    {
        ExecutionProgram = executionProgram;
        _searchPlan = searchPlan;
        _structuralLinearProgram = structuralLinearProgram;
        _simplePatternPlan = simplePatternPlan;
    }

    public static Utf8OperationMatchCursorPlans Empty { get; } = new(
        executionProgram: null,
        searchPlan: default,
        structuralLinearProgram: default,
        simplePatternPlan: default);

    public Utf8ExecutionProgram? ExecutionProgram { get; }

    public ref readonly Utf8SearchPlan SearchPlan => ref _searchPlan;

    public ref readonly Utf8StructuralLinearProgram StructuralLinearProgram => ref _structuralLinearProgram;

    public ref readonly AsciiSimplePatternPlan SimplePatternPlan => ref _simplePatternPlan;
}
