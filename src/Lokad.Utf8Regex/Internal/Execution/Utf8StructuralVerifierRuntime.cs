namespace Lokad.Utf8Regex.Internal.Execution;

internal abstract class Utf8StructuralVerifierRuntime
{
    protected Utf8StructuralVerifierRuntime(Utf8StructuralVerifierPlan plan)
    {
        Plan = plan;
    }

    public Utf8StructuralVerifierPlan Plan { get; }

    public abstract bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionBudget? budget, out int matchedLength);
}

internal sealed class Utf8NoStructuralVerifierRuntime : Utf8StructuralVerifierRuntime
{
    public Utf8NoStructuralVerifierRuntime(Utf8StructuralVerifierPlan plan)
        : base(plan)
    {
    }

    public override bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        matchedLength = 0;
        return false;
    }
}

internal sealed class Utf8AsciiStructuralVerifierRuntime : Utf8StructuralVerifierRuntime
{
    public Utf8AsciiStructuralVerifierRuntime(Utf8StructuralVerifierPlan plan)
        : base(plan)
    {
    }

    public override bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        return Plan.AsciiProgram.TryMatch(input, matchIndex, prefixLength, out matchedLength);
    }
}

internal sealed class Utf8ByteSafeLinearVerifierRuntime : Utf8StructuralVerifierRuntime
{
    public Utf8ByteSafeLinearVerifierRuntime(Utf8StructuralVerifierPlan plan)
        : base(plan)
    {
    }

    public override bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        if (Plan.ByteSafeLinearProgram.HasValue)
        {
            if (Plan.ByteSafeLinearProgram.TryMatch(input, matchIndex, out matchedLength, out var requiresCompatibilityFallback))
            {
                return true;
            }

            if (!requiresCompatibilityFallback)
            {
                return false;
            }
        }

        if (Plan.ByteSafeProgram is null)
        {
            matchedLength = 0;
            return false;
        }

        if (Plan.ByteSafeGuards.HasValue && !Plan.ByteSafeGuards.Matches(input, matchIndex))
        {
            matchedLength = 0;
            return false;
        }

        return Utf8ByteSafeLinearVerifierRunner.TryMatchPrefix(input, Plan.ByteSafeProgram, matchIndex, captures: null, budget, out matchedLength);
    }
}

internal sealed class Utf8ByteSafeLazyDfaVerifierRuntime : Utf8StructuralVerifierRuntime
{
    public Utf8ByteSafeLazyDfaVerifierRuntime(Utf8StructuralVerifierPlan plan)
        : base(plan)
    {
    }

    public override bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        if (Plan.ByteSafeLazyDfaProgram.HasValue)
        {
            return Plan.ByteSafeLazyDfaProgram.TryMatch(input, matchIndex, out matchedLength);
        }

        matchedLength = 0;
        return false;
    }
}
