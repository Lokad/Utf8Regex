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
        Utf8AsciiCultureInvariantStrategy? asciiCultureInvariantStrategy,
        Utf8EmittedKernelMatcher? directStructuralFamilyKernelMatcher,
        int[] groupNumbers,
        string[] groupNames)
    {
        PreparedRegex = preparedRegex;
        CompiledEngine = compiledEngine;
        VerifierRuntime = verifierRuntime;
        CompiledEngineRuntime = compiledEngineRuntime;
        AsciiCultureInvariantStrategy = asciiCultureInvariantStrategy;
        DirectStructuralFamilyKernelMatcher = directStructuralFamilyKernelMatcher;
        GroupNumbers = groupNumbers;
        GroupNames = groupNames;
    }

    public Utf8PreparedRegex PreparedRegex { get; }

    public Utf8CompiledEngine CompiledEngine { get; }

    public Utf8VerifierRuntime VerifierRuntime { get; }

    public Utf8CompiledEngineRuntime CompiledEngineRuntime { get; }

    public Utf8AsciiCultureInvariantStrategy? AsciiCultureInvariantStrategy { get; }

    public Utf8EmittedKernelMatcher? DirectStructuralFamilyKernelMatcher { get; }

    public int[] GroupNumbers { get; }

    public string[] GroupNames { get; }

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
        var fallbackRegex = verifierRuntime.FallbackCandidateVerifier.FallbackRegex;
        var groupNumbers = fallbackRegex.GetGroupNumbers();
        var groupNames = fallbackRegex.GetGroupNames();
        var asciiCultureInvariantStrategy =
            (effectiveOptions & RegexOptions.IgnoreCase) != 0 &&
            (effectiveOptions & RegexOptions.CultureInvariant) == 0
                ? new Utf8AsciiCultureInvariantStrategy(
                    pattern,
                    options,
                    matchTimeout,
                    groupNames)
                : null;
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
            asciiCultureInvariantStrategy,
            directStructuralFamilyKernelMatcher,
            groupNumbers,
            groupNames);
    }
}
