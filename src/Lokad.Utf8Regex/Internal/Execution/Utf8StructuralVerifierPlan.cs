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
        AsciiStructuralVerifierProgram asciiProgram = default,
        Utf8ByteSafeLazyDfaVerifierProgram byteSafeLazyDfaProgram = default,
        Utf8ByteSafeLinearVerifierProgram byteSafeLinearProgram = default,
        Utf8ExecutionProgram? byteSafeProgram = null,
        Utf8DeterministicVerifierGuards byteSafeGuards = default)
    {
        Kind = kind;
        AsciiProgram = asciiProgram;
        ByteSafeLazyDfaProgram = byteSafeLazyDfaProgram;
        ByteSafeLinearProgram = byteSafeLinearProgram;
        ByteSafeProgram = byteSafeProgram;
        ByteSafeGuards = byteSafeGuards;
    }

    public Utf8StructuralVerifierKind Kind { get; }

    public AsciiStructuralVerifierProgram AsciiProgram { get; }

    public Utf8ByteSafeLazyDfaVerifierProgram ByteSafeLazyDfaProgram { get; }

    public Utf8ByteSafeLinearVerifierProgram ByteSafeLinearProgram { get; }

    public Utf8ExecutionProgram? ByteSafeProgram { get; }

    public Utf8DeterministicVerifierGuards ByteSafeGuards { get; }

    public bool HasValue => Kind != Utf8StructuralVerifierKind.None;

    public static Utf8StructuralVerifierPlan Create(AsciiStructuralIdentifierFamilyPlan structuralFamilyPlan)
    {
        return structuralFamilyPlan.VerifierProgram.HasValue
            ? new Utf8StructuralVerifierPlan(Utf8StructuralVerifierKind.AsciiStructuralProgram, structuralFamilyPlan.VerifierProgram)
            : default;
    }

    public static Utf8StructuralVerifierPlan CreateByteSafe(Utf8ExecutionTree? tree, Utf8ExecutionProgram? program, Utf8DeterministicVerifierGuards guards)
    {
        var linearProgram = Utf8ByteSafeLinearVerifierProgram.Create(tree);
        var lazyDfaProgram = Utf8ByteSafeLazyDfaVerifierProgram.Create(linearProgram);
        return program is null
            ? default
            : new Utf8StructuralVerifierPlan(
                lazyDfaProgram.HasValue ? Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram : Utf8StructuralVerifierKind.ByteSafeLinearProgram,
                byteSafeLazyDfaProgram: lazyDfaProgram,
                byteSafeLinearProgram: linearProgram,
                byteSafeProgram: program,
                byteSafeGuards: guards);
    }

    public Utf8StructuralVerifierRuntime CreateRuntime()
    {
        switch (Kind)
        {
            case Utf8StructuralVerifierKind.AsciiStructuralProgram:
                return new Utf8AsciiStructuralVerifierRuntime(this);

            case Utf8StructuralVerifierKind.ByteSafeLinearProgram:
                return new Utf8ByteSafeLinearVerifierRuntime(this);

            case Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram:
                return new Utf8ByteSafeLazyDfaVerifierRuntime(this);

            default:
                return new Utf8NoStructuralVerifierRuntime(this);
        }
    }
}
