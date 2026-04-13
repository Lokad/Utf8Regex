using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum ReplicaBenchmarkSource : byte
{
    DotNetPerformance = 0,
    Lokad = 1,
}

internal sealed class ReplicaCountBenchmarkCase
{
    private ReplicaCountBenchmarkCase(
        ReplicaBenchmarkSource source,
        string id,
        string pattern,
        string utf8Pattern,
        RegexOptions options,
        int inputChars,
        byte[] inputBytes,
        Utf8Regex utf8Regex,
        Regex regex,
        Regex compiledRegex,
        Func<int> countDecodeThenRegex,
        Func<int> countDecodeThenCompiledRegex,
        Func<int> countPredecodedRegex,
        Func<int> countPredecodedCompiledRegex,
        string? origin = null,
        string? patternMode = null,
        string? group = null,
        string? intent = null,
        bool requiresDedicatedMeasurement = false)
    {
        Source = source;
        Id = id;
        Pattern = pattern;
        Utf8Pattern = utf8Pattern;
        Options = options;
        InputChars = inputChars;
        InputBytes = inputBytes;
        Utf8Regex = utf8Regex;
        Regex = regex;
        CompiledRegex = compiledRegex;
        CountDecodeThenRegex = countDecodeThenRegex;
        CountDecodeThenCompiledRegex = countDecodeThenCompiledRegex;
        CountPredecodedRegex = countPredecodedRegex;
        CountPredecodedCompiledRegex = countPredecodedCompiledRegex;
        Origin = origin;
        PatternMode = patternMode;
        Group = group;
        Intent = intent;
        RequiresDedicatedMeasurement = requiresDedicatedMeasurement;
    }

    public ReplicaBenchmarkSource Source { get; }

    public string Id { get; }

    public string Pattern { get; }

    public string Utf8Pattern { get; }

    public RegexOptions Options { get; }

    public int InputChars { get; }

    public byte[] InputBytes { get; }

    public Utf8Regex Utf8Regex { get; }

    public Regex Regex { get; }

    public Regex CompiledRegex { get; }

    public Func<int> CountDecodeThenRegex { get; }

    public Func<int> CountDecodeThenCompiledRegex { get; }

    public Func<int> CountPredecodedRegex { get; }

    public Func<int> CountPredecodedCompiledRegex { get; }

    public string? Origin { get; }

    public string? PatternMode { get; }

    public string? Group { get; }

    public string? Intent { get; }

    public bool RequiresDedicatedMeasurement { get; }

    public static ReplicaCountBenchmarkCase Resolve(string caseId)
    {
        EnsureUniqueIds();

        var dotNetPerformance = TryGetDotNetPerformance(caseId);
        var lokadCode = TryGetLokadCode(caseId);
        var lokadScript = TryGetLokadScript(caseId);
        var hits = (dotNetPerformance is not null ? 1 : 0) + (lokadCode is not null ? 1 : 0) + (lokadScript is not null ? 1 : 0);
        if (hits > 1)
        {
            throw new InvalidOperationException($"Benchmark case id '{caseId}' is ambiguous across replica catalogs.");
        }

        return dotNetPerformance ?? lokadCode ?? lokadScript ?? throw new InvalidOperationException($"Benchmark case '{caseId}' was not found in replica catalogs.");
    }

    public static bool TryResolve(string caseId, out ReplicaCountBenchmarkCase? benchmarkCase)
    {
        EnsureUniqueIds();

        var dotNetPerformance = TryGetDotNetPerformance(caseId);
        var lokadCode = TryGetLokadCode(caseId);
        var lokadScript = TryGetLokadScript(caseId);
        var hits = (dotNetPerformance is not null ? 1 : 0) + (lokadCode is not null ? 1 : 0) + (lokadScript is not null ? 1 : 0);
        if (hits > 1)
        {
            throw new InvalidOperationException($"Benchmark case id '{caseId}' is ambiguous across replica catalogs.");
        }

        benchmarkCase = dotNetPerformance ?? lokadCode ?? lokadScript;
        return benchmarkCase is not null;
    }

    public static IEnumerable<ReplicaCountBenchmarkCase> GetAll(ReplicaBenchmarkSource source)
    {
        return source switch
        {
            ReplicaBenchmarkSource.DotNetPerformance => DotNetPerformanceReplicaBenchmarkCatalog.GetAllCases()
                .Where(static c => c.Model == DotNetPerformanceReplicaBenchmarkModel.Count && !c.IsDevelopmentSlice)
                .Select(static c => FromDotNetPerformance(c)),
            ReplicaBenchmarkSource.Lokad => LokadReplicaCodeBenchmarkCatalog.GetAllCases()
                .Where(static c => c.Model == LokadReplicaCodeBenchmarkModel.Count)
                .Select(static c => FromLokadCode(c))
                .Concat(LokadReplicaScriptBenchmarkCatalog.GetAllCases().Select(static c => FromLokadScript(c))),
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };
    }

    private static ReplicaCountBenchmarkCase? TryGetDotNetPerformance(string caseId)
    {
        var benchmarkCase = DotNetPerformanceReplicaBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId && c.Model == DotNetPerformanceReplicaBenchmarkModel.Count);
        return benchmarkCase is null ? null : FromDotNetPerformance(benchmarkCase);
    }

    private static ReplicaCountBenchmarkCase? TryGetLokadCode(string caseId)
    {
        var benchmarkCase = LokadReplicaCodeBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId && c.Model == LokadReplicaCodeBenchmarkModel.Count);
        return benchmarkCase is null ? null : FromLokadCode(benchmarkCase);
    }

    private static ReplicaCountBenchmarkCase? TryGetLokadScript(string caseId)
    {
        var benchmarkCase = LokadReplicaScriptBenchmarkCatalog.GetAllCases().FirstOrDefault(c => c.Id == caseId);
        return benchmarkCase is null ? null : FromLokadScript(benchmarkCase);
    }

    private static ReplicaCountBenchmarkCase FromDotNetPerformance(DotNetPerformanceReplicaBenchmarkCase benchmarkCase)
    {
        var context = new DotNetPerformanceReplicaBenchmarkContext(benchmarkCase);
        return new ReplicaCountBenchmarkCase(
            ReplicaBenchmarkSource.DotNetPerformance,
            benchmarkCase.Id,
            context.Pattern,
            context.Pattern,
            benchmarkCase.Options,
            context.InputString.Length,
            context.InputBytes,
            context.Utf8Regex,
            context.Regex,
            context.CompiledRegex,
            context.CountDecodeThenRegex,
            context.CountDecodeThenCompiledRegex,
            context.CountPredecodedRegex,
            context.CountPredecodedCompiledRegex,
            origin: benchmarkCase.Origin,
            group: benchmarkCase.Group);
    }

    private static ReplicaCountBenchmarkCase FromLokadCode(LokadReplicaCodeBenchmarkCase benchmarkCase)
    {
        var context = new LokadReplicaCodeBenchmarkContext(benchmarkCase);
        return new ReplicaCountBenchmarkCase(
            ReplicaBenchmarkSource.Lokad,
            benchmarkCase.Id,
            context.Pattern,
            context.CompiledPattern,
            benchmarkCase.Options,
            context.InputString.Length,
            context.InputBytes,
            context.Utf8Regex,
            context.Regex,
            context.CompiledRegex,
            context.CountDecodeThenRegex,
            context.CountDecodeThenCompiledRegex,
            context.CountPredecodedRegex,
            context.CountPredecodedCompiledRegex,
            patternMode: benchmarkCase.PatternMode.ToString(),
            group: benchmarkCase.Group,
            intent: benchmarkCase.Intent);
    }

    private static ReplicaCountBenchmarkCase FromLokadScript(LokadReplicaScriptBenchmarkCase benchmarkCase)
    {
        var context = new LokadReplicaScriptBenchmarkContext(benchmarkCase);
        return new ReplicaCountBenchmarkCase(
            ReplicaBenchmarkSource.Lokad,
            benchmarkCase.Id,
            context.Pattern,
            context.Pattern,
            benchmarkCase.Utf8Options,
            context.InputChars,
            context.BenchmarkCase.Model == LokadReplicaScriptBenchmarkModel.Count ? context.InputBytes : Array.Empty<byte>(),
            context.Utf8Regex,
            context.Regex,
            context.Regex,
            context.ExecuteDecodeThenRegex,
            context.ExecuteDecodeThenCompiledRegex,
            context.ExecutePredecodedRegex,
            context.ExecutePredecodedCompiledRegex,
            group: benchmarkCase.Group,
            patternMode: benchmarkCase.Model.ToString(),
            requiresDedicatedMeasurement: benchmarkCase.Model != LokadReplicaScriptBenchmarkModel.Count);
    }

    private static void EnsureUniqueIds()
    {
        if (s_checkedUniqueIds)
        {
            return;
        }

        var duplicateId = DotNetPerformanceReplicaBenchmarkCatalog.GetAllCases()
            .Where(static c => c.Model == DotNetPerformanceReplicaBenchmarkModel.Count)
            .Select(static c => c.Id)
            .Intersect(
                LokadReplicaCodeBenchmarkCatalog.GetAllCases()
                    .Where(static c => c.Model == LokadReplicaCodeBenchmarkModel.Count)
                    .Select(static c => c.Id),
                StringComparer.Ordinal)
            .FirstOrDefault();

        if (duplicateId is null)
        {
            duplicateId = DotNetPerformanceReplicaBenchmarkCatalog.GetAllCases()
                .Where(static c => c.Model == DotNetPerformanceReplicaBenchmarkModel.Count)
                .Select(static c => c.Id)
                .Intersect(
                    LokadReplicaScriptBenchmarkCatalog.GetAllCases()
                        .Select(static c => c.Id),
                    StringComparer.Ordinal)
                .FirstOrDefault();
        }

        if (duplicateId is null)
        {
            duplicateId = LokadReplicaCodeBenchmarkCatalog.GetAllCases()
                .Where(static c => c.Model == LokadReplicaCodeBenchmarkModel.Count)
                .Select(static c => c.Id)
                .Intersect(
                    LokadReplicaScriptBenchmarkCatalog.GetAllCases()
                        .Select(static c => c.Id),
                    StringComparer.Ordinal)
                .FirstOrDefault();
        }

        if (duplicateId is not null)
        {
            throw new InvalidOperationException($"Benchmark case id '{duplicateId}' is duplicated across replica catalogs.");
        }

        s_checkedUniqueIds = true;
    }

    private static bool s_checkedUniqueIds;
}

