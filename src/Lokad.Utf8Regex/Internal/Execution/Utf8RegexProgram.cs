using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8RegexProgram
{
    // Conservative helper metadata; major program/search payloads are admitted separately.
    internal static ulong ObjectBytes => 128;

    private Utf8RegexProgram(Utf8PreparedRegex preparedRegex, Utf8CompiledEngineKind compiledEngineKind,
        Utf8LiteralCompiledEngineRuntime? compiledEngineRuntime, Utf8CaptureCatalog captureCatalog)
    {
        PreparedRegex = preparedRegex;
        CompiledEngineKind = compiledEngineKind;
        CompiledEngineRuntime = compiledEngineRuntime;
        CaptureCatalog = captureCatalog;
    }

    public Utf8PreparedRegex PreparedRegex { get; }

    public Utf8CompiledEngineKind CompiledEngineKind { get; }

    public Utf8LiteralCompiledEngineRuntime? CompiledEngineRuntime { get; }

    public Utf8CaptureCatalog CaptureCatalog { get; }

    internal static Utf8RegexProgram Compile(Utf8ConstructionPattern pattern, RegexOptions options,
        bool prepareRuntime, out Utf8TreeNode? root)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var effectiveOptions = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
            var preparedRegex = Utf8FrontEnd.Compile(pattern, effectiveOptions, options, out root);
            var compiledEngineKind = Utf8CompiledEngineSelector.Select(preparedRegex);
            // The scalar program owns any compiled anchored verifier. Fixed
            // numeric policies can use it without a separate literal runtime.
            var compiledEngineRuntime = prepareRuntime && compiledEngineKind != Utf8CompiledEngineKind.FallbackRegex
                ? Utf8LiteralCompiledEngineRuntime.Create(compiledEngineKind, preparedRegex, (options & RegexOptions.RightToLeft) != 0) : null;
            // Successful owned parsing always finishes capture numbering before
            // publishing the tree. Borrow that catalog for every public route;
            // absent metadata would indicate broken internal preparation.
            var captureCatalog = preparedRegex.SemanticRegex.CaptureCatalog;
            if (captureCatalog is null)
                throw new InvalidOperationException("Owned preparation must produce capture metadata.");
            budget.CheckBytes(ObjectBytes);
            budget.ChargeWork(2);
            budget.ReserveBytes(ObjectBytes);
            var result = new Utf8RegexProgram(preparedRegex, compiledEngineKind, compiledEngineRuntime, captureCatalog);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

}
