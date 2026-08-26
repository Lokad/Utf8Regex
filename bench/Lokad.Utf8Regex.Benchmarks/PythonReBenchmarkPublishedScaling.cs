using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const int PythonRePublishedScalingSamples = 5;

    private static int VerifyPythonReScaling()
    {
        var definitions = CreatePythonReScalingFamilies();
        var snapshot = LoadPythonReBenchmarkSnapshot();
        var expectedIds = definitions.Select(static definition => definition.Id).ToArray();
        var actualIds = snapshot.ScalingFamilies.Keys.ToArray();
        if (!actualIds.SequenceEqual(expectedIds.OrderBy(static id => id, StringComparer.Ordinal)))
        {
            Console.Error.WriteLine(
                "PythonRe scaling families are incomplete or stale. Expected: " +
                string.Join(", ", expectedIds) + "; actual: " + string.Join(", ", actualIds) + ".");
            return 1;
        }

        foreach (var definition in definitions)
        {
            var family = snapshot.ScalingFamilies[definition.Id];
            VerifyPublishedScalingFamily(definition, family);
        }

        Console.WriteLine(
            $"PythonRe scaling evidence is current: {definitions.Length} families, " +
            $"{definitions.Sum(static definition => definition.Points.Length)} points.");
        return 0;
    }

    private static void VerifyPublishedScalingFamily(
        PythonReScalingFamilyDefinition definition,
        PythonReScalingFamilyMeasurement family)
    {
        if (!family.Dimension.Equals(definition.Dimension, StringComparison.Ordinal) ||
            !family.Operation.Equals(definition.Points[0].BenchmarkCase.Operation.ToString(), StringComparison.Ordinal) ||
            !family.ResultContract.Equals(
                GetPythonReResultContract(definition.Points[0].BenchmarkCase),
                StringComparison.Ordinal) ||
            family.Samples is < 5 or > 9 ||
            family.Points.Count != definition.Points.Length)
        {
            throw new InvalidOperationException(
                $"PythonRe scaling family '{definition.Id}' has stale definition metadata.");
        }

        if (family.ManagedEnvironment.TrackedDirty ||
            family.ManagedEnvironment.HasUntrackedFiles ||
            !IsPythonReCommitId(family.ManagedEnvironment.SourceCommit) ||
            string.IsNullOrWhiteSpace(family.ManagedEnvironment.Runtime) ||
            string.IsNullOrWhiteSpace(family.ManagedEnvironment.OperatingSystem) ||
            string.IsNullOrWhiteSpace(family.ManagedEnvironment.Processor) ||
            !family.CpythonEnvironment.Implementation.Equals("CPython", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(family.CpythonEnvironment.Version) ||
            !IsPythonReSha256(family.CpythonEnvironment.ExecutableSha256) ||
            !IsPythonReSha256(family.CpythonEnvironment.RunnerSha256) ||
            !IsPythonReQualifiedProcessorPolicy(family.CpuPolicy) ||
            string.IsNullOrWhiteSpace(family.CpuAffinityMask))
        {
            throw new InvalidOperationException(
                $"PythonRe scaling family '{definition.Id}' lacks clean, exact runtime provenance.");
        }

        for (var index = 0; index < definition.Points.Length; index++)
        {
            var definitionPoint = definition.Points[index];
            var point = family.Points[index];
            var context = new PythonReBenchmarkContext(definitionPoint.BenchmarkCase);
            if (!point.Label.Equals(definitionPoint.Label, StringComparison.Ordinal) ||
                point.Scale != definitionPoint.Scale ||
                point.WorkUnits != definitionPoint.WorkUnits ||
                point.OutputUtf8Bytes != definitionPoint.OutputUtf8Bytes ||
                point.InputUtf8Bytes != context.InputBytes.Length ||
                !point.InputSha256.Equals(
                    Convert.ToHexString(SHA256.HashData(context.InputBytes)),
                    StringComparison.Ordinal) ||
                !point.SemanticDigest.Equals(
                    context.ExecutePythonReSemanticDigest().ToString("X16"),
                    StringComparison.Ordinal) ||
                !point.ManagedRoute.Equals(context.DescribeManagedRoute(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"PythonRe scaling point '{definition.Id}/{definitionPoint.Label}' is stale.");
            }

            if (point.ManagedIterations <= 0 ||
                point.CpythonIterations <= 0 ||
                !IsPositiveFinite(point.ManagedMedianMicroseconds) ||
                !IsPositiveFinite(point.CpythonMedianMicroseconds) ||
                !IsPositiveFinite(point.RatioMedian) ||
                !IsPositiveFinite(point.RatioLower95) ||
                !IsPositiveFinite(point.RatioUpper95) ||
                point.RatioLower95 > point.RatioMedian ||
                point.RatioMedian > point.RatioUpper95 ||
                !IsPositiveFinite(point.ManagedSpread) ||
                !IsPositiveFinite(point.CpythonSpread) ||
                point.ManagedAllocatedBytes < 0 ||
                point.ManagedWarmupCalls <= 0 ||
                !IsPositiveFinite(point.ManagedWarmupMilliseconds) ||
                point.CpythonWarmupCalls <= 0 ||
                !IsPositiveFinite(point.CpythonWarmupMilliseconds) ||
                !IsPositiveFinite(point.OrderEffect) ||
                point.Samples.Count != family.Samples)
            {
                throw new InvalidOperationException(
                    $"PythonRe scaling point '{definition.Id}/{definitionPoint.Label}' has invalid statistics.");
            }

            var managedSamples = new double[family.Samples];
            var cpythonSamples = new double[family.Samples];
            var ratios = new double[family.Samples];
            var allocations = new double[family.Samples];
            for (var sampleIndex = 0; sampleIndex < family.Samples; sampleIndex++)
            {
                var sample = point.Samples[sampleIndex];
                if (sample.Order is not nameof(PythonRePairLaneOrder.ManagedFirst) and
                    not nameof(PythonRePairLaneOrder.CpythonFirst) ||
                    (sampleIndex > 0 && sample.Order.Equals(
                        point.Samples[sampleIndex - 1].Order,
                        StringComparison.Ordinal)) ||
                    !IsPositiveFinite(sample.ManagedMicroseconds) ||
                    !IsPositiveFinite(sample.CpythonMicroseconds) ||
                    !IsPositiveFinite(sample.Ratio) ||
                    !IsPositiveFinite(sample.ManagedElapsedMilliseconds) ||
                    !IsPositiveFinite(sample.CpythonElapsedMilliseconds) ||
                    sample.ManagedAllocatedBytes < 0 ||
                    sample.ManagedGcCollections.Length != 3 ||
                    sample.CpythonGcCollections.Length != 3 ||
                    sample.ManagedGcCollections.Any(static count => count < 0) ||
                    sample.CpythonGcCollections.Any(static count => count < 0))
                {
                    throw new InvalidOperationException(
                        $"PythonRe scaling sample '{definition.Id}/{definitionPoint.Label}/{sampleIndex}' is invalid.");
                }

                VerifyPythonReStatistic(
                    definition.Id + " sample managed elapsed",
                    sample.ManagedMicroseconds * point.ManagedIterations / 1_000,
                    sample.ManagedElapsedMilliseconds);
                VerifyPythonReStatistic(
                    definition.Id + " sample CPython elapsed",
                    sample.CpythonMicroseconds * point.CpythonIterations / 1_000,
                    sample.CpythonElapsedMilliseconds);
                VerifyPythonReStatistic(
                    definition.Id + " sample ratio",
                    sample.ManagedMicroseconds / sample.CpythonMicroseconds,
                    sample.Ratio);
                managedSamples[sampleIndex] = sample.ManagedMicroseconds;
                cpythonSamples[sampleIndex] = sample.CpythonMicroseconds;
                ratios[sampleIndex] = sample.Ratio;
                allocations[sampleIndex] = sample.ManagedAllocatedBytes;
            }

            var interval = BenchmarkPairedStatistics.BootstrapMedianLogRatio(
                ratios.Select(static ratio => Math.Log(ratio)).ToArray(),
                PythonReQualificationBootstrapSeed,
                PythonReQualificationBootstrapResamples);
            var managedFirstRatios = point.Samples
                .Where(static sample => sample.Order.Equals(
                    nameof(PythonRePairLaneOrder.ManagedFirst),
                    StringComparison.Ordinal))
                .Select(static sample => sample.Ratio)
                .ToArray();
            var cpythonFirstRatios = point.Samples
                .Where(static sample => sample.Order.Equals(
                    nameof(PythonRePairLaneOrder.CpythonFirst),
                    StringComparison.Ordinal))
                .Select(static sample => sample.Ratio)
                .ToArray();
            VerifyPythonReStatistic(
                definition.Id + " managed median",
                BenchmarkPairedStatistics.Median(managedSamples),
                point.ManagedMedianMicroseconds);
            VerifyPythonReStatistic(
                definition.Id + " CPython median",
                BenchmarkPairedStatistics.Median(cpythonSamples),
                point.CpythonMedianMicroseconds);
            VerifyPythonReStatistic(
                definition.Id + " ratio median",
                Math.Exp(BenchmarkPairedStatistics.Median(
                    ratios.Select(static ratio => Math.Log(ratio)))),
                point.RatioMedian);
            VerifyPythonReStatistic(
                definition.Id + " ratio lower",
                Math.Exp(interval.Lower),
                point.RatioLower95);
            VerifyPythonReStatistic(
                definition.Id + " ratio upper",
                Math.Exp(interval.Upper),
                point.RatioUpper95);
            VerifyPythonReStatistic(
                definition.Id + " managed spread",
                BenchmarkPairedStatistics.InterquartileSpread(managedSamples),
                point.ManagedSpread);
            VerifyPythonReStatistic(
                definition.Id + " CPython spread",
                BenchmarkPairedStatistics.InterquartileSpread(cpythonSamples),
                point.CpythonSpread);
            VerifyPythonReStatistic(
                definition.Id + " managed allocation",
                BenchmarkPairedStatistics.Median(allocations),
                point.ManagedAllocatedBytes);
            VerifyPythonReStatistic(
                definition.Id + " order effect",
                BenchmarkPairedStatistics.Median(managedFirstRatios) /
                    BenchmarkPairedStatistics.Median(cpythonFirstRatios),
                point.OrderEffect);
        }

        var routes = family.Points.Select(static point => point.ManagedRoute)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var routeStable = routes.Length == 1;
        var managedFit = FitPublishedScaling(
            family.Points,
            static point => point.ManagedMedianMicroseconds,
            static point => point.ManagedSpread);
        var cpythonFit = FitPublishedScaling(
            family.Points,
            static point => point.CpythonMedianMicroseconds,
            static point => point.CpythonSpread);
        var maximumOrderEffect = family.Points.Max(static point =>
            Math.Max(point.OrderEffect, 1 / point.OrderEffect));
        var minimumLaneElapsedMilliseconds = family.Points
            .SelectMany(static point => point.Samples)
            .Min(static sample => Math.Min(
                sample.ManagedElapsedMilliseconds,
                sample.CpythonElapsedMilliseconds));
        var gatePassed = routeStable &&
            managedFit.MaximumRelativeResidual <= 0.25 &&
            cpythonFit.MaximumRelativeResidual <= 0.25 &&
            managedFit.MaximumSpread <= 1.10 &&
            cpythonFit.MaximumSpread <= 1.10 &&
            maximumOrderEffect <= 1.10 &&
            minimumLaneElapsedMilliseconds >= 5;
        VerifyPythonReStatistic(
            definition.Id + " managed scaling slope",
            managedFit.Slope,
            family.ManagedSlopePerScaleUnit);
        VerifyPythonReStatistic(
            definition.Id + " CPython scaling slope",
            cpythonFit.Slope,
            family.CpythonSlopePerScaleUnit);
        VerifyPythonReStatistic(
            definition.Id + " managed scaling residual",
            managedFit.MaximumRelativeResidual,
            family.ManagedMaximumRelativeResidual);
        VerifyPythonReStatistic(
            definition.Id + " CPython scaling residual",
            cpythonFit.MaximumRelativeResidual,
            family.CpythonMaximumRelativeResidual);
        VerifyPythonReStatistic(
            definition.Id + " managed scaling spread",
            managedFit.MaximumSpread,
            family.ManagedMaximumSpread);
        VerifyPythonReStatistic(
            definition.Id + " CPython scaling spread",
            cpythonFit.MaximumSpread,
            family.CpythonMaximumSpread);
        VerifyPythonReStatistic(
            definition.Id + " maximum order effect",
            maximumOrderEffect,
            family.MaximumOrderEffect);
        VerifyPythonReStatistic(
            definition.Id + " minimum lane duration",
            minimumLaneElapsedMilliseconds,
            family.MinimumLaneElapsedMilliseconds);
        if (family.RouteStable != routeStable ||
            !family.ManagedRoute.Equals(string.Join(" | ", routes), StringComparison.Ordinal) ||
            !family.FitGate.Equals(gatePassed ? "Pass" : "Reject", StringComparison.Ordinal) ||
            !family.FitGateReason.Equals(
                DescribePythonReScalingGate(
                    routeStable,
                    managedFit,
                    cpythonFit,
                    maximumOrderEffect,
                    minimumLaneElapsedMilliseconds),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"PythonRe scaling family '{definition.Id}' has stale fit-gate metadata.");
        }

        static bool IsPositiveFinite(double value) => value > 0 && double.IsFinite(value);
    }

    private static bool IsPythonReSha256(string value) =>
        value.Length == 64 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static bool IsPythonReCommitId(string value) =>
        value.Length is >= 12 and <= 64 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static int RefreshPythonReScaling(int samples, string? requestedFamily)
    {
#if DEBUG
        Console.Error.WriteLine("PythonRe scaling refresh requires a Release build.");
        return 1;
#else
        var definitions = CreatePythonReScalingFamilies();
        if (requestedFamily is not null &&
            definitions.All(definition => !definition.Id.Equals(requestedFamily, StringComparison.Ordinal)))
        {
            Console.Error.WriteLine($"Unknown PythonRe scaling family '{requestedFamily}'.");
            return 1;
        }

        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        using var worker = new CpythonStreamWorker();
        var snapshot = LoadPythonReBenchmarkSnapshot();
        var measurements = requestedFamily is null
            ? new SortedDictionary<string, PythonReScalingFamilyMeasurement>(StringComparer.Ordinal)
            : new SortedDictionary<string, PythonReScalingFamilyMeasurement>(
                snapshot.ScalingFamilies,
                StringComparer.Ordinal);
        foreach (var indexedDefinition in definitions
                     .Select(static (definition, index) => (Definition: definition, Index: index))
                     .Where(item => requestedFamily is null ||
                         item.Definition.Id.Equals(requestedFamily, StringComparison.Ordinal)))
        {
            var definition = indexedDefinition.Definition;
            Console.WriteLine($"Scaling family     : {definition.Id}");
            measurements[definition.Id] = MeasurePythonReScalingFamily(
                definition,
                samples,
                worker,
                processorScope,
                indexedDefinition.Index);
        }

        WriteSnapshot(new PythonReBenchmarkSnapshot
        {
            SchemaVersion = PythonReBenchmarkSchemaVersion,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            CatalogSha256 = snapshot.CatalogSha256,
            CatalogCaseIds = snapshot.CatalogCaseIds,
            Corpus = snapshot.Corpus,
            Cases = snapshot.Cases,
            Lifecycle = snapshot.Lifecycle,
            ScalingFamilies = measurements,
        });
        Console.WriteLine(
            $"Updated {measurements.Count} PythonRe scaling families on {processorScope.Policy}.");
        return 0;
#endif
    }

    private static PythonReScalingFamilyMeasurement MeasurePythonReScalingFamily(
        PythonReScalingFamilyDefinition definition,
        int samples,
        CpythonStreamWorker worker,
        BenchmarkProcessorScope processorScope,
        int familyIndex)
    {
        List<PythonReScalingPointMeasurement> points = [];
        for (var pointIndex = 0; pointIndex < definition.Points.Length; pointIndex++)
        {
            points.Add(MeasurePythonReScalingPoint(
                definition.Points[pointIndex],
                samples,
                worker,
                cpythonFirst: ((familyIndex + pointIndex) & 1) != 0));
        }

        var routes = points.Select(static point => point.ManagedRoute)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var managedFit = FitPublishedScaling(
            points,
            static point => point.ManagedMedianMicroseconds,
            static point => point.ManagedSpread);
        var cpythonFit = FitPublishedScaling(
            points,
            static point => point.CpythonMedianMicroseconds,
            static point => point.CpythonSpread);
        var routeStable = routes.Length == 1;
        var maximumOrderEffect = points.Max(static point =>
            Math.Max(point.OrderEffect, 1 / point.OrderEffect));
        var minimumLaneElapsedMilliseconds = points
            .SelectMany(static point => point.Samples)
            .Min(static sample => Math.Min(
                sample.ManagedElapsedMilliseconds,
                sample.CpythonElapsedMilliseconds));
        var fitGatePassed = routeStable &&
            managedFit.MaximumRelativeResidual <= 0.25 &&
            cpythonFit.MaximumRelativeResidual <= 0.25 &&
            managedFit.MaximumSpread <= 1.10 &&
            cpythonFit.MaximumSpread <= 1.10 &&
            maximumOrderEffect <= 1.10 &&
            minimumLaneElapsedMilliseconds >= 5;
        return new PythonReScalingFamilyMeasurement
        {
            Dimension = definition.Dimension,
            Operation = definition.Points[0].BenchmarkCase.Operation.ToString(),
            ResultContract = GetPythonReResultContract(definition.Points[0].BenchmarkCase),
            Samples = samples,
            MeasuredAtUtc = DateTimeOffset.UtcNow,
            ManagedEnvironment = CaptureEnvironment(),
            CpythonEnvironment = worker.Environment,
            CpuPolicy = processorScope.Policy,
            CpuAffinityMask = processorScope.AffinityMask,
            CpuEfficiencyClass = processorScope.EfficiencyClass,
            ManagedRoute = string.Join(" | ", routes),
            RouteStable = routeStable,
            ManagedSlopePerScaleUnit = managedFit.Slope,
            CpythonSlopePerScaleUnit = cpythonFit.Slope,
            ManagedMaximumRelativeResidual = managedFit.MaximumRelativeResidual,
            CpythonMaximumRelativeResidual = cpythonFit.MaximumRelativeResidual,
            ManagedMaximumSpread = managedFit.MaximumSpread,
            CpythonMaximumSpread = cpythonFit.MaximumSpread,
            MaximumOrderEffect = maximumOrderEffect,
            MinimumLaneElapsedMilliseconds = minimumLaneElapsedMilliseconds,
            FitGate = fitGatePassed ? "Pass" : "Reject",
            FitGateReason = DescribePythonReScalingGate(
                routeStable,
                managedFit,
                cpythonFit,
                maximumOrderEffect,
                minimumLaneElapsedMilliseconds),
            Points = points,
        };
    }

    private static PythonReScalingPointMeasurement MeasurePythonReScalingPoint(
        PythonReScalingPointDefinition definition,
        int samples,
        CpythonStreamWorker worker,
        bool cpythonFirst)
    {
        var benchmarkCase = definition.BenchmarkCase;
        var context = new PythonReBenchmarkContext(benchmarkCase);
        var expectedChecksum = context.ExecutePythonRe();
        var expectedSemanticDigest = context.ExecutePythonReSemanticDigest();
        var expectedConsumptionToken = context.ExecutePythonReConsumptionToken();
        var prepared = worker.Prepare(benchmarkCase, context.InputBytes, enableByteControl: false);
        if (prepared.Checksum != expectedChecksum ||
            prepared.SemanticDigest != expectedSemanticDigest ||
            prepared.ConsumptionChecksum != expectedConsumptionToken)
        {
            throw new InvalidOperationException(
                $"PythonRe scaling '{benchmarkCase.Id}' failed its CPython structured preflight.");
        }

        var managedIterations = CalibrateOneShotManagedIterations(
            context,
            minimumIterations: 1,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken);
        var cpythonIterations = worker.Calibrate(
            CpythonStreamLane.Predecoded,
            PythonReScalingTargetSampleMilliseconds,
            PythonReScalingMaximumIterations).Iterations;
        var managedWarmup = WarmOneShotManaged(
            context,
            managedIterations,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken);
        var cpythonWarmup = worker.Warm(
            CpythonStreamLane.Predecoded,
            cpythonIterations,
            minimumMilliseconds: 20,
            minimumCalls: 1_024,
            maximumBatches: 8);

        var managed = new double[samples];
        var cpython = new double[samples];
        var ratios = new double[samples];
        var allocations = new long[samples];
        var managedMilliseconds = new double[samples];
        var cpythonMilliseconds = new double[samples];
        var managedGcCollections = new int[samples][];
        var cpythonGcCollections = new int[samples][];
        var laneOrders = new PythonRePairLaneOrder[samples];
        for (var sample = 0; sample < samples; sample++)
        {
            var cpythonRunsFirst = cpythonFirst ^ (sample % 2 != 0);
            laneOrders[sample] = cpythonRunsFirst
                ? PythonRePairLaneOrder.CpythonFirst
                : PythonRePairLaneOrder.ManagedFirst;
            if (!cpythonRunsFirst)
            {
                MeasureManaged();
                MeasureCpython();
            }
            else
            {
                MeasureCpython();
                MeasureManaged();
            }

            ratios[sample] = managed[sample] / cpython[sample];

            void MeasureManaged()
            {
                var gcBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
                var batch = context.MeasurePythonReQualificationBatch(managedIterations);
                managedGcCollections[sample] = Enumerable.Range(0, 3)
                    .Select(generation => GC.CollectionCount(generation) - gcBefore[generation])
                    .ToArray();
                VerifyManagedResult(
                    batch,
                    expectedChecksum,
                    expectedSemanticDigest,
                    expectedConsumptionToken,
                    managedIterations,
                    "published scaling");
                managed[sample] = batch.Elapsed.TotalMicroseconds / managedIterations;
                managedMilliseconds[sample] = batch.Elapsed.TotalMilliseconds;
                allocations[sample] = batch.AllocatedBytes / managedIterations;
            }

            void MeasureCpython()
            {
                var response = worker.Measure(CpythonStreamLane.Predecoded, cpythonIterations);
                cpython[sample] = response.ElapsedNanoseconds / (double)cpythonIterations / 1_000;
                cpythonMilliseconds[sample] = response.ElapsedNanoseconds / 1_000_000d;
                cpythonGcCollections[sample] = response.GcCollections;
            }
        }

        var interval = BenchmarkPairedStatistics.BootstrapMedianLogRatio(
            ratios.Select(static ratio => Math.Log(ratio)).ToArray(),
            PythonReQualificationBootstrapSeed,
            PythonReQualificationBootstrapResamples);
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
        return new PythonReScalingPointMeasurement
        {
            Label = definition.Label,
            Scale = definition.Scale,
            WorkUnits = definition.WorkUnits,
            OutputUtf8Bytes = definition.OutputUtf8Bytes,
            InputUtf8Bytes = context.InputBytes.Length,
            InputSha256 = Convert.ToHexString(SHA256.HashData(context.InputBytes)),
            SemanticDigest = expectedSemanticDigest.ToString("X16"),
            ManagedRoute = context.DescribeManagedRoute(),
            ManagedIterations = managedIterations,
            CpythonIterations = cpythonIterations,
            ManagedMedianMicroseconds = BenchmarkPairedStatistics.Median(managed),
            CpythonMedianMicroseconds = BenchmarkPairedStatistics.Median(cpython),
            RatioMedian = Math.Exp(BenchmarkPairedStatistics.Median(
                ratios.Select(static ratio => Math.Log(ratio)))),
            RatioLower95 = Math.Exp(interval.Lower),
            RatioUpper95 = Math.Exp(interval.Upper),
            ManagedSpread = BenchmarkPairedStatistics.InterquartileSpread(managed),
            CpythonSpread = BenchmarkPairedStatistics.InterquartileSpread(cpython),
            ManagedAllocatedBytes = (long)Math.Round(BenchmarkPairedStatistics.Median(
                allocations.Select(static allocation => (double)allocation))),
            ManagedWarmupCalls = managedWarmup.Iterations,
            ManagedWarmupMilliseconds = managedWarmup.Elapsed.TotalMilliseconds,
            CpythonWarmupCalls = cpythonWarmup.Iterations,
            CpythonWarmupMilliseconds = cpythonWarmup.ElapsedNanoseconds / 1_000_000d,
            OrderEffect = orderEffect,
            Samples = Enumerable.Range(0, samples)
                .Select(index => new PythonReScalingSampleMeasurement
                {
                    Order = laneOrders[index].ToString(),
                    ManagedMicroseconds = managed[index],
                    CpythonMicroseconds = cpython[index],
                    Ratio = ratios[index],
                    ManagedElapsedMilliseconds = managedMilliseconds[index],
                    CpythonElapsedMilliseconds = cpythonMilliseconds[index],
                    ManagedAllocatedBytes = allocations[index],
                    ManagedGcCollections = managedGcCollections[index],
                    CpythonGcCollections = cpythonGcCollections[index],
                })
                .ToList(),
        };
    }

    private static PythonRePublishedScalingFit FitPublishedScaling(
        IReadOnlyList<PythonReScalingPointMeasurement> points,
        Func<PythonReScalingPointMeasurement, double> selector,
        Func<PythonReScalingPointMeasurement, double> spreadSelector)
    {
        List<double> slopes = [];
        for (var left = 0; left < points.Count; left++)
        {
            for (var right = left + 1; right < points.Count; right++)
            {
                slopes.Add(
                    (selector(points[right]) - selector(points[left])) /
                    (points[right].Scale - points[left].Scale));
            }
        }

        var slope = BenchmarkPairedStatistics.Median(slopes);
        var intercept = BenchmarkPairedStatistics.Median(points.Select(
            point => selector(point) - slope * point.Scale));
        var maximumRelativeResidual = points.Max(point =>
        {
            var observed = selector(point);
            var predicted = intercept + slope * point.Scale;
            return Math.Abs(observed - predicted) / Math.Max(observed, 0.000_001);
        });
        return new PythonRePublishedScalingFit(
            slope,
            maximumRelativeResidual,
            points.Max(spreadSelector));
    }

    private static string DescribePythonReScalingGate(
        bool routeStable,
        PythonRePublishedScalingFit managed,
        PythonRePublishedScalingFit cpython,
        double maximumOrderEffect,
        double minimumLaneElapsedMilliseconds)
    {
        List<string> failures = [];
        if (!routeStable)
        {
            failures.Add("managed route changed across points");
        }

        if (managed.MaximumRelativeResidual > 0.25 || cpython.MaximumRelativeResidual > 0.25)
        {
            failures.Add(
                $"maximum relative residual is {managed.MaximumRelativeResidual:P1}/{cpython.MaximumRelativeResidual:P1}");
        }

        if (managed.MaximumSpread > 1.10 || cpython.MaximumSpread > 1.10)
        {
            failures.Add($"maximum spread is {managed.MaximumSpread:F3}/{cpython.MaximumSpread:F3}");
        }

        if (maximumOrderEffect > 1.10)
        {
            failures.Add($"maximum symmetric order effect is {maximumOrderEffect:F3}");
        }

        if (minimumLaneElapsedMilliseconds < 5)
        {
            failures.Add($"minimum lane duration is {minimumLaneElapsedMilliseconds:F3} ms");
        }

        return failures.Count == 0
            ? "Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms."
            : string.Join("; ", failures) + ".";
    }

    private static PythonReScalingFamilyDefinition[] CreatePythonReScalingFamilies() =>
    [
        new("input-length", "UTF-8 input bytes", CreateInputLengthScalingPoints()),
        new("candidate-position", "candidate byte position", CreateCandidatePositionScalingPoints()),
        new("match-count", "discovered match count", CreateMatchCountScalingPoints()),
        new("capture-count", "capture group count", CreateCaptureCountScalingPoints()),
        new("output-growth", "replacement output UTF-8 bytes", CreateOutputGrowthScalingPoints()),
        new("zero-width-progression", "zero-width-aware result count", CreateZeroWidthScalingPoints()),
        new("unicode-coordinate-density", "UTF-8 bytes per scalar", CreateUnicodeDensityScalingPoints()),
    ];

    private static PythonReScalingPointDefinition[] CreateInputLengthScalingPoints() =>
        new[] { 64, 1_024, 16_384, 65_536 }
            .Select(size => ScalingPoint(
                $"{size} B",
                size,
                workUnits: 0,
                outputUtf8Bytes: 0,
                "needle",
                PythonReBenchmarkOperation.Search,
                new string('x', size),
                string.Empty,
                includesResultMaterialization: false,
                "Input length"))
            .ToArray();

    private static PythonReScalingPointDefinition[] CreateCandidatePositionScalingPoints()
    {
        const int inputSize = 65_536;
        return new[] { 0, 16_384, 32_768, inputSize - 6 }
            .Select(position => ScalingPoint(
                $"byte {position}",
                position,
                workUnits: 1,
                outputUtf8Bytes: 0,
                "needle",
                PythonReBenchmarkOperation.Search,
                new string('x', position) + "needle" + new string('x', inputSize - position - 6),
                string.Empty,
                includesResultMaterialization: false,
                "Candidate position"))
            .ToArray();
    }

    private static PythonReScalingPointDefinition[] CreateMatchCountScalingPoints() =>
        new[] { 0, 16, 64, 256 }
            .Select(count => ScalingPoint(
                $"{count} matches",
                count,
                count,
                outputUtf8Bytes: 0,
                "z",
                PythonReBenchmarkOperation.Count,
                BuildFixedLengthMatchCountSubject(65_536, count),
                string.Empty,
                includesResultMaterialization: false,
                "Match count"))
            .ToArray();

    private static PythonReScalingPointDefinition[] CreateCaptureCountScalingPoints() =>
        new[] { 0, 2, 4, 8 }
            .Select(count => ScalingPoint(
                $"{count} captures",
                count,
                count + 1,
                outputUtf8Bytes: 0,
                BuildCaptureCountPattern(count),
                PythonReBenchmarkOperation.SearchDetailed,
                "ABCDEFGH",
                string.Empty,
                includesResultMaterialization: true,
                "Capture count"))
            .ToArray();

    private static PythonReScalingPointDefinition[] CreateOutputGrowthScalingPoints() =>
        new[] { 0, 4, 16, 64 }
            .Select(replacementLength =>
            {
                var outputBytes = 256 * (replacementLength + 1);
                return ScalingPoint(
                    $"{outputBytes} B output",
                    outputBytes,
                    workUnits: 256,
                    outputBytes,
                    "x",
                    PythonReBenchmarkOperation.ReplaceString,
                    string.Concat(Enumerable.Repeat("x,", 256)),
                    new string('r', replacementLength),
                    includesResultMaterialization: true,
                    "Output growth");
            })
            .ToArray();

    private static PythonReScalingPointDefinition[] CreateZeroWidthScalingPoints() =>
        new[] { 16, 64, 256, 1_024 }
            .Select(tokenCount => ScalingPoint(
                $"{tokenCount * 3} results",
                tokenCount * 3,
                tokenCount * 3,
                outputUtf8Bytes: 0,
                @"\b|\w+",
                PythonReBenchmarkOperation.FindAllStrings,
                string.Concat(Enumerable.Repeat("y ", tokenCount)),
                string.Empty,
                includesResultMaterialization: true,
                "Zero-width progression"))
            .ToArray();

    private static PythonReScalingPointDefinition[] CreateUnicodeDensityScalingPoints()
    {
        const string suffix = "needle";
        return
        [
            ScalingPoint("1-byte scalar", 1, 1, 0, "(?P<target>needle)",
                PythonReBenchmarkOperation.SearchDetailed, new string('x', 65_536) + suffix,
                string.Empty, true, "Unicode coordinate density"),
            ScalingPoint("2-byte scalar", 2, 1, 0, "(?P<target>needle)",
                PythonReBenchmarkOperation.SearchDetailed, new string('Ж', 32_768) + suffix,
                string.Empty, true, "Unicode coordinate density"),
            ScalingPoint("3-byte scalar", 3, 1, 0, "(?P<target>needle)",
                PythonReBenchmarkOperation.SearchDetailed, new string('東', 21_845) + "x" + suffix,
                string.Empty, true, "Unicode coordinate density"),
            ScalingPoint("4-byte scalar", 4, 1, 0, "(?P<target>needle)",
                PythonReBenchmarkOperation.SearchDetailed,
                string.Concat(Enumerable.Repeat("😀", 16_384)) + suffix,
                string.Empty, true, "Unicode coordinate density"),
        ];
    }

    private static PythonReScalingPointDefinition ScalingPoint(
        string label,
        int scale,
        int workUnits,
        int outputUtf8Bytes,
        string pattern,
        PythonReBenchmarkOperation operation,
        string input,
        string replacement,
        bool includesResultMaterialization,
        string featureFamily)
    {
        var id = $"scaling/{featureFamily.ToLowerInvariant().Replace(' ', '-')}/{scale}";
        var coverage = new PythonReBenchmarkCoverage(
            "Scaling evidence",
            featureFamily,
            label,
            workUnits.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StartOffsetInBytes: 0,
            ReplacementCount: -1,
            MaxSplit: -1,
            includesResultMaterialization ? "EagerMaterializedResult" : "ScalarOrRanges",
            "CPython scaling worker",
            "OperationExcluded",
            "Measured",
            "Generated bounded scaling subject",
            "Scaling",
            FirstMilestoneSentinel: false);
        return new PythonReScalingPointDefinition(
            label,
            scale,
            workUnits,
            outputUtf8Bytes,
            new PythonReBenchmarkCase(
                id,
                pattern,
                PythonReCompileOptions.None,
                operation,
                input,
                replacement,
                includesResultMaterialization,
                coverage));
    }

    private static string BuildFixedLengthMatchCountSubject(int size, int count)
    {
        var characters = new string('x', size).ToCharArray();
        for (var index = 0; index < count; index++)
        {
            characters[(index + 1) * size / (count + 1)] = 'z';
        }

        return new string(characters);
    }

    private static string BuildCaptureCountPattern(int captureCount)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < 8; index++)
        {
            builder.Append(index < captureCount ? "([A-Z])" : "(?:[A-Z])");
        }

        return builder.ToString();
    }
}

internal readonly record struct PythonReScalingFamilyDefinition(
    string Id,
    string Dimension,
    PythonReScalingPointDefinition[] Points);

internal readonly record struct PythonReScalingPointDefinition(
    string Label,
    int Scale,
    int WorkUnits,
    int OutputUtf8Bytes,
    PythonReBenchmarkCase BenchmarkCase);

internal readonly record struct PythonRePublishedScalingFit(
    double Slope,
    double MaximumRelativeResidual,
    double MaximumSpread);
