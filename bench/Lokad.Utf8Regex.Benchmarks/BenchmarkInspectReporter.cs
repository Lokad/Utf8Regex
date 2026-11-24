using System.Text;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private static bool s_wroteMeasurementEnvironment;
    private static BenchmarkEnvironmentJson? s_measurementEnvironment;
    private static int s_allocationSink;

    public static int RunInspectPattern(string pattern, string? optionsText)
    {
        var options = ParseRegexOptions(optionsText);
        var regex = new Utf8Regex(pattern, options: options);
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

        MeasureUtf8CaseLane("Utf8RegexConstruct", samples, iterations, () =>
            RuntimeHelpers.GetHashCode(new Utf8Regex(benchmarkCase.Pattern, options: benchmarkCase.Options)));
        MeasureUtf8CaseLane("Utf8CompiledConstruct", samples, iterations, () =>
            RuntimeHelpers.GetHashCode(new Utf8Regex(                benchmarkCase.Pattern, options:                 benchmarkCase.Options | RegexOptions.Compiled)));

        if (benchmarkCase.Operation == Utf8RegexBenchmarkOperation.IsMatch &&
            context.Utf8Regex.Inspection.DebugTryIsMatchExactLiteral(context.InputBytes, out _))
        {
            MeasureUtf8CaseLane("ExactLiteralSearchOnly", samples, iterations, () =>
                context.Utf8Regex.Inspection.DebugTryIsMatchExactLiteral(context.InputBytes, out var isMatch) && isMatch ? 1 : 0);

            if (TryCreateExactCompositeTrailingSearch(
                    context.Utf8Regex,
                    out var compositeSearch,
                    out _))
            {
                MeasureUtf8CaseLane("CompositeTrailingSearchOnly", samples, iterations, () =>
                    compositeSearch.IndexOf(context.InputBytes));
            }
        }

        if (benchmarkCase.Operation == Utf8RegexBenchmarkOperation.Match &&
            context.Utf8Regex.Inspection.DebugTryMatchExactLiteral(context.InputBytes, out _))
        {
            MeasureUtf8CaseLane("ExactLiteralMatchOnly", samples, iterations, () =>
            {
                context.Utf8Regex.Inspection.DebugTryMatchExactLiteral(context.InputBytes, out var match);
                return match.Success
                    ? HashCode.Combine(match.StartOffsetInBytes, match.EndOffsetInBytes)
                    : 0;
            });

            if (TryCreateExactCompositeTrailingSearch(
                    context.Utf8Regex,
                    out var compositeSearch,
                    out var matchByteLength))
            {
                MeasureUtf8CaseLane("CompositeTrailingMatchOnly", samples, iterations, () =>
                    ExecuteCompositeTrailingMatch(
                        context.InputBytes,
                        compositeSearch,
                        matchByteLength));
            }
        }

        if (benchmarkCase.Operation == Utf8RegexBenchmarkOperation.Match &&
            context.Utf8Regex.Inspection.ExecutionKind == NativeExecutionKind.ExactUtf8Literals)
        {
            var directMatch = ExecuteDirectLiteralFamilyMatch(context.Utf8Regex, context.InputBytes);
            var compiledDirectMatch = ExecuteDirectLiteralFamilyMatch(context.CompiledUtf8Regex, context.InputBytes);
            var publicMatch = context.Utf8Regex.Match(context.InputBytes);
            var compiledPublicMatch = context.CompiledUtf8Regex.Match(context.InputBytes);
            if (!MatchesExactly(publicMatch, directMatch) ||
                !MatchesExactly(compiledPublicMatch, compiledDirectMatch))
            {
                throw new InvalidOperationException("Direct literal-family Match diagnostic is not equivalent to the public result.");
            }

            var confirmedMatch = ExecuteConfirmedLiteralFamilySearch(context.Utf8Regex, context.InputBytes);
            var compiledConfirmedMatch = ExecuteConfirmedLiteralFamilySearch(context.CompiledUtf8Regex, context.InputBytes);
            var shortCircuitConfirmedMatch = ExecuteShortCircuitBoundaryFamilySearch(context.Utf8Regex, context.InputBytes);
            var literalBoundaryWordKinds = CreateLiteralBoundaryWordKinds(
                context.Utf8Regex.Inspection.SearchPlan.AlternateLiteralsUtf8);
            var literalEdgeConfirmedMatch = ExecuteLiteralEdgeBoundaryFamilySearch(
                context.Utf8Regex,
                context.InputBytes,
                literalBoundaryWordKinds);
            if (context.Utf8Regex.Inspection.SearchPlan.HasBoundaryRequirements &&
                (confirmedMatch.Index != shortCircuitConfirmedMatch.Index ||
                 confirmedMatch.Length != shortCircuitConfirmedMatch.Length ||
                 confirmedMatch.LiteralId != shortCircuitConfirmedMatch.LiteralId ||
                 confirmedMatch.Index != literalEdgeConfirmedMatch.Index ||
                 confirmedMatch.Length != literalEdgeConfirmedMatch.Length ||
                 confirmedMatch.LiteralId != literalEdgeConfirmedMatch.LiteralId))
            {
                throw new InvalidOperationException("Boundary confirmation diagnostics are not equivalent.");
            }
            var rawCandidateCount = CountRawLiteralFamilyCandidatesThrough(
                context.Utf8Regex,
                context.InputBytes,
                confirmedMatch);
            Console.WriteLine($"RawCandidatesThroughMatch: {rawCandidateCount:N0}");

            MeasureUtf8CaseLane("RawFamilyCandidatesThroughMatch", samples, iterations, () =>
                CountRawLiteralFamilyCandidatesThrough(context.Utf8Regex, context.InputBytes, confirmedMatch));
            MeasureUtf8CaseLane("ConfirmedFamilySearchOnly", samples, iterations, () =>
                EncodePreparedSearchMatch(ExecuteConfirmedLiteralFamilySearch(context.Utf8Regex, context.InputBytes)));
            MeasureUtf8CaseLane("CompiledConfirmedFamilySearchOnly", samples, iterations, () =>
                EncodePreparedSearchMatch(ExecuteConfirmedLiteralFamilySearch(context.CompiledUtf8Regex, context.InputBytes)));
            if (context.Utf8Regex.Inspection.SearchPlan.HasBoundaryRequirements &&
                !context.Utf8Regex.Inspection.SearchPlan.HasAlternateLiteralPrefixOverlap &&
                !context.Utf8Regex.Inspection.SearchPlan.HasAlternateLiteralProperStartOverlap)
            {
                MeasureUtf8CaseLane("ShortCircuitBoundarySearchOnly", samples, iterations, () =>
                    EncodePreparedSearchMatch(ExecuteShortCircuitBoundaryFamilySearch(context.Utf8Regex, context.InputBytes)));
                MeasureUtf8CaseLane("CompiledShortCircuitBoundarySearchOnly", samples, iterations, () =>
                    EncodePreparedSearchMatch(ExecuteShortCircuitBoundaryFamilySearch(context.CompiledUtf8Regex, context.InputBytes)));
                MeasureUtf8CaseLane("LiteralEdgeBoundarySearchOnly", samples, iterations, () =>
                    EncodePreparedSearchMatch(ExecuteLiteralEdgeBoundaryFamilySearch(
                        context.Utf8Regex,
                        context.InputBytes,
                        literalBoundaryWordKinds)));
                MeasureUtf8CaseLane("CompiledLiteralEdgeBoundarySearchOnly", samples, iterations, () =>
                    EncodePreparedSearchMatch(ExecuteLiteralEdgeBoundaryFamilySearch(
                        context.CompiledUtf8Regex,
                        context.InputBytes,
                        literalBoundaryWordKinds)));
                MeasureUtf8CaseLane("Utf8IsMatchControl", samples, iterations, () =>
                    context.Utf8Regex.IsMatch(context.InputBytes) ? 1 : 0);
                MeasureUtf8CaseLane("CompiledUtf8IsMatchControl", samples, iterations, () =>
                    context.CompiledUtf8Regex.IsMatch(context.InputBytes) ? 1 : 0);
            }
            MeasureUtf8CaseLane("DirectEnumerationFamilyMatchOnly", samples, iterations, () =>
                EncodeValueMatch(ExecuteDirectLiteralFamilyMatch(context.Utf8Regex, context.InputBytes)));
            MeasureUtf8CaseLane("CompiledDirectEnumerationFamilyMatchOnly", samples, iterations, () =>
                EncodeValueMatch(ExecuteDirectLiteralFamilyMatch(context.CompiledUtf8Regex, context.InputBytes)));
        }

        if (benchmarkCase.Operation == Utf8RegexBenchmarkOperation.Replace)
        {
            EnsureEquivalentReplaceResults(context);
            var destinationLength = context.Utf8Regex.Replace(
                context.InputBytes,
                context.ReplacementUtf8).Length;
            var ordinaryUtf8Destination = new byte[destinationLength];
            var compiledUtf8Destination = new byte[destinationLength];
            MeasureUtf8CaseLane("DestinationUtf8Ordinary", samples, iterations, () =>
                ExecuteTryReplace(
                    context.Utf8Regex,
                    context.InputBytes,
                    context.ReplacementUtf8,
                    ordinaryUtf8Destination));
            MeasureUtf8CaseLane("DestinationUtf8Compiled", samples, iterations, () =>
                ExecuteTryReplace(
                    context.CompiledUtf8Regex,
                    context.InputBytes,
                    context.ReplacementUtf8,
                    compiledUtf8Destination));
        }

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

        if (regex.Inspection.ExecutionKind == NativeExecutionKind.ExactUtf8Literals)
        {
            var plan = regex.Inspection.SearchPlan;
            var expectedCount = ExecutePublicEnumeratorMoveNextCount(regex, context.InputBytes);
            var expectedIndexSum = ExecutePublicEnumeratorIndexSum(regex, context.InputBytes);
            if (ExecutePreparedSearcherCountWithBoundaries(plan.PreparedSearcher, plan, context.InputBytes) != expectedCount ||
                ExecuteExactUtf8LiteralFamilyConfirmedCount(plan, context.InputBytes, pollDeadline: false) != expectedCount ||
                ExecuteExactUtf8LiteralFamilyConfirmedCount(plan, context.InputBytes, pollDeadline: true) != expectedCount ||
                ExecuteExactUtf8LiteralFamilyBoundaryFilteredIndexSum(plan, context.InputBytes) != expectedIndexSum ||
                ExecuteExactUtf8LiteralFamilyConfirmedIndexSum(plan, context.InputBytes, pollDeadline: false) != expectedIndexSum ||
                ExecuteExactUtf8LiteralFamilyConfirmedIndexSum(plan, context.InputBytes, pollDeadline: true) != expectedIndexSum)
            {
                throw new InvalidOperationException("Exact UTF-8 literal-family diagnostic phases disagree with the public enumerator.");
            }
        }

        Measure("PublicMoveNext", iterations, () => ExecutePublicEnumeratorMoveNextCount(regex, context.InputBytes));
        Measure("PublicIndexSum", iterations, () => ExecutePublicEnumeratorIndexSum(regex, context.InputBytes));

        if (regex.Inspection.ExecutionKind == NativeExecutionKind.ExactUtf8Literal && regex.Inspection.SearchPlan.LiteralUtf8 is { Length: > 0 } literal)
        {
            Measure("SearchOnlyCount", iterations, () => ExecuteExactUtf8LiteralSearchCount(regex.Inspection.SearchPlan, literal, context.InputBytes));
            Measure("DirectEnumeratorMoveNext", iterations, () => ExecuteDirectExactUtf8LiteralEnumeratorMoveNext(regex.Inspection.PreparedRegex, literal, context.InputBytes));
            Measure("DirectEnumeratorIndexSum", iterations, () => ExecuteDirectExactUtf8LiteralEnumeratorIndexSum(regex.Inspection.PreparedRegex, literal, context.InputBytes));
            if (regex.Inspection.SearchPlan.LiteralSearch is { } literalSearch)
            {
                Measure("DirectSearchCount", iterations, () => ExecutePreparedSubstringCount(literalSearch, context.InputBytes));
            }
        }
        else if (regex.Inspection.ExecutionKind == NativeExecutionKind.ExactUtf8Literals)
        {
            Measure("SearchOnlyCount", iterations, () => ExecuteExactUtf8LiteralFamilySearchCount(regex.Inspection.SearchPlan, context.InputBytes));
            Measure("BoundaryFilteredCount", iterations, () => ExecutePreparedSearcherCountWithBoundaries(regex.Inspection.SearchPlan.PreparedSearcher, regex.Inspection.SearchPlan, context.InputBytes));
            Measure("ConfirmedCount", iterations, () => ExecuteExactUtf8LiteralFamilyConfirmedCount(regex.Inspection.SearchPlan, context.InputBytes, pollDeadline: false));
            Measure("BudgetedConfirmedCount", iterations, () => ExecuteExactUtf8LiteralFamilyConfirmedCount(regex.Inspection.SearchPlan, context.InputBytes, pollDeadline: true));
            Measure("DirectEnumeratorMoveNext", iterations, () => ExecuteDirectExactUtf8LiteralFamilyEnumeratorMoveNext(regex.Inspection.PreparedRegex, context.InputBytes));
            Measure("DirectEnumeratorIndexSum", iterations, () => ExecuteDirectExactUtf8LiteralFamilyEnumeratorIndexSum(regex.Inspection.PreparedRegex, context.InputBytes));
            Measure("BoundaryFilteredIndexSum", iterations, () => ExecuteExactUtf8LiteralFamilyBoundaryFilteredIndexSum(regex.Inspection.SearchPlan, context.InputBytes));
            Measure("ConfirmedIndexSum", iterations, () => ExecuteExactUtf8LiteralFamilyConfirmedIndexSum(regex.Inspection.SearchPlan, context.InputBytes, pollDeadline: false));
            Measure("BudgetedConfirmedIndexSum", iterations, () => ExecuteExactUtf8LiteralFamilyConfirmedIndexSum(regex.Inspection.SearchPlan, context.InputBytes, pollDeadline: true));
        }

        Measure("DecodeThenRegex", iterations, () => ExecuteDecodeThenRegex(context));
        Measure("PredecodedRegex", iterations, () => ExecutePredecodedRegex(context));
        return 0;
    }

    public static int RunMeasureUtf8SplitCase(string caseId, string? iterationsText, string? samplesText)
    {
        var benchmarkCase = Utf8RegexBenchmarkCatalog.Get(caseId);
        if (benchmarkCase.Operation != Utf8RegexBenchmarkOperation.EnumerateSplits)
        {
            Console.Error.WriteLine($"Case '{caseId}' is not an EnumerateSplits case.");
            return 1;
        }

        var context = new Utf8RegexBenchmarkContext(benchmarkCase);
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"ExecutionKind     : {context.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledKind      : {context.CompiledUtf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CanUseNative      : {context.Utf8Regex.CanUseNativeSplit()}");
        Console.WriteLine($"CompiledCanUseNative: {context.CompiledUtf8Regex.CanUseNativeSplit()}");

        MeasureUtf8CaseLane("Utf8Regex", samples, iterations, () => ExecuteUtf8(context));
        MeasureUtf8CaseLane("Utf8Compiled", samples, iterations, () => ExecuteUtf8Compiled(context));
        MeasureUtf8CaseLane("Utf8ManualOrdinary", samples, iterations, () =>
            SumUtf8SplitsManual(context.Utf8Regex, context.InputBytes));
        MeasureUtf8CaseLane("Utf8ManualCompiled", samples, iterations, () =>
            SumUtf8SplitsManual(context.CompiledUtf8Regex, context.InputBytes));
        MeasureUtf8CaseLane("DecodeThenRegex", samples, iterations, () => ExecuteDecodeThenRegex(context));
        MeasureUtf8CaseLane("DecodeThenCompiled", samples, iterations, () => ExecuteDecodeThenCompiledRegex(context));
        MeasureUtf8CaseLane("PredecodedRegex", samples, iterations, () => ExecutePredecodedRegex(context));
        MeasureUtf8CaseLane("PredecodedCompiled", samples, iterations, () => ExecutePredecodedCompiledRegex(context));
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
            return RunMeasureLokadScriptPrefixCase(caseId, iterationsText);
        }

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine("MeasureDeep       : case was not found in any benchmark catalog.");
        return 1;
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
                    Measure("PreparedCount", iterations, () => ExecutePreparedSubstringCount(ignoreCaseSearch, benchmarkCase.InputBytes));
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

        Measure("RequiredPrefilterOnly", iterations, () => Utf8PrefilterReplay.Rejects(benchmarkCase.Utf8Regex.Inspection.SearchPlan, benchmarkCase.InputBytes) ? 1 : 0);

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
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, options: benchmarkCase.Options | RegexOptions.Compiled);
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
        var correlatedPrefilter = default(PreparedMultiLiteralPackedNibbleSimdPrefilter);
        PreparedAsciiIgnoreCaseLiteralSetSearch? ignoreCaseLiteralSearch = null;

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"BaselineEngine    : {baselineRegex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"BaselinePrepared  : {baselinePlan.PreparedSearcher.Kind}");
        Console.WriteLine($"CompiledPrepared  : {compiledPlan.PreparedSearcher.Kind}");
        Console.WriteLine($"LiteralCount      : {baselinePlan.MultiLiteralSearch.Literals.Length}");
        Console.WriteLine($"Samples           : {samples}");
        if (baselinePlan.AlternateIgnoreCaseLiteralSearch is { } ignoreCaseSearch)
        {
            ignoreCaseLiteralSearch = ignoreCaseSearch;
            correlatedPrefilter = PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(
                ignoreCaseSearch.Buckets.SelectMany(static bucket => bucket.Literals).ToArray());
            Console.WriteLine($"CorrelatedOffsets: {string.Join(',', correlatedPrefilter.MaskOffsets)}");
            var candidates = CountIgnoreCaseLiteralFamilyCandidates(ignoreCaseSearch, benchmarkCase.InputBytes);
            Console.WriteLine($"FirstByteCandidates: {candidates.FirstByte}");
            Console.WriteLine($"PrefixCandidates  : {candidates.CorrelatedPrefix}");
            Console.WriteLine($"PrefixLength      : {candidates.PrefixLength}");
        }

        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);

        if (ignoreCaseLiteralSearch is { } directSearch && correlatedPrefilter.HasValue)
        {
            Measure("CorrelatedCandidates", samples, iterations, () =>
                ExecuteCorrelatedCandidateCount(correlatedPrefilter, benchmarkCase.InputBytes));
            Measure("CorrelatedBoundaryCount", samples, iterations, () =>
                ExecuteIgnoreCaseCorrelatedCountWithBoundaries(
                    correlatedPrefilter,
                    directSearch,
                    baselinePlan,
                    benchmarkCase.InputBytes));
        }
        Measure("BaselinePreparedBoundaryCount", samples, iterations, () => ExecutePreparedSearcherCountWithBoundaries(baselinePlan.PreparedSearcher, baselinePlan, benchmarkCase.InputBytes));
        Measure("CompiledPreparedBoundaryCount", samples, iterations, () => ExecutePreparedSearcherCountWithBoundaries(compiledPlan.PreparedSearcher, compiledPlan, benchmarkCase.InputBytes));


        Measure("Utf8Regex", samples, iterations, () => baselineRegex.Count(benchmarkCase.InputBytes));
        Measure("Utf8Compiled", samples, iterations, () => compiledUtf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", samples, iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", samples, iterations, benchmarkCase.CountDecodeThenCompiledRegex);
        Measure("PredecodedRegex", samples, iterations, benchmarkCase.CountPredecodedRegex);
        Measure("CompiledRegex", samples, iterations, benchmarkCase.CountPredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureSmallAsciiLiteralFamilyPrimitiveCase(string caseId, string? iterationsText)
    {
        try
        {
            var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
            var regex = benchmarkCase.Utf8Regex;
            var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, options: benchmarkCase.Options | RegexOptions.Compiled);
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

            Measure("PrimitiveCount", iterations, () => primitive.Count(benchmarkCase.InputBytes));
            Measure("PreparedBoundaryCount", iterations, () => ExecutePreparedSearcherCountWithBoundaries(regex.Inspection.SearchPlan.PreparedSearcher, regex.Inspection.SearchPlan, benchmarkCase.InputBytes));

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

            Measure("PrimitiveCount", iterations, () => primitive.Count(context.InputBytes));
            Measure("PreparedBoundaryCount", iterations, () => ExecutePreparedSearcherCountWithBoundaries(regex.Inspection.SearchPlan.PreparedSearcher, regex.Inspection.SearchPlan, context.InputBytes));

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

        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Utf8Pattern, options: benchmarkCase.Options | RegexOptions.Compiled);
        Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(benchmarkCase.InputBytes));

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
        var snapshot = LoadReadmePresentationSnapshot();
        foreach (var section in ParseReadmeSections(sectionsText))
        {
            Console.Write(BuildReadmeSectionMarkdownFromSnapshot(section, snapshot));
            Console.WriteLine();
        }

        return 0;
    }

    public static int RunRewriteReadmeBenchmarkMarkdown()
    {
        RewriteReadmeFromSnapshot(LoadReadmePresentationSnapshot());
        Console.WriteLine("Rewrote README.md benchmark sections from bench/Snapshots/Core.json.");
        return 0;
    }

    public static int RunRefreshReadmeBenchmarks(string? sectionsText, string? iterationsText, string? samplesText, bool resume = false)
    {
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var sections = ParseReadmeSections(sectionsText);
        var snapshot = LoadReadmeBenchmarkSnapshot(startFreshIfMissing:
            sections.Count == Enum.GetValues<ReadmeBenchmarkSection>().Length);
        var remainingSections = sections.ToHashSet();

        RefreshReadmeSnapshotSectionPairIfRequested(
            snapshot,
            remainingSections,
            ReadmeBenchmarkSection.DotNetPerformance,
            ReadmeBenchmarkSection.DotNetPerformanceCompiled,
            iterations,
            samples,
            resume);
        RefreshReadmeSnapshotSectionPairIfRequested(
            snapshot,
            remainingSections,
            ReadmeBenchmarkSection.Lokad,
            ReadmeBenchmarkSection.LokadCompiled,
            iterations,
            samples,
            resume);

        foreach (var section in sections.Where(remainingSections.Contains))
        {
            RefreshReadmeSnapshotSection(snapshot, section, iterations, samples, resume);
        }

        SaveReadmeBenchmarkSnapshot(snapshot, publishPresentation: true);
        Console.WriteLine($"Updated README sections: {string.Join(", ", sections.Select(static s => s.ToString()))}");
        return 0;
    }

    public static int RunRefreshReadmeCase(string caseId, string? iterationsText, string? samplesText)
    {
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var snapshot = LoadReadmeBenchmarkSnapshot();

        var targets = GetReadmeTargetsForCase(caseId).ToArray();
        if (targets.Length == 0)
        {
            throw new InvalidOperationException($"README benchmark case '{caseId}' does not belong to a published section.");
        }

        var measurement = MeasureReadmeSnapshotCase(targets[0], caseId, iterations, samples);
        foreach (var target in targets)
        {
            GetOrAddSnapshotSection(snapshot, target).Cases[caseId] = ReadmeCaseMeasurementJson.FromMeasurement(measurement);
        }

        SaveReadmeBenchmarkSnapshot(snapshot, publishPresentation: true);
        Console.WriteLine($"Updated README case: {caseId}");
        return 0;
    }

    public static int RunMeasureReadmeCase(string caseId, string? iterationsText, string? samplesText)
        => RunMeasureReadmeCase(caseId, iterationsText, samplesText, reverseMeasurementOrder: false);

    public static int RunMeasureReadmeCaseReversed(string caseId, string? iterationsText, string? samplesText)
        => RunMeasureReadmeCase(caseId, iterationsText, samplesText, reverseMeasurementOrder: true);

    private static int RunMeasureReadmeCase(
        string caseId, string? iterationsText, string? samplesText, bool reverseMeasurementOrder)
    {
        var requestedIterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        Console.WriteLine("InputContract     : caller-valid unchanged UTF-8");
        Console.WriteLine($"Environment       : {JsonSerializer.Serialize(CaptureBenchmarkEnvironment())}");

        var lokadScriptCase = LokadReplicaScriptBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        if (lokadScriptCase is not null)
        {
            var context = new LokadReplicaScriptBenchmarkContext(lokadScriptCase);
            var expected = lokadScriptCase.Model == LokadReplicaScriptBenchmarkModel.PrefixMatchLoop
                ? context.VerifyPrefixMatchResults() : (Bytes: 0, Units: 0);
            var scriptIterationTarget = GetReadmeLokadScriptIterationTarget(lokadScriptCase, requestedIterations);
            var scriptRow = MeasureReadmeCase(
                requestedIterations,
                scriptIterationTarget,
                samples,
                () => context.ExecuteUtf8Regex() - expected.Bytes,
                () => context.ExecuteUtf8Compiled() - expected.Bytes,
                () => context.ExecutePredecodedRegex() - expected.Units,
                () => context.ExecutePredecodedCompiledRegex() - expected.Units,
                () => context.ExecuteDecodeThenRegex() - expected.Units,
                () => context.ExecuteDecodeThenCompiledRegex() - expected.Units,
                reverseMeasurementOrder);
            Console.WriteLine(FormatReadmeCaseRow(scriptRow));
            return 0;
        }

        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var utf8Regex = new Utf8Regex(benchmarkCase.Utf8Pattern, options: benchmarkCase.Options);
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Utf8Pattern, options: benchmarkCase.Options | RegexOptions.Compiled);
        Console.WriteLine($"CaseId            : {caseId}; InputBytes={benchmarkCase.InputBytes.Length}; InputSha256={Convert.ToHexString(SHA256.HashData(benchmarkCase.InputBytes))}");
        var iterationTarget = GetReadmeReplicaIterationTarget(benchmarkCase, requestedIterations);
        var row = MeasureReadmeCase(
            requestedIterations,
            iterationTarget,
            samples,
            () => utf8Regex.Count(benchmarkCase.InputBytes),
            () => compiledUtf8Regex.Count(benchmarkCase.InputBytes),
            benchmarkCase.CountPredecodedRegex,
            benchmarkCase.CountPredecodedCompiledRegex,
            benchmarkCase.CountDecodeThenRegex,
            benchmarkCase.CountDecodeThenCompiledRegex,
            reverseMeasurementOrder);
        Console.WriteLine(FormatReadmeCaseRow(row));
        return 0;
    }

    public static int RunMeasureReadmePublicCase(string caseId, string? iterationsText, string? samplesText)
        => RunMeasureReadmePublicCase(caseId, iterationsText, samplesText, reverseMeasurementOrder: false);

    public static int RunMeasureReadmePublicCaseReversed(string caseId, string? iterationsText, string? samplesText)
        => RunMeasureReadmePublicCase(caseId, iterationsText, samplesText, reverseMeasurementOrder: true);

    private static int RunMeasureReadmePublicCase(
        string caseId, string? iterationsText, string? samplesText, bool reverseMeasurementOrder)
    {
        var requestedIterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var context = new LokadPublicBenchmarkContext(caseId);
        context.VerifyMatchRanges();
        Console.WriteLine("InputContract     : caller-valid unchanged UTF-8");
        Console.WriteLine($"Environment       : {JsonSerializer.Serialize(CaptureBenchmarkEnvironment())}");
        Console.WriteLine($"CaseId            : {caseId}; InputBytes={context.InputBytes.Length}; InputSha256={Convert.ToHexString(SHA256.HashData(context.InputBytes))}");
        var iterationTarget = GetReadmePublicIterationTarget(context, requestedIterations);
        var row = MeasureReadmeCase(
            requestedIterations,
            iterationTarget,
            samples,
            context.ExecuteUtf8Regex,
            context.ExecuteUtf8Compiled,
            context.ExecutePredecodedRegex,
            context.ExecutePredecodedCompiledRegex,
            context.ExecuteDecodeThenRegex,
            context.ExecuteDecodeThenCompiledRegex,
            reverseMeasurementOrder);
        Console.WriteLine(FormatReadmeCaseRow(row));
        return 0;
    }

    private const string ReadmeBenchmarkSnapshotFileName = "README.Benchmarks.json";
    private const string ReadmeMeasurementProtocol = "IndependentSixLaneV3";

    private static ReadmeBenchmarkSnapshot LoadReadmeBenchmarkSnapshot(bool startFreshIfMissing = false)
    {
        var path = GetReadmeSnapshotPath();
        if (startFreshIfMissing && !File.Exists(path)) return new ReadmeBenchmarkSnapshot();
        var json = File.ReadAllText(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<ReadmeBenchmarkSnapshot>(json, ReadmeBenchmarkSnapshotJsonOptions) ?? throw new InvalidOperationException($"Could not deserialize {ReadmeBenchmarkSnapshotFileName}.");
    }

    private static void SaveReadmeBenchmarkSnapshot(ReadmeBenchmarkSnapshot snapshot, bool publishPresentation = false)
    {
        snapshot.SchemaVersion = 6;
        var path = GetReadmeSnapshotPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(snapshot, ReadmeBenchmarkSnapshotJsonOptions);
        BenchmarkFileWriter.WriteTextAtomically(path, json + Environment.NewLine);
        // A fresh or interrupted refresh can checkpoint an incomplete catalog.
        // The parity report and README still require every published row.
        if (!publishPresentation) return;
        WriteReadmeParityReport(snapshot, path);
        WriteReadmeBenchmarkSummary(snapshot);
        RewriteReadmeFromSnapshot(snapshot);
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

    private static void RefreshReadmeSnapshotSection(
        ReadmeBenchmarkSnapshot snapshot,
        ReadmeBenchmarkSection section,
        int iterations,
        int samples,
        bool resume)
    {
        var sectionSnapshot = GetOrAddSnapshotSection(snapshot, section);
        var expectedCaseIds = new HashSet<string>(StringComparer.Ordinal);

        void MeasureAndRecord(string command, string caseId)
        {
            expectedCaseIds.Add(caseId);
            if (resume &&
                sectionSnapshot.Cases.TryGetValue(caseId, out var existing) &&
                IsCurrentReadmeMeasurement(existing, iterations, samples))
            {
                Console.WriteLine($"Skipping current README benchmark case: {caseId}");
                return;
            }

            var measurement = MeasureReadmeCaseOutOfProcess(command, caseId, iterations, samples);
            sectionSnapshot.Cases[caseId] = ReadmeCaseMeasurementJson.FromMeasurement(measurement);
            SaveReadmeBenchmarkSnapshot(snapshot);
        }

        switch (section)
        {
            case ReadmeBenchmarkSection.DotNetPerformance:
            case ReadmeBenchmarkSection.DotNetPerformanceCompiled:
                foreach (var caseId in ReplicaCountBenchmarkCase.GetAllIds(ReplicaBenchmarkSource.DotNetPerformance))
                {
                    MeasureAndRecord("--measure-readme-case", caseId);
                }

                foreach (var caseId in LokadPublicBenchmarkContext.GetAllCaseIds())
                {
                    MeasureAndRecord("--measure-readme-public-case", caseId);
                }
                break;

            case ReadmeBenchmarkSection.Lokad:
            case ReadmeBenchmarkSection.LokadCompiled:
                foreach (var caseId in ReplicaCountBenchmarkCase.GetAllIds(ReplicaBenchmarkSource.Lokad))
                {
                    MeasureAndRecord("--measure-readme-case", caseId);
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }

        RemoveUnexpectedReadmeCases(sectionSnapshot, expectedCaseIds);
        SaveReadmeBenchmarkSnapshot(snapshot);
    }

    private static void RefreshReadmeSnapshotSectionPairIfRequested(
        ReadmeBenchmarkSnapshot snapshot,
        HashSet<ReadmeBenchmarkSection> remainingSections,
        ReadmeBenchmarkSection firstSection,
        ReadmeBenchmarkSection secondSection,
        int iterations,
        int samples,
        bool resume)
    {
        if (!remainingSections.Contains(firstSection) || !remainingSections.Contains(secondSection))
        {
            return;
        }

        var firstSnapshot = GetOrAddSnapshotSection(snapshot, firstSection);
        var secondSnapshot = GetOrAddSnapshotSection(snapshot, secondSection);
        var expectedCaseIds = new HashSet<string>(StringComparer.Ordinal);

        void MeasureAndRecord(string command, string caseId)
        {
            expectedCaseIds.Add(caseId);
            if (resume &&
                firstSnapshot.Cases.TryGetValue(caseId, out var firstExisting) &&
                secondSnapshot.Cases.TryGetValue(caseId, out var secondExisting) &&
                IsCurrentReadmeMeasurement(firstExisting, iterations, samples) &&
                IsCurrentReadmeMeasurement(secondExisting, iterations, samples) &&
                HaveSameReadmeMeasurement(firstExisting, secondExisting))
            {
                Console.WriteLine($"Skipping current paired README benchmark case: {caseId}");
                return;
            }

            var measurement = MeasureReadmeCaseOutOfProcess(command, caseId, iterations, samples);
            firstSnapshot.Cases[caseId] = ReadmeCaseMeasurementJson.FromMeasurement(measurement);
            secondSnapshot.Cases[caseId] = ReadmeCaseMeasurementJson.FromMeasurement(measurement);
            SaveReadmeBenchmarkSnapshot(snapshot);
        }

        if (firstSection == ReadmeBenchmarkSection.DotNetPerformance)
        {
            foreach (var caseId in ReplicaCountBenchmarkCase.GetAllIds(ReplicaBenchmarkSource.DotNetPerformance))
            {
                MeasureAndRecord("--measure-readme-case", caseId);
            }

            foreach (var caseId in LokadPublicBenchmarkContext.GetAllCaseIds())
            {
                MeasureAndRecord("--measure-readme-public-case", caseId);
            }
        }
        else if (firstSection == ReadmeBenchmarkSection.Lokad)
        {
            foreach (var caseId in ReplicaCountBenchmarkCase.GetAllIds(ReplicaBenchmarkSource.Lokad))
            {
                MeasureAndRecord("--measure-readme-case", caseId);
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(firstSection));
        }

        RemoveUnexpectedReadmeCases(firstSnapshot, expectedCaseIds);
        RemoveUnexpectedReadmeCases(secondSnapshot, expectedCaseIds);
        SaveReadmeBenchmarkSnapshot(snapshot);
        remainingSections.Remove(firstSection);
        remainingSections.Remove(secondSection);
    }

    private static bool IsCurrentReadmeMeasurement(
        ReadmeCaseMeasurementJson measurement,
        int requestedIterations,
        int samples)
    {
        var current = CaptureBenchmarkEnvironment();
        var measured = measurement.Environment;
        return !current.TrackedDirty &&
            measured is not null &&
            !measured.TrackedDirty &&
            measurement.MeasurementProtocol == ReadmeMeasurementProtocol &&
            measurement.RequestedIterations == requestedIterations &&
            measurement.EffectiveIterations >= requestedIterations &&
            measurement.Samples == samples &&
            measurement.Utf8Regex > 0 &&
            measurement.Utf8Compiled > 0 &&
            measurement.PredecodedRegex > 0 &&
            measurement.CompiledRegex > 0 &&
            measurement.DecodeThenRegex > 0 &&
            measurement.DecodeThenCompiledRegex > 0 &&
            measured.SourceCommit == current.SourceCommit &&
            measured.BenchmarkAssembly == current.BenchmarkAssembly &&
            measured.CoreAssembly == current.CoreAssembly &&
            measured.Pcre2Assembly == current.Pcre2Assembly &&
            measured.PythonReAssembly == current.PythonReAssembly &&
            measured.Runtime == current.Runtime &&
            measured.OperatingSystem == current.OperatingSystem &&
            measured.Processor == current.Processor &&
            measured.TieredPgo == current.TieredPgo;
    }

    private static bool HaveSameReadmeMeasurement(
        ReadmeCaseMeasurementJson first,
        ReadmeCaseMeasurementJson second)
        => first.MeasurementProtocol == second.MeasurementProtocol &&
            JsonSerializer.Serialize(first.ToMeasurement()) == JsonSerializer.Serialize(second.ToMeasurement());

    private static void RemoveUnexpectedReadmeCases(
        ReadmeBenchmarkSectionJson section,
        HashSet<string> expectedCaseIds)
    {
        foreach (var caseId in section.Cases.Keys.Where(caseId => !expectedCaseIds.Contains(caseId)).ToArray())
        {
            section.Cases.Remove(caseId);
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
        writer.WriteLine("| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |");
        writer.WriteLine("|---|---:|---:|---:|---:|");
    }

    private static void WriteReadmeCompiledTableHeader(StringWriter writer)
    {
        writer.WriteLine("| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |");
        writer.WriteLine("|---|---:|---:|---:|---:|");
    }

    private static void WriteReadmePublicDefaultTableHeader(StringWriter writer)
    {
        writer.WriteLine("| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |");
        writer.WriteLine("|---|---|---:|---:|---:|---:|");
    }

    private static void WriteReadmePublicCompiledTableHeader(StringWriter writer)
    {
        writer.WriteLine("| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |");
        writer.WriteLine("|---|---|---:|---:|---:|---:|");
    }

    private static void WriteReadmePlainDefaultCaseRow(StringWriter writer, string caseId, ReadmeCaseMeasurement row)
    {
        writer.WriteLine($"| `{caseId}` | {FormatMicros(row.Utf8Regex)} | {FormatMicros(row.PredecodedRegex)} | {FormatMicros(row.DecodeThenRegex)} | {FormatReadmeRatio(row.Utf8Regex, row.PredecodedRegex)} |");
    }

    private static void WriteReadmePlainCompiledCaseRow(StringWriter writer, string caseId, ReadmeCaseMeasurement row)
    {
        writer.WriteLine($"| `{caseId}` | {FormatMicros(row.Utf8Compiled)} | {FormatMicros(row.CompiledRegex)} | {FormatMicros(row.DecodeThenCompiledRegex)} | {FormatReadmeRatio(row.Utf8Compiled, row.CompiledRegex)} |");
    }

    private static void WriteReadmePlainPublicDefaultCaseRow(StringWriter writer, string caseId, ReadmeCaseMeasurement row, LokadPublicBenchmarkOperation operation)
    {
        writer.WriteLine($"| `{caseId}` | `{operation}` | {FormatMicros(row.Utf8Regex)} | {FormatMicros(row.PredecodedRegex)} | {FormatMicros(row.DecodeThenRegex)} | {FormatReadmeRatio(row.Utf8Regex, row.PredecodedRegex)} |");
    }

    private static void WriteReadmePlainPublicCompiledCaseRow(StringWriter writer, string caseId, ReadmeCaseMeasurement row, LokadPublicBenchmarkOperation operation)
    {
        writer.WriteLine($"| `{caseId}` | `{operation}` | {FormatMicros(row.Utf8Compiled)} | {FormatMicros(row.CompiledRegex)} | {FormatMicros(row.DecodeThenCompiledRegex)} | {FormatReadmeRatio(row.Utf8Compiled, row.CompiledRegex)} |");
    }

    private static string FormatReadmeRatio(double utf8, double predecoded)
    {
        var ratio = utf8 / predecoded;
        return ratio.ToString(ratio is > 0 and < 0.01 ? "G2" : "F2", CultureInfo.InvariantCulture) + "x";
    }

    private static string BuildReadmeSectionMarkdownFromSnapshot(ReadmeBenchmarkSection section, ReadmeBenchmarkSnapshot snapshot)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var sectionSnapshot = GetRequiredSnapshotSection(snapshot, section);

        var compiled = section is ReadmeBenchmarkSection.DotNetPerformanceCompiled or ReadmeBenchmarkSection.LokadCompiled;
        IEnumerable<string> OrderedCases(IEnumerable<string> ids) => ids.OrderByDescending(id =>
        {
            var row = GetRequiredSnapshotMeasurement(sectionSnapshot, id);
            return compiled ? row.Utf8Compiled / row.CompiledRegex : row.Utf8Regex / row.PredecodedRegex;
        }).ThenBy(static id => id, StringComparer.Ordinal);

        switch (section)
        {
            case ReadmeBenchmarkSection.DotNetPerformance:
                writer.WriteLine("## DotNetPerformance Benchmarks");
                writer.WriteLine();
                writer.WriteLine("These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:");
                writer.WriteLine("- `Utf8Regex`: direct UTF-8 input");
                writer.WriteLine("- `.NET predecoded`: `.NET Regex` on an already-decoded `string`");
                writer.WriteLine("- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`");
                WriteReadmeComparatorPolicy(writer);
                writer.WriteLine();
                writer.WriteLine("All stress rows below are for `Count(...)`.");
                writer.WriteLine("Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.");
                writer.WriteLine();
                writer.WriteLine("### Stress Count Workloads");
                writer.WriteLine();
                WriteReadmeDefaultTableHeader(writer);
                foreach (var caseId in OrderedCases(ReplicaCountBenchmarkCase.GetAllIds(ReplicaBenchmarkSource.DotNetPerformance)))
                {
                    WriteReadmePlainDefaultCaseRow(writer, caseId, GetRequiredSnapshotMeasurement(sectionSnapshot, caseId).ToMeasurement());
                }

                writer.WriteLine();
                writer.WriteLine("### Public/Common and Industry Workloads");
                writer.WriteLine();
                writer.WriteLine("These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.");
                writer.WriteLine();
                WriteReadmePublicDefaultTableHeader(writer);
                foreach (var caseId in OrderedCases(LokadPublicBenchmarkContext.GetAllCaseIds()))
                {
                    WriteReadmePlainPublicDefaultCaseRow(writer, caseId, GetRequiredSnapshotMeasurement(sectionSnapshot, caseId).ToMeasurement(), LokadPublicBenchmarkContext.GetOperation(caseId));
                }
                break;

            case ReadmeBenchmarkSection.DotNetPerformanceCompiled:
                writer.WriteLine("## DotNetPerformance Benchmarks (Compiled)");
                writer.WriteLine();
                writer.WriteLine("These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:");
                writer.WriteLine("- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`");
                writer.WriteLine("- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`");
                writer.WriteLine("- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`");
                WriteReadmeComparatorPolicy(writer);
                writer.WriteLine();
                writer.WriteLine("All stress rows below are for `Count(...)`.");
                writer.WriteLine("Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.");
                writer.WriteLine();
                writer.WriteLine("### Stress Count Workloads");
                writer.WriteLine();
                WriteReadmeCompiledTableHeader(writer);
                foreach (var caseId in OrderedCases(ReplicaCountBenchmarkCase.GetAllIds(ReplicaBenchmarkSource.DotNetPerformance)))
                {
                    WriteReadmePlainCompiledCaseRow(writer, caseId, GetRequiredSnapshotMeasurement(sectionSnapshot, caseId).ToMeasurement());
                }

                writer.WriteLine();
                writer.WriteLine("### Public/Common and Industry Workloads");
                writer.WriteLine();
                writer.WriteLine("These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.");
                writer.WriteLine();
                WriteReadmePublicCompiledTableHeader(writer);
                foreach (var caseId in OrderedCases(LokadPublicBenchmarkContext.GetAllCaseIds()))
                {
                    WriteReadmePlainPublicCompiledCaseRow(writer, caseId, GetRequiredSnapshotMeasurement(sectionSnapshot, caseId).ToMeasurement(), LokadPublicBenchmarkContext.GetOperation(caseId));
                }
                break;

            case ReadmeBenchmarkSection.Lokad:
                writer.WriteLine("## Lokad Benchmarks");
                writer.WriteLine();
                writer.WriteLine("These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:");
                writer.WriteLine("- `Utf8Regex`: direct UTF-8 input");
                writer.WriteLine("- `.NET predecoded`: `.NET Regex` on an already-decoded `string`");
                writer.WriteLine("- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`");
                WriteReadmeComparatorPolicy(writer);
                writer.WriteLine();
                writer.WriteLine("This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.");
                writer.WriteLine();
                WriteReadmeDefaultTableHeader(writer);
                foreach (var caseId in OrderedCases(ReplicaCountBenchmarkCase.GetAllIds(ReplicaBenchmarkSource.Lokad)))
                {
                    WriteReadmePlainDefaultCaseRow(writer, caseId, GetRequiredSnapshotMeasurement(sectionSnapshot, caseId).ToMeasurement());
                }
                break;

            case ReadmeBenchmarkSection.LokadCompiled:
                writer.WriteLine("## Lokad Benchmarks (Compiled)");
                writer.WriteLine();
                writer.WriteLine("These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:");
                writer.WriteLine("- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`");
                writer.WriteLine("- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`");
                writer.WriteLine("- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`");
                WriteReadmeComparatorPolicy(writer);
                writer.WriteLine();
                writer.WriteLine("This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.");
                writer.WriteLine();
                WriteReadmeCompiledTableHeader(writer);
                foreach (var caseId in OrderedCases(ReplicaCountBenchmarkCase.GetAllIds(ReplicaBenchmarkSource.Lokad)))
                {
                    WriteReadmePlainCompiledCaseRow(writer, caseId, GetRequiredSnapshotMeasurement(sectionSnapshot, caseId).ToMeasurement());
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }

        return writer.ToString().TrimEnd();
    }

    private static void WriteReadmeComparatorPolicy(StringWriter writer)
    {
        writer.WriteLine();
        writer.WriteLine("Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.");
        writer.WriteLine("Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.");
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
            readme = BenchmarkFileWriter.ReplaceGeneratedSection(
                readme, markerName, BuildReadmeSectionMarkdownFromSnapshot(section, snapshot));
        }

        File.WriteAllText(readmePath, readme, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
            ("unicode-family", "Ã©clair|bravo|charlie", "Ã©clair bravo charlie ", false),
        ];

        Console.WriteLine($"Iterations        : {iterations}");
        foreach (var control in controls)
        {
            var input = RepeatToLength(control.Seed, 8_192);
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var utf8Regex = new Utf8Regex(control.Pattern);
            var utf8Compiled = new Utf8Regex(control.Pattern, options: RegexOptions.Compiled);
            var regex = new Regex(control.Pattern);
            var compiledRegex = new Regex(control.Pattern, RegexOptions.Compiled);
            var utf8Eligible = utf8Regex.TryGetSmallAsciiLiteralFamilySearch(out _);
            var compiledEligible = utf8Compiled.TryGetSmallAsciiLiteralFamilySearch(out _);
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

        Measure("PrefilterOnly", iterations, context.ExecuteUtf8PrefixPrefilterOnly);
        Measure("DirectUrlMatcher", iterations, context.ExecuteLokadScriptDirectUrlMatcher);
        Measure("CompiledMatchOnly", iterations, context.ExecuteUtf8PrefixCompiledMatchOnly);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
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

        Measure("PrefilterOnly", iterations, context.ExecuteUtf8CountPrefilterOnly);
        Measure("CompiledCount", iterations, context.ExecuteUtf8CountCompiled);
        Measure("FallbackCandidates", iterations, context.ExecuteUtf8CountFallbackCandidates);
        Measure("FallbackBoundaryCandidates", iterations, context.ExecuteUtf8CountFallbackBoundaryCandidates);
        Measure("FallbackVerifiedCount", iterations, context.ExecuteUtf8CountFallbackVerified);
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

        Measure("AsciiOnlyCheck", iterations, () => Utf8InputAnalyzer.IsAscii(context.InputBytes) ? context.InputBytes.Length : 0);
        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureCountAttributionCase(
        string caseId,
        string? iterationsText,
        string? samplesText)
    {
        var iterations = string.IsNullOrWhiteSpace(iterationsText) ? 20 : ParseIterations(iterationsText);
        var samples = string.IsNullOrWhiteSpace(samplesText) ? 5 : Math.Clamp(ParseSamples(samplesText), 1, 9);

        if (LokadPublicBenchmarkContext.GetAllCaseIds().Contains(caseId, StringComparer.Ordinal))
        {
            var context = new LokadPublicBenchmarkContext(caseId);
            if (context.Operation != LokadPublicBenchmarkOperation.Count)
            {
                Console.Error.WriteLine($"Case '{caseId}' is a {context.Operation} case, not a Count case.");
                return 1;
            }

            return RunMeasureCountAttributionCaseCore(
                caseId,
                origin: "public",
                context.Pattern,
                context.InputBytes,
                context.Utf8Regex,
                context.CompiledUtf8Regex,
                context.ExecutePredecodedRegex,
                context.ExecutePredecodedCompiledRegex,
                iterations,
                samples);
        }

        if (!ReplicaCountBenchmarkCase.TryResolve(caseId, out var benchmarkCase))
        {
            Console.Error.WriteLine($"Count benchmark case '{caseId}' was not found.");
            return 1;
        }

        var resolvedCase = benchmarkCase!;
        return RunMeasureCountAttributionCaseCore(
            caseId,
            resolvedCase.Source.ToString(),
            resolvedCase.Pattern,
            resolvedCase.InputBytes,
            resolvedCase.Utf8Regex,
            new Utf8Regex(resolvedCase.Utf8Pattern, options: resolvedCase.Options | RegexOptions.Compiled),
            resolvedCase.CountPredecodedRegex,
            resolvedCase.CountPredecodedCompiledRegex,
            iterations,
            samples);
    }

    private static int RunMeasureCountAttributionCaseCore(
        string caseId,
        string origin,
        string pattern,
        byte[] input,
        Utf8Regex ordinaryRegex,
        Utf8Regex compiledRegex,
        Func<int> predecodedOrdinary,
        Func<int> predecodedCompiled,
        int iterations,
        int samples)
    {
        if (!Utf8MatchInspection.TryCountSelectedKernelWithoutValidation(ordinaryRegex, input, out var ordinaryKernelResult) ||
            !Utf8MatchInspection.TryCountSelectedKernelWithoutValidation(compiledRegex, input, out var compiledKernelResult))
        {
            Console.Error.WriteLine($"Case '{caseId}' does not expose a separable selected Count kernel.");
            return 1;
        }

        var expectedResult = predecodedOrdinary();
        RequireCountResult("PredecodedCompiled", expectedResult, predecodedCompiled());
        RequireCountResult("OrdinarySelectedKernel", expectedResult, ordinaryKernelResult);
        RequireCountResult("CompiledSelectedKernel", expectedResult, compiledKernelResult);
        RequireCountResult("Utf8Regex", expectedResult, ordinaryRegex.Count(input));
        RequireCountResult("Utf8Compiled", expectedResult, compiledRegex.Count(input));

        var hasOrdinaryMetrics = Utf8CountKernelCensus.TryInspect(ordinaryRegex,
            input,
            out var ordinaryMetrics);
        var hasCompiledMetrics = Utf8CountKernelCensus.TryInspect(compiledRegex,
            input,
            out var compiledMetrics);
        if (hasOrdinaryMetrics)
        {
            RequireCountResult("OrdinarySelectedMetrics", expectedResult, ordinaryMetrics.Matches);
        }

        if (hasCompiledMetrics)
        {
            RequireCountResult("CompiledSelectedMetrics", expectedResult, compiledMetrics.Matches);
        }

        var ordinaryDiagnostics = ordinaryRegex.CollectCountDiagnostics(input);
        var compiledDiagnostics = compiledRegex.CollectCountDiagnostics(input);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Origin            : {origin}");
        Console.WriteLine($"Pattern           : {pattern}");
        Console.WriteLine($"InputBytes        : {input.Length}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine("AttributionProtocol: AlternatingSelectedCountPhasesV3");
        Console.WriteLine("Input contract    : caller-valid UTF-8 / predecoded .NET text");
        Console.WriteLine("WarmupPerLane     : at least 250 ms and 64 calls");
        Console.WriteLine($"ExpectedResult    : {expectedResult}");
        Console.WriteLine($"OrdinaryKernel    : {Utf8MatchInspection.SelectedCountKernelKind(ordinaryRegex)}");
        Console.WriteLine($"CompiledKernel    : {Utf8MatchInspection.SelectedCountKernelKind(compiledRegex)}");
        Console.WriteLine($"OrdinaryRoute     : {ordinaryDiagnostics.ExecutionRoute}");
        Console.WriteLine($"CompiledRoute     : {compiledDiagnostics.ExecutionRoute}");
        WriteSelectedCountKernelMetrics("Ordinary", hasOrdinaryMetrics, ordinaryMetrics);
        WriteSelectedCountKernelMetrics("Compiled", hasCompiledMetrics, compiledMetrics);

        var lanes = new CountAttributionLane[]
        {
            new("OrdinarySelectedKernel", () => ExecuteSelectedCountKernel(ordinaryRegex, input), expectedResult),
            new("CompiledSelectedKernel", () => ExecuteSelectedCountKernel(compiledRegex, input), expectedResult),
            new("Utf8Regex", () => ordinaryRegex.Count(input), expectedResult),
            new("Utf8Compiled", () => compiledRegex.Count(input), expectedResult),
            new("PredecodedRegex", predecodedOrdinary, expectedResult),
            new("PredecodedCompiledRegex", predecodedCompiled, expectedResult),
        };

        MeasureAlternatingCountAttribution(lanes, iterations, samples);
        return 0;
    }

    private static void WriteSelectedCountKernelMetrics(
        string label,
        bool hasMetrics,
        Utf8SelectedCountKernelMetrics metrics)
    {
        if (!hasMetrics)
        {
            Console.WriteLine($"{label}Metrics     : unavailable for selected route");
            return;
        }

        var fullVerifications = metrics.FullVerifications.HasValue
            ? metrics.FullVerifications.Value.ToString(CultureInfo.InvariantCulture)
            : "unavailable";
        var candidates = metrics.Candidates.HasValue
            ? metrics.Candidates.Value.ToString(CultureInfo.InvariantCulture)
            : "unavailable";
        var rejected = metrics.Candidates.HasValue
            ? (metrics.Candidates.Value - metrics.Matches).ToString(CultureInfo.InvariantCulture)
            : "unavailable";
        Console.WriteLine($"{label}MetricRoute : {metrics.Route}");
        Console.WriteLine($"{label}CandidateDef: {metrics.CandidateDefinition}");
        Console.WriteLine($"{label}Candidates  : {candidates}");
        Console.WriteLine($"{label}Verifies    : {fullVerifications}");
        Console.WriteLine($"{label}MetricMatches: {metrics.Matches}");
        Console.WriteLine($"{label}Rejected    : {rejected}");
    }

    private static void MeasureAlternatingCountAttribution(
        CountAttributionLane[] lanes,
        int iterations,
        int samples)
    {
        WriteMeasurementEnvironment();
        var elapsedByLane = new TimeSpan[lanes.Length][];
        var warmupCalls = new int[lanes.Length];
        for (var laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
        {
            elapsedByLane[laneIndex] = new TimeSpan[samples];
            warmupCalls[laneIndex] = WarmupForMilliseconds(
                lanes[laneIndex].Action,
                milliseconds: 250,
                minimumIterations: 64);
        }

        for (var sample = 0; sample < samples; sample++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            for (var position = 0; position < lanes.Length; position++)
            {
                var laneIndex = (sample & 1) == 0 ? position : lanes.Length - 1 - position;
                var measured = MeasureTimedCore(iterations, lanes[laneIndex].Action);
                elapsedByLane[laneIndex][sample] = measured.Elapsed;
                GC.KeepAlive(measured.Sink);
            }
        }

        for (var laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
        {
            var elapsed = elapsedByLane[laneIndex];
            Array.Sort(elapsed);
            var medianMicroseconds = elapsed[elapsed.Length / 2].TotalMicroseconds / iterations;
            var minimumMicroseconds = elapsed[0].TotalMicroseconds / iterations;
            var maximumMicroseconds = elapsed[^1].TotalMicroseconds / iterations;
            Console.WriteLine(
                $"{lanes[laneIndex].Label,-32}: {medianMicroseconds,10:F3} us/op | " +
                $"range={minimumMicroseconds:F3}..{maximumMicroseconds:F3} | " +
                $"result={lanes[laneIndex].ExpectedResult} | warmupCalls={warmupCalls[laneIndex]}");
        }
    }

    private static int WarmupForMilliseconds(Func<int> action, int milliseconds, int minimumIterations)
    {
        const int maximumIterations = 10_000_000;
        var target = TimeSpan.FromMilliseconds(milliseconds);
        var iterations = 0;
        var sink = 0;
        var stopwatch = Stopwatch.StartNew();
        do
        {
            sink ^= action();
            iterations++;
        }
        while (iterations < maximumIterations &&
               (iterations < minimumIterations || stopwatch.Elapsed < target));

        GC.KeepAlive(sink);
        return iterations;
    }

    private static int ExecuteSelectedCountKernel(Utf8Regex regex, byte[] input)
    {
        if (!Utf8MatchInspection.TryCountSelectedKernelWithoutValidation(regex, input, out var count))
        {
            throw new InvalidOperationException("The selected Count kernel became unavailable during measurement.");
        }

        return count;
    }

    private static void RequireCountResult(string lane, int expected, int actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException($"Count attribution lane '{lane}' returned {actual}; expected {expected}.");
        }
    }

    private readonly record struct CountAttributionLane(
        string Label,
        Func<int> Action,
        int ExpectedResult);

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

            WriteCountDiagnostics("Baseline", baselineDiagnostics);
            WriteCountDiagnostics("Compiled", compiledDiagnostics);

            Measure("FallbackCandidates", iterations, () => Utf8FallbackReplay.CountCandidates(context.Utf8Regex.Inspection.SearchPlan, context.InputBytes, false));
            Measure("FallbackBoundaryCandidates", iterations, () => Utf8FallbackReplay.CountCandidates(context.Utf8Regex.Inspection.SearchPlan, context.InputBytes, true));
            Measure("FallbackVerifiedCount", iterations, () => Utf8FallbackReplay.Count(context.Utf8Regex, context.InputBytes));
        }
        else if (context.Operation == LokadPublicBenchmarkOperation.IsMatch)
        {
            var baselineDiagnostics = context.Utf8Regex.CollectIsMatchDiagnostics(context.InputBytes);
            var compiledDiagnostics = context.CompiledUtf8Regex.CollectIsMatchDiagnostics(context.InputBytes);

            WriteIsMatchDiagnostics("Baseline", baselineDiagnostics);
            WriteIsMatchDiagnostics("Compiled", compiledDiagnostics);
        }
        else if (context.Operation == LokadPublicBenchmarkOperation.Split)
        {
            Console.WriteLine($"BaselineCanUseNativeSplit : {context.Utf8Regex.CanUseNativeSplit()}");
            Console.WriteLine($"CompiledCanUseNativeSplit : {context.CompiledUtf8Regex.CanUseNativeSplit()}");
            Console.WriteLine($"BaselineCanUseSmallAsciiLiteralFamilySplit : {context.Utf8Regex.TryGetSmallAsciiLiteralFamilySearch(out _)}");
            Console.WriteLine($"CompiledCanUseSmallAsciiLiteralFamilySplit : {context.CompiledUtf8Regex.TryGetSmallAsciiLiteralFamilySearch(out _)}");
        }

        Measure("Utf8Regex", iterations, context.ExecuteUtf8Regex);
        Measure("Utf8Compiled", iterations, context.ExecuteUtf8Compiled);
        Measure("DecodeThenRegex", iterations, context.ExecuteDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, context.ExecuteDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, context.ExecutePredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, context.ExecutePredecodedCompiledRegex);
        return 0;
    }

    public static int RunMeasureUnicodeLiteralCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var regex = benchmarkCase.Utf8Regex;
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, options: benchmarkCase.Options | RegexOptions.Compiled);
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

        var hasPreparedSearch = isExactLiteral && regex.Inspection.DebugTryCountExactUtf8LiteralPreparedSearch(benchmarkCase.InputBytes, out _);
        var hasAnchorByte = isExactLiteral && regex.Inspection.DebugTryCountExactUtf8LiteralAnchored(benchmarkCase.InputBytes, out _);

        Console.WriteLine($"HasPreparedSearch : {hasPreparedSearch}");
        Console.WriteLine($"HasAnchorByte     : {hasAnchorByte}");
        if (regex.Inspection.SearchPlan.AlternateLiteralsUtf8 is { Length: > 0 } alternateLiterals)
        {
            Console.WriteLine($"SmallAsciiFamilyPrimitive: {PreparedSmallAsciiLiteralFamilySearch.TryCreate(alternateLiterals, out _)}");
        }

        var strictUtf8 = new UTF8Encoding(false, true);
        Measure("StrictDecodeOnly", iterations, () => strictUtf8.GetString(benchmarkCase.InputBytes).Length);
        Measure("DefaultDecodeOnly", iterations, () => Encoding.UTF8.GetString(benchmarkCase.InputBytes).Length);

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

        Measure("MatchProbe", iterations, () =>
            Utf8MatchInspection.IsMatchCompiled(regex, benchmarkCase.InputBytes) ? 1 : 0);
        Measure("CompiledMatchProbe", iterations, () =>
            Utf8MatchInspection.IsMatchCompiled(compiledUtf8Regex, benchmarkCase.InputBytes) ? 1 : 0);
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
                Utf8BackendInstructionExecutor.IsMatchLiteralFamily(
                    regex.Inspection.SearchPlan,
                    regex.Inspection.SearchPlan.SearchOperation,
                    benchmarkCase.InputBytes,
                    Utf8ExecutionDeadline.Infinite,
                    rightToLeft) ? 1 : 0);
            Measure("CompiledPreparedFamilyIsMatch", iterations, () =>
                Utf8BackendInstructionExecutor.IsMatchLiteralFamily(
                    compiledUtf8Regex.Inspection.SearchPlan,
                    compiledUtf8Regex.Inspection.SearchPlan.SearchOperation,
                    benchmarkCase.InputBytes,
                    Utf8ExecutionDeadline.Infinite,
                    rightToLeft) ? 1 : 0);
        }


        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("DecodeThenCompiledRegex", iterations, benchmarkCase.CountDecodeThenCompiledRegex);
        Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
        Measure("PredecodedCompiledRegex", iterations, benchmarkCase.CountPredecodedCompiledRegex);

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
        var earliest = PreparedMultiLiteralCandidatePrefilter.CreateEarliest(literals);
        var leadingUtf8Segment = PreparedMultiLiteralCandidatePrefilter.CreateLeadingUtf8Segment(literals);
        var packedNibbleSimd = literals.Length <= 8
            ? PreparedMultiLiteralCandidatePrefilter.CreatePackedNibbleSimd(literals)
            : default;
        var hasPacked = PreparedMultiLiteralPackedSearch.TryCreate(literals, out var packed);
        var trie = PreparedMultiLiteralTrieSearch.Create(literals);
        var automaton = PreparedMultiLiteralAutomatonSearch.Create(literals);
        var diagnostics = regex.CollectCountDiagnostics(benchmarkCase.InputBytes);
        var leadingUtf8SegmentHybrid512 = leadingUtf8Segment.HasValue ? DiagnoseHybridChunkedLiteralSetCount(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 512) : default;
        var leadingUtf8SegmentHybrid2048 = leadingUtf8Segment.HasValue ? DiagnoseHybridChunkedLiteralSetCount(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 2048) : default;
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
        if (packedNibbleSimd.HasValue)
        {
            WriteHybridMetrics("PackedNibbleSimd512", packedNibbleSimdHybrid512);
            WriteHybridMetrics("PackedNibbleSimd2048", packedNibbleSimdHybrid2048);
        }

        Measure("ExactDirectCandidates", iterations, () => ExecuteExactLiteralSetCount(direct, benchmarkCase.InputBytes));
        Measure("ExactDirectBoundaryCount", iterations, () => ExecuteExactLiteralSetCountWithBoundaries(direct, searchPlan, benchmarkCase.InputBytes));
        if (leadingUtf8Segment.HasValue)
        {
            Measure("LeadingUtf8SegmentCandidates", iterations, () => ExecuteCandidatePrefilterCount(leadingUtf8Segment, benchmarkCase.InputBytes));
            Measure("LeadingUtf8SegmentProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 512));
            Measure("LeadingUtf8SegmentProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 2048));
            Measure("LeadingUtf8SegmentHybrid512", iterations, () => ExecuteHybridChunkedLiteralSetCount(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 512));
            Measure("LeadingUtf8SegmentHybrid2048", iterations, () => ExecuteHybridChunkedLiteralSetCount(leadingUtf8Segment, automaton, benchmarkCase.InputBytes, 2048));
            Measure("LeadingUtf8SegmentBoundaryCount", iterations, () => ExecuteCandidatePrefilterCountWithBoundaries(leadingUtf8Segment, searchPlan, benchmarkCase.InputBytes));
        }
        if (packedNibbleSimd.HasValue)
        {
            Measure("PackedNibbleSimdCandidates", iterations, () => ExecuteCandidatePrefilterCount(packedNibbleSimd, benchmarkCase.InputBytes));
            Measure("PackedNibbleSimdVerifiedCount", iterations, () => ExecuteCandidatePrefilterCountWithBoundaries(packedNibbleSimd, searchPlan, benchmarkCase.InputBytes));
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
        Measure("DirectEnumeratorMoveNext", iterations, () => ExecuteDirectExactUtf8LiteralFamilyEnumeratorMoveNext(regex.Inspection.PreparedRegex, benchmarkCase.InputBytes));
        Measure("DirectEnumeratorIndexSum", iterations, () => ExecuteDirectExactUtf8LiteralFamilyEnumeratorIndexSum(regex.Inspection.PreparedRegex, benchmarkCase.InputBytes));
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
        var earliest = PreparedMultiLiteralCandidatePrefilter.CreateEarliest(literals);
        var automaton = PreparedMultiLiteralAutomatonSearch.Create(literals);
        var span = benchmarkCase.InputBytes.AsSpan();
        var hybrid128 = DiagnoseHybridChunkedLiteralSetCount(direct, automaton, span, 128);
        var hybrid512 = DiagnoseHybridChunkedLiteralSetCount(direct, automaton, span, 512);
        var hybrid2048 = DiagnoseHybridChunkedLiteralSetCount(direct, automaton, span, 2048);
        var hybrid8192 = DiagnoseHybridChunkedLiteralSetCount(direct, automaton, span, 8192);
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
        WriteHybridMetrics("EarliestHybrid512", earliestHybrid512);
        WriteHybridMetrics("EarliestHybrid2048", earliestHybrid2048);

        Measure("ExactDirect", iterations, () => ExecuteExactLiteralSetCount(direct, benchmarkCase.InputBytes));
        Measure("EarliestExact", iterations, () => ExecuteCandidatePrefilterCount(earliest, benchmarkCase.InputBytes));
        Measure("Automaton", iterations, () => ExecuteAutomatonLiteralSetCount(automaton, benchmarkCase.InputBytes));
        Measure("HybridProbe128", iterations, () => ExecuteHybridChunkedLiteralSetProbe(direct, automaton, benchmarkCase.InputBytes, 128));
        Measure("HybridProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(direct, automaton, benchmarkCase.InputBytes, 512));
        Measure("HybridProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(direct, automaton, benchmarkCase.InputBytes, 2048));
        Measure("HybridProbe8192", iterations, () => ExecuteHybridChunkedLiteralSetProbe(direct, automaton, benchmarkCase.InputBytes, 8192));
        Measure("EarliestProbe512", iterations, () => ExecuteHybridChunkedLiteralSetProbe(earliest, automaton, benchmarkCase.InputBytes, 512));
        Measure("EarliestProbe2048", iterations, () => ExecuteHybridChunkedLiteralSetProbe(earliest, automaton, benchmarkCase.InputBytes, 2048));
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

    public static int RunMeasureBoundedRepeatReplicaCase(string caseId, string? iterationsText)
    {
        var benchmarkCase = ReplicaCountBenchmarkCase.Resolve(caseId);
        var iterations = ParseIterations(iterationsText);
        var compiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, options: benchmarkCase.Options | RegexOptions.Compiled);
        var baselineDiagnostics = benchmarkCase.Utf8Regex.CollectCountDiagnostics(benchmarkCase.InputBytes);
        var compiledDiagnostics = compiledUtf8Regex.CollectCountDiagnostics(benchmarkCase.InputBytes);

        WriteReplicaCaseHeader(benchmarkCase, iterations);
        Console.WriteLine($"BaselineEngine    : {benchmarkCase.Utf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"CompiledEngine    : {compiledUtf8Regex.Inspection.CompiledEngineKind}");
        Console.WriteLine($"ExecutionKind     : {benchmarkCase.Utf8Regex.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledExecKind  : {compiledUtf8Regex.Inspection.ExecutionKind}");
        WriteCountDiagnostics("Baseline", baselineDiagnostics);
        WriteCountDiagnostics("Compiled", compiledDiagnostics);



        Measure("Utf8Regex", iterations, () => benchmarkCase.Utf8Regex.Count(benchmarkCase.InputBytes));
        Measure("Utf8Compiled", iterations, () => compiledUtf8Regex.Count(benchmarkCase.InputBytes));
        Measure("DecodeThenRegex", iterations, benchmarkCase.CountDecodeThenRegex);
        Measure("PredecodedRegex", iterations, benchmarkCase.CountPredecodedRegex);
        Measure("CompiledRegex", iterations, benchmarkCase.CountPredecodedCompiledRegex);
        return 0;
    }

    public static int RunDumpRuntimeTreePattern(string pattern, string? optionsText)
    {
        var options = ParseRegexOptions(optionsText);
        using var owner = Utf8ConstructionScope.Enter();
        var analysis = Utf8FrontEnd.Analyze(pattern, options);
        var prepared = Utf8RegexPreparer.Prepare(analysis);
        Console.WriteLine($"Pattern           : {pattern}");
        Console.WriteLine($"Options           : {options}");
        Console.WriteLine($"ExecutionKind     : {prepared.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {Utf8CompiledEngineSelector.Select(prepared)}");
        Console.WriteLine($"FallbackReason    : {prepared.FallbackReason ?? "<native>"}");
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
                $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net{{Environment.Version.Major}}.0</TargetFramework>
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
                UseShellExecute = false
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

    public static int RunDumpStructuralSearchRebarCase(string caseId)
    {
        var benchmarkCase = DotNetPerformanceReplicaBenchmarkCatalog.Get(caseId);
        var context = new DotNetPerformanceReplicaBenchmarkContext(benchmarkCase);
        WriteStructuralSearchDump(caseId, benchmarkCase.Origin, context.Pattern, benchmarkCase.Options, context.Utf8Regex.Inspection.StructuralSearchPlan);
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
        Console.WriteLine($"AnchoredLiteral   : {regex.Inspection.DebugAnchoredLiteralKind}");
        Console.WriteLine($"OwnedScalarPlan   : {regex.Inspection.AvailableOwnedSliceProgram is not null}");
        Console.WriteLine($"ScalarPrepared    : {regex.Inspection.PreparedRegex.ScalarProgram is not null}");
        Console.WriteLine($"SearchKind        : {regex.Inspection.SearchPlan.Kind}");
        Console.WriteLine($"PortfolioKind     : {regex.Inspection.SearchPortfolioKind}");
        Console.WriteLine($"PreparedSearcher  : {regex.Inspection.SearchPlan.PreparedSearcher.Kind}");
        Console.WriteLine($"RequiredPrefilter : {(regex.Inspection.SearchPlan.RequiredPrefilterSearcher.HasValue ? regex.Inspection.SearchPlan.RequiredPrefilterSearcher.Kind : "None")}");
        WriteStructuralSearchPlan(regex.Inspection.StructuralSearchPlan);
        Console.WriteLine($"FallbackReason    : {regex.Inspection.FallbackReason ?? "<native>"}");
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
        WriteStructuralSearchPlan(plan);
    }

    private static void WriteStructuralSearchPlan(Utf8StructuralSearchPlan plan)
    {
        ref readonly var data = ref plan.Payload;
        Console.WriteLine($"StructuralSource  : {DescribeStructuralSource(plan)}");
        if (data.StartTransform.HasValue)
        {
            Console.WriteLine($"StartTransform    : {data.StartTransform.Kind}, offset {data.StartTransform.Offset}");
        }
        if (data.LeadingBoundary != Utf8BoundaryRequirement.None || data.TrailingBoundary != Utf8BoundaryRequirement.None)
        {
            Console.WriteLine($"Boundaries        : {data.LeadingBoundary}, {data.TrailingBoundary}");
        }
        if (data.TrailingLiteral is { Length: > 0 } literal)
        {
            Console.WriteLine($"TrailingLiteral   : {Encoding.UTF8.GetString(literal)}");
        }
        if (data.ExactLength > 0)
        {
            Console.WriteLine($"ExactLength       : {data.ExactLength}");
        }
        if (data.MaxLength > 0)
        {
            Console.WriteLine($"MaximumLength     : {data.MaxLength}");
        }
    }

    private static string DescribeStructuralSource(Utf8StructuralSearchPlan plan)
    {
        ref readonly var data = ref plan.Payload;
        return !plan.HasValue ? "None" : data.Searcher.HasValue
            ? data.Searcher.Kind.ToString()
            : $"{data.FixedDistanceSets!.Length} fixed-distance sets";
    }

    private static string DumpNode(Utf8TreeNode node, int depth = 0)
    {
        var indent = new string(' ', depth * 2);
        var line = $"{indent}{node.Kind} ch={node.Ch} str={node.Str ?? "<null>"} m={node.M} n={node.N} children={node.ChildCount}";
        if (node.ChildCount == 0)
        {
            return line;
        }

        return line + Environment.NewLine + string.Join(Environment.NewLine,
            Enumerable.Range(0, node.ChildCount).Select(index => DumpNode(node.Child(index), depth + 1)));
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

    private static int ParseLokadPrefixIterations(string? text)
    {
        return Math.Max(20000, ParseIterations(text));
    }

    private static int GetShortPublicIterationFloor(LokadPublicBenchmarkContext context)
    {
        if (context.CaseId is
            "common/date-miss" or
            "common/ip-match" or
            "common/ip-miss" or
            "common/uri-match" or
            "industry/boostdocs-float-match")
        {
            // These sub-0.1-us Boolean routes need enough calls for Tiered PGO
            // to optimize their specialized dispatch. The generic family floor
            // records a transient Tier-0/Tier-1 blend instead of warm throughput.
            return 100000;
        }

        if (context.CaseId == "common/matches-word")
        {
            // The exact-literal Count route needs this bounded floor to reach
            // its stable Tiered-PGO throughput state.
            return 10000;
        }

        if (context.Operation is LokadPublicBenchmarkOperation.IsMatch or LokadPublicBenchmarkOperation.Match)
        {
            return context.CaseId.StartsWith("common/", StringComparison.Ordinal)
                ? 10000
                : context.CaseId.StartsWith("industry/boostdocs-", StringComparison.Ordinal)
                    ? 5000
                    : 2000;
        }

        if (context.Operation is LokadPublicBenchmarkOperation.Replace or LokadPublicBenchmarkOperation.Split)
        {
            return context.CaseId.StartsWith("common/", StringComparison.Ordinal) ? 2000 : 1000;
        }

        return 500;
    }

    private static int GetReadmePublicIterationTarget(LokadPublicBenchmarkContext context, int requestedIterations)
        => Math.Max(GetShortPublicIterationFloor(context), requestedIterations);

    private static int ParseReadmeLokadScriptIterations(LokadReplicaScriptBenchmarkCase benchmarkCase, string? text)
        => GetReadmeLokadScriptIterationTarget(benchmarkCase, ParseIterations(text));

    private static int GetReadmeLokadScriptIterationTarget(
        LokadReplicaScriptBenchmarkCase benchmarkCase,
        int requestedIterations)
    {
        return benchmarkCase.Model == LokadReplicaScriptBenchmarkModel.PrefixMatchLoop
            ? Math.Max(20000, requestedIterations)
            : requestedIterations;
    }

    private static int GetReadmeReplicaIterationTarget(
        ReplicaCountBenchmarkCase benchmarkCase,
        int requestedIterations)
    {
        var inputBytes = benchmarkCase.InputBytes.Length;

        if (inputBytes <= 128 * 1024)
        {
            if (benchmarkCase.Group is "literal" or "literal-family" or "structural")
            {
                return Math.Max(20000, requestedIterations);
            }

            return Math.Max(10000, requestedIterations);
        }

        if (inputBytes <= 512 * 1024)
        {
            return Math.Max(5000, requestedIterations);
        }

        return requestedIterations;
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
        int requestedIterations,
        int iterationTarget,
        int samples,
        Func<int> utf8,
        Func<int> utf8Compiled,
        Func<int> predecoded,
        Func<int> compiledRegex,
        Func<int> decodeThenRegex,
        Func<int> decodeThenCompiledRegex,
        bool reverseMeasurementOrder = false)
    {
        Func<int>[] actions =
        [
            utf8,
            utf8Compiled,
            predecoded,
            compiledRegex,
            decodeThenRegex,
            decodeThenCompiledRegex,
        ];
        var comparison = MeasureComparisonLanes(requestedIterations, iterationTarget, samples, actions,
            ["Utf8Regex", "Utf8Compiled", "PredecodedRegex", "CompiledRegex", "DecodeThenRegex", "DecodeThenCompiledRegex"],
            reverseMeasurementOrder);
        return new ReadmeCaseMeasurement(
            comparison.Medians[0],
            comparison.Medians[1],
            comparison.Medians[2],
            comparison.Medians[3],
            comparison.Medians[4],
            comparison.Medians[5],
            comparison.AllocatedBytes[0],
            comparison.AllocatedBytes[1],
            comparison.AllocatedBytes[2],
            comparison.AllocatedBytes[3],
            comparison.AllocatedBytes[4],
            comparison.AllocatedBytes[5],
            requestedIterations,
            comparison.Iterations.Min(),
            samples,
            comparison.Iterations,
            comparison.RawSamples,
            comparison.Phases);
    }
    private static (double[] Medians, double[] AllocatedBytes, int[] Iterations, double[][] RawSamples,
        Dictionary<string, double> Phases) MeasureComparisonLanes(
        int requestedIterations, int iterationTarget, int samples, Func<int>[] actions,
        string[] laneNames, bool reverseMeasurementOrder = false)
    {
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        var expected = actions[0]();
        for (var index = 1; index < actions.Length; index++)
        {
            if (actions[index]() != expected)
                throw new InvalidOperationException($"Comparison lane {index} disagrees with the UTF-8 result.");
        }
        var phases = new Dictionary<string, double>();
        var phase = Stopwatch.StartNew();
        var warmupSink = 0;
        foreach (var action in actions)
            warmupSink ^= WarmupCore(action).Sink;
        Volatile.Write(ref s_allocationSink, warmupSink);
        phases["Warmup"] = phase.Elapsed.TotalMilliseconds;
        phase.Restart();
        var laneIterations = CalibrateReadmeIterations(requestedIterations, iterationTarget, actions);
        phases["Calibration"] = phase.Elapsed.TotalMilliseconds;
        phase.Restart();
        var (measurements, rawSamples) = MeasureReadmeAlternatingMedianMicroseconds(
            samples, laneIterations, actions, laneNames, reverseMeasurementOrder);
        phases["Sampling"] = phase.Elapsed.TotalMilliseconds;
        phase.Restart();
        var allocatedBytes = new double[actions.Length];
        for (var position = 0; position < actions.Length; position++)
        {
            var index = reverseMeasurementOrder ? actions.Length - position - 1 : position;
            allocatedBytes[index] = MeasureAllocatedBytesPerOperation(laneIterations[index], actions[index]);
        }
        phases["Allocation"] = phase.Elapsed.TotalMilliseconds;

        return (measurements, allocatedBytes, laneIterations, rawSamples, phases);
    }

    private static (double[] Medians, double[][] RawSamples) MeasureReadmeAlternatingMedianMicroseconds(
        int samples,
        int[] laneIterations,
        IReadOnlyList<Func<int>> actions,
        IReadOnlyList<string> laneNames,
        bool reverseFirstSample)
    {
        var sink = 0;
        var elapsedByAction = new TimeSpan[actions.Count][];
        for (var index = 0; index < elapsedByAction.Length; index++)
        {
            elapsedByAction[index] = new TimeSpan[samples];
        }

        for (var sample = 0; sample < samples; sample++)
        {
            var reverse = reverseFirstSample ^ ((sample & 1) != 0);
            for (var position = 0; position < actions.Count; position++)
            {
                var index = reverse ? actions.Count - position - 1 : position;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var measurement = MeasureTimedCore(laneIterations[index], actions[index]);
                elapsedByAction[index][sample] = measurement.Elapsed;
                sink ^= measurement.Sink;
            }
        }

        // Preserve the paired evidence before sorting; report outside timed loops.
        for (var sample = 0; sample < samples; sample++)
        {
            for (var index = 0; index < elapsedByAction.Length; index++)
            {
                Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"ComparisonSample;Index={sample};Lane={laneNames[index]};Iterations={laneIterations[index]};ElapsedMicroseconds={elapsedByAction[index][sample].TotalMicroseconds:F3}"));
            }
        }

        var rawSamples = elapsedByAction.Select(static lane => lane.Select(static elapsed => elapsed.TotalMicroseconds).ToArray()).ToArray();
        var medians = new double[actions.Count];
        for (var index = 0; index < elapsedByAction.Length; index++)
        {
            Array.Sort(elapsedByAction[index]);
            medians[index] = elapsedByAction[index][samples / 2].TotalMicroseconds / laneIterations[index];
        }

        GC.KeepAlive(sink);
        return (medians, rawSamples);
    }

    private static double MeasureAllocatedBytesPerOperation(int iterations, Func<int> action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var sink = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            sink ^= action();
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Volatile.Write(ref s_allocationSink, sink);
        return (double)allocatedBytes / iterations;
    }

    private static int[] CalibrateReadmeIterations(
        int requestedIterations,
        int iterationTarget,
        IReadOnlyList<Func<int>> actions)
    {
        // Independent counts prevent decode-heavy lanes from reducing matching
        // samples to microseconds. All lanes still perform the same public work
        // per operation; normalize by their actual counts and alternate order.
        // Allocating decode lanes can accelerate after calibration. Leave
        // headroom above the unchanged 20 ms qualification floor.
        const double targetSampleMicroseconds = 75_000;
        var maximumIterations = Math.Max(requestedIterations, 10_000_000);
        var counts = new int[actions.Count];
        for (var lane = 0; lane < actions.Count; lane++)
        {
            var action = actions[lane];
            var cost = MeasureReadmeProbeMicrosecondsPerOperation(action, Math.Max(iterationTarget, 1_000_000));
            var count = (int)Math.Clamp(Math.Ceiling(targetSampleMicroseconds / Math.Max(cost, double.Epsilon)),
                requestedIterations, maximumIterations);
            // One duration confirmation after calibration. Do not keep retrying
            // noisy cases: raw samples below 20 ms remain unqualified.
            // Match sampling's GC preflight. Uncollected decode allocations
            // otherwise inflate this probe and leave its later samples too short.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var elapsed = MeasureTimedCore(count, action).Elapsed.TotalMicroseconds;
            if (elapsed > 0 && (elapsed < targetSampleMicroseconds * .8 || elapsed > targetSampleMicroseconds * 1.5))
                count = (int)Math.Clamp(Math.Ceiling(count * targetSampleMicroseconds / elapsed), requestedIterations, maximumIterations);
            counts[lane] = count;
        }
        return counts;
    }

    private static double MeasureReadmeProbeMicrosecondsPerOperation(Func<int> action, int iterationTarget)
    {
        const double probeTargetMicroseconds = 20_000;
        var sink = action();
        var probeIterations = 1;
        while (true)
        {
            var measurement = MeasureTimedCore(probeIterations, action);
            sink ^= measurement.Sink;
            var elapsedMicroseconds = measurement.Elapsed.TotalMicroseconds;
            if (elapsedMicroseconds >= probeTargetMicroseconds || probeIterations >= iterationTarget)
            {
                GC.KeepAlive(sink);
                return elapsedMicroseconds / probeIterations;
            }

            probeIterations = probeIterations > iterationTarget / 2
                ? iterationTarget
                : probeIterations * 2;
        }
    }

    private static ReadmeCaseMeasurement MeasureReadmeCaseOutOfProcess(string command, string caseId, int iterations, int samples)
    {
        const int caseTimeoutMilliseconds = 120_000;
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };
        psi.ArgumentList.Add(typeof(BenchmarkInspectReporter).Assembly.Location);
        psi.ArgumentList.Add(command);
        psi.ArgumentList.Add(caseId);
        psi.ArgumentList.Add(iterations.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(samples.ToString(CultureInfo.InvariantCulture));

        Console.WriteLine($"Measuring README benchmark case: {caseId} (requested={iterations}, samples={samples}, cap=120s)");
        Console.Out.Flush();
        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Unable to start child process for {command} {caseId}.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(caseTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            var partialError = errorTask.GetAwaiter().GetResult().Trim();
            throw new TimeoutException(
                $"README benchmark case '{caseId}' exceeded its 120-second child budget. " +
                $"Prior cases remain checkpointed. Inspect the case with a smaller bounded command before resuming." +
                (partialError.Length == 0 ? string.Empty : $" Child error: {partialError}"));
        }

        var output = outputTask.GetAwaiter().GetResult().Trim();
        var error = errorTask.GetAwaiter().GetResult().Trim();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Child process failed for {command} {caseId}: " +
                (error.Length == 0 ? output : error));
        }

        if (error.Length != 0)
            Console.Error.WriteLine(error);
        return ParseReadmeCaseRow(output);
    }

    private static string FormatReadmeCaseRow(ReadmeCaseMeasurement row)
        => JsonSerializer.Serialize(row);

    private static ReadmeCaseMeasurement ParseReadmeCaseRow(string text)
    {
        // Policy/environment labels precede the single-line machine result.
        // Consume its final line, while retaining support for plain JSON output.
        var output = text.AsSpan().TrimEnd();
        var row = JsonSerializer.Deserialize<ReadmeCaseMeasurement>(output[(output.LastIndexOf('\n') + 1)..]);
        if (row.EffectiveIterations <= 0 || row.Samples <= 0)
            throw new InvalidOperationException("README child did not emit a measurement row.");
        return row;
    }

    private static readonly JsonSerializerOptions ReadmeBenchmarkSnapshotJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private sealed class ReadmeBenchmarkSnapshot
    {
        public int SchemaVersion { get; set; } = 6;

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

        public string? MeasurementProtocol { get; set; }

        public int RequestedIterations { get; set; }

        public int EffectiveIterations { get; set; }

        public int Samples { get; set; }

        public int[]? LaneIterations { get; set; }

        public double[][]? SampleMicroseconds { get; set; }

        public Dictionary<string, double>? PhaseMilliseconds { get; set; }

        public double Utf8Regex { get; set; }

        public double Utf8Compiled { get; set; }

        public double PredecodedRegex { get; set; }

        public double CompiledRegex { get; set; }

        public double DecodeThenRegex { get; set; }

        public double DecodeThenCompiledRegex { get; set; }

        public double Utf8RegexAllocatedBytes { get; set; }

        public double Utf8CompiledAllocatedBytes { get; set; }

        public double PredecodedRegexAllocatedBytes { get; set; }

        public double CompiledRegexAllocatedBytes { get; set; }

        public double DecodeThenRegexAllocatedBytes { get; set; }

        public double DecodeThenCompiledRegexAllocatedBytes { get; set; }

        public static ReadmeCaseMeasurementJson FromMeasurement(ReadmeCaseMeasurement measurement)
            => new()
            {
                MeasuredAtUtc = DateTimeOffset.UtcNow,
                Environment = CaptureBenchmarkEnvironment(),
                MeasurementProtocol = ReadmeMeasurementProtocol,
                RequestedIterations = measurement.RequestedIterations,
                EffectiveIterations = measurement.EffectiveIterations,
                Samples = measurement.Samples,
                LaneIterations = measurement.LaneIterations,
                SampleMicroseconds = measurement.SampleMicroseconds,
                PhaseMilliseconds = measurement.PhaseMilliseconds,
                Utf8Regex = measurement.Utf8Regex,
                Utf8Compiled = measurement.Utf8Compiled,
                PredecodedRegex = measurement.PredecodedRegex,
                CompiledRegex = measurement.CompiledRegex,
                DecodeThenRegex = measurement.DecodeThenRegex,
                DecodeThenCompiledRegex = measurement.DecodeThenCompiledRegex,
                Utf8RegexAllocatedBytes = measurement.Utf8RegexAllocatedBytes,
                Utf8CompiledAllocatedBytes = measurement.Utf8CompiledAllocatedBytes,
                PredecodedRegexAllocatedBytes = measurement.PredecodedRegexAllocatedBytes,
                CompiledRegexAllocatedBytes = measurement.CompiledRegexAllocatedBytes,
                DecodeThenRegexAllocatedBytes = measurement.DecodeThenRegexAllocatedBytes,
                DecodeThenCompiledRegexAllocatedBytes = measurement.DecodeThenCompiledRegexAllocatedBytes
            };

        public ReadmeCaseMeasurement ToMeasurement()
            => new(
                Utf8Regex,
                Utf8Compiled,
                PredecodedRegex,
                CompiledRegex,
                DecodeThenRegex,
                DecodeThenCompiledRegex,
                Utf8RegexAllocatedBytes,
                Utf8CompiledAllocatedBytes,
                PredecodedRegexAllocatedBytes,
                CompiledRegexAllocatedBytes,
                DecodeThenRegexAllocatedBytes,
                DecodeThenCompiledRegexAllocatedBytes,
                RequestedIterations,
                EffectiveIterations,
                Samples,
                LaneIterations,
                SampleMicroseconds,
                PhaseMilliseconds);
    }

    private readonly record struct ReadmeCaseMeasurement(
        double Utf8Regex,
        double Utf8Compiled,
        double PredecodedRegex,
        double CompiledRegex,
        double DecodeThenRegex,
        double DecodeThenCompiledRegex,
        double Utf8RegexAllocatedBytes,
        double Utf8CompiledAllocatedBytes,
        double PredecodedRegexAllocatedBytes,
        double CompiledRegexAllocatedBytes,
        double DecodeThenRegexAllocatedBytes,
        double DecodeThenCompiledRegexAllocatedBytes,
        int RequestedIterations,
        int EffectiveIterations,
        int Samples,
        int[]? LaneIterations = null,
        double[][]? SampleMicroseconds = null,
        Dictionary<string, double>? PhaseMilliseconds = null);
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
        Console.WriteLine($"{label}Result     : {diagnostics.Result}");
        Console.WriteLine($"{label}Route      : {diagnostics.ExecutionRoute}");
        Console.WriteLine($"{label}ExecKind   : {diagnostics.ExecutionKind}");
        Console.WriteLine($"{label}SearchKind : {diagnostics.SearchKind}");
        Console.WriteLine($"{label}Candidates : {diagnostics.SearchCandidates}");
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
        Console.WriteLine($"{label}Candidates : {diagnostics.SearchCandidates}");
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

    // Calibration, warmup and sampling share this delegate call site.
    [MethodImpl(MethodImplOptions.NoInlining)]
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
        const int maximumIterations = 10_000_000;
        var target = TimeSpan.FromMilliseconds(250);
        var sink = 0;
        var iterations = 0;
        var batchIterations = 1;
        var stopwatch = Stopwatch.StartNew();
        do
        {
            // Warm the actual timing loop and its delegate call site too.
            // Start small for slow operations and amortize the timer over
            // batches of at most 1024 calls.
            var batch = Math.Min(batchIterations, maximumIterations - iterations);
            sink ^= MeasureTimedCore(batch, action).Sink;
            iterations += batch;
            batchIterations = Math.Min(batchIterations * 2, 1024);
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
        Console.WriteLine($"CheckoutCommit    : {environment.SourceCommit}");
        Console.WriteLine($"BenchmarkAssembly : {environment.BenchmarkAssembly}");
        Console.WriteLine($"CoreAssembly      : {environment.CoreAssembly}");
        Console.WriteLine($"Pcre2Assembly     : {environment.Pcre2Assembly}");
        Console.WriteLine($"PythonReAssembly  : {environment.PythonReAssembly}");
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
            // SourceCommit remains the checkout identity in historical JSON.
            // Assembly producer versions and hashes identify the code actually loaded.
            SourceCommit = RunGit("rev-parse", "--short=12", "HEAD") ?? "<unknown>",
            BenchmarkAssembly = GetAssemblyIdentity(typeof(BenchmarkInspectReporter).Assembly),
            CoreAssembly = GetAssemblyIdentity(typeof(Utf8Regex).Assembly),
            Pcre2Assembly = GetAssemblyIdentity(typeof(Utf8Pcre2Regex).Assembly),
            PythonReAssembly = GetAssemblyIdentity(typeof(PythonRe.Utf8PythonRegex).Assembly),
            TrackedDirty = !string.IsNullOrWhiteSpace(RunGit(
                "status",
                "--porcelain=v1",
                "--untracked-files=no",
                "--",
                ".",
                ":(exclude)README.md",
                ":(exclude)bench/Snapshots",
                ":(exclude)src/Lokad.Utf8Regex.PythonRe/README.md",
                ":(exclude)src/Lokad.Utf8Regex.Pcre2/README.md",
                ":(exclude)src/Lokad.Utf8Regex.Posix/README.md")),
            HasUntrackedFiles = !string.IsNullOrWhiteSpace(RunGit("ls-files", "--others", "--exclude-standard")),
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? RuntimeInformation.ProcessArchitecture.ToString(),
            TieredPgo = Environment.GetEnvironmentVariable("DOTNET_TieredPGO") ??
                Environment.GetEnvironmentVariable("COMPlus_TieredPGO") ??
                "runtime-default"
        };

    internal static string GetAssemblyIdentity(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "<unknown>";
        using var stream = File.OpenRead(assembly.Location);
        return $"{version} sha256={Convert.ToHexString(SHA256.HashData(stream))}";
    }

    private sealed class BenchmarkEnvironmentJson
    {
        public string BenchmarkAssembly { get; set; } = string.Empty;
        public string CoreAssembly { get; set; } = string.Empty;
        public string Pcre2Assembly { get; set; } = string.Empty;
        public string PythonReAssembly { get; set; } = string.Empty;

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
            UseShellExecute = false
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

    private static void EnsureEquivalentReplaceResults(Utf8RegexBenchmarkContext context)
    {
        var expected = context.Utf8Regex.Replace(context.InputBytes, context.ReplacementUtf8);
        var compiled = context.CompiledUtf8Regex.Replace(context.InputBytes, context.ReplacementUtf8);
        var decoded = Encoding.UTF8.GetString(context.InputBytes);
        var decodeThenRegex = RegexBenchmarkResult.ReplaceUtf8(context.Regex, decoded, context.Replacement);
        var decodeThenCompiled = RegexBenchmarkResult.ReplaceUtf8(context.CompiledRegex, decoded, context.Replacement);
        var predecodedRegex = RegexBenchmarkResult.ReplaceUtf8(context.Regex, context.InputString, context.Replacement);
        var predecodedCompiled = RegexBenchmarkResult.ReplaceUtf8(context.CompiledRegex, context.InputString, context.Replacement);

        if (!expected.AsSpan().SequenceEqual(compiled) ||
            !expected.AsSpan().SequenceEqual(decodeThenRegex) ||
            !expected.AsSpan().SequenceEqual(decodeThenCompiled) ||
            !expected.AsSpan().SequenceEqual(predecodedRegex) ||
            !expected.AsSpan().SequenceEqual(predecodedCompiled))
        {
            throw new InvalidOperationException("Replace benchmark routes do not produce equivalent UTF-8 output.");
        }
    }

    private static int ExecuteUtf8(Utf8RegexBenchmarkContext context)
    {
        return context.BenchmarkCase.Operation switch
        {
            Utf8RegexBenchmarkOperation.IsMatch => context.Utf8Regex.IsMatch(context.InputBytes) ? 1 : 0,
            Utf8RegexBenchmarkOperation.Count => context.Utf8Regex.Count(context.InputBytes),
            Utf8RegexBenchmarkOperation.Match => context.Utf8Regex.Match(context.InputBytes).StartOffsetInBytes,
            Utf8RegexBenchmarkOperation.EnumerateMatches => SumUtf8Matches(context),
            Utf8RegexBenchmarkOperation.EnumerateSplits => SumUtf8Splits(context),
            Utf8RegexBenchmarkOperation.Replace => context.Utf8Regex.Replace(context.InputBytes, context.ReplacementUtf8).Length,
            _ => 0,
        };
    }

    private static bool TryCreateExactCompositeTrailingSearch(
        Utf8Regex regex,
        out PreparedSubstringSearch search,
        out int matchByteLength)
    {
        var searchPlan = regex.Inspection.SearchPlan;
        if (searchPlan.HasBoundaryRequirements ||
            searchPlan.LiteralUtf8 is not { Length: > 0 } leadingLiteral ||
            searchPlan.TrailingLiteralUtf8 is not { Length: > 0 } trailingLiteral ||
            regex.Inspection.ExecutionKind is not
                (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal))
        {
            search = default;
            matchByteLength = 0;
            return false;
        }

        byte[] compositeLiteral = [.. leadingLiteral, .. trailingLiteral];
        search = new PreparedSubstringSearch(compositeLiteral, ignoreCase: false);
        matchByteLength = leadingLiteral.Length;
        return true;
    }

    private static int ExecuteCompositeTrailingMatch(
        byte[] input,
        PreparedSubstringSearch search,
        int matchByteLength)
    {
        var matchByteIndex = search.IndexOf(input);
        if (matchByteIndex < 0)
        {
            return 0;
        }

        return HashCode.Combine(matchByteIndex, matchByteLength);
    }

    private static Utf8CaptureRange ExecuteDirectLiteralFamilyMatch(Utf8Regex regex, ReadOnlySpan<byte> input)
    {
        var plan = regex.Inspection.SearchPlan;
        return Utf8BackendInstructionExecutor.MatchLiteralFamily(
            plan,
            plan.SearchOperation,
            input,
            Utf8ExecutionDeadline.Infinite,
            (regex.Options & RegexOptions.RightToLeft) != 0);
    }

    private static PreparedSearchMatch ExecuteConfirmedLiteralFamilySearch(Utf8Regex regex, ReadOnlySpan<byte> input)
    {
        var plan = regex.Inspection.SearchPlan;
        var program = plan.SearchOperation;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        return Utf8BackendInstructionExecutor.TryFindNextLiteralFamilyMatch(
            in plan,
            in program,
            input,
            ref state,
            Utf8ExecutionDeadline.Infinite,
            out var match)
                ? match
                : default;
    }

    private static int CountRawLiteralFamilyCandidatesThrough(
        Utf8Regex regex,
        ReadOnlySpan<byte> input,
        PreparedSearchMatch terminalMatch)
    {
        var searcher = regex.Inspection.SearchPlan.PreparedSearcher;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var count = 0;
        while (searcher.TryFindNextNonOverlappingMatch(input, ref state, out var match))
        {
            count++;
            if (match.Index == terminalMatch.Index &&
                match.Length == terminalMatch.Length &&
                match.LiteralId == terminalMatch.LiteralId)
            {
                return count;
            }
        }

        return -count;
    }

    private static PreparedSearchMatch ExecuteShortCircuitBoundaryFamilySearch(
        Utf8Regex regex,
        ReadOnlySpan<byte> input)
    {
        var plan = regex.Inspection.SearchPlan;
        if (plan.HasAlternateLiteralPrefixOverlap || plan.HasAlternateLiteralProperStartOverlap)
        {
            return default;
        }

        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (plan.PreparedSearcher.TryFindNextNonOverlappingMatch(input, ref state, out var match))
        {
            if (MatchesBoundaryRequirementsShortCircuit(in plan, input, match.Index, match.Length))
            {
                return match;
            }
        }

        return default;
    }

    private static bool MatchesBoundaryRequirementsShortCircuit(
        in Utf8SearchPlan plan,
        ReadOnlySpan<byte> input,
        int startIndex,
        int literalLength)
    {
        if (!MatchesBoundaryRequirementShortCircuit(plan.LeadingBoundary, input, startIndex))
        {
            return false;
        }

        var endIndex = startIndex + literalLength;
        return (plan.TrailingLiteralUtf8 is null || input[endIndex..].StartsWith(plan.TrailingLiteralUtf8)) &&
            MatchesBoundaryRequirementShortCircuit(plan.TrailingBoundary, input, endIndex);
    }

    private static bool MatchesBoundaryRequirementShortCircuit(
        Utf8BoundaryRequirement requirement,
        ReadOnlySpan<byte> input,
        int byteOffset)
    {
        if (requirement == Utf8BoundaryRequirement.None)
        {
            return true;
        }

        var isBoundary = Utf8ScalarWordBoundary.TryGetAsciiBoundary(input, byteOffset, out var asciiBoundary)
            ? asciiBoundary
            : Utf8ScalarWordBoundary.IsBoundary(input, byteOffset);
        return requirement == Utf8BoundaryRequirement.Boundary
            ? isBoundary
            : requirement == Utf8BoundaryRequirement.NonBoundary && !isBoundary;
    }

    private static byte[] CreateLiteralBoundaryWordKinds(byte[][]? literals)
    {
        if (literals is not { Length: > 0 })
        {
            return [];
        }

        var kinds = new byte[literals.Length];
        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            var hasFirst = Utf8ScalarNeighbors.TryGetNext(literal, 0, out var first);
            var hasLast = Utf8ScalarNeighbors.TryGetPrevious(literal, literal.Length, out var last);
            if (hasFirst && IsBoundaryWordRune(first))
            {
                kinds[i] |= 1;
            }

            if (hasLast && IsBoundaryWordRune(last))
            {
                kinds[i] |= 2;
            }
        }

        return kinds;
    }

    private static PreparedSearchMatch ExecuteLiteralEdgeBoundaryFamilySearch(
        Utf8Regex regex,
        ReadOnlySpan<byte> input,
        byte[] literalBoundaryWordKinds)
    {
        var plan = regex.Inspection.SearchPlan;
        if (plan.HasAlternateLiteralPrefixOverlap || plan.HasAlternateLiteralProperStartOverlap)
        {
            return default;
        }

        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (plan.PreparedSearcher.TryFindNextNonOverlappingMatch(input, ref state, out var match))
        {
            if ((uint)match.LiteralId < (uint)literalBoundaryWordKinds.Length &&
                MatchesBoundaryRequirementsWithLiteralEdges(
                    in plan,
                    input,
                    match.Index,
                    match.Length,
                    literalBoundaryWordKinds[match.LiteralId]))
            {
                return match;
            }
        }

        return default;
    }

    private static bool MatchesBoundaryRequirementsWithLiteralEdges(
        in Utf8SearchPlan plan,
        ReadOnlySpan<byte> input,
        int startIndex,
        int literalLength,
        byte literalBoundaryWordKind)
    {
        var firstIsWord = (literalBoundaryWordKind & 1) != 0;
        if (!MatchesKnownNextBoundaryRequirement(plan.LeadingBoundary, input, startIndex, firstIsWord))
        {
            return false;
        }

        var endIndex = startIndex + literalLength;
        if (plan.TrailingLiteralUtf8 is not null && !input[endIndex..].StartsWith(plan.TrailingLiteralUtf8))
        {
            return false;
        }

        var lastIsWord = (literalBoundaryWordKind & 2) != 0;
        return MatchesKnownPreviousBoundaryRequirement(plan.TrailingBoundary, input, endIndex, lastIsWord);
    }

    private static bool MatchesKnownNextBoundaryRequirement(
        Utf8BoundaryRequirement requirement,
        ReadOnlySpan<byte> input,
        int byteOffset,
        bool nextIsWord)
    {
        if (requirement == Utf8BoundaryRequirement.None)
        {
            return true;
        }

        var previousIsWord = IsPreviousBoundaryWord(input, byteOffset);
        return MatchesBoundaryRequirement(requirement, previousIsWord != nextIsWord);
    }

    private static bool MatchesKnownPreviousBoundaryRequirement(
        Utf8BoundaryRequirement requirement,
        ReadOnlySpan<byte> input,
        int byteOffset,
        bool previousIsWord)
    {
        if (requirement == Utf8BoundaryRequirement.None)
        {
            return true;
        }

        var nextIsWord = IsNextBoundaryWord(input, byteOffset);
        return MatchesBoundaryRequirement(requirement, previousIsWord != nextIsWord);
    }

    private static bool IsPreviousBoundaryWord(ReadOnlySpan<byte> input, int byteOffset)
    {
        if (byteOffset == 0)
        {
            return false;
        }

        var previousByte = input[byteOffset - 1];
        return previousByte < 0x80
            ? Utf8AsciiBytePredicates.IsWord(previousByte)
            : Utf8ScalarNeighbors.TryGetPrevious(input, byteOffset, out var previous) &&
              IsBoundaryWordRune(previous);
    }

    private static bool IsNextBoundaryWord(ReadOnlySpan<byte> input, int byteOffset)
    {
        if (byteOffset == input.Length)
        {
            return false;
        }

        var nextByte = input[byteOffset];
        return nextByte < 0x80
            ? Utf8AsciiBytePredicates.IsWord(nextByte)
            : Utf8ScalarNeighbors.TryGetNext(input, byteOffset, out var next) &&
              IsBoundaryWordRune(next);
    }

    private static bool MatchesBoundaryRequirement(Utf8BoundaryRequirement requirement, bool isBoundary)
    {
        return requirement == Utf8BoundaryRequirement.Boundary
            ? isBoundary
            : requirement == Utf8BoundaryRequirement.NonBoundary && !isBoundary;
    }

    private static bool IsBoundaryWordRune(Rune value)
    {
        return Utf8SliceScalarClasses.IsWord(value, includeJoiners: true, ecma: false);
    }

    private static int EncodePreparedSearchMatch(PreparedSearchMatch match)
    {
        return HashCode.Combine(match.Index, match.Length, match.LiteralId);
    }

    private static bool MatchesExactly(Utf8CaptureRange left, Utf8CaptureRange right)
    {
        return left.Success == right.Success &&
            left.Success == right.Success &&
            left.StartOffsetInBytes == right.StartOffsetInBytes &&
            left.EndOffsetInBytes == right.EndOffsetInBytes;
    }

    private static int EncodeValueMatch(Utf8CaptureRange match)
    {
        return match.Success
            ? HashCode.Combine(
                match.Success, match.StartOffsetInBytes, match.EndOffsetInBytes)
            : 0;
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
            Utf8RegexBenchmarkOperation.Match => context.CompiledUtf8Regex.Match(context.InputBytes).StartOffsetInBytes,
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
            Utf8RegexBenchmarkOperation.Replace => RegexBenchmarkResult.ReplaceUtf8Length(context.Regex, decoded, context.Replacement),
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
            Utf8RegexBenchmarkOperation.Replace => RegexBenchmarkResult.ReplaceUtf8Length(context.Regex, context.InputString, context.Replacement),
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
            Utf8RegexBenchmarkOperation.Replace => RegexBenchmarkResult.ReplaceUtf8Length(context.CompiledRegex, decoded, context.Replacement),
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
            Utf8RegexBenchmarkOperation.Replace => RegexBenchmarkResult.ReplaceUtf8Length(context.CompiledRegex, context.InputString, context.Replacement),
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
            var relative = search.FirstByteSearch.IndexOf(input[start..]);
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

    private static int ExecutePreparedSubstringCount(PreparedSubstringSearch search, byte[] input)
        => search.Count(input);

    private static int SumUtf8Matches(Utf8RegexBenchmarkContext context)
    {
        return SumUtf8Matches(context.Utf8Regex, context.InputBytes);
    }

    private static int SumUtf8Matches(Utf8Regex regex, byte[] input)
    {
        var sum = 0;
        foreach (var match in regex.EnumerateMatches(input))
        {
            sum += match.StartOffsetInBytes;
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
        return enumerator.MoveNext() ? enumerator.Current.StartOffsetInBytes : -1;
    }

    private static int FirstCopiedUtf8EnumeratorMatch(Utf8Regex regex, byte[] input)
    {
        var source = regex.EnumerateMatches(input);
        var enumerator = source.GetEnumerator();
        return enumerator.MoveNext() ? enumerator.Current.StartOffsetInBytes : -1;
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
            sum += split.LengthInBytes;
        }

        return sum;
    }

    private static int SumUtf8SplitsManual(Utf8Regex regex, byte[] input)
    {
        var sum = 0;
        var enumerator = regex.EnumerateSplits(input);
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.LengthInBytes;
        }

        return sum;
    }

    private static int ExecuteTryReplace(
        Utf8Regex regex,
        byte[] input,
        byte[] replacement,
        byte[] destination)
    {
        var status = regex.TryReplace(input, replacement, destination, out var bytesWritten);
        if (status != OperationStatus.Done)
        {
            throw new InvalidOperationException($"Destination UTF-8 replacement returned {status}.");
        }

        return bytesWritten;
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
            sum += Encoding.UTF8.GetByteCount(input.AsSpan(split));
        }

        return sum;
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
            sum += enumerator.Current.StartOffsetInBytes;
        }

        return sum;
    }

    private static int ExecuteReplicaUtf8EnumerateMatchIndexSum(ReplicaCountBenchmarkCase benchmarkCase)
    {
        var sum = 0;
        var enumerator = benchmarkCase.Utf8Regex.EnumerateMatches(benchmarkCase.InputBytes);
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.StartOffsetInBytes;
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

    private static int ExecuteDirectExactUtf8LiteralEnumeratorMoveNext(Utf8PreparedRegex regexPlan, byte[] literal, byte[] input)
    {
        var count = 0;
        var enumerator = new Utf8ValueMatchEnumerator(
            new Utf8OperationMatchCursor(
                input,
                regexPlan,
                literal,
                budget: Utf8ExecutionDeadline.Infinite));
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int ExecuteDirectExactUtf8LiteralEnumeratorIndexSum(Utf8PreparedRegex regexPlan, byte[] literal, byte[] input)
    {
        var sum = 0;
        var enumerator = new Utf8ValueMatchEnumerator(
            new Utf8OperationMatchCursor(
                input,
                regexPlan,
                literal,
                budget: Utf8ExecutionDeadline.Infinite));
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.StartOffsetInBytes;
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

    private static int ExecuteDirectExactUtf8LiteralFamilyEnumeratorMoveNext(Utf8PreparedRegex regexPlan, byte[] input)
    {
        var count = 0;
        var enumerator = new Utf8ValueMatchEnumerator(
            new Utf8OperationMatchCursor(input, regexPlan, budget: Utf8ExecutionDeadline.Infinite));
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int ExecuteDirectExactUtf8LiteralFamilyEnumeratorIndexSum(Utf8PreparedRegex regexPlan, byte[] input)
    {
        var sum = 0;
        var enumerator = new Utf8ValueMatchEnumerator(
            new Utf8OperationMatchCursor(input, regexPlan, budget: Utf8ExecutionDeadline.Infinite));
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.StartOffsetInBytes;
        }

        return sum;
    }

    private static int ExecuteCorrelatedCandidateCount(
        PreparedMultiLiteralPackedNibbleSimdPrefilter prefilter,
        byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (prefilter.TryFindNextCandidate(input, ref state, out _))
        {
            count++;
        }

        return count;
    }

    private static int ExecuteIgnoreCaseCorrelatedCountWithBoundaries(
        PreparedMultiLiteralPackedNibbleSimdPrefilter prefilter,
        PreparedAsciiIgnoreCaseLiteralSetSearch search,
        Utf8SearchPlan plan,
        byte[] input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (prefilter.TryFindNextCandidate(span, ref state, out var candidate))
        {
            if (!search.TryGetMatchedLiteralLength(span, candidate, out var matchedLength))
            {
                continue;
            }

            if (Utf8SearchExecutor.MatchesBoundaryRequirements(plan, span, candidate, matchedLength))
            {
                count++;
            }

            state = new PreparedMultiLiteralScanState(candidate + matchedLength, 0, 0);
        }

        return count;
    }



    private static int ExecuteExactUtf8LiteralFamilyBoundaryFilteredIndexSum(Utf8SearchPlan plan, byte[] input)
    {
        var sum = 0;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        while (plan.PreparedSearcher.TryFindNextNonOverlappingMatch(span, ref state, out var match))
        {
            if (!Utf8SearchExecutor.MatchesBoundaryRequirements(plan, span, match.Index, match.Length))
            {
                continue;
            }

            sum += match.Index;
        }

        return sum;
    }

    private static int ExecuteExactUtf8LiteralFamilyConfirmedCount(Utf8SearchPlan plan, byte[] input, bool pollDeadline)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        var budget = Utf8ExecutionDeadline.Infinite;
        while (true)
        {
            if (pollDeadline)
            {
                budget.Step();
            }

            if (!plan.PreparedSearcher.TryFindNextNonOverlappingMatch(span, ref state, out var match))
            {
                return count;
            }

            if (Utf8ConfirmationExecutor.IsMatch(
                    in plan,
                    plan.SearchOperation.Confirmation,
                    span,
                    match.Index,
                    match.Length))
            {
                count++;
            }
        }
    }

    private static int ExecuteExactUtf8LiteralFamilyConfirmedIndexSum(Utf8SearchPlan plan, byte[] input, bool pollDeadline)
    {
        var sum = 0;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var span = input.AsSpan();
        var budget = Utf8ExecutionDeadline.Infinite;
        while (true)
        {
            if (pollDeadline)
            {
                budget.Step();
            }

            if (!plan.PreparedSearcher.TryFindNextNonOverlappingMatch(span, ref state, out var match))
            {
                return sum;
            }

            if (!Utf8ConfirmationExecutor.IsMatch(
                    in plan,
                    plan.SearchOperation.Confirmation,
                    span,
                    match.Index,
                    match.Length))
            {
                continue;
            }

            sum += match.Index;
        }
    }

}

