using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("MicroCost", "ShortCall", "Public")]
public class ShortPublicMatchMicroCostBenchmarks
{
    private const int RepeatCount = 1024;

    private LokadPublicBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds =>
    [
        "common/email-match",
        "common/uri-match",
        "common/match-word",
        "industry/boostdocs-postcode-match",
        "industry/boostdocs-credit-card-match",
    ];

    [GlobalSetup]
    public void Setup() => _context = new LokadPublicBenchmarkContext(CaseId);

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int ValidationOnly()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8ValidationOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int WellFormedOnly()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8WellFormedOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int PrefilterOnly()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8PrefilterOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int DirectHook()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8DirectHookOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int DirectFixedLengthOnly()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8DirectFixedLengthOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int DirectFixedAlternationOnly()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8DirectFixedAlternationOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int PublicAfterValidation()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8PublicAfterValidationOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int CompiledAfterValidation()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8CompiledAfterValidationOnly();
        }

        return total;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = RepeatCount)]
    public int Utf8Regex()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8Regex();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int PredecodedRegex()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecutePredecodedRegex();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int PredecodedCompiledRegex()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecutePredecodedCompiledRegex();
        }

        return total;
    }
}

[MemoryDiagnoser]
[BenchmarkCategory("MicroCost", "ShortCall", "PrefixLoop")]
public class ShortPrefixLoopMicroCostBenchmarks
{
    private const int RepeatCount = 128;

    private LokadReplicaScriptBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds =>
    [
        "lokad/style/hex-color",
        "lokad/langserv/helper-identifier",
        "lokad/langserv/identifier-validator",
        "lokad/lexer/doc-line",
    ];

    [GlobalSetup]
    public void Setup() => _context = new LokadReplicaScriptBenchmarkContext(LokadReplicaScriptBenchmarkCatalog.Get(CaseId));

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int ValidationOnly()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8PrefixValidationOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int WellFormedOnly()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8PrefixWellFormedOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int PrefilterOnly()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8PrefixPrefilterOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int DirectHook()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8PrefixDirectHookOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int PublicAfterValidation()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8PrefixPublicAfterValidationOnly();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int CompiledAfterValidation()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8PrefixCompiledAfterValidation();
        }

        return total;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = RepeatCount)]
    public int Utf8Regex()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8Regex();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int Utf8Compiled()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecuteUtf8Compiled();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int PredecodedRegex()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecutePredecodedRegex();
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int PredecodedCompiledRegex()
    {
        var total = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            total += _context.ExecutePredecodedCompiledRegex();
        }

        return total;
    }
}
