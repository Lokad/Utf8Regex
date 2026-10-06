using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8RegexProgram
{
    private Utf8RegexProgram(
        Utf8PreparedRegex preparedRegex,
        Utf8CompiledEngine compiledEngine,
        Utf8VerifierRuntime verifierRuntime,
        Utf8CompiledEngineRuntime compiledEngineRuntime,
        Utf8EmittedKernelMatcher? directStructuralFamilyKernelMatcher,
        Lazy<int[]> groupNumbers,
        Lazy<string[]> groupNames,
        Utf8GroupMetadata? groupMetadata)
    {
        PreparedRegex = preparedRegex;
        CompiledEngine = compiledEngine;
        VerifierRuntime = verifierRuntime;
        CompiledEngineRuntime = compiledEngineRuntime;
        DirectStructuralFamilyKernelMatcher = directStructuralFamilyKernelMatcher;
        GroupNumbers = groupNumbers;
        GroupNames = groupNames;
        GroupMetadata = groupMetadata;
    }

    public Utf8PreparedRegex PreparedRegex { get; }

    public Utf8CompiledEngine CompiledEngine { get; }

    public Utf8VerifierRuntime VerifierRuntime { get; }

    public Utf8CompiledEngineRuntime CompiledEngineRuntime { get; }

    public Utf8EmittedKernelMatcher? DirectStructuralFamilyKernelMatcher { get; }

    public Lazy<int[]> GroupNumbers { get; }

    public Lazy<string[]> GroupNames { get; }

    public Utf8GroupMetadata? GroupMetadata { get; }

    public static Utf8RegexProgram Compile(
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        var effectiveOptions = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        var preparedRegex = Utf8FrontEnd.Compile(pattern, effectiveOptions);
        var compiledEngine = Utf8CompiledEngineSelector.Select(
            preparedRegex,
            (options & RegexOptions.Compiled) != 0);
        var verifierRuntime = Utf8VerifierRuntime.Create(
            preparedRegex,
            pattern,
            options,
            matchTimeout);
        var compiledEngineRuntime = Utf8CompiledEngineRuntime.Create(
            compiledEngine,
            preparedRegex,
            verifierRuntime,
            options);
        // R02 extraction 4: group metadata comes from the owned parse tree whenever it exists;
        // the BCL query remains only for patterns the owned parser declines, resolved lazily
        // so it never constructs the fallback engine by itself.
        Utf8GroupMetadata.TryCreate(preparedRegex.SemanticRegex.RuntimeTree, out var groupMetadata);
        var fallbackVerifier = verifierRuntime.FallbackCandidateVerifier;
        var groupNumbers = new Lazy<int[]>(() => groupMetadata?.GroupNumbers ?? fallbackVerifier.FallbackRegex.GetGroupNumbers());
        var groupNames = new Lazy<string[]>(() => groupMetadata?.GroupNames ?? fallbackVerifier.FallbackRegex.GetGroupNames());
        var directStructuralFamilyKernelMatcher =
            preparedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
            Utf8EmittedKernelMatcher.TryCreate(
                preparedRegex.StructuralIdentifierFamilyPlan,
                preparedRegex.SearchPlan,
                out var structuralFamilyKernelMatcher)
                    ? structuralFamilyKernelMatcher
                    : null;

        return new Utf8RegexProgram(
            preparedRegex,
            compiledEngine,
            verifierRuntime,
            compiledEngineRuntime,
            directStructuralFamilyKernelMatcher,
            groupNumbers,
            groupNames,
            groupMetadata);
    }
}
