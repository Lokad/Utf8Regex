using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal abstract class Utf8StructuralVerifierRuntime
{
    internal static ulong ObjectBytes => 128;

    private readonly Utf8PreparedRegex _regexPlan;

    protected Utf8StructuralVerifierRuntime(Utf8PreparedRegex regexPlan)
    {
        _regexPlan = regexPlan;
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
    internal Utf8NoStructuralVerifierRuntime(Utf8PreparedRegex regexPlan)
        : base(regexPlan)
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
    internal Utf8AsciiStructuralVerifierRuntime(Utf8PreparedRegex regexPlan)
        : base(regexPlan)
    {
    }

    public override bool TryMatch(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        return Plan.AsciiProgram.TryMatch(input, matchIndex, prefixLength, budget, out matchedLength);
    }
}
