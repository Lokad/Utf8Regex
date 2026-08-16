using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.FrontEnd.Runtime;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private static bool s_wroteMeasurementEnvironment;
    private static BenchmarkEnvironmentJson? s_measurementEnvironment;

    public static int RunInspectPattern(string pattern, string? optionsText)
    {
        var options = ParseRegexOptions(optionsText);
        var regex = new Utf8Regex(pattern, options);
        WriteRegexInspection("pattern", pattern, options, inputChars: null, inputBytes: null, origin: null, regex);
        return 0;
    }

    public static int RunInspectUtf8Case(string caseId)
    {
        var benchmarkCase = Utf8RegexBenchmarkCatalog.Get(caseId);
        var context = new Utf8RegexBenchmarkContext(benchmarkCase);
        WriteRegexInspection(
            "utf8-case",
            benchmarkCase.Pattern,
            benchmarkCase.Options,
            context.InputString.Length,
            context.InputBytes.Length,
            $"{benchmarkCase.Operation}/{benchmarkCase.Family}",
            context.Utf8Regex);
        return 0;
    }

    public static int RunInspectReplicaCase(string caseId)
    {
        if (ReplicaCountBenchmarkCase.TryResolve(caseId, out var benchmarkCase))
        {
            var resolvedCase = benchmarkCase!;
            WriteRegexInspection(
                resolvedCase.Source switch
                {
                    ReplicaBenchmarkSource.DotNetPerformance => "dotnet-performance-case",
                    ReplicaBenchmarkSource.Lokad => "lokad-case",
                    _ => "replica-case",
                },
                resolvedCase.Pattern,
                resolvedCase.Options,
                resolvedCase.InputChars,
                resolvedCase.InputBytes.Length,
                BuildReplicaOrigin(resolvedCase),
                resolvedCase.Utf8Regex);
            return 0;
        }

        if (LokadPublicBenchmarkContext.GetAllCaseIds().Contains(caseId, StringComparer.Ordinal))
        {
            var context = new LokadPublicBenchmarkContext(caseId);
            WriteRegexInspection(
                "dotnet-performance-case",
                context.Pattern,
                context.Options,
                context.InputString.Length,
                context.InputBytes.Length,
                context.Operation switch
                {
                    LokadPublicBenchmarkOperation.Count => "public//Count",
                    LokadPublicBenchmarkOperation.IsMatch => "public//IsMatch",
                    LokadPublicBenchmarkOperation.Match => "public//Match",
                    LokadPublicBenchmarkOperation.Replace => "public//Replace",
                    LokadPublicBenchmarkOperation.Split => "public//Split",
                    _ => "public"
                },
                context.Utf8Regex);
            return 0;
        }

        Console.Error.WriteLine($"Case '{caseId}' was not found.");
        return 1;
    }

    public static int RunInspectAsciiTwinReplicaCase(string caseId)
    {
        LokadReplicaScriptBenchmarkCase benchmarkCase;
        try
        {
            benchmarkCase = LokadReplicaScriptBenchmarkCatalog.Get(caseId);
        }
        catch (Exception)
        {
            Console.Error.WriteLine($"Case '{caseId}' was not found.");
            return 1;
        }

        if (benchmarkCase.Model != LokadReplicaScriptBenchmarkModel.PrefixMatchLoop)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Model             : {benchmarkCase.Model}");
            Console.WriteLine("AsciiTwinInspect  : case is not a PrefixMatchLoop");
            return 1;
        }

        var context = new LokadReplicaScriptBenchmarkContext(benchmarkCase);
        var regex = context.Utf8Regex;

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Utf8Options}");
        Console.WriteLine($"HasAsciiTwin      : {regex.Inspection.DebugHasAsciiCultureInvariantTwin}");
        if (!regex.Inspection.DebugHasAsciiCultureInvariantTwin || !regex.Inspection.DebugTryGetAsciiCultureInvariantTwin(out var twin))
        {
            return 0;
        }

        Console.WriteLine($"TwinExecutionKind : {twin.ExecutionKind}");
        Console.WriteLine($"TwinCompiledEngine: {twin.CompiledEngineKind}");
        Console.WriteLine($"TwinFallback      : {twin.FallbackReason ?? "<native>"}");

        for (var i = 0; i < context.SampleBytes.Length; i++)
        {
            var sample = context.SampleBytes[i];
            var mainMatch = regex.Match(sample);
            var twinMatch = twin.Match(sample);
            Console.WriteLine(
                $"Sample[{i}]         : ascii={Utf8InputAnalyzer.IsAscii(sample)} len={sample.Length} " +
                $"main={mainMatch.Success}/{(mainMatch.Success ? mainMatch.LengthInBytes : 0)} " +
                $"twin={twinMatch.Success}/{(twinMatch.Success ? twinMatch.LengthInBytes : 0)}");
        }

        return 0;
    }

    public static int RunDumpDotNetGeneratedRegexCase(string caseId)
    {
        if (TryResolveCasePattern(caseId, out var pattern, out var options, out var origin))
        {
            WriteGeneratedRegexDump(caseId, origin, pattern, options);
            return 0;
        }

        Console.Error.WriteLine($"Case '{caseId}' was not found.");
        return 1;
    }

    public static int RunDumpDotNetGeneratedRegexPattern(string pattern, string? optionsText)
    {
        var options = ParseRegexOptions(optionsText);
        WriteGeneratedRegexDump(caseId: null, origin: "pattern", pattern, options);
        return 0;
    }

    public static int RunMeasureUtf8Case(string caseId, string? iterationsText, string? samplesText)
    {
        var benchmarkCase = Utf8RegexBenchmarkCatalog.Get(caseId);
        var context = new Utf8RegexBenchmarkContext(benchmarkCase);
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {benchmarkCase.Operation}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"Samples           : {samples}");

        MeasureUtf8CaseLane("Utf8Regex", samples, iterations, () => ExecuteUtf8(context));
        MeasureUtf8CaseLane("Utf8Compiled", samples, iterations, () => ExecuteUtf8Compiled(context));
        MeasureUtf8CaseLane("DecodeThenRegex", samples, iterations, () => ExecuteDecodeThenRegex(context));
        MeasureUtf8CaseLane("DecodeThenCompiledRegex", samples, iterations, () => ExecuteDecodeThenCompiledRegex(context));
        MeasureUtf8CaseLane("PredecodedRegex", samples, iterations, () => ExecutePredecodedRegex(context));
        MeasureUtf8CaseLane("PredecodedCompiledRegex", samples, iterations, () => ExecutePredecodedCompiledRegex(context));
        return 0;
    }

    public static int RunMeasureEnumeratorTransportCase(
        string caseId,
        string? shortIterationsText,
        string? fullIterationsText,
        string? samplesText)
    {
        var benchmarkCase = Utf8RegexBenchmarkCatalog.Get(caseId);
        if (benchmarkCase.Operation != Utf8RegexBenchmarkOperation.EnumerateMatches)
        {
            Console.Error.WriteLine($"Case '{caseId}' is not an EnumerateMatches case.");
            return 1;
        }

        var context = new Utf8RegexBenchmarkContext(benchmarkCase);
        var shortIterations = ParseIterations(shortIterationsText);
        var fullIterations = ParseIterations(fullIterationsText);
        var samples = ParseSamples(samplesText);
        var shortCharLength = Math.Min(64, context.InputString.Length);
        if (shortCharLength > 0 &&
            shortCharLength < context.InputString.Length &&
            char.IsHighSurrogate(context.InputString[shortCharLength - 1]))
        {
            shortCharLength--;
        }

        var shortInput = Encoding.UTF8.GetBytes(context.InputString[..shortCharLength]);
        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"ShortInputBytes   : {shortInput.Length}");
        Console.WriteLine($"FullInputBytes    : {context.InputBytes.Length}");
        Console.WriteLine($"ShortIterations   : {shortIterations}");
        Console.WriteLine($"FullIterations    : {fullIterations}");
        Console.WriteLine($"Samples           : {samples}");

        MeasureUtf8CaseLane("CreateOrdinary", samples, shortIterations, () => CreateUtf8Enumerator(context.Utf8Regex, shortInput));
        MeasureUtf8CaseLane("CreateCompiled", samples, shortIterations, () => CreateUtf8Enumerator(context.CompiledUtf8Regex, shortInput));
        MeasureUtf8CaseLane("FirstOrdinary", samples, shortIterations, () => FirstUtf8EnumeratorMatch(context.Utf8Regex, shortInput));
        MeasureUtf8CaseLane("FirstCompiled", samples, shortIterations, () => FirstUtf8EnumeratorMatch(context.CompiledUtf8Regex, shortInput));
        MeasureUtf8CaseLane("CopyFirstOrdinary", samples, shortIterations, () => FirstCopiedUtf8EnumeratorMatch(context.Utf8Regex, shortInput));
        MeasureUtf8CaseLane("CopyFirstCompiled", samples, shortIterations, () => FirstCopiedUtf8EnumeratorMatch(context.CompiledUtf8Regex, shortInput));
        MeasureUtf8CaseLane("ShortAllOrdinary", samples, shortIterations, () => SumUtf8Matches(context.Utf8Regex, shortInput));
        MeasureUtf8CaseLane("ShortAllCompiled", samples, shortIterations, () => SumUtf8Matches(context.CompiledUtf8Regex, shortInput));
        MeasureUtf8CaseLane("FullAllOrdinary", samples, fullIterations, () => SumUtf8Matches(context.Utf8Regex, context.InputBytes));
        MeasureUtf8CaseLane("FullAllCompiled", samples, fullIterations, () => SumUtf8Matches(context.CompiledUtf8Regex, context.InputBytes));
        return 0;
    }

    public static int RunMeasureUtf8EnumeratorCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = Utf8RegexBenchmarkCatalog.Get(caseId);
        var context = new Utf8RegexBenchmarkContext(benchmarkCase);
        var regex = context.Utf8Regex;
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {benchmarkCase.Operation}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");

        if (benchmarkCase.Operation != Utf8RegexBenchmarkOperation.EnumerateMatches ||
            regex.Inspection.ExecutionKind is not (NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals))
        {
            Console.WriteLine("Utf8Enumerator    : case is not an exact UTF-8 enumerate-matches native path");
            return 1;
        }

        Measure("PublicMoveNext", iterations, () => ExecutePublicEnumeratorMoveNextCount(regex, context.InputBytes));
        Measure("PublicIndexSum", iterations, () => ExecutePublicEnumeratorIndexSum(regex, context.InputBytes));
        Measure("ValidationOnly", iterations, () => ExecuteValidationOnly(context.InputBytes));
        Measure("BoundaryMapOnly", iterations, () => ExecuteBoundaryMapOnly(context.InputBytes));

        if (regex.Inspection.ExecutionKind == NativeExecutionKind.ExactUtf8Literal && regex.Inspection.SearchPlan.LiteralUtf8 is { Length: > 0 } literal)
        {
            Measure("SearchOnlyCount", iterations, () => ExecuteExactUtf8LiteralSearchCount(regex.Inspection.SearchPlan, literal, context.InputBytes));
            Measure("DirectEnumeratorMoveNext", iterations, () => ExecuteDirectExactUtf8LiteralEnumeratorMoveNext(regex.Inspection.SearchPlan, literal, context.InputBytes));
            Measure("DirectEnumeratorIndexSum", iterations, () => ExecuteDirectExactUtf8LiteralEnumeratorIndexSum(regex.Inspection.SearchPlan, literal, context.InputBytes));
            if (regex.Inspection.SearchPlan.LiteralSearch is { } literalSearch)
            {
                Measure("DirectSearchCount", iterations, () => ExecutePreparedSubstringCount(literalSearch, context.InputBytes));
                Measure("DirectIncrementalIndexSum", iterations, () => ExecuteExactUtf8LiteralDirectIncrementalIndexSum(literalSearch, literal, context.InputBytes, Utf8Validation.Validate(literal).Utf16Length));
                Measure("DirectBoundaryMapIndexSum", iterations, () => ExecuteExactUtf8LiteralDirectBoundaryMapIndexSum(literalSearch, literal, context.InputBytes));
            }
            Measure("BoundaryMapIndexSum", iterations, () => ExecuteExactUtf8LiteralBoundaryMapIndexSum(regex.Inspection.SearchPlan, literal, context.InputBytes));
        }
        else if (regex.Inspection.ExecutionKind == NativeExecutionKind.ExactUtf8Literals)
        {
            Measure("SearchOnlyCount", iterations, () => ExecuteExactUtf8LiteralFamilySearchCount(regex.Inspection.SearchPlan, context.InputBytes));
            Measure("DirectEnumeratorMoveNext", iterations, () => ExecuteDirectExactUtf8LiteralFamilyEnumeratorMoveNext(regex.Inspection.SearchPlan, context.InputBytes));
            Measure("DirectEnumeratorIndexSum", iterations, () => ExecuteDirectExactUtf8LiteralFamilyEnumeratorIndexSum(regex.Inspection.SearchPlan, context.InputBytes));
            Measure("DirectFamilyIncrementalIndexSum", iterations, () => ExecuteExactUtf8LiteralFamilyDirectIncrementalIndexSum(regex.Inspection.SearchPlan, context.InputBytes));
            Measure("BoundaryMapIndexSum", iterations, () => ExecuteExactUtf8LiteralFamilyBoundaryMapIndexSum(regex.Inspection.SearchPlan, context.InputBytes));
        }

        Measure("DecodeThenRegex", iterations, () => ExecuteDecodeThenRegex(context));
        Measure("PredecodedRegex", iterations, () => ExecutePredecodedRegex(context));
        return 0;
    }

    public static int RunMeasureReplicaCase(string caseId, string? iterationsText)
    {
        if (!ReplicaCountBenchmarkCase.TryResolve(caseId, out var benchmarkCase))
        {
            if (LokadPublicBenchmarkContext.GetAllCaseIds().Contains(caseId, StringComparer.Ordinal))
            {
                return RunMeasureLokadPublicCase(caseId, iterationsText);
            }

            throw new InvalidOperationException($"Benchmark case '{caseId}' was not found in replica catalogs.");
        }

        var resolvedCase = benchmarkCase!;

        if (resolvedCase.RequiresDedicatedMeasurement)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine("ReplicaMeasure    : this case requires a dedicated measurement path.");
            Console.WriteLine("SuggestedCommand  : --measure-case-deep");
            return 1;
        }

        var iterations = ParseIterations(iterationsText);

        WriteReplicaCaseHeader(resolvedCase, iterations);
        Measure("Utf8Regex", iterations, () => resolvedCase.Utf8Regex.Count(resolvedCase.InputBytes));
        Measure(resolvedCase.Source == ReplicaBenchmarkSource.DotNetPerformance ? "DotNetRegex" : "DecodeThenRegex", iterations, resolvedCase.Source == ReplicaBenchmarkSource.DotNetPerformance ? resolvedCase.CountPredecodedRegex : resolvedCase.CountDecodeThenRegex);
        if (resolvedCase.Source != ReplicaBenchmarkSource.DotNetPerformance)
        {
            Measure("PredecodedRegex", iterations, resolvedCase.CountPredecodedRegex);
        }
        return 0;
    }

    public static int RunMeasureReplicaCaseDeep(string caseId, string? iterationsText)
    {
        if (LokadPublicBenchmarkContext.GetAllCaseIds().Contains(caseId, StringComparer.Ordinal))
        {
            return RunMeasureLokadPublicCaseDeep(caseId, iterationsText);
        }

        var utf8Case = Utf8RegexBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (utf8Case is not null)
        {
            return RunMeasureUtf8Case(caseId, iterationsText, samplesText: null);
        }

        var dotNetPerformanceCase = DotNetPerformanceReplicaBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (dotNetPerformanceCase is not null)
        {
            if (dotNetPerformanceCase.Group == "bounded-repeat")
            {
                return RunMeasureBoundedRepeatReplicaCase(caseId, iterationsText);
            }

            var context = new DotNetPerformanceReplicaBenchmarkContext(dotNetPerformanceCase);
            var analysis = Utf8FrontEnd.Compile(context.Pattern, dotNetPerformanceCase.Options);
            if (Utf8CompiledEngineSelector.Select(analysis).Kind == Utf8CompiledEngineKind.ByteSafeLinear)
            {
                return RunMeasureByteSafeRebarCase(caseId, iterationsText);
            }

            if (analysis.StructuralLinearProgram.HasValue)
            {
                return RunMeasureStructuralLinearRebarCase(caseId, iterationsText);
            }

            if (analysis.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals)
            {
                return RunMeasureCompiledLiteralFamilyReplicaCase(caseId, iterationsText);
            }

            if (analysis.ExecutionKind == NativeExecutionKind.ExactUtf8Literals)
            {
                return RunMeasureExactLiteralFamilyReplicaCase(caseId, iterationsText);
            }

            return RunMeasureReplicaCase(caseId, iterationsText);
        }

        var lokadCodeCase = LokadReplicaCodeBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (lokadCodeCase is not null)
        {
            var context = new LokadReplicaCodeBenchmarkContext(lokadCodeCase);
            var analysis = Utf8FrontEnd.Compile(context.CompiledPattern, lokadCodeCase.Options);
            if (analysis.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
                analysis.SearchPlan.PreparedSearcher.HasValue)
            {
                return RunMeasureIdentifierFamilyLokadCodeCase(caseId, iterationsText);
            }

            if (analysis.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiStructuralFamily)
            {
                return RunMeasureStructuralFamilyLokadCodeCase(caseId, iterationsText);
            }

            if (analysis.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals)
            {
                return RunMeasureCompiledLiteralFamilyReplicaCase(caseId, iterationsText);
            }

            if (analysis.ExecutionKind == NativeExecutionKind.ExactUtf8Literals)
            {
                return RunMeasureExactLiteralFamilyReplicaCase(caseId, iterationsText);
            }

            return RunMeasureReplicaCase(caseId, iterationsText);
        }

        var lokadScriptCase = LokadReplicaScriptBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (lokadScriptCase is not null)
        {
            if (lokadScriptCase.Model == LokadReplicaScriptBenchmarkModel.Count)
            {
                return RunMeasureLokadScriptCountCase(caseId, iterationsText);
            }

            var context = new LokadReplicaScriptBenchmarkContext(lokadScriptCase);
            if (context.Utf8Regex.Inspection.CompiledEngineKind == Utf8CompiledEngineKind.ByteSafeLinear)
            {
                return RunMeasureLokadScriptByteSafePrefixCase(caseId, iterationsText);
            }

            return RunMeasureLokadScriptPrefixCase(caseId, iterationsText);
        }

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine("MeasureDeep       : case was not found in any benchmark catalog.");
        return 1;
    }

    public static int RunMeasureCompiledFallbackReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        if (benchmarkCase.RequiresDedicatedMeasurement)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine("CompiledFallback  : this case requires a dedicated measurement path.");
            return 1;
        }

        var iterations = ParseIterations(iterationsText);
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options | RegexOptions.Compiled);
        var baselineDiagnostics = benchmarkCase.Utf8Regex.CollectCountDiagnostics(benchmarkCase.InputBytes);
        var compiledDiagnostics = compiledUtf8Regex.CollectCountDiagnostics(benchmarkCase.InputBytes);

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"BaselineEngine    : {benchmarkCase.Utf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        Measure("ValidationOnly", iterations, () => ExecuteValidationOnly(benchmarkCase.InputBytes));
        Measure("WellFormedOnly", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(benchmarkCase.InputBytes);
            return benchmarkCase.InputBytes.Length;
        });
        Measure("BaselineDirect", iterations, () => benchmarkCase.Utf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("CompiledDirect", iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(benchmarkCase.InputBytes));
        Measure(benchmarkCase.Source == ReplicaBenchmarkSource.DotNetPerformance ? "DotNetRegex" : "DecodeThenRegex", iterations, benchmarkCase.Source == ReplicaBenchmarkSource.DotNetPerformance ? benchmarkCase.CountPredecodedRegex : benchmarkCase.CountDecodeThenRegex);
        Measure("CompiledRegex", iterations, benchmarkCase.CountPredecodedCompiledRegex);
        if (benchmarkCase.Source == ReplicaBenchmarkSource.Lokad)
        {
            Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
        }

        return 0;
    }

    public static int RunMeasureLiteralSearchReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        var iterations = ParseIterations(iterationsText);

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
        Console.WriteLine($"PreparedSearcher  : {regex.Inspection.SearchPlan.PreparedSearcher.Kind}");

        switch (regex.Inspection.ExecutionKind)
        {
            case NativeExecutionKind.ExactAsciiLiteral:
            case NativeExecutionKind.ExactUtf8Literal:
                Measure("PrimitiveCount", iterations, () => ExecuteExactLiteralPrimitiveCount(regex.Inspection.SearchPlan.LiteralUtf8 ?? [], benchmarkCase.InputBytes));
                if (regex.Inspection.SearchPlan.LiteralSearch is { } exactSearch)
                {
                    Measure("PreparedCount", iterations, () => ExecutePreparedSubstringCount(exactSearch, benchmarkCase.InputBytes));
                }
                break;

            case NativeExecutionKind.AsciiLiteralIgnoreCase:
                Measure("PrimitiveCount", iterations, () => ExecuteIgnoreCaseLiteralPrimitiveCount(regex.Inspection.SearchPlan.LiteralUtf8 ?? [], benchmarkCase.InputBytes));
                if (regex.Inspection.SearchPlan.LiteralSearch is { } ignoreCaseSearch)
                {
                    Console.WriteLine($"IgnoreCaseTier    : {ignoreCaseSearch.IgnoreCaseTier}");
                    Measure("PreparedCount", iterations, () => ExecutePreparedSubstringCount(ignoreCaseSearch, benchmarkCase.InputBytes));
                    Measure("PreparedCandidates", iterations, () => ExecutePreparedSubstringCandidateCount(ignoreCaseSearch, benchmarkCase.InputBytes));
                    Measure("PreparedVerifications", iterations, () => ExecutePreparedSubstringVerificationCount(ignoreCaseSearch, benchmarkCase.InputBytes));
                    if (ignoreCaseSearch.Length > 10)
                    {
                        Measure("VectorAnchoredCount", iterations, () => ExecutePreparedSubstringCountWithTier(ignoreCaseSearch, benchmarkCase.InputBytes, PreparedIgnoreCaseSearchTier.VectorAnchored));
                        Measure("PackedPairCount", iterations, () => ExecutePreparedSubstringCountWithTier(ignoreCaseSearch, benchmarkCase.InputBytes, PreparedIgnoreCaseSearchTier.PackedPair));
                        Measure("VectorAnchoredCandidates", iterations, () => ExecutePreparedSubstringCandidateCountWithTier(ignoreCaseSearch, benchmarkCase.InputBytes, PreparedIgnoreCaseSearchTier.VectorAnchored));
                        Measure("PackedPairCandidates", iterations, () => ExecutePreparedSubstringCandidateCountWithTier(ignoreCaseSearch, benchmarkCase.InputBytes, PreparedIgnoreCaseSearchTier.PackedPair));
                        Measure("VectorAnchoredVerifications", iterations, () => ExecutePreparedSubstringVerificationCountWithTier(ignoreCaseSearch, benchmarkCase.InputBytes, PreparedIgnoreCaseSearchTier.VectorAnchored));
                        Measure("PackedPairVerifications", iterations, () => ExecutePreparedSubstringVerificationCountWithTier(ignoreCaseSearch, benchmarkCase.InputBytes, PreparedIgnoreCaseSearchTier.PackedPair));
                    }
                }
                break;

            case NativeExecutionKind.ExactUtf8Literals:
            case NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals:
                Measure("PreparedCount", iterations, () => ExecutePreparedSearcherCount(regex.Inspection.SearchPlan.PreparedSearcher, benchmarkCase.InputBytes));
                break;

            default:
                Console.WriteLine("LiteralSearch     : case is not on a native literal engine");
                return 1;
        }

        Measure("ValidationOnly", iterations, () => Utf8Validation.Validate(benchmarkCase.InputBytes).Utf16Length);
        Measure("WellFormedOnly", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(benchmarkCase.InputBytes);
            return benchmarkCase.InputBytes.Length;
        });
        Measure("RequiredPrefilterOnly", iterations, () => benchmarkCase.Utf8Regex.Inspection.DebugRejectsByRequiredPrefilter(benchmarkCase.InputBytes) ? 1 : 0);
        Measure("CompiledCount", iterations, () => benchmarkCase.Utf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure(benchmarkCase.Source == ReplicaBenchmarkSource.DotNetPerformance ? "DotNetRegex" : "DecodeThenRegex", iterations, benchmarkCase.Source == ReplicaBenchmarkSource.DotNetPerformance ? benchmarkCase.CountPredecodedRegex : benchmarkCase.CountDecodeThenRegex);
        if (benchmarkCase.Source == ReplicaBenchmarkSource.Lokad)
        {
            Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
        }
        return 0;
    }

    public static int RunMeasureCompiledLiteralFamilyReplicaCase(
        string caseId,
        string? iterationsText,
        string? samplesText = null)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var baselineRegex = benchmarkCase.Utf8Regex;
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options | RegexOptions.Compiled);
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);

        if (!IsLiteralFamilyExecutionKind(baselineRegex.Inspection.ExecutionKind) ||
            !IsLiteralFamilyExecutionKind(compiledUtf8Regex.Inspection.ExecutionKind))
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"BaselineKind      : {baselineRegex.Inspection.ExecutionKind}");
            Console.WriteLine($"CompiledKind      : {compiledUtf8Regex.Inspection.ExecutionKind}");
            Console.WriteLine("LiteralFamily     : case is not on a supported literal family for both routes");
            return 1;
        }

        var baselineDiagnostics = baselineRegex.CollectCountDiagnostics(benchmarkCase.InputBytes);
        var compiledDiagnostics = compiledUtf8Regex.CollectCountDiagnostics(benchmarkCase.InputBytes);
        var baselinePlan = baselineRegex.Inspection.SearchPlan;
        var compiledPlan = compiledUtf8Regex.Inspection.SearchPlan;

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"BaselineEngine    : {baselineRegex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"BaselinePrepared  : {baselinePlan.PreparedSearcher.Kind}");
        Console.WriteLine($"CompiledPrepared  : {compiledPlan.PreparedSearcher.Kind}");
        Console.WriteLine($"LiteralCount      : {baselinePlan.MultiLiteralSearch.Literals.Length}");
        Console.WriteLine($"Samples           : {samples}");
        if (baselinePlan.AlternateIgnoreCaseLiteralSearch is { } ignoreCaseSearch)
        {
            var candidates = CountIgnoreCaseLiteralFamilyCandidates(ignoreCaseSearch, benchmarkCase.InputBytes);
            Console.WriteLine($"FirstByteCandidates: {candidates.FirstByte}");
            Console.WriteLine($"PrefixCandidates  : {candidates.CorrelatedPrefix}");
            Console.WriteLine($"PrefixLength      : {candidates.PrefixLength}");
        }

        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        Measure("ValidationOnly", samples, iterations, () => ExecuteValidationOnly(benchmarkCase.InputBytes));
        Measure("WellFormedOnly", samples, iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(benchmarkCase.InputBytes);
            return benchmarkCase.InputBytes.Length;
        });
        Measure("BaselinePreparedBoundaryCount", samples, iterations, () => ExecutePreparedSearcherCountWithBoundaries(baselinePlan.PreparedSearcher, baselinePlan, benchmarkCase.InputBytes));
        Measure("CompiledPreparedBoundaryCount", samples, iterations, () => ExecutePreparedSearcherCountWithBoundaries(compiledPlan.PreparedSearcher, compiledPlan, benchmarkCase.InputBytes));
        Measure("BaselineDirect", samples, iterations, () => baselineRegex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("CompiledDirect", samples, iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("Utf8Regex", samples, iterations, () => baselineRegex.Count(benchmarkCase.InputBytes));
        Measure("Utf8Compiled", samples, iterations, () => compiledUtf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", samples, iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", samples, iterations, benchmarkCase.CountDecodeThenCompiledRegex);
        Measure("PredecodedRegex", samples, iterations, benchmarkCase.CountPredecodedRegex);
        Measure("CompiledRegex", samples, iterations, benchmarkCase.CountPredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureCompiledConstructionReplicaCase(
        string caseId,
        string? instancesText,
        string? samplesText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var instanceCount = string.IsNullOrWhiteSpace(instancesText)
            ? 4
            : Math.Clamp(ParseIterations(instancesText), 1, 16);
        var samples = Math.Clamp(ParseSamples(samplesText), 1, 9);
        var ordinaryOptions = benchmarkCase.Options & ~RegexOptions.Compiled;
        var compiledOptions = benchmarkCase.Options | RegexOptions.Compiled;
        var decoded = Encoding.UTF8.GetString(benchmarkCase.InputBytes);

        Console.WriteLine($"CaseId            : {benchmarkCase.Id}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"BaseOptions       : {ordinaryOptions}");
        Console.WriteLine($"Instances         : {instanceCount}");
        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"InputBytes        : {benchmarkCase.InputBytes.Length}");

        // Prime shared JIT and dynamic backends outside every timed sample.
        // The first-call rows below intentionally isolate per-instance work;
        // process-cold qualification requires separate process launches.
        const int sharedTierPrimeCalls = 64;
        var warmUtf8Ordinary = new Utf8Regex(benchmarkCase.Pattern, ordinaryOptions);
        var warmUtf8Compiled = new Utf8Regex(benchmarkCase.Pattern, compiledOptions);
        var warmRegexOrdinary = new Regex(benchmarkCase.Pattern, ordinaryOptions, Regex.InfiniteMatchTimeout);
        var warmRegexCompiled = new Regex(benchmarkCase.Pattern, compiledOptions, Regex.InfiniteMatchTimeout);
        var primeSink = 0;
        for (var i = 0; i < sharedTierPrimeCalls; i++)
        {
            primeSink ^= warmUtf8Ordinary.Count(benchmarkCase.InputBytes);
            primeSink ^= warmUtf8Compiled.Count(benchmarkCase.InputBytes);
            primeSink ^= warmRegexOrdinary.Count(decoded);
            primeSink ^= warmRegexCompiled.Count(decoded);
        }

        GC.KeepAlive(primeSink);
        Console.WriteLine($"SharedTierPrimeCalls: {sharedTierPrimeCalls}");

        var effectiveOptions = Utf8RegexSyntax.NormalizeNonSemanticOptions(ordinaryOptions);
        var phasePreparedRegex = Utf8FrontEnd.Compile(benchmarkCase.Pattern, effectiveOptions);
        var phaseOrdinaryEngine = Utf8CompiledEngineSelector.Select(phasePreparedRegex, preferCompiled: false);
        var phaseCompiledEngine = Utf8CompiledEngineSelector.Select(phasePreparedRegex, preferCompiled: true);
        var phaseOrdinaryVerifier = Utf8VerifierRuntime.Create(
            phasePreparedRegex,
            benchmarkCase.Pattern,
            ordinaryOptions,
            Regex.InfiniteMatchTimeout);
        var phaseCompiledVerifier = Utf8VerifierRuntime.Create(
            phasePreparedRegex,
            benchmarkCase.Pattern,
            compiledOptions,
            Regex.InfiniteMatchTimeout);
        var phaseFallbackRegex = phaseOrdinaryVerifier.FallbackCandidateVerifier.FallbackRegex;

        if (phasePreparedRegex.SearchPlan.AlternateLiteralsUtf8 is { Length: > 0 } exactLiterals &&
            phasePreparedRegex.SearchPlan.Kind is Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals)
        {
            MeasureFixedConstruction("ExactLiteralSearchData", samples, instanceCount,
                () => AsciiSearch.CreateExactLiteralSearchData(exactLiterals).Buckets);
        }

        MeasureFixedConstruction("Utf8FrontEndCompile", samples, instanceCount,
            () => Utf8FrontEnd.Compile(benchmarkCase.Pattern, effectiveOptions));
        MeasureFixedConstruction("Utf8OrdinaryVerifier", samples, instanceCount,
            () => Utf8VerifierRuntime.Create(
                phasePreparedRegex,
                benchmarkCase.Pattern,
                ordinaryOptions,
                Regex.InfiniteMatchTimeout));
        MeasureFixedConstruction("Utf8CompiledVerifier", samples, instanceCount,
            () => Utf8VerifierRuntime.Create(
                phasePreparedRegex,
                benchmarkCase.Pattern,
                compiledOptions,
                Regex.InfiniteMatchTimeout));
        MeasureFixedConstruction("Utf8OrdinaryRuntime", samples, instanceCount,
            () => Utf8CompiledEngineRuntime.Create(
                phaseOrdinaryEngine,
                phasePreparedRegex,
                phaseOrdinaryVerifier,
                ordinaryOptions));
        MeasureFixedConstruction("Utf8CompiledRuntime", samples, instanceCount,
            () => Utf8CompiledEngineRuntime.Create(
                phaseCompiledEngine,
                phasePreparedRegex,
                phaseCompiledVerifier,
                compiledOptions));
        MeasureFixedConstruction("Utf8OrdinaryProgram", samples, instanceCount,
            () => Utf8RegexProgram.Compile(
                benchmarkCase.Pattern,
                ordinaryOptions,
                Regex.InfiniteMatchTimeout));
        MeasureFixedConstruction("Utf8CompiledProgram", samples, instanceCount,
            () => Utf8RegexProgram.Compile(
                benchmarkCase.Pattern,
                compiledOptions,
                Regex.InfiniteMatchTimeout));
        MeasureFixedConstruction("RegexOrdinaryAnchored", samples, instanceCount,
            () => new Regex(
                @"\G(?:" + benchmarkCase.Pattern + ")",
                ordinaryOptions,
                Regex.InfiniteMatchTimeout));
        MeasureFixedConstruction("RegexCompiledAnchored", samples, instanceCount,
            () => new Regex(
                @"\G(?:" + benchmarkCase.Pattern + ")",
                compiledOptions,
                Regex.InfiniteMatchTimeout));
        MeasureFixedConstruction("RegexGroupNumbers", samples, instanceCount,
            phaseFallbackRegex.GetGroupNumbers);
        MeasureFixedConstruction("RegexGroupNames", samples, instanceCount,
            phaseFallbackRegex.GetGroupNames);

        MeasureFixedConstruction("Utf8OrdinaryConstruct", samples, instanceCount,
            () => new Utf8Regex(benchmarkCase.Pattern, ordinaryOptions));
        MeasureFixedConstruction("Utf8CompiledConstruct", samples, instanceCount,
            () => new Utf8Regex(benchmarkCase.Pattern, compiledOptions));
        MeasureFixedConstruction("RegexOrdinaryConstruct", samples, instanceCount,
            () => new Regex(benchmarkCase.Pattern, ordinaryOptions, Regex.InfiniteMatchTimeout));
        MeasureFixedConstruction("RegexCompiledConstruct", samples, instanceCount,
            () => new Regex(benchmarkCase.Pattern, compiledOptions, Regex.InfiniteMatchTimeout));

        MeasureFixedFirstCall("Utf8OrdinaryFirstCountPrimed", samples, instanceCount,
            () => new Utf8Regex(benchmarkCase.Pattern, ordinaryOptions),
            regex => regex.Count(benchmarkCase.InputBytes));
        MeasureFixedFirstCall("Utf8CompiledFirstCountPrimed", samples, instanceCount,
            () => new Utf8Regex(benchmarkCase.Pattern, compiledOptions),
            regex => regex.Count(benchmarkCase.InputBytes));
        MeasureFixedFirstCall("RegexOrdinaryFirstCountPrimed", samples, instanceCount,
            () => new Regex(benchmarkCase.Pattern, ordinaryOptions, Regex.InfiniteMatchTimeout),
            regex => regex.Count(decoded));
        MeasureFixedFirstCall("RegexCompiledFirstCountPrimed", samples, instanceCount,
            () => new Regex(benchmarkCase.Pattern, compiledOptions, Regex.InfiniteMatchTimeout),
            regex => regex.Count(decoded));
        return 0;
    }

    public static int RunMeasureSmallAsciiLiteralFamilyPrimitiveCase(string caseId, string? iterationsText)
    {
        try
        {
            var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
            var regex = benchmarkCase.Utf8Regex;
            var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options | RegexOptions.Compiled);
            var iterations = ParseIterations(iterationsText);

            if (regex.Inspection.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
                regex.Inspection.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals ||
                !PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var primitive))
            {
                Console.WriteLine($"CaseId            : {caseId}");
                Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
                Console.WriteLine("SmallAsciiPrimitive: case is not eligible for the small ASCII literal-family primitive");
                return 1;
            }

            WriteReplicaCaseHeader(benchmarkCase, iterations);
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine($"LiteralCount      : {literals.Length}");
            Console.WriteLine($"ShortestLength    : {primitive.ShortestLength}");

            Measure("PrimitiveScalar", iterations, () => ExecuteSmallAsciiLiteralFamilyPrimitiveCountScalar(primitive, benchmarkCase.InputBytes));
            Measure("PrimitiveSimd", iterations, () => ExecuteSmallAsciiLiteralFamilyPrimitiveCountSimd(primitive, benchmarkCase.InputBytes));
            Measure("PreparedBoundaryCount", iterations, () => ExecutePreparedSearcherCountWithBoundaries(regex.Inspection.SearchPlan.PreparedSearcher, regex.Inspection.SearchPlan, benchmarkCase.InputBytes));
            Measure("CompiledDirect", iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
            Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(benchmarkCase.InputBytes));
            Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
            Measure("CompiledRegex", iterations, benchmarkCase.CountPredecodedCompiledRegex);
            return 0;
        }
        catch (InvalidOperationException)
        {
            var context = new LokadPublicBenchmarkContext(caseId);
            var regex = context.Utf8Regex;
            var compiledUtf8Regex = context.CompiledUtf8Regex;
            var iterations = ParseIterations(iterationsText);

            if (context.Operation is not (LokadPublicBenchmarkOperation.Count or LokadPublicBenchmarkOperation.Split) ||
                regex.Inspection.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
                regex.Inspection.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals ||
                !PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var primitive))
            {
                Console.WriteLine($"CaseId            : {caseId}");
                Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
                Console.WriteLine("SmallAsciiPrimitive: case is not eligible for the small ASCII literal-family primitive");
                return 1;
            }

            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Operation         : {context.Operation}");
            Console.WriteLine($"Pattern           : {context.Pattern}");
            Console.WriteLine($"Options           : {context.Options}");
            Console.WriteLine($"Iterations        : {iterations}");
            Console.WriteLine($"InputChars        : {context.InputString.Length}");
            Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine($"LiteralCount      : {literals.Length}");
            Console.WriteLine($"ShortestLength    : {primitive.ShortestLength}");

            Measure("PrimitiveScalar", iterations, () => ExecuteSmallAsciiLiteralFamilyPrimitiveCountScalar(primitive, context.InputBytes));
            Measure("PrimitiveSimd", iterations, () => ExecuteSmallAsciiLiteralFamilyPrimitiveCountSimd(primitive, context.InputBytes));
            Measure("PreparedBoundaryCount", iterations, () => ExecutePreparedSearcherCountWithBoundaries(regex.Inspection.SearchPlan.PreparedSearcher, regex.Inspection.SearchPlan, context.InputBytes));
            Measure("CompiledDirect", iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
            Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
            Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
            Measure("CompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
            return 0;
        }
    }

    public static int RunMeasureSmallAsciiLiteralFamilyFirstMatchCase(string caseId, string? iterationsText)
    {
        var context = new LokadPublicBenchmarkContext(caseId);
        var regex = context.Utf8Regex;
        var iterations = ParseIterations(iterationsText);

        if (context.Operation != LokadPublicBenchmarkOperation.Match ||
            regex.Inspection.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            regex.Inspection.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals ||
            !PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var primitive))
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine("SmallAsciiFirstMatch: case is not eligible for the small ASCII literal-family primitive");
            return 1;
        }

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {context.Operation}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {context.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputString.Length}");
        Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
        Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
        Console.WriteLine($"LiteralCount      : {literals.Length}");
        Console.WriteLine($"ShortestLength    : {primitive.ShortestLength}");

        Measure("PrimitiveFirstMatch", iterations, () => ExecuteSmallAsciiLiteralFamilyPrimitiveFirstMatch(primitive, context.InputBytes));
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("CompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureIgnoreCasePrimitiveReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        if (regex.Inspection.ExecutionKind != NativeExecutionKind.AsciiLiteralIgnoreCase)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine("IgnoreCasePrimitive: case is not on AsciiLiteralIgnoreCase");
            return 1;
        }

        var iterations = ParseIterations(iterationsText);
        WriteReplicaCaseHeader(benchmarkCase, iterations);
        var literal = regex.Inspection.SearchPlan.LiteralUtf8 ?? [];
        Console.WriteLine($"LiteralLength     : {literal.Length}");
        if (literal.Length > 1 && AsciiSearch.TryGetDotNetLikeAsciiAnchorSelection(literal, ignoreCase: true, out var anchors))
        {
            Console.WriteLine($"DotNetAnchor2     : {anchors.Anchor2}");
            Console.WriteLine($"DotNetAnchor3     : {anchors.Anchor3}");
        }
        Measure("PrimitiveCount", iterations, () => ExecuteIgnoreCaseLiteralPrimitiveCount(literal, benchmarkCase.InputBytes));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("Utf8Direct", iterations, () => benchmarkCase.Utf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Utf8Pattern, benchmarkCase.Options | RegexOptions.Compiled);
        Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(benchmarkCase.InputBytes));
        Measure("Utf8CompiledDirect", iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, benchmarkCase.CountPredecodedCompiledRegex);
        if (literal.Length is >= 2 and <= 16)
        {
            for (var compareIndex = 1; compareIndex < literal.Length; compareIndex++)
            {
                var capturedIndex = compareIndex;
                Measure($"PrimitiveCompare[{capturedIndex}]", iterations, () => ExecuteIgnoreCaseLiteralPrimitiveCountWithCompareIndex(literal, benchmarkCase.InputBytes, capturedIndex));
            }
        }

        return 0;
    }

    public static int RunEmitReadmeBenchmarkMarkdown(string? sectionsText, string? iterationsText, string? samplesText)
    {
        _ = iterationsText;
        _ = samplesText;
        var snapshot = LoadReadmeBenchmarkSnapshot();
        foreach (var section in ParseReadmeSections(sectionsText))
        {
            Console.Write(BuildReadmeSectionMarkdownFromSnapshot(section, snapshot));
            Console.WriteLine();
        }

        return 0;
    }

    public static int RunRefreshReadmeBenchmarks(string? sectionsText, string? iterationsText, string? samplesText)
    {
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var snapshot = LoadReadmeBenchmarkSnapshot();

        foreach (var section in ParseReadmeSections(sectionsText))
        {
            RefreshReadmeSnapshotSection(snapshot, section, iterations, samples);
        }

        SaveReadmeBenchmarkSnapshot(snapshot);
        RewriteReadmeFromSnapshot(snapshot);
        Console.WriteLine($"Updated README sections: {string.Join(", ", ParseReadmeSections(sectionsText).Select(static s => s.ToString()))}");
        return 0;
    }

    public static int RunMigrateReadmeBenchmarkJson()
    {
        var readme = File.ReadAllText(FindRepoFile("README.md"), Encoding.UTF8);
        var snapshot = ParseReadmeBenchmarkSnapshot(readme);
        SaveReadmeBenchmarkSnapshot(snapshot);
        RewriteReadmeFromSnapshot(snapshot);
        Console.WriteLine($"Wrote {ReadmeBenchmarkSnapshotFileName} from current README rows and regenerated README from JSON.");
        return 0;
    }

    public static int RunRefreshReadmeCase(string caseId, string? iterationsText, string? samplesText)
    {
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var snapshot = LoadReadmeBenchmarkSnapshot();

        foreach (var target in GetReadmeTargetsForCase(caseId))
        {
            var measurement = MeasureReadmeSnapshotCase(target, caseId, iterations, samples);
            GetOrAddSnapshotSection(snapshot, target).Cases[caseId] = ReadmeCaseMeasurementJson.FromMeasurement(measurement);
        }

        SaveReadmeBenchmarkSnapshot(snapshot);
        RewriteReadmeFromSnapshot(snapshot);
        Console.WriteLine($"Updated README case: {caseId}");
        return 0;
    }

    public static int RunMeasureReadmeCase(string caseId, string? iterationsText, string? samplesText)
    {
        var samples = ParseSamples(samplesText);

        var lokadScriptCase = LokadReplicaScriptBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (lokadScriptCase is not null)
        {
            var context = new LokadReplicaScriptBenchmarkContext(lokadScriptCase);
            var iterations = ParseReadmeLokadScriptIterations(lokadScriptCase, iterationsText);
            var scriptRow = MeasureReadmeCase(
                iterations,
                samples,
                context.ExecuteUtf8Regex,
                context.ExecuteUtf8Compiled,
                context.ExecutePredecodedRegex,
                context.ExecutePredecodedCompiledRegex,
                context.ExecuteDecodeThenRegex,
                context.ExecuteDecodeThenCompiledRegex);
            Console.WriteLine(FormatReadmeCaseRow(scriptRow));
            return 0;
        }

        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Utf8Pattern, benchmarkCase.Options | RegexOptions.Compiled);
        var replicaIterations = ParseReadmeReplicaIterations(benchmarkCase, iterationsText);
        var row = MeasureReadmeCase(
            replicaIterations,
            samples,
            () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes),
            () => compiledUtf8Regex.Count(benchmarkCase.InputBytes),
            benchmarkCase.CountPredecodedRegex,
            benchmarkCase.CountPredecodedCompiledRegex,
            benchmarkCase.CountDecodeThenRegex,
            benchmarkCase.CountDecodeThenCompiledRegex);
        Console.WriteLine(FormatReadmeCaseRow(row));
        return 0;
    }

    public static int RunMeasureReadmePublicCase(string caseId, string? iterationsText, string? samplesText)
    {
        var samples = ParseSamples(samplesText);
        var context = new LokadPublicBenchmarkContext(caseId);
        var iterations = ParseReadmePublicIterations(context, iterationsText);
        var row = MeasureReadmeCase(
            iterations,
            samples,
            context.ExecuteUtf8Regex,
            context.ExecuteUtf8Compiled,
            context.ExecutePredecodedRegex,
            context.ExecutePredecodedCompiledRegex,
            context.ExecuteDecodeThenRegex,
            context.ExecuteDecodeThenCompiledRegex);
        Console.WriteLine(FormatReadmeCaseRow(row));
        return 0;
    }

    private const string ReadmeBenchmarkSnapshotFileName = "README.Benchmarks.json";

    private static ReadmeBenchmarkSnapshot LoadReadmeBenchmarkSnapshot()
    {
        var path = FindRepoFile(ReadmeBenchmarkSnapshotFileName);
        var json = File.ReadAllText(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<ReadmeBenchmarkSnapshot>(json, ReadmeBenchmarkSnapshotJsonOptions) ?? throw new InvalidOperationException($"Could not deserialize {ReadmeBenchmarkSnapshotFileName}.");
    }

    private static void SaveReadmeBenchmarkSnapshot(ReadmeBenchmarkSnapshot snapshot)
    {
        snapshot.SchemaVersion = 2;
        var path = Path.Combine(Path.GetDirectoryName(FindRepoFile("README.md"))!, ReadmeBenchmarkSnapshotFileName);
        var json = JsonSerializer.Serialize(snapshot, ReadmeBenchmarkSnapshotJsonOptions);
        File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string FindRepoFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate '{fileName}' by walking upward from '{AppContext.BaseDirectory}'.", fileName);
    }

    private enum ReadmeBenchmarkSection : byte
    {
        DotNetPerformance = 0,
        DotNetPerformanceCompiled = 1,
        Lokad = 2,
        LokadCompiled = 3,
    }

    private static IReadOnlyList<ReadmeBenchmarkSection> ParseReadmeSections(string? sectionsText)
    {
        if (string.IsNullOrWhiteSpace(sectionsText))
        {
            return
            [
                ReadmeBenchmarkSection.DotNetPerformance,
                ReadmeBenchmarkSection.DotNetPerformanceCompiled,
                ReadmeBenchmarkSection.Lokad,
                ReadmeBenchmarkSection.LokadCompiled,
            ];
        }

        if (char.IsDigit(sectionsText[0]))
        {
            return
            [
                ReadmeBenchmarkSection.DotNetPerformance,
                ReadmeBenchmarkSection.DotNetPerformanceCompiled,
                ReadmeBenchmarkSection.Lokad,
                ReadmeBenchmarkSection.LokadCompiled,
            ];
        }

        var sections = new List<ReadmeBenchmarkSection>();
        foreach (var token in sectionsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            sections.Add(token.ToLowerInvariant() switch
            {
                "dotnet-performance" => ReadmeBenchmarkSection.DotNetPerformance,
                "dotnet-performance-compiled" => ReadmeBenchmarkSection.DotNetPerformanceCompiled,
                "lokad" => ReadmeBenchmarkSection.Lokad,
                "lokad-compiled" => ReadmeBenchmarkSection.LokadCompiled,
                "all" => throw new InvalidOperationException("Use an empty section argument to refresh all README benchmark sections."),
                _ => throw new InvalidOperationException($"Unknown README benchmark section '{token}'. Expected dotnet-performance, dotnet-performance-compiled, lokad, or lokad-compiled."),
            });
        }

        return sections.Distinct().ToArray();
    }

    private static void RefreshReadmeSnapshotSection(ReadmeBenchmarkSnapshot snapshot, ReadmeBenchmarkSection section, int iterations, int samples)
    {
        var sectionSnapshot = GetOrAddSnapshotSection(snapshot, section);
        sectionSnapshot.Cases.Clear();

        switch (section)
        {
            case ReadmeBenchmarkSection.DotNetPerformance:
            case ReadmeBenchmarkSection.DotNetPerformanceCompiled:
                foreach (var benchmarkCase in ReplicaCountBenchmarkCase.GetAll(ReplicaBenchmarkSource.DotNetPerformance))
                {
                    sectionSnapshot.Cases[benchmarkCase.Id] = ReadmeCaseMeasurementJson.FromMeasurement(MeasureReadmeCaseOutOfProcess("--measure-readme-case", benchmarkCase.Id, iterations, samples));
                }

                foreach (var caseId in LokadPublicBenchmarkContext.GetAllCaseIds())
                {
                    sectionSnapshot.Cases[caseId] = ReadmeCaseMeasurementJson.FromMeasurement(MeasureReadmeCaseOutOfProcess("--measure-readme-public-case", caseId, iterations, samples));
                }
                break;

            case ReadmeBenchmarkSection.Lokad:
            case ReadmeBenchmarkSection.LokadCompiled:
                foreach (var benchmarkCase in ReplicaCountBenchmarkCase.GetAll(ReplicaBenchmarkSource.Lokad))
                {
                    sectionSnapshot.Cases[benchmarkCase.Id] = ReadmeCaseMeasurementJson.FromMeasurement(MeasureReadmeCaseOutOfProcess("--measure-readme-case", benchmarkCase.Id, iterations, samples));
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }
    }

    private static ReadmeCaseMeasurement MeasureReadmeSnapshotCase(ReadmeBenchmarkSection section, string caseId, int iterations, int samples)
    {
        _ = section;
        if (LokadPublicBenchmarkContext.GetAllCaseIds().Contains(caseId, StringComparer.Ordinal))
        {
            return MeasureReadmeCaseOutOfProcess("--measure-readme-public-case", caseId, iterations, samples);
        }

        _ = ReplicaCountBenchmarkCase.Resolve(caseId);
        return MeasureReadmeCaseOutOfProcess("--measure-readme-case", caseId, iterations, samples);
    }

    private static string GetReadmeMarkerName(ReadmeBenchmarkSection section)
    {
        return section switch
        {
            ReadmeBenchmarkSection.DotNetPerformance => "DOTNET_PERFORMANCE",
            ReadmeBenchmarkSection.DotNetPerformanceCompiled => "DOTNET_PERFORMANCE_COMPILED",
            ReadmeBenchmarkSection.Lokad => "LOKAD",
            ReadmeBenchmarkSection.LokadCompiled => "LOKAD_COMPILED",
            _ => throw new ArgumentOutOfRangeException(nameof(section)),
        };
    }

    private static string GetReadmeSectionToken(ReadmeBenchmarkSection section)
    {
        return section switch
        {
            ReadmeBenchmarkSection.DotNetPerformance => "dotnet-performance",
            ReadmeBenchmarkSection.DotNetPerformanceCompiled => "dotnet-performance-compiled",
            ReadmeBenchmarkSection.Lokad => "lokad",
            ReadmeBenchmarkSection.LokadCompiled => "lokad-compiled",
            _ => throw new ArgumentOutOfRangeException(nameof(section)),
        };
    }

    private static void WriteReadmeDefaultTableHeader(StringWriter writer)
    {
        writer.WriteLine("| Case | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |");
        writer.WriteLine("|---|---:|---:|---:|");
    }

    private static void WriteReadmeCompiledTableHeader(StringWriter writer)
    {
        writer.WriteLine("| Case | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |");
        writer.WriteLine("|---|---:|---:|---:|");
    }

    private static void WriteReadmePublicDefaultTableHeader(StringWriter writer)
    {
        writer.WriteLine("| Case | Operation | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |");
        writer.WriteLine("|---|---|---:|---:|---:|");
    }

    private static void WriteReadmePublicCompiledTableHeader(StringWriter writer)
    {
        writer.WriteLine("| Case | Operation | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |");
        writer.WriteLine("|---|---|---:|---:|---:|");
    }

    private static void WriteReadmePlainDefaultCaseRow(StringWriter writer, string caseId, ReadmeCaseMeasurement row)
    {
        writer.WriteLine($"| `{caseId}` | {FormatMicros(row.Utf8Regex)} | {FormatMicros(row.PredecodedRegex)} | {FormatMicros(row.DecodeThenRegex)} |");
    }

    private static void WriteReadmePlainCompiledCaseRow(StringWriter writer, string caseId, ReadmeCaseMeasurement row)
    {
        writer.WriteLine($"| `{caseId}` | {FormatMicros(row.Utf8Compiled)} | {FormatMicros(row.CompiledRegex)} | {FormatMicros(row.DecodeThenCompiledRegex)} |");
    }

    private static void WriteReadmePlainPublicDefaultCaseRow(StringWriter writer, string caseId, ReadmeCaseMeasurement row, LokadPublicBenchmarkOperation operation)
    {
        writer.WriteLine($"| `{caseId}` | `{operation}` | {FormatMicros(row.Utf8Regex)} | {FormatMicros(row.PredecodedRegex)} | {FormatMicros(row.DecodeThenRegex)} |");
    }

    private static void WriteReadmePlainPublicCompiledCaseRow(StringWriter writer, string caseId, ReadmeCaseMeasurement row, LokadPublicBenchmarkOperation operation)
    {
        writer.WriteLine($"| `{caseId}` | `{operation}` | {FormatMicros(row.Utf8Compiled)} | {FormatMicros(row.CompiledRegex)} | {FormatMicros(row.DecodeThenCompiledRegex)} |");
    }

    private static string ReplaceMarkedSection(string text, string beginMarker, string endMarker, string replacement)
    {
        var start = text.IndexOf(beginMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"README marker not found: {beginMarker}");
        }

        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException($"README marker not found: {endMarker}");
        }

        end += endMarker.Length;
        return text[..start] + replacement + text[end..];
    }

    private static string GetMarkedSection(string text, string beginMarker, string endMarker)
    {
        var start = text.IndexOf(beginMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"README marker not found: {beginMarker}");
        }

        start += beginMarker.Length;
        if (start < text.Length && text[start] == '\r')
        {
            start++;
        }

        if (start < text.Length && text[start] == '\n')
        {
            start++;
        }

        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException($"README marker not found: {endMarker}");
        }

        if (end > start && text[end - 1] == '\n')
        {
            end--;
            if (end > start && text[end - 1] == '\r')
            {
                end--;
            }
        }

        return text[start..end];
    }

    private static string BuildReadmeSectionMarkdownFromSnapshot(ReadmeBenchmarkSection section, ReadmeBenchmarkSnapshot snapshot)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var sectionSnapshot = GetRequiredSnapshotSection(snapshot, section);

        switch (section)
        {
            case ReadmeBenchmarkSection.DotNetPerformance:
                writer.WriteLine("## DotNetPerformance Benchmarks");
                writer.WriteLine();
                writer.WriteLine("These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:");
                writer.WriteLine("- `Utf8Regex`: direct UTF-8 input");
                writer.WriteLine("- `.NET predecoded`: `.NET Regex` on an already-decoded `string`");
                writer.WriteLine("- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`");
                writer.WriteLine();
                writer.WriteLine("All stress rows below are for `Count(...)`.");
                writer.WriteLine("Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.");
                writer.WriteLine();
                writer.WriteLine("### Stress Count Workloads");
                writer.WriteLine();
                WriteReadmeDefaultTableHeader(writer);
                foreach (var benchmarkCase in ReplicaCountBenchmarkCase.GetAll(ReplicaBenchmarkSource.DotNetPerformance))
                {
                    WriteReadmePlainDefaultCaseRow(writer, benchmarkCase.Id, GetRequiredSnapshotMeasurement(sectionSnapshot, benchmarkCase.Id).ToMeasurement());
                }

                writer.WriteLine();
                writer.WriteLine("### Public/Common and Industry Workloads");
                writer.WriteLine();
                writer.WriteLine("These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.");
                writer.WriteLine();
                WriteReadmePublicDefaultTableHeader(writer);
                foreach (var caseId in LokadPublicBenchmarkContext.GetAllCaseIds())
                {
                    WriteReadmePlainPublicDefaultCaseRow(writer, caseId, GetRequiredSnapshotMeasurement(sectionSnapshot, caseId).ToMeasurement(), new LokadPublicBenchmarkContext(caseId).Operation);
                }
                break;

            case ReadmeBenchmarkSection.DotNetPerformanceCompiled:
                writer.WriteLine("## DotNetPerformance Benchmarks (Compiled)");
                writer.WriteLine();
                writer.WriteLine("These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:");
                writer.WriteLine("- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`");
                writer.WriteLine("- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`");
                writer.WriteLine("- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`");
                writer.WriteLine();
                writer.WriteLine("All stress rows below are for `Count(...)`.");
                writer.WriteLine("Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.");
                writer.WriteLine();
                writer.WriteLine("### Stress Count Workloads");
                writer.WriteLine();
                WriteReadmeCompiledTableHeader(writer);
                foreach (var benchmarkCase in ReplicaCountBenchmarkCase.GetAll(ReplicaBenchmarkSource.DotNetPerformance))
                {
                    WriteReadmePlainCompiledCaseRow(writer, benchmarkCase.Id, GetRequiredSnapshotMeasurement(sectionSnapshot, benchmarkCase.Id).ToMeasurement());
                }

                writer.WriteLine();
                writer.WriteLine("### Public/Common and Industry Workloads");
                writer.WriteLine();
                writer.WriteLine("These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.");
                writer.WriteLine();
                WriteReadmePublicCompiledTableHeader(writer);
                foreach (var caseId in LokadPublicBenchmarkContext.GetAllCaseIds())
                {
                    WriteReadmePlainPublicCompiledCaseRow(writer, caseId, GetRequiredSnapshotMeasurement(sectionSnapshot, caseId).ToMeasurement(), new LokadPublicBenchmarkContext(caseId).Operation);
                }
                break;

            case ReadmeBenchmarkSection.Lokad:
                writer.WriteLine("## Lokad Benchmarks");
                writer.WriteLine();
                writer.WriteLine("These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:");
                writer.WriteLine("- `Utf8Regex`: direct UTF-8 input");
                writer.WriteLine("- `.NET predecoded`: `.NET Regex` on an already-decoded `string`");
                writer.WriteLine("- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`");
                writer.WriteLine();
                writer.WriteLine("This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.");
                writer.WriteLine();
                WriteReadmeDefaultTableHeader(writer);
                foreach (var benchmarkCase in ReplicaCountBenchmarkCase.GetAll(ReplicaBenchmarkSource.Lokad))
                {
                    WriteReadmePlainDefaultCaseRow(writer, benchmarkCase.Id, GetRequiredSnapshotMeasurement(sectionSnapshot, benchmarkCase.Id).ToMeasurement());
                }
                break;

            case ReadmeBenchmarkSection.LokadCompiled:
                writer.WriteLine("## Lokad Benchmarks (Compiled)");
                writer.WriteLine();
                writer.WriteLine("These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:");
                writer.WriteLine("- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`");
                writer.WriteLine("- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`");
                writer.WriteLine("- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`");
                writer.WriteLine();
                writer.WriteLine("This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.");
                writer.WriteLine();
                WriteReadmeCompiledTableHeader(writer);
                foreach (var benchmarkCase in ReplicaCountBenchmarkCase.GetAll(ReplicaBenchmarkSource.Lokad))
                {
                    WriteReadmePlainCompiledCaseRow(writer, benchmarkCase.Id, GetRequiredSnapshotMeasurement(sectionSnapshot, benchmarkCase.Id).ToMeasurement());
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }

        return writer.ToString().TrimEnd();
    }

    private static IEnumerable<ReadmeBenchmarkSection> GetReadmeTargetsForCase(string caseId)
    {
        if (ReplicaCountBenchmarkCase.TryResolve(caseId, out var benchmarkCase))
        {
            yield return benchmarkCase!.Source switch
            {
                ReplicaBenchmarkSource.DotNetPerformance => ReadmeBenchmarkSection.DotNetPerformance,
                ReplicaBenchmarkSource.Lokad => ReadmeBenchmarkSection.Lokad,
                _ => throw new ArgumentOutOfRangeException(nameof(benchmarkCase.Source)),
            };

            yield return benchmarkCase.Source switch
            {
                ReplicaBenchmarkSource.DotNetPerformance => ReadmeBenchmarkSection.DotNetPerformanceCompiled,
                ReplicaBenchmarkSource.Lokad => ReadmeBenchmarkSection.LokadCompiled,
                _ => throw new ArgumentOutOfRangeException(nameof(benchmarkCase.Source)),
            };

            yield break;
        }

        if (LokadPublicBenchmarkContext.GetAllCaseIds().Contains(caseId, StringComparer.Ordinal))
        {
            yield return ReadmeBenchmarkSection.DotNetPerformance;
            yield return ReadmeBenchmarkSection.DotNetPerformanceCompiled;
            yield break;
        }

        throw new InvalidOperationException($"Benchmark case '{caseId}' was not found in README benchmark catalogs.");
    }

    private static void RewriteReadmeFromSnapshot(ReadmeBenchmarkSnapshot snapshot)
    {
        var readmePath = FindRepoFile("README.md");
        var readme = File.ReadAllText(readmePath, Encoding.UTF8);

        foreach (var section in ParseReadmeSections(null))
        {
            var markerName = GetReadmeMarkerName(section);
            var beginMarker = $"<!-- BEGIN GENERATED {markerName} BENCHMARKS -->";
            var endMarker = $"<!-- END GENERATED {markerName} BENCHMARKS -->";
            var replacement = beginMarker + Environment.NewLine
                + BuildReadmeSectionMarkdownFromSnapshot(section, snapshot).TrimEnd()
                + Environment.NewLine + endMarker;
            readme = ReplaceMarkedSection(readme, beginMarker, endMarker, replacement);
        }

        File.WriteAllText(readmePath, readme, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static ReadmeBenchmarkSnapshot ParseReadmeBenchmarkSnapshot(string readme)
    {
        var snapshot = new ReadmeBenchmarkSnapshot();
        foreach (var section in ParseReadmeSections(null))
        {
            var markerName = GetReadmeMarkerName(section);
            var beginMarker = $"<!-- BEGIN GENERATED {markerName} BENCHMARKS -->";
            var endMarker = $"<!-- END GENERATED {markerName} BENCHMARKS -->";
            var sectionText = GetMarkedSection(readme, beginMarker, endMarker);
            var sectionSnapshot = new ReadmeBenchmarkSectionJson();

            foreach (var line in sectionText.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                if (!TryParseReadmeSnapshotRow(section, line, out var caseId, out var measurement))
                {
                    continue;
                }

                sectionSnapshot.Cases[caseId] = measurement;
            }

            snapshot.Sections[GetReadmeSectionToken(section)] = sectionSnapshot;
        }

        return snapshot;
    }

    private static bool TryParseReadmeSnapshotRow(ReadmeBenchmarkSection section, string line, out string caseId, out ReadmeCaseMeasurementJson measurement)
    {
        caseId = string.Empty;
        measurement = new ReadmeCaseMeasurementJson();

        if (line.StartsWith("<!--", StringComparison.Ordinal) || !line.StartsWith("| `", StringComparison.Ordinal))
        {
            return false;
        }

        var cells = line.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (cells.Length < 4)
        {
            return false;
        }

        caseId = cells[0].Trim().Trim('`');
        if (caseId.Length == 0)
        {
            return false;
        }

        static double ParseMicrosCell(string value)
        {
            try
            {
                return double.Parse(value.Replace(" us", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Could not parse README benchmark cell '{value}'.", ex);
            }
        }

        var isCompiled = section is ReadmeBenchmarkSection.DotNetPerformanceCompiled or ReadmeBenchmarkSection.LokadCompiled;
        var isPublic = cells.Length >= 5;

        if (isPublic)
        {
            var utf8 = ParseMicrosCell(cells[2]);
            var predecoded = ParseMicrosCell(cells[3]);
            var decode = ParseMicrosCell(cells[4]);
            measurement = isCompiled
                ? new ReadmeCaseMeasurementJson { Utf8Compiled = utf8, CompiledRegex = predecoded, DecodeThenCompiledRegex = decode }
                : new ReadmeCaseMeasurementJson { Utf8Regex = utf8, PredecodedRegex = predecoded, DecodeThenRegex = decode };
            return true;
        }

        var primary = ParseMicrosCell(cells[1]);
        var secondary = ParseMicrosCell(cells[2]);
        var tertiary = ParseMicrosCell(cells[3]);
        measurement = isCompiled
            ? new ReadmeCaseMeasurementJson { Utf8Compiled = primary, CompiledRegex = secondary, DecodeThenCompiledRegex = tertiary }
            : new ReadmeCaseMeasurementJson { Utf8Regex = primary, PredecodedRegex = secondary, DecodeThenRegex = tertiary };
        return true;
    }

    private static ReadmeBenchmarkSectionJson GetOrAddSnapshotSection(ReadmeBenchmarkSnapshot snapshot, ReadmeBenchmarkSection section)
    {
        var key = GetReadmeSectionToken(section);
        if (!snapshot.Sections.TryGetValue(key, out var sectionSnapshot))
        {
            sectionSnapshot = new ReadmeBenchmarkSectionJson();
            snapshot.Sections[key] = sectionSnapshot;
        }

        return sectionSnapshot;
    }

    private static ReadmeBenchmarkSectionJson GetRequiredSnapshotSection(ReadmeBenchmarkSnapshot snapshot, ReadmeBenchmarkSection section)
    {
        var key = GetReadmeSectionToken(section);
        if (!snapshot.Sections.TryGetValue(key, out var sectionSnapshot))
        {
            throw new InvalidOperationException($"Missing README snapshot section '{key}' in {ReadmeBenchmarkSnapshotFileName}.");
        }

        return sectionSnapshot;
    }

    private static ReadmeCaseMeasurementJson GetRequiredSnapshotMeasurement(ReadmeBenchmarkSectionJson sectionSnapshot, string caseId)
    {
        if (!sectionSnapshot.Cases.TryGetValue(caseId, out var measurement))
        {
            throw new InvalidOperationException($"Missing README snapshot case '{caseId}' in {ReadmeBenchmarkSnapshotFileName}.");
        }

        return measurement;
    }

    public static int RunMeasureLokadScriptCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaScriptBenchmarkCatalog.Get(caseId);
        var context = new LokadReplicaScriptBenchmarkContext(benchmarkCase);
        var iterations = ParseReadmeLokadScriptIterations(benchmarkCase, iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Model             : {benchmarkCase.Model}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"DotNetOptions     : {benchmarkCase.DotNetOptions}");
        Console.WriteLine($"Utf8Options       : {benchmarkCase.Utf8Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputChars}");
        Console.WriteLine($"InputBytes        : {context.TotalInputBytes}");
        if (benchmarkCase.Model == LokadReplicaScriptBenchmarkModel.PrefixMatchLoop)
        {
            Console.WriteLine($"SampleCount       : {context.Samples.Length}");
        }

        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureSmallAsciiLiteralFamilySplitControls(string? iterationsText)
    {
        var iterations = ParseIterations(iterationsText);
        (string Name, string Pattern, string Seed, bool ExpectedEligible)[] controls =
        [
            ("three-literal-dense", "tempus|magna|semper", "vitae tempus magna semper ", true),
            ("five-literal-dense", "Sherlock Holmes|John Watson|Mycroft Holmes|Mary Morstan|Mrs Hudson", "John Watson and Mrs Hudson met Sherlock Holmes. ", true),
            ("prefix-overlap", "alpha|alphabet|bravo", "alphabet bravo alpha ", true),
            ("three-literal-miss", "alpha|bravo|charlie", "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx ", true),
            ("two-short-literals", "foo|bar", "foo x bar y ", false),
            ("seven-literals", "alpha|bravo|charlie|deltaa|echoo|foxtrot|golfx", "alpha foxtrot golfx ", false),
            ("unicode-family", "éclair|bravo|charlie", "éclair bravo charlie ", false),
        ];

        Console.WriteLine($"Iterations        : {iterations}");
        foreach (var control in controls)
        {
            var input = RepeatToLength(control.Seed, 8_192);
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var utf8Regex = new Utf8Regex(control.Pattern);
            var utf8Compiled = new Utf8Regex(control.Pattern, RegexOptions.Compiled);
            var regex = new Regex(control.Pattern);
            var compiledRegex = new Regex(control.Pattern, RegexOptions.Compiled);
            var utf8Eligible = utf8Regex.Inspection.DebugCanUseSmallAsciiLiteralFamilySplit(inputBytes);
            var compiledEligible = utf8Compiled.Inspection.DebugCanUseSmallAsciiLiteralFamilySplit(inputBytes);
            if (utf8Eligible != control.ExpectedEligible || compiledEligible != control.ExpectedEligible)
            {
                throw new InvalidOperationException($"Control '{control.Name}' Split eligibility did not match its expectation.");
            }

            var literals = control.Pattern.Split('|').Select(Encoding.UTF8.GetBytes).ToArray();
            var hasPrimitive = PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var primitive);
            if (control.ExpectedEligible && !hasPrimitive)
            {
                throw new InvalidOperationException($"Control '{control.Name}' is not eligible for the prepared primitive.");
            }

            Console.WriteLine();
            Console.WriteLine($"Control           : {control.Name}");
            Console.WriteLine($"Pattern           : {control.Pattern}");
            Console.WriteLine($"InputBytes        : {inputBytes.Length}");
            Console.WriteLine($"Utf8Eligible      : {utf8Eligible}");
            Console.WriteLine($"CompiledEligible  : {compiledEligible}");

            Measure("Utf8Regex", 5, iterations, () => CountUtf8SplitValues(utf8Regex, inputBytes));
            Measure("Utf8Compiled", 5, iterations, () => CountUtf8SplitValues(utf8Compiled, inputBytes));
            if (hasPrimitive)
            {
                Measure("PrimitiveCount", 5, iterations, () => primitive.Count(inputBytes));
            }
            Measure("Utf8Fallback", 5, iterations, () => utf8Regex.Inspection.DebugCountSplitsViaFallback(inputBytes));
            Measure("CompiledFallback", 5, iterations, () => utf8Compiled.Inspection.DebugCountSplitsViaFallback(inputBytes));
            Measure("DecodeThenRegex", 5, iterations, () => CountRegexSplitValues(regex, Encoding.UTF8.GetString(inputBytes)));
            Measure("DecodeThenCompiledRegex", 5, iterations, () => CountRegexSplitValues(compiledRegex, Encoding.UTF8.GetString(inputBytes)));
            Measure("PredecodedRegex", 5, iterations, () => CountRegexSplitValues(regex, input));
            Measure("PredecodedCompiledRegex", 5, iterations, () => CountRegexSplitValues(compiledRegex, input));

            var allocationIterations = Math.Min(iterations, 1_000);
            Console.WriteLine($"Utf8AllocatedBytes: {MeasureAllocatedBytesPerInvocation(allocationIterations, () => CountUtf8SplitValues(utf8Regex, inputBytes))}");
            Console.WriteLine($"CompiledAllocatedBytes: {MeasureAllocatedBytesPerInvocation(allocationIterations, () => CountUtf8SplitValues(utf8Compiled, inputBytes))}");
            Console.WriteLine($"DecodeAllocatedBytes: {MeasureAllocatedBytesPerInvocation(allocationIterations, () => CountRegexSplitValues(regex, Encoding.UTF8.GetString(inputBytes)))}");
            Console.WriteLine($"DecodeCompiledAllocatedBytes: {MeasureAllocatedBytesPerInvocation(allocationIterations, () => CountRegexSplitValues(compiledRegex, Encoding.UTF8.GetString(inputBytes)))}");
        }

        return 0;
    }

    public static int RunMeasureLokadScriptPrefixCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaScriptBenchmarkCatalog.Get(caseId);
        if (benchmarkCase.Model != LokadReplicaScriptBenchmarkModel.PrefixMatchLoop)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Model             : {benchmarkCase.Model}");
            Console.WriteLine("LokadScriptPrefix    : case is not a PrefixMatchLoop");
            return 1;
        }

        var context = new LokadReplicaScriptBenchmarkContext(benchmarkCase);
        var iterations = ParseLokadPrefixIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Model             : {benchmarkCase.Model}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"DotNetOptions     : {benchmarkCase.DotNetOptions}");
        Console.WriteLine($"Utf8Options       : {benchmarkCase.Utf8Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputChars}");
        Console.WriteLine($"InputBytes        : {context.TotalInputBytes}");
        Console.WriteLine($"SampleCount       : {context.Samples.Length}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        if (!string.IsNullOrEmpty(context.Utf8Regex.Inspection.FallbackReason))
        {
            Console.WriteLine($"FallbackReason    : {context.Utf8Regex.Inspection.FallbackReason}");
        }

        Measure("ValidationOnly", iterations, context.ExecuteUtf8PrefixValidationOnly);
        Measure("WellFormedOnly", iterations, context.ExecuteUtf8PrefixWellFormedOnly);
        Measure("PrefilterOnly", iterations, context.ExecuteUtf8PrefixPrefilterOnly);
        Measure("DirectMatchHook", iterations, context.ExecuteUtf8PrefixDirectHookOnly);
        if (context.Utf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            Measure("AnchoredValidatorNative", iterations, context.ExecuteAnchoredValidatorNativeOnly);
            if (context.Utf8Regex.Inspection.SimplePatternPlan.AnchoredHeadTailRunPlan.HasValue)
            {
                Measure("HeadTailBoolOnly", iterations, context.ExecuteAnchoredHeadTailBoolOnly);
                Measure("HeadTailMatchOnly", iterations, context.ExecuteAnchoredHeadTailMatchOnly);
            }
            Measure("SimplePatternBoolOnly", iterations, context.ExecuteAsciiSimplePatternDirectBoolOnly);
            Measure("SimplePatternMatchOnly", iterations, context.ExecuteAsciiSimplePatternDirectMatchOnly);
            Measure("WholeMatchProjectionOnly", iterations, context.ExecuteWholeMatchProjectionOnly);
            Measure("AnchoredValidatorEmitted", iterations, context.ExecuteAnchoredValidatorEmittedOnly);
        }
        if (context.Utf8Regex.Inspection.DebugHasAsciiCultureInvariantTwin)
        {
            Measure("AsciiTwinMatchOnly", iterations, context.ExecuteUtf8PrefixAsciiCultureInvariantTwinOnly);
            Measure("AsciiTwinDirectOnly", iterations, context.ExecuteUtf8PrefixAsciiCultureInvariantTwinDirectOnly);
        }
        Measure("DirectUrlMatcher", iterations, context.ExecuteLokadScriptDirectUrlMatcher);
        Measure("PublicAfterValidation", iterations, context.ExecuteUtf8PrefixPublicAfterValidationOnly);
        Measure("CompiledMatchOnly", iterations, context.ExecuteUtf8PrefixCompiledAfterValidation);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureLokadScriptLexerPrimitiveCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaScriptBenchmarkCatalog.Get(caseId);
        if (benchmarkCase.Model != LokadReplicaScriptBenchmarkModel.PrefixMatchLoop)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Model             : {benchmarkCase.Model}");
            Console.WriteLine("LokadScriptLexer     : case is not a PrefixMatchLoop");
            return 1;
        }

        var context = new LokadReplicaScriptBenchmarkContext(benchmarkCase);
        var iterations = ParseLokadPrefixIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Model             : {benchmarkCase.Model}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"DotNetOptions     : {benchmarkCase.DotNetOptions}");
        Console.WriteLine($"Utf8Options       : {benchmarkCase.Utf8Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputChars}");
        Console.WriteLine($"InputBytes        : {context.TotalInputBytes}");
        Console.WriteLine($"SampleCount       : {context.Samples.Length}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        if (!string.IsNullOrEmpty(context.Utf8Regex.Inspection.FallbackReason))
        {
            Console.WriteLine($"FallbackReason    : {context.Utf8Regex.Inspection.FallbackReason}");
        }

        Measure("WellFormedOnly", iterations, context.ExecuteUtf8PrefixWellFormedOnly);
        Measure("DirectMatchHook", iterations, context.ExecuteUtf8PrefixDirectHookOnly);
        Measure("PublicAfterValidation", iterations, context.ExecuteUtf8PrefixPublicAfterValidationOnly);
        Measure("LexerPrimitiveOnly", iterations, context.ExecuteLokadScriptLexerPrimitiveOnly);
        Measure("WellFormedPrimitive", iterations, context.ExecuteLokadScriptLexerWellFormedPrimitiveOnly);
        Measure("CompiledMatchOnly", iterations, context.ExecuteUtf8PrefixCompiledAfterValidation);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureLokadScriptUrlCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaScriptBenchmarkCatalog.Get(caseId);
        if (benchmarkCase.Model != LokadReplicaScriptBenchmarkModel.PrefixMatchLoop ||
            !benchmarkCase.Id.StartsWith("lokad/langserv/url-", StringComparison.Ordinal))
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Model             : {benchmarkCase.Model}");
            Console.WriteLine("LokadScriptUrl    : case is not a Lokad script URL PrefixMatchLoop");
            return 1;
        }

        return RunMeasureLokadScriptPrefixCase(caseId, iterationsText);
    }

    public static int RunMeasureTokenFinderCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaScriptBenchmarkCatalog.Get(caseId);
        if (benchmarkCase.Model != LokadReplicaScriptBenchmarkModel.PrefixMatchLoop ||
            benchmarkCase.Id != "lokad/langserv/helper-identifier")
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Model             : {benchmarkCase.Model}");
            Console.WriteLine("TokenFinder       : this exploratory model currently targets helper-identifier only");
            return 1;
        }

        var context = new LokadReplicaScriptBenchmarkContext(benchmarkCase);
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Model             : {benchmarkCase.Model}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputChars}");
        Console.WriteLine($"InputBytes        : {context.TotalInputBytes}");
        Console.WriteLine($"SampleCount       : {context.Samples.Length}");

        Measure("TokenFinderModel", iterations, context.ExecuteAsciiTokenFinderModel);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        return 0;
    }

    public static int RunMeasureLineFamilyCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaScriptBenchmarkCatalog.Get(caseId);
        if (benchmarkCase.Model != LokadReplicaScriptBenchmarkModel.Count ||
            benchmarkCase.Id is not ("lokad/imports/module-imports" or "lokad/folding/region-marker"))
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Model             : {benchmarkCase.Model}");
            Console.WriteLine("LineFamily        : this exploratory model currently targets module-imports and region-marker only");
            return 1;
        }

        var context = new LokadReplicaScriptBenchmarkContext(benchmarkCase);
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Model             : {benchmarkCase.Model}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputChars}");
        Console.WriteLine($"InputBytes        : {context.TotalInputBytes}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        if (!string.IsNullOrEmpty(context.Utf8Regex.Inspection.FallbackReason))
        {
            Console.WriteLine($"FallbackReason    : {context.Utf8Regex.Inspection.FallbackReason}");
        }

        Measure("LiteralFinderModel", iterations, context.ExecuteLiteralFinderModel);
        Measure("LinePrefixModel", iterations, context.ExecuteLinePrefixModel);
        Measure("LineVerifierModel", iterations, context.ExecuteLineVerifierModel);
        if (benchmarkCase.Id == "lokad/imports/module-imports")
        {
            Measure("LineWalkModel", iterations, context.ExecuteImportLineWalkModel);
        }
        Measure("FallbackDirectCount", iterations, context.ExecuteUtf8CountFallbackDirect);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        return 0;
    }

    public static int RunMeasureLokadScriptByteSafePrefixCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaScriptBenchmarkCatalog.Get(caseId);
        if (benchmarkCase.Model != LokadReplicaScriptBenchmarkModel.PrefixMatchLoop)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Model             : {benchmarkCase.Model}");
            Console.WriteLine("LokadScriptByteSafe  : case is not a PrefixMatchLoop");
            return 1;
        }

        var context = new LokadReplicaScriptBenchmarkContext(benchmarkCase);
        var analysis = Utf8FrontEnd.Compile(context.Pattern, benchmarkCase.Utf8Options);
        var regexPlan = analysis;
        if (Utf8CompiledEngineSelector.Select(regexPlan).Kind != Utf8CompiledEngineKind.ByteSafeLinear)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"CompiledEngine    : {Utf8CompiledEngineSelector.Select(regexPlan).Kind}");
            Console.WriteLine("LokadScriptByteSafe  : not a byte-safe linear case");
            return 1;
        }

        var verifierRuntime = Utf8VerifierRuntime.Create(regexPlan, context.Pattern, benchmarkCase.Utf8Options, Regex.InfiniteMatchTimeout);
        var iterations = ParseLokadPrefixIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Model             : {benchmarkCase.Model}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"DotNetOptions     : {benchmarkCase.DotNetOptions}");
        Console.WriteLine($"Utf8Options       : {benchmarkCase.Utf8Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputChars}");
        Console.WriteLine($"InputBytes        : {context.TotalInputBytes}");
        Console.WriteLine($"SampleCount       : {context.Samples.Length}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        if (!string.IsNullOrEmpty(context.Utf8Regex.Inspection.FallbackReason))
        {
            Console.WriteLine($"FallbackReason    : {context.Utf8Regex.Inspection.FallbackReason}");
        }

        Measure("AnchorCandidates", iterations, () => ExecuteLokadScriptByteSafeAcrossSamples(context.SampleBytes, input => ExecuteByteSafeAnchorCandidateCount(regexPlan, input)));
        Measure("AnchorCandidateSum", iterations, () => ExecuteLokadScriptByteSafeAcrossSamples(context.SampleBytes, input => ExecuteByteSafeAnchorCandidateIndexSum(regexPlan, input)));
        Measure("StatefulAnchorCandidates", iterations, () => ExecuteLokadScriptByteSafeAcrossSamples(context.SampleBytes, input => ExecuteByteSafeStatefulAnchorCandidateCount(regexPlan, input)));
        Measure("StatefulAnchorCandidateSum", iterations, () => ExecuteLokadScriptByteSafeAcrossSamples(context.SampleBytes, input => ExecuteByteSafeStatefulAnchorCandidateIndexSum(regexPlan, input)));
        Measure("StructuralCandidates", iterations, () => ExecuteLokadScriptByteSafeAcrossSamples(context.SampleBytes, input => ExecuteByteSafeStructuralCandidateCount(regexPlan, input)));
        Measure("StructuralCandidateSum", iterations, () => ExecuteLokadScriptByteSafeAcrossSamples(context.SampleBytes, input => ExecuteByteSafeStructuralCandidateIndexSum(regexPlan, input)));
        Measure("VerifierCount", iterations, () => ExecuteLokadScriptByteSafeAcrossSamples(context.SampleBytes, input => ExecuteByteSafeVerifierCount(regexPlan, verifierRuntime, input)));
        Measure("VerifierIndexSum", iterations, () => ExecuteLokadScriptByteSafeAcrossSamples(context.SampleBytes, input => ExecuteByteSafeVerifierIndexSum(regexPlan, verifierRuntime, input)));
        Measure("CompiledMatchOnly", iterations, context.ExecuteUtf8PrefixCompiledAfterValidation);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        return 0;
    }

    public static int RunMeasureLokadScriptCountCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaScriptBenchmarkCatalog.Get(caseId);
        if (benchmarkCase.Model != LokadReplicaScriptBenchmarkModel.Count)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Model             : {benchmarkCase.Model}");
            Console.WriteLine("LokadScriptCount     : case is not a Count case");
            return 1;
        }

        var context = new LokadReplicaScriptBenchmarkContext(benchmarkCase);
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Model             : {benchmarkCase.Model}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"DotNetOptions     : {benchmarkCase.DotNetOptions}");
        Console.WriteLine($"Utf8Options       : {benchmarkCase.Utf8Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputChars}");
        Console.WriteLine($"InputBytes        : {context.TotalInputBytes}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        if (!string.IsNullOrEmpty(context.Utf8Regex.Inspection.FallbackReason))
        {
            Console.WriteLine($"FallbackReason    : {context.Utf8Regex.Inspection.FallbackReason}");
        }

        Measure("ValidationOnly", iterations, context.ExecuteUtf8CountValidationOnly);
        Measure("WellFormedOnly", iterations, context.ExecuteUtf8CountWellFormedOnly);
        Measure("PrefilterOnly", iterations, context.ExecuteUtf8CountPrefilterOnly);
        Measure("CompiledCount", iterations, context.ExecuteUtf8CountCompiled);
        Measure("FallbackCandidates", iterations, context.ExecuteUtf8CountFallbackCandidates);
        Measure("FallbackBoundaryCandidates", iterations, context.ExecuteUtf8CountFallbackBoundaryCandidates);
        Measure("FallbackVerifiedCount", iterations, context.ExecuteUtf8CountFallbackVerified);
        Measure("FallbackDirectCount", iterations, context.ExecuteUtf8CountFallbackDirect);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        return 0;
    }

    public static int RunMeasureLokadPublicCase(string caseId, string? iterationsText)
    {
        var context = new LokadPublicBenchmarkContext(caseId);
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {context.Operation}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {context.Options}");
        Console.WriteLine($"InputChars        : {context.InputString.Length}");
        Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.CompiledUtf8Regex.Inspection.CompiledEngineKind}");
        if (!string.IsNullOrEmpty(context.CompiledUtf8Regex.Inspection.FallbackReason))
        {
            Console.WriteLine($"FallbackReason    : {context.CompiledUtf8Regex.Inspection.FallbackReason}");
        }

        Measure("ValidationOnly", iterations, () => Utf8Validation.Validate(context.InputBytes).Utf16Length);
        Measure("AsciiOnlyCheck", iterations, () => Utf8InputAnalyzer.IsAscii(context.InputBytes) ? context.InputBytes.Length : 0);
        Measure("Utf8IsValid", iterations, () => Utf8.IsValid(context.InputBytes) ? context.InputBytes.Length : 0);
        Measure("WellFormedOnly", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
            return context.InputBytes.Length;
        });
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureCompiledMicrocostCase(string caseId, string? iterationsText)
    {
        if (LokadPublicBenchmarkContext.GetAllCaseIds().Contains(caseId, StringComparer.Ordinal))
        {
            var context = new LokadPublicBenchmarkContext(caseId);
            var iterations = ParseShortPublicIterations(context, iterationsText);

            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Origin            : public");
            Console.WriteLine($"Operation         : {context.Operation}");
            Console.WriteLine($"Pattern           : {context.Pattern}");
            Console.WriteLine($"Options           : {context.Options}");
            Console.WriteLine($"Iterations        : {iterations}");
            Console.WriteLine($"InputChars        : {context.InputString.Length}");
            Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
            Console.WriteLine($"ExecutionKind     : {context.CompiledUtf8Regex.Inspection.ExecutionKind}");
            Console.WriteLine($"CompiledEngine    : {context.CompiledUtf8Regex.Inspection.CompiledEngineKind}");
            Console.WriteLine($"CompiledBackend   : {context.CompiledUtf8Regex.Inspection.CompiledExecutionBackend}");
            Console.WriteLine($"CompiledRuntime   : {context.CompiledUtf8Regex.Inspection.DebugCompiledEngineRuntimeType}");
            Console.WriteLine($"CompiledCanEmit   : {context.CompiledUtf8Regex.Inspection.DebugCanLowerEmittedKernel}");
            Console.WriteLine($"CompiledEmitKind  : {context.CompiledUtf8Regex.Inspection.DebugLoweredEmittedKernelKind}");

            Measure("ValidationOnly", iterations, context.ExecuteUtf8ValidationOnly);
            Measure("WellFormedOnly", iterations, context.ExecuteUtf8WellFormedOnly);

            switch (context.Operation)
            {
                case LokadPublicBenchmarkOperation.Count:
                    WriteCountDiagnostics("Compiled", context.CompiledUtf8Regex.CollectCountDiagnostics(context.InputBytes));
                    Console.WriteLine($"CompiledGateUtf8Literal      : {context.CompiledUtf8Regex.Inspection.DebugCanUseFusedCompiledUtf8LiteralCount}");
                    Console.WriteLine($"CompiledGateUtf8LiteralFamily: {context.CompiledUtf8Regex.Inspection.DebugCanUseFusedCompiledUtf8LiteralFamilyCount}");
                    Console.WriteLine($"CompiledBudgetIsNull         : {context.CompiledUtf8Regex.Inspection.DebugCreatedExecutionBudgetIsNull}");
                    Measure("Utf8IsValid", iterations, context.ExecuteUtf8IsValidOnly);
                    Measure("Utf8InputValidateOnly", iterations, context.ExecuteUtf8InputValidateOnly);
                    Measure("Utf8ValidationCoreOnly", iterations, context.ExecuteUtf8ValidationCoreWellFormedOnly);
                    Measure("CompiledDirectCount", iterations, context.ExecuteUtf8CompiledDirectCountOnly);
                    Measure("CompiledDirectWithCreatedBudget", iterations, () => context.CompiledUtf8Regex.Inspection.DebugCountViaCompiledEngineWithCreatedBudget(context.InputBytes));
                    if (context.CompiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralValidatedThreeByte(context.InputBytes, out _))
                    {
                        Measure("CompiledLiteralValidatedThreeByte", iterations, () =>
                        {
                            context.CompiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralValidatedThreeByte(context.InputBytes, out var count);
                            return count;
                        });
                    }

                    if (context.CompiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch(context.InputBytes, out _))
                    {
                        Measure("CompiledLiteralPreparedSearch", iterations, () =>
                        {
                            context.CompiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch(context.InputBytes, out var count);
                            return count;
                        });
                    }

                    if (context.CompiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralAnchored(context.InputBytes, out _))
                    {
                        Measure("CompiledLiteralAnchored", iterations, () =>
                        {
                            context.CompiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralAnchored(context.InputBytes, out var count);
                            return count;
                        });
                    }
                    break;

                case LokadPublicBenchmarkOperation.IsMatch:
                case LokadPublicBenchmarkOperation.Match:
                    if (context.Operation == LokadPublicBenchmarkOperation.IsMatch)
                    {
                        Measure("DirectBoolOnly", iterations, context.ExecuteUtf8DirectBoolOnly);
                    }
                    if (context.Utf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
                    {
                        Console.WriteLine($"AnchoredValidatorSegments   : {context.Utf8Regex.Inspection.DebugAnchoredValidatorSegmentSummary}");
                        Measure("AnchoredValidatorBoolOnly", iterations, context.ExecuteUtf8DirectBoolOnly);
                        Measure("AnchoredValidatorFixedPrefix", iterations, context.ExecuteAnchoredValidatorFixedPrefixOnly);
                        Measure("AnchoredValidatorFirstBounded", iterations, context.ExecuteAnchoredValidatorFirstBoundedSegmentOnly);
                        Measure("AnchoredValidatorSuffixAfterFirstBounded", iterations, context.ExecuteAnchoredValidatorSuffixAfterFirstBoundedOnly);
                        Measure("AnchoredValidatorNativeWhole", iterations, context.ExecuteAnchoredValidatorNativeWholeOnly);
                        Measure("CompiledAnchoredValidatorDirect", iterations, context.ExecuteCompiledAnchoredValidatorDirectOnly);
                    }
                    if (context.Utf8Regex.Inspection.DebugTryFindDirectFallbackTokenWithoutValidation(context.InputBytes, out _, out _))
                    {
                        Measure("DirectFallbackTokenRaw", iterations, context.ExecuteDirectFallbackTokenRawOnly);
                    }
                    if (context.CaseId == "common/date-match")
                    {
                        Measure("DateTokenWhole", iterations, context.ExecuteDateTokenWholeOnly);
                    }
                    if (context.CaseId == "common/uri-match")
                    {
                        Measure("UriTokenWhole", iterations, context.ExecuteUriTokenWholeOnly);
                    }
                    if (context.Operation == LokadPublicBenchmarkOperation.Match &&
                        context.CompiledUtf8Regex.Inspection.DebugTryMatchCompiledAsciiLiteralFamilyRaw(context.InputBytes, out _, out _))
                    {
                        Measure("CompiledLiteralFamilyRawMatch", iterations, context.ExecuteUtf8CompiledLiteralFamilyRawMatchOnly);
                        Measure("CompiledLiteralFamilyProjectionOnly", iterations, context.ExecuteUtf8CompiledLiteralFamilyProjectionOnly);
                    }
                    Measure("CompiledBoolAfterValidation", iterations, context.ExecuteUtf8CompiledBoolAfterValidationOnly);
                    Measure("CompiledMatchAfterValidation", iterations, context.ExecuteUtf8CompiledAfterValidationOnly);
                    Measure("CompiledDirectNoValidation", iterations, context.ExecuteUtf8CompiledDirectNoValidationOnly);
                    break;
            }

            Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
            Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
            Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
            return 0;
        }

        var lokadScriptCase = LokadReplicaScriptBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (lokadScriptCase is not null)
        {
            var context = new LokadReplicaScriptBenchmarkContext(lokadScriptCase);
            var iterations = lokadScriptCase.Model == LokadReplicaScriptBenchmarkModel.PrefixMatchLoop
                ? ParseLokadPrefixIterations(iterationsText)
                : ParseIterations(iterationsText);

            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Origin            : lokad-script");
            Console.WriteLine($"Model             : {lokadScriptCase.Model}");
            Console.WriteLine($"Pattern           : {lokadScriptCase.Pattern}");
            Console.WriteLine($"DotNetOptions     : {lokadScriptCase.DotNetOptions}");
            Console.WriteLine($"Utf8Options       : {lokadScriptCase.Utf8Options}");
            Console.WriteLine($"Iterations        : {iterations}");
            Console.WriteLine($"InputChars        : {context.InputChars}");
            Console.WriteLine($"InputBytes        : {context.TotalInputBytes}");
            Console.WriteLine($"InputIsAscii      : {Utf8InputAnalyzer.ValidateOnly(context.InputBytes).IsAscii}");
            Console.WriteLine($"ExecutionKind     : {context.CompiledUtf8Regex.Inspection.ExecutionKind}");
            Console.WriteLine($"CompiledEngine    : {context.CompiledUtf8Regex.Inspection.CompiledEngineKind}");
            Console.WriteLine($"CompiledBackend   : {context.CompiledUtf8Regex.Inspection.CompiledExecutionBackend}");
            Console.WriteLine($"CompiledRuntime   : {context.CompiledUtf8Regex.Inspection.DebugCompiledEngineRuntimeType}");
            Console.WriteLine($"CompiledCanEmit   : {context.CompiledUtf8Regex.Inspection.DebugCanLowerEmittedKernel}");
            Console.WriteLine($"CompiledEmitKind  : {context.CompiledUtf8Regex.Inspection.DebugLoweredEmittedKernelKind}");
            Console.WriteLine($"CompiledUsesEmit  : {context.CompiledUtf8Regex.Inspection.DebugUsesEmittedKernelMatcher}");

            if (lokadScriptCase.Model == LokadReplicaScriptBenchmarkModel.Count)
            {
                WriteCountDiagnostics("Compiled", context.CompiledUtf8Regex.CollectCountDiagnostics(context.InputBytes));
                Console.WriteLine($"CompiledGateUtf8Literal      : {context.CompiledUtf8Regex.Inspection.DebugCanUseFusedCompiledUtf8LiteralCount}");
                Console.WriteLine($"CompiledGateUtf8LiteralFamily: {context.CompiledUtf8Regex.Inspection.DebugCanUseFusedCompiledUtf8LiteralFamilyCount}");
                Console.WriteLine($"CompiledBudgetIsNull         : {context.CompiledUtf8Regex.Inspection.DebugCreatedExecutionBudgetIsNull}");
                Measure("ValidationOnly", iterations, context.ExecuteUtf8CountValidationOnly);
                Measure("WellFormedOnly", iterations, context.ExecuteUtf8CountWellFormedOnly);
                Measure("CompiledDirectCount", iterations, context.ExecuteUtf8CountCompiledDirect);
                Measure("CompiledDirectWithCreatedBudget", iterations, () => context.CompiledUtf8Regex.Inspection.DebugCountViaCompiledEngineWithCreatedBudget(context.InputBytes));
                Measure("Utf8Compiled", iterations, context.ExecuteUtf8CountCompiled);
            }
            else
            {
                Measure("ValidationOnly", iterations, context.ExecuteUtf8PrefixValidationOnly);
                Measure("WellFormedOnly", iterations, context.ExecuteUtf8PrefixWellFormedOnly);
                Measure("CompiledBoolAfterValidation", iterations, context.ExecuteUtf8PrefixCompiledBoolAfterValidation);
                Measure("CompiledMatchAfterValidation", iterations, context.ExecuteUtf8PrefixCompiledAfterValidation);
                Measure("CompiledDirectNoValidation", iterations, context.ExecuteUtf8PrefixCompiledDirectHookOnly);
                Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
            }

            Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
            Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
            return 0;
        }

        var replicaCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var compiledUtf8Regex = new Utf8Regex(replicaCase.Utf8Pattern, replicaCase.Options | RegexOptions.Compiled);
        var compiledIterations = ParseIterations(iterationsText);

        WriteReplicaCaseHeader(replicaCase, compiledIterations);
        Console.WriteLine($"InputIsAscii      : {Utf8InputAnalyzer.ValidateOnly(replicaCase.InputBytes).IsAscii}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledBackend   : {compiledUtf8Regex.Inspection.CompiledExecutionBackend}");
        Console.WriteLine($"CompiledRuntime   : {compiledUtf8Regex.Inspection.DebugCompiledEngineRuntimeType}");
        Console.WriteLine($"CompiledCanEmit   : {compiledUtf8Regex.Inspection.DebugCanLowerEmittedKernel}");
        Console.WriteLine($"CompiledEmitKind  : {compiledUtf8Regex.Inspection.DebugLoweredEmittedKernelKind}");
        Console.WriteLine($"CompiledUsesEmit  : {compiledUtf8Regex.Inspection.DebugUsesEmittedKernelMatcher}");
        WriteCountDiagnostics("Compiled", compiledUtf8Regex.CollectCountDiagnostics(replicaCase.InputBytes));
        Console.WriteLine($"CompiledGateUtf8Literal      : {compiledUtf8Regex.Inspection.DebugCanUseFusedCompiledUtf8LiteralCount}");
        Console.WriteLine($"CompiledGateUtf8LiteralFamily: {compiledUtf8Regex.Inspection.DebugCanUseFusedCompiledUtf8LiteralFamilyCount}");
        Console.WriteLine($"CompiledThrowIfInvalidCount  : {compiledUtf8Regex.Inspection.DebugSupportsThrowIfInvalidOnlyCount}");
        Console.WriteLine($"CompiledBudgetIsNull         : {compiledUtf8Regex.Inspection.DebugCreatedExecutionBudgetIsNull}");
        Console.WriteLine($"ThrowIfInvalidMode          : {Utf8InputAnalyzer.SelectThrowIfInvalidOnlyMode(replicaCase.InputBytes)}");
        var inputShape = Utf8InputAnalyzer.DescribeLeadByteSample(replicaCase.InputBytes);
        Console.WriteLine($"ShapeSampleBytes            : {inputShape.SampleLength}");
        Console.WriteLine($"ShapeAsciiBytes             : {inputShape.AsciiBytes}");
        Console.WriteLine($"ShapeFirstNonAscii          : {inputShape.FirstNonAsciiOffset}");
        Console.WriteLine($"ShapeTwoByteLeads           : {inputShape.TwoByteLeads}");
        Console.WriteLine($"ShapeThreeByteLeads         : {inputShape.ThreeByteLeads}");
        Console.WriteLine($"ShapeFourByteLeads          : {inputShape.FourByteLeads}");

        Measure("ValidationOnly", compiledIterations, () => ExecuteValidationOnly(replicaCase.InputBytes));
        Measure("Utf8IsValid", compiledIterations, () => Utf8.IsValid(replicaCase.InputBytes) ? replicaCase.InputBytes.Length : 0);
        Measure("Utf8InputValidateOnly", compiledIterations, () => Utf8InputAnalyzer.ValidateOnly(replicaCase.InputBytes).Utf16Length);
        Measure("Utf8ValidationCoreOnly", compiledIterations, () =>
        {
            Utf8ValidationCore.TryValidate(replicaCase.InputBytes, computeUtf16Length: false, out _, out _);
            return replicaCase.InputBytes.Length;
        });
        Measure("WellFormedOnly", compiledIterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(replicaCase.InputBytes);
            return replicaCase.InputBytes.Length;
        });
        Measure("CompiledDirectCount", compiledIterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(replicaCase.InputBytes));
        Measure("CompiledDirectWithCreatedBudget", compiledIterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngineWithCreatedBudget(replicaCase.InputBytes));
        if (compiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralValidatedThreeByte(replicaCase.InputBytes, out _))
        {
            Measure("CompiledLiteralValidatedThreeByte", compiledIterations, () =>
            {
                compiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralValidatedThreeByte(replicaCase.InputBytes, out var count);
                return count;
            });
        }

        if (compiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch(replicaCase.InputBytes, out _))
        {
            Measure("CompiledLiteralPreparedSearch", compiledIterations, () =>
            {
                compiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch(replicaCase.InputBytes, out var count);
                return count;
            });
        }

        if (compiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralAnchored(replicaCase.InputBytes, out _))
        {
            Measure("CompiledLiteralAnchored", compiledIterations, () =>
            {
                compiledUtf8Regex.Inspection.DebugTryCountExactUtf8LiteralAnchored(replicaCase.InputBytes, out var count);
                return count;
            });
        }

        Measure("Utf8Compiled", compiledIterations, () => compiledUtf8Regex.Count(replicaCase.InputBytes));
        Measure("DecodeThenCompiledRegex", compiledIterations, replicaCase.CountDecodeThenCompiledRegex);
        Measure("PredecodedCompiledRegex", compiledIterations, replicaCase.CountPredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureLokadPublicCaseDeep(string caseId, string? iterationsText)
    {
        var context = new LokadPublicBenchmarkContext(caseId);
        var iterations = ParseLokadPublicDeepIterations(context, iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {context.Operation}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {context.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputString.Length}");
        Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.CompiledUtf8Regex.Inspection.CompiledEngineKind}");
        if (!string.IsNullOrEmpty(context.CompiledUtf8Regex.Inspection.FallbackReason))
        {
            Console.WriteLine($"FallbackReason    : {context.CompiledUtf8Regex.Inspection.FallbackReason}");
        }

        if (context.Operation == LokadPublicBenchmarkOperation.Count)
        {
            var baselineDiagnostics = context.Utf8Regex.CollectCountDiagnostics(context.InputBytes);
            var compiledDiagnostics = context.CompiledUtf8Regex.CollectCountDiagnostics(context.InputBytes);
            var simplePatternPlan = context.Utf8Regex.Inspection.PreparedRegex.SimplePatternPlan;

            WriteCountDiagnostics("Baseline", baselineDiagnostics);
            WriteCountDiagnostics("Compiled", compiledDiagnostics);
            if (context.Utf8Regex.Inspection.ExecutionKind == NativeExecutionKind.AsciiSimplePattern)
            {
                Console.WriteLine($"RunPlanHasValue   : {simplePatternPlan.RunPlan.HasValue}");
                Console.WriteLine($"BoundedSuffixPlan : {simplePatternPlan.BoundedSuffixLiteralPlan.HasValue}");
            }

            Measure("ValidationOnly", iterations, () => Utf8Validation.Validate(context.InputBytes).Utf16Length);
            Measure("WellFormedOnly", iterations, () =>
            {
                Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
                return context.InputBytes.Length;
            });
            Measure("Utf8Direct", iterations, () => context.Utf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
            Measure("Utf8CompiledDirect", iterations, () => context.CompiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
            if (simplePatternPlan.BoundedSuffixLiteralPlan.HasValue)
            {
                Measure("BoundedSuffixDirect", iterations, () => Utf8AsciiBoundedSuffixLiteralExecutor.Count(context.InputBytes, simplePatternPlan.BoundedSuffixLiteralPlan, budget: Utf8ExecutionDeadline.Infinite));
            }
            if (simplePatternPlan.RunPlan.HasValue)
            {
                Measure("RunPlanDirect", iterations, () => Utf8AsciiCharClassRunExecutor.Count(context.InputBytes, simplePatternPlan.RunPlan, budget: Utf8ExecutionDeadline.Infinite));
            }
            Measure("FallbackCandidates", iterations, () => context.Utf8Regex.Inspection.DebugCountFallbackCandidates(context.InputBytes));
            Measure("FallbackBoundaryCandidates", iterations, () => context.Utf8Regex.Inspection.DebugCountFallbackBoundaryCandidates(context.InputBytes));
            Measure("FallbackVerifiedCount", iterations, () => context.Utf8Regex.Inspection.DebugCountFallbackViaSearchStarts(context.InputBytes));
            Measure("FallbackDirectCount", iterations, () => context.Utf8Regex.Inspection.DebugCountFallbackDirect(context.InputBytes));
        }
        else if (context.Operation == LokadPublicBenchmarkOperation.IsMatch)
        {
            var baselineDiagnostics = context.Utf8Regex.CollectIsMatchDiagnostics(context.InputBytes);
            var compiledDiagnostics = context.CompiledUtf8Regex.CollectIsMatchDiagnostics(context.InputBytes);

            WriteIsMatchDiagnostics("Baseline", baselineDiagnostics);
            WriteIsMatchDiagnostics("Compiled", compiledDiagnostics);

            if (context.Utf8Regex.Inspection.ExecutionKind == NativeExecutionKind.AsciiSimplePattern)
            {
                var simplePatternPlan = context.Utf8Regex.Inspection.SimplePatternPlan;
                Console.WriteLine($"BranchCount       : {context.Utf8Regex.Inspection.DebugSimplePatternBranchCount}");
                Console.WriteLine($"BranchLengths     : {context.Utf8Regex.Inspection.DebugSimplePatternBranchLengths}");
                Console.WriteLine($"HasAnchoredValidator : {simplePatternPlan.AnchoredValidatorPlan.HasValue}");
                Console.WriteLine($"HasAnchoredHeadTail  : {simplePatternPlan.AnchoredHeadTailRunPlan.HasValue}");
                Console.WriteLine($"HasRepeatedDigitGroup: {simplePatternPlan.RepeatedDigitGroupPlan.HasValue}");
                Console.WriteLine($"CanDirectFixedLength : {context.Utf8Regex.Inspection.DebugSimplePatternCanUseDirectAnchoredFixedLength}");
                Console.WriteLine($"CanDirectFixedAlt    : {context.Utf8Regex.Inspection.DebugSimplePatternCanUseDirectAnchoredFixedAlternation}");
                if (context.Utf8Regex.Inspection.DebugSimplePatternCanUseDirectAnchoredFixedAlternation)
                {
                    Console.WriteLine($"DirectFixedAltEval  : {context.Utf8Regex.Inspection.DebugDirectAnchoredFixedAlternationSummary(context.InputBytes)}");
                }
                Console.WriteLine($"CompiledUsesEmittedValidator : {context.CompiledUtf8Regex.Inspection.DebugUsesEmittedAnchoredValidatorMatcher}");
            }

            Measure("ValidationOnly", iterations, () => Utf8Validation.Validate(context.InputBytes).Utf16Length);
            Measure("WellFormedOnly", iterations, () =>
            {
                Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
                return context.InputBytes.Length;
            });
            if (context.Utf8Regex.Inspection.ExecutionKind == NativeExecutionKind.AsciiSimplePattern)
            {
                var simplePatternPlan = context.Utf8Regex.Inspection.SimplePatternPlan;
                if (context.Utf8Regex.Inspection.DebugSimplePatternCanUseDirectAnchoredFixedLength)
                {
                    Measure("DirectFixedLengthOnly", iterations, () =>
                        context.Utf8Regex.Inspection.DebugTryMatchDirectAnchoredFixedLengthSimplePattern(context.InputBytes, out var matchedLength)
                            ? matchedLength
                            : 0);
                }

                if (context.Utf8Regex.Inspection.DebugSimplePatternCanUseDirectAnchoredFixedAlternation)
                {
                    Measure("DirectFixedAlternationOnly", iterations, () =>
                        context.Utf8Regex.Inspection.DebugTryMatchDirectAnchoredFixedAlternationSimplePattern(context.InputBytes, out var matchedLength)
                            ? matchedLength
                            : 0);
                }

                if (simplePatternPlan.RepeatedDigitGroupPlan.HasValue)
                {
                    Measure("RepeatedDigitGroupWhole", iterations, context.ExecuteRepeatedDigitGroupWholeOnly);
                    Measure("RepeatedDigitGroupFind", iterations, context.ExecuteRepeatedDigitGroupFindOnly);
                }
            }
        }
        else if (context.Operation == LokadPublicBenchmarkOperation.Replace)
        {
            Console.WriteLine($"BaselinePreferFallbackTextOps : {context.Utf8Regex.Inspection.DebugShouldPreferFallbackForCompiledLiteralFamilyTextOperations()}");
            Console.WriteLine($"CompiledPreferFallbackTextOps : {context.CompiledUtf8Regex.Inspection.DebugShouldPreferFallbackForCompiledLiteralFamilyTextOperations()}");

            Measure("ValidationOnly", iterations, () => Utf8Validation.Validate(context.InputBytes).Utf16Length);
            Measure("WellFormedOnly", iterations, () =>
            {
                Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
                return context.InputBytes.Length;
            });
            Measure("FallbackReplace", iterations, () => context.Utf8Regex.Inspection.DebugReplaceViaFallback(context.InputBytes, context.Replacement));
            Measure("CompiledFallbackReplace", iterations, () => context.CompiledUtf8Regex.Inspection.DebugReplaceViaFallback(context.InputBytes, context.Replacement));
            Measure("NativeReplace", iterations, () => context.Utf8Regex.Inspection.DebugReplaceViaNativeTextOperations(context.InputBytes, context.Replacement));
            Measure("CompiledNativeReplace", iterations, () => context.CompiledUtf8Regex.Inspection.DebugReplaceViaNativeTextOperations(context.InputBytes, context.Replacement));
        }
        else if (context.Operation == LokadPublicBenchmarkOperation.Split)
        {
            Console.WriteLine($"BaselineCanUseNativeSplit : {context.Utf8Regex.Inspection.DebugCanUseNativeSplit(context.InputBytes)}");
            Console.WriteLine($"CompiledCanUseNativeSplit : {context.CompiledUtf8Regex.Inspection.DebugCanUseNativeSplit(context.InputBytes)}");
            Console.WriteLine($"BaselineCanUseSmallAsciiLiteralFamilySplit : {context.Utf8Regex.Inspection.DebugCanUseSmallAsciiLiteralFamilySplit(context.InputBytes)}");
            Console.WriteLine($"CompiledCanUseSmallAsciiLiteralFamilySplit : {context.CompiledUtf8Regex.Inspection.DebugCanUseSmallAsciiLiteralFamilySplit(context.InputBytes)}");
            Console.WriteLine($"BaselinePreferFallbackTextOps : {context.Utf8Regex.Inspection.DebugShouldPreferFallbackForCompiledLiteralFamilyTextOperations()}");
            Console.WriteLine($"CompiledPreferFallbackTextOps : {context.CompiledUtf8Regex.Inspection.DebugShouldPreferFallbackForCompiledLiteralFamilyTextOperations()}");

            Measure("ValidationOnly", iterations, () => Utf8Validation.Validate(context.InputBytes).Utf16Length);
            Measure("WellFormedOnly", iterations, () =>
            {
                Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
                return context.InputBytes.Length;
            });
            Measure("FallbackSplit", iterations, () => context.Utf8Regex.Inspection.DebugCountSplitsViaFallback(context.InputBytes));
            Measure("CompiledFallbackSplit", iterations, () => context.CompiledUtf8Regex.Inspection.DebugCountSplitsViaFallback(context.InputBytes));
            Measure("NativeSplit", iterations, () => context.Utf8Regex.Inspection.DebugCountSplitsViaCompiledEngine(context.InputBytes));
            Measure("CompiledNativeSplit", iterations, () => context.CompiledUtf8Regex.Inspection.DebugCountSplitsViaCompiledEngine(context.InputBytes));
        }

        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        if (context.Operation == LokadPublicBenchmarkOperation.Replace)
        {
            Measure(
                "FallbackReplaceAfterApi",
                iterations,
                () => context.Utf8Regex.Inspection.DebugReplaceViaFallback(context.InputBytes, context.Replacement));
            Measure(
                "CompiledFallbackReplaceAfterApi",
                iterations,
                () => context.CompiledUtf8Regex.Inspection.DebugReplaceViaFallback(context.InputBytes, context.Replacement));
            Measure(
                "NativeReplaceAfterApi",
                iterations,
                () => context.Utf8Regex.Inspection.DebugReplaceViaNativeTextOperations(context.InputBytes, context.Replacement));
            Measure(
                "CompiledNativeReplaceAfterApi",
                iterations,
                () => context.CompiledUtf8Regex.Inspection.DebugReplaceViaNativeTextOperations(context.InputBytes, context.Replacement));
        }
        else if (context.Operation == LokadPublicBenchmarkOperation.Split)
        {
            Measure(
                "FallbackSplitAfterApi",
                iterations,
                () => context.Utf8Regex.Inspection.DebugCountSplitsViaFallback(context.InputBytes));
            Measure(
                "CompiledFallbackSplitAfterApi",
                iterations,
                () => context.CompiledUtf8Regex.Inspection.DebugCountSplitsViaFallback(context.InputBytes));
            Measure(
                "NativeSplitAfterApi",
                iterations,
                () => context.Utf8Regex.Inspection.DebugCountSplitsViaCompiledEngine(context.InputBytes));
            Measure(
                "CompiledNativeSplitAfterApi",
                iterations,
                () => context.CompiledUtf8Regex.Inspection.DebugCountSplitsViaCompiledEngine(context.InputBytes));
        }

        return 0;
    }

    public static int RunMeasureShortPublicMicrocostCase(string caseId, string? iterationsText)
    {
        var context = new LokadPublicBenchmarkContext(caseId);
        var iterations = ParseShortPublicIterations(context, iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {context.Operation}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {context.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputString.Length}");
        Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CanDirectFixedLength : {context.Utf8Regex.Inspection.DebugSimplePatternCanUseDirectAnchoredFixedLength}");
        Console.WriteLine($"CanDirectFixedAlt    : {context.Utf8Regex.Inspection.DebugSimplePatternCanUseDirectAnchoredFixedAlternation}");

        Measure("ValidationOnly", iterations, context.ExecuteUtf8ValidationOnly);
        Measure("WellFormedOnly", iterations, context.ExecuteUtf8WellFormedOnly);
        Measure("PrefilterOnly", iterations, context.ExecuteUtf8PrefilterOnly);
        Measure("DirectHook", iterations, context.ExecuteUtf8DirectHookOnly);
        if (context.Utf8Regex.Inspection.DebugCanGuideFallbackVerification)
        {
            Measure("FallbackSearchStartsOnly", iterations, context.ExecuteUtf8FallbackSearchStartsOnly);
        }
        if (context.Utf8Regex.Inspection.DebugSimplePatternCanUseDirectAnchoredFixedLength)
        {
            Measure("DirectFixedLengthOnly", iterations, context.ExecuteUtf8DirectFixedLengthOnly);
        }

        if (context.Utf8Regex.Inspection.DebugSimplePatternCanUseDirectAnchoredFixedAlternation)
        {
            Measure("DirectFixedAlternationOnly", iterations, context.ExecuteUtf8DirectFixedAlternationOnly);
        }

        Measure("PublicAfterValidation", iterations, context.ExecuteUtf8PublicAfterValidationOnly);
        Measure("CompiledAfterValidation", iterations, context.ExecuteUtf8CompiledAfterValidationOnly);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureShortPrefixMicrocostCase(string caseId, string? iterationsText)
    {
        var context = new LokadReplicaScriptBenchmarkContext(LokadReplicaScriptBenchmarkCatalog.Get(caseId));
        var iterations = ParseLokadPrefixIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Model             : {context.BenchmarkCase.Model}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"DotNetOptions     : {context.BenchmarkCase.DotNetOptions}");
        Console.WriteLine($"Utf8Options       : {context.BenchmarkCase.Utf8Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputChars}");
        Console.WriteLine($"InputBytes        : {context.TotalInputBytes}");
        Console.WriteLine($"SampleCount       : {context.Samples.Length}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");

        Measure("ValidationOnly", iterations, context.ExecuteUtf8PrefixValidationOnly);
        Measure("WellFormedOnly", iterations, context.ExecuteUtf8PrefixWellFormedOnly);
        Measure("PrefilterOnly", iterations, context.ExecuteUtf8PrefixPrefilterOnly);
        Measure("DirectHook", iterations, context.ExecuteUtf8PrefixDirectHookOnly);
        Measure("PublicAfterValidation", iterations, context.ExecuteUtf8PrefixPublicAfterValidationOnly);
        Measure("CompiledAfterValidation", iterations, context.ExecuteUtf8PrefixCompiledAfterValidation);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureLokadPublicValidatorDeep(string caseId, string? iterationsText)
    {
        var context = new LokadPublicBenchmarkContext(caseId);
        if (context.Operation != LokadPublicBenchmarkOperation.IsMatch ||
            !caseId.StartsWith("common/", StringComparison.Ordinal))
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Operation         : {context.Operation}");
            Console.WriteLine("ValidatorDeep     : only common/* IsMatch cases are supported");
            return 1;
        }

        var iterations = ParseValidatorDeepIterations(context, iterationsText);
        var validation = Utf8Validation.Validate(context.InputBytes);
        var baselineDiagnostics = context.Utf8Regex.CollectIsMatchDiagnostics(context.InputBytes);
        var compiledDiagnostics = context.CompiledUtf8Regex.CollectIsMatchDiagnostics(context.InputBytes);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {context.Operation}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {context.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputString.Length}");
        Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.CompiledUtf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"BaselineRuntime   : {context.Utf8Regex.Inspection.DebugCompiledEngineRuntimeType}");
        Console.WriteLine($"CompiledRuntime   : {context.CompiledUtf8Regex.Inspection.DebugCompiledEngineRuntimeType}");
        Console.WriteLine($"BaselineDirectFam : {context.Utf8Regex.Inspection.DebugFallbackDirectFamilyKind}");
        Console.WriteLine($"CompiledDirectFam : {context.CompiledUtf8Regex.Inspection.DebugFallbackDirectFamilyKind}");
        Console.WriteLine($"BaselineSupportsWellFormed : {context.Utf8Regex.Inspection.DebugSupportsWellFormedOnlyMatch}");
        Console.WriteLine($"CompiledSupportsWellFormed : {context.CompiledUtf8Regex.Inspection.DebugSupportsWellFormedOnlyMatch}");
        Console.WriteLine($"BaselineWellFormedMissDefinitive : {context.Utf8Regex.Inspection.DebugWellFormedOnlyMatchMissIsDefinitive}");
        Console.WriteLine($"CompiledWellFormedMissDefinitive : {context.CompiledUtf8Regex.Inspection.DebugWellFormedOnlyMatchMissIsDefinitive}");

        WriteIsMatchDiagnostics("Baseline", baselineDiagnostics);
        WriteIsMatchDiagnostics("Compiled", compiledDiagnostics);

        Measure("ValidationOnly", iterations, () => Utf8Validation.Validate(context.InputBytes).Utf16Length);
        Measure("WellFormedOnly", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
            return context.InputBytes.Length;
        });
        Measure("TryMatchNoValidation", iterations, () => context.Utf8Regex.Inspection.DebugTryMatchWithoutValidation(context.InputBytes, out _) ? 1 : 0);
        Measure("CompiledTryMatchNoValidation", iterations, () => context.CompiledUtf8Regex.Inspection.DebugTryMatchWithoutValidation(context.InputBytes, out _) ? 1 : 0);
        if (context.Utf8Regex.Inspection.DebugCanGuideFallbackVerification)
        {
            Measure("FallbackSearchStartsOnly", iterations, context.ExecuteUtf8FallbackSearchStartsOnly);
        }
        Measure("MatchAfterWellFormed", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
            return context.Utf8Regex.Inspection.DebugIsMatchViaCompiledEngine(context.InputBytes, default) ? 1 : 0;
        });
        Measure("CompiledMatchAfterWellFormed", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
            return context.CompiledUtf8Regex.Inspection.DebugIsMatchViaCompiledEngine(context.InputBytes, default) ? 1 : 0;
        });
        Measure("MatchAfterValidation", iterations, () => context.Utf8Regex.Inspection.DebugIsMatchViaCompiledEngine(context.InputBytes, validation) ? 1 : 0);
        Measure("CompiledMatchAfterValidation", iterations, () => context.CompiledUtf8Regex.Inspection.DebugIsMatchViaCompiledEngine(context.InputBytes, validation) ? 1 : 0);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureLokadPublicCompiledFallbackCase(string caseId, string? iterationsText)
    {
        var context = new LokadPublicBenchmarkContext(caseId);
        if (context.Operation != LokadPublicBenchmarkOperation.Count)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Operation         : {context.Operation}");
            Console.WriteLine("CompiledFallback  : only count cases are supported");
            return 1;
        }

        var iterations = ParseIterations(iterationsText);
        var baselineDiagnostics = context.Utf8Regex.CollectCountDiagnostics(context.InputBytes);
        var compiledDiagnostics = context.CompiledUtf8Regex.CollectCountDiagnostics(context.InputBytes);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {context.Operation}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {context.Options}");
        Console.WriteLine($"InputChars        : {context.InputString.Length}");
        Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
        Console.WriteLine($"BaselineEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledEngine    : {context.CompiledUtf8Regex.Inspection.CompiledEngineKind}");
        if (!string.IsNullOrEmpty(context.CompiledUtf8Regex.Inspection.FallbackReason))
        {
            Console.WriteLine($"FallbackReason    : {context.CompiledUtf8Regex.Inspection.FallbackReason}");
        }

        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        Measure("ValidationOnly", iterations, () => Utf8Validation.Validate(context.InputBytes).Utf16Length);
        Measure("AsciiOnlyCheck", iterations, () => Utf8InputAnalyzer.IsAscii(context.InputBytes) ? context.InputBytes.Length : 0);
        Measure("Utf8IsValid", iterations, () => Utf8.IsValid(context.InputBytes) ? context.InputBytes.Length : 0);
        Measure("WellFormedOnly", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
            return context.InputBytes.Length;
        });
        Measure("BaselineDirect", iterations, () => context.Utf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("CompiledDirect", iterations, () => context.CompiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunInspectIgnoreCaseLiteralReplicaCase(string caseId)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        if (regex.Inspection.ExecutionKind != NativeExecutionKind.AsciiLiteralIgnoreCase || regex.Inspection.SearchPlan.LiteralSearch is not { } ignoreCaseSearch)
        {
            Console.Error.WriteLine($"Case '{caseId}' is not on NativeExecutionKind.AsciiLiteralIgnoreCase.");
            return 1;
        }

        var count = ignoreCaseSearch.CountWithMetrics(benchmarkCase.InputBytes, out var candidateCount, out var verifyCount);

        WriteReplicaCaseHeader(benchmarkCase, iterations: null);
        Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
        Console.WriteLine("PreparedSearcher  : IgnoreCaseLiteral");
        Console.WriteLine($"MatchCount        : {count}");
        Console.WriteLine($"CandidateCount    : {candidateCount}");
        Console.WriteLine($"VerificationCount : {verifyCount}");
        return 0;
    }

    public static int RunMeasureIgnoreCaseLiteralCompareSweepReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        if (regex.Inspection.ExecutionKind != NativeExecutionKind.AsciiLiteralIgnoreCase || regex.Inspection.SearchPlan.LiteralSearch is not { } search)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine("IgnoreCaseCompare : case is not on AsciiLiteralIgnoreCase");
            return 1;
        }

        var iterations = ParseIterations(iterationsText);
        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
        Console.WriteLine($"PreparedSearcher  : {regex.Inspection.SearchPlan.PreparedSearcher.Kind}");
        Console.WriteLine($"LiteralLength     : {search.Length}");
        Console.WriteLine($"CurrentCompare    : {search.GetIgnoreCasePreferredCompareIndex()}");

        Measure("PreparedCount", iterations, () => ExecutePreparedSubstringCount(search, benchmarkCase.InputBytes));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);

        if (!search.IgnoreCase || search.Length <= 10)
        {
            Console.WriteLine("CompareSweep      : not applicable to short or exact literals");
            return 0;
        }

        for (var compareIndex = 1; compareIndex < search.Length; compareIndex++)
        {
            var count = search.CountIgnoreCaseWithPreferredCompareIndex(benchmarkCase.InputBytes, compareIndex, out var candidateCount, out var verifyCount);
            Console.WriteLine($"CompareDiag[{compareIndex,2}]: count={count} candidates={candidateCount} verifies={verifyCount}");
            Measure($"Compare[{compareIndex}]", iterations, () =>
                ExecutePreparedSubstringCountWithPreferredCompare(search, benchmarkCase.InputBytes, compareIndex));
        }

        return 0;
    }

    public static int RunMeasureUtf8ValidationProfile(string profileName, string? iterationsText)
    {
        var iterations = ParseIterations(iterationsText);
        var input = Utf8ValidationBenchmarkProfiles.Create(profileName);

        Console.WriteLine($"Profile           : {profileName}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputBytes        : {input.Length}");

        Measure("ThrowIfInvalidOnly", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return input.Length;
        });
        Measure("Utf8IsValid", iterations, () => Utf8.IsValid(input) ? input.Length : 0);
        Measure("ValidateOnly", iterations, () => Utf8Validation.Validate(input).Utf16Length);
        return 0;
    }

    public static int RunInspectDirectFamilyPattern(string pattern, string? optionsText)
    {
        var options = ParseRegexOptions(optionsText);
        var regex = new Utf8Regex(pattern, options);
        Console.WriteLine($"Pattern           : {pattern}");
        Console.WriteLine($"Options           : {options}");
        Console.WriteLine($"DirectFamily      : {regex.Inspection.DebugFallbackDirectFamilyKind}");
        Console.WriteLine($"CompiledEngine    : {regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
        Console.WriteLine($"FallbackReason    : {regex.Inspection.FallbackReason ?? "<native>"}");
        return 0;
    }

    public static int RunInspectDirectFamilyCase(string caseId)
    {
        var utf8Case = Utf8RegexBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (utf8Case is not null)
        {
            var context = new Utf8RegexBenchmarkContext(utf8Case);
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Pattern           : {utf8Case.Pattern}");
            Console.WriteLine($"Options           : {utf8Case.Options}");
            Console.WriteLine($"DirectFamily      : {context.Utf8Regex.Inspection.DebugFallbackDirectFamilyKind}");
            Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
            Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
            Console.WriteLine($"FallbackReason    : {context.Utf8Regex.Inspection.FallbackReason ?? "<native>"}");
            return 0;
        }

        var dotNetPerformanceCase = DotNetPerformanceReplicaBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (dotNetPerformanceCase is not null)
        {
            var context = new DotNetPerformanceReplicaBenchmarkContext(dotNetPerformanceCase);
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Pattern           : {context.Pattern}");
            Console.WriteLine($"Options           : {dotNetPerformanceCase.Options}");
            Console.WriteLine($"DirectFamily      : {context.Utf8Regex.Inspection.DebugFallbackDirectFamilyKind}");
            Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
            Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
            Console.WriteLine($"FallbackReason    : {context.Utf8Regex.Inspection.FallbackReason ?? "<native>"}");
            return 0;
        }

        var lokadCodeCase = LokadReplicaCodeBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (lokadCodeCase is not null)
        {
            var context = new LokadReplicaCodeBenchmarkContext(lokadCodeCase);
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Pattern           : {context.CompiledPattern}");
            Console.WriteLine($"Options           : {lokadCodeCase.Options}");
            Console.WriteLine($"DirectFamily      : {context.Utf8Regex.Inspection.DebugFallbackDirectFamilyKind}");
            Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
            Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
            Console.WriteLine($"FallbackReason    : {context.Utf8Regex.Inspection.FallbackReason ?? "<native>"}");
            return 0;
        }

        var lokadScriptCase = LokadReplicaScriptBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (lokadScriptCase is not null)
        {
            var context = new LokadReplicaScriptBenchmarkContext(lokadScriptCase);
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Pattern           : {context.Pattern}");
            Console.WriteLine($"Options           : {lokadScriptCase.Utf8Options}");
            Console.WriteLine($"DirectFamily      : {context.Utf8Regex.Inspection.DebugFallbackDirectFamilyKind}");
            Console.WriteLine($"CompiledEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
            Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
            Console.WriteLine($"FallbackReason    : {context.Utf8Regex.Inspection.FallbackReason ?? "<native>"}");
            return 0;
        }

        Console.Error.WriteLine($"Case '{caseId}' was not found.");
        return 1;
    }

    public static int RunMeasureUnicodeLiteralCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options | RegexOptions.Compiled);
        var iterations = ParseIterations(iterationsText);

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"LiteralBytes      : {(regex.Inspection.SearchPlan.LiteralUtf8 ?? []).Length}");
        Console.WriteLine($"InputBytes        : {benchmarkCase.InputBytes.Length}");

        var isExactLiteral = regex.Inspection.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            regex.Inspection.SearchPlan.LiteralUtf8 is { Length: > 0 };
        if (!isExactLiteral)
        {
            Console.WriteLine("UnicodeLiteral    : semantic fallback; exact-literal microkernels unavailable");
        }

        var hasValidatedThreeByte = isExactLiteral && regex.Inspection.DebugTryCountExactUtf8LiteralValidatedThreeByte(benchmarkCase.InputBytes, out _);
        var hasLeadingScalar = isExactLiteral && regex.Inspection.DebugTryCountExactUtf8LiteralLeadingScalarAnchored(benchmarkCase.InputBytes, out _);
        var hasPreparedSearch = isExactLiteral && regex.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch(benchmarkCase.InputBytes, out _);
        var hasAnchorByte = isExactLiteral && regex.Inspection.DebugTryCountExactUtf8LiteralAnchored(benchmarkCase.InputBytes, out _);

        Console.WriteLine($"HasValidatedThreeByte: {hasValidatedThreeByte}");
        Console.WriteLine($"HasLeadingScalar  : {hasLeadingScalar}");
        Console.WriteLine($"HasPreparedSearch : {hasPreparedSearch}");
        Console.WriteLine($"HasAnchorByte     : {hasAnchorByte}");
        if (regex.Inspection.SearchPlan.AlternateLiteralsUtf8 is { Length: > 0 } alternateLiterals)
        {
            Console.WriteLine($"SmallAsciiFamilyPrimitive: {PreparedSmallAsciiLiteralFamilySearch.TryCreate(alternateLiterals, out _)}");
        }

        Measure("ThrowIfInvalidOnly", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(benchmarkCase.InputBytes);
            return benchmarkCase.InputBytes.Length;
        });
        Measure("Utf8IsValid", iterations, () => Utf8.IsValid(benchmarkCase.InputBytes) ? benchmarkCase.InputBytes.Length : 0);
        Measure("ValidateOnly", iterations, () => Utf8Validation.Validate(benchmarkCase.InputBytes).Utf16Length);
        Measure("StrictDecodeOnly", iterations, () => Utf8Validation.DecodeStrict(benchmarkCase.InputBytes).Length);
        Measure("DefaultDecodeOnly", iterations, () => Encoding.UTF8.GetString(benchmarkCase.InputBytes).Length);

        if (hasValidatedThreeByte)
        {
            Measure("ValidatedThreeByte", iterations, () =>
            {
                regex.Inspection.DebugTryCountExactUtf8LiteralValidatedThreeByte(benchmarkCase.InputBytes, out var count);
                return count;
            });
        }

        if (hasLeadingScalar)
        {
            Measure("LeadingScalarAnchored", iterations, () =>
            {
                regex.Inspection.DebugTryCountExactUtf8LiteralLeadingScalarAnchored(benchmarkCase.InputBytes, out var count);
                return count;
            });
        }

        if (hasPreparedSearch)
        {
            Measure("PreparedLiteralSearch", iterations, () =>
            {
                regex.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch(benchmarkCase.InputBytes, out var count);
                return count;
            });
        }

        if (hasAnchorByte)
        {
            Measure("AnchorByteCount", iterations, () =>
            {
                regex.Inspection.DebugTryCountExactUtf8LiteralAnchored(benchmarkCase.InputBytes, out var count);
                return count;
            });
        }

        Measure("UnvalidatedMatchProbe", iterations, () =>
        {
            var handled = regex.Inspection.DebugTryMatchWithoutValidation(benchmarkCase.InputBytes, out var match);
            if (!handled)
            {
                return -1;
            }

            return match.Success ? 1 : 0;
        });
        Measure("CompiledUnvalidatedMatchProbe", iterations, () =>
        {
            var handled = compiledUtf8Regex.Inspection.DebugTryMatchWithoutValidation(benchmarkCase.InputBytes, out var match);
            if (!handled)
            {
                return -1;
            }

            return match.Success ? 1 : 0;
        });

        var validation = Utf8Validation.Validate(benchmarkCase.InputBytes);
        Measure("MatchAfterValidation", iterations, () =>
            regex.Inspection.DebugIsMatchViaCompiledEngine(benchmarkCase.InputBytes, validation) ? 1 : 0);
        Measure("CompiledMatchAfterValidation", iterations, () =>
            compiledUtf8Regex.Inspection.DebugIsMatchViaCompiledEngine(benchmarkCase.InputBytes, validation) ? 1 : 0);
        Measure("RuntimeFamilyIsMatch", iterations, () =>
        {
            var handled = regex.Inspection.DebugTryIsMatchLiteralFamily(benchmarkCase.InputBytes, out var isMatch);
            if (!handled)
            {
                return -1;
            }

            return isMatch ? 1 : 0;
        });
        Measure("CompiledRuntimeFamilyIsMatch", iterations, () =>
        {
            var handled = compiledUtf8Regex.Inspection.DebugTryIsMatchLiteralFamily(benchmarkCase.InputBytes, out var isMatch);
            if (!handled)
            {
                return -1;
            }

            return isMatch ? 1 : 0;
        });
        if (regex.Inspection.ExecutionKind is NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals)
        {
            var rightToLeft = (benchmarkCase.Options & RegexOptions.RightToLeft) != 0;
            Measure("PreparedFamilyIsMatch", iterations, () =>
                Utf8SearchStrategyExecutor.IsMatchLiteralFamily(
                    regex.Inspection.SearchPlan,
                    benchmarkCase.InputBytes,
                    Utf8ExecutionDeadline.Infinite,
                    rightToLeft) ? 1 : 0);
            Measure("CompiledPreparedFamilyIsMatch", iterations, () =>
                Utf8SearchStrategyExecutor.IsMatchLiteralFamily(
                    compiledUtf8Regex.Inspection.SearchPlan,
                    benchmarkCase.InputBytes,
                    Utf8ExecutionDeadline.Infinite,
                    rightToLeft) ? 1 : 0);
        }

        Measure("CompiledDirect", iterations, () => regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);

        var decoded = Encoding.UTF8.GetString(benchmarkCase.InputBytes);
        Measure("Utf8IsMatch", iterations, () => benchmarkCase.Utf8Regex.IsMatch(benchmarkCase.InputBytes) ? 1 : 0);
        Measure("Utf8CompiledIsMatch", iterations, () => compiledUtf8Regex.IsMatch(benchmarkCase.InputBytes) ? 1 : 0);
        Measure("DecodeIsMatch", iterations, () => benchmarkCase.Regex.IsMatch(Encoding.UTF8.GetString(benchmarkCase.InputBytes)) ? 1 : 0);
        Measure("DecodeCompiledIsMatch", iterations, () => benchmarkCase.CompiledRegex.IsMatch(Encoding.UTF8.GetString(benchmarkCase.InputBytes)) ? 1 : 0);
        Measure("PredecodedIsMatch", iterations, () => benchmarkCase.Regex.IsMatch(decoded) ? 1 : 0);
        Measure("PredecodedCompiledIsMatch", iterations, () => benchmarkCase.CompiledRegex.IsMatch(decoded) ? 1 : 0);

        if (benchmarkCase.Pattern.AsSpan().IndexOfAnyInRange('\u0080', char.MaxValue) < 0)
        {
            var asciiMiss = new byte[benchmarkCase.InputBytes.Length];
            asciiMiss.AsSpan().Fill((byte)'x');
            Measure("AsciiMissUnvalidatedProbe", iterations, () =>
            {
                var handled = regex.Inspection.DebugTryMatchWithoutValidation(asciiMiss, out var match);
                if (!handled)
                {
                    return -1;
                }

                return match.Success ? 1 : 0;
            });
            Measure("AsciiMissRuntimeFamilyIsMatch", iterations, () =>
            {
                var handled = regex.Inspection.DebugTryIsMatchLiteralFamily(asciiMiss, out var isMatch);
                if (!handled)
                {
                    return -1;
                }

                return isMatch ? 1 : 0;
            });
            Measure("AsciiMissCompiledRuntimeFamilyIsMatch", iterations, () =>
            {
                var handled = compiledUtf8Regex.Inspection.DebugTryIsMatchLiteralFamily(asciiMiss, out var isMatch);
                if (!handled)
                {
                    return -1;
                }

                return isMatch ? 1 : 0;
            });
            Measure("AsciiMissIsMatch", iterations, () => benchmarkCase.Utf8Regex.IsMatch(asciiMiss) ? 1 : 0);
            Measure("AsciiMissCompiledIsMatch", iterations, () => compiledUtf8Regex.IsMatch(asciiMiss) ? 1 : 0);
        }

        return 0;
    }

    public static int RunMeasureUtf8ValidationReplicaCase(string caseId, string? iterationsText)
    {
        var iterations = ParseIterations(iterationsText);
        if (ReplicaCountBenchmarkCase.TryResolve(caseId, out var benchmarkCase))
        {
            WriteReplicaCaseHeader(benchmarkCase!, iterations);
            Console.WriteLine($"InputBytes        : {benchmarkCase!.InputBytes.Length}");
            Measure("ThrowIfInvalidOnly", iterations, () =>
            {
                Utf8Validation.ThrowIfInvalidOnly(benchmarkCase.InputBytes);
                return benchmarkCase.InputBytes.Length;
            });
            Measure("Utf8IsValid", iterations, () => Utf8.IsValid(benchmarkCase.InputBytes) ? benchmarkCase.InputBytes.Length : 0);
            Measure("ValidateOnly", iterations, () => Utf8Validation.Validate(benchmarkCase.InputBytes).Utf16Length);
            return 0;
        }

        if (LokadPublicBenchmarkContext.GetAllCaseIds().Contains(caseId, StringComparer.Ordinal))
        {
            var context = new LokadPublicBenchmarkContext(caseId);
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Operation         : {context.Operation}");
            Console.WriteLine($"Pattern           : {context.Pattern}");
            Console.WriteLine($"Options           : {context.Options}");
            Console.WriteLine($"Iterations        : {iterations}");
            Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
            Measure("ThrowIfInvalidOnly", iterations, () =>
            {
                Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
                return context.InputBytes.Length;
            });
            Measure("Utf8IsValid", iterations, () => Utf8.IsValid(context.InputBytes) ? context.InputBytes.Length : 0);
            Measure("ValidateOnly", iterations, () => Utf8Validation.Validate(context.InputBytes).Utf16Length);
            return 0;
        }

        Console.Error.WriteLine($"Case '{caseId}' was not found.");
        return 1;
    }

    public static int RunMeasureExactLiteralFamilyBackendsReplicaCase(
        string caseId,
        string? iterationsText,
        string? inputModeText = null)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        var iterations = ParseIterations(iterationsText);
        if (regex.Inspection.ExecutionKind != NativeExecutionKind.ExactUtf8Literals)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine("LiteralFamily     : case is not on ExactUtf8Literals");
            return 1;
        }

        var literals = regex.Inspection.SearchPlan.MultiLiteralSearch.Literals;
        var direct = new PreparedLiteralSetSearch(literals);
        var rootByte = PreparedMultiLiteralCandidatePrefilter.CreateRootByte(literals);
        var earliest = PreparedMultiLiteralCandidatePrefilter.CreateEarliest(literals);
        var packed = PreparedMultiLiteralPackedSearch.TryCreate(literals, out var packedSearch) ? packedSearch : default;
        var trie = PreparedMultiLiteralTrieSearch.Create(literals);
        var automaton = PreparedMultiLiteralAutomatonSearch.Create(literals);
        var input = inputModeText switch
        {
            null or "original" => benchmarkCase.InputBytes,
            "ascii-miss" => new byte[benchmarkCase.InputBytes.Length],
            _ => throw new ArgumentException($"Unsupported input mode '{inputModeText}'.", nameof(inputModeText)),
        };
        if (inputModeText == "ascii-miss")
        {
            input.AsSpan().Fill((byte)'x');
        }

        var decoded = Encoding.UTF8.GetString(input);
        var expected = benchmarkCase.Regex.Count(decoded);
        AssertSelectorResult(caseId, "Utf8Regex", expected, regex.Count(input));

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"InputMode         : {inputModeText ?? "original"}");
        Console.WriteLine($"EffectiveInputBytes: {input.Length}");
        Console.WriteLine($"ExpectedCount      : {expected}");
        Console.WriteLine($"LiteralCount      : {literals.Length}");
        Console.WriteLine($"PreparedKind      : {regex.Inspection.SearchPlan.MultiLiteralSearch.Kind}");
        Console.WriteLine($"PackedAvailable   : {packed.ShortestLength != 0}");
        Console.WriteLine($"AutomatonStates   : {automaton.StateCount}");
        Console.WriteLine($"AutomatonRootFanout: {automaton.RootFanout}");
        Console.WriteLine($"AutomatonOutputs  : {automaton.OutputStateCount}");

        Measure("ExactDirect", iterations, () => ExecuteExactLiteralSetCount(direct, input));
        Measure("RootByte", iterations, () => ExecuteCandidatePrefilterCount(rootByte, input));
        Measure("EarliestExact", iterations, () => ExecuteCandidatePrefilterCount(earliest, input));
        if (packed.ShortestLength != 0)
        {
            Measure("Packed", iterations, () => ExecutePackedLiteralSetCount(packed, input));
        }
        Measure("Trie", iterations, () => ExecuteTrieLiteralSetCount(trie, input));
        Measure("Automaton", iterations, () => ExecuteAutomatonLiteralSetCount(automaton, input));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(input));
        Measure(
            benchmarkCase.Source == ReplicaBenchmarkSource.DotNetPerformance ? "DotNetRegex" : "DecodeThenRegex",
            iterations,
            benchmarkCase.Source == ReplicaBenchmarkSource.DotNetPerformance
                ? () => benchmarkCase.Regex.Count(decoded)
                : () => benchmarkCase.Regex.Count(Encoding.UTF8.GetString(input)));
        if (benchmarkCase.Source == ReplicaBenchmarkSource.Lokad)
        {
            Measure("PredecodedRegex", iterations, () => benchmarkCase.Regex.Count(decoded));
        }

        Measure("ExactDirectIsMatch", iterations, () =>
            direct.TryFindFirstMatchWithLength(input, out _, out _) ? 1 : 0);
        Measure("RootByteIsMatch", iterations, () => ExecuteCandidatePrefilterIsMatch(rootByte, input));
        Measure("Utf8IsMatch", iterations, () => regex.IsMatch(input) ? 1 : 0);
        Measure("DotNetIsMatch", iterations, () => benchmarkCase.Regex.IsMatch(decoded) ? 1 : 0);
        return 0;
    }

    public static int RunMeasureExactLiteralFamilyReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        var iterations = ParseIterations(iterationsText);
        if (regex.Inspection.ExecutionKind != NativeExecutionKind.ExactUtf8Literals)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine("LiteralFamily     : case is not on ExactUtf8Literals");
            return 1;
        }

        var literals = regex.Inspection.SearchPlan.MultiLiteralSearch.Literals;
        var searchPlan = regex.Inspection.SearchPlan;
        var direct = new PreparedLiteralSetSearch(literals);
        var rootByte = PreparedMultiLiteralCandidatePrefilter.CreateRootByte(literals);
        var earliest = PreparedMultiLiteralCandidatePrefilter.CreateEarliest(literals);
        var rareByte = PreparedMultiLiteralCandidatePrefilter.CreateRareByte(literals);
        var leadingUtf8Segment = PreparedMultiLiteralCandidatePrefilter.CreateLeadingUtf8Segment(literals);
        var offsetMask = PreparedMultiLiteralCandidatePrefilter.CreateOffsetMask(literals);
        var nibbleMask = literals.Length <= 64
            ? PreparedMultiLiteralCandidatePrefilter.CreateNibbleMask(literals)
            : default;
        var packedNibbleSimd = literals.Length <= 8
            ? PreparedMultiLiteralCandidatePrefilter.CreatePackedNibbleSimd(literals)
            : default;
        var hasPacked = PreparedMultiLiteralPackedSearch.TryCreate(literals, out var packed);
        var trie = PreparedMultiLiteralTrieSearch.Create(literals);
        var automaton = PreparedMultiLiteralAutomatonSearch.Create(literals);
        var diagnostics = regex.CollectCountDiagnostics(benchmarkCase.InputBytes);
        var leadingUtf8SegmentHybrid512 = leadingUtf8Segment.HasValue ? DiagnoseHybridChunkedLiteralSetCount(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 512) : default;
        var leadingUtf8SegmentHybrid2048 = leadingUtf8Segment.HasValue ? DiagnoseHybridChunkedLiteralSetCount(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 2048) : default;
        var rareHybrid512 = rareByte.HasValue ? DiagnoseHybridChunkedLiteralSetCount(rareByte, automaton, benchmarkCase.InputBytes, 512) : default;
        var rareHybrid2048 = rareByte.HasValue ? DiagnoseHybridChunkedLiteralSetCount(rareByte, automaton, benchmarkCase.InputBytes, 2048) : default;
        var offsetMaskHybrid512 = offsetMask.HasValue ? DiagnoseHybridChunkedLiteralSetCount(offsetMask, automaton, benchmarkCase.InputBytes, 512) : default;
        var offsetMaskHybrid2048 = offsetMask.HasValue ? DiagnoseHybridChunkedLiteralSetCount(offsetMask, automaton, benchmarkCase.InputBytes, 2048) : default;
        var nibbleMaskHybrid512 = nibbleMask.HasValue ? DiagnoseHybridChunkedLiteralSetCount(nibbleMask, automaton, benchmarkCase.InputBytes, 512) : default;
        var nibbleMaskHybrid2048 = nibbleMask.HasValue ? DiagnoseHybridChunkedLiteralSetCount(nibbleMask, automaton, benchmarkCase.InputBytes, 2048) : default;
        var packedNibbleSimdHybrid512 = packedNibbleSimd.HasValue ? DiagnoseHybridChunkedLiteralSetCount(packedNibbleSimd, automaton, benchmarkCase.InputBytes, 512) : default;
        var packedNibbleSimdHybrid2048 = packedNibbleSimd.HasValue ? DiagnoseHybridChunkedLiteralSetCount(packedNibbleSimd, automaton, benchmarkCase.InputBytes, 2048) : default;

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"LiteralCount      : {literals.Length}");
        Console.WriteLine($"PreparedKind      : {regex.Inspection.SearchPlan.MultiLiteralSearch.Kind}");
        Console.WriteLine($"PortfolioKind     : {regex.Inspection.SearchPortfolioKind}");
        Console.WriteLine($"HasBoundaries     : {searchPlan.HasBoundaryRequirements}");
        Console.WriteLine($"LeadingBoundary   : {searchPlan.LeadingBoundary}");
        Console.WriteLine($"TrailingBoundary  : {searchPlan.TrailingBoundary}");
        Console.WriteLine($"PackedAvailable   : {hasPacked}");
        Console.WriteLine($"LeadingUtf8SegmentAvail: {leadingUtf8Segment.HasValue}");
        Console.WriteLine($"RareByteAvail     : {rareByte.HasValue}");
        Console.WriteLine($"OffsetMaskAvail   : {offsetMask.HasValue}");
        Console.WriteLine($"NibbleMaskAvail   : {nibbleMask.HasValue}");
        Console.WriteLine($"PackedNibbleSimdAvail: {packedNibbleSimd.HasValue}");
        Console.WriteLine($"AutomatonStates   : {automaton.StateCount}");
        Console.WriteLine($"AutomatonRootFanout: {automaton.RootFanout}");
        Console.WriteLine($"AutomatonOutputs  : {automaton.OutputStateCount}");
        WriteCountDiagnostics("Utf8", diagnostics);
        if (leadingUtf8Segment.HasValue)
        {
            WriteHybridMetrics("LeadingUtf8Seg512", leadingUtf8SegmentHybrid512);
            WriteHybridMetrics("LeadingUtf8Seg2048", leadingUtf8SegmentHybrid2048);
        }
        if (rareByte.HasValue)
        {
            WriteHybridMetrics("RareHybrid512", rareHybrid512);
            WriteHybridMetrics("RareHybrid2048", rareHybrid2048);
        }
        if (offsetMask.HasValue)
        {
            WriteHybridMetrics("OffsetMask512", offsetMaskHybrid512);
            WriteHybridMetrics("OffsetMask2048", offsetMaskHybrid2048);
        }
        if (nibbleMask.HasValue)
        {
            WriteHybridMetrics("NibbleMask512", nibbleMaskHybrid512);
            WriteHybridMetrics("NibbleMask2048", nibbleMaskHybrid2048);
        }
        if (packedNibbleSimd.HasValue)
        {
            WriteHybridMetrics("PackedNibbleSimd512", packedNibbleSimdHybrid512);
            WriteHybridMetrics("PackedNibbleSimd2048", packedNibbleSimdHybrid2048);
        }

        Measure("ExactDirectCandidates", iterations, () => ExecuteExactLiteralSetCount(direct, benchmarkCase.InputBytes));
        Measure("ExactDirectBoundaryCount", iterations, () => ExecuteExactLiteralSetCountWithBoundaries(direct, searchPlan, benchmarkCase.InputBytes));
        Measure("RootByteCandidates", iterations, () => ExecuteCandidatePrefilterCount(rootByte, benchmarkCase.InputBytes));
        Measure("RootByteBoundaryCount", iterations, () => ExecuteCandidatePrefilterCountWithBoundaries(rootByte, searchPlan, benchmarkCase.InputBytes));
        if (leadingUtf8Segment.HasValue)
        {
            Measure("LeadingUtf8SegmentCandidates", iterations, () => ExecuteCandidatePrefilterCount(leadingUtf8Segment, benchmarkCase.InputBytes));
            Measure("LeadingUtf8SegmentProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 512));
            Measure("LeadingUtf8SegmentProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 2048));
            Measure("LeadingUtf8SegmentHybrid512", iterations, () => ExecuteHybridChunkedLiteralSetCount(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 512));
            Measure("LeadingUtf8SegmentHybrid2048", iterations, () => ExecuteHybridChunkedLiteralSetCount(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 2048));
            Measure("LeadingUtf8SegmentBoundaryCount", iterations, () => ExecuteCandidatePrefilterCountWithBoundaries(leadingUtf8Segment, searchPlan, benchmarkCase.InputBytes));
        }
        if (rareByte.HasValue)
        {
            Measure("RareByteCandidates", iterations, () => ExecuteCandidatePrefilterCount(rareByte, benchmarkCase.InputBytes));
            Measure("RareByteProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(rareByte, automaton, benchmarkCase.InputBytes, 512));
            Measure("RareByteProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(rareByte, automaton, benchmarkCase.InputBytes, 2048));
            Measure("RareByteHybrid512", iterations, () => ExecuteHybridChunkedLiteralSetCount(rareByte, automaton, benchmarkCase.InputBytes, 512));
            Measure("RareByteHybrid2048", iterations, () => ExecuteHybridChunkedLiteralSetCount(rareByte, automaton, benchmarkCase.InputBytes, 2048));
        }
        if (offsetMask.HasValue)
        {
            Measure("OffsetMaskCandidates", iterations, () => ExecuteCandidatePrefilterCount(offsetMask, benchmarkCase.InputBytes));
            Measure("OffsetMaskProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(offsetMask, automaton, benchmarkCase.InputBytes, 512));
            Measure("OffsetMaskProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(offsetMask, automaton, benchmarkCase.InputBytes, 2048));
            Measure("OffsetMaskHybrid512", iterations, () => ExecuteHybridChunkedLiteralSetCount(offsetMask, automaton, benchmarkCase.InputBytes, 512));
            Measure("OffsetMaskHybrid2048", iterations, () => ExecuteHybridChunkedLiteralSetCount(offsetMask, automaton, benchmarkCase.InputBytes, 2048));
        }
        if (nibbleMask.HasValue)
        {
            Measure("NibbleMaskCandidates", iterations, () => ExecuteCandidatePrefilterCount(nibbleMask, benchmarkCase.InputBytes));
            Measure("NibbleMaskProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(nibbleMask, automaton, benchmarkCase.InputBytes, 512));
            Measure("NibbleMaskProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(nibbleMask, automaton, benchmarkCase.InputBytes, 2048));
            Measure("NibbleMaskHybrid512", iterations, () => ExecuteHybridChunkedLiteralSetCount(nibbleMask, automaton, benchmarkCase.InputBytes, 512));
            Measure("NibbleMaskHybrid2048", iterations, () => ExecuteHybridChunkedLiteralSetCount(nibbleMask, automaton, benchmarkCase.InputBytes, 2048));
        }
        if (packedNibbleSimd.HasValue)
        {
            Measure("PackedNibbleSimdCandidates", iterations, () => ExecuteCandidatePrefilterCount(packedNibbleSimd, benchmarkCase.InputBytes));
            Measure("PackedNibbleSimdProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(packedNibbleSimd, automaton, benchmarkCase.InputBytes, 512));
            Measure("PackedNibbleSimdProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(packedNibbleSimd, automaton, benchmarkCase.InputBytes, 2048));
            Measure("PackedNibbleSimdHybrid512", iterations, () => ExecuteHybridChunkedLiteralSetCount(packedNibbleSimd, automaton, benchmarkCase.InputBytes, 512));
            Measure("PackedNibbleSimdHybrid2048", iterations, () => ExecuteHybridChunkedLiteralSetCount(packedNibbleSimd, automaton, benchmarkCase.InputBytes, 2048));
        }
        Measure("EarliestExactCandidates", iterations, () => ExecuteCandidatePrefilterCount(earliest, benchmarkCase.InputBytes));
        Measure("EarliestExactBoundaryCount", iterations, () => ExecuteCandidatePrefilterCountWithBoundaries(earliest, searchPlan, benchmarkCase.InputBytes));
        if (hasPacked)
        {
            Measure("PackedCandidates", iterations, () => ExecutePackedLiteralSetCount(packed, benchmarkCase.InputBytes));
            Measure("PackedBoundaryCount", iterations, () => ExecutePackedLiteralSetCountWithBoundaries(packed, searchPlan, benchmarkCase.InputBytes));
        }
        Measure("TrieCandidates", iterations, () => ExecuteTrieLiteralSetCount(trie, benchmarkCase.InputBytes));
        Measure("TrieBoundaryCount", iterations, () => ExecuteTrieLiteralSetCountWithBoundaries(trie, searchPlan, benchmarkCase.InputBytes));
        Measure("AutomatonCandidates", iterations, () => ExecuteAutomatonLiteralSetCount(automaton, benchmarkCase.InputBytes));
        Measure("AutomatonBoundaryCount", iterations, () => ExecuteAutomatonLiteralSetCountWithBoundaries(automaton, searchPlan, benchmarkCase.InputBytes));
        Measure("PreparedSearcherBoundaryCount", iterations, () => ExecutePreparedSearcherCountWithBoundaries(searchPlan.PreparedSearcher, searchPlan, benchmarkCase.InputBytes));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
        return 0;
    }

    public static int RunMeasureCompiledStructuralReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var iterations = ParseIterations(iterationsText);
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options | RegexOptions.Compiled);

        if (benchmarkCase.Utf8Regex.Inspection.CompiledEngineKind != Utf8CompiledEngineKind.StructuralLinearAutomaton ||
            compiledUtf8Regex.Inspection.CompiledEngineKind != Utf8CompiledEngineKind.StructuralLinearAutomaton)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"BaselineEngine    : {benchmarkCase.Utf8Regex.Inspection.CompiledEngineKind}");
            Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
            Console.WriteLine("StructuralCompiled: case is not on StructuralLinearAutomaton for both routes");
            return 1;
        }

        var baselineDiagnostics = benchmarkCase.Utf8Regex.CollectCountDiagnostics(benchmarkCase.InputBytes);
        var compiledDiagnostics = compiledUtf8Regex.CollectCountDiagnostics(benchmarkCase.InputBytes);

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"BaselineEngine    : {benchmarkCase.Utf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"BaselineBackend   : {benchmarkCase.Utf8Regex.Inspection.CompiledExecutionBackend}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledBackend   : {compiledUtf8Regex.Inspection.CompiledExecutionBackend}");
        Console.WriteLine($"ExecutionKind     : {benchmarkCase.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledExecKind  : {compiledUtf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"ProgramKind       : {benchmarkCase.Utf8Regex.Inspection.StructuralLinearProgramKind}");
        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        Measure("ValidationOnly", iterations, () => ExecuteValidationOnly(benchmarkCase.InputBytes));
        Measure("WellFormedOnly", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(benchmarkCase.InputBytes);
            return benchmarkCase.InputBytes.Length;
        });
        Measure("BaselineDirect", iterations, () => benchmarkCase.Utf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("CompiledDirect", iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
        Measure("CompiledRegex", iterations, benchmarkCase.CountPredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureExactLiteralFamilyPackedOffsetsReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        var iterations = ParseIterations(iterationsText);
        if (regex.Inspection.ExecutionKind != NativeExecutionKind.ExactUtf8Literals)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine("LiteralFamily     : case is not on ExactUtf8Literals");
            return 1;
        }

        var literals = regex.Inspection.SearchPlan.MultiLiteralSearch.Literals;
        var shortestLength = literals.Min(static literal => literal.Length);

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"LiteralCount      : {literals.Length}");
        Console.WriteLine($"ShortestLength    : {shortestLength}");

        for (var offset = 0; offset < shortestLength; offset++)
        {
            if (!PreparedMultiLiteralPackedSearch.TryCreateAtDiscriminatorOffset(literals, offset, out var packed))
            {
                continue;
            }

            Console.WriteLine($"PackedOffset      : {offset}");
            Console.WriteLine($"PackedValues      : {FormatByteSet(packed.DiscriminatorSearch.Values)}");
            Measure($"PackedOffset{offset}Candidates", iterations, () => ExecutePackedLiteralSetCount(packed, benchmarkCase.InputBytes));
            Measure($"PackedOffset{offset}BoundaryCount", iterations, () => ExecutePackedLiteralSetCountWithBoundaries(packed, regex.Inspection.SearchPlan, benchmarkCase.InputBytes));
        }

        Measure("PreparedSearcherBoundaryCount", iterations, () => ExecutePreparedSearcherCountWithBoundaries(regex.Inspection.SearchPlan.PreparedSearcher, regex.Inspection.SearchPlan, benchmarkCase.InputBytes));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
        Measure("CompiledRegex", iterations, benchmarkCase.CountPredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureExactLiteralFamilyIsMatchReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        var iterations = ParseIterations(iterationsText);
        if (regex.Inspection.ExecutionKind != NativeExecutionKind.ExactUtf8Literals)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine("LiteralFamily     : case is not on ExactUtf8Literals");
            return 1;
        }

        var decoded = Encoding.UTF8.GetString(benchmarkCase.InputBytes);

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"LiteralCount      : {regex.Inspection.SearchPlan.MultiLiteralSearch.Literals.Length}");
        Console.WriteLine($"PreparedKind      : {regex.Inspection.SearchPlan.MultiLiteralSearch.Kind}");
        Console.WriteLine($"PortfolioKind     : {regex.Inspection.SearchPortfolioKind}");
        Console.WriteLine($"HasBoundaries     : {regex.Inspection.SearchPlan.HasBoundaryRequirements}");
        Console.WriteLine($"LeadingBoundary   : {regex.Inspection.SearchPlan.LeadingBoundary}");
        Console.WriteLine($"TrailingBoundary  : {regex.Inspection.SearchPlan.TrailingBoundary}");

        Measure("Utf8Regex", iterations, () => regex.IsMatch(benchmarkCase.InputBytes) ? 1 : 0);
        Measure("DecodeThenRegex", iterations, () => Regex.IsMatch(Encoding.UTF8.GetString(benchmarkCase.InputBytes), benchmarkCase.Pattern, benchmarkCase.Options) ? 1 : 0);
        Measure("PredecodedRegex", iterations, () => benchmarkCase.Regex.IsMatch(decoded) ? 1 : 0);
        return 0;
    }

    public static int RunMeasureExactLiteralFamilyEnumeratorReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        var iterations = ParseIterations(iterationsText);
        if (regex.Inspection.ExecutionKind != NativeExecutionKind.ExactUtf8Literals)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine("LiteralFamily     : case is not on ExactUtf8Literals");
            return 1;
        }

        var decoded = Encoding.UTF8.GetString(benchmarkCase.InputBytes);

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"LiteralCount      : {regex.Inspection.SearchPlan.MultiLiteralSearch.Literals.Length}");
        Console.WriteLine($"PreparedKind      : {regex.Inspection.SearchPlan.MultiLiteralSearch.Kind}");
        Console.WriteLine($"PortfolioKind     : {regex.Inspection.SearchPortfolioKind}");
        Console.WriteLine($"HasBoundaries     : {regex.Inspection.SearchPlan.HasBoundaryRequirements}");
        Console.WriteLine($"LeadingBoundary   : {regex.Inspection.SearchPlan.LeadingBoundary}");
        Console.WriteLine($"TrailingBoundary  : {regex.Inspection.SearchPlan.TrailingBoundary}");

        Measure("SearchOnlyCount", iterations, () => ExecuteExactUtf8LiteralFamilySearchCount(regex.Inspection.SearchPlan, benchmarkCase.InputBytes));
        Measure("DirectEnumeratorMoveNext", iterations, () => ExecuteDirectExactUtf8LiteralFamilyEnumeratorMoveNext(regex.Inspection.SearchPlan, benchmarkCase.InputBytes));
        Measure("DirectEnumeratorIndexSum", iterations, () => ExecuteDirectExactUtf8LiteralFamilyEnumeratorIndexSum(regex.Inspection.SearchPlan, benchmarkCase.InputBytes));
        Measure("DirectFamilyIncrementalIndexSum", iterations, () => ExecuteExactUtf8LiteralFamilyDirectIncrementalIndexSum(regex.Inspection.SearchPlan, benchmarkCase.InputBytes));
        Measure("BoundaryMapIndexSum", iterations, () => ExecuteExactUtf8LiteralFamilyBoundaryMapIndexSum(regex.Inspection.SearchPlan, benchmarkCase.InputBytes));
        Measure("Utf8Regex", iterations, () => ExecuteReplicaUtf8EnumerateMatchIndexSum(benchmarkCase));
        Measure("DecodeThenRegex", iterations, () => ExecuteReplicaDecodeEnumerateMatchIndexSum(benchmarkCase));
        Measure("PredecodedRegex", iterations, () => ExecuteReplicaPredecodedEnumerateMatchIndexSum(benchmarkCase, decoded));
        return 0;
    }

    public static int RunMeasureExactLiteralFamilyHybridReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        var iterations = ParseIterations(iterationsText);
        if (regex.Inspection.ExecutionKind != NativeExecutionKind.ExactUtf8Literals)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine("LiteralFamily     : case is not on ExactUtf8Literals");
            return 1;
        }

        var searchPlan = regex.Inspection.SearchPlan;
        if (searchPlan.HasBoundaryRequirements || searchPlan.HasTrailingLiteralRequirement)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"PreparedKind      : {searchPlan.MultiLiteralSearch.Kind}");
            Console.WriteLine("HybridFamily      : only supported for no-boundary exact literal families");
            return 1;
        }

        var literals = searchPlan.MultiLiteralSearch.Literals;
        var direct = new PreparedLiteralSetSearch(literals);
        var rootByte = PreparedMultiLiteralCandidatePrefilter.CreateRootByte(literals);
        var earliest = PreparedMultiLiteralCandidatePrefilter.CreateEarliest(literals);
        var automaton = PreparedMultiLiteralAutomatonSearch.Create(literals);
        var span = benchmarkCase.InputBytes.AsSpan();
        var hybrid128 = DiagnoseHybridChunkedLiteralSetCount(direct, automaton, span, 128);
        var hybrid512 = DiagnoseHybridChunkedLiteralSetCount(direct, automaton, span, 512);
        var hybrid2048 = DiagnoseHybridChunkedLiteralSetCount(direct, automaton, span, 2048);
        var hybrid8192 = DiagnoseHybridChunkedLiteralSetCount(direct, automaton, span, 8192);
        var rootByteHybrid512 = DiagnoseHybridChunkedLiteralSetCount(rootByte, automaton, span, 512);
        var rootByteHybrid2048 = DiagnoseHybridChunkedLiteralSetCount(rootByte, automaton, span, 2048);
        var earliestHybrid512 = DiagnoseHybridChunkedLiteralSetCount(earliest, automaton, span, 512);
        var earliestHybrid2048 = DiagnoseHybridChunkedLiteralSetCount(earliest, automaton, span, 2048);

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"LiteralCount      : {literals.Length}");
        Console.WriteLine($"PreparedKind      : {searchPlan.MultiLiteralSearch.Kind}");
        Console.WriteLine($"PortfolioKind     : {regex.Inspection.SearchPortfolioKind}");
        Console.WriteLine($"AutomatonStates   : {automaton.StateCount}");
        Console.WriteLine($"AutomatonRootFanout: {automaton.RootFanout}");
        Console.WriteLine($"AutomatonOutputs  : {automaton.OutputStateCount}");
        WriteHybridMetrics("Hybrid128", hybrid128);
        WriteHybridMetrics("Hybrid512", hybrid512);
        WriteHybridMetrics("Hybrid2048", hybrid2048);
        WriteHybridMetrics("Hybrid8192", hybrid8192);
        WriteHybridMetrics("RootByteHybrid512", rootByteHybrid512);
        WriteHybridMetrics("RootByteHybrid2048", rootByteHybrid2048);
        WriteHybridMetrics("EarliestHybrid512", earliestHybrid512);
        WriteHybridMetrics("EarliestHybrid2048", earliestHybrid2048);

        Measure("ExactDirect", iterations, () => ExecuteExactLiteralSetCount(direct, benchmarkCase.InputBytes));
        Measure("RootByte", iterations, () => ExecuteCandidatePrefilterCount(rootByte, benchmarkCase.InputBytes));
        Measure("EarliestExact", iterations, () => ExecuteCandidatePrefilterCount(earliest, benchmarkCase.InputBytes));
        Measure("Automaton", iterations, () => ExecuteAutomatonLiteralSetCount(automaton, benchmarkCase.InputBytes));
        Measure("HybridProbe128", iterations, () => ExecuteHybridChunkedLiteralSetProbe(direct, automaton, benchmarkCase.InputBytes, 128));
        Measure("HybridProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(direct, automaton, benchmarkCase.InputBytes, 512));
        Measure("HybridProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(direct, automaton, benchmarkCase.InputBytes, 2048));
        Measure("HybridProbe8192", iterations, () => ExecuteHybridChunkedLiteralSetProbe(direct, automaton, benchmarkCase.InputBytes, 8192));
        Measure("RootByteProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(rootByte, automaton, benchmarkCase.InputBytes, 512));
        Measure("RootByteProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(rootByte, automaton, benchmarkCase.InputBytes, 2048));
        Measure("EarliestProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(earliest, automaton, benchmarkCase.InputBytes, 512));
        Measure("EarliestProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(earliest, automaton, benchmarkCase.InputBytes, 2048));
        Measure("RootByteHybrid512", iterations, () => ExecuteHybridChunkedLiteralSetCount(rootByte, automaton, benchmarkCase.InputBytes, 512));
        Measure("RootByteHybrid2048", iterations, () => ExecuteHybridChunkedLiteralSetCount(rootByte, automaton, benchmarkCase.InputBytes, 2048));
        Measure("EarliestHybrid512", iterations, () => ExecuteHybridChunkedLiteralSetCount(earliest, automaton, benchmarkCase.InputBytes, 512));
        Measure("EarliestHybrid2048", iterations, () => ExecuteHybridChunkedLiteralSetCount(earliest, automaton, benchmarkCase.InputBytes, 2048));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
        return 0;
    }

    public static int RunInspectExactLiteralFamilyReplicaCase(string caseId)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        if (regex.Inspection.ExecutionKind != NativeExecutionKind.ExactUtf8Literals)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
            Console.WriteLine("LiteralFamily     : case is not on ExactUtf8Literals");
            return 1;
        }

        var multi = regex.Inspection.SearchPlan.MultiLiteralSearch;
        var exact = multi.Kind == PreparedMultiLiteralKind.ExactDirect
            ? multi.ExactSearch
            : new PreparedLiteralSetSearch(multi.Literals);

        WriteReplicaCaseHeader(benchmarkCase, iterations: null);
        Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
        Console.WriteLine($"PreparedKind      : {multi.Kind}");
        Console.WriteLine($"ExactStrategy     : {exact.Strategy}");
        Console.WriteLine($"BucketCount       : {exact.SearchData.Buckets.Length}");
        Console.WriteLine($"LiteralCount      : {multi.Literals.Length}");
        Console.WriteLine($"ShortestLength    : {exact.SearchData.ShortestLength}");
        if (multi.Kind == PreparedMultiLiteralKind.ExactAutomaton)
        {
            Console.WriteLine($"AutomatonStates   : {multi.AutomatonSearch.StateCount}");
            Console.WriteLine($"AutomatonRootFanout: {multi.AutomatonSearch.RootFanout}");
            Console.WriteLine($"AutomatonOutputs  : {multi.AutomatonSearch.OutputStateCount}");
        }
        else if (multi.Kind == PreparedMultiLiteralKind.ExactPacked)
        {
            var packed = multi.PackedSearch;
            Console.WriteLine($"PackedOffset      : {packed.DiscriminatorOffset}");
            Console.WriteLine($"PackedValues      : {FormatByteSet(packed.DiscriminatorSearch.Values)}");
            Console.WriteLine($"PackedSecondGuard : {packed.HasSecondByteAnchor}");
            Console.WriteLine($"PackedThirdGuard  : {packed.HasThirdByteAnchor}");
        }

        for (var i = 0; i < exact.SearchData.Buckets.Length; i++)
        {
            var bucket = exact.SearchData.Buckets[i];
            var prefixText = bucket.CommonPrefix.Length == 0
                ? "<none>"
                : Encoding.ASCII.GetString(bucket.CommonPrefix);
            Console.WriteLine($"Bucket[{i}]        : first={(char)bucket.FirstByte} literals={bucket.Literals.Length} prefix={prefixText} prefixLen={bucket.CommonPrefix.Length} prefixDiscriminator={bucket.PrefixDiscriminator.HasValue}");
        }

        return 0;
    }

    public static int RunMeasureByteSafeRebarCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = DotNetPerformanceReplicaBenchmarkCatalog.Get(caseId);
        var context = new DotNetPerformanceReplicaBenchmarkContext(benchmarkCase);
        var analysis = Utf8FrontEnd.Compile(context.Pattern, benchmarkCase.Options);
        var regexPlan = analysis;
        if (Utf8CompiledEngineSelector.Select(regexPlan).Kind != Utf8CompiledEngineKind.ByteSafeLinear)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"CompiledEngine    : {Utf8CompiledEngineSelector.Select(regexPlan).Kind}");
            Console.WriteLine("ByteSafe          : not a byte-safe linear/lazy-DFA case");
            return 1;
        }

        var verifierRuntime = Utf8VerifierRuntime.Create(regexPlan, context.Pattern, benchmarkCase.Options, Regex.InfiniteMatchTimeout);
        var input = context.InputBytes;
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"VerifierKind      : {regexPlan.StructuralVerifier.Kind}");
        Console.WriteLine($"HasAnchor         : {regexPlan.DeterministicAnchor.HasValue}");
        Console.WriteLine($"HasStructuralPlan : {regexPlan.StructuralSearchPlan.HasValue}");

        Measure("AnchorCandidates", iterations, () => ExecuteByteSafeAnchorCandidateCount(regexPlan, input));
        Measure("AnchorCandidateSum", iterations, () => ExecuteByteSafeAnchorCandidateIndexSum(regexPlan, input));
        Measure("StatefulAnchorCandidates", iterations, () => ExecuteByteSafeStatefulAnchorCandidateCount(regexPlan, input));
        Measure("StatefulAnchorCandidateSum", iterations, () => ExecuteByteSafeStatefulAnchorCandidateIndexSum(regexPlan, input));
        Measure("StructuralCandidates", iterations, () => ExecuteByteSafeStructuralCandidateCount(regexPlan, input));
        Measure("StructuralCandidateSum", iterations, () => ExecuteByteSafeStructuralCandidateIndexSum(regexPlan, input));
        Measure("VerifierCount", iterations, () => ExecuteByteSafeVerifierCount(regexPlan, verifierRuntime, input));
        Measure("VerifierIndexSum", iterations, () => ExecuteByteSafeVerifierIndexSum(regexPlan, verifierRuntime, input));
        Measure("Utf8Regex", iterations, () => context.Utf8Regex.Count(input));
        Measure("DotNetRegex", iterations, () => context.Regex.Count(context.InputString));
        return 0;
    }

    public static int RunMeasureBoundedRepeatReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var iterations = ParseIterations(iterationsText);
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options | RegexOptions.Compiled);
        var baselineDiagnostics = benchmarkCase.Utf8Regex.CollectCountDiagnostics(benchmarkCase.InputBytes);
        var compiledDiagnostics = compiledUtf8Regex.CollectCountDiagnostics(benchmarkCase.InputBytes);

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"BaselineEngine    : {benchmarkCase.Utf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"ExecutionKind     : {benchmarkCase.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledExecKind  : {compiledUtf8Regex.Inspection.ExecutionKind}");
        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        Measure("ValidationOnly", iterations, () => ExecuteValidationOnly(benchmarkCase.InputBytes));
        Measure("WellFormedOnly", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(benchmarkCase.InputBytes);
            return benchmarkCase.InputBytes.Length;
        });
        Measure("BaselineDirect", iterations, () => benchmarkCase.Utf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("CompiledDirect", iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(benchmarkCase.InputBytes));
        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
        Measure("CompiledRegex", iterations, benchmarkCase.CountPredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureStructuralLinearScanCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = Utf8RegexBenchmarkCatalog.Get(caseId);
        var analysis = Utf8FrontEnd.Compile(benchmarkCase.Pattern, benchmarkCase.Options);
        if (!analysis.StructuralLinearProgram.DeterministicProgram.HasValue)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine("StructuralLinear  : no deterministic structural-linear program");
            return 1;
        }

        var input = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        var program = analysis.StructuralLinearProgram;
        var regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options);
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"ProgramKind       : {program.Kind}");
        Console.WriteLine($"Deterministic     : {program.DeterministicProgram.HasValue}");

        Measure("StructuralCount", iterations, () => ExecuteStructuralLinearCount(program, input));
        Measure("FixedWidthIndexSum", iterations, () => ExecuteStructuralLinearFixedWidthIndexSum(program, input));
        Measure("RawStructuralCount", iterations, () => ExecuteStructuralLinearRawCount(program, input));
        Measure("RawStructuralScan", iterations, () => ExecuteStructuralLinearRawScan(program, input));
        Measure("EnumeratorMoveNext", iterations, () => ExecuteStructuralLinearEnumeratorMoveNextCount(program, input));
        Measure("EnumeratorIndexSum", iterations, () => ExecuteStructuralLinearEnumeratorIndexSum(program, input));
        Measure("ValidationOnly", iterations, () => ExecuteValidationOnly(input));
        Measure("PublicMoveNext", iterations, () => ExecutePublicEnumeratorMoveNextCount(regex, input));
        Measure("PublicIndexSum", iterations, () => ExecutePublicEnumeratorIndexSum(regex, input));
        return 0;
    }

    public static int RunMeasureStructuralFamilyCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = Utf8RegexBenchmarkCatalog.Get(caseId);
        var analysis = Utf8FrontEnd.Compile(benchmarkCase.Pattern, benchmarkCase.Options);
        var program = analysis.StructuralLinearProgram;
        if (program.Kind != Utf8StructuralLinearProgramKind.AsciiStructuralFamily)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ProgramKind       : {program.Kind}");
            Console.WriteLine("StructuralFamily  : no ascii structural-family structural-linear program");
            return 1;
        }

        var input = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        var validation = Utf8InputAnalyzer.ValidateOnly(input);
        if (!validation.IsAscii)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine("StructuralFamily  : input is not ASCII");
            return 1;
        }

        var regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options);
        var context = new Utf8RegexBenchmarkContext(benchmarkCase);
        var verifierRuntime = Utf8VerifierRuntime.Create(analysis, benchmarkCase.Pattern, benchmarkCase.Options, Regex.InfiniteMatchTimeout);
        var linearRuntime = Utf8StructuralLinearRuntime.Create(program);
        var structuralFamilyRuntime = linearRuntime as Utf8AsciiStructuralFamilyLinearRuntime;
        _ = Utf8EmittedKernelMatcher.TryCreate(analysis, out var emittedKernelMatcher);
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"ProgramKind       : {program.Kind}");

        if (emittedKernelMatcher is not null)
        {
            Measure("EmittedCount", iterations, () => emittedKernelMatcher.Count(input));
            Measure("EmittedFindNextCount", iterations, () => ExecuteEmittedKernelFindNextCount(emittedKernelMatcher, input));
            Measure("EmittedFindNextSum", iterations, () => ExecuteEmittedKernelFindNextIndexSum(emittedKernelMatcher, input));
        }
        Measure("NativeCount", iterations, () => ExecuteStructuralFamilyNativeCount(linearRuntime, verifierRuntime, input, validation));
        Measure("NativeFindNextCount", iterations, () => ExecuteStructuralFamilyFindNextCount(linearRuntime, verifierRuntime, input, validation));
        Measure("NativeFindNextSum", iterations, () => ExecuteStructuralFamilyFindNextIndexSum(linearRuntime, verifierRuntime, input, validation));
        if (structuralFamilyRuntime is not null && structuralFamilyRuntime.CanUseStatefulSearch())
        {
            Measure("StatefulCandidates", iterations, () => ExecuteStructuralFamilyStatefulCandidateCount(structuralFamilyRuntime, input));
            Measure("StatefulCandidateSum", iterations, () => ExecuteStructuralFamilyStatefulCandidateIndexSum(structuralFamilyRuntime, input));
            Measure("StatefulCount", iterations, () => ExecuteStructuralFamilyStatefulCount(structuralFamilyRuntime, verifierRuntime, input));
            Measure("StatefulFindNextCount", iterations, () => ExecuteStructuralFamilyStatefulFindNextCount(structuralFamilyRuntime, verifierRuntime, input));
            Measure("StatefulFindNextSum", iterations, () => ExecuteStructuralFamilyStatefulFindNextIndexSum(structuralFamilyRuntime, verifierRuntime, input));
        }
        Measure("PublicCount", iterations, () => regex.Count(input));
        Measure("PublicMoveNext", iterations, () => ExecutePublicEnumeratorMoveNextCount(regex, input));
        Measure("PublicIndexSum", iterations, () => ExecutePublicEnumeratorIndexSum(regex, input));
        Measure("PredecodedRegex", iterations, () => ExecutePredecodedRegex(context));
        return 0;
    }

    public static int RunMeasureStructuralFamilyLokadCodeCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaCodeBenchmarkCatalog.Get(caseId);
        var context = new LokadReplicaCodeBenchmarkContext(benchmarkCase);
        var analysis = Utf8FrontEnd.Compile(context.CompiledPattern, benchmarkCase.Options);
        var program = analysis.StructuralLinearProgram;
        if (program.Kind != Utf8StructuralLinearProgramKind.AsciiStructuralFamily)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ProgramKind       : {program.Kind}");
            Console.WriteLine("StructuralFamily  : no ascii structural-family structural-linear program");
            return 1;
        }

        var validation = Utf8InputAnalyzer.ValidateOnly(context.InputBytes);
        if (!validation.IsAscii)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine("StructuralFamily  : input is not ASCII");
            return 1;
        }

        var verifierRuntime = Utf8VerifierRuntime.Create(analysis, context.CompiledPattern, benchmarkCase.Options, Regex.InfiniteMatchTimeout);
        var linearRuntime = Utf8StructuralLinearRuntime.Create(program);
        var structuralFamilyRuntime = linearRuntime as Utf8AsciiStructuralFamilyLinearRuntime;
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"CompiledPattern   : {context.CompiledPattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"ProgramKind       : {program.Kind}");

        Measure("NativeCount", iterations, () => ExecuteStructuralFamilyNativeCount(linearRuntime, verifierRuntime, context.InputBytes, validation));
        Measure("NativeFindNextCount", iterations, () => ExecuteStructuralFamilyFindNextCount(linearRuntime, verifierRuntime, context.InputBytes, validation));
        Measure("NativeFindNextSum", iterations, () => ExecuteStructuralFamilyFindNextIndexSum(linearRuntime, verifierRuntime, context.InputBytes, validation));
        if (structuralFamilyRuntime is not null && structuralFamilyRuntime.CanUseStatefulSearch())
        {
            Measure("StatefulCandidates", iterations, () => ExecuteStructuralFamilyStatefulCandidateCount(structuralFamilyRuntime, context.InputBytes));
            Measure("StatefulCandidateSum", iterations, () => ExecuteStructuralFamilyStatefulCandidateIndexSum(structuralFamilyRuntime, context.InputBytes));
            Measure("StatefulCount", iterations, () => ExecuteStructuralFamilyStatefulCount(structuralFamilyRuntime, verifierRuntime, context.InputBytes));
            Measure("StatefulFindNextCount", iterations, () => ExecuteStructuralFamilyStatefulFindNextCount(structuralFamilyRuntime, verifierRuntime, context.InputBytes));
            Measure("StatefulFindNextSum", iterations, () => ExecuteStructuralFamilyStatefulFindNextIndexSum(structuralFamilyRuntime, verifierRuntime, context.InputBytes));
        }
        Measure("PublicCount", iterations, () => context.Utf8Regex.Count(context.InputBytes));
        Measure("PublicMoveNext", iterations, () => ExecutePublicEnumeratorMoveNextCount(context.Utf8Regex, context.InputBytes));
        Measure("PublicIndexSum", iterations, () => ExecutePublicEnumeratorIndexSum(context.Utf8Regex, context.InputBytes));
        Measure("PredecodedRegex", iterations, () => context.CountPredecodedRegex());
        return 0;
    }

    public static int RunMeasureIdentifierFamilyLokadCodeCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaCodeBenchmarkCatalog.Get(caseId);
        var context = new LokadReplicaCodeBenchmarkContext(benchmarkCase);
        var analysis = Utf8FrontEnd.Compile(context.CompiledPattern, benchmarkCase.Options);
        if (analysis.ExecutionKind != NativeExecutionKind.AsciiStructuralIdentifierFamily ||
            !analysis.SearchPlan.PreparedSearcher.HasValue)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
            Console.WriteLine("IdentifierFamily  : no prepared-search-backed ascii structural identifier family");
            return 1;
        }

        var plan = analysis.StructuralIdentifierFamilyPlan;
        var searchPlan = analysis.SearchPlan;
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"CompiledPattern   : {context.CompiledPattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
        Console.WriteLine($"PreparedKind      : {searchPlan.PreparedSearcher.Kind}");
        Console.WriteLine($"LeadingBoundary   : {plan.LeadingBoundary}");
        Console.WriteLine($"SeparatorSet      : {plan.SeparatorSet ?? "<none>"}");
        Console.WriteLine($"IdentifierStart   : {plan.IdentifierStartSet ?? "<none>"}");
        Console.WriteLine($"IdentifierTailMin : {plan.IdentifierTailMinCount}");
        Console.WriteLine($"IdentifierTailMax : {plan.IdentifierTailMaxCount}");

        Measure("PreparedOverlaps", iterations, () => ExecuteIdentifierFamilyPreparedOverlapCount(searchPlan, context.InputBytes));
        Measure("PreparedNonOverlaps", iterations, () => ExecuteIdentifierFamilyPreparedNonOverlappingCount(searchPlan, context.InputBytes));
        Measure("BoundaryFiltered", iterations, () => ExecuteIdentifierFamilyBoundaryFilteredCount(searchPlan, plan, context.InputBytes));
        Measure("BoundaryFilteredNonOverlaps", iterations, () => ExecuteIdentifierFamilyBoundaryFilteredNonOverlappingCount(searchPlan, plan, context.InputBytes));
        Measure("BoundaryIndexSum", iterations, () => ExecuteIdentifierFamilyBoundaryFilteredIndexSum(searchPlan, plan, context.InputBytes));
        Measure("IdentifierMatches", iterations, () => ExecuteIdentifierFamilyPreparedMatchCount(searchPlan, plan, context.InputBytes));
        Measure("IdentifierMatchesNonOverlaps", iterations, () => ExecuteIdentifierFamilyPreparedMatchNonOverlappingCount(searchPlan, plan, context.InputBytes));
        Measure("IdentifierIndexSum", iterations, () => ExecuteIdentifierFamilyPreparedMatchIndexSum(searchPlan, plan, context.InputBytes));
        Measure("PublicCount", iterations, () => context.Utf8Regex.Count(context.InputBytes));
        Measure("PredecodedRegex", iterations, () => context.CountPredecodedRegex());
        return 0;
    }

    public static int RunMeasureCompiledStructuralFamilyLokadCodeCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaCodeBenchmarkCatalog.Get(caseId);
        var context = new LokadReplicaCodeBenchmarkContext(benchmarkCase);
        var analysis = Utf8FrontEnd.Compile(context.CompiledPattern, benchmarkCase.Options);
        if (analysis.ExecutionKind != NativeExecutionKind.AsciiStructuralIdentifierFamily ||
            !analysis.SearchPlan.PreparedSearcher.HasValue)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
            Console.WriteLine("CompiledStructural: no prepared-search-backed ascii structural identifier family");
            return 1;
        }

        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options | RegexOptions.Compiled);
        var baselineDiagnostics = context.Utf8Regex.CollectCountDiagnostics(context.InputBytes);
        var compiledDiagnostics = compiledUtf8Regex.CollectCountDiagnostics(context.InputBytes);
        var plan = analysis.StructuralIdentifierFamilyPlan;
        var searchPlan = analysis.SearchPlan;
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"CompiledPattern   : {context.CompiledPattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
        Console.WriteLine($"BaselineEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        Measure("PreparedOverlaps", iterations, () => ExecuteIdentifierFamilyPreparedOverlapCount(searchPlan, context.InputBytes));
        Measure("PreparedNonOverlaps", iterations, () => ExecuteIdentifierFamilyPreparedNonOverlappingCount(searchPlan, context.InputBytes));
        Measure("BoundaryFiltered", iterations, () => ExecuteIdentifierFamilyBoundaryFilteredCount(searchPlan, plan, context.InputBytes));
        Measure("BoundaryFilteredNonOverlaps", iterations, () => ExecuteIdentifierFamilyBoundaryFilteredNonOverlappingCount(searchPlan, plan, context.InputBytes));
        Measure("FamilyMatches", iterations, () => ExecuteIdentifierFamilyPreparedMatchCount(searchPlan, plan, context.InputBytes));
        Measure("FamilyMatchesNonOverlaps", iterations, () => ExecuteIdentifierFamilyPreparedMatchNonOverlappingCount(searchPlan, plan, context.InputBytes));
        Measure("BaselineDirect", iterations, () => context.Utf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("CompiledDirect", iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("Utf8Regex", iterations, () => context.Utf8Regex.Count(context.InputBytes));
        Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(context.InputBytes));
        Measure("PredecodedRegex", iterations, () => context.CountPredecodedRegex());
        Measure("CompiledRegex", iterations, () => context.CountPredecodedCompiledRegex());
        return 0;
    }

    public static int RunMeasureCompiledOrderedWindowLokadCodeCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaCodeBenchmarkCatalog.Get(caseId);
        var context = new LokadReplicaCodeBenchmarkContext(benchmarkCase);
        var compiledUtf8Regex = new Utf8Regex(context.Pattern, benchmarkCase.Options | RegexOptions.Compiled);
        var analysis = Utf8FrontEnd.Compile(context.Pattern, benchmarkCase.Options);
        if (analysis.ExecutionKind != NativeExecutionKind.AsciiOrderedLiteralWindow)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
            Console.WriteLine("OrderedWindow     : case is not on AsciiOrderedLiteralWindow");
            return 1;
        }

        var plan = analysis.StructuralLinearProgram.OrderedLiteralWindowPlan;
        if (!AsciiOrderedLiteralWindowExecutor.CanUseSeparatorOnlySingleLiteralFastPath(plan))
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine("OrderedWindow     : case does not use separator-only single-literal fast path");
            return 1;
        }

        var iterations = ParseIterations(iterationsText);
        var baselineDiagnostics = context.Utf8Regex.CollectCountDiagnostics(context.InputBytes);
        var compiledDiagnostics = compiledUtf8Regex.CollectCountDiagnostics(context.InputBytes);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"CompiledPattern   : {context.CompiledPattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
        Console.WriteLine($"BaselineEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        Measure("TrailingCandidates", iterations, () => AsciiOrderedLiteralWindowExecutor.CountSeparatorOnlySingleLiteralTrailingCandidates(context.InputBytes, plan));
        Measure("SeparatorQualified", iterations, () => AsciiOrderedLiteralWindowExecutor.CountSeparatorOnlySingleLiteralSeparatorQualifiedCandidates(context.InputBytes, plan));
        Measure("LeadingMatches", iterations, () => AsciiOrderedLiteralWindowExecutor.CountSeparatorOnlySingleLiteralLeadingMatches(context.InputBytes, plan));
        Measure("BaselineDirect", iterations, () => context.Utf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("CompiledDirect", iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("Utf8Regex", iterations, () => context.Utf8Regex.Count(context.InputBytes));
        Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(context.InputBytes));
        Measure("PredecodedRegex", iterations, () => context.CountPredecodedRegex());
        Measure("CompiledRegex", iterations, () => context.CountPredecodedCompiledRegex());
        return 0;
    }

    public static int RunMeasureCompiledSingleLiteralOrderedWindowLokadCodeCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = LokadReplicaCodeBenchmarkCatalog.Get(caseId);
        var context = new LokadReplicaCodeBenchmarkContext(benchmarkCase);
        var compiledUtf8Regex = new Utf8Regex(context.Pattern, benchmarkCase.Options | RegexOptions.Compiled);
        var analysis = Utf8FrontEnd.Compile(context.Pattern, benchmarkCase.Options);
        if (analysis.ExecutionKind != NativeExecutionKind.AsciiOrderedLiteralWindow)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
            Console.WriteLine("OrderedWindow     : case is not on AsciiOrderedLiteralWindow");
            return 1;
        }

        var plan = analysis.StructuralLinearProgram.OrderedLiteralWindowPlan;
        if (plan.IsLiteralFamily)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine("OrderedWindow     : case is not on a single-literal ordered-window plan");
            return 1;
        }

        var iterations = ParseIterations(iterationsText);
        var baselineDiagnostics = context.Utf8Regex.CollectCountDiagnostics(context.InputBytes);
        var compiledDiagnostics = compiledUtf8Regex.CollectCountDiagnostics(context.InputBytes);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"CompiledPattern   : {context.CompiledPattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
        Console.WriteLine($"BaselineEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        Measure("TrailingCandidates", iterations, () => AsciiOrderedLiteralWindowExecutor.CountSingleLiteralTrailingBoundaryCandidates(context.InputBytes, plan));
        Measure("GapQualified", iterations, () => AsciiOrderedLiteralWindowExecutor.CountSingleLiteralGapQualifiedCandidates(context.InputBytes, plan));
        Measure("LeadingMatches", iterations, () => AsciiOrderedLiteralWindowExecutor.CountSingleLiteralLeadingQualifiedMatches(context.InputBytes, plan));
        Measure("BaselineDirect", iterations, () => context.Utf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("CompiledDirect", iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("Utf8Regex", iterations, () => context.Utf8Regex.Count(context.InputBytes));
        Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(context.InputBytes));
        Measure("PredecodedRegex", iterations, () => context.CountPredecodedRegex());
        Measure("CompiledRegex", iterations, () => context.CountPredecodedCompiledRegex());
        return 0;
    }

    public static int RunMeasureLokadPublicOrderedWindowCase(string caseId, string? iterationsText)
    {
        var context = new LokadPublicBenchmarkContext(caseId);
        var compiledUtf8Regex = context.CompiledUtf8Regex;
        var analysis = Utf8FrontEnd.Compile(context.Pattern, context.Options);
        if (analysis.ExecutionKind != NativeExecutionKind.AsciiOrderedLiteralWindow)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
            Console.WriteLine("OrderedWindow     : case is not on AsciiOrderedLiteralWindow");
            return 1;
        }

        var plan = analysis.StructuralLinearProgram.OrderedLiteralWindowPlan;
        var iterations = ParseIterations(iterationsText);
        var baselineDiagnostics = context.Utf8Regex.CollectCountDiagnostics(context.InputBytes);
        var compiledDiagnostics = compiledUtf8Regex.CollectCountDiagnostics(context.InputBytes);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {context.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
        Console.WriteLine($"BaselineEngine    : {context.Utf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"IsLiteralFamily   : {plan.IsLiteralFamily}");
        Console.WriteLine($"PairedTrailing    : {plan.HasPairedTrailingLiterals}");
        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        if (plan.IsLiteralFamily && plan.HasPairedTrailingLiterals && analysis.SearchPlan.AlternateLiteralSearch is { } familySearch)
        {
            Measure("LeadingCandidates", iterations, () => AsciiOrderedLiteralWindowExecutor.CountPairedLiteralFamilyLeadingCandidates(context.InputBytes, plan, familySearch));
            Measure("GapQualified", iterations, () => AsciiOrderedLiteralWindowExecutor.CountPairedLiteralFamilyGapQualifiedCandidates(context.InputBytes, plan, familySearch));
            Measure("LeadingMatches", iterations, () => AsciiOrderedLiteralWindowExecutor.CountPairedLiteralFamily(context.InputBytes, plan, familySearch, budget: Utf8ExecutionDeadline.Infinite));
            Measure("TrailingAnchorMatches", iterations, () => AsciiOrderedLiteralWindowExecutor.CountPairedLiteralFamilyTrailingAnchorMatches(context.InputBytes, plan, familySearch));
        }
        else
        {
            Measure("TrailingCandidates", iterations, () => AsciiOrderedLiteralWindowExecutor.CountSingleLiteralTrailingBoundaryCandidates(context.InputBytes, plan));
            Measure("GapQualified", iterations, () => AsciiOrderedLiteralWindowExecutor.CountSingleLiteralGapQualifiedCandidates(context.InputBytes, plan));
            Measure("LeadingMatches", iterations, () => AsciiOrderedLiteralWindowExecutor.CountSingleLiteralLeadingQualifiedMatches(context.InputBytes, plan));
        }
        Measure("BaselineDirect", iterations, () => context.Utf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("CompiledDirect", iterations, () => compiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("CompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureSymmetricWindowLokadPublicCase(string caseId, string? iterationsText)
    {
        var context = new LokadPublicBenchmarkContext(caseId);
        if (context.Operation != LokadPublicBenchmarkOperation.Count ||
            context.Utf8Regex.Inspection.ExecutionKind != NativeExecutionKind.AsciiSimplePattern ||
            !context.Utf8Regex.Inspection.PreparedRegex.SimplePatternPlan.SymmetricLiteralWindowPlan.HasValue)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine($"Operation         : {context.Operation}");
            Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
            Console.WriteLine("SymmetricWindow   : case is not on a symmetric simple-pattern window");
            return 1;
        }

        var plan = context.Utf8Regex.Inspection.PreparedRegex.SimplePatternPlan.SymmetricLiteralWindowPlan;
        var iterations = ParseIterations(iterationsText);
        var baselineDiagnostics = context.Utf8Regex.CollectCountDiagnostics(context.InputBytes);
        var compiledDiagnostics = context.CompiledUtf8Regex.CollectCountDiagnostics(context.InputBytes);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {context.Operation}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {context.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"InputChars        : {context.InputString.Length}");
        Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {context.CompiledUtf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"AnchorOffset      : {plan.AnchorOffset}");
        Console.WriteLine($"AnchorBytes       : {(char)plan.AnchorByteA}|{(char)plan.AnchorByteB}");
        Console.WriteLine($"GapRange          : {plan.MinGap}..{plan.MaxGap}");
        Console.WriteLine($"GapSameLine       : {plan.GapSameLine}");
        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        Measure("AnchorCandidates", iterations, () => Utf8AsciiSymmetricLiteralWindowExecutor.CountAnchorCandidates(context.InputBytes, plan));
        Measure("FilterQualified", iterations, () => Utf8AsciiSymmetricLiteralWindowExecutor.CountFilterQualifiedCandidates(context.InputBytes, plan));
        Measure("LeadingMatches", iterations, () => Utf8AsciiSymmetricLiteralWindowExecutor.CountLeadingLiteralMatches(context.InputBytes, plan));
        Measure("Utf8Direct", iterations, () => context.Utf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("Utf8CompiledDirect", iterations, () => context.CompiledUtf8Regex.Inspection.DebugCountViaCompiledEngine(context.InputBytes));
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureStructuralLinearRebarCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = DotNetPerformanceReplicaBenchmarkCatalog.Get(caseId);
        var context = new DotNetPerformanceReplicaBenchmarkContext(benchmarkCase);
        var analysis = Utf8FrontEnd.Compile(context.Pattern, benchmarkCase.Options);
        var program = analysis.StructuralLinearProgram;
        if (!program.HasValue)
        {
            Console.WriteLine($"CaseId            : {caseId}");
            Console.WriteLine("StructuralLinear  : no structural-linear program");
            return 1;
        }

        var validation = Utf8InputAnalyzer.ValidateOnly(context.InputBytes);
        var verifierRuntime = Utf8VerifierRuntime.Create(analysis, context.Pattern, benchmarkCase.Options, Regex.InfiniteMatchTimeout);
        var linearRuntime = Utf8StructuralLinearRuntime.Create(program);
        var iterations = ParseIterations(iterationsText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"ProgramKind       : {program.Kind}");
        Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");

        if (program.Kind == Utf8StructuralLinearProgramKind.AsciiLiteralFamilyRun)
        {
            Measure("LiteralFamilyCandidates", iterations, () => ExecuteLiteralFamilyRunCandidateCount(program, context.InputBytes));
            Measure("LiteralFamilyCandidateSum", iterations, () => ExecuteLiteralFamilyRunCandidateIndexSum(program, context.InputBytes));
        }

        Measure("ValidationOnly", iterations, () => ExecuteValidationOnly(context.InputBytes));
        Measure("WellFormedOnly", iterations, () =>
        {
            Utf8Validation.ThrowIfInvalidOnly(context.InputBytes);
            return context.InputBytes.Length;
        });
        Measure("NativeCount", iterations, () => ExecuteStructuralFamilyNativeCount(linearRuntime, verifierRuntime, context.InputBytes, validation));
        Measure("NativeFindNextCount", iterations, () => ExecuteStructuralFamilyFindNextCount(linearRuntime, verifierRuntime, context.InputBytes, validation));
        Measure("NativeFindNextSum", iterations, () => ExecuteStructuralFamilyFindNextIndexSum(linearRuntime, verifierRuntime, context.InputBytes, validation));
        Measure("Utf8Regex", iterations, () => context.Utf8Regex.Count(context.InputBytes));
        Measure("DecodeThenRegex", iterations, () => context.CountDecodeThenRegex());
        Measure("PredecodedRegex", iterations, () => context.CountPredecodedRegex());
        return 0;
    }

    public static int RunDumpLazyDfaPattern(string pattern, string? optionsText)
    {
        var options = ParseRegexOptions(optionsText);
        var regex = new Utf8Regex(pattern, options);
        WriteLazyDfaDump(pattern, options, regex);
        return 0;
    }

    public static int RunDumpRuntimeTreePattern(string pattern, string? optionsText)
    {
        var options = ParseRegexOptions(optionsText);
        var analysis = Utf8FrontEnd.Compile(pattern, options);
        Console.WriteLine($"Pattern           : {pattern}");
        Console.WriteLine($"Options           : {options}");
        Console.WriteLine($"ExecutionBackend  : {analysis.ExecutionBackend}");
        Console.WriteLine($"ExecutionKind     : {analysis.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {Utf8CompiledEngineSelector.Select(analysis).Kind}");
        Console.WriteLine($"FallbackReason    : {analysis.FallbackReason ?? "<native>"}");
        Console.WriteLine("RuntimeTree       :");
        Console.WriteLine(DumpNode(analysis.SemanticRegex.RuntimeTree!.Root));
        return 0;
    }

    private static bool TryResolveCasePattern(string caseId, out string pattern, out RegexOptions options, out string origin)
    {
        pattern = string.Empty;
        options = RegexOptions.None;
        origin = string.Empty;

        var utf8Case = Utf8RegexBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (utf8Case is not null)
        {
            pattern = utf8Case.Pattern;
            options = utf8Case.Options;
            origin = $"utf8/{utf8Case.Operation}/{utf8Case.Family}";
            return true;
        }

        var dotNetPerformanceCase = DotNetPerformanceReplicaBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (dotNetPerformanceCase is not null)
        {
            pattern = dotNetPerformanceCase.Pattern ?? string.Empty;
            options = dotNetPerformanceCase.Options;
            origin = $"dotnet-performance/{dotNetPerformanceCase.Origin}";
            return true;
        }

        var siftCase = LokadReplicaCodeBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (siftCase is not null)
        {
            pattern = siftCase.Pattern;
            options = siftCase.Options;
            origin = $"lokad-code/{siftCase.Group}/{siftCase.PatternMode}";
            return true;
        }

        var envisionCase = LokadReplicaScriptBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (envisionCase is not null)
        {
            pattern = envisionCase.Pattern;
            options = envisionCase.Utf8Options;
            origin = $"lokad/{envisionCase.Group}/{envisionCase.Model}";
            return true;
        }

        return false;
    }

    private static bool IsLiteralFamilyExecutionKind(NativeExecutionKind executionKind)
        => executionKind is NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals;

    private static void WriteGeneratedRegexDump(string? caseId, string origin, string pattern, RegexOptions options)
    {
        var effectiveOptions = options & ~RegexOptions.Compiled;
        var tempRoot = Path.Combine(Path.GetTempPath(), "utf8regex-dotnet-generated-regex", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var generatedDir = Path.Combine(tempRoot, "Generated");
            Directory.CreateDirectory(generatedDir);

            var projectText =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
                  </PropertyGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(tempRoot, "probe.csproj"), projectText, Encoding.UTF8);

            var optionsLiteral = BuildGeneratedRegexOptionsLiteral(effectiveOptions);
            var probeText =
                $$"""
                using System.Text.RegularExpressions;
                partial class Probe
                {
                    [GeneratedRegex(@"{{EscapeForVerbatimString(pattern)}}", {{optionsLiteral}})]
                    private static partial Regex R();
                }
                """;
            File.WriteAllText(Path.Combine(tempRoot, "Probe.cs"), probeText, Encoding.UTF8);

            var psi = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = tempRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("build");
            psi.ArgumentList.Add("--tl:off");
            psi.ArgumentList.Add("--nologo");
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("minimal");
            psi.ArgumentList.Add("probe.csproj");

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start dotnet build for regex generator probe.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Regex generator probe build failed.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
            }

            var generatedFile = Directory.GetFiles(generatedDir, "*.cs", SearchOption.AllDirectories)
                .OrderByDescending(static p => p.Length)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Regex generator probe did not produce any generated C# files.");

            Console.WriteLine($"CaseId            : {caseId ?? "<pattern>"}");
            Console.WriteLine($"Origin            : {origin}");
            Console.WriteLine($"Pattern           : {pattern}");
            Console.WriteLine($"Options           : {effectiveOptions}");
            Console.WriteLine($"GeneratedFile     : {generatedFile}");
            Console.WriteLine("GeneratedRegexCSharp");
            Console.WriteLine("```csharp");
            Console.WriteLine(File.ReadAllText(generatedFile, Encoding.UTF8).TrimEnd());
            Console.WriteLine("```");
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best effort cleanup only.
            }
        }
    }

    private static string BuildGeneratedRegexOptionsLiteral(RegexOptions options)
    {
        if (options == RegexOptions.None)
        {
            return "RegexOptions.None";
        }

        var names = Enum.GetValues<RegexOptions>()
            .Where(value => value != RegexOptions.None && options.HasFlag(value))
            .Select(static value => $"RegexOptions.{value}")
            .ToArray();
        return names.Length == 0 ? "RegexOptions.None" : string.Join(" | ", names);
    }

    private static string EscapeForVerbatimString(string value)
    {
        return value.Replace("\"", "\"\"");
    }

    public static int RunDumpStructuralLinearPattern(string pattern, string? optionsText)
    {
        var options = ParseRegexOptions(optionsText);
        var analysis = Utf8FrontEnd.Compile(pattern, options);
        WriteStructuralLinearDump(pattern, options, analysis.StructuralLinearProgram);
        return 0;
    }

    public static int RunDumpStructuralLinearUtf8Case(string caseId)
    {
        var benchmarkCase = Utf8RegexBenchmarkCatalog.Get(caseId);
        var analysis = Utf8FrontEnd.Compile(benchmarkCase.Pattern, benchmarkCase.Options);
        WriteStructuralLinearDump(benchmarkCase.Pattern, benchmarkCase.Options, analysis.StructuralLinearProgram, caseId, $"{benchmarkCase.Operation}/{benchmarkCase.Family}");
        return 0;
    }

    public static int RunDumpStructuralSearchRebarCase(string caseId)
    {
        var benchmarkCase = DotNetPerformanceReplicaBenchmarkCatalog.Get(caseId);
        var context = new DotNetPerformanceReplicaBenchmarkContext(benchmarkCase);
        WriteStructuralSearchDump(caseId, benchmarkCase.Origin, context.Pattern, benchmarkCase.Options, context.Utf8Regex.Inspection.StructuralSearchPlan);
        return 0;
    }

    public static int RunDumpLazyDfaRebarCase(string caseId)
    {
        var benchmarkCase = DotNetPerformanceReplicaBenchmarkCatalog.Get(caseId);
        var context = new DotNetPerformanceReplicaBenchmarkContext(benchmarkCase);
        WriteLazyDfaDump(context.Pattern, benchmarkCase.Options, context.Utf8Regex, caseId, benchmarkCase.Origin);
        return 0;
    }

    private static void WriteRegexInspection(
        string sourceKind,
        string pattern,
        RegexOptions options,
        int? inputChars,
        int? inputBytes,
        string? origin,
        Utf8Regex regex)
    {
        Console.WriteLine($"SourceKind        : {sourceKind}");
        if (origin is not null)
        {
            Console.WriteLine($"Origin            : {origin}");
        }

        Console.WriteLine($"Pattern           : {pattern}");
        Console.WriteLine($"Options           : {options}");
        if (inputChars is not null)
        {
            Console.WriteLine($"InputChars        : {inputChars}");
        }

        if (inputBytes is not null)
        {
            Console.WriteLine($"InputBytes        : {inputBytes}");
        }

        Console.WriteLine($"CompiledEngine    : {regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"ExecutionKind     : {regex.Inspection.ExecutionKind}");
        Console.WriteLine($"HasAsciiTwin      : {regex.Inspection.DebugHasAsciiCultureInvariantTwin}");
        if (regex.Inspection.DebugHasAsciiCultureInvariantTwin)
        {
            Console.WriteLine($"AsciiTwinEngine   : {regex.Inspection.DebugAsciiCultureInvariantTwinCompiledEngineKind}");
            Console.WriteLine($"AsciiTwinKind     : {regex.Inspection.DebugAsciiCultureInvariantTwinExecutionKind}");
            Console.WriteLine($"AsciiTwinFallback : {regex.Inspection.DebugAsciiCultureInvariantTwinFallbackReason ?? "<native>"}");
        }
        Console.WriteLine($"DirectFamily      : {regex.Inspection.DebugFallbackDirectFamilyKind}");
        Console.WriteLine($"SearchKind        : {regex.Inspection.SearchPlan.Kind}");
        Console.WriteLine($"PortfolioKind     : {regex.Inspection.SearchPortfolioKind}");
        Console.WriteLine($"PreparedSearcher  : {regex.Inspection.SearchPlan.PreparedSearcher.Kind}");
        Console.WriteLine($"RequiredPrefilter : {(regex.Inspection.SearchPlan.RequiredPrefilterSearcher.HasValue ? regex.Inspection.SearchPlan.RequiredPrefilterSearcher.Kind : "None")}");
        Console.WriteLine($"WindowSearch      : {(regex.Inspection.SearchPlan.WindowSearch.HasValue ? "Present" : "None")}");
        Console.WriteLine($"StructuralYield   : {regex.Inspection.StructuralSearchPlan.YieldKind}");
        Console.WriteLine($"StructuralStages  : {regex.Inspection.StructuralSearchPlan.Stages?.Length ?? 0}");
        if (regex.Inspection.StructuralSearchPlan.Stages is { Length: > 0 } structuralStages)
        {
            for (var i = 0; i < structuralStages.Length; i++)
            {
                Console.WriteLine($"StructuralStage[{i}] : {FormatStructuralSearchStage(structuralStages[i])}");
            }
        }
        if (regex.Inspection.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily)
        {
            var diagnostics = regex.Inspection.DebugStructuralSharedPrefixSuffixKernelDiagnostics;
            Console.WriteLine($"StructuralSharedPrefixBuckets      : {diagnostics.BucketCount}");
            Console.WriteLine($"StructuralSharedPrefixLength       : {diagnostics.CommonPrefixLength}");
            Console.WriteLine($"StructuralSharedPrefixDiscriminator: {diagnostics.HasPrefixDiscriminator}");
            Console.WriteLine($"StructuralSharedPrefixDiscOffset   : {diagnostics.PrefixDiscriminatorOffset?.ToString() ?? "None"}");
            Console.WriteLine($"StructuralSuffixLiteralLength      : {diagnostics.SuffixLiteralLength}");
            Console.WriteLine($"StructuralSeparatorMinCount        : {diagnostics.SeparatorMinCount}");
            Console.WriteLine($"StructuralWhitespaceSeparator      : {diagnostics.HasAsciiWhitespaceSeparatorClass}");
            Console.WriteLine($"StructuralLeadingBoundary          : {diagnostics.LeadingBoundary}");
            Console.WriteLine($"StructuralTrailingBoundary         : {diagnostics.TrailingBoundary}");
            Console.WriteLine($"StructuralSharedPrefixKernel       : {diagnostics.CanUseSharedPrefixSuffixKernelSpec}");
            Console.WriteLine($"StructuralSharedPrefixFamilyKernel : {diagnostics.CanUseSharedPrefixSuffixLiteralFamilyKernelSpec}");
            Console.WriteLine($"StructuralWhitespaceSuffixKernel   : {diagnostics.CanUseAsciiWhitespaceSingleByteSuffixKernel}");
        }
        Console.WriteLine($"FallbackPlans     : {regex.Inspection.SearchPlan.FallbackCandidatePlans?.Length ?? 0}");
        if (regex.Inspection.SearchPlan.FallbackCandidatePlans is { Length: > 0 } fallbackPlans)
        {
            for (var i = 0; i < fallbackPlans.Length; i++)
            {
                var plan = fallbackPlans[i];
                Console.WriteLine($"FallbackPlan[{i}]  : Yield={plan.YieldKind}, Stages={plan.Stages?.Length ?? 0}");
            }
        }
        Console.WriteLine($"Verifier          : {DescribeVerifier(regex)}");
        Console.WriteLine($"FallbackReason    : {regex.Inspection.FallbackReason ?? "<native>"}");
    }

    private static void WriteLazyDfaDump(
        string pattern,
        RegexOptions options,
        Utf8Regex regex,
        string? caseId = null,
        string? origin = null)
    {
        var linear = regex.Inspection.StructuralVerifierPlan.ByteSafeLinearProgram;
        var lazy = regex.Inspection.StructuralVerifierPlan.ByteSafeLazyDfaProgram;

        if (caseId is not null)
        {
            Console.WriteLine($"CaseId            : {caseId}");
        }

        if (origin is not null)
        {
            Console.WriteLine($"Origin            : {origin}");
        }

        Console.WriteLine($"Pattern           : {pattern}");
        Console.WriteLine($"Options           : {options}");
        Console.WriteLine($"CompiledEngine    : {regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"VerifierKind      : {regex.Inspection.StructuralVerifierPlan.Kind}");
        Console.WriteLine($"LazyDfaHasValue   : {lazy.HasValue}");
        Console.WriteLine($"LazyDfaReject     : {Utf8ByteSafeLazyDfaVerifierProgram.Compile(linear).FailureKind}");
        Console.WriteLine($"LazyDfaStates     : {lazy.StateCount}");
        Console.WriteLine($"LazyDfaTransitions: {lazy.TransitionCount}");
        Console.WriteLine($"LinearStepCount   : {linear.Steps.Length}");

        for (var i = 0; i < linear.Steps.Length; i++)
        {
            Console.WriteLine($"Step[{i}]          : {FormatLinearStep(linear.Steps[i])}");
        }
    }

    private static void WriteStructuralLinearDump(
        string pattern,
        RegexOptions options,
        Utf8StructuralLinearProgram program,
        string? caseId = null,
        string? origin = null)
    {
        var deterministic = program.DeterministicProgram;

        if (caseId is not null)
        {
            Console.WriteLine($"CaseId            : {caseId}");
        }

        if (origin is not null)
        {
            Console.WriteLine($"Origin            : {origin}");
        }

        Console.WriteLine($"Pattern           : {pattern}");
        Console.WriteLine($"Options           : {options}");
        Console.WriteLine($"ProgramKind       : {program.Kind}");
        Console.WriteLine($"Deterministic     : {deterministic.HasValue}");
        if (deterministic.HasValue)
        {
            Console.WriteLine($"SearchOffset      : {deterministic.SearchLiteralOffset}");
            Console.WriteLine($"SearchLiterals    : {FormatByteLiterals(deterministic.SearchLiterals)}");
            Console.WriteLine($"FixedWidthLength  : {deterministic.FixedWidthLength}");
            Console.WriteLine($"FixedChecks       : {FormatFixedLiteralChecks(deterministic.FixedLiteralChecks)}");
            Console.WriteLine($"EndAnchored       : {deterministic.IsEndAnchored}");
            Console.WriteLine($"IgnoreCase        : {deterministic.IgnoreCase}");

            for (var i = 0; i < deterministic.FixedWidthChecks.Length; i++)
            {
                Console.WriteLine($"FixedWidth[{i}]    : {FormatFixedWidthCheck(deterministic.FixedWidthChecks[i])}");
            }

            for (var i = 0; i < deterministic.Steps.Length; i++)
            {
                Console.WriteLine($"Step[{i}]          : {FormatDeterministicStep(deterministic.Steps[i])}");
            }
        }
        else
        {
            if (program.Kind == Utf8StructuralLinearProgramKind.AsciiStructuralFamily)
            {
                var stages = program.StructuralSearchPlan.Stages ?? [];
                Console.WriteLine($"StructuralStages  : {stages.Length}");
                for (var i = 0; i < stages.Length; i++)
                {
                    Console.WriteLine($"Stage[{i}]         : {FormatStructuralSearchStage(stages[i])}");
                }
                return;
            }

            var instructionProgram = program.InstructionProgram;
            Console.WriteLine($"SearchOffset      : {instructionProgram.SearchLiteralOffset}");
            Console.WriteLine($"SearchLiterals    : {FormatByteLiterals(instructionProgram.SearchLiterals)}");
            Console.WriteLine($"FixedChecks       : {FormatFixedLiteralChecks(instructionProgram.FixedLiteralChecks)}");
            Console.WriteLine($"StartAnchored     : {instructionProgram.IsStartAnchored}");
            Console.WriteLine($"EndAnchored       : {instructionProgram.IsEndAnchored}");
            Console.WriteLine($"IgnoreCase        : {instructionProgram.IgnoreCase}");
            var instructions = instructionProgram.Instructions ?? [];
            Console.WriteLine($"InstructionCount  : {instructions.Length}");

            for (var i = 0; i < instructions.Length; i++)
            {
                Console.WriteLine($"Instruction[{i}]  : {FormatStructuralInstruction(instructions[i])}");
            }
        }
    }

    private static void WriteStructuralSearchDump(
        string caseId,
        string? origin,
        string pattern,
        RegexOptions options,
        Utf8StructuralSearchPlan plan)
    {
        Console.WriteLine($"CaseId            : {caseId}");
        if (origin is not null)
        {
            Console.WriteLine($"Origin            : {origin}");
        }

        Console.WriteLine($"Pattern           : {pattern}");
        Console.WriteLine($"Options           : {options}");
        Console.WriteLine($"YieldKind         : {plan.YieldKind}");
        var stages = plan.Stages ?? [];
        Console.WriteLine($"StructuralStages  : {stages.Length}");
        for (var i = 0; i < stages.Length; i++)
        {
            Console.WriteLine($"Stage[{i}]         : {FormatStructuralSearchStage(stages[i])}");
        }
    }

    private static string DescribeVerifier(Utf8Regex regex)
    {
        if (regex.Inspection.CompiledEngineKind == Utf8CompiledEngineKind.ByteSafeLinear)
        {
            if (regex.Inspection.StructuralVerifierPlan.ByteSafeLazyDfaProgram.HasValue)
            {
                var lazy = regex.Inspection.StructuralVerifierPlan.ByteSafeLazyDfaProgram;
                return $"CompiledByteSafeLazyDfa(states={lazy.StateCount}, transitions={lazy.TransitionCount})";
            }

            return regex.Inspection.StructuralVerifierPlan.ByteSafeLinearProgram.HasValue
                ? $"CompiledByteSafeLinear(reject={regex.Inspection.StructuralVerifierPlan.LazyDfaCompileOutcome.FailureKind})"
                : "CompatByteSafeLinear";
        }

        return regex.Inspection.StructuralVerifierPlan.Kind.ToString();
    }

    private static string FormatLinearStep(Utf8ByteSafeLinearVerifierStep step)
    {
        return step.Kind switch
        {
            Utf8ByteSafeLinearVerifierStepKind.MatchByte => $"MatchByte '{(char)step.Value}'",
            Utf8ByteSafeLinearVerifierStepKind.MatchText => $"MatchText \"{Encoding.UTF8.GetString(step.Text!)}\"",
            Utf8ByteSafeLinearVerifierStepKind.MatchSet => $"MatchSet {FormatOpaqueText(step.Set)}",
            Utf8ByteSafeLinearVerifierStepKind.MatchProjectedAsciiSet => $"MatchProjectedAsciiSet {FormatOpaqueText(step.Set)}",
            Utf8ByteSafeLinearVerifierStepKind.LoopByte => $"LoopByte '{(char)step.Value}' min={step.Min} max={FormatMax(step.Max)}",
            Utf8ByteSafeLinearVerifierStepKind.LoopText => $"LoopText \"{Encoding.UTF8.GetString(step.Text!)}\" min={step.Min} max={FormatMax(step.Max)}",
            Utf8ByteSafeLinearVerifierStepKind.LoopSet => $"LoopSet {FormatOpaqueText(step.Set)} min={step.Min} max={FormatMax(step.Max)}",
            Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet => $"LoopProjectedAsciiSet {FormatOpaqueText(step.Set)} min={step.Min} max={FormatMax(step.Max)}",
            Utf8ByteSafeLinearVerifierStepKind.RequireBeginning => "RequireBeginning",
            Utf8ByteSafeLinearVerifierStepKind.RequireEnd => "RequireEnd",
            Utf8ByteSafeLinearVerifierStepKind.RequireBoundary => "RequireBoundary",
            Utf8ByteSafeLinearVerifierStepKind.RequireNonBoundary => "RequireNonBoundary",
            Utf8ByteSafeLinearVerifierStepKind.MatchAnyText => $"MatchAnyText [{FormatAlternatives(step.Alternatives!)}]",
            Utf8ByteSafeLinearVerifierStepKind.MatchAnyTextOptional => $"MatchAnyTextOptional [{FormatAlternatives(step.Alternatives!)}]",
            Utf8ByteSafeLinearVerifierStepKind.LoopAnyText => $"LoopAnyText [{FormatAlternatives(step.Alternatives!)}] min={step.Min} max={FormatMax(step.Max)}",
            Utf8ByteSafeLinearVerifierStepKind.LoopProgram => $"LoopProgram steps={step.Program?.Length ?? 0} min={step.Min} max={FormatMax(step.Max)}",
            Utf8ByteSafeLinearVerifierStepKind.Accept => "Accept",
            _ => step.Kind.ToString(),
        };
    }

    private static string FormatDeterministicStep(Utf8AsciiDeterministicStep step)
    {
        return step.Kind switch
        {
            Utf8AsciiDeterministicStepKind.Literal => $"Literal '{(char)step.Literal}'",
            Utf8AsciiDeterministicStepKind.AnyByte => "AnyByte",
            Utf8AsciiDeterministicStepKind.CharClass => $"CharClass {step.CharClass}",
            Utf8AsciiDeterministicStepKind.RunCharClass => $"RunCharClass {step.CharClass} min={step.MinCount} max={FormatMax(step.MaxCount)}",
            Utf8AsciiDeterministicStepKind.Accept => "Accept",
            _ => step.Kind.ToString(),
        };
    }

    private static string FormatFixedWidthCheck(Utf8AsciiDeterministicFixedWidthCheck check)
    {
        return check.Kind switch
        {
            Utf8AsciiDeterministicFixedWidthCheckKind.Literal => $"Literal '{(char)check.Literal}'",
            Utf8AsciiDeterministicFixedWidthCheckKind.AnyByte => "AnyByte",
            Utf8AsciiDeterministicFixedWidthCheckKind.CharClass => $"CharClass {check.CharClass}",
            _ => check.Kind.ToString(),
        };
    }

    private static string FormatStructuralInstruction(Utf8StructuralLinearInstruction instruction)
    {
        return instruction.Kind switch
        {
            Utf8StructuralLinearInstructionKind.Literal => $"Literal '{(char)instruction.Literal}'",
            Utf8StructuralLinearInstructionKind.AnyByte => "AnyByte",
            Utf8StructuralLinearInstructionKind.CharClass => $"CharClass {instruction.CharClass}",
            Utf8StructuralLinearInstructionKind.RunCharClass => $"RunCharClass {instruction.CharClass} min={instruction.MinCount} max={FormatMax(instruction.MaxCount)}",
            Utf8StructuralLinearInstructionKind.RepeatedSegment => "RepeatedSegment",
            Utf8StructuralLinearInstructionKind.TokenWindow => "TokenWindow",
            Utf8StructuralLinearInstructionKind.QuotedRelation => "QuotedRelation",
            Utf8StructuralLinearInstructionKind.LiteralFamilyRun => "LiteralFamilyRun",
            Utf8StructuralLinearInstructionKind.Accept => "Accept",
            _ => instruction.Kind.ToString(),
        };
    }

    private static string FormatStructuralSearchStage(Utf8StructuralSearchStage stage)
    {
        return stage.Kind switch
        {
            Utf8StructuralSearchStageKind.FindLiteralFamily => $"FindLiteralFamily {stage.Searcher.Kind}",
            Utf8StructuralSearchStageKind.FindWindow => "FindWindow",
            Utf8StructuralSearchStageKind.TransformCandidateStart => $"TransformStart {stage.StartTransform.Kind}",
            Utf8StructuralSearchStageKind.RequireByteAtOffset => stage.LiteralByte is byte b
                ? $"RequireByteAtOffset @{stage.ByteOffset}='{(char)b}'"
                : $"RequireSetAtOffset @{stage.ByteOffset}={FormatOpaqueText(stage.Set)}",
            Utf8StructuralSearchStageKind.RequireLiteralAtOffset => $"RequireLiteralAtOffset @{stage.ByteOffset}=\"{Encoding.UTF8.GetString(stage.LiteralUtf8 ?? [])}\"",
            Utf8StructuralSearchStageKind.RequireMinLength => $"RequireMinLength {stage.MinLength}",
            Utf8StructuralSearchStageKind.RequireWithinByteSpan => $"RequireWithinByteSpan {stage.MaxSpan}",
            Utf8StructuralSearchStageKind.RequireWithinLineSpan => $"RequireWithinLineSpan {stage.MaxLines}",
            Utf8StructuralSearchStageKind.RequireLeadingBoundary => $"RequireLeadingBoundary {stage.BoundaryRequirement}",
            Utf8StructuralSearchStageKind.RequireTrailingBoundary => $"RequireTrailingBoundary {stage.BoundaryRequirement}",
            Utf8StructuralSearchStageKind.RequireTrailingLiteral => $"RequireTrailingLiteral \"{Encoding.UTF8.GetString(stage.LiteralUtf8 ?? [])}\"",
            Utf8StructuralSearchStageKind.RequireExactLength => $"RequireExactLength {stage.MinLength}",
            Utf8StructuralSearchStageKind.BoundMaxLength => $"BoundMaxLength {stage.MaxSpan}",
            Utf8StructuralSearchStageKind.YieldStart => "YieldStart",
            Utf8StructuralSearchStageKind.YieldWindow => "YieldWindow",
            _ => stage.Kind.ToString(),
        };
    }

    private static string FormatByteLiterals(byte[][]? literals)
    {
        if (literals is null || literals.Length == 0)
        {
            return "[]";
        }

        return "[" + string.Join(", ", literals.Select(static bytes => $"\"{Encoding.UTF8.GetString(bytes)}\"")) + "]";
    }

    private static string FormatFixedLiteralChecks(AsciiFixedLiteralCheck[]? checks)
    {
        if (checks is null || checks.Length == 0)
        {
            return "[]";
        }

        return "[" + string.Join(", ", checks.Select(static check => $"@{check.Offset}=\"{Encoding.UTF8.GetString(check.Literal)}\"")) + "]";
    }

    private static string FormatAlternatives(byte[][] alternatives) =>
        string.Join(", ", alternatives.Select(static bytes => $"\"{Encoding.UTF8.GetString(bytes)}\""));

    private static string FormatOpaqueText(string? text)
    {
        if (text is null)
        {
            return "<null>";
        }

        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            builder.Append(ch switch
            {
                '\0' => "\\0",
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                '\\' => "\\\\",
                _ when char.IsControl(ch) => $"\\u{(int)ch:x4}",
                _ => ch,
            });
        }

        return builder.ToString();
    }

    private static string FormatMax(int max) => max < 0 || max == int.MaxValue ? "open" : max.ToString();

    private static string DumpNode(RegexNode node, int depth = 0)
    {
        var indent = new string(' ', depth * 2);
        var line = $"{indent}{node.Kind} ch={node.Ch} str={node.Str ?? "<null>"} m={node.M} n={node.N} children={node.ChildCount}";
        if (node.ChildCount == 0)
        {
            return line;
        }

        return line + Environment.NewLine + string.Join(Environment.NewLine, node.ChildList.Select(child => DumpNode(child, depth + 1)));
    }

    private static string BuildReplicaOrigin(ReplicaCountBenchmarkCase benchmarkCase)
    {
        if (benchmarkCase.Source == ReplicaBenchmarkSource.DotNetPerformance)
        {
            return benchmarkCase.Origin ?? benchmarkCase.Group ?? benchmarkCase.Id;
        }

        return $"{benchmarkCase.Group}/{benchmarkCase.Intent}/{benchmarkCase.PatternMode}";
    }

    private static void WriteReplicaCaseHeader(ReplicaCountBenchmarkCase benchmarkCase, int? iterations)
    {
        Console.WriteLine($"CaseId            : {benchmarkCase.Id}");
        Console.WriteLine($"Source            : {benchmarkCase.Source}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        if (iterations is int count)
        {
            Console.WriteLine($"Iterations        : {count}");
        }

        if (benchmarkCase.PatternMode is not null)
        {
            Console.WriteLine($"PatternMode       : {benchmarkCase.PatternMode}");
        }

        if (benchmarkCase.Group is not null)
        {
            Console.WriteLine($"Group             : {benchmarkCase.Group}");
        }

        if (benchmarkCase.Intent is not null)
        {
            Console.WriteLine($"Intent            : {benchmarkCase.Intent}");
        }

        if (benchmarkCase.Origin is not null)
        {
            Console.WriteLine($"Origin            : {benchmarkCase.Origin}");
        }

        Console.WriteLine($"InputBytes        : {benchmarkCase.InputBytes.Length}");
    }

    private static RegexOptions ParseRegexOptions(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return RegexOptions.None;
        }

        if (int.TryParse(text, out var numeric))
        {
            return (RegexOptions)numeric;
        }

        var options = RegexOptions.None;
        foreach (var part in text.Split(['|', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            options |= Enum.Parse<RegexOptions>(part, ignoreCase: true);
        }

        return options;
    }

    private static int ParseIterations(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 100;
        }

        return int.TryParse(text, out var iterations) && iterations > 0 ? iterations : 100;
    }

    private static int ParseShortIterations(string? text)
    {
        return Math.Max(50, ParseIterations(text));
    }

    private static int ParseLokadPrefixIterations(string? text)
    {
        return Math.Max(20000, ParseIterations(text));
    }

    private static int ParseShortPublicIterations(LokadPublicBenchmarkContext context, string? text)
    {
        int floor;
        if (context.Operation is LokadPublicBenchmarkOperation.IsMatch or LokadPublicBenchmarkOperation.Match)
        {
            floor = context.CaseId.StartsWith("common/", StringComparison.Ordinal)
                ? 10000
                : context.CaseId.StartsWith("industry/boostdocs-", StringComparison.Ordinal)
                    ? 5000
                    : 2000;
        }
        else if (context.Operation is LokadPublicBenchmarkOperation.Replace or LokadPublicBenchmarkOperation.Split)
        {
            floor = context.CaseId.StartsWith("common/", StringComparison.Ordinal) ? 2000 : 1000;
        }
        else
        {
            floor = 500;
        }

        return Math.Max(floor, ParseIterations(text));
    }

    private static int ParseReadmePublicIterations(LokadPublicBenchmarkContext context, string? text)
    {
        return ParseShortPublicIterations(context, text);
    }

    private static int ParseReadmeLokadScriptIterations(LokadReplicaScriptBenchmarkCase benchmarkCase, string? text)
    {
        return benchmarkCase.Model == LokadReplicaScriptBenchmarkModel.PrefixMatchLoop
            ? ParseLokadPrefixIterations(text)
            : ParseIterations(text);
    }

    private static int ParseReadmeReplicaIterations(ReplicaCountBenchmarkCase benchmarkCase, string? text)
    {
        var requested = ParseIterations(text);
        var inputBytes = benchmarkCase.InputBytes.Length;

        if (inputBytes <= 128 * 1024)
        {
            if (benchmarkCase.Group is "literal" or "literal-family" or "structural")
            {
                return Math.Max(20000, requested);
            }

            return Math.Max(10000, requested);
        }

        if (inputBytes <= 512 * 1024)
        {
            return Math.Max(5000, requested);
        }

        return requested;
    }

    private static int ParseSamples(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 5;
        }

        return int.TryParse(text, out var samples) && samples > 0 ? samples : 5;
    }

    private static double MeasureMedianMicroseconds(int samples, int iterations, Func<int> action)
        => MeasureMedianCore(samples, iterations, action).Elapsed.TotalMicroseconds / iterations;

    private static string FormatMicros(double value) => $"{value:N3} us";

    private static ReadmeCaseMeasurement MeasureReadmeCase(
        int iterations,
        int samples,
        Func<int> utf8,
        Func<int> utf8Compiled,
        Func<int> predecoded,
        Func<int> compiledRegex,
        Func<int> decodeThenRegex,
        Func<int> decodeThenCompiledRegex)
        => new(
            MeasureMedianMicroseconds(samples, iterations, utf8),
            MeasureMedianMicroseconds(samples, iterations, utf8Compiled),
            MeasureMedianMicroseconds(samples, iterations, predecoded),
            MeasureMedianMicroseconds(samples, iterations, compiledRegex),
            MeasureMedianMicroseconds(samples, iterations, decodeThenRegex),
            MeasureMedianMicroseconds(samples, iterations, decodeThenCompiledRegex));

    private static ReadmeCaseMeasurement MeasureReadmeCaseOutOfProcess(string command, string caseId, int iterations, int samples)
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
        psi.ArgumentList.Add(command);
        psi.ArgumentList.Add(caseId);
        psi.ArgumentList.Add(iterations.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(samples.ToString(CultureInfo.InvariantCulture));

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Unable to start child process for {command} {caseId}.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Child process failed for {command} {caseId}: {output}");
        }

        return ParseReadmeCaseRow(output);
    }

    private static string FormatReadmeCaseRow(ReadmeCaseMeasurement row)
        => string.Create(CultureInfo.InvariantCulture, $"Utf8Regex={row.Utf8Regex:F3};Utf8Compiled={row.Utf8Compiled:F3};PredecodedRegex={row.PredecodedRegex:F3};CompiledRegex={row.CompiledRegex:F3};DecodeThenRegex={row.DecodeThenRegex:F3};DecodeThenCompiledRegex={row.DecodeThenCompiledRegex:F3}");

    private static ReadmeCaseMeasurement ParseReadmeCaseRow(string text)
    {
        double utf8 = 0;
        double utf8Compiled = 0;
        double predecoded = 0;
        double compiledRegex = 0;
        double decode = 0;
        double decodeCompiled = 0;

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length != 2)
            {
                continue;
            }

            var value = double.Parse(pieces[1], CultureInfo.InvariantCulture);
            switch (pieces[0])
            {
                case "Utf8Regex":
                    utf8 = value;
                    break;
                case "PredecodedRegex":
                    predecoded = value;
                    break;
                case "Utf8Compiled":
                    utf8Compiled = value;
                    break;
                case "CompiledRegex":
                    compiledRegex = value;
                    break;
                case "DecodeThenRegex":
                    decode = value;
                    break;
                case "DecodeThenCompiledRegex":
                    decodeCompiled = value;
                    break;
            }
        }

        return new ReadmeCaseMeasurement(utf8, utf8Compiled, predecoded, compiledRegex, decode, decodeCompiled);
    }

    private static readonly JsonSerializerOptions ReadmeBenchmarkSnapshotJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private sealed class ReadmeBenchmarkSnapshot
    {
        public int SchemaVersion { get; set; } = 2;

        public Dictionary<string, ReadmeBenchmarkSectionJson> Sections { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class ReadmeBenchmarkSectionJson
    {
        public Dictionary<string, ReadmeCaseMeasurementJson> Cases { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class ReadmeCaseMeasurementJson
    {
        public DateTimeOffset? MeasuredAtUtc { get; set; }

        public BenchmarkEnvironmentJson? Environment { get; set; }

        public double Utf8Regex { get; set; }

        public double Utf8Compiled { get; set; }

        public double PredecodedRegex { get; set; }

        public double CompiledRegex { get; set; }

        public double DecodeThenRegex { get; set; }

        public double DecodeThenCompiledRegex { get; set; }

        public static ReadmeCaseMeasurementJson FromMeasurement(ReadmeCaseMeasurement measurement)
            => new()
            {
                MeasuredAtUtc = DateTimeOffset.UtcNow,
                Environment = CaptureBenchmarkEnvironment(),
                Utf8Regex = measurement.Utf8Regex,
                Utf8Compiled = measurement.Utf8Compiled,
                PredecodedRegex = measurement.PredecodedRegex,
                CompiledRegex = measurement.CompiledRegex,
                DecodeThenRegex = measurement.DecodeThenRegex,
                DecodeThenCompiledRegex = measurement.DecodeThenCompiledRegex,
            };

        public ReadmeCaseMeasurement ToMeasurement()
            => new(Utf8Regex, Utf8Compiled, PredecodedRegex, CompiledRegex, DecodeThenRegex, DecodeThenCompiledRegex);
    }

    private readonly record struct ReadmeCaseMeasurement(double Utf8Regex, double Utf8Compiled, double PredecodedRegex, double CompiledRegex, double DecodeThenRegex, double DecodeThenCompiledRegex);
    private readonly record struct HybridLiteralFamilyMetrics(
        int Count,
        int Windows,
        int SkippedWindows,
        int PromotedWindows,
        int SkippedBytes,
        int PromotedBytes);
    private delegate bool HybridProbe<TProbe>(TProbe probe, ReadOnlySpan<byte> window, int chunkLimit);

    private static void WriteHybridMetrics(string label, HybridLiteralFamilyMetrics metrics)
    {
        Console.WriteLine(
            $"{label,-18}: approxCount={metrics.Count} windows={metrics.Windows} skipped={metrics.SkippedWindows} " +
            $"promoted={metrics.PromotedWindows} skippedBytes={metrics.SkippedBytes} promotedBytes={metrics.PromotedBytes}");
    }

    private static void WriteCountDiagnostics(string label, Utf8CountDiagnostics diagnostics)
    {
        Console.WriteLine($"{label}Route      : {diagnostics.ExecutionRoute}");
        Console.WriteLine($"{label}ExecKind   : {diagnostics.ExecutionKind}");
        Console.WriteLine($"{label}SearchKind : {diagnostics.SearchKind}");
        Console.WriteLine($"{label}Fallback   : {diagnostics.FallbackVerifierMode}");
        Console.WriteLine($"{label}Candidates : {diagnostics.SearchCandidates}");
        Console.WriteLine($"{label}VerifierInv: {diagnostics.VerifierInvocations}");
        Console.WriteLine($"{label}VerifierHit: {diagnostics.VerifierMatches}");
        Console.WriteLine($"{label}ProbeWin   : {diagnostics.PrefilterWindows}");
        Console.WriteLine($"{label}ProbeSkip  : {diagnostics.PrefilterSkippedWindows}");
        Console.WriteLine($"{label}ProbeProm  : {diagnostics.PrefilterPromotedWindows}");
        Console.WriteLine($"{label}SkipBytes  : {diagnostics.PrefilterSkippedBytes}");
        Console.WriteLine($"{label}PromBytes  : {diagnostics.PrefilterPromotedBytes}");
        Console.WriteLine($"{label}Demotions  : {diagnostics.EngineDemotions}");
    }

    private static void WriteIsMatchDiagnostics(string label, Utf8IsMatchDiagnostics diagnostics)
    {
        Console.WriteLine($"{label}Result     : {diagnostics.Result}");
        Console.WriteLine($"{label}ExecKind   : {diagnostics.ExecutionKind}");
        Console.WriteLine($"{label}SearchKind : {diagnostics.SearchKind}");
        Console.WriteLine($"{label}Fallback   : {diagnostics.FallbackVerifierMode}");
        Console.WriteLine($"{label}Candidates : {diagnostics.SearchCandidates}");
        Console.WriteLine($"{label}VerifierInv: {diagnostics.VerifierInvocations}");
        Console.WriteLine($"{label}VerifierHit: {diagnostics.VerifierMatches}");
        Console.WriteLine($"{label}ProbeWin   : {diagnostics.PrefilterWindows}");
        Console.WriteLine($"{label}ProbeSkip  : {diagnostics.PrefilterSkippedWindows}");
        Console.WriteLine($"{label}ProbeProm  : {diagnostics.PrefilterPromotedWindows}");
        Console.WriteLine($"{label}SkipBytes  : {diagnostics.PrefilterSkippedBytes}");
        Console.WriteLine($"{label}PromBytes  : {diagnostics.PrefilterPromotedBytes}");
        Console.WriteLine($"{label}Demotions  : {diagnostics.EngineDemotions}");
    }

    private static int ParseLokadPublicDeepIterations(LokadPublicBenchmarkContext context, string? iterationsText)
    {
        if (iterationsText is not null)
        {
            return ParseIterations(iterationsText);
        }

        return context.Operation switch
        {
            LokadPublicBenchmarkOperation.IsMatch => 20000,
            LokadPublicBenchmarkOperation.Match => 10000,
            _ => 50,
        };
    }

    private static int ParseValidatorDeepIterations(LokadPublicBenchmarkContext context, string? iterationsText)
    {
        if (iterationsText is not null)
        {
            return ParseIterations(iterationsText);
        }

        return context.Operation switch
        {
            LokadPublicBenchmarkOperation.IsMatch => 20000,
            LokadPublicBenchmarkOperation.Match => 10000,
            _ => 2000,
        };
    }

    private static string FormatByteSet(ReadOnlySpan<byte> values)
    {
        if (values.Length == 0)
        {
            return "<none>";
        }

        return string.Join(" ", values.ToArray().Select(static b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }
    private static void Measure(string label, int samples, int iterations, Func<int> action)
    {
        WriteMeasurementEnvironment();
        var measurement = MeasureMedianCore(samples, iterations, action);
        Console.WriteLine(
            $"{label,-18}: {measurement.Elapsed.TotalMilliseconds,10:F3} ms total | " +
            $"{measurement.Elapsed.TotalMicroseconds / iterations,10:F3} us/op | sink={measurement.Sink} | " +
            $"samples={samples} range={measurement.Minimum.TotalMicroseconds / iterations:F3}..{measurement.Maximum.TotalMicroseconds / iterations:F3} us/op | " +
            $"warmup={measurement.WarmupIterations} calls/{measurement.WarmupElapsed.TotalMilliseconds:F1} ms");
    }

    private static void Measure(string label, int iterations, Func<int> action)
        => Measure(label, 1, iterations, action);

    private static long MeasureAllocatedBytesPerInvocation(int iterations, Func<int> action)
    {
        _ = action();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var i = 0; i < iterations; i++)
        {
            checksum ^= action();
        }

        GC.KeepAlive(checksum);
        return (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
    }

    private static void MeasureFixedConstruction<T>(
        string label,
        int samples,
        int instanceCount,
        Func<T> factory)
        where T : class
    {
        MeasureFixedInstances(label, samples, instanceCount, factory, firstCall: null);
    }

    private static void MeasureFixedFirstCall<T>(
        string label,
        int samples,
        int instanceCount,
        Func<T> factory,
        Func<T, int> firstCall)
        where T : class
    {
        MeasureFixedInstances(label, samples, instanceCount, factory, firstCall);
    }

    private static void MeasureFixedInstances<T>(
        string label,
        int samples,
        int instanceCount,
        Func<T> factory,
        Func<T, int>? firstCall)
        where T : class
    {
        WriteMeasurementEnvironment();
        var elapsed = new TimeSpan[samples];
        var allocatedBytes = new long[samples];
        var sink = 0;
        for (var sample = 0; sample < samples; sample++)
        {
            var instances = new T[instanceCount];
            if (firstCall is not null)
            {
                for (var i = 0; i < instances.Length; i++)
                {
                    instances[i] = factory();
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            if (firstCall is null)
            {
                for (var i = 0; i < instances.Length; i++)
                {
                    instances[i] = factory();
                }
            }
            else
            {
                for (var i = 0; i < instances.Length; i++)
                {
                    sink ^= firstCall(instances[i]);
                }
            }

            elapsed[sample] = Stopwatch.GetElapsedTime(start);
            allocatedBytes[sample] = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(instances);
        }

        Array.Sort(elapsed);
        Array.Sort(allocatedBytes);
        var medianElapsed = elapsed[elapsed.Length / 2].TotalMicroseconds / instanceCount;
        var minimumElapsed = elapsed[0].TotalMicroseconds / instanceCount;
        var maximumElapsed = elapsed[^1].TotalMicroseconds / instanceCount;
        var medianAllocated = allocatedBytes[allocatedBytes.Length / 2] / instanceCount;
        var minimumAllocated = allocatedBytes[0] / instanceCount;
        var maximumAllocated = allocatedBytes[^1] / instanceCount;
        Console.WriteLine(
            $"{label,-28}: {medianElapsed,10:F3} us/op | " +
            $"range={minimumElapsed:F3}..{maximumElapsed:F3} us/op | " +
            $"allocated={medianAllocated:N0} B/op range={minimumAllocated:N0}..{maximumAllocated:N0} | " +
            $"instances={instanceCount} samples={samples} sink={sink}");
    }

    private static (TimeSpan Elapsed, int Sink) MeasureTimedCore(int iterations, Func<int> action)
    {
        var sink = 0;
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            sink ^= action();
        }

        stopwatch.Stop();
        return (stopwatch.Elapsed, sink);
    }

    private static MeasurementSummary MeasureMedianCore(int samples, int iterations, Func<int> action)
    {
        var warmup = WarmupCore(action);
        var sampleValues = new (TimeSpan Elapsed, int Sink)[samples];
        for (var sample = 0; sample < samples; sample++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            sampleValues[sample] = MeasureTimedCore(iterations, action);
        }

        Array.Sort(sampleValues, static (x, y) => x.Elapsed.CompareTo(y.Elapsed));
        var median = sampleValues[sampleValues.Length / 2];
        return new MeasurementSummary(
            median.Elapsed,
            sampleValues[0].Elapsed,
            sampleValues[^1].Elapsed,
            median.Sink ^ warmup.Sink,
            warmup.Iterations,
            warmup.Elapsed);
    }

    private static WarmupSummary WarmupCore(Func<int> action)
    {
        const int maximumIterations = 262144;
        var target = TimeSpan.FromMilliseconds(250);
        var sink = 0;
        var iterations = 0;
        var stopwatch = Stopwatch.StartNew();
        do
        {
            sink ^= action();
            iterations++;
        }
        while (iterations < maximumIterations && stopwatch.Elapsed < target);

        stopwatch.Stop();
        return new WarmupSummary(iterations, stopwatch.Elapsed, sink);
    }

    private readonly record struct MeasurementSummary(
        TimeSpan Elapsed,
        TimeSpan Minimum,
        TimeSpan Maximum,
        int Sink,
        int WarmupIterations,
        TimeSpan WarmupElapsed);

    private readonly record struct WarmupSummary(int Iterations, TimeSpan Elapsed, int Sink);

    private static void WriteMeasurementEnvironment()
    {
        if (s_wroteMeasurementEnvironment)
        {
            return;
        }

        s_wroteMeasurementEnvironment = true;
        var environment = CaptureBenchmarkEnvironment();
        Console.WriteLine($"SourceCommit      : {environment.SourceCommit}");
        Console.WriteLine($"TrackedDirty      : {environment.TrackedDirty}");
        Console.WriteLine($"HasUntrackedFiles : {environment.HasUntrackedFiles}");
        Console.WriteLine($"Runtime           : {environment.Runtime}");
        Console.WriteLine($"OperatingSystem   : {environment.OperatingSystem}");
        Console.WriteLine($"Processor         : {environment.Processor}");
        Console.WriteLine($"TieredPGO         : {environment.TieredPgo}");
    }

    private static BenchmarkEnvironmentJson CaptureBenchmarkEnvironment()
        => s_measurementEnvironment ??= new BenchmarkEnvironmentJson
        {
            SourceCommit = RunGit("rev-parse", "--short=12", "HEAD") ?? "<unknown>",
            TrackedDirty = !string.IsNullOrWhiteSpace(RunGit(
                "status",
                "--porcelain=v1",
                "--untracked-files=no",
                "--",
                ".",
                ":(exclude)README.md",
                ":(exclude)README.Benchmarks.json",
                ":(exclude)PCRE2.Benchmarks.json",
                ":(exclude)bench/Lokad.Utf8Regex.Benchmarks/Pcre2PerfLedger.md")),
            HasUntrackedFiles = !string.IsNullOrWhiteSpace(RunGit("ls-files", "--others", "--exclude-standard")),
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? RuntimeInformation.ProcessArchitecture.ToString(),
            TieredPgo = Environment.GetEnvironmentVariable("DOTNET_TieredPGO") ??
                Environment.GetEnvironmentVariable("COMPlus_TieredPGO") ??
                "runtime-default",
        };

    private sealed class BenchmarkEnvironmentJson
    {
        public required string SourceCommit { get; set; }

        public bool TrackedDirty { get; set; }

        public bool HasUntrackedFiles { get; set; }

        public required string Runtime { get; set; }

        public required string OperatingSystem { get; set; }

        public required string Processor { get; set; }

        public required string TieredPgo { get; set; }
    }

    private static string? RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null || !process.WaitForExit(2000))
            {
                process?.Kill(entireProcessTree: true);
                return null;
            }

            return process.ExitCode == 0 ? process.StandardOutput.ReadToEnd().Trim() : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static bool TryParseMicrosecondsLine(string line, string label, out double value)
    {
        value = 0;
        if (!line.StartsWith(label, StringComparison.Ordinal))
        {
            return false;
        }

        var marker = " us/op";
        var markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        var pipeIndex = line.LastIndexOf('|', markerIndex);
        if (pipeIndex < 0)
        {
            return false;
        }

        var numberText = line[(pipeIndex + 1)..markerIndex].Trim();
        return double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string ExtractValueAfterColon(string line)
    {
        var colonIndex = line.IndexOf(':');
        return colonIndex < 0 ? string.Empty : line[(colonIndex + 1)..].Trim();
    }

    private static string FormatLedgerValue(double? microseconds)
        => microseconds is null ? "-" : microseconds.Value.ToString("F3", CultureInfo.InvariantCulture);

    private static string FormatLedgerRatio(double? numerator, double? denominator)
    {
        if (numerator is null || denominator is null || denominator.Value == 0)
        {
            return "-";
        }

        return (numerator.Value / denominator.Value).ToString("F2", CultureInfo.InvariantCulture) + "x";
    }

    private static double MeasureMicroseconds(int samples, int iterations, Func<int> action)
        => MeasureMedianCore(samples, iterations, action).Elapsed.TotalMicroseconds / iterations;

    private static int ExecuteUtf8(Utf8RegexBenchmarkContext context)
    {
        return context.BenchmarkCase.Operation switch
        {
            Utf8RegexBenchmarkOperation.IsMatch => context.Utf8Regex.IsMatch(context.InputBytes) ? 1 : 0,
            Utf8RegexBenchmarkOperation.Count => context.Utf8Regex.Count(context.InputBytes),
            Utf8RegexBenchmarkOperation.Match => context.Utf8Regex.Match(context.InputBytes).IndexInUtf16,
            Utf8RegexBenchmarkOperation.EnumerateMatches => SumUtf8Matches(context),
            Utf8RegexBenchmarkOperation.EnumerateSplits => SumUtf8Splits(context),
            Utf8RegexBenchmarkOperation.Replace => context.Utf8Regex.Replace(context.InputBytes, context.ReplacementUtf8).Length,
            _ => 0,
        };
    }

    private static void MeasureUtf8CaseLane(string label, int samples, int iterations, Func<int> action)
    {
        Measure(label, samples, iterations, action);
        Console.WriteLine($"{label + "Allocated",-18}: {MeasureAllocatedBytesPerInvocation(iterations, action),10:N0} B/op");
    }

    private static int ExecuteUtf8Compiled(Utf8RegexBenchmarkContext context)
    {
        return context.BenchmarkCase.Operation switch
        {
            Utf8RegexBenchmarkOperation.IsMatch => context.CompiledUtf8Regex.IsMatch(context.InputBytes) ? 1 : 0,
            Utf8RegexBenchmarkOperation.Count => context.CompiledUtf8Regex.Count(context.InputBytes),
            Utf8RegexBenchmarkOperation.Match => context.CompiledUtf8Regex.Match(context.InputBytes).IndexInUtf16,
            Utf8RegexBenchmarkOperation.EnumerateMatches => SumUtf8Matches(context.CompiledUtf8Regex, context.InputBytes),
            Utf8RegexBenchmarkOperation.EnumerateSplits => SumUtf8Splits(context.CompiledUtf8Regex, context.InputBytes),
            Utf8RegexBenchmarkOperation.Replace => context.CompiledUtf8Regex.Replace(context.InputBytes, context.ReplacementUtf8).Length,
            _ => 0,
        };
    }

    private static int ExecuteDecodeThenRegex(Utf8RegexBenchmarkContext context)
    {
        var decoded = Encoding.UTF8.GetString(context.InputBytes);
        return context.BenchmarkCase.Operation switch
        {
            Utf8RegexBenchmarkOperation.IsMatch => context.Regex.IsMatch(decoded) ? 1 : 0,
            Utf8RegexBenchmarkOperation.Count => context.Regex.Count(decoded),
            Utf8RegexBenchmarkOperation.Match => context.Regex.Match(decoded).Index,
            Utf8RegexBenchmarkOperation.EnumerateMatches => SumRegexMatches(context.Regex, decoded),
            Utf8RegexBenchmarkOperation.EnumerateSplits => SumRegexSplits(context.Regex, decoded),
            Utf8RegexBenchmarkOperation.Replace => context.Regex.Replace(decoded, context.Replacement).Length,
            _ => 0,
        };
    }

    private static int ExecutePredecodedRegex(Utf8RegexBenchmarkContext context)
    {
        return context.BenchmarkCase.Operation switch
        {
            Utf8RegexBenchmarkOperation.IsMatch => context.Regex.IsMatch(context.InputString) ? 1 : 0,
            Utf8RegexBenchmarkOperation.Count => context.Regex.Count(context.InputString),
            Utf8RegexBenchmarkOperation.Match => context.Regex.Match(context.InputString).Index,
            Utf8RegexBenchmarkOperation.EnumerateMatches => SumRegexMatches(context.Regex, context.InputString),
            Utf8RegexBenchmarkOperation.EnumerateSplits => SumRegexSplits(context.Regex, context.InputString),
            Utf8RegexBenchmarkOperation.Replace => context.Regex.Replace(context.InputString, context.Replacement).Length,
            _ => 0,
        };
    }

    private static int ExecuteDecodeThenCompiledRegex(Utf8RegexBenchmarkContext context)
    {
        var decoded = Encoding.UTF8.GetString(context.InputBytes);
        return context.BenchmarkCase.Operation switch
        {
            Utf8RegexBenchmarkOperation.IsMatch => context.CompiledRegex.IsMatch(decoded) ? 1 : 0,
            Utf8RegexBenchmarkOperation.Count => context.CompiledRegex.Count(decoded),
            Utf8RegexBenchmarkOperation.Match => context.CompiledRegex.Match(decoded).Index,
            Utf8RegexBenchmarkOperation.EnumerateMatches => SumRegexMatches(context.CompiledRegex, decoded),
            Utf8RegexBenchmarkOperation.EnumerateSplits => SumRegexSplits(context.CompiledRegex, decoded),
            Utf8RegexBenchmarkOperation.Replace => context.CompiledRegex.Replace(decoded, context.Replacement).Length,
            _ => 0,
        };
    }

    private static int ExecutePredecodedCompiledRegex(Utf8RegexBenchmarkContext context)
    {
        return context.BenchmarkCase.Operation switch
        {
            Utf8RegexBenchmarkOperation.IsMatch => context.CompiledRegex.IsMatch(context.InputString) ? 1 : 0,
            Utf8RegexBenchmarkOperation.Count => context.CompiledRegex.Count(context.InputString),
            Utf8RegexBenchmarkOperation.Match => context.CompiledRegex.Match(context.InputString).Index,
            Utf8RegexBenchmarkOperation.EnumerateMatches => SumRegexMatches(context.CompiledRegex, context.InputString),
            Utf8RegexBenchmarkOperation.EnumerateSplits => SumRegexSplits(context.CompiledRegex, context.InputString),
            Utf8RegexBenchmarkOperation.Replace => context.CompiledRegex.Replace(context.InputString, context.Replacement).Length,
            _ => 0,
        };
    }

    private static int ExecuteExactLiteralPrimitiveCount(byte[] literal, byte[] input)
    {
        var count = 0;
        var index = 0;
        var span = input.AsSpan();
        while (index <= span.Length - literal.Length)
        {
            var found = span[index..].IndexOf(literal);
            if (found < 0)
            {
                return count;
            }

            count++;
            index += found + literal.Length;
        }

        return count;
    }

    private static int ExecuteIgnoreCaseLiteralPrimitiveCount(byte[] literal, byte[] input)
    {
        var count = 0;
        var index = 0;
        var span = input.AsSpan();
        while (index <= span.Length - literal.Length)
        {
            var found = AsciiSearch.IndexOfIgnoreCase(span[index..], literal);
            if (found < 0)
            {
                return count;
            }

            count++;
            index += found + literal.Length;
        }

        return count;
    }

    private static int ExecuteIgnoreCaseLiteralPrimitiveCountWithCompareIndex(byte[] literal, byte[] input, int compareIndex)
    {
        var count = 0;
        var index = 0;
        var span = input.AsSpan();
        var foldedLiteral = literal.Select(AsciiSearch.FoldCase).ToArray();
        var probe = foldedLiteral[compareIndex];
        var hasVariants = AsciiSearch.TryGetCaseVariants(probe, out var lower, out var upper);
        while (index <= span.Length - literal.Length)
        {
            var searchSlice = span[(index + compareIndex)..];
            var relative = hasVariants
                ? searchSlice.IndexOfAny(lower, upper)
                : searchSlice.IndexOf(probe);
            if (relative < 0)
            {
                return count;
            }

            var found = index + compareIndex + relative;
            var candidate = found - compareIndex;
            if (candidate <= span.Length - literal.Length &&
                AsciiSearch.MatchesFoldedIgnoreCase(span.Slice(candidate, literal.Length), foldedLiteral))
            {
                count++;
                index = candidate + literal.Length;
                continue;
            }

            index = found + 1;
        }

        return count;
    }


    private static int ExecuteSmallAsciiLiteralFamilyPrimitiveCountScalar(PreparedSmallAsciiLiteralFamilySearch search, byte[] input)
        => search.CountScalar(input);

    private static int ExecuteSmallAsciiLiteralFamilyPrimitiveCountSimd(PreparedSmallAsciiLiteralFamilySearch search, byte[] input)
        => search.Count(input);

    private static int ExecuteSmallAsciiLiteralFamilyPrimitiveFirstMatch(PreparedSmallAsciiLiteralFamilySearch search, byte[] input)
        => search.TryFindFirst(input, out var index, out var matchedLength) ? index + matchedLength : -1;

    private static int ExecutePreparedSearcherCount(PreparedSearcher searcher, byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (searcher.TryFindNextNonOverlappingLength(span, ref state, out _, out _))
        {
            count++;
        }

        return count;
    }

    private static string RepeatToLength(string seed, int length)
    {
        var builder = new StringBuilder(length + seed.Length);
        while (builder.Length < length)
        {
            builder.Append(seed);
        }

        return builder.ToString(0, length);
    }

    private static int CountUtf8SplitValues(Utf8Regex regex, byte[] input)
    {
        var count = 0;
        foreach (var _ in regex.EnumerateSplits(input))
        {
            count++;
        }

        return count;
    }

    private static (int FirstByte, int CorrelatedPrefix, int PrefixLength) CountIgnoreCaseLiteralFamilyCandidates(
        PreparedAsciiIgnoreCaseLiteralSetSearch search,
        ReadOnlySpan<byte> input)
    {
        if (search.ShortestLength <= 0 ||
            search.ShortestLength == int.MaxValue ||
            input.Length < search.ShortestLength)
        {
            return (0, 0, 0);
        }

        var firstByteCandidates = 0;
        var correlatedPrefixCandidates = 0;
        var prefixLength = Math.Min(4, search.ShortestLength);
        var maximumStart = input.Length - search.ShortestLength;
        var start = 0;
        while (start <= maximumStart)
        {
            var relative = input[start..].IndexOfAny(search.FirstByteSearchValues);
            if (relative < 0)
            {
                break;
            }

            var candidate = start + relative;
            if (candidate > maximumStart)
            {
                break;
            }

            firstByteCandidates++;
            var prefix = input.Slice(candidate, prefixLength);
            var matchesPrefix = false;
            foreach (var bucket in search.Buckets)
            {
                foreach (var literal in bucket.Literals)
                {
                    if (AsciiSearch.MatchesFoldedIgnoreCase(prefix, literal.AsSpan(0, prefixLength)))
                    {
                        matchesPrefix = true;
                        break;
                    }
                }

                if (matchesPrefix)
                {
                    break;
                }
            }

            if (matchesPrefix)
            {
                correlatedPrefixCandidates++;
            }

            start = candidate + 1;
        }

        return (firstByteCandidates, correlatedPrefixCandidates, prefixLength);
    }

    private static int CountRegexSplitValues(Regex regex, string input)
    {
        var count = 0;
        foreach (var _ in regex.EnumerateSplits(input))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteExactLiteralSetCount(PreparedLiteralSetSearch search, byte[] input)
    {
        var count = 0;
        var index = 0;
        var span = input.AsSpan();
        while (index <= span.Length - search.SearchData.ShortestLength)
        {
            var found = search.IndexOf(span[index..]);
            if (found < 0)
            {
                return count;
            }

            var absolute = index + found;
            if (!search.TryGetMatchedLiteralLength(span, absolute, out var matchedLength))
            {
                return count;
            }

            count++;
            index = absolute + matchedLength;
        }

        return count;
    }

    private static int ExecuteTrieLiteralSetCount(PreparedMultiLiteralTrieSearch search, byte[] input)
    {
        var count = 0;
        var index = 0;
        var span = input.AsSpan();
        while (index <= span.Length - search.ShortestLength)
        {
            var found = search.IndexOf(span[index..]);
            if (found < 0)
            {
                return count;
            }

            var absolute = index + found;
            if (!search.TryGetMatchedLiteralLength(span, absolute, out var matchedLength))
            {
                return count;
            }

            count++;
            index = absolute + matchedLength;
        }

        return count;
    }

    private static int ExecutePackedLiteralSetCount(PreparedMultiLiteralPackedSearch search, byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (search.TryFindNextNonOverlappingMatch(span, ref state, out _, out _, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteCandidatePrefilterCount(PreparedMultiLiteralCandidatePrefilter search, byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (search.TryFindNextCandidate(span, ref state, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteCandidatePrefilterIsMatch(
        PreparedMultiLiteralCandidatePrefilter search,
        byte[] input)
    {
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (search.TryFindNextCandidate(span, ref state, out var candidateIndex))
        {
            if (search.TryGetMatchedLength(span, candidateIndex, out _))
            {
                return 1;
            }
        }

        return 0;
    }

    private static int ExecuteAutomatonLiteralSetCount(PreparedMultiLiteralAutomatonSearch search, byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (search.TryFindNextNonOverlappingMatch(span, ref state, out _, out _, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecutePreparedSearcherCountWithBoundaries(PreparedSearcher searcher, Utf8SearchPlan plan, byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (searcher.TryFindNextNonOverlappingLength(span, ref state, out var index, out var matchedLength))
        {
            if (Utf8SearchExecutor.MatchesBoundaryRequirements(plan, span, index, matchedLength))
            {
                count++;
            }
        }

        return count;
    }

    private static int ExecuteExactLiteralSetCountWithBoundaries(PreparedLiteralSetSearch search, Utf8SearchPlan plan, byte[] input)
    {
        var count = 0;
        var index = 0;
        var span = input.AsSpan();
        while (index <= span.Length - search.SearchData.ShortestLength)
        {
            var found = search.IndexOf(span[index..]);
            if (found < 0)
            {
                return count;
            }

            var absolute = index + found;
            if (!search.TryGetMatchedLiteralLength(span, absolute, out var matchedLength))
            {
                return count;
            }

            if (Utf8SearchExecutor.MatchesBoundaryRequirements(plan, span, absolute, matchedLength))
            {
                count++;
            }

            index = absolute + matchedLength;
        }

        return count;
    }

    private static int ExecuteTrieLiteralSetCountWithBoundaries(PreparedMultiLiteralTrieSearch search, Utf8SearchPlan plan, byte[] input)
    {
        var count = 0;
        var index = 0;
        var span = input.AsSpan();
        while (index <= span.Length - search.ShortestLength)
        {
            var found = search.IndexOf(span[index..]);
            if (found < 0)
            {
                return count;
            }

            var absolute = index + found;
            if (!search.TryGetMatchedLiteralLength(span, absolute, out var matchedLength))
            {
                return count;
            }

            if (Utf8SearchExecutor.MatchesBoundaryRequirements(plan, span, absolute, matchedLength))
            {
                count++;
            }

            index = absolute + matchedLength;
        }

        return count;
    }

    private static int ExecutePackedLiteralSetCountWithBoundaries(PreparedMultiLiteralPackedSearch search, Utf8SearchPlan plan, byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (search.TryFindNextNonOverlappingMatch(span, ref state, out var index, out var matchedLength, out _))
        {
            if (Utf8SearchExecutor.MatchesBoundaryRequirements(plan, span, index, matchedLength))
            {
                count++;
            }
        }

        return count;
    }

    private static int ExecuteCandidatePrefilterCountWithBoundaries(PreparedMultiLiteralCandidatePrefilter search, Utf8SearchPlan plan, byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (search.TryFindNextCandidate(span, ref state, out var index))
        {
            if (search.TryGetMatchedLength(span, index, out var matchedLength) &&
                Utf8SearchExecutor.MatchesBoundaryRequirements(plan, span, index, matchedLength))
            {
                count++;
            }
        }

        return count;
    }

    private static int ExecuteAutomatonLiteralSetCountWithBoundaries(PreparedMultiLiteralAutomatonSearch search, Utf8SearchPlan plan, byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (search.TryFindNextNonOverlappingMatch(span, ref state, out var index, out var matchedLength, out _))
        {
            if (Utf8SearchExecutor.MatchesBoundaryRequirements(plan, span, index, matchedLength))
            {
                count++;
            }
        }

        return count;
    }

    private static int ExecuteHybridChunkedLiteralSetProbe(
        PreparedLiteralSetSearch direct,
        PreparedMultiLiteralAutomatonSearch automaton,
        byte[] input,
        int chunkBytes)
    {
        return DiagnoseHybridChunkedLiteralSetCount(direct, automaton, input, chunkBytes).PromotedWindows;
    }

    private static int ExecuteHybridChunkedLiteralSetCount(
        PreparedMultiLiteralCandidatePrefilter probe,
        PreparedMultiLiteralAutomatonSearch automaton,
        byte[] input,
        int chunkBytes)
    {
        return DiagnoseHybridChunkedLiteralSetCount(probe, automaton, input, chunkBytes).PromotedWindows;
    }

    private static int ExecuteHybridChunkedLiteralSetProbe(
        PreparedMultiLiteralCandidatePrefilter probe,
        PreparedMultiLiteralAutomatonSearch automaton,
        byte[] input,
        int chunkBytes)
    {
        return DiagnoseHybridChunkedLiteralSetCount(probe, automaton, input, chunkBytes).Count;
    }

    private static HybridLiteralFamilyMetrics DiagnoseHybridChunkedLiteralSetCount(
        PreparedLiteralSetSearch direct,
        PreparedMultiLiteralAutomatonSearch automaton,
        ReadOnlySpan<byte> input,
        int chunkBytes)
    {
        var count = 0;
        var windows = 0;
        var skippedWindows = 0;
        var promotedWindows = 0;
        var skippedBytes = 0;
        var promotedBytes = 0;
        var position = 0;
        var shortestLength = direct.SearchData.ShortestLength;
        var maxLiteralLength = 0;
        for (var i = 0; i < direct.SearchData.Buckets.Length; i++)
        {
            var literals = direct.SearchData.Buckets[i].Literals;
            for (var j = 0; j < literals.Length; j++)
            {
                maxLiteralLength = Math.Max(maxLiteralLength, literals[j].Length);
            }
        }

        while (position <= input.Length - shortestLength)
        {
            windows++;
            var chunkEnd = Math.Min(input.Length, position + chunkBytes);
            var probeEnd = Math.Min(input.Length, chunkEnd + maxLiteralLength - 1);
            var probe = input[position..probeEnd];
            var relative = direct.IndexOf(probe);
            if (relative < 0 || position + relative >= chunkEnd)
            {
                skippedWindows++;
                skippedBytes += chunkEnd - position;
                position = chunkEnd;
                continue;
            }

            promotedWindows++;
            promotedBytes += chunkEnd - position;
            var state = new PreparedMultiLiteralScanState(0, 0, 0);
            var promotedSlice = input[position..probeEnd];
            while (automaton.TryFindNextNonOverlappingMatch(promotedSlice, ref state, out var matchIndex, out var matchedLength, out _))
            {
                if (matchIndex >= chunkEnd - position)
                {
                    break;
                }

                count++;
            }

            position = Math.Max(chunkEnd, position + state.NextStart);
        }

        return new HybridLiteralFamilyMetrics(count, windows, skippedWindows, promotedWindows, skippedBytes, promotedBytes);
    }

    private static HybridLiteralFamilyMetrics DiagnoseHybridChunkedLiteralSetCount(
        PreparedMultiLiteralCandidatePrefilter probe,
        PreparedMultiLiteralAutomatonSearch automaton,
        ReadOnlySpan<byte> input,
        int chunkBytes)
    {
        return DiagnoseHybridChunkedLiteralSetCountCore(
            input,
            probe.ShortestLength,
            probe.LongestLength,
            chunkBytes,
            static (search, window, chunkLimit) =>
            {
                var state = new PreparedMultiLiteralScanState(0, 0, 0);
                return search.TryFindNextCandidate(window, ref state, out var index) && index < chunkLimit;
            },
            probe,
            automaton);
    }

    private static HybridLiteralFamilyMetrics DiagnoseHybridChunkedLiteralSetCountCore<TProbe>(
        ReadOnlySpan<byte> input,
        int shortestLength,
        int maxLiteralLength,
        int chunkBytes,
        HybridProbe<TProbe> hasCandidate,
        TProbe probe,
        PreparedMultiLiteralAutomatonSearch automaton)
    {
        var count = 0;
        var windows = 0;
        var skippedWindows = 0;
        var promotedWindows = 0;
        var skippedBytes = 0;
        var promotedBytes = 0;
        var position = 0;

        while (position <= input.Length - shortestLength)
        {
            windows++;
            var chunkEnd = Math.Min(input.Length, position + chunkBytes);
            var probeEnd = Math.Min(input.Length, chunkEnd + maxLiteralLength - 1);
            var probeWindow = input[position..probeEnd];
            if (!hasCandidate(probe, probeWindow, chunkEnd - position))
            {
                skippedWindows++;
                skippedBytes += chunkEnd - position;
                position = chunkEnd;
                continue;
            }

            promotedWindows++;
            promotedBytes += chunkEnd - position;
            var state = new PreparedMultiLiteralScanState(0, 0, 0);
            var nextPosition = chunkEnd;
            while (automaton.TryFindNextNonOverlappingMatch(probeWindow, ref state, out var matchIndex, out _, out _))
            {
                if (matchIndex >= chunkEnd - position)
                {
                    nextPosition = position + matchIndex;
                    break;
                }

                count++;
                nextPosition = Math.Max(chunkEnd, position + state.NextStart);
            }

            position = nextPosition;
        }

        return new HybridLiteralFamilyMetrics(count, windows, skippedWindows, promotedWindows, skippedBytes, promotedBytes);
    }

    private static int GetLongestLength(byte[][] literals)
    {
        var longest = 0;
        for (var i = 0; i < literals.Length; i++)
        {
            longest = Math.Max(longest, literals[i].Length);
        }

        return longest;
    }

    private static int GetLongestLength(AsciiExactLiteralBucket[] buckets)
    {
        var longest = 0;
        for (var i = 0; i < buckets.Length; i++)
        {
            var literals = buckets[i].Literals;
            for (var j = 0; j < literals.Length; j++)
            {
                longest = Math.Max(longest, literals[j].Length);
            }
        }

        return longest;
    }

    private static int ExecutePreparedSubstringCount(PreparedSubstringSearch search, byte[] input)
    {
        var count = 0;
        var index = 0;
        var span = input.AsSpan();
        while (index <= span.Length - search.Length)
        {
            var found = search.IndexOf(span[index..]);
            if (found < 0)
            {
                return count;
            }

            count++;
            index += found + search.Length;
        }

        return count;
    }

    private static int ExecutePreparedSubstringCountWithPreferredCompare(PreparedSubstringSearch search, byte[] input, int compareIndex)
    {
        return search.CountIgnoreCaseWithPreferredCompareIndex(input, compareIndex, out _, out _);
    }

    private static int ExecutePreparedSubstringCountWithTier(PreparedSubstringSearch search, byte[] input, PreparedIgnoreCaseSearchTier tier)
    {
        return search.CountIgnoreCaseWithTier(input, tier, out _, out _);
    }

    private static int ExecutePreparedSubstringCandidateCount(PreparedSubstringSearch search, byte[] input)
    {
        _ = search.CountWithMetrics(input, out var candidateCount, out _);
        return candidateCount;
    }

    private static int ExecutePreparedSubstringCandidateCountWithTier(PreparedSubstringSearch search, byte[] input, PreparedIgnoreCaseSearchTier tier)
    {
        _ = search.CountIgnoreCaseWithTier(input, tier, out var candidateCount, out _);
        return candidateCount;
    }

    private static int ExecutePreparedSubstringVerificationCount(PreparedSubstringSearch search, byte[] input)
    {
        _ = search.CountWithMetrics(input, out _, out var verificationCount);
        return verificationCount;
    }

    private static int ExecutePreparedSubstringVerificationCountWithTier(PreparedSubstringSearch search, byte[] input, PreparedIgnoreCaseSearchTier tier)
    {
        _ = search.CountIgnoreCaseWithTier(input, tier, out _, out var verificationCount);
        return verificationCount;
    }

    private static int SumUtf8Matches(Utf8RegexBenchmarkContext context)
    {
        return SumUtf8Matches(context.Utf8Regex, context.InputBytes);
    }

    private static int SumUtf8Matches(Utf8Regex regex, byte[] input)
    {
        var sum = 0;
        foreach (var match in regex.EnumerateMatches(input))
        {
            sum += match.IndexInUtf16;
        }

        return sum;
    }

    private static int CreateUtf8Enumerator(Utf8Regex regex, byte[] input)
    {
        var enumerator = regex.EnumerateMatches(input);
        return enumerator.Current.Success ? 1 : 0;
    }

    private static int FirstUtf8EnumeratorMatch(Utf8Regex regex, byte[] input)
    {
        var enumerator = regex.EnumerateMatches(input);
        return enumerator.MoveNext() ? enumerator.Current.IndexInUtf16 : -1;
    }

    private static int FirstCopiedUtf8EnumeratorMatch(Utf8Regex regex, byte[] input)
    {
        var source = regex.EnumerateMatches(input);
        var enumerator = source.GetEnumerator();
        return enumerator.MoveNext() ? enumerator.Current.IndexInUtf16 : -1;
    }

    private static int SumUtf8Splits(Utf8RegexBenchmarkContext context)
    {
        return SumUtf8Splits(context.Utf8Regex, context.InputBytes);
    }

    private static int SumUtf8Splits(Utf8Regex regex, byte[] input)
    {
        var sum = 0;
        foreach (var split in regex.EnumerateSplits(input))
        {
            sum += split.LengthInUtf16;
        }

        return sum;
    }

    private static int SumRegexMatches(Regex regex, string input)
    {
        var sum = 0;
        foreach (var match in regex.EnumerateMatches(input))
        {
            sum += match.Index;
        }

        return sum;
    }

    private static int SumRegexSplits(Regex regex, string input)
    {
        var sum = 0;
        foreach (var split in regex.EnumerateSplits(input))
        {
            sum += split.GetOffsetAndLength(input.Length).Length;
        }

        return sum;
    }

    private static int ExecuteStructuralLinearRawScan(Utf8StructuralLinearProgram program, byte[] input)
    {
        var sum = 0;
        var state = new Utf8AsciiDeterministicScanState(0, program.DeterministicProgram.SearchLiteralOffset);
        while (Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicRawMatch(
            program,
            input,
            ref state,
            budget: Utf8ExecutionDeadline.Infinite,
            out var match))
        {
            sum += match.Index;
        }

        return sum;
    }

    private static int ExecuteStructuralLinearCount(Utf8StructuralLinearProgram program, byte[] input)
    {
        return Utf8AsciiInstructionLinearExecutor.CountDeterministic(program, input, budget: Utf8ExecutionDeadline.Infinite);
    }

    private static int ExecuteStructuralLinearFixedWidthIndexSum(Utf8StructuralLinearProgram program, byte[] input)
    {
        if (program.DeterministicProgram.FixedWidthLength <= 0)
        {
            return 0;
        }

        var sum = 0;
        var state = new Utf8AsciiDeterministicScanState(0, program.DeterministicProgram.SearchLiteralOffset);
        while (Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicFixedWidthMatch(
            program,
            input,
            ref state,
            budget: Utf8ExecutionDeadline.Infinite,
            out var index))
        {
            sum += index;
        }

        return sum;
    }

    private static int ExecuteStructuralLinearRawCount(Utf8StructuralLinearProgram program, byte[] input)
    {
        var count = 0;
        var state = new Utf8AsciiDeterministicScanState(0, program.DeterministicProgram.SearchLiteralOffset);
        while (Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicRawMatch(
            program,
            input,
            ref state,
            budget: Utf8ExecutionDeadline.Infinite,
            out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteStructuralLinearEnumeratorMoveNextCount(Utf8StructuralLinearProgram program, byte[] input)
    {
        var count = 0;
        var enumerator = new Utf8ValueMatchEnumerator(input, program, budget: Utf8ExecutionDeadline.Infinite);
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int ExecuteStructuralLinearEnumeratorIndexSum(Utf8StructuralLinearProgram program, byte[] input)
    {
        var sum = 0;
        var enumerator = new Utf8ValueMatchEnumerator(input, program, budget: Utf8ExecutionDeadline.Infinite);
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.IndexInUtf16;
        }

        return sum;
    }

    private static int ExecuteValidationOnly(byte[] input)
    {
        return Utf8Validation.Validate(input).Utf16Length;
    }

    private static int ExecuteBoundaryMapOnly(byte[] input)
    {
        return Utf8BoundaryMap.Create(input).Utf16Length;
    }

    private static int ExecutePublicEnumeratorMoveNextCount(Utf8Regex regex, byte[] input)
    {
        var count = 0;
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int ExecutePublicEnumeratorIndexSum(Utf8Regex regex, byte[] input)
    {
        var sum = 0;
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.IndexInUtf16;
        }

        return sum;
    }

    private static int ExecuteReplicaUtf8EnumerateMatchIndexSum(ReplicaCountBenchmarkCase benchmarkCase)
    {
        var sum = 0;
        var enumerator = benchmarkCase.Utf8Regex.EnumerateMatches(benchmarkCase.InputBytes);
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.IndexInUtf16;
        }

        return sum;
    }

    private static int ExecuteReplicaDecodeEnumerateMatchIndexSum(ReplicaCountBenchmarkCase benchmarkCase)
    {
        var decoded = Encoding.UTF8.GetString(benchmarkCase.InputBytes);
        var sum = 0;
        foreach (var match in Regex.EnumerateMatches(decoded, benchmarkCase.Pattern, benchmarkCase.Options))
        {
            sum += match.Index;
        }

        return sum;
    }

    private static int ExecuteReplicaPredecodedEnumerateMatchIndexSum(ReplicaCountBenchmarkCase benchmarkCase, string decoded)
    {
        var sum = 0;
        foreach (var match in benchmarkCase.Regex.EnumerateMatches(decoded))
        {
            sum += match.Index;
        }

        return sum;
    }

    private static int ExecuteDirectExactUtf8LiteralEnumeratorMoveNext(Utf8SearchPlan plan, byte[] literal, byte[] input)
    {
        var count = 0;
        var literalUtf16Length = Utf8Validation.Validate(literal).Utf16Length;
        var enumerator = new Utf8ValueMatchEnumerator(input, plan, literal, literalUtf16Length, budget: Utf8ExecutionDeadline.Infinite);
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int ExecuteDirectExactUtf8LiteralEnumeratorIndexSum(Utf8SearchPlan plan, byte[] literal, byte[] input)
    {
        var sum = 0;
        var literalUtf16Length = Utf8Validation.Validate(literal).Utf16Length;
        var enumerator = new Utf8ValueMatchEnumerator(input, plan, literal, literalUtf16Length, budget: Utf8ExecutionDeadline.Infinite);
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.IndexInUtf16;
        }

        return sum;
    }

    private static int ExecuteExactUtf8LiteralSearchCount(Utf8SearchPlan plan, byte[] literal, byte[] input)
    {
        var count = 0;
        var remaining = input.AsSpan();
        while (true)
        {
            var index = Utf8SearchExecutor.FindFirst(plan, remaining);
            if (index < 0)
            {
                return count;
            }

            count++;
            remaining = remaining[(index + literal.Length)..];
        }
    }

    private static int ExecuteExactUtf8LiteralBoundaryMapIndexSum(Utf8SearchPlan plan, byte[] literal, byte[] input)
    {
        var sum = 0;
        var boundaryMap = Utf8BoundaryMap.Create(input);
        var remaining = input.AsSpan();
        var consumed = 0;
        while (true)
        {
            var index = Utf8SearchExecutor.FindFirst(plan, remaining);
            if (index < 0)
            {
                return sum;
            }

            var absolute = consumed + index;
            sum += boundaryMap.GetUtf16OffsetForByteOffset(absolute);
            var advance = index + literal.Length;
            remaining = remaining[advance..];
            consumed += advance;
        }
    }

    private static int ExecuteExactUtf8LiteralDirectBoundaryMapIndexSum(PreparedSubstringSearch search, byte[] literal, byte[] input)
    {
        var sum = 0;
        var boundaryMap = Utf8BoundaryMap.Create(input);
        var span = input.AsSpan();
        var consumed = 0;
        while (consumed <= span.Length - literal.Length)
        {
            var found = search.IndexOf(span[consumed..]);
            if (found < 0)
            {
                return sum;
            }

            var absolute = consumed + found;
            sum += boundaryMap.GetUtf16OffsetForByteOffset(absolute);
            consumed = absolute + literal.Length;
        }

        return sum;
    }

    private static int ExecuteExactUtf8LiteralDirectIncrementalIndexSum(PreparedSubstringSearch search, byte[] literal, byte[] input, int literalUtf16Length)
    {
        var sum = 0;
        var span = input.AsSpan();
        var remaining = span;
        var consumedBytes = 0;
        var consumedUtf16 = 0;
        while (consumedBytes <= span.Length - literal.Length)
        {
            var found = search.IndexOf(remaining);
            if (found < 0)
            {
                return sum;
            }

            var relativeUtf16Index = found == 0 ? 0 : Utf8Validation.Validate(remaining[..found]).Utf16Length;
            sum += consumedUtf16 + relativeUtf16Index;
            var advance = found + literal.Length;
            remaining = remaining[advance..];
            consumedBytes += advance;
            consumedUtf16 += relativeUtf16Index + literalUtf16Length;
        }

        return sum;
    }

    private static int ExecuteExactUtf8LiteralFamilySearchCount(Utf8SearchPlan plan, byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (plan.PreparedSearcher.TryFindNextNonOverlappingMatch(span, ref state, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteExactUtf8LiteralFamilyBoundaryMapIndexSum(Utf8SearchPlan plan, byte[] input)
    {
        var sum = 0;
        var boundaryMap = Utf8BoundaryMap.Create(input);
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (plan.PreparedSearcher.TryFindNextNonOverlappingMatch(span, ref state, out var match))
        {
            sum += boundaryMap.GetUtf16OffsetForByteOffset(match.Index);
        }

        return sum;
    }

    private static int ExecuteExactUtf8LiteralFamilyDirectIncrementalIndexSum(Utf8SearchPlan plan, byte[] input)
    {
        var sum = 0;
        var remaining = input.AsSpan();
        var consumedUtf16 = 0;
        var utf16Lengths = plan.AlternateLiteralUtf16Lengths ?? [];
        while (plan.PreparedSearcher.TryFindFirstMatch(remaining, out var match))
        {
            var relativeUtf16Index = match.Index == 0 ? 0 : Utf8Validation.Validate(remaining[..match.Index]).Utf16Length;
            sum += consumedUtf16 + relativeUtf16Index;
            var matchedUtf16Length = (uint)match.LiteralId < (uint)utf16Lengths.Length
                ? utf16Lengths[match.LiteralId]
                : Utf8Validation.Validate(remaining.Slice(match.Index, match.Length)).Utf16Length;
            var advance = match.Index + match.Length;
            remaining = remaining[advance..];
            consumedUtf16 += relativeUtf16Index + matchedUtf16Length;
        }

        return sum;
    }

    private static int ExecuteDirectExactUtf8LiteralFamilyEnumeratorMoveNext(Utf8SearchPlan plan, byte[] input)
    {
        var count = 0;
        var enumerator = new Utf8ValueMatchEnumerator(input, plan, budget: Utf8ExecutionDeadline.Infinite);
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int ExecuteDirectExactUtf8LiteralFamilyEnumeratorIndexSum(Utf8SearchPlan plan, byte[] input)
    {
        var sum = 0;
        var enumerator = new Utf8ValueMatchEnumerator(input, plan, budget: Utf8ExecutionDeadline.Infinite);
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.IndexInUtf16;
        }

        return sum;
    }

    private static int ExecuteStructuralFamilyFindNextCount(
        Utf8StructuralLinearRuntime linearRuntime,
        Utf8VerifierRuntime verifierRuntime,
        byte[] input,
        Utf8ValidationResult validation)
    {
        var count = 0;
        var startIndex = 0;
        while (linearRuntime.TryFindNext(input, validation, verifierRuntime, startIndex, budget: Utf8ExecutionDeadline.Infinite, out var matchIndex, out var matchedLength))
        {
            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int ExecuteEmittedKernelFindNextCount(Utf8EmittedKernelMatcher matcher, byte[] input)
    {
        var count = 0;
        var startIndex = 0;
        while (matcher.FindNext(input, startIndex, out var matchedLength) is var matchIndex && matchIndex >= 0)
        {
            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int ExecuteEmittedKernelFindNextIndexSum(Utf8EmittedKernelMatcher matcher, byte[] input)
    {
        var sum = 0;
        var startIndex = 0;
        while (matcher.FindNext(input, startIndex, out var matchedLength) is var matchIndex && matchIndex >= 0)
        {
            sum += matchIndex;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return sum;
    }

    private static int ExecuteStructuralFamilyNativeCount(
        Utf8StructuralLinearRuntime linearRuntime,
        Utf8VerifierRuntime verifierRuntime,
        byte[] input,
        Utf8ValidationResult validation)
    {
        return linearRuntime.Count(input, validation, verifierRuntime, budget: Utf8ExecutionDeadline.Infinite);
    }

    private static int ExecuteStructuralFamilyFindNextIndexSum(
        Utf8StructuralLinearRuntime linearRuntime,
        Utf8VerifierRuntime verifierRuntime,
        byte[] input,
        Utf8ValidationResult validation)
    {
        var sum = 0;
        var startIndex = 0;
        while (linearRuntime.TryFindNext(input, validation, verifierRuntime, startIndex, budget: Utf8ExecutionDeadline.Infinite, out var matchIndex, out var matchedLength))
        {
            sum += matchIndex;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return sum;
    }

    private static int ExecuteStructuralFamilyStatefulFindNextCount(
        Utf8AsciiStructuralFamilyLinearRuntime linearRuntime,
        Utf8VerifierRuntime verifierRuntime,
        byte[] input)
    {
        var count = 0;
        var state = linearRuntime.CreateScanState(0);
        while (linearRuntime.TryFindNextStateful(input, verifierRuntime, ref state, budget: Utf8ExecutionDeadline.Infinite, out _, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteStructuralFamilyStatefulCount(
        Utf8AsciiStructuralFamilyLinearRuntime linearRuntime,
        Utf8VerifierRuntime verifierRuntime,
        byte[] input)
    {
        var count = 0;
        var state = linearRuntime.CreateScanState(0);
        while (linearRuntime.TryFindNextStateful(input, verifierRuntime, ref state, budget: Utf8ExecutionDeadline.Infinite, out _, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteStructuralFamilyStatefulFindNextIndexSum(
        Utf8AsciiStructuralFamilyLinearRuntime linearRuntime,
        Utf8VerifierRuntime verifierRuntime,
        byte[] input)
    {
        var sum = 0;
        var state = linearRuntime.CreateScanState(0);
        while (linearRuntime.TryFindNextStateful(input, verifierRuntime, ref state, budget: Utf8ExecutionDeadline.Infinite, out var matchIndex, out _))
        {
            sum += matchIndex;
        }

        return sum;
    }

    private static int ExecuteStructuralFamilyStatefulCandidateCount(
        Utf8AsciiStructuralFamilyLinearRuntime linearRuntime,
        byte[] input)
    {
        var count = 0;
        var state = linearRuntime.CreateScanState(0);
        while (linearRuntime.TryFindNextCandidateStateful(input, ref state, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteStructuralFamilyStatefulCandidateIndexSum(
        Utf8AsciiStructuralFamilyLinearRuntime linearRuntime,
        byte[] input)
    {
        var sum = 0;
        var state = linearRuntime.CreateScanState(0);
        while (linearRuntime.TryFindNextCandidateStateful(input, ref state, out var candidate))
        {
            sum += candidate.StartIndex;
        }

        return sum;
    }

    private static int ExecuteIdentifierFamilyPreparedOverlapCount(
        Utf8SearchPlan searchPlan,
        byte[] input)
    {
        var count = 0;
        var state = new PreparedSearchScanState(0, default);
        while (searchPlan.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteIdentifierFamilyPreparedNonOverlappingCount(
        Utf8SearchPlan searchPlan,
        byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (searchPlan.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out _, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteIdentifierFamilyBoundaryFilteredCount(
        Utf8SearchPlan searchPlan,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        byte[] input)
    {
        var count = 0;
        var state = new PreparedSearchScanState(0, default);
        while (searchPlan.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            if (AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.LeadingBoundary, input, match.Index))
            {
                count++;
            }
        }

        return count;
    }

    private static int ExecuteIdentifierFamilyBoundaryFilteredNonOverlappingCount(
        Utf8SearchPlan searchPlan,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (searchPlan.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out var matchIndex, out _))
        {
            if (AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.LeadingBoundary, input, matchIndex))
            {
                count++;
            }
        }

        return count;
    }

    private static int ExecuteIdentifierFamilyBoundaryFilteredIndexSum(
        Utf8SearchPlan searchPlan,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        byte[] input)
    {
        var sum = 0;
        var state = new PreparedSearchScanState(0, default);
        while (searchPlan.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            if (AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.LeadingBoundary, input, match.Index))
            {
                sum += match.Index;
            }
        }

        return sum;
    }

    private static int ExecuteIdentifierFamilyPreparedMatchCount(
        Utf8SearchPlan searchPlan,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        byte[] input)
    {
        var count = 0;
        var state = new PreparedSearchScanState(0, default);
        while (searchPlan.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.LeadingBoundary, input, match.Index))
            {
                continue;
            }

            if (AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, match.Index, match.Length, familyPlan, out _))
            {
                count++;
            }
        }

        return count;
    }

    private static int ExecuteIdentifierFamilyPreparedMatchNonOverlappingCount(
        Utf8SearchPlan searchPlan,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (searchPlan.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out var matchIndex, out var matchLength))
        {
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.LeadingBoundary, input, matchIndex))
            {
                continue;
            }

            if (AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, matchIndex, matchLength, familyPlan, out _))
            {
                count++;
            }
        }

        return count;
    }

    private static int ExecuteIdentifierFamilyPreparedMatchIndexSum(
        Utf8SearchPlan searchPlan,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        byte[] input)
    {
        var sum = 0;
        var state = new PreparedSearchScanState(0, default);
        while (searchPlan.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.LeadingBoundary, input, match.Index))
            {
                continue;
            }

            if (AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, match.Index, match.Length, familyPlan, out _))
            {
                sum += match.Index;
            }
        }

        return sum;
    }

    private static int ExecuteLiteralFamilyRunCandidateCount(Utf8StructuralLinearProgram program, byte[] input)
    {
        var count = 0;
        var searchFrom = program.InstructionProgram.SearchLiteralOffset;
        while (searchFrom <= input.Length)
        {
            var found = TryFindLiteralFamilyRunCandidate(program, input, searchFrom, out var absoluteAnchor, out _);
            if (!found)
            {
                return count;
            }

            count++;
            searchFrom = absoluteAnchor + 1;
        }

        return count;
    }

    private static int ExecuteLiteralFamilyRunCandidateIndexSum(Utf8StructuralLinearProgram program, byte[] input)
    {
        var sum = 0;
        var searchFrom = program.InstructionProgram.SearchLiteralOffset;
        while (searchFrom <= input.Length)
        {
            var found = TryFindLiteralFamilyRunCandidate(program, input, searchFrom, out var absoluteAnchor, out _);
            if (!found)
            {
                return sum;
            }

            sum += absoluteAnchor - program.InstructionProgram.SearchLiteralOffset;
            searchFrom = absoluteAnchor + 1;
        }

        return sum;
    }

    private static bool TryFindLiteralFamilyRunCandidate(
        Utf8StructuralLinearProgram program,
        byte[] input,
        int searchFrom,
        out int absoluteAnchor,
        out int prefixLength)
    {
        absoluteAnchor = -1;
        prefixLength = 0;
        var instructionProgram = program.InstructionProgram;
        if (instructionProgram.SearchLiterals.Length == 0 || searchFrom > input.Length)
        {
            return false;
        }

        if (instructionProgram.SearchLiterals.Length == 1)
        {
            var relative = AsciiSearch.IndexOfExact(input.AsSpan(searchFrom), instructionProgram.SearchLiterals[0]);
            if (relative < 0)
            {
                return false;
            }

            absoluteAnchor = searchFrom + relative;
            prefixLength = instructionProgram.SearchLiterals[0].Length;
            return true;
        }

        if (!instructionProgram.SearchLiteralsSearch.TryFindFirstMatchWithLength(input.AsSpan(searchFrom), out var relativeIndex, out prefixLength))
        {
            return false;
        }

        absoluteAnchor = searchFrom + relativeIndex;
        return true;
    }

    private static int ExecuteLokadScriptByteSafeAcrossSamples(byte[][] sampleBytes, Func<byte[], int> measureOne)
    {
        var total = 0;
        for (var i = 0; i < sampleBytes.Length; i++)
        {
            total += measureOne(sampleBytes[i]);
        }

        return total;
    }

    private static int ExecuteByteSafeAnchorCandidateCount(Utf8PreparedRegex regexPlan, byte[] input)
    {
        if (!regexPlan.DeterministicAnchor.HasValue)
        {
            return 0;
        }

        var count = 0;
        var anchor = regexPlan.DeterministicAnchor;
        for (var candidate = FindNextAnchorCandidate(input, anchor, 0);
             candidate >= 0;
             candidate = FindNextAnchorCandidate(input, anchor, candidate + 1))
        {
            var matchStart = candidate - anchor.Offset;
            if ((uint)matchStart > (uint)input.Length)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static int ExecuteByteSafeAnchorCandidateIndexSum(Utf8PreparedRegex regexPlan, byte[] input)
    {
        if (!regexPlan.DeterministicAnchor.HasValue)
        {
            return 0;
        }

        var sum = 0;
        var anchor = regexPlan.DeterministicAnchor;
        for (var candidate = FindNextAnchorCandidate(input, anchor, 0);
             candidate >= 0;
             candidate = FindNextAnchorCandidate(input, anchor, candidate + 1))
        {
            var matchStart = candidate - anchor.Offset;
            if ((uint)matchStart > (uint)input.Length)
            {
                continue;
            }

            sum += matchStart;
        }

        return sum;
    }

    private static int ExecuteByteSafeVerifierCount(Utf8PreparedRegex regexPlan, Utf8VerifierRuntime verifierRuntime, byte[] input)
    {
        var count = 0;
        var startIndex = 0;
        while (Utf8ByteSafeLinearExecutor.FindNext(input, regexPlan, verifierRuntime.StructuralVerifierRuntime, startIndex, budget: Utf8ExecutionDeadline.Infinite, out var matchedLength) is var matchIndex && matchIndex >= 0)
        {
            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int ExecuteByteSafeVerifierIndexSum(Utf8PreparedRegex regexPlan, Utf8VerifierRuntime verifierRuntime, byte[] input)
    {
        var sum = 0;
        var startIndex = 0;
        while (Utf8ByteSafeLinearExecutor.FindNext(input, regexPlan, verifierRuntime.StructuralVerifierRuntime, startIndex, budget: Utf8ExecutionDeadline.Infinite, out var matchedLength) is var matchIndex && matchIndex >= 0)
        {
            sum += matchIndex;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return sum;
    }

    private static int ExecuteByteSafeStructuralCandidateCount(Utf8PreparedRegex regexPlan, byte[] input)
    {
        if (!regexPlan.StructuralSearchPlan.HasValue ||
            regexPlan.StructuralSearchPlan.YieldKind != Utf8StructuralSearchYieldKind.Start ||
            !CanMeasureByteSafeStructuralCandidates(regexPlan))
        {
            return 0;
        }

        var count = 0;
        var state = new Utf8StructuralSearchState(
            new PreparedSearchScanState(0, default),
            new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));
        while (regexPlan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteByteSafeStructuralCandidateIndexSum(Utf8PreparedRegex regexPlan, byte[] input)
    {
        if (!regexPlan.StructuralSearchPlan.HasValue ||
            regexPlan.StructuralSearchPlan.YieldKind != Utf8StructuralSearchYieldKind.Start ||
            !CanMeasureByteSafeStructuralCandidates(regexPlan))
        {
            return 0;
        }

        var sum = 0;
        var state = new Utf8StructuralSearchState(
            new PreparedSearchScanState(0, default),
            new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));
        while (regexPlan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate))
        {
            sum += candidate.StartIndex;
        }

        return sum;
    }

    private static bool CanMeasureByteSafeStructuralCandidates(Utf8PreparedRegex regexPlan)
    {
        if (!regexPlan.StructuralSearchPlan.HasValue)
        {
            return false;
        }

        if (regexPlan.StructuralSearchPlan.Stages is not { Length: > 0 } stages)
        {
            return false;
        }

        foreach (var stage in stages)
        {
            if (stage.Set is not null)
            {
                // The deep benchmark candidate probe only simulates byte-level start filtering.
                // Runtime set strings that require RegexCharClass parsing are not safe here.
                return false;
            }
        }

        return true;
    }

    private static int ExecuteByteSafeStatefulAnchorCandidateCount(Utf8PreparedRegex regexPlan, byte[] input)
    {
        if (!regexPlan.DeterministicAnchor.HasValue)
        {
            return 0;
        }

        var count = 0;
        var anchor = regexPlan.DeterministicAnchor;
        var state = new PreparedSearchScanState(0, default);
        while (anchor.Searcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            var matchStart = match.Index - anchor.Offset;
            if ((uint)matchStart <= (uint)input.Length)
            {
                count++;
            }
        }

        return count;
    }

    private static int ExecuteByteSafeStatefulAnchorCandidateIndexSum(Utf8PreparedRegex regexPlan, byte[] input)
    {
        if (!regexPlan.DeterministicAnchor.HasValue)
        {
            return 0;
        }

        var sum = 0;
        var anchor = regexPlan.DeterministicAnchor;
        var state = new PreparedSearchScanState(0, default);
        while (anchor.Searcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            var matchStart = match.Index - anchor.Offset;
            if ((uint)matchStart <= (uint)input.Length)
            {
                sum += matchStart;
            }
        }

        return sum;
    }

    private static int FindNextAnchorCandidate(byte[] input, Utf8DeterministicAnchorSearch anchor, int startIndex)
    {
        if ((uint)startIndex >= (uint)input.Length)
        {
            return -1;
        }

        var relative = anchor.Searcher.FindFirst(input[startIndex..]);
        return relative < 0 ? -1 : startIndex + relative;
    }
}

