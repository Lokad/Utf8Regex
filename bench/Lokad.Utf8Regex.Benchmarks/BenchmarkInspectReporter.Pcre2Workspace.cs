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
        var context = new Utf8Pcre2BenchmarkContext(Utf8Pcre2BenchmarkCatalog.Get(caseId));
        if (context.Utf8Pcre2Regex.DebugCompiledProgram.Operations.Count is not Pcre2BacktrackingDirectProgram direct)
        {
            Console.Error.WriteLine($"Case '{caseId}' does not use the PCRE2 backtracking Count runner.");
            return 2;
        }

        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var diagnostics = context.Utf8Pcre2Regex.DebugCountWithDiagnostics(context.InputBytes, 0).Execution;
        var poolReplay = MeasureMedianMicroseconds(
            samples,
            iterations,
            () => ReplayWorkspacePoolTraffic(direct.Program, diagnostics));
        var loopControl = MeasureMedianMicroseconds(
            samples,
            iterations,
            () => ReplayWorkspaceLoopControl(diagnostics));

        Console.WriteLine($"CaseId            : {caseId}");
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
}
