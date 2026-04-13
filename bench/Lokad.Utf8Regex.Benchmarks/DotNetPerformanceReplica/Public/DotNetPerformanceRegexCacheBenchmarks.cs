using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("LokadReplica", "PublicCache")]
public class DotNetPerformanceRegexCacheBenchmarks
{
    private const int MaxConcurrency = 4;
    private int _oldDotNetCacheSize;
    private int _oldUtf8CacheSize;
    private IReadOnlyDictionary<(int total, int unique), string[]> _patterns = null!;
    private readonly byte[] _utf8Input = "0123456789"u8.ToArray();

    [GlobalSetup]
    public void Setup()
    {
        _oldDotNetCacheSize = Regex.CacheSize;
        _oldUtf8CacheSize = Utf8RegexCache.MaxEntries;
        Regex.CacheSize = 0;
        Utf8RegexCache.MaxEntries = 1;

        _patterns = new Dictionary<(int total, int unique), string[]>
        {
            { (400_000, 7), CreatePatterns(400_000, 7) },
            { (400_000, 1), CreatePatterns(400_000, 1) },
            { (40_000, 7), CreatePatterns(40_000, 7) },
            { (40_000, 1_600), CreatePatterns(40_000, 1_600) },
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Regex.CacheSize = _oldDotNetCacheSize;
        Utf8RegexCache.MaxEntries = _oldUtf8CacheSize;
        Utf8RegexCache.ResetForTests();
    }

    [Benchmark(Baseline = true)]
    [Arguments(400_000, 7, 15)]
    [Arguments(400_000, 1, 15)]
    [Arguments(40_000, 7, 0)]
    [Arguments(40_000, 1_600, 15)]
    [Arguments(40_000, 1_600, 800)]
    [Arguments(40_000, 1_600, 3_200)]
    public bool DotNetStaticIsMatch(int total, int unique, int cacheSize)
    {
        if (Regex.CacheSize != cacheSize)
        {
            Regex.CacheSize = cacheSize;
        }

        return RunDotNet(0, total, _patterns[(total, unique)]);
    }

    [Benchmark]
    [Arguments(400_000, 7, 15)]
    [Arguments(400_000, 1, 15)]
    [Arguments(40_000, 7, 0)]
    [Arguments(40_000, 1_600, 15)]
    [Arguments(40_000, 1_600, 800)]
    [Arguments(40_000, 1_600, 3_200)]
    public bool Utf8StaticIsMatch(int total, int unique, int cacheSize)
    {
        var effectiveSize = Math.Max(cacheSize, 1);
        if (Utf8RegexCache.MaxEntries != effectiveSize)
        {
            Utf8RegexCache.MaxEntries = effectiveSize;
        }

        return RunUtf8(0, total, _patterns[(total, unique)]);
    }

    [Benchmark]
    [BenchmarkCategory("Multithreaded")]
    [Arguments(400_000, 7, 15)]
    [Arguments(40_000, 1_600, 800)]
    public async Task DotNetStaticIsMatch_Multithreaded(int total, int unique, int cacheSize)
    {
        if (Regex.CacheSize != cacheSize)
        {
            Regex.CacheSize = cacheSize;
        }

        var patterns = _patterns[(total, unique)];
        var slice = total / MaxConcurrency;
        var tasks = new Task[MaxConcurrency];
        for (var i = 0; i < MaxConcurrency; i++)
        {
            var start = i * slice;
            tasks[i] = Task.Run(() => RunDotNet(start, slice, patterns));
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    [BenchmarkCategory("Multithreaded")]
    [Arguments(400_000, 7, 15)]
    [Arguments(40_000, 1_600, 800)]
    public async Task Utf8StaticIsMatch_Multithreaded(int total, int unique, int cacheSize)
    {
        var effectiveSize = Math.Max(cacheSize, 1);
        if (Utf8RegexCache.MaxEntries != effectiveSize)
        {
            Utf8RegexCache.MaxEntries = effectiveSize;
        }

        var patterns = _patterns[(total, unique)];
        var slice = total / MaxConcurrency;
        var tasks = new Task[MaxConcurrency];
        for (var i = 0; i < MaxConcurrency; i++)
        {
            var start = i * slice;
            tasks[i] = Task.Run(() => RunUtf8(start, slice, patterns));
        }

        await Task.WhenAll(tasks);
    }

    private bool RunDotNet(int start, int total, string[] patterns)
    {
        var result = false;
        for (var i = 0; i < total; i++)
        {
            result ^= Regex.IsMatch("0123456789", patterns[start + i], RegexOptions.CultureInvariant);
        }

        return result;
    }

    private bool RunUtf8(int start, int total, string[] patterns)
    {
        var result = false;
        for (var i = 0; i < total; i++)
        {
            result ^= Utf8Regex.IsMatch(_utf8Input, patterns[start + i], RegexOptions.CultureInvariant);
        }

        return result;
    }

    private static string[] CreatePatterns(int total, int unique)
    {
        var patterns = new string[total];
        for (var i = 0; i < unique; i++)
        {
            var builder = new StringBuilder();
            foreach (var c in i.ToString())
            {
                builder.Append('(').Append(c).Append("+)");
            }

            patterns[i] = builder.ToString();
        }

        for (var i = unique; i < total; i++)
        {
            patterns[i] = patterns[i % unique];
        }

        var random = new Random(101);
        for (var i = 0; i < total; i++)
        {
            var j = random.Next(i, total);
            (patterns[i], patterns[j]) = (patterns[j], patterns[i]);
        }

        return patterns;
    }
}
