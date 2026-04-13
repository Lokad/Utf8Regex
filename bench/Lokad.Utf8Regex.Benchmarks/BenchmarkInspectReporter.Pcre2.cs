using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private const string Pcre2BenchmarkSnapshotFileName = "PCRE2.Benchmarks.json";

    public static int RunInspectUtf8Pcre2Case(string caseId)
    {
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);

        Console.WriteLine($"CaseId            : {benchmarkCase.Id}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"CompileSettings   : {benchmarkCase.CompileSettings}");
        Console.WriteLine($"Replacement       : {benchmarkCase.Replacement}");
        Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
        Console.WriteLine($"SupportsOps       : {benchmarkCase.SupportedOperations}");
        Console.WriteLine($"SupportsBackends  : {benchmarkCase.SupportedBackends}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Pcre2Regex.DebugExecutionKindName}");
        Console.WriteLine($"UsesTranslation   : {context.Utf8Pcre2Regex.DebugUsesUtf8RegexTranslation}");
        Console.WriteLine($"Utf8ExecKind      : {context.Utf8Pcre2Regex.DebugUtf8RegexExecutionKindName}");
        Console.WriteLine($"HasUtf8Equivalent : {context.Utf8Pcre2Regex.DebugHasUtf8SearchEquivalentRegex}");
        Console.WriteLine($"HasManagedRegex   : {context.Utf8Pcre2Regex.DebugHasManagedRegex}");
        Console.WriteLine($"ExecutionPlan     : {context.Utf8Pcre2Regex.DebugDescribeExecutionPlan()}");
        Console.WriteLine($"HasUtf8Regex      : {context.Utf8Regex is not null}");
        Console.WriteLine($"HasRegex          : {context.Regex is not null}");
        return 0;
    }

    public static int RunEmitPcre2TranslationReport()
    {
        var rows = Utf8Pcre2BenchmarkCatalog.GetAllCases()
            .Select(
                static benchmarkCase =>
                {
                    var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
                    var regex = context.Utf8Pcre2Regex;
                    return new Pcre2TranslationRow(
                        benchmarkCase.Id,
                        regex.DebugUsesUtf8RegexTranslation,
                        benchmarkCase.SupportedOperations,
                        benchmarkCase.SupportedBackends,
                        regex.DebugExecutionKindName,
                        regex.DebugUtf8RegexExecutionKindName,
                        regex.DebugHasUtf8SearchEquivalentRegex,
                        regex.DebugHasManagedRegex,
                        regex.DebugDescribeExecutionPlan());
                })
            .OrderBy(static row => row.CaseId)
            .ToArray();

        Console.WriteLine("# PCRE2 Translation Report");
        Console.WriteLine();
        Console.WriteLine($"TotalCases        : {rows.Length}");
        Console.WriteLine($"TranslatedCases   : {rows.Count(static row => row.UsesTranslation)}");
        Console.WriteLine($"UntranslatedCases : {rows.Count(static row => !row.UsesTranslation)}");
        Console.WriteLine();

        WritePcre2TranslationGroup(
            "Untranslated Managed-Comparable Cases",
            rows.Where(static row => !row.UsesTranslation &&
                                     row.SupportedBackends != Utf8Pcre2BenchmarkBackend.Pcre2Only));

        WritePcre2TranslationGroup(
            "Modeled PCRE2-Only Cases",
            rows.Where(static row => !row.UsesTranslation &&
                                     row.SupportedBackends == Utf8Pcre2BenchmarkBackend.Pcre2Only));

        WritePcre2TranslationGroup(
            "Translated Cases",
            rows.Where(static row => row.UsesTranslation));

        return 0;
    }

    public static int RunMeasureUtf8Pcre2Case(string caseId, string? iterationsText, string? samplesText)
    {
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
        var requestedIterations = ParseIterations(iterationsText);
        var iterations = Math.Max(requestedIterations, 16384);
        var samples = ParseSamples(samplesText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        if (iterations != requestedIterations)
        {
            Console.WriteLine($"RequestedIters    : {requestedIterations}");
        }

        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"SupportsOps       : {benchmarkCase.SupportedOperations}");
        Console.WriteLine($"SupportsBackends  : {benchmarkCase.SupportedBackends}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Pcre2Regex.DebugExecutionKindName}");
        Console.WriteLine($"ExecutionPlan     : {context.Utf8Pcre2Regex.DebugDescribeExecutionPlan()}");

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.IsMatch) != 0)
        {
            Measure("Pcre2IsMatch", samples, iterations, () => context.Utf8Pcre2Regex.IsMatch(context.InputBytes) ? 1 : 0);
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.Count) != 0)
        {
            Measure("RawCount", samples, iterations, () => context.Utf8Pcre2Regex.DebugCountRaw(context.InputBytes));
            Measure("PublicCount", samples, iterations, () => context.Utf8Pcre2Regex.Count(context.InputBytes));
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.EnumerateMatches) != 0)
        {
            Measure("PublicConstruct", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumeratePublicConstructionOnly(context.InputBytes));
            Measure("NativeMaterialize", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateNativeMaterializationOnly(context.InputBytes));
            Measure("ArrayConstruct", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateArrayBackedConstructionOnly(context.InputBytes));
            Measure("RawEnumerateSum", samples, iterations, () => ExecutePcre2PublicRawEnumeratorIndexSum(context.Utf8Pcre2Regex, context.InputBytes));
            Measure("ArrayPublicMoveNext", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateArrayBackedPublicMoveNextCount(context.InputBytes));
            Measure("ArrayPublicEnumerateSum", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateArrayBackedPublicIndexSum(context.InputBytes));
            Measure("InternalPublicMoveNext", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateInternalPublicMoveNextCount(context.InputBytes));
            Measure("InternalPublicCurrent", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateInternalPublicCurrentCount(context.InputBytes));
            Measure("InternalPublicCurrentStartSum", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateInternalPublicCurrentStartSum(context.InputBytes));
            Measure("InternalPublicEnumerateSum", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateInternalPublicIndexSum(context.InputBytes));
            Measure("PublicMoveNext", samples, iterations, () => ExecutePcre2PublicEnumeratorMoveNextCount(context.Utf8Pcre2Regex, context.InputBytes));
            Measure("PublicEnumerateSum", samples, iterations, () => ExecutePcre2PublicEnumeratorIndexSum(context.Utf8Pcre2Regex, context.InputBytes));
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.MatchMany) != 0)
        {
            Measure("MatchManySum", samples, iterations, () => ExecutePcre2MatchManyIndexSum(context.Utf8Pcre2Regex, context.InputBytes));
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.Replace) != 0)
        {
            Measure("ReplacementOnly", samples, iterations, () => context.Utf8Pcre2Regex.DebugEvaluateFirstReplacementOnly(context.InputBytes, context.Replacement));
            Measure("PublicReplace", samples, iterations, () => context.Utf8Pcre2Regex.Replace(context.InputBytes, context.Replacement).Length);
        }

        return 0;
    }

    public static int RunMeasureUtf8Pcre2CompatibleCase(string caseId, string? iterationsText, string? samplesText)
    {
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        if ((benchmarkCase.SupportedBackends & Utf8Pcre2BenchmarkBackend.AllManagedComparisons) != Utf8Pcre2BenchmarkBackend.AllManagedComparisons)
        {
            Console.Error.WriteLine($"Case '{caseId}' is not managed-compatible.");
            return 1;
        }

        var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
        var requestedIterations = ParseIterations(iterationsText);
        var iterations = Math.Max(requestedIterations, 16384);
        var samples = ParseSamples(samplesText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        if (iterations != requestedIterations)
        {
            Console.WriteLine($"RequestedIters    : {requestedIterations}");
        }

        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Pcre2Regex.DebugExecutionKindName}");
        Console.WriteLine($"ExecutionPlan     : {context.Utf8Pcre2Regex.DebugDescribeExecutionPlan()}");

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.IsMatch) != 0)
        {
            Measure("Pcre2IsMatch", samples, iterations, () => context.Utf8Pcre2Regex.IsMatch(context.InputBytes) ? 1 : 0);
            Measure("Utf8IsMatch", samples, iterations, () => context.Utf8Regex!.IsMatch(context.InputBytes) ? 1 : 0);
            Measure("DecodeIsMatch", samples, iterations, () => context.Regex!.IsMatch(Encoding.UTF8.GetString(context.InputBytes)) ? 1 : 0);
            Measure("PredecodedIsMatch", samples, iterations, () => context.Regex!.IsMatch(context.InputString) ? 1 : 0);
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.Count) != 0)
        {
            Measure("Pcre2RawCount", samples, iterations, () => context.Utf8Pcre2Regex.DebugCountRaw(context.InputBytes));
            Measure("Pcre2PublicCount", samples, iterations, () => context.Utf8Pcre2Regex.Count(context.InputBytes));
            Measure("Utf8Count", samples, iterations, () => context.Utf8Regex!.Count(context.InputBytes));
            Measure("DecodeCount", samples, iterations, () => context.Regex!.Count(Encoding.UTF8.GetString(context.InputBytes)));
            Measure("PredecodedCount", samples, iterations, () => context.Regex!.Count(context.InputString));
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.EnumerateMatches) != 0)
        {
            Measure("Pcre2PublicConstruct", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumeratePublicConstructionOnly(context.InputBytes));
            Measure("Pcre2PublicMoveNext", samples, iterations, () => ExecutePcre2PublicEnumeratorMoveNextCount(context.Utf8Pcre2Regex, context.InputBytes));
            Measure("Pcre2PublicEnumerateSum", samples, iterations, () => ExecutePcre2PublicEnumeratorIndexSum(context.Utf8Pcre2Regex, context.InputBytes));
            Measure("Utf8EnumerateSum", samples, iterations, () => ExecuteUtf8Pcre2Utf8EnumeratorIndexSum(context));
            Measure("DecodeEnumerateSum", samples, iterations, () => ExecuteUtf8Pcre2DecodeEnumerateIndexSum(context));
            Measure("PredecodedEnumerateSum", samples, iterations, () => ExecuteUtf8Pcre2PredecodedEnumerateIndexSum(context));
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.MatchMany) != 0)
        {
            Measure("Pcre2MatchManySum", samples, iterations, () => ExecutePcre2MatchManyIndexSum(context.Utf8Pcre2Regex, context.InputBytes));
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.Replace) != 0)
        {
            Measure("Pcre2ReplacementOnly", samples, iterations, () => context.Utf8Pcre2Regex.DebugEvaluateFirstReplacementOnly(context.InputBytes, context.Replacement));
            Measure("Pcre2PublicReplace", samples, iterations, () => context.Utf8Pcre2Regex.Replace(context.InputBytes, context.Replacement).Length);
            Measure("Utf8Replace", samples, iterations, () => context.Utf8Regex!.Replace(context.InputBytes, Encoding.UTF8.GetBytes(context.Replacement)).Length);
            Measure("DecodeReplace", samples, iterations, () => context.Regex!.Replace(Encoding.UTF8.GetString(context.InputBytes), context.Replacement).Length);
            Measure("PredecodedReplace", samples, iterations, () => context.Regex!.Replace(context.InputString, context.Replacement).Length);
        }

        return 0;
    }

    public static int RunMeasureUtf8Pcre2SpecialCase(string caseId, string? iterationsText, string? samplesText)
    {
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        if (benchmarkCase.SupportedBackends != Utf8Pcre2BenchmarkBackend.Pcre2Only)
        {
            Console.Error.WriteLine($"Case '{caseId}' is not PCRE2-only.");
            return 1;
        }

        var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
        var requestedIterations = ParseIterations(iterationsText);
        var iterations = Math.Max(requestedIterations, 16384);
        var samples = ParseSamples(samplesText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        if (iterations != requestedIterations)
        {
            Console.WriteLine($"RequestedIters    : {requestedIterations}");
        }

        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Pcre2Regex.DebugExecutionKindName}");
        Console.WriteLine($"ExecutionPlan     : {context.Utf8Pcre2Regex.DebugDescribeExecutionPlan()}");

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.IsMatch) != 0)
        {
            Measure("Pcre2IsMatch", samples, iterations, () => context.Utf8Pcre2Regex.IsMatch(context.InputBytes) ? 1 : 0);
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.Count) != 0)
        {
            Measure("Pcre2RawCount", samples, iterations, () => context.Utf8Pcre2Regex.DebugCountRaw(context.InputBytes));
            Measure("Pcre2PublicCount", samples, iterations, () => context.Utf8Pcre2Regex.Count(context.InputBytes));
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.EnumerateMatches) != 0)
        {
            Measure("Pcre2NativeMaterialize", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateNativeMaterializationOnly(context.InputBytes));
            Measure("Pcre2RawEnumerateSum", samples, iterations, () => ExecutePcre2PublicRawEnumeratorIndexSum(context.Utf8Pcre2Regex, context.InputBytes));
            Measure("Pcre2InternalPublicMoveNext", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateInternalPublicMoveNextCount(context.InputBytes));
            Measure("Pcre2InternalPublicCurrent", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateInternalPublicCurrentCount(context.InputBytes));
            Measure("Pcre2InternalPublicCurrentStartSum", samples, iterations, () => context.Utf8Pcre2Regex.DebugEnumerateInternalPublicCurrentStartSum(context.InputBytes));
            Measure("Pcre2PublicEnumerateSum", samples, iterations, () => ExecutePcre2PublicEnumeratorIndexSum(context.Utf8Pcre2Regex, context.InputBytes));
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.MatchMany) != 0)
        {
            Measure("Pcre2MatchManySum", samples, iterations, () => ExecutePcre2MatchManyIndexSum(context.Utf8Pcre2Regex, context.InputBytes));
        }

        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.Replace) != 0)
        {
            Measure("Pcre2ReplacementOnly", samples, iterations, () => context.Utf8Pcre2Regex.DebugEvaluateFirstReplacementOnly(context.InputBytes, context.Replacement));
            Measure("Pcre2PublicReplace", samples, iterations, () => context.Utf8Pcre2Regex.Replace(context.InputBytes, context.Replacement).Length);
        }

        return 0;
    }

    public static int RunEmitPcre2BenchmarkJson()
    {
        var snapshot = LoadPcre2BenchmarkSnapshot();
        Console.WriteLine(JsonSerializer.Serialize(snapshot, Pcre2BenchmarkSnapshotJsonOptions));
        return 0;
    }

    public static int RunRefreshPcre2BenchmarkCase(string caseId, string? iterationsText, string? samplesText)
    {
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var snapshot = LoadPcre2BenchmarkSnapshot();

        foreach (var section in GetPcre2SectionsForCase(caseId))
        {
            var measurement = MeasurePcre2SnapshotCase(section, caseId, iterations, samples);
            GetOrAddPcre2SnapshotSection(snapshot, section).Cases[caseId] = measurement;
        }

        SavePcre2BenchmarkSnapshot(snapshot);
        Console.WriteLine($"Updated PCRE2 benchmark case: {caseId}");
        return 0;
    }

    public static int RunRefreshPcre2Benchmarks(string? sectionsText, string? iterationsText, string? samplesText)
    {
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var snapshot = LoadPcre2BenchmarkSnapshot();
        var sections = ParsePcre2Sections(sectionsText);

        foreach (var section in sections)
        {
            RefreshPcre2SnapshotSection(snapshot, section, iterations, samples);
        }

        SavePcre2BenchmarkSnapshot(snapshot);
        Console.WriteLine($"Updated PCRE2 benchmark sections: {string.Join(", ", sections.Select(GetPcre2SectionToken))}");
        return 0;
    }

    public static int RunEmitPcre2PriorityReport()
    {
        var snapshot = LoadPcre2BenchmarkSnapshot();
        var rows = snapshot.Sections
            .SelectMany(
                static kvp => kvp.Value.Cases.Select(
                    caseKvp => new Pcre2PriorityRow(
                        kvp.Key,
                        caseKvp.Key,
                        caseKvp.Value.Utf8Pcre2,
                        caseKvp.Value.Utf8Regex,
                        caseKvp.Value.PredecodedRegex,
                        caseKvp.Value.DecodeThenRegex)))
            .ToArray();

        Console.WriteLine("# PCRE2 Priority Report");
        Console.WriteLine();

        WritePcre2PriorityGroup(
            "Worst PredecodedRegex Ratios",
            rows.Where(static r => r.PredecodedRegex is > 0)
                .OrderByDescending(static r => r.Utf8Pcre2 / r.PredecodedRegex!.Value)
                .Take(12),
            static r => FormatPriorityRatio(r.Utf8Pcre2, r.PredecodedRegex));

        WritePcre2PriorityGroup(
            "Worst DecodeThenRegex Ratios",
            rows.Where(static r => r.DecodeThenRegex is > 0)
                .OrderByDescending(static r => r.Utf8Pcre2 / r.DecodeThenRegex!.Value)
                .Take(12),
            static r => FormatPriorityRatio(r.Utf8Pcre2, r.DecodeThenRegex));

        WritePcre2PriorityGroup(
            "Highest Absolute Utf8Pcre2",
            rows.OrderByDescending(static r => r.Utf8Pcre2).Take(12),
            static r => r.Utf8Pcre2.ToString("F3", CultureInfo.InvariantCulture) + " us");

        WritePcre2PriorityGroup(
            "PCRE2-Specific Rows",
            rows.Where(static r => r.PredecodedRegex is null && r.DecodeThenRegex is null)
                .OrderByDescending(static r => r.Utf8Pcre2)
                .Take(12),
            static _ => "no .NET baseline");

        return 0;
    }

    public static int RunEmitUtf8Pcre2PerfLedger(string? iterationsText)
    {
        var requestedIterations = ParseIterations(iterationsText);
        var iterations = Math.Max(requestedIterations, 16384);
        const int samples = 3;
        var cases =
            new[]
            {
                "pcre2/branch-reset-nested",
                "pcre2/duplicate-names",
                "pcre2/same-start-global",
                "pcre2/kreset-repeat",
                "pcre2/kreset-captured-repeat",
                "pcre2/kreset-atomic-alt",
                "pcre2/branch-reset-followup",
                "pcre2/conditional-negative-lookahead",
                "pcre2/conditional-accept-negative-lookahead",
                "pcre2/backslash-c-literal",
                "pcre2/recursive-optional",
            };

        Console.WriteLine("# PCRE2 Perf Ledger");
        Console.WriteLine();
        Console.WriteLine($"Iterations: {iterations}");
        if (iterations != requestedIterations)
        {
            Console.WriteLine($"RequestedIterations: {requestedIterations}");
        }

        Console.WriteLine($"Samples: {samples}");
        Console.WriteLine();
        Console.WriteLine("| Case | Kind | Plan | IsMatch us | RawCount us | PublicCount us | Count Tax | NativeMaterialize us | RawEnumerate us | PublicEnumerate us | Enumerate Tax | MatchMany us | MatchMany Tax | ReplacementOnly us | PublicReplace us | Replace Tax |");
        Console.WriteLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        foreach (var caseId in cases)
        {
            var row = MeasureUtf8Pcre2CaseOutOfProcess(caseId, iterations, samples);
            Console.WriteLine(
                $"| {caseId} | {row.ExecutionKind} | `{row.ExecutionPlan}` | {FormatLedgerValue(row.IsMatch)} | {FormatLedgerValue(row.RawCount)} | {FormatLedgerValue(row.PublicCount)} | {FormatLedgerRatio(row.PublicCount, row.RawCount)} | {FormatLedgerValue(row.NativeMaterialize)} | {FormatLedgerValue(row.RawEnumerate)} | {FormatLedgerValue(row.PublicEnumerate)} | {FormatLedgerRatio(row.PublicEnumerate, row.RawEnumerate)} | {FormatLedgerValue(row.MatchMany)} | {FormatLedgerRatio(row.MatchMany, row.RawEnumerate)} | {FormatLedgerValue(row.ReplacementOnly)} | {FormatLedgerValue(row.PublicReplace)} | {FormatLedgerRatio(row.PublicReplace, row.ReplacementOnly)} |");
        }

        return 0;
    }

    public static int RunEmitUtf8Pcre2ManagedPerfLedger(string? iterationsText)
    {
        var requestedIterations = ParseIterations(iterationsText);
        var iterations = Math.Max(requestedIterations, 16384);
        const int samples = 3;
        var cases =
            new[]
            {
                "simple/foo-dense",
                "simple/foo-optional-bar",
                "simple/ab-plus",
                "simple/httpclient-caseless",
                "simple/loglevel-multiline",
            };

        Console.WriteLine("# PCRE2 Managed-Compatible Perf Ledger");
        Console.WriteLine();
        Console.WriteLine($"Iterations: {iterations}");
        if (iterations != requestedIterations)
        {
            Console.WriteLine($"RequestedIterations: {requestedIterations}");
        }

        Console.WriteLine($"Samples: {samples}");
        Console.WriteLine();
        Console.WriteLine("| Case | Plan | Pcre2 Count us | Utf8 Count us | Decode Count us | Predecoded Count us | Pcre2 Enum us | Utf8 Enum us | Decode Enum us | Predecoded Enum us | Pcre2 Replace us | Utf8 Replace us | Decode Replace us | Predecoded Replace us |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        foreach (var caseId in cases)
        {
            var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
            var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);

            var pcre2Count = MeasureMicroseconds(samples, iterations, () => context.Utf8Pcre2Regex.Count(context.InputBytes));
            var utf8Count = MeasureMicroseconds(samples, iterations, () => context.Utf8Regex!.Count(context.InputBytes));
            var decodeCount = MeasureMicroseconds(samples, iterations, () => context.Regex!.Count(Encoding.UTF8.GetString(context.InputBytes)));
            var predecodedCount = MeasureMicroseconds(samples, iterations, () => context.Regex!.Count(context.InputString));
            var pcre2Enumerate = MeasureMicroseconds(samples, iterations, () => ExecutePcre2PublicEnumeratorIndexSum(context.Utf8Pcre2Regex, context.InputBytes));
            var utf8Enumerate = MeasureMicroseconds(samples, iterations, () => ExecuteUtf8Pcre2Utf8EnumeratorIndexSum(context));
            var decodeEnumerate = MeasureMicroseconds(samples, iterations, () => ExecuteUtf8Pcre2DecodeEnumerateIndexSum(context));
            var predecodedEnumerate = MeasureMicroseconds(samples, iterations, () => ExecuteUtf8Pcre2PredecodedEnumerateIndexSum(context));
            var pcre2Replace = MeasureMicroseconds(samples, iterations, () => context.Utf8Pcre2Regex.Replace(context.InputBytes, context.Replacement).Length);
            var utf8Replace = MeasureMicroseconds(samples, iterations, () => context.Utf8Regex!.Replace(context.InputBytes, Encoding.UTF8.GetBytes(context.Replacement)).Length);
            var decodeReplace = MeasureMicroseconds(samples, iterations, () => context.Regex!.Replace(Encoding.UTF8.GetString(context.InputBytes), context.Replacement).Length);
            var predecodedReplace = MeasureMicroseconds(samples, iterations, () => context.Regex!.Replace(context.InputString, context.Replacement).Length);

            Console.WriteLine(
                $"| {caseId} | `{context.Utf8Pcre2Regex.DebugDescribeExecutionPlan()}` | {FormatLedgerValue(pcre2Count)} | {FormatLedgerValue(utf8Count)} | {FormatLedgerValue(decodeCount)} | {FormatLedgerValue(predecodedCount)} | {FormatLedgerValue(pcre2Enumerate)} | {FormatLedgerValue(utf8Enumerate)} | {FormatLedgerValue(decodeEnumerate)} | {FormatLedgerValue(predecodedEnumerate)} | {FormatLedgerValue(pcre2Replace)} | {FormatLedgerValue(utf8Replace)} | {FormatLedgerValue(decodeReplace)} | {FormatLedgerValue(predecodedReplace)} |");
        }

        return 0;
    }

    private sealed record Utf8Pcre2CaseMeasurementRow(
        string ExecutionKind,
        string ExecutionPlan,
        double? IsMatch,
        double? RawCount,
        double? PublicCount,
        double? NativeMaterialize,
        double? RawEnumerate,
        double? PublicEnumerate,
        double? MatchMany,
        double? ReplacementOnly,
        double? PublicReplace);

    private static Utf8Pcre2CaseMeasurementRow MeasureUtf8Pcre2CaseOutOfProcess(string caseId, int iterations, int samples)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(@".\bench\Lokad.Utf8Regex.Benchmarks\Lokad.Utf8Regex.Benchmarks.csproj");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Release");
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add("--measure-pcre2-case");
        psi.ArgumentList.Add(caseId);
        psi.ArgumentList.Add(iterations.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(samples.ToString(CultureInfo.InvariantCulture));

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Unable to start child process for --measure-pcre2-case {caseId}.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Child process failed for --measure-pcre2-case {caseId}: {output}");
        }

        return ParseUtf8Pcre2CaseMeasurementRow(output);
    }

    private static Utf8Pcre2CaseMeasurementRow ParseUtf8Pcre2CaseMeasurementRow(string text)
    {
        string executionKind = string.Empty;
        string executionPlan = string.Empty;
        double? isMatch = null;
        double? rawCount = null;
        double? publicCount = null;
        double? nativeMaterialize = null;
        double? rawEnumerate = null;
        double? publicEnumerate = null;
        double? matchMany = null;
        double? replacementOnly = null;
        double? publicReplace = null;

        foreach (var line in text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("ExecutionKind", StringComparison.Ordinal))
            {
                executionKind = ExtractValueAfterColon(line);
                continue;
            }

            if (line.StartsWith("ExecutionPlan", StringComparison.Ordinal))
            {
                executionPlan = ExtractValueAfterColon(line);
                continue;
            }

            if (TryParseMicrosecondsLine(line, "Pcre2IsMatch", out var value)) { isMatch = value; continue; }
            if (TryParseMicrosecondsLine(line, "RawCount", out value)) { rawCount = value; continue; }
            if (TryParseMicrosecondsLine(line, "PublicCount", out value)) { publicCount = value; continue; }
            if (TryParseMicrosecondsLine(line, "NativeMaterialize", out value)) { nativeMaterialize = value; continue; }
            if (TryParseMicrosecondsLine(line, "RawEnumerateSum", out value)) { rawEnumerate = value; continue; }
            if (TryParseMicrosecondsLine(line, "PublicEnumerateSum", out value)) { publicEnumerate = value; continue; }
            if (TryParseMicrosecondsLine(line, "MatchManySum", out value)) { matchMany = value; continue; }
            if (TryParseMicrosecondsLine(line, "ReplacementOnly", out value)) { replacementOnly = value; continue; }
            if (TryParseMicrosecondsLine(line, "PublicReplace", out value)) { publicReplace = value; continue; }
        }

        return new Utf8Pcre2CaseMeasurementRow(
            executionKind,
            executionPlan,
            isMatch,
            rawCount,
            publicCount,
            nativeMaterialize,
            rawEnumerate,
            publicEnumerate,
            matchMany,
            replacementOnly,
            publicReplace);
    }

    private static int ExecutePcre2PublicRawEnumeratorIndexSum(Utf8Pcre2Regex regex, byte[] input)
        => regex.DebugEnumerateRawIndexSum(input);

    private static int ExecutePcre2PublicEnumeratorIndexSum(Utf8Pcre2Regex regex, byte[] input)
    {
        var sum = 0;
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.StartOffsetInBytes;
        }

        return sum;
    }

    private static int ExecutePcre2PublicEnumeratorMoveNextCount(Utf8Pcre2Regex regex, byte[] input)
    {
        var count = 0;
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int ExecutePcre2MatchManyIndexSum(Utf8Pcre2Regex regex, byte[] input)
    {
        Span<Utf8Pcre2MatchData> buffer = stackalloc Utf8Pcre2MatchData[8];
        var written = regex.MatchMany(input, buffer, out _);
        var sum = 0;
        for (var i = 0; i < written; i++)
        {
            sum += buffer[i].StartOffsetInBytes;
        }

        return sum;
    }

    private static int ExecuteUtf8Pcre2Utf8EnumeratorIndexSum(Utf8Pcre2BenchmarkContext context)
    {
        var sum = 0;
        var enumerator = context.Utf8Regex!.EnumerateMatches(context.InputBytes);
        while (enumerator.MoveNext())
        {
            if (!enumerator.Current.TryGetByteRange(out var indexInBytes, out _))
            {
                throw new InvalidOperationException("Utf8Regex benchmark enumerator produced a non-byte-aligned match.");
            }

            sum += indexInBytes;
        }

        return sum;
    }

    private static int ExecuteUtf8Pcre2DecodeEnumerateIndexSum(Utf8Pcre2BenchmarkContext context)
        => SumRegexMatches(context.Regex!, Encoding.UTF8.GetString(context.InputBytes));

    private static int ExecuteUtf8Pcre2PredecodedEnumerateIndexSum(Utf8Pcre2BenchmarkContext context)
        => SumRegexMatches(context.Regex!, context.InputString);

    private static Pcre2BenchmarkSnapshot LoadPcre2BenchmarkSnapshot()
    {
        var path = Path.Combine(Path.GetDirectoryName(FindRepoFile("README.md"))!, Pcre2BenchmarkSnapshotFileName);
        if (!File.Exists(path))
        {
            return new Pcre2BenchmarkSnapshot();
        }

        var json = File.ReadAllText(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<Pcre2BenchmarkSnapshot>(json, Pcre2BenchmarkSnapshotJsonOptions) ?? throw new InvalidOperationException($"Could not deserialize {Pcre2BenchmarkSnapshotFileName}.");
    }

    private static void SavePcre2BenchmarkSnapshot(Pcre2BenchmarkSnapshot snapshot)
    {
        var path = Path.Combine(Path.GetDirectoryName(FindRepoFile("README.md"))!, Pcre2BenchmarkSnapshotFileName);
        var json = JsonSerializer.Serialize(snapshot, Pcre2BenchmarkSnapshotJsonOptions);
        File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static IReadOnlyList<Pcre2BenchmarkSection> ParsePcre2Sections(string? sectionsText)
    {
        if (string.IsNullOrWhiteSpace(sectionsText))
        {
            return Enum.GetValues<Pcre2BenchmarkSection>();
        }

        var sections = new List<Pcre2BenchmarkSection>();
        foreach (var token in sectionsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            sections.Add(token.ToLowerInvariant() switch
            {
                "pcre2-managed-compatible-ismatch" => Pcre2BenchmarkSection.ManagedCompatibleIsMatch,
                "pcre2-managed-compatible-count" => Pcre2BenchmarkSection.ManagedCompatibleCount,
                "pcre2-managed-compatible-enumerate" => Pcre2BenchmarkSection.ManagedCompatibleEnumerate,
                "pcre2-managed-compatible-matchmany" => Pcre2BenchmarkSection.ManagedCompatibleMatchMany,
                "pcre2-managed-compatible-replace" => Pcre2BenchmarkSection.ManagedCompatibleReplace,
                "pcre2-special-ismatch" => Pcre2BenchmarkSection.SpecialIsMatch,
                "pcre2-special-count" => Pcre2BenchmarkSection.SpecialCount,
                "pcre2-special-enumerate" => Pcre2BenchmarkSection.SpecialEnumerate,
                "pcre2-special-matchmany" => Pcre2BenchmarkSection.SpecialMatchMany,
                "pcre2-special-replace" => Pcre2BenchmarkSection.SpecialReplace,
                "all" => throw new InvalidOperationException("Use an empty section argument to refresh all PCRE2 benchmark sections."),
                _ => throw new InvalidOperationException($"Unknown PCRE2 benchmark section '{token}'."),
            });
        }

        return sections.Distinct().ToArray();
    }

    private static IEnumerable<Pcre2BenchmarkSection> GetPcre2SectionsForCase(string caseId)
    {
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        foreach (var section in Enum.GetValues<Pcre2BenchmarkSection>())
        {
            if (IsCaseInPcre2Section(benchmarkCase, section))
            {
                yield return section;
            }
        }
    }

    private static void RefreshPcre2SnapshotSection(Pcre2BenchmarkSnapshot snapshot, Pcre2BenchmarkSection section, int iterations, int samples)
    {
        var sectionSnapshot = GetOrAddPcre2SnapshotSection(snapshot, section);
        sectionSnapshot.Cases.Clear();

        foreach (var benchmarkCase in Utf8Pcre2BenchmarkCatalog.GetAllCases().Where(c => IsCaseInPcre2Section(c, section)).OrderBy(static c => c.Id, StringComparer.Ordinal))
        {
            sectionSnapshot.Cases[benchmarkCase.Id] = MeasurePcre2SnapshotCase(section, benchmarkCase.Id, iterations, samples);
        }
    }

    private static bool IsCaseInPcre2Section(Utf8Pcre2BenchmarkCase benchmarkCase, Pcre2BenchmarkSection section)
    {
        var (operation, backends, exactBackends) = GetPcre2SectionRequirements(section);
        if ((benchmarkCase.SupportedOperations & operation) == 0)
        {
            return false;
        }

        if (exactBackends)
        {
            return benchmarkCase.SupportedBackends == backends;
        }

        return (benchmarkCase.SupportedBackends & backends) == backends;
    }

    private static Pcre2CaseMeasurementJson MeasurePcre2SnapshotCase(Pcre2BenchmarkSection section, string caseId, int iterations, int samples)
    {
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
        var operation = GetPcre2SectionRequirements(section).Operation;
        var effectiveIterations = ParsePcre2SnapshotIterations(benchmarkCase, section, iterations);

        double utf8Pcre2 = operation switch
        {
            Utf8Pcre2BenchmarkOperation.IsMatch => MeasureMicroseconds(samples, effectiveIterations, () => context.Utf8Pcre2Regex.IsMatch(context.InputBytes) ? 1 : 0),
            Utf8Pcre2BenchmarkOperation.Count => MeasureMicroseconds(samples, effectiveIterations, () => context.Utf8Pcre2Regex.Count(context.InputBytes)),
            Utf8Pcre2BenchmarkOperation.EnumerateMatches => MeasureMicroseconds(samples, effectiveIterations, () => ExecutePcre2PublicEnumeratorIndexSum(context.Utf8Pcre2Regex, context.InputBytes)),
            Utf8Pcre2BenchmarkOperation.MatchMany => MeasureMicroseconds(samples, effectiveIterations, () => ExecutePcre2MatchManyIndexSum(context.Utf8Pcre2Regex, context.InputBytes)),
            Utf8Pcre2BenchmarkOperation.Replace => MeasureMicroseconds(samples, effectiveIterations, () => context.Utf8Pcre2Regex.Replace(context.InputBytes, context.Replacement).Length),
            _ => throw new InvalidOperationException($"Unsupported PCRE2 snapshot operation '{operation}'."),
        };

        double? utf8Regex = null;
        double? predecodedRegex = null;
        double? decodeThenRegex = null;

        if ((benchmarkCase.SupportedBackends & Utf8Pcre2BenchmarkBackend.Utf8Regex) != 0)
        {
            utf8Regex = operation switch
            {
                Utf8Pcre2BenchmarkOperation.IsMatch => MeasureMicroseconds(samples, effectiveIterations, () => context.Utf8Regex!.IsMatch(context.InputBytes) ? 1 : 0),
                Utf8Pcre2BenchmarkOperation.Count => MeasureMicroseconds(samples, effectiveIterations, () => context.Utf8Regex!.Count(context.InputBytes)),
                Utf8Pcre2BenchmarkOperation.EnumerateMatches => MeasureMicroseconds(samples, effectiveIterations, () => ExecuteUtf8Pcre2Utf8EnumeratorIndexSum(context)),
                Utf8Pcre2BenchmarkOperation.Replace => MeasureMicroseconds(samples, effectiveIterations, () => context.Utf8Regex!.Replace(context.InputBytes, Encoding.UTF8.GetBytes(context.Replacement)).Length),
                _ => null,
            };
        }

        if ((benchmarkCase.SupportedBackends & Utf8Pcre2BenchmarkBackend.PredecodedRegex) != 0)
        {
            predecodedRegex = operation switch
            {
                Utf8Pcre2BenchmarkOperation.IsMatch => MeasureMicroseconds(samples, effectiveIterations, () => context.Regex!.IsMatch(context.InputString) ? 1 : 0),
                Utf8Pcre2BenchmarkOperation.Count => MeasureMicroseconds(samples, effectiveIterations, () => context.Regex!.Count(context.InputString)),
                Utf8Pcre2BenchmarkOperation.EnumerateMatches => MeasureMicroseconds(samples, effectiveIterations, () => ExecuteUtf8Pcre2PredecodedEnumerateIndexSum(context)),
                Utf8Pcre2BenchmarkOperation.Replace => MeasureMicroseconds(samples, effectiveIterations, () => context.Regex!.Replace(context.InputString, context.Replacement).Length),
                _ => null,
            };
        }

        if ((benchmarkCase.SupportedBackends & Utf8Pcre2BenchmarkBackend.DecodeThenRegex) != 0)
        {
            decodeThenRegex = operation switch
            {
                Utf8Pcre2BenchmarkOperation.IsMatch => MeasureMicroseconds(samples, effectiveIterations, () => context.Regex!.IsMatch(Encoding.UTF8.GetString(context.InputBytes)) ? 1 : 0),
                Utf8Pcre2BenchmarkOperation.Count => MeasureMicroseconds(samples, effectiveIterations, () => context.Regex!.Count(Encoding.UTF8.GetString(context.InputBytes))),
                Utf8Pcre2BenchmarkOperation.EnumerateMatches => MeasureMicroseconds(samples, effectiveIterations, () => ExecuteUtf8Pcre2DecodeEnumerateIndexSum(context)),
                Utf8Pcre2BenchmarkOperation.Replace => MeasureMicroseconds(samples, effectiveIterations, () => context.Regex!.Replace(Encoding.UTF8.GetString(context.InputBytes), context.Replacement).Length),
                _ => null,
            };
        }

        return new Pcre2CaseMeasurementJson
        {
            Utf8Pcre2 = utf8Pcre2,
            Utf8Regex = utf8Regex,
            PredecodedRegex = predecodedRegex,
            DecodeThenRegex = decodeThenRegex,
        };
    }

    private static int ParsePcre2SnapshotIterations(Utf8Pcre2BenchmarkCase benchmarkCase, Pcre2BenchmarkSection section, int requestedIterations)
    {
        if (benchmarkCase.Id.Contains('/', StringComparison.Ordinal) &&
            LokadPublicBenchmarkContext.GetAllCaseIds().Contains(benchmarkCase.Id, StringComparer.Ordinal) &&
            section is
                Pcre2BenchmarkSection.ManagedCompatibleIsMatch or
                Pcre2BenchmarkSection.ManagedCompatibleCount or
                Pcre2BenchmarkSection.ManagedCompatibleReplace)
        {
            var context = new LokadPublicBenchmarkContext(benchmarkCase.Id);
            return ParseReadmePublicIterations(context, requestedIterations.ToString(CultureInfo.InvariantCulture));
        }

        var inputBytes = Encoding.UTF8.GetByteCount(benchmarkCase.Input);
        var operation = GetPcre2SectionRequirements(section).Operation;
        var isManagedCompatibleSection = section is
            Pcre2BenchmarkSection.ManagedCompatibleIsMatch or
            Pcre2BenchmarkSection.ManagedCompatibleCount or
            Pcre2BenchmarkSection.ManagedCompatibleEnumerate or
            Pcre2BenchmarkSection.ManagedCompatibleMatchMany or
            Pcre2BenchmarkSection.ManagedCompatibleReplace;

        if (inputBytes <= 256)
        {
            if (isManagedCompatibleSection)
            {
                return operation switch
                {
                    Utf8Pcre2BenchmarkOperation.IsMatch => Math.Max(10000, requestedIterations),
                    Utf8Pcre2BenchmarkOperation.Count => Math.Max(5000, requestedIterations),
                    Utf8Pcre2BenchmarkOperation.EnumerateMatches => Math.Max(5000, requestedIterations),
                    Utf8Pcre2BenchmarkOperation.MatchMany => Math.Max(5000, requestedIterations),
                    Utf8Pcre2BenchmarkOperation.Replace => Math.Max(5000, requestedIterations),
                    _ => requestedIterations,
                };
            }

            return operation switch
            {
                Utf8Pcre2BenchmarkOperation.IsMatch => Math.Max(5000, requestedIterations),
                Utf8Pcre2BenchmarkOperation.Count => Math.Max(2000, requestedIterations),
                Utf8Pcre2BenchmarkOperation.EnumerateMatches => Math.Max(2000, requestedIterations),
                Utf8Pcre2BenchmarkOperation.MatchMany => Math.Max(2000, requestedIterations),
                Utf8Pcre2BenchmarkOperation.Replace => Math.Max(2000, requestedIterations),
                _ => requestedIterations,
            };
        }

        if (inputBytes <= 4 * 1024)
        {
            return operation switch
            {
                Utf8Pcre2BenchmarkOperation.IsMatch => Math.Max(2000, requestedIterations),
                Utf8Pcre2BenchmarkOperation.Count => Math.Max(1000, requestedIterations),
                Utf8Pcre2BenchmarkOperation.EnumerateMatches => Math.Max(1000, requestedIterations),
                Utf8Pcre2BenchmarkOperation.MatchMany => Math.Max(1000, requestedIterations),
                Utf8Pcre2BenchmarkOperation.Replace => Math.Max(1000, requestedIterations),
                _ => requestedIterations,
            };
        }

        return requestedIterations;
    }

    private static Pcre2BenchmarkSectionJson GetOrAddPcre2SnapshotSection(Pcre2BenchmarkSnapshot snapshot, Pcre2BenchmarkSection section)
    {
        var key = GetPcre2SectionToken(section);
        if (!snapshot.Sections.TryGetValue(key, out var sectionSnapshot))
        {
            sectionSnapshot = new Pcre2BenchmarkSectionJson();
            snapshot.Sections[key] = sectionSnapshot;
        }

        return sectionSnapshot;
    }

    private static string GetPcre2SectionToken(Pcre2BenchmarkSection section)
        => section switch
        {
            Pcre2BenchmarkSection.ManagedCompatibleIsMatch => "pcre2-managed-compatible-ismatch",
            Pcre2BenchmarkSection.ManagedCompatibleCount => "pcre2-managed-compatible-count",
            Pcre2BenchmarkSection.ManagedCompatibleEnumerate => "pcre2-managed-compatible-enumerate",
            Pcre2BenchmarkSection.ManagedCompatibleMatchMany => "pcre2-managed-compatible-matchmany",
            Pcre2BenchmarkSection.ManagedCompatibleReplace => "pcre2-managed-compatible-replace",
            Pcre2BenchmarkSection.SpecialIsMatch => "pcre2-special-ismatch",
            Pcre2BenchmarkSection.SpecialCount => "pcre2-special-count",
            Pcre2BenchmarkSection.SpecialEnumerate => "pcre2-special-enumerate",
            Pcre2BenchmarkSection.SpecialMatchMany => "pcre2-special-matchmany",
            Pcre2BenchmarkSection.SpecialReplace => "pcre2-special-replace",
            _ => throw new ArgumentOutOfRangeException(nameof(section)),
        };

    private static (Utf8Pcre2BenchmarkOperation Operation, Utf8Pcre2BenchmarkBackend Backends, bool ExactBackends) GetPcre2SectionRequirements(Pcre2BenchmarkSection section)
        => section switch
        {
            Pcre2BenchmarkSection.ManagedCompatibleIsMatch => (Utf8Pcre2BenchmarkOperation.IsMatch, Utf8Pcre2BenchmarkBackend.AllManagedComparisons, exactBackends: false),
            Pcre2BenchmarkSection.ManagedCompatibleCount => (Utf8Pcre2BenchmarkOperation.Count, Utf8Pcre2BenchmarkBackend.AllManagedComparisons, exactBackends: false),
            Pcre2BenchmarkSection.ManagedCompatibleEnumerate => (Utf8Pcre2BenchmarkOperation.EnumerateMatches, Utf8Pcre2BenchmarkBackend.AllManagedComparisons, exactBackends: false),
            Pcre2BenchmarkSection.ManagedCompatibleMatchMany => (Utf8Pcre2BenchmarkOperation.MatchMany, Utf8Pcre2BenchmarkBackend.AllManagedComparisons, exactBackends: false),
            Pcre2BenchmarkSection.ManagedCompatibleReplace => (Utf8Pcre2BenchmarkOperation.Replace, Utf8Pcre2BenchmarkBackend.AllManagedComparisons, exactBackends: false),
            Pcre2BenchmarkSection.SpecialIsMatch => (Utf8Pcre2BenchmarkOperation.IsMatch, Utf8Pcre2BenchmarkBackend.Pcre2Only, exactBackends: true),
            Pcre2BenchmarkSection.SpecialCount => (Utf8Pcre2BenchmarkOperation.Count, Utf8Pcre2BenchmarkBackend.Pcre2Only, exactBackends: true),
            Pcre2BenchmarkSection.SpecialEnumerate => (Utf8Pcre2BenchmarkOperation.EnumerateMatches, Utf8Pcre2BenchmarkBackend.Pcre2Only, exactBackends: true),
            Pcre2BenchmarkSection.SpecialMatchMany => (Utf8Pcre2BenchmarkOperation.MatchMany, Utf8Pcre2BenchmarkBackend.Pcre2Only, exactBackends: true),
            Pcre2BenchmarkSection.SpecialReplace => (Utf8Pcre2BenchmarkOperation.Replace, Utf8Pcre2BenchmarkBackend.Pcre2Only, exactBackends: true),
            _ => throw new ArgumentOutOfRangeException(nameof(section)),
        };

    private static string FormatPriorityRatio(double utf8Pcre2, double? baseline)
    {
        if (baseline is null || baseline.Value == 0)
        {
            return "-";
        }

        return (utf8Pcre2 / baseline.Value).ToString("F2", CultureInfo.InvariantCulture) + "x";
    }

    private static void WritePcre2PriorityGroup(string title, IEnumerable<Pcre2PriorityRow> rows, Func<Pcre2PriorityRow, string> scoreSelector)
    {
        Console.WriteLine($"## {title}");
        foreach (var row in rows)
        {
            Console.WriteLine($"- [{row.Section}] {row.CaseId}: Utf8Pcre2={row.Utf8Pcre2.ToString("F3", CultureInfo.InvariantCulture)} us, score={scoreSelector(row)}");
        }

        Console.WriteLine();
    }

    private static void WritePcre2TranslationGroup(string title, IEnumerable<Pcre2TranslationRow> rows)
    {
        Console.WriteLine($"## {title}");
        foreach (var row in rows)
        {
            Console.WriteLine($"- {row.CaseId}: ops={row.SupportedOperations}, kind={row.ExecutionKind}, utf8={row.Utf8ExecutionKind}, searchEq={(row.HasUtf8SearchEquivalentRegex ? "yes" : "no")}, managed={(row.HasManagedRegex ? "yes" : "no")}, plan={row.ExecutionPlan}");
        }

        Console.WriteLine();
    }

    private static readonly JsonSerializerOptions Pcre2BenchmarkSnapshotJsonOptions = new()
    {
        WriteIndented = true,
    };

    private enum Pcre2BenchmarkSection : byte
    {
        ManagedCompatibleIsMatch = 0,
        ManagedCompatibleCount = 1,
        ManagedCompatibleEnumerate = 2,
        ManagedCompatibleMatchMany = 3,
        ManagedCompatibleReplace = 4,
        SpecialIsMatch = 5,
        SpecialCount = 6,
        SpecialEnumerate = 7,
        SpecialMatchMany = 8,
        SpecialReplace = 9,
    }

    private sealed class Pcre2BenchmarkSnapshot
    {
        public int SchemaVersion { get; set; } = 1;

        public Dictionary<string, Pcre2BenchmarkSectionJson> Sections { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class Pcre2BenchmarkSectionJson
    {
        public Dictionary<string, Pcre2CaseMeasurementJson> Cases { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class Pcre2CaseMeasurementJson
    {
        public double Utf8Pcre2 { get; set; }

        public double? Utf8Regex { get; set; }

        public double? PredecodedRegex { get; set; }

        public double? DecodeThenRegex { get; set; }
    }

    private readonly record struct Pcre2PriorityRow(
        string Section,
        string CaseId,
        double Utf8Pcre2,
        double? Utf8Regex,
        double? PredecodedRegex,
        double? DecodeThenRegex);

    private readonly record struct Pcre2TranslationRow(
        string CaseId,
        bool UsesTranslation,
        Utf8Pcre2BenchmarkOperation SupportedOperations,
        Utf8Pcre2BenchmarkBackend SupportedBackends,
        string ExecutionKind,
        string Utf8ExecutionKind,
        bool HasUtf8SearchEquivalentRegex,
        bool HasManagedRegex,
        string ExecutionPlan);
}
