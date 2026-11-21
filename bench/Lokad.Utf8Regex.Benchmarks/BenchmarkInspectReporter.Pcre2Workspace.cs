using System.Buffers;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    public static int RunMeasurePcre2WorkspacePoolCost(
        string caseId,
        string? iterationsText,
        string? samplesText)
    {
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
        if (!TryCapturePcre2AttributionDiagnostics(benchmarkCase, context, out var attribution))
        {
            return 2;
        }

        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var diagnostics = attribution.Execution;
        var poolReplay = MeasureMedianMicroseconds(
            samples,
            iterations,
            () => ReplayWorkspacePoolTraffic(attribution.Program, diagnostics));
        var loopControl = MeasureMedianMicroseconds(
            samples,
            iterations,
            () => ReplayWorkspaceLoopControl(diagnostics));

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {attribution.Operation}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"WorkspaceRents    : {diagnostics.WorkspacePoolRents}");
        Console.WriteLine($"WorkspaceFixed    : {diagnostics.WorkspaceFixedRents}");
        Console.WriteLine($"WorkspaceFrames   : {diagnostics.WorkspaceFrameRents}");
        Console.WriteLine($"WorkspaceRepeats  : {diagnostics.WorkspaceRepeatMutationRents}");
        Console.WriteLine($"WorkspaceCaptures : {diagnostics.WorkspaceCaptureMutationRents}");
        Console.WriteLine($"WorkspaceControls : {diagnostics.WorkspaceControlRents}");
        Console.WriteLine($"WorkspaceGrowths  : {diagnostics.WorkspacePoolGrowths}");
        Console.WriteLine($"PoolReplay        : {poolReplay:F3} us/op");
        Console.WriteLine($"LoopControl       : {loopControl:F3} us/op");
        Console.WriteLine($"NetPoolEstimate   : {Math.Max(0, poolReplay - loopControl):F3} us/op");
        Console.WriteLine("ReplayScope       : minimum-size rent/return traffic; excludes growth copies and VM state reset");
        return 0;
    }

    private static bool TryCapturePcre2AttributionDiagnostics(
        Utf8Pcre2BenchmarkCase benchmarkCase,
        Utf8Pcre2BenchmarkContext context,
        out Pcre2AttributionDiagnostics attribution)
    {
        var operations = context.Utf8Pcre2Regex.DebugCompiledProgram.Operations;
        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.Count) != 0)
        {
            var program = operations.Enumerate switch
            {
                Pcre2BacktrackingDirectProgram direct => direct.Program,
                _ => null,
            };
            if (program is not null)
            {
                attribution = new Pcre2AttributionDiagnostics(
                    Utf8Pcre2BenchmarkOperation.Count,
                    program,
                    InspectPcre2CountWithDiagnostics(context.Utf8Pcre2Regex, context.InputBytes, 0).Execution);
                return true;
            }
        }
        else if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.IsMatch) != 0)
        {
            var program = operations.IsMatch switch
            {
                Pcre2BacktrackingDirectProgram direct => direct.Program,
                Pcre2LiteralFamilyDirectProgram literalFamily => literalFamily.Fallback,
                _ => null,
            };
            if (program is not null)
            {
                attribution = new Pcre2AttributionDiagnostics(
                    Utf8Pcre2BenchmarkOperation.IsMatch,
                    program,
                    InspectPcre2IsMatchWithDiagnostics(context.Utf8Pcre2Regex, context.InputBytes, 0).Execution);
                return true;
            }
        }

        attribution = default;
        Console.Error.WriteLine(
            $"Case '{benchmarkCase.Id}' does not expose a backtracking attribution route for its selected public operation.");
        return false;
    }

    private static int ReplayWorkspacePoolTraffic(
        Pcre2BacktrackingProgram program,
        Pcre2ExecutionDiagnostics diagnostics)
    {
        var sink = 0;
        for (ulong i = 0; i < diagnostics.WorkspaceFixedRents; i++)
        {
            var items = ArrayPool<int>.Shared.Rent(checked(program.RepeatCount * 2));
            sink ^= items.Length;
            ArrayPool<int>.Shared.Return(items);
        }

        for (ulong i = 0; i < diagnostics.WorkspaceFrameRents; i++)
        {
            var items = ArrayPool<Pcre2BacktrackingFrame>.Shared.Rent(4);
            sink ^= items.Length;
            ArrayPool<Pcre2BacktrackingFrame>.Shared.Return(items);
        }

        for (ulong i = 0; i < diagnostics.WorkspaceRepeatMutationRents; i++)
        {
            var items = ArrayPool<Pcre2RepeatMutation>.Shared.Rent(4);
            sink ^= items.Length;
            ArrayPool<Pcre2RepeatMutation>.Shared.Return(items);
        }

        return sink;
    }

    private static int ReplayWorkspaceLoopControl(Pcre2ExecutionDiagnostics diagnostics)
    {
        var iterations = diagnostics.WorkspaceFixedRents +
            diagnostics.WorkspaceFrameRents +
            diagnostics.WorkspaceRepeatMutationRents;
        var sink = 0;
        for (ulong i = 0; i < iterations; i++)
        {
            sink ^= (int)i;
        }

        return sink;
    }

    private readonly record struct Pcre2AttributionDiagnostics(
        Utf8Pcre2BenchmarkOperation Operation,
        Pcre2BacktrackingProgram Program,
        Pcre2ExecutionDiagnostics Execution);
}
