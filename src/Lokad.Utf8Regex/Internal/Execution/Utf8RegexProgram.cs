using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8RegexProgram
{
    private readonly record struct ConstructionState(
        Utf8PreparedRegex PreparedRegex,
        Utf8CompiledEngine CompiledEngine,
        Utf8VerifierRuntime VerifierRuntime,
        Utf8CompiledEngineRuntime CompiledEngineRuntime,
        Utf8EmittedKernelMatcher? DirectStructuralFamilyKernelMatcher,
        Utf8CaptureCatalog CaptureCatalog);

    private Utf8RegexProgram(ConstructionState state)
    {
        PreparedRegex = state.PreparedRegex;
        CompiledEngine = state.CompiledEngine;
        VerifierRuntime = state.VerifierRuntime;
        CompiledEngineRuntime = state.CompiledEngineRuntime;
        DirectStructuralFamilyKernelMatcher = state.DirectStructuralFamilyKernelMatcher;
        CaptureCatalog = state.CaptureCatalog;
    }

    public Utf8PreparedRegex PreparedRegex { get; }

    public Utf8CompiledEngine CompiledEngine { get; }

    public Utf8VerifierRuntime VerifierRuntime { get; }

    public Utf8CompiledEngineRuntime CompiledEngineRuntime { get; }

    public Utf8EmittedKernelMatcher? DirectStructuralFamilyKernelMatcher { get; }

    public Utf8CaptureCatalog CaptureCatalog { get; }

    public static Utf8RegexProgram Compile(
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var effectiveOptions = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
            var preparedRegex = Utf8FrontEnd.Compile(pattern, effectiveOptions, options);
            var compiledEngine = Utf8CompiledEngineSelector.Select(
                preparedRegex,
                (options & RegexOptions.Compiled) != 0);
            var verifierRuntime = Utf8VerifierRuntime.Create(preparedRegex);
            var compiledEngineRuntime = Utf8CompiledEngineRuntime.Create(
                compiledEngine,
                preparedRegex,
                verifierRuntime,
                options);
            // Successful owned parsing always finishes capture numbering before
            // publishing the tree. Borrow that catalog for every public route;
            // absent metadata would indicate broken internal preparation.
            var captureCatalog = preparedRegex.SemanticRegex.CaptureCatalog;
            if (captureCatalog is null)
                throw new InvalidOperationException("Owned preparation must produce capture metadata.");
            var directStructuralFamilyKernelMatcher =
                preparedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
                Utf8EmittedKernelMatcher.TryCreate(
                    preparedRegex.StructuralIdentifierFamilyPlan,
                    preparedRegex.SearchPlan,
                    out var structuralFamilyKernelMatcher)
                        ? structuralFamilyKernelMatcher
                        : null;

            var result = budget.AllocateObject(new ConstructionState(
                preparedRegex, compiledEngine, verifierRuntime, compiledEngineRuntime,
                directStructuralFamilyKernelMatcher, captureCatalog),
                static state => new Utf8RegexProgram(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

}
