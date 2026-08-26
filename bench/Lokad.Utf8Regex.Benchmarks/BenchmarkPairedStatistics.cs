namespace Lokad.Utf8Regex.Benchmarks;

internal static class BenchmarkPairedStatistics
{
    internal static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0)
        {
            throw new InvalidOperationException("Cannot compute the median of an empty sample.");
        }

        var midpoint = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[midpoint - 1] + sorted[midpoint]) / 2
            : sorted[midpoint];
    }

    internal static double InterquartileSpread(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0)
        {
            throw new InvalidOperationException("Cannot compute the interquartile spread of an empty sample.");
        }

        var lower = sorted[(int)Math.Floor((sorted.Length - 1) * 0.25)];
        var upper = sorted[(int)Math.Ceiling((sorted.Length - 1) * 0.75)];
        return upper / lower;
    }

    internal static BenchmarkConfidenceInterval BootstrapMedianLogRatio(
        IReadOnlyList<double> logRatios,
        int seed,
        int resamples)
    {
        if (logRatios.Count == 0)
        {
            throw new InvalidOperationException("Cannot bootstrap an empty paired sample.");
        }

        if (resamples <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resamples));
        }

        var random = new Random(seed);
        var bootstrapMedians = new double[resamples];
        var resample = new double[logRatios.Count];
        for (var bootstrapIndex = 0; bootstrapIndex < bootstrapMedians.Length; bootstrapIndex++)
        {
            for (var sampleIndex = 0; sampleIndex < resample.Length; sampleIndex++)
            {
                resample[sampleIndex] = logRatios[random.Next(logRatios.Count)];
            }

            bootstrapMedians[bootstrapIndex] = Median(resample);
        }

        Array.Sort(bootstrapMedians);
        var lowerIndex = (int)Math.Floor((bootstrapMedians.Length - 1) * 0.025);
        var upperIndex = (int)Math.Ceiling((bootstrapMedians.Length - 1) * 0.975);
        return new BenchmarkConfidenceInterval(
            bootstrapMedians[lowerIndex],
            bootstrapMedians[upperIndex]);
    }
}

internal readonly record struct BenchmarkConfidenceInterval(double Lower, double Upper);
