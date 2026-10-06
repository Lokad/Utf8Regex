using Lokad.Utf8Regex.Internal.FrontEnd;
using System.Globalization;
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
        Lazy<int[]> groupNumbers,
        Lazy<string[]> groupNames,
        Utf8GroupMetadata? groupMetadata)
    {
        PreparedRegex = preparedRegex;
        CompiledEngine = compiledEngine;
        VerifierRuntime = verifierRuntime;
        CompiledEngineRuntime = compiledEngineRuntime;
        AsciiCultureInvariantStrategy = asciiCultureInvariantStrategy;
        DirectStructuralFamilyKernelMatcher = directStructuralFamilyKernelMatcher;
        GroupNumbers = groupNumbers;
        GroupNames = groupNames;
        GroupMetadata = groupMetadata;
    }

    public Utf8PreparedRegex PreparedRegex { get; }

    public Utf8CompiledEngine CompiledEngine { get; }

    public Utf8VerifierRuntime VerifierRuntime { get; }

    public Utf8CompiledEngineRuntime CompiledEngineRuntime { get; }

    public Utf8AsciiCultureInvariantStrategy? AsciiCultureInvariantStrategy { get; }

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
        // The ASCII twin replays matching with invariant case folding, which is
        // only valid when the construction culture folds ASCII the invariant way
        // (PLAN.md C02). Turkic dotted/dotless-I cultures do not: "I" and "i"
        // differ there even though both are ASCII. Probe the 52 ASCII letters
        // instead of naming cultures, so custom cultures stay correct. The probe
        // stays a local function: single-caller private helpers count as source debt.
        static bool FoldsAsciiCaseInvariantly(CultureInfo constructionCulture)
        {
            for (var c = 'A'; c <= 'Z'; c++)
            {
                if (char.ToLower(c, constructionCulture) != char.ToLowerInvariant(c) ||
                    char.ToUpper(c, constructionCulture) != char.ToUpperInvariant(c))
                {
                    return false;
                }
            }

            for (var c = 'a'; c <= 'z'; c++)
            {
                if (char.ToLower(c, constructionCulture) != char.ToLowerInvariant(c) ||
                    char.ToUpper(c, constructionCulture) != char.ToUpperInvariant(c))
                {
                    return false;
                }
            }

            return true;
        }

        var isCultureSensitiveIgnoreCase =
            (effectiveOptions & RegexOptions.IgnoreCase) != 0 &&
            (effectiveOptions & RegexOptions.CultureInvariant) == 0;
        var asciiCultureInvariantStrategy = isCultureSensitiveIgnoreCase &&
            FoldsAsciiCaseInvariantly(Utf8OwnedOptionPrelude.GetTargetCulture(effectiveOptions))
                ? new Utf8AsciiCultureInvariantStrategy(
                    pattern,
                    options,
                    matchTimeout,
                    groupNames.Value)
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
            groupNames,
            groupMetadata);
    }
}
