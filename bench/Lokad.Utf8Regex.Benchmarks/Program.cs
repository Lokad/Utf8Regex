using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Lokad.Utf8Regex.Benchmarks;

if (args.Length >= 2 && args[0].Equals("--diagnose-ismatch", StringComparison.Ordinal))
{
    return Utf8RegexDiagnosticsReporter.RunIsMatch(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--inspect-pattern", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunInspectPattern(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--inspect-utf8-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunInspectUtf8Case(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--inspect-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunInspectReplicaCase(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--measure-utf8-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureUtf8Case(args[1], args.Length >= 3 ? args[2] : null);
}

if (BenchmarkProgramRouter.TryHandlePcre2Command(args, out var pcre2ExitCode))
{
    return pcre2ExitCode;
}

if (args.Length >= 2 && args[0].Equals("--measure-utf8-enumerator-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureUtf8EnumeratorCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-compiled-fallback-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureCompiledFallbackReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-script-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadScriptCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-script-prefix-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadScriptPrefixCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-script-lexer-primitive-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadScriptLexerPrimitiveCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-script-url-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadScriptUrlCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-token-finder-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureTokenFinderCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-line-family-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLineFamilyCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-literal-finder-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLineFamilyCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-script-byte-safe-prefix-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadScriptByteSafePrefixCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-script-count-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadScriptCountCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-public-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadPublicCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-public-case-deep", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadPublicCaseDeep(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-public-validator-deep", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadPublicValidatorDeep(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-public-ordered-window-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadPublicOrderedWindowCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-public-compiled-fallback-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadPublicCompiledFallbackCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-small-ascii-literal-family-primitive-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureSmallAsciiLiteralFamilyPrimitiveCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-small-ascii-literal-family-first-match-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureSmallAsciiLiteralFamilyFirstMatchCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-symmetric-window-lokad-public-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureSymmetricWindowLokadPublicCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 1 && args[0].Equals("--emit-readme-benchmark-markdown", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunEmitReadmeBenchmarkMarkdown(
        args.Length >= 2 ? args[1] : null,
        args.Length >= 3 ? args[2] : null,
        args.Length >= 4 ? args[3] : null);
}

if (args.Length >= 1 && args[0].Equals("--refresh-readme-benchmarks", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunRefreshReadmeBenchmarks(
        args.Length >= 2 ? args[1] : null,
        args.Length >= 3 ? args[2] : null,
        args.Length >= 4 ? args[3] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-readme-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureReadmeReplicaCase(args[1], args.Length >= 3 ? args[2] : null, args.Length >= 4 ? args[3] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-literal-search-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLiteralSearchReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-compiled-literal-family-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureCompiledLiteralFamilyReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-compiled-structural-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureCompiledStructuralReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-ignore-case-primitive-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureIgnoreCasePrimitiveReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--inspect-ignore-case-literal-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunInspectIgnoreCaseLiteralReplicaCase(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--measure-ignore-case-compare-sweep-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureIgnoreCaseLiteralCompareSweepReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-utf8-validation-profile", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureUtf8ValidationProfile(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-utf8-validation-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureUtf8ValidationReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-exact-literal-family-backends-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureExactLiteralFamilyBackendsReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-exact-literal-family-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureExactLiteralFamilyReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-exact-literal-family-packed-offsets-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureExactLiteralFamilyPackedOffsetsReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-exact-literal-family-ismatch-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureExactLiteralFamilyIsMatchReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-exact-literal-family-enumerator-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureExactLiteralFamilyEnumeratorReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-exact-literal-family-hybrid-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureExactLiteralFamilyHybridReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--inspect-exact-literal-family-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunInspectExactLiteralFamilyReplicaCase(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--measure-byte-safe-rebar-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureByteSafeRebarCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-bounded-repeat-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureBoundedRepeatReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-structural-linear-scan-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureStructuralLinearScanCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-structural-family-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureStructuralFamilyCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-structural-family-lokad-code-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureStructuralFamilyLokadCodeCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-identifier-family-lokad-code-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureIdentifierFamilyLokadCodeCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-compiled-structural-family-lokad-code-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureCompiledStructuralFamilyLokadCodeCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-compiled-ordered-window-lokad-code-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureCompiledOrderedWindowLokadCodeCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-compiled-single-literal-ordered-window-lokad-code-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureCompiledSingleLiteralOrderedWindowLokadCodeCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-structural-linear-rebar-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureStructuralLinearRebarCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--dump-lazy-dfa-pattern", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunDumpLazyDfaPattern(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--dump-runtime-tree-pattern", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunDumpRuntimeTreePattern(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--dump-dotnet-generated-regex-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunDumpDotNetGeneratedRegexCase(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--dump-dotnet-generated-regex-pattern", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunDumpDotNetGeneratedRegexPattern(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--dump-structural-linear-pattern", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunDumpStructuralLinearPattern(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--dump-structural-linear-utf8-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunDumpStructuralLinearUtf8Case(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--dump-structural-search-rebar-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunDumpStructuralSearchRebarCase(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--dump-lazy-dfa-rebar-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunDumpLazyDfaRebarCase(args[1]);
}

if (args.Length >= 1 && args[0].Equals("--engine-portfolio-report", StringComparison.Ordinal))
{
    return EnginePortfolioReporter.Run();
}

var artifactsPath = Path.Combine(
    AppContext.BaseDirectory,
    "BenchmarkDotNet.Artifacts",
    "runs",
    $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}");
Directory.CreateDirectory(artifactsPath);
var config = ManualConfig.Create(DefaultConfig.Instance)
    .WithArtifactsPath(artifactsPath);
var effectiveArgs = args;
if (BenchmarkProgramRouter.ShouldUseInProcessForPcre2SpecialBenchmarks(args))
{
    config.AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Instance));
    effectiveArgs = StripJobArguments(args);
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(effectiveArgs, config);
return 0;

static string[] StripJobArguments(string[] arguments)
{
    var result = new List<string>(arguments.Length);
    for (var i = 0; i < arguments.Length; i++)
    {
        var argument = arguments[i];
        if (argument.Equals("--job", StringComparison.OrdinalIgnoreCase) ||
            argument.Equals("-j", StringComparison.OrdinalIgnoreCase))
        {
            i++;
            continue;
        }

        result.Add(argument);
    }

    return [.. result];
}
