using System.Diagnostics;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const double PythonReScalingTargetSampleMilliseconds = 10;
    private const int PythonReScalingMaximumIterations = 2_000_000;
    private const int PythonReScalingShortRouteCalibrationIterations = 10_000;
    private const int PythonReScalingShortRouteWarmupCalls = 1_000_000;

    private static int MeasureOneShotScaling(string id, int minimumIterations, int samples)
    {
#if DEBUG
        Console.Error.WriteLine("PythonRe one-shot scaling requires a Release build.");
        return 1;
#else
        if (samples < 3)
        {
            Console.Error.WriteLine("PythonRe one-shot scaling requires at least three samples.");
            return 1;
        }

        var sourceCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.Ordinal));
        if (sourceCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{id}'.");
            return 1;
        }

        if (sourceCase.Id is not "literal/search" and not "literal/search-miss" and not "prefix/match")
        {
            Console.Error.WriteLine(
                "PythonRe one-shot scaling currently requires literal/search, literal/search-miss, or prefix/match.");
            return 1;
        }

        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        using var worker = new CpythonStreamWorker();
        List<PythonReOneShotScalingPoint> points = [];
        int[] inputSizes = [64, 256, 1_024, 4_096, 16_384, 65_536];
        foreach (var position in new[]
                 {
                     PythonReOneShotPosition.HitAtStart,
                     PythonReOneShotPosition.HitLate,
                     PythonReOneShotPosition.Miss,
                 })
        {
            foreach (var inputSize in inputSizes)
            {
                var subject = BuildOneShotScalingSubject(sourceCase, position, inputSize);
                var benchmarkCase = sourceCase with
                {
                    Id = $"scaling/{DescribeOneShotPosition(position)}/{inputSize}",
                    Input = subject,
                };
                points.Add(MeasureOneShotScalingPoint(
                    benchmarkCase,
                    position,
                    minimumIterations,
                    samples,
                    worker));
            }
        }

        Console.WriteLine($"SourceCase          : {sourceCase.Id}");
        Console.WriteLine($"Pattern             : {sourceCase.Pattern}");
        Console.WriteLine($"Samples             : {samples}");
        Console.WriteLine($"TargetSample        : {PythonReScalingTargetSampleMilliseconds:F0} ms/lane");
        Console.WriteLine($"CpuPolicy           : {processorScope.Policy}");
        Console.WriteLine($"CpuAffinityMask     : {processorScope.AffinityMask}");
        Console.WriteLine("Result model        : retained value ranges with checksum and semantic-digest preflight");
        Console.WriteLine("Status effect       : diagnostic only; this command never rewrites the snapshot");
        Console.WriteLine();
        Console.WriteLine(
            "Position       Bytes  Managed us  Alloc B  CPython str  CPython bytes  Rstrong  Rbyte  Spread M/S/B");
        foreach (var point in points)
        {
            Console.WriteLine(
                $"{DescribeOneShotPosition(point.Position),-13} " +
                $"{point.InputBytes,6} " +
                $"{point.ManagedMicroseconds,11:F3} " +
                $"{point.ManagedAllocatedBytes,8} " +
                $"{point.CpythonPredecodedMicroseconds,12:F3} " +
                $"{point.CpythonBytesMicroseconds,13:F3} " +
                $"{point.ManagedMicroseconds / point.CpythonPredecodedMicroseconds,8:F3} " +
                $"{point.ManagedMicroseconds / point.CpythonBytesMicroseconds,7:F3} " +
                $"{point.ManagedSpread:F2}/{point.CpythonPredecodedSpread:F2}/{point.CpythonBytesSpread:F2}");
        }

        Console.WriteLine();
        Console.WriteLine(
            "Position       Lane           Intercept us  Slope us/KiB  Local slope range  Max residual  Max spread  Fit gate");
        foreach (var position in Enum.GetValues<PythonReOneShotPosition>())
        {
            var family = points.Where(point => point.Position == position).ToArray();
            var managedFit = FitOneShotScaling(
                family,
                static point => point.ManagedMicroseconds,
                static point => point.ManagedSpread);
            var predecodedFit = FitOneShotScaling(
                family,
                static point => point.CpythonPredecodedMicroseconds,
                static point => point.CpythonPredecodedSpread);
            var bytesFit = FitOneShotScaling(
                family,
                static point => point.CpythonBytesMicroseconds,
                static point => point.CpythonBytesSpread);
            PrintOneShotScalingFit(position, "Managed", managedFit);
            PrintOneShotScalingFit(position, "CPython str", predecodedFit);
            PrintOneShotScalingFit(position, "CPython bytes", bytesFit);
            Console.WriteLine(
                $"{DescribeOneShotPosition(position),-13} Break-even     " +
                $"str={DescribeBreakEven(managedFit, predecodedFit),-14} " +
                $"bytes={DescribeBreakEven(managedFit, bytesFit)}");
        }

        return 0;
#endif
    }

    private static PythonReOneShotScalingPoint MeasureOneShotScalingPoint(
        PythonReBenchmarkCase benchmarkCase,
        PythonReOneShotPosition position,
        int minimumIterations,
        int samples,
        CpythonStreamWorker worker)
    {
        var context = new PythonReBenchmarkContext(benchmarkCase);
        if (!context.SupportsOneShotPhases ||
            !context.UsesZeroOffsetUtf8ValueFastPath &&
            !context.UsesAsciiLiteralPrefixDigitMatchFastPath)
        {
            throw new InvalidOperationException(
                $"PythonRe one-shot scaling '{benchmarkCase.Id}' did not retain its direct one-shot route.");
        }

        var eligibility = PythonReBenchmarkCatalog.GetByteControlEligibility(
            benchmarkCase,
            context.InputBytes);
        if (!eligibility.IsEligible)
        {
            throw new InvalidOperationException(
                $"PythonRe one-shot scaling '{benchmarkCase.Id}' has no bytes control: {eligibility.Reason}");
        }

        var expectedChecksum = context.ExecutePythonRe();
        var expectedSemanticDigest = context.ExecutePythonReSemanticDigest();
        var expectedConsumptionToken = context.ExecutePythonReConsumptionToken();
        var prepared = worker.Prepare(benchmarkCase, context.InputBytes, enableByteControl: true);
        if (prepared.Checksum != expectedChecksum ||
            prepared.SemanticDigest != expectedSemanticDigest ||
            prepared.ConsumptionChecksum != expectedConsumptionToken ||
            !prepared.ByteControlAvailable ||
            prepared.ByteControlChecksum != expectedChecksum ||
            prepared.ByteControlSemanticDigest != expectedSemanticDigest ||
            prepared.ByteControlConsumptionChecksum != expectedConsumptionToken)
        {
            throw new InvalidOperationException(
                $"PythonRe one-shot scaling '{benchmarkCase.Id}' failed structured CPython preflight.");
        }

        var managedIterations = CalibrateOneShotManagedIterations(
            context,
            minimumIterations,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken);
        var predecodedIterations = Math.Max(
            minimumIterations,
            worker.Calibrate(
                CpythonStreamLane.Predecoded,
                PythonReScalingTargetSampleMilliseconds,
                PythonReScalingMaximumIterations).Iterations);
        var bytesIterations = Math.Max(
            minimumIterations,
            worker.Calibrate(
                CpythonStreamLane.Bytes,
                PythonReScalingTargetSampleMilliseconds,
                PythonReScalingMaximumIterations).Iterations);
        WarmOneShotManaged(
            context,
            managedIterations,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken);
        _ = worker.Warm(
            CpythonStreamLane.Predecoded,
            predecodedIterations,
            minimumMilliseconds: 20,
            minimumCalls: 1_024,
            maximumBatches: 8);
        _ = worker.Warm(
            CpythonStreamLane.Bytes,
            bytesIterations,
            minimumMilliseconds: 20,
            minimumCalls: 1_024,
            maximumBatches: 8);

        var managed = new double[samples];
        var allocations = new long[samples];
        var predecoded = new double[samples];
        var bytes = new double[samples];
        for (var sample = 0; sample < samples; sample++)
        {
            for (var laneOffset = 0; laneOffset < 3; laneOffset++)
            {
                switch ((sample + laneOffset) % 3)
                {
                    case 0:
                    {
                        var batch = context.MeasurePythonReQualificationBatch(managedIterations);
                        VerifyManagedResult(
                            batch,
                            expectedChecksum,
                            expectedSemanticDigest,
                            expectedConsumptionToken,
                            managedIterations,
                            "one-shot scaling");
                        managed[sample] = batch.Elapsed.TotalMicroseconds / managedIterations;
                        allocations[sample] = batch.AllocatedBytes / managedIterations;
                        break;
                    }
                    case 1:
                    {
                        var response = worker.Measure(CpythonStreamLane.Predecoded, predecodedIterations);
                        predecoded[sample] = response.ElapsedNanoseconds /
                            (double)predecodedIterations / 1_000;
                        break;
                    }
                    case 2:
                    {
                        var response = worker.Measure(CpythonStreamLane.Bytes, bytesIterations);
                        bytes[sample] = response.ElapsedNanoseconds /
                            (double)bytesIterations / 1_000;
                        break;
                    }
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        return new PythonReOneShotScalingPoint(
            position,
            context.InputBytes.Length,
            BenchmarkPairedStatistics.Median(managed),
            (long)Math.Round(BenchmarkPairedStatistics.Median(
                allocations.Select(static allocation => (double)allocation))),
            BenchmarkPairedStatistics.Median(predecoded),
            BenchmarkPairedStatistics.Median(bytes),
            BenchmarkPairedStatistics.InterquartileSpread(managed),
            BenchmarkPairedStatistics.InterquartileSpread(predecoded),
            BenchmarkPairedStatistics.InterquartileSpread(bytes));
    }

    private static int CalibrateOneShotManagedIterations(
        PythonReBenchmarkContext context,
        int minimumIterations,
        int expectedChecksum,
        ulong expectedSemanticDigest,
        ulong expectedConsumptionToken)
    {
        var iterations = Math.Max(16, minimumIterations);
        PythonReBenchmarkBatch batch;
        while (true)
        {
            batch = context.MeasurePythonReQualificationBatch(iterations);
            VerifyManagedResult(
                batch,
                expectedChecksum,
                expectedSemanticDigest,
                expectedConsumptionToken,
                iterations,
                "one-shot scaling calibration");
            if (batch.Elapsed.TotalMilliseconds >= 2 ||
                iterations >= PythonReScalingMaximumIterations)
            {
                break;
            }

            iterations = Math.Min(PythonReScalingMaximumIterations, checked(iterations * 2));
        }

        return (int)Math.Clamp(
            Math.Ceiling(
                iterations * PythonReScalingTargetSampleMilliseconds /
                Math.Max(batch.Elapsed.TotalMilliseconds, 0.000_001)),
            minimumIterations,
            PythonReScalingMaximumIterations);
    }

    private static PythonReWarmup WarmOneShotManaged(
        PythonReBenchmarkContext context,
        int iterations,
        int expectedChecksum,
        ulong expectedSemanticDigest,
        ulong expectedConsumptionToken)
    {
        var minimumCalls = iterations >= PythonReScalingShortRouteCalibrationIterations
            ? PythonReScalingShortRouteWarmupCalls
            : 1_024;
        var maximumCalls = Math.Max((long)minimumCalls, iterations * 8L);
        var elapsed = Stopwatch.StartNew();
        var calls = 0;
        while ((elapsed.ElapsedMilliseconds < 20 || calls < minimumCalls) && calls < maximumCalls)
        {
            var batch = context.MeasurePythonReQualificationBatch(iterations);
            VerifyManagedResult(
                batch,
                expectedChecksum,
                expectedSemanticDigest,
                expectedConsumptionToken,
                iterations,
                "one-shot scaling warmup");
            calls += iterations;
        }

        return new PythonReWarmup(calls, elapsed.Elapsed);
    }

    private static string BuildOneShotScalingSubject(
        PythonReBenchmarkCase sourceCase,
        PythonReOneShotPosition position,
        int inputSize)
    {
        if (sourceCase.Id == "prefix/match")
        {
            const string match = "header:12345";
            const string incomplete = "header:";
            if (inputSize < match.Length)
            {
                throw new InvalidOperationException("PythonRe prefix Match scaling input is too short.");
            }

            return position switch
            {
                PythonReOneShotPosition.HitAtStart => match + new string('x', inputSize - match.Length),
                PythonReOneShotPosition.HitLate => new string('x', inputSize - match.Length) + match,
                PythonReOneShotPosition.Miss => incomplete + new string('x', inputSize - incomplete.Length),
                _ => throw new ArgumentOutOfRangeException(nameof(position)),
            };
        }

        var pattern = sourceCase.Pattern;
        if (pattern != "needle" || inputSize < pattern.Length)
        {
            throw new InvalidOperationException(
                "PythonRe one-shot scaling currently requires the catalog's ASCII 'needle' literal.");
        }

        return position switch
        {
            PythonReOneShotPosition.HitAtStart => pattern + new string('x', inputSize - pattern.Length),
            PythonReOneShotPosition.HitLate => new string('x', inputSize - pattern.Length) + pattern,
            PythonReOneShotPosition.Miss => new string('x', inputSize),
            _ => throw new ArgumentOutOfRangeException(nameof(position)),
        };
    }

    private static PythonReOneShotScalingFit FitOneShotScaling(
        IReadOnlyList<PythonReOneShotScalingPoint> points,
        Func<PythonReOneShotScalingPoint, double> selector,
        Func<PythonReOneShotScalingPoint, double> spreadSelector)
    {
        List<double> slopes = [];
        for (var left = 0; left < points.Count; left++)
        {
            for (var right = left + 1; right < points.Count; right++)
            {
                slopes.Add(
                    (selector(points[right]) - selector(points[left])) /
                    (points[right].InputBytes - points[left].InputBytes) * 1_024);
            }
        }

        var slope = BenchmarkPairedStatistics.Median(slopes);
        var intercept = BenchmarkPairedStatistics.Median(points.Select(
            point => selector(point) - slope * point.InputBytes / 1_024));
        var maximumRelativeResidual = points.Max(point =>
        {
            var observed = selector(point);
            var predicted = intercept + slope * point.InputBytes / 1_024;
            return Math.Abs(observed - predicted) / Math.Max(observed, 0.000_001);
        });
        var localSlopes = points.Zip(points.Skip(1), (left, right) =>
            (selector(right) - selector(left)) /
            (right.InputBytes - left.InputBytes) * 1_024).ToArray();
        return new PythonReOneShotScalingFit(
            intercept,
            slope,
            localSlopes.Min(),
            localSlopes.Max(),
            maximumRelativeResidual,
            points.Max(spreadSelector));
    }

    private static void PrintOneShotScalingFit(
        PythonReOneShotPosition position,
        string lane,
        PythonReOneShotScalingFit fit)
    {
        Console.WriteLine(
            $"{DescribeOneShotPosition(position),-13} " +
            $"{lane,-14} " +
            $"{fit.InterceptMicroseconds,12:F3} " +
            $"{fit.SlopeMicrosecondsPerKib,13:F4} " +
            $"{fit.MinimumLocalSlope,8:F4}..{fit.MaximumLocalSlope,8:F4} " +
            $"{fit.MaximumRelativeResidual,11:P1} " +
            $"{fit.MaximumPointSpread,10:F3} " +
            $"{(fit.IsStable ? "pass" : "reject")}");
    }

    private static string DescribeBreakEven(
        PythonReOneShotScalingFit managed,
        PythonReOneShotScalingFit comparator)
    {
        var slopeDifference = managed.SlopeMicrosecondsPerKib - comparator.SlopeMicrosecondsPerKib;
        if (Math.Abs(slopeDifference) < 0.000_000_1)
        {
            return managed.InterceptMicroseconds <= comparator.InterceptMicroseconds
                ? "managed-always"
                : "CPython-always";
        }

        var interceptDifference = managed.InterceptMicroseconds - comparator.InterceptMicroseconds;
        var breakEvenKib =
            -interceptDifference / slopeDifference;
        if (breakEvenKib >= 0 && double.IsFinite(breakEvenKib))
        {
            return $"{breakEvenKib:F2} KiB";
        }

        return interceptDifference <= 0 && slopeDifference <= 0
            ? "managed-always"
            : "CPython-always";
    }

    private static string DescribeOneShotPosition(PythonReOneShotPosition position) => position switch
    {
        PythonReOneShotPosition.HitAtStart => "hit-start",
        PythonReOneShotPosition.HitLate => "hit-late",
        PythonReOneShotPosition.Miss => "miss",
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };
}

internal enum PythonReOneShotPosition : byte
{
    HitAtStart = 0,
    HitLate = 1,
    Miss = 2,
}

internal readonly record struct PythonReOneShotScalingPoint(
    PythonReOneShotPosition Position,
    int InputBytes,
    double ManagedMicroseconds,
    long ManagedAllocatedBytes,
    double CpythonPredecodedMicroseconds,
    double CpythonBytesMicroseconds,
    double ManagedSpread,
    double CpythonPredecodedSpread,
    double CpythonBytesSpread);

internal readonly record struct PythonReOneShotScalingFit(
    double InterceptMicroseconds,
    double SlopeMicrosecondsPerKib,
    double MinimumLocalSlope,
    double MaximumLocalSlope,
    double MaximumRelativeResidual,
    double MaximumPointSpread)
{
    internal bool IsStable => MaximumRelativeResidual <= 0.15 && MaximumPointSpread <= 1.10;
}
