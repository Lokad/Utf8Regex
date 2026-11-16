using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8RegexProgram
{
    // Conservative helper metadata; major program/search payloads are admitted separately.
    internal static ulong ObjectBytes => 128;

    private Utf8RegexProgram(Utf8PreparedRegex preparedRegex, Utf8CompiledEngine compiledEngine,
        Utf8VerifierRuntime? verifierRuntime, Utf8CompiledEngineRuntime? compiledEngineRuntime,
        Utf8CaptureCatalog captureCatalog)
    {
        PreparedRegex = preparedRegex;
        CompiledEngine = compiledEngine;
        VerifierRuntime = verifierRuntime;
        CompiledEngineRuntime = compiledEngineRuntime;
        CaptureCatalog = captureCatalog;
    }

    public Utf8PreparedRegex PreparedRegex { get; }

    public Utf8CompiledEngine CompiledEngine { get; }

    public Utf8VerifierRuntime? VerifierRuntime { get; }

    public Utf8CompiledEngineRuntime? CompiledEngineRuntime { get; }

    public Utf8CaptureCatalog CaptureCatalog { get; }

    public static Utf8RegexProgram Compile(
        string pattern,
        RegexOptions options,
        bool prepareRuntime)
        => Compile(new Utf8ConstructionPattern(pattern), options, prepareRuntime);

    internal static Utf8RegexProgram Compile(Utf8ConstructionPattern pattern, RegexOptions options, bool prepareRuntime)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var effectiveOptions = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
            var preparedRegex = Utf8FrontEnd.Compile(pattern, effectiveOptions, options);
            var compiledEngine = Utf8CompiledEngineSelector.Select(preparedRegex);
            // Fixed numeric policies select metered execution for the entire
            // instance lifetime. Keep its scalar/search payloads, but acquire no
            // unreachable verifier, unmetered runtime or emitted delegates.
            var verifierRuntime = prepareRuntime ? Utf8VerifierRuntime.Create(preparedRegex) : null;
            var compiledEngineRuntime = verifierRuntime is null ? null :
                Utf8CompiledEngineRuntime.Create(compiledEngine, preparedRegex, verifierRuntime, options);
            // Successful owned parsing always finishes capture numbering before
            // publishing the tree. Borrow that catalog for every public route;
            // absent metadata would indicate broken internal preparation.
            var captureCatalog = preparedRegex.SemanticRegex.CaptureCatalog;
            if (captureCatalog is null)
                throw new InvalidOperationException("Owned preparation must produce capture metadata.");
            budget.CheckBytes(ObjectBytes);
            budget.ChargeWork(2);
            budget.ReserveBytes(ObjectBytes);
            var result = new Utf8RegexProgram(preparedRegex, compiledEngine, verifierRuntime,
                compiledEngineRuntime, captureCatalog);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

}
