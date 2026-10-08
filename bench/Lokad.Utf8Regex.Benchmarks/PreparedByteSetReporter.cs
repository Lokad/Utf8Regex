using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class PreparedByteSetReporter
{
    internal static int Run(string outputPath)
    {
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        // Paired kernel attribution only. Compilation, validation and result
        // projection are excluded; this command does not update any snapshot.
        var profiles = new (string Name, byte[] Values)[]
        {
            ("sparse-ascii", "noxyzq"u8.ToArray()),
            ("mixed-byte", [0, 15, 16, 63, 64, 127, 128, 143, 191, 192, 254, 255]),
            ("word", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_"u8.ToArray()),
            ("range", Enumerable.Range(97, 26).Select(value => (byte)value).ToArray()),
        };
        using var writer = new StreamWriter(File.Open(outputPath, FileMode.CreateNew));
        writer.WriteLine(JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(),
            avx2 = Avx2.IsSupported, vector128 = Vector128.IsHardwareAccelerated,
            processorCount = Environment.ProcessorCount, processorScope.Policy, processorScope.AffinityMask,
            processorScope.EfficiencyClass, kernel = typeof(PreparedByteSet).Assembly.ManifestModule.ModuleVersionId }));
        var count = 0;
        foreach (var (name, values) in profiles)
        foreach (var length in new[] { 31, 65536 })
        foreach (var late in new[] { false, true })
        foreach (var operation in new[] { "forward", "reverse", "except", "reverse-except" })
        {
            var set = new PreparedByteSet(PreparedByteSetState.FromValues(values));
            var oracle = SearchValues.Create(values);
            var except = operation.Contains("except", StringComparison.Ordinal);
            var member = values[0];
            var nonmember = Enumerable.Range(0, 256).Select(value => (byte)value).First(value => !values.Contains(value));
            var input = Enumerable.Repeat(except ? member : nonmember, length).ToArray();
            if (late) input[operation.StartsWith("reverse", StringComparison.Ordinal) ? 0 : length - 1] = except ? nonmember : member;
            var expected = late ? operation.StartsWith("reverse", StringComparison.Ordinal) ? 0 : length - 1 : -1;
            Func<int> owned = operation switch
            {
                "forward" => () => set.IndexOf(input), "reverse" => () => set.LastIndexOf(input),
                "except" => () => set.IndexOfAnyExcept(input), _ => () => set.LastIndexOfAnyExcept(input),
            };
            Func<int> bcl = operation switch
            {
                "forward" => () => input.AsSpan().IndexOfAny(oracle), "reverse" => () => input.AsSpan().LastIndexOfAny(oracle),
                "except" => () => input.AsSpan().IndexOfAnyExcept(oracle), _ => () => input.AsSpan().LastIndexOfAnyExcept(oracle),
            };
            if (owned() != expected || bcl() != expected) throw new InvalidOperationException("Byte-set result disagreement.");
            var minimum = length < 128 ? 1_000_000 : 16_384;
            var warmCalls = length < 128 ? 5_000_000 : 0;
            Warm(owned, warmCalls); Warm(bcl, warmCalls);
            // Prime calibration, then rewarm the retained lanes. Tiered-PGO can
            // optimize a small route after the first duration-only warmup.
            _ = Calibrate(owned, minimum, expected); _ = Calibrate(bcl, minimum, expected);
            Warm(owned, warmCalls); Warm(bcl, warmCalls);
            var ownedIterations = Calibrate(owned, minimum, expected);
            var bclIterations = Calibrate(bcl, minimum, expected);
            var ownedSamples = new double[5]; var bclSamples = new double[5];
            long allocated = 0;
            for (var sample = 0; sample < 5; sample++)
            {
                foreach (var managed in sample % 2 == 0 ? new[] { true, false } : new[] { false, true })
                {
                    var result = Measure(managed ? owned : bcl, managed ? ownedIterations : bclIterations, expected);
                    (managed ? ownedSamples : bclSamples)[sample] = result.Nanoseconds;
                    allocated += result.Allocated;
                }
            }
            var row = new { profile = name, length, subject = late ? "late" : "miss", operation, ownedIterations, bclIterations, expected,
                ownedNanoseconds = ownedSamples, bclNanoseconds = bclSamples, allocated,
                floorNanoseconds = Measure(static () => -1, Math.Max(ownedIterations, bclIterations), -1).Nanoseconds };
            writer.WriteLine(JsonSerializer.Serialize(row)); writer.Flush(); count++;
        }
        var summary = JsonSerializer.Serialize(new { protocol = "rewarmed-250ms-5m-small-calls-fastest-of-3-independent-calibration-alternating-5-pairs-kernel-attribution", rows = count });
        writer.WriteLine(summary); Console.WriteLine(summary);
        return 0;
    }

    private static void Warm(Func<int> operation, int minimumCalls)
    {
        var start = Stopwatch.GetTimestamp();
        var calls = 0;
        do { for (var i = 0; i < 1024; i++) _ = operation(); calls += 1024; }
        while (calls < minimumCalls || Stopwatch.GetElapsedTime(start).TotalMilliseconds < 250);
    }

    private static int Calibrate(Func<int> operation, int minimum, int expected)
    {
        var nanoseconds = Enumerable.Range(0, 3).Min(_ => Measure(operation, minimum, expected).Nanoseconds);
        return checked((int)Math.Min(250_000_000, Math.Max(minimum, Math.Ceiling(30_000_000 / nanoseconds))));
    }

    private static (double Nanoseconds, long Allocated) Measure(Func<int> operation, int iterations, int expected)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp(); long checksum = 0;
        for (var i = 0; i < iterations; i++) checksum += operation();
        var elapsed = Stopwatch.GetElapsedTime(start);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (checksum != (long)expected * iterations) throw new InvalidOperationException("Byte-set checksum disagreement.");
        return (elapsed.TotalNanoseconds / iterations, allocated);
    }
}
