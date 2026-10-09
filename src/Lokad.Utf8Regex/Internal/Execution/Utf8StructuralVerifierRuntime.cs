using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal abstract class Utf8StructuralVerifierRuntime
{
    internal readonly record struct ConstructionState(Utf8PreparedRegex RegexPlan);

    private readonly Utf8PreparedRegex _regexPlan;

    protected Utf8StructuralVerifierRuntime(ConstructionState state)
    {
        _regexPlan = state.RegexPlan;
    }

    public Utf8StructuralVerifierPlan Plan => _regexPlan.StructuralVerifier;

    /// <summary>
    /// Verifies a structural candidate beginning at <paramref name="matchIndex"/>
    /// after <paramref name="prefixLength"/> bytes have already been admitted.
    /// Both offsets must be within the supplied well-formed subject. All work
    /// charges <paramref name="budget"/>; false definitively rejects this start.
    /// </summary>
    public abstract bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionDeadline budget, out int matchedLength);
}

internal sealed class Utf8NoStructuralVerifierRuntime : Utf8StructuralVerifierRuntime
{
    internal Utf8NoStructuralVerifierRuntime(ConstructionState state)
        : base(state)
    {
    }

    public override bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        matchedLength = 0;
        return false;
    }
}

internal sealed class Utf8AsciiStructuralVerifierRuntime : Utf8StructuralVerifierRuntime
{
    internal Utf8AsciiStructuralVerifierRuntime(ConstructionState state)
        : base(state)
    {
    }

    public override bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        return Plan.AsciiProgram.TryMatch(input, matchIndex, prefixLength, budget, out matchedLength);
    }
}

internal sealed class Utf8ByteSafeLinearVerifierRuntime : Utf8StructuralVerifierRuntime
{
    internal Utf8ByteSafeLinearVerifierRuntime(ConstructionState state)
        : base(state)
    {
    }

    public override bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        if (Plan.ByteSafeLinearProgram.HasValue)
        {
            if (Plan.ByteSafeLinearProgram.TryMatch(input, matchIndex, budget, out matchedLength, out var requiresCompatibilityFallback))
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
    internal Utf8ByteSafeLazyDfaVerifierRuntime(ConstructionState state)
        : base(state)
    {
    }

    public override bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        if (Plan.ByteSafeLazyDfaProgram.HasValue)
        {
            return Plan.ByteSafeLazyDfaProgram.TryMatch(input, matchIndex, budget, out matchedLength);
        }

        matchedLength = 0;
        return false;
    }
}
