using System.Diagnostics;
using System.Text.Json;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const int PythonReQualificationProtocolVersion = 2;
    private const int PythonReQualificationBootstrapSeed = 31302;
    private const int PythonReQualificationBootstrapResamples = 10_000;
    private const int PythonReQualificationMaximumIterations = 10_000_000;
    private const double PythonReQualificationTargetSampleMilliseconds = 40;
    private const double PythonReQualificationPilotMilliseconds = 5;
    private const double PythonReQualificationMinimumSampleMilliseconds = 20;
    private const double PythonReQualificationMaximumSpread = 1.10;
    private static readonly TimeSpan s_cpythonResponseTimeout = TimeSpan.FromSeconds(10);

    private static int MeasurePairedCase(string caseId, int samples, bool cpythonFirst)
    {
#if DEBUG
        Console.Error.WriteLine("PythonRe paired measurement requires a Release build.");
        return 1;
#else
        if (samples < 9)
        {
            Console.Error.WriteLine("PythonRe paired measurement requires at least nine samples.");
            return 1;
        }

        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(caseId, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{caseId}'.");
            return 1;
        }

        var worktreeState = RunGit(
            "status",
            "--porcelain=v1",
            "--untracked-files=all",
            "--",
            ".",
            ":(exclude)UTF8REGEX-PERFORMANCE-ROADMAP.md");
        if (worktreeState is null)
        {
            Console.Error.WriteLine("Could not verify the worktree before PythonRe paired measurement.");
            return 1;
        }

        var worktreeQualified = string.IsNullOrWhiteSpace(worktreeState);
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        using var currentProcess = Process.GetCurrentProcess();
        var context = new PythonReBenchmarkContext(benchmarkCase);
        var expectedChecksum = context.ExecutePythonRe();
        using var worker = new CpythonStreamWorker();
        var prepared = worker.Prepare(benchmarkCase, context.InputBytes);
        if (prepared.Checksum != expectedChecksum)
        {
            throw new InvalidOperationException(
                $"PythonRe benchmark '{caseId}' disagrees with CPython streaming preflight: " +
                $"managed={expectedChecksum}, CPython={prepared.Checksum}.");
        }

        for (var warmupCall = 0; warmupCall < 64; warmupCall++)
        {
            var batch = context.MeasurePythonReBatch(1);
            VerifyManagedChecksum(batch, expectedChecksum, "tiering warmup");
        }

        var preliminaryManagedIterations = CalibrateManagedBatch(context, expectedChecksum);
        var preliminaryCpythonCalibration = worker.Calibrate(
            CpythonStreamLane.Predecoded,
            PythonReQualificationTargetSampleMilliseconds,
            PythonReQualificationMaximumIterations);
        var managedWarmup = WarmManagedLane(context, preliminaryManagedIterations, expectedChecksum);
        var cpythonWarmup = worker.Warm(
            CpythonStreamLane.Predecoded,
            preliminaryCpythonCalibration.Iterations,
            minimumMilliseconds: 100,
            maximumBatches: 32);
        var managedIterations = CalibrateManagedBatch(context, expectedChecksum);
        var cpythonCalibration = worker.Calibrate(
            CpythonStreamLane.Predecoded,
            PythonReQualificationTargetSampleMilliseconds,
            PythonReQualificationMaximumIterations);
        var cpythonIterations = cpythonCalibration.Iterations;

        var laneOrders = new List<PythonRePairLaneOrder>(samples);
        var managedMicroseconds = new List<double>(samples);
        var cpythonMicroseconds = new List<double>(samples);
        var managedMilliseconds = new List<double>(samples);
        var cpythonMilliseconds = new List<double>(samples);
        var managedProcessCpuMilliseconds = new List<double>(samples);
        var cpythonProcessCpuMilliseconds = new List<double>(samples);
        var managedGcCollections = new List<int[]>(samples);
        var cpythonGcCollections = new List<int[]>(samples);
        var managedAllocatedBytes = new List<long>(samples);
        var ratios = new List<double>(samples);

        for (var sample = 0; sample < samples; sample++)
        {
            var order = (sample + (cpythonFirst ? 1 : 0)) % 2 == 0
                ? PythonRePairLaneOrder.ManagedFirst
                : PythonRePairLaneOrder.CpythonFirst;
            PythonReManagedSample managed;
            CpythonStreamResponse cpython;
            if (order == PythonRePairLaneOrder.ManagedFirst)
            {
                managed = MeasureManagedSample(context, managedIterations, expectedChecksum, currentProcess);
                cpython = worker.Measure(CpythonStreamLane.Predecoded, cpythonIterations);
            }
            else
            {
                cpython = worker.Measure(CpythonStreamLane.Predecoded, cpythonIterations);
                managed = MeasureManagedSample(context, managedIterations, expectedChecksum, currentProcess);
            }

            var managedPerOperation = managed.Batch.Elapsed.TotalMicroseconds / managedIterations;
            var cpythonPerOperation = cpython.ElapsedNanoseconds / (double)cpythonIterations / 1_000;
            laneOrders.Add(order);
            managedMicroseconds.Add(managedPerOperation);
            cpythonMicroseconds.Add(cpythonPerOperation);
            managedMilliseconds.Add(managed.Batch.Elapsed.TotalMilliseconds);
            cpythonMilliseconds.Add(cpython.ElapsedNanoseconds / 1_000_000d);
            managedProcessCpuMilliseconds.Add(managed.ProcessCpu.TotalMilliseconds);
            cpythonProcessCpuMilliseconds.Add(cpython.ProcessCpuNanoseconds / 1_000_000d);
            managedGcCollections.Add(managed.GcCollections);
            cpythonGcCollections.Add(cpython.GcCollections);
            managedAllocatedBytes.Add(managed.Batch.AllocatedBytes / managedIterations);
            ratios.Add(managedPerOperation / cpythonPerOperation);
        }

        const int floorSamples = 3;
        var managedFloorMicroseconds = new List<double>(floorSamples);
        var cpythonFloorMicroseconds = new List<double>(floorSamples);
        for (var sample = 0; sample < floorSamples; sample++)
        {
            var managedFloor = MeasureManagedEmptyLoop(managedIterations);
            var cpythonFloor = worker.Measure(CpythonStreamLane.EmptyLoop, cpythonIterations);
            managedFloorMicroseconds.Add(managedFloor.Elapsed.TotalMicroseconds / managedIterations);
            cpythonFloorMicroseconds.Add(
                cpythonFloor.ElapsedNanoseconds / (double)cpythonIterations / 1_000);
            s_sink ^= managedFloor.Checksum ^ cpythonFloor.Checksum;
        }

        var logRatios = ratios.Select(static ratio => Math.Log(ratio)).ToArray();
        var interval = BenchmarkPairedStatistics.BootstrapMedianLogRatio(
            logRatios,
            PythonReQualificationBootstrapSeed,
            PythonReQualificationBootstrapResamples);
        var ratioMedian = Math.Exp(BenchmarkPairedStatistics.Median(logRatios));
        var ratioLower = Math.Exp(interval.Lower);
        var ratioUpper = Math.Exp(interval.Upper);
        var managedMedian = BenchmarkPairedStatistics.Median(managedMicroseconds);
        var cpythonMedian = BenchmarkPairedStatistics.Median(cpythonMicroseconds);
        var managedFloorMedian = BenchmarkPairedStatistics.Median(managedFloorMicroseconds);
        var cpythonFloorMedian = BenchmarkPairedStatistics.Median(cpythonFloorMicroseconds);
        var managedFloorFraction = managedFloorMedian / managedMedian;
        var cpythonFloorFraction = cpythonFloorMedian / cpythonMedian;
        var managedFirstRatios = laneOrders
            .Select((order, index) => new PythonReOrderedRatio(order, ratios[index]))
            .Where(static sample => sample.Order == PythonRePairLaneOrder.ManagedFirst)
            .Select(static sample => sample.Ratio)
            .ToArray();
        var cpythonFirstRatios = laneOrders
            .Select((order, index) => new PythonReOrderedRatio(order, ratios[index]))
            .Where(static sample => sample.Order == PythonRePairLaneOrder.CpythonFirst)
            .Select(static sample => sample.Ratio)
            .ToArray();
        var orderEffect = BenchmarkPairedStatistics.Median(managedFirstRatios) /
            BenchmarkPairedStatistics.Median(cpythonFirstRatios);
        var managedSpread = BenchmarkPairedStatistics.InterquartileSpread(managedMicroseconds);
        var cpythonSpread = BenchmarkPairedStatistics.InterquartileSpread(cpythonMicroseconds);
        var durationsQualified = managedMilliseconds.All(
                                     static duration => duration >= PythonReQualificationMinimumSampleMilliseconds) &&
                                 cpythonMilliseconds.All(
                                     static duration => duration >= PythonReQualificationMinimumSampleMilliseconds);
        var placementQualified = processorScope.Policy == "single-highest-efficiency-processor";
        var status = DeriveStatus(
            worktreeQualified,
            placementQualified,
            durationsQualified,
            ratioLower,
            ratioUpper,
            orderEffect,
            managedSpread,
            cpythonSpread,
            managedFloorFraction,
            cpythonFloorFraction);

        Console.WriteLine($"CaseId             : {caseId}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine(
            $"Protocol           : streaming v{PythonReQualificationProtocolVersion}; " +
            "alternating paired samples");
        Console.WriteLine($"CPU placement      : {processorScope.Description}");
        Console.WriteLine($"Worktree qualified : {worktreeQualified}");
        Console.WriteLine($"Initial lane       : {(cpythonFirst ? "CPython" : "PythonRe")}");
        Console.WriteLine($"Samples            : {samples}");
        Console.WriteLine($"Iterations         : managed={managedIterations}; CPython={cpythonIterations}");
        Console.WriteLine(
            $"Warmup             : managed={managedWarmup.Iterations} calls/" +
            $"{managedWarmup.Elapsed.TotalMilliseconds:F1} ms; " +
            $"CPython={cpythonWarmup.Iterations} calls/" +
            $"{cpythonWarmup.ElapsedNanoseconds / 1_000_000d:F1} ms");
        Console.WriteLine($"PythonRe elapsed   : {managedMedian:F3} us/op");
        Console.WriteLine($"CPython elapsed    : {cpythonMedian:F3} us/op");
        Console.WriteLine($"Rstrong            : {ratioMedian:F3} [{ratioLower:F3}, {ratioUpper:F3}]");
        Console.WriteLine($"Estrong            : {managedMedian - cpythonMedian:+0.000;-0.000;0.000} us/op");
        Console.WriteLine($"Order effect       : {orderEffect:F3}");
        Console.WriteLine($"IQR spread         : managed={managedSpread:F3}; CPython={cpythonSpread:F3}");
        Console.WriteLine($"Harness floor      : managed={managedFloorFraction:P2}; CPython={cpythonFloorFraction:P2}");
        Console.WriteLine(
            $"Managed alloc      : " +
            $"{BenchmarkPairedStatistics.Median(managedAllocatedBytes.Select(static value => (double)value)):F0} B/op");
        Console.WriteLine(
            $"Process CPU/sample : managed=" +
            $"{BenchmarkPairedStatistics.Median(managedProcessCpuMilliseconds):F1} ms; " +
            $"CPython={BenchmarkPairedStatistics.Median(cpythonProcessCpuMilliseconds):F1} ms (diagnostic)");
        Console.WriteLine($"Status             : {FormatStatus(status.Status)}");
        Console.WriteLine($"Status reason      : {status.Reason ?? "qualified paired evidence"}");
        Console.WriteLine($"CPython            : {worker.Environment.Implementation} " +
                          $"{worker.Environment.Version}; {worker.Environment.ExecutableSha256}");
        Console.WriteLine("Raw pairs:");
        for (var sample = 0; sample < samples; sample++)
        {
            Console.WriteLine(
                $"  {sample + 1,2}: {laneOrders[sample],12}; " +
                $"managed={managedMicroseconds[sample],10:F3} us; " +
                $"CPython={cpythonMicroseconds[sample],10:F3} us; R={ratios[sample]:F3}; " +
                $"GC={FormatGcCollections(managedGcCollections[sample])}/" +
                $"{FormatGcCollections(cpythonGcCollections[sample])}");
        }

        return 0;

        static string FormatGcCollections(IReadOnlyList<int> collections) =>
            collections.Count == 3
                ? $"{collections[0]},{collections[1]},{collections[2]}"
                : "unavailable";
#endif
    }

    private static int CalibrateManagedBatch(PythonReBenchmarkContext context, int expectedChecksum)
    {
        var iterations = 1;
        var pilot = context.MeasurePythonReBatch(iterations);
        VerifyManagedChecksum(pilot, expectedChecksum, "calibration");
        while (pilot.Elapsed.TotalMilliseconds < PythonReQualificationPilotMilliseconds &&
               iterations < PythonReQualificationMaximumIterations)
        {
            var elapsed = Math.Max(pilot.Elapsed.TotalMilliseconds, 0.000_001);
            var growth = Math.Max(2, (int)Math.Ceiling(PythonReQualificationPilotMilliseconds / elapsed));
            iterations = (int)Math.Min(
                PythonReQualificationMaximumIterations,
                (long)iterations * growth);
            pilot = context.MeasurePythonReBatch(iterations);
            VerifyManagedChecksum(pilot, expectedChecksum, "calibration");
        }

        iterations = (int)Math.Clamp(
            Math.Round(iterations * PythonReQualificationTargetSampleMilliseconds /
                       Math.Max(pilot.Elapsed.TotalMilliseconds, 0.000_001)),
            1,
            PythonReQualificationMaximumIterations);
        const int confirmationAttempts = 2;
        for (var attempt = 0; attempt < confirmationAttempts; attempt++)
        {
            var confirmation = context.MeasurePythonReBatch(iterations);
            VerifyManagedChecksum(confirmation, expectedChecksum, "calibration confirmation");
            if (confirmation.Elapsed.TotalMilliseconds is >= 30 and <= 50)
            {
                break;
            }

            iterations = (int)Math.Clamp(
                Math.Round(
                    iterations * PythonReQualificationTargetSampleMilliseconds /
                    Math.Max(confirmation.Elapsed.TotalMilliseconds, 0.000_001)),
                1,
                PythonReQualificationMaximumIterations);
        }

        return iterations;
    }

    private static PythonReWarmup WarmManagedLane(
        PythonReBenchmarkContext context,
        int iterations,
        int expectedChecksum)
    {
        const int maximumBatches = 32;
        var started = Stopwatch.GetTimestamp();
        var batches = 0;
        do
        {
            var batch = context.MeasurePythonReBatch(iterations);
            VerifyManagedChecksum(batch, expectedChecksum, "warmup");
            s_sink ^= batch.Checksum;
            batches++;
        }
        while (batches < maximumBatches && Stopwatch.GetElapsedTime(started).TotalMilliseconds < 100);

        return new PythonReWarmup(
            batches * iterations,
            Stopwatch.GetElapsedTime(started));
    }

    private static PythonReManagedSample MeasureManagedSample(
        PythonReBenchmarkContext context,
        int iterations,
        int expectedChecksum,
        Process process)
    {
        var processCpuBefore = process.TotalProcessorTime;
        var collectionsBefore = new[]
        {
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
        };
        var batch = context.MeasurePythonReBatch(iterations);
        var processCpu = process.TotalProcessorTime - processCpuBefore;
        var collectionsAfter = new[]
        {
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
        };
        VerifyManagedChecksum(batch, expectedChecksum, "paired sample");
        s_sink ^= batch.Checksum;
        return new PythonReManagedSample(
            batch,
            processCpu,
            [
                collectionsAfter[0] - collectionsBefore[0],
                collectionsAfter[1] - collectionsBefore[1],
                collectionsAfter[2] - collectionsBefore[2],
            ]);
    }

    private static PythonReEmptyBatch MeasureManagedEmptyLoop(int iterations)
    {
        var checksum = 0;
        var started = Stopwatch.GetTimestamp();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            checksum ^= iteration;
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        GC.KeepAlive(checksum);
        return new PythonReEmptyBatch(elapsed, checksum);
    }

    private static void VerifyManagedChecksum(
        PythonReBenchmarkBatch batch,
        int expectedChecksum,
        string phase)
    {
        if (batch.Checksum != expectedChecksum)
        {
            throw new InvalidOperationException(
                $"PythonRe {phase} checksum {batch.Checksum} does not match preflight {expectedChecksum}.");
        }
    }

    private static PythonReStatusResult DeriveStatus(
        bool worktreeQualified,
        bool placementQualified,
        bool durationsQualified,
        double lowerRatio,
        double upperRatio,
        double orderEffect,
        double managedSpread,
        double cpythonSpread,
        double managedFloorFraction,
        double cpythonFloorFraction)
    {
        if (!worktreeQualified)
        {
            return new(PythonRePublicStatus.Unqualified, "The source worktree is not clean.");
        }

        if (!placementQualified)
        {
            return new(
                PythonRePublicStatus.Unqualified,
                "A single common highest-efficiency processor was unavailable.");
        }

        if (!durationsQualified)
        {
            return new(
                PythonRePublicStatus.Unqualified,
                $"At least one lane sample was shorter than {PythonReQualificationMinimumSampleMilliseconds:F0} ms.");
        }

        if (managedSpread > PythonReQualificationMaximumSpread ||
            cpythonSpread > PythonReQualificationMaximumSpread)
        {
            return new(
                PythonRePublicStatus.Inconclusive,
                $"Lane interquartile spreads are {managedSpread:F3}/{cpythonSpread:F3}; " +
                $"the maximum is {PythonReQualificationMaximumSpread:F2}.");
        }

        if (orderEffect is < 0.98 or > 1.02)
        {
            return new(
                PythonRePublicStatus.Inconclusive,
                $"Lane-order median ratios differ by {Math.Abs(orderEffect - 1) * 100:F2}%.");
        }

        var floorContaminated = managedFloorFraction > 0.05 || cpythonFloorFraction > 0.05;
        if (floorContaminated)
        {
            var sensitiveLower = lowerRatio * Math.Max(0, 1 - managedFloorFraction);
            var sensitiveUpper = upperRatio / Math.Max(0.001, 1 - cpythonFloorFraction);
            if (sensitiveUpper < 0.98)
            {
                return new(
                    PythonRePublicStatus.ManagedFaster,
                    "The conclusion survives the conservative harness-floor bound.");
            }

            if (sensitiveLower > 1.02)
            {
                return new(
                    PythonRePublicStatus.CpythonFaster,
                    "The conclusion survives the conservative harness-floor bound.");
            }

            return new(
                PythonRePublicStatus.Unqualified,
                $"Removable harness floors are {managedFloorFraction:P2}/{cpythonFloorFraction:P2} of the lanes.");
        }

        if (upperRatio < 0.98)
        {
            return new(PythonRePublicStatus.ManagedFaster, null);
        }

        if (lowerRatio > 1.02)
        {
            return new(PythonRePublicStatus.CpythonFaster, null);
        }

        return lowerRatio >= 0.98 && upperRatio <= 1.02
            ? new(PythonRePublicStatus.Equivalent, null)
            : new(PythonRePublicStatus.Inconclusive, "The paired 95% interval crosses a Status boundary.");
    }

    private static string FormatStatus(PythonRePublicStatus status) => status switch
    {
        PythonRePublicStatus.Unqualified => "Unqualified",
        PythonRePublicStatus.Inconclusive => "Inconclusive",
        PythonRePublicStatus.Equivalent => "Equivalent",
        PythonRePublicStatus.ManagedFaster => "Managed faster",
        PythonRePublicStatus.CpythonFaster => "CPython faster",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private sealed class CpythonStreamWorker : IDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _standardError;
        private bool _disposed;

        internal CpythonStreamWorker()
        {
            var executable = System.Environment.GetEnvironmentVariable("UTF8REGEX_CPYTHON");
            if (string.IsNullOrWhiteSpace(executable))
            {
                executable = "python";
            }

            var startInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-I");
            startInfo.ArgumentList.Add(FindRepositoryFile(CpythonRunnerRelativePath));
            startInfo.ArgumentList.Add("--stream");
            _process = Process.Start(startInfo) ??
                throw new InvalidOperationException($"Could not start CPython executable '{executable}'.");
            if (OperatingSystem.IsWindows())
            {
                using var parent = Process.GetCurrentProcess();
                _process.ProcessorAffinity = parent.ProcessorAffinity;
            }

            _standardError = _process.StandardError.ReadToEndAsync();
            try
            {
                Ready = Read("Ready");
                Environment = Ready.Environment ??
                    throw new InvalidOperationException("CPython streaming ready response has no environment.");
            }
            catch
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }

                _process.Dispose();
                throw;
            }
        }

        internal CpythonStreamResponse Ready { get; }

        internal CpythonStreamEnvironment Environment { get; }

        internal CpythonStreamResponse Prepare(PythonReBenchmarkCase benchmarkCase, byte[] inputBytes) => Send(
            new CpythonStreamCommand
            {
                ProtocolVersion = PythonReQualificationProtocolVersion,
                Kind = "Prepare",
                Pattern = benchmarkCase.Pattern,
                Options = (int)benchmarkCase.Options,
                Operation = benchmarkCase.Operation.ToString(),
                InputBase64 = Convert.ToBase64String(inputBytes),
                Replacement = benchmarkCase.Replacement,
            },
            "Prepared");

        internal CpythonStreamResponse Calibrate(
            CpythonStreamLane lane,
            double targetMilliseconds,
            int maximumIterations) => Send(
                new CpythonStreamCommand
                {
                    ProtocolVersion = PythonReQualificationProtocolVersion,
                    Kind = "Calibrate",
                    Lane = lane.ToString(),
                    TargetNanoseconds = checked((long)(targetMilliseconds * 1_000_000)),
                    MaximumIterations = maximumIterations,
                },
                "Calibrated");

        internal CpythonStreamResponse Warm(
            CpythonStreamLane lane,
            int iterations,
            int minimumMilliseconds,
            int maximumBatches) => Send(
                new CpythonStreamCommand
                {
                    ProtocolVersion = PythonReQualificationProtocolVersion,
                    Kind = "Warm",
                    Lane = lane.ToString(),
                    Iterations = iterations,
                    MinimumMilliseconds = minimumMilliseconds,
                    MaximumBatches = maximumBatches,
                },
                "Warmed");

        internal CpythonStreamResponse Measure(CpythonStreamLane lane, int iterations) => Send(
            new CpythonStreamCommand
            {
                ProtocolVersion = PythonReQualificationProtocolVersion,
                Kind = "Measure",
                Lane = lane.ToString(),
                Iterations = iterations,
            },
            "Measured");

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (!_process.HasExited)
                {
                    _ = Send(
                        new CpythonStreamCommand
                        {
                            ProtocolVersion = PythonReQualificationProtocolVersion,
                            Kind = "Shutdown",
                        },
                        "Shutdown");
                    _process.StandardInput.Close();
                    if (!_process.WaitForExit(2_000))
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
            }
            catch
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            finally
            {
                _disposed = true;
                _process.Dispose();
            }
        }

        private CpythonStreamResponse Send(CpythonStreamCommand command, string expectedKind)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CpythonStreamWorker));
            }

            _process.StandardInput.WriteLine(JsonSerializer.Serialize(command));
            _process.StandardInput.Flush();
            return Read(expectedKind);
        }

        private CpythonStreamResponse Read(string expectedKind)
        {
            string? line;
            using (var cancellation = new CancellationTokenSource(s_cpythonResponseTimeout))
            {
                try
                {
                    line = _process.StandardOutput.ReadLineAsync(cancellation.Token).AsTask().GetAwaiter().GetResult();
                }
                catch (OperationCanceledException exception)
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                    }

                    throw new TimeoutException(
                        "CPython streaming worker did not answer within " +
                        $"{s_cpythonResponseTimeout.TotalSeconds:F0} seconds.",
                        exception);
                }
            }

            if (line is null)
            {
                var error = _standardError.IsCompletedSuccessfully ? _standardError.Result.Trim() : string.Empty;
                throw new InvalidOperationException(
                    $"CPython streaming worker exited before '{expectedKind}'. {error}".Trim());
            }

            var envelope = JsonSerializer.Deserialize<CpythonStreamResponse>(line) ??
                throw new InvalidOperationException("CPython streaming worker returned invalid JSON.");
            if (envelope.ProtocolVersion != PythonReQualificationProtocolVersion)
            {
                throw new InvalidOperationException(
                    $"CPython streaming worker returned protocol {envelope.ProtocolVersion}.");
            }

            if (envelope.Kind == "Error")
            {
                throw new InvalidOperationException(
                    $"CPython streaming worker failed with {envelope.ErrorType}: {envelope.Message}");
            }

            if (!envelope.Kind.Equals(expectedKind, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"CPython streaming worker returned '{envelope.Kind}', expected '{expectedKind}'.");
            }

            return envelope;
        }
    }
}

internal enum CpythonStreamLane : byte
{
    Predecoded = 0,
    EmptyLoop = 1,
}

internal enum PythonRePairLaneOrder : byte
{
    ManagedFirst = 0,
    CpythonFirst = 1,
}

internal enum PythonRePublicStatus : byte
{
    Unqualified = 0,
    Inconclusive = 1,
    Equivalent = 2,
    ManagedFaster = 3,
    CpythonFaster = 4,
}

internal readonly record struct PythonReManagedSample(
    PythonReBenchmarkBatch Batch,
    TimeSpan ProcessCpu,
    int[] GcCollections);

internal readonly record struct PythonReEmptyBatch(TimeSpan Elapsed, int Checksum);

internal readonly record struct PythonReWarmup(int Iterations, TimeSpan Elapsed);

internal readonly record struct PythonReOrderedRatio(PythonRePairLaneOrder Order, double Ratio);

internal readonly record struct PythonReStatusResult(PythonRePublicStatus Status, string? Reason);

internal sealed class CpythonStreamCommand
{
    public required int ProtocolVersion { get; init; }
    public required string Kind { get; init; }
    public string? Pattern { get; init; }
    public int? Options { get; init; }
    public string? Operation { get; init; }
    public string? InputBase64 { get; init; }
    public string? Replacement { get; init; }
    public string? Lane { get; init; }
    public int? Iterations { get; init; }
    public long? TargetNanoseconds { get; init; }
    public int? MaximumIterations { get; init; }
    public int? MinimumMilliseconds { get; init; }
    public int? MaximumBatches { get; init; }
}

internal sealed class CpythonStreamResponse
{
    public int ProtocolVersion { get; init; }
    public string Kind { get; init; } = string.Empty;
    public CpythonStreamEnvironment? Environment { get; init; }
    public string? Lane { get; init; }
    public int Iterations { get; init; }
    public int Batches { get; init; }
    public long ElapsedNanoseconds { get; init; }
    public long ProcessCpuNanoseconds { get; init; }
    public int Checksum { get; init; }
    public int[] GcCollections { get; init; } = [];
    public string? ErrorType { get; init; }
    public string? Message { get; init; }
}

internal sealed class CpythonStreamEnvironment
{
    public string Implementation { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string VersionDetail { get; init; } = string.Empty;
    public long HexVersion { get; init; }
    public string[] Git { get; init; } = [];
    public string CacheTag { get; init; } = string.Empty;
    public string Compiler { get; init; } = string.Empty;
    public string SoAbi { get; init; } = string.Empty;
    public bool DebugBuild { get; init; }
    public bool? GilEnabled { get; init; }
    public string Executable { get; init; } = string.Empty;
    public string ExecutableSha256 { get; init; } = string.Empty;
    public string RuntimeLibrary { get; init; } = string.Empty;
    public string RuntimeLibrarySha256 { get; init; } = string.Empty;
    public string Platform { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string RunnerSha256 { get; init; } = string.Empty;
    public CpythonStreamTimer Timer { get; init; } = new();
}

internal sealed class CpythonStreamTimer
{
    public string Implementation { get; init; } = string.Empty;
    public double ResolutionSeconds { get; init; }
    public bool Monotonic { get; init; }
    public bool Adjustable { get; init; }
}
