namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8BackendInstructionKind : byte
{
    None = 0,
    Search = 1,
    Confirm = 2,
    Project = 3,
}

internal readonly struct Utf8BackendInstruction
{
    public Utf8BackendInstruction(
        Utf8BackendInstructionKind kind,
        Utf8SearchMetaStrategyPlan strategy = default,
        Utf8ConfirmationPlan confirmation = default,
        Utf8ProjectionPlan projection = default)
    {
        Kind = kind;
        Strategy = strategy;
        Confirmation = confirmation;
        Projection = projection;
    }

    public Utf8BackendInstructionKind Kind { get; }

    public Utf8SearchMetaStrategyPlan Strategy { get; }

    public Utf8ConfirmationPlan Confirmation { get; }

    public Utf8ProjectionPlan Projection { get; }
}

internal readonly struct Utf8BackendInstructionProgram
{
    public Utf8BackendInstructionProgram(
        Utf8ExecutablePipelinePlan pipeline,
        Utf8BackendInstruction first = default,
        Utf8BackendInstruction second = default,
        Utf8BackendInstruction third = default,
        int instructionCount = 0)
    {
        Pipeline = pipeline;
        First = first;
        Second = second;
        Third = third;
        InstructionCount = instructionCount;
    }

    public Utf8ExecutablePipelinePlan Pipeline { get; }

    public Utf8BackendInstruction First { get; }

    public Utf8BackendInstruction Second { get; }

    public Utf8BackendInstruction Third { get; }

    public int InstructionCount { get; }

    public bool HasValue => Pipeline.HasValue;

    public Utf8SearchMetaStrategyPlan Strategy => Pipeline.Strategy;

    public Utf8ConfirmationPlan Confirmation => Pipeline.Confirmation;

    public Utf8ProjectionPlan Projection => Pipeline.Projection;

    public Utf8BackendInstruction GetInstruction(int index)
    {
        return index switch
        {
            0 when index < InstructionCount => First,
            1 when index < InstructionCount => Second,
            2 when index < InstructionCount => Third,
            _ => default,
        };
    }
}

internal static class Utf8BackendInstructionProgramBuilder
{
    public static Utf8BackendInstructionProgram Create(Utf8ExecutablePipelinePlan pipeline)
    {
        if (!pipeline.HasValue)
        {
            return default;
        }

        var first = new Utf8BackendInstruction(Utf8BackendInstructionKind.Search, strategy: pipeline.Strategy);
        var instructionCount = 1;
        Utf8BackendInstruction second = default;
        Utf8BackendInstruction third = default;

        if (pipeline.Confirmation.HasValue)
        {
            second = new Utf8BackendInstruction(Utf8BackendInstructionKind.Confirm, confirmation: pipeline.Confirmation);
            instructionCount = 2;
        }

        if (pipeline.Projection.HasValue)
        {
            third = new Utf8BackendInstruction(Utf8BackendInstructionKind.Project, projection: pipeline.Projection);
            instructionCount = pipeline.Confirmation.HasValue ? 3 : 2;
            if (!pipeline.Confirmation.HasValue)
            {
                second = third;
                third = default;
            }
        }

        return new Utf8BackendInstructionProgram(pipeline, first, second, third, instructionCount);
    }
}
