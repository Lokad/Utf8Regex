namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private const double Pcre2FastLaneMaximumMedianMicroseconds = 10;
    private const double Pcre2FastLaneMaximumInterquartileSpread = 1.10;

    public static int RunVerifyPcre2QualificationConsistency()
    {
        var snapshot = LoadPcre2BenchmarkSnapshot();
        var errors = new List<string>();
        var pairedRows = snapshot.Sections
            .SelectMany(static section => section.Value.Cases.Select(row => new
            {
                Section = section.Key,
                CaseId = row.Key,
                Pair = row.Value.PcreNetNativePair,
            }))
            .Where(static row => row.Pair is not null)
            .ToArray();
        var fastRows = 0;
        var maximumFastSpread = 1.0;

        foreach (var row in pairedRows)
        {
            var pair = row.Pair!;
            var label = $"{row.Section}/{row.CaseId}";
            if (pair.ProtocolVersion != Pcre2QualificationProtocolVersion)
            {
                errors.Add($"{label}: protocol {pair.ProtocolVersion} is not current protocol {Pcre2QualificationProtocolVersion}.");
            }

            var sampleCount = pair.SampleCount;
            if (sampleCount < 9 ||
                pair.LaneOrders.Count != sampleCount ||
                pair.ManagedSampleMicroseconds.Count != sampleCount ||
                pair.ComparatorSampleMicroseconds.Count != sampleCount ||
                pair.ManagedSampleMilliseconds.Count != sampleCount ||
                pair.ComparatorSampleMilliseconds.Count != sampleCount ||
                pair.PairedRatios.Count != sampleCount)
            {
                errors.Add($"{label}: paired sample vectors are incomplete.");
                continue;
            }

            var managedFirstCount = pair.LaneOrders.Count(static order => order == Pcre2PairLaneOrder.ManagedFirst);
            var comparatorFirstCount = pair.LaneOrders.Count(static order => order == Pcre2PairLaneOrder.ComparatorFirst);
            if (Math.Abs(managedFirstCount - comparatorFirstCount) > 1)
            {
                errors.Add($"{label}: lane order is not balanced ({managedFirstCount}/{comparatorFirstCount}).");
            }

            for (var sample = 1; sample < sampleCount; sample++)
            {
                if (pair.LaneOrders[sample] == pair.LaneOrders[sample - 1])
                {
                    errors.Add($"{label}: lane order does not alternate at sample {sample}.");
                    break;
                }
            }

            var recomputedRatios = pair.ManagedSampleMicroseconds
                .Select((managed, index) => managed / pair.ComparatorSampleMicroseconds[index])
                .ToArray();
            if (!recomputedRatios.Zip(pair.PairedRatios).All(static values => NearlyEqual(values.First, values.Second)))
            {
                errors.Add($"{label}: a stored paired ratio does not match its lane samples.");
            }

            var recomputedRatioMedian = Math.Exp(BenchmarkPairedStatistics.Median(
                recomputedRatios.Select(static ratio => Math.Log(ratio))));
            if (!NearlyEqual(recomputedRatioMedian, pair.RatioMedian))
            {
                errors.Add($"{label}: the stored median ratio is stale.");
            }

            var managedFirstRatios = pair.LaneOrders
                .Select((order, index) => (Order: order, Ratio: recomputedRatios[index]))
                .Where(static sample => sample.Order == Pcre2PairLaneOrder.ManagedFirst)
                .Select(static sample => sample.Ratio);
            var comparatorFirstRatios = pair.LaneOrders
                .Select((order, index) => (Order: order, Ratio: recomputedRatios[index]))
                .Where(static sample => sample.Order == Pcre2PairLaneOrder.ComparatorFirst)
                .Select(static sample => sample.Ratio);
            var recomputedOrderEffect = BenchmarkPairedStatistics.Median(managedFirstRatios) /
                BenchmarkPairedStatistics.Median(comparatorFirstRatios);
            if (!NearlyEqual(recomputedOrderEffect, pair.OrderEffectRatio))
            {
                errors.Add($"{label}: the stored lane-order effect is stale.");
            }


            var managedSpread = BenchmarkPairedStatistics.InterquartileSpread(pair.ManagedSampleMicroseconds);
            var comparatorSpread = BenchmarkPairedStatistics.InterquartileSpread(
                pair.ComparatorSampleMicroseconds);
            if (!NearlyEqual(managedSpread, pair.ManagedInterquartileSpreadRatio) ||
                !NearlyEqual(comparatorSpread, pair.ComparatorInterquartileSpreadRatio))
            {
                errors.Add($"{label}: a stored lane interquartile spread is stale.");
            }

            var durationsQualified = pair.ManagedSampleMilliseconds.All(
                                         static duration => duration >= Pcre2QualificationMinimumSampleMilliseconds) &&
                                     pair.ComparatorSampleMilliseconds.All(
                                         static duration => duration >= Pcre2QualificationMinimumSampleMilliseconds);
            var expectedStatus = DerivePcre2ConsistencyStatus(
                pair.RatioLower95,
                pair.RatioUpper95,
                recomputedOrderEffect,
                managedSpread,
                comparatorSpread,
                durationsQualified);
            if (pair.Status != expectedStatus)
            {
                errors.Add($"{label}: stored Status {pair.Status} should be {expectedStatus}.");
            }

            if (Math.Max(pair.ManagedMedianMicroseconds, pair.ComparatorMedianMicroseconds) <
                Pcre2FastLaneMaximumMedianMicroseconds)
            {
                fastRows++;
                maximumFastSpread = Math.Max(maximumFastSpread, Math.Max(managedSpread, comparatorSpread));
            }
        }

        var sentinels = new (string Section, string CaseId)[]
        {
            ("pcre2-managed-compatible-enumerate", "simple/foo-dense"),
            ("pcre2-managed-compatible-ismatch", "common/email-miss"),
            ("pcre2-managed-compatible-count", "industry/rust-sherlock-letter-count"),
        };
        foreach (var sentinel in sentinels)
        {
            if (!pairedRows.Any(row => row.Section == sentinel.Section && row.CaseId == sentinel.CaseId))
            {
                errors.Add($"{sentinel.Section}/{sentinel.CaseId}: required qualification sentinel is missing.");
            }
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        Console.WriteLine(
            $"PCRE2 qualification consistency passed: {pairedRows.Length} paired rows, " +
            $"{fastRows} fast-row repeatability checks, maximum IQR spread {maximumFastSpread:F3}, " +
            $"balanced alternating lane orders, current derived Status, {sentinels.Length} required sentinels.");
        return 0;
    }

    private static Pcre2NativeComparisonStatus DerivePcre2ConsistencyStatus(
        double lowerRatio,
        double upperRatio,
        double orderEffectRatio,
        double managedInterquartileSpread,
        double comparatorInterquartileSpread,
        bool sampleDurationsQualified)
    {
        if (!sampleDurationsQualified)
        {
            return Pcre2NativeComparisonStatus.Unqualified;
        }

        if (managedInterquartileSpread > Pcre2FastLaneMaximumInterquartileSpread ||
            comparatorInterquartileSpread > Pcre2FastLaneMaximumInterquartileSpread)
        {
            return Pcre2NativeComparisonStatus.Inconclusive;
        }

        if (orderEffectRatio is < 0.98 or > 1.02)
        {
            return Pcre2NativeComparisonStatus.Inconclusive;
        }

        if (upperRatio < 0.98)
        {
            return Pcre2NativeComparisonStatus.ManagedFaster;
        }

        if (lowerRatio > 1.02)
        {
            return Pcre2NativeComparisonStatus.NativeFaster;
        }

        return lowerRatio >= 0.98 && upperRatio <= 1.02
            ? Pcre2NativeComparisonStatus.Equivalent
            : Pcre2NativeComparisonStatus.Inconclusive;
    }

    private static bool NearlyEqual(double left, double right) =>
        Math.Abs(left - right) <= Math.Max(Math.Abs(left), Math.Abs(right)) * 1e-12;
}
