using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Lokad.Utf8Regex.Benchmarks;

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

if (args.Length >= 2 && args[0].Equals("--inspect-direct-family-pattern", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunInspectDirectFamilyPattern(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--inspect-direct-family-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunInspectDirectFamilyCase(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--measure-utf8-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureUtf8Case(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-case-deep", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureReplicaCaseDeep(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-compiled-microcost-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureCompiledMicrocostCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--dump-dotnet-generated-regex-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunDumpDotNetGeneratedRegexCase(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--dump-dotnet-generated-regex-pattern", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunDumpDotNetGeneratedRegexPattern(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-utf8-validation-profile", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureUtf8ValidationProfile(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-utf8-validation-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureUtf8ValidationReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-unicode-literal-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureUnicodeLiteralCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-token-finder-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureTokenFinderCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-line-family-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLineFamilyCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-literal-search-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLiteralSearchReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--inspect-ignore-case-literal-replica-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunInspectIgnoreCaseLiteralReplicaCase(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--inspect-ascii-twin-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunInspectAsciiTwinReplicaCase(args[1]);
}

if (args.Length >= 2 && args[0].Equals("--measure-ignore-case-primitive-replica-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureIgnoreCasePrimitiveReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-ignore-case-literal-compare-sweep-replica-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureIgnoreCaseLiteralCompareSweepReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-literal-finder-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLineFamilyCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-script-byte-safe-prefix-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadScriptByteSafePrefixCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-lokad-script-prefix-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureLokadScriptPrefixCase(args[1], args.Length >= 3 ? args[2] : null);
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

if (args.Length >= 2 && args[0].Equals("--measure-short-public-microcost-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureShortPublicMicrocostCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-short-prefix-microcost-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureShortPrefixMicrocostCase(args[1], args.Length >= 3 ? args[2] : null);
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

if (args.Length >= 2 && args[0].Equals("--measure-bounded-repeat-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureBoundedRepeatReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-compiled-fallback-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureCompiledFallbackReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-compiled-literal-family-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureCompiledLiteralFamilyReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-exact-literal-family-backends-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureExactLiteralFamilyBackendsReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-exact-literal-family-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureExactLiteralFamilyReplicaCase(args[1], args.Length >= 3 ? args[2] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-readme-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureReadmeCase(
        args[1],
        args.Length >= 3 ? args[2] : null,
        args.Length >= 4 ? args[3] : null);
}

if (args.Length >= 2 && args[0].Equals("--measure-readme-public-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMeasureReadmePublicCase(
        args[1],
        args.Length >= 3 ? args[2] : null,
        args.Length >= 4 ? args[3] : null);
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

if (args.Length >= 1 && args[0].Equals("--migrate-readme-benchmark-json", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunMigrateReadmeBenchmarkJson();
}

if (args.Length >= 2 && args[0].Equals("--refresh-readme-case", StringComparison.Ordinal))
{
    return BenchmarkInspectReporter.RunRefreshReadmeCase(
        args[1],
        args.Length >= 3 ? args[2] : null,
        args.Length >= 4 ? args[3] : null);
}

var artifactsPath = Path.Combine(
    AppContext.BaseDirectory,
    "BenchmarkDotNet.Artifacts",
    "runs",
    $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}");
Directory.CreateDirectory(artifactsPath);
var config = ManualConfig.Create(DefaultConfig.Instance)
    .WithArtifactsPath(artifactsPath);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
return 0;
