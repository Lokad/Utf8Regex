using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class PythonReBenchmarkReporter
{
    private const string SnapshotFileName = "PythonRe.Benchmarks.json";
    private static int s_sink;

    internal static bool TryHandleCommand(string[] args, out int exitCode)
    {
        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-case", StringComparison.Ordinal))
        {
            exitCode = MeasureCase(
                args[1],
                ParsePositive(args, 2, 200),
                ParsePositive(args, 3, 5));
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--refresh-pythonre-benchmark-case", StringComparison.Ordinal))
        {
            exitCode = RefreshCase(
                args[1],
                ParsePositive(args, 2, 200),
                ParsePositive(args, 3, 5));
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--refresh-pythonre-benchmarks", StringComparison.Ordinal))
        {
            exitCode = Refresh(
                ParsePositive(args, 1, 200),
                ParsePositive(args, 2, 5));
            return true;
        }

        exitCode = 0;
        return false;
    }

    private static int Refresh(int iterations, int samples)
    {
        var measurements = new SortedDictionary<string, PythonReCaseMeasurement>(StringComparer.Ordinal);
        foreach (var benchmarkCase in PythonReBenchmarkCatalog.Cases)
        {
            Console.WriteLine();
            var measurement = Measure(benchmarkCase, iterations, samples);
            Print(benchmarkCase, measurement);
            measurements.Add(benchmarkCase.Id, measurement);
        }

        var snapshot = new PythonReBenchmarkSnapshot
        {
            SchemaVersion = 2,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Corpus = CaptureCorpusProvenance(),
            Cases = measurements,
        };
        WriteSnapshot(snapshot);
        Console.WriteLine();
        Console.WriteLine($"Snapshot           : {Path.GetFullPath(SnapshotFileName)}");
        return 0;
    }

    private static int RefreshCase(string id, int iterations, int samples)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{id}'.");
            return 1;
        }

        if (!File.Exists(SnapshotFileName))
        {
            Console.Error.WriteLine($"PythonRe snapshot '{Path.GetFullPath(SnapshotFileName)}' does not exist.");
            return 1;
        }

        var snapshot = JsonSerializer.Deserialize<PythonReBenchmarkSnapshot>(File.ReadAllText(SnapshotFileName));
        if (snapshot is null || snapshot.SchemaVersion != 2)
        {
            Console.Error.WriteLine("PythonRe snapshot is missing or has an unsupported schema version.");
            return 1;
        }

        Console.WriteLine();
        var measurement = Measure(benchmarkCase, iterations, samples);
        Print(benchmarkCase, measurement);
        snapshot.Cases[id] = measurement;
        WriteSnapshot(new PythonReBenchmarkSnapshot
        {
            SchemaVersion = 2,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Corpus = CaptureCorpusProvenance(),
            Cases = snapshot.Cases,
        });
        Console.WriteLine();
        Console.WriteLine($"Snapshot           : {Path.GetFullPath(SnapshotFileName)}");
        return 0;
    }

    private static void WriteSnapshot(PythonReBenchmarkSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        var snapshotPath = Path.GetFullPath(SnapshotFileName);
        var temporaryPath = snapshotPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, json + Environment.NewLine, new UTF8Encoding(false));
            File.Move(temporaryPath, snapshotPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static int MeasureCase(string id, int iterations, int samples)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{id}'.");
            return 1;
        }

        var measurement = Measure(benchmarkCase, iterations, samples);
        Print(benchmarkCase, measurement);
        return 0;
    }

    private static PythonReCaseMeasurement Measure(
        PythonReBenchmarkCase benchmarkCase,
        int iterations,
        int samples)
    {
        var effectiveIterations = GetEffectiveIterations(benchmarkCase, iterations);
        var context = new PythonReBenchmarkContext(benchmarkCase);
        var pythonResult = context.ExecutePythonRe();
        var decodeResult = context.ExecuteDecodeThenRegex();
        var predecodedResult = context.ExecutePredecodedRegex();
        if (pythonResult != decodeResult || pythonResult != predecodedResult)
        {
            throw new InvalidOperationException(
                $"PythonRe benchmark '{benchmarkCase.Id}' produced incomparable sinks: " +
                $"PythonRe={pythonResult}, decode={decodeResult}, predecoded={predecodedResult}.");
        }

        return new PythonReCaseMeasurement
        {
            Pattern = benchmarkCase.Pattern,
            Options = benchmarkCase.Options.ToString(),
            Operation = benchmarkCase.Operation.ToString(),
            InputUtf8Bytes = context.InputBytes.Length,
            EffectiveIterations = effectiveIterations,
            Samples = samples,
            IncludesResultMaterialization = benchmarkCase.IncludesResultMaterialization,
            Environment = CaptureEnvironment(),
            PythonRe = MeasureOperation(context.ExecutePythonRe, effectiveIterations, samples),
            DecodeThenRegex = MeasureOperation(context.ExecuteDecodeThenRegex, effectiveIterations, samples),
            PredecodedRegex = MeasureOperation(context.ExecutePredecodedRegex, effectiveIterations, samples),
        };
    }

    private static int GetEffectiveIterations(PythonReBenchmarkCase benchmarkCase, int requestedIterations)
    {
        if (benchmarkCase.Operation is PythonReBenchmarkOperation.IsMatch or
                PythonReBenchmarkOperation.Search or
                PythonReBenchmarkOperation.Match or
                PythonReBenchmarkOperation.FullMatch or
                PythonReBenchmarkOperation.SearchDetailed &&
            Encoding.UTF8.GetByteCount(benchmarkCase.Input) <= 128)
        {
            return Math.Max(requestedIterations, 20_000);
        }

        var minimum = benchmarkCase.Operation switch
        {
            PythonReBenchmarkOperation.IsMatch or
            PythonReBenchmarkOperation.Search or
            PythonReBenchmarkOperation.Match or
            PythonReBenchmarkOperation.FullMatch or
            PythonReBenchmarkOperation.SearchDetailed => 5_000,
            PythonReBenchmarkOperation.Count when benchmarkCase.Id == "zero-width/count" => 5_000,
            PythonReBenchmarkOperation.Count => 500,
            PythonReBenchmarkOperation.FindAllStrings or
            PythonReBenchmarkOperation.FindAllUtf8 or
            PythonReBenchmarkOperation.FindIterDetailed => 1_000,
            _ => 2_000,
        };
        return Math.Max(requestedIterations, minimum);
    }

    private static PythonReOperationMeasurement MeasureOperation(
        Func<int> operation,
        int iterations,
        int samples)
    {
        var warmup = Stopwatch.StartNew();
        var warmupCalls = 0;
        do
        {
            s_sink ^= operation();
            warmupCalls++;
        }
        while (warmup.ElapsedMilliseconds < 100 && warmupCalls < 65_536);

        var microseconds = new double[samples];
        var allocations = new long[samples];
        for (var sample = 0; sample < samples; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            var local = 0;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                local ^= operation();
            }

            stopwatch.Stop();
            allocations[sample] = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
            microseconds[sample] = stopwatch.Elapsed.TotalMicroseconds / iterations;
            s_sink ^= local;
        }

        Array.Sort(microseconds);
        Array.Sort(allocations);
        return new PythonReOperationMeasurement
        {
            MedianMicroseconds = microseconds[microseconds.Length / 2],
            MinimumMicroseconds = microseconds[0],
            MaximumMicroseconds = microseconds[^1],
            MedianAllocatedBytes = allocations[allocations.Length / 2],
            WarmupCalls = warmupCalls,
            WarmupMilliseconds = warmup.Elapsed.TotalMilliseconds,
        };
    }

    private static void Print(PythonReBenchmarkCase benchmarkCase, PythonReCaseMeasurement measurement)
    {
        Console.WriteLine($"CaseId             : {benchmarkCase.Id}");
        Console.WriteLine($"Pattern            : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options            : {benchmarkCase.Options}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine($"InputBytes         : {measurement.InputUtf8Bytes}");
        Console.WriteLine($"Iterations         : {measurement.EffectiveIterations}");
        Console.WriteLine($"Samples            : {measurement.Samples}");
        PrintOperation("PythonRe", measurement.PythonRe);
        PrintOperation("DecodeThenRegex", measurement.DecodeThenRegex);
        PrintOperation("PredecodedRegex", measurement.PredecodedRegex);
    }

    private static void PrintOperation(string name, PythonReOperationMeasurement measurement)
    {
        Console.WriteLine(
            $"{name,-19}: {measurement.MedianMicroseconds,10:F3} us/op | " +
            $"range={measurement.MinimumMicroseconds:F3}..{measurement.MaximumMicroseconds:F3} | " +
            $"alloc={measurement.MedianAllocatedBytes} B/op | " +
            $"warmup={measurement.WarmupCalls} calls/{measurement.WarmupMilliseconds:F1} ms");
    }

    private static PythonReBenchmarkEnvironment CaptureEnvironment()
    {
        var trackedStatus = RunGit(
            "status",
            "--porcelain=v1",
            "--untracked-files=no",
            "--",
            ".",
            ":(exclude)README.md",
            ":(exclude)README.Benchmarks.json",
            ":(exclude)PCRE2.Benchmarks.json",
            ":(exclude)PythonRe.Benchmarks.json",
            ":(exclude)bench/Lokad.Utf8Regex.Benchmarks/Pcre2PerfLedger.md");
        var untrackedStatus = RunGit("ls-files", "--others", "--exclude-standard");
        return new PythonReBenchmarkEnvironment
        {
            SourceCommit = RunGit("rev-parse", "--short=12", "HEAD") ?? "<unknown>",
            TrackedDirty = !string.IsNullOrWhiteSpace(trackedStatus),
            HasUntrackedFiles = !string.IsNullOrWhiteSpace(untrackedStatus),
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ??
                RuntimeInformation.ProcessArchitecture.ToString(),
        };
    }

    private static PythonReCorpusProvenance CaptureCorpusProvenance()
    {
        const string sourceFile = "tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json";
        var fullPath = FindRepositoryFile(sourceFile);
        var hash = SHA256.HashData(File.ReadAllBytes(fullPath));
        using var corpus = JsonDocument.Parse(File.ReadAllText(fullPath));
        return new PythonReCorpusProvenance
        {
            SourceFile = sourceFile,
            Sha256 = Convert.ToHexString(hash),
            VectorCount = corpus.RootElement.GetArrayLength(),
            UpstreamCpythonRevision = "not-recorded-in-repository",
            Limitation = "The original upstream CPython version was not recorded; do not infer one from local vector names.",
        };
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private static string? RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output.Trim() : null;
    }

    private static int ParsePositive(string[] args, int index, int defaultValue)
    {
        if (index >= args.Length)
        {
            return defaultValue;
        }

        return int.TryParse(args[index], out var value) && value > 0
            ? value
            : throw new ArgumentException($"'{args[index]}' must be a positive integer.");
    }
}

internal enum PythonReBenchmarkOperation : byte
{
    IsMatch = 0,
    Search = 1,
    Match = 2,
    FullMatch = 3,
    SearchDetailed = 4,
    Count = 5,
    FindAllStrings = 6,
    FindAllUtf8 = 7,
    FindIterDetailed = 8,
    ReplaceString = 9,
    ReplaceUtf8 = 10,
    SubnString = 11,
    SubnUtf8 = 12,
    SubnEvaluatorString = 13,
    SplitStrings = 14,
    SubnEvaluatorUtf8 = 15,
}

internal sealed record PythonReBenchmarkCase(
    string Id,
    string Pattern,
    PythonReCompileOptions Options,
    PythonReBenchmarkOperation Operation,
    string Input,
    string Replacement,
    bool IncludesResultMaterialization);

internal static class PythonReBenchmarkCatalog
{
    internal static IReadOnlyList<PythonReBenchmarkCase> Cases { get; } =
    [
        new("literal/ismatch", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.IsMatch,
            new string('x', 65_536) + "needle", string.Empty, false),
        new("literal/search", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.Search,
            new string('x', 65_536) + "needle", string.Empty, false),
        new("literal/search-miss", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.Search,
            new string('x', 65_536), string.Empty, false),
        new("prefix/match", "header:[0-9]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.Match,
            "header:12345 " + new string('x', 16_384), string.Empty, false),
        new("literal/fullmatch", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-", PythonReCompileOptions.None, PythonReBenchmarkOperation.FullMatch,
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-", string.Empty, false),
        new("unicode/fullmatch", "(?:Шерлок )+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FullMatch,
            Repeat("Шерлок ", 1_024), string.Empty, false),
        new("capture/search-detailed", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SearchDetailed,
            "prefix item-123 suffix", string.Empty, true),
        new("family/count", "cat|dog|bird", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("cat fox dog owl bird ", 4_096), string.Empty, false),
        new("class-run/count", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("alpha beta gamma 123 ", 4_096), string.Empty, false),
        new("unicode/count", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("Шерлок и Ватсон. ", 4_096), string.Empty, false),
        new("findall/full-strings", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("alpha beta gamma 123 ", 1_024), string.Empty, true),
        new("findall/full-utf8", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("alpha beta gamma 123 ", 1_024), string.Empty, true),
        new("findall/unicode-full-strings", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("Шерлок и Ватсон. ", 512), string.Empty, true),
        new("findall/unicode-full-utf8", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("Шерлок и Ватсон. ", 512), string.Empty, true),
        new("findall/one-capture-strings", "item-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("item-12 item-345 ", 1_024), string.Empty, true),
        new("findall/many-capture-strings", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("item-12 other-345 ", 1_024), string.Empty, true),
        new("findall/many-capture-utf8", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("item-12 other-345 ", 1_024), string.Empty, true),
        new("findall/unicode-capture-utf8", "(é+)-(𝒜𝒜|𝒜)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("éé-𝒜𝒜 é-𝒜 ", 512), string.Empty, true),
        new("iteration/finditer-detailed", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindIterDetailed,
            Repeat("item-12 other-345 ", 256), string.Empty, true),
        new("zero-width/count", @"\b", PythonReCompileOptions.Ascii, PythonReBenchmarkOperation.Count,
            Repeat("alpha beta gamma ", 1_024), string.Empty, false),
        new("replacement/fixed-string", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.ReplaceString,
            Repeat("cat fox cat dog ", 2_048), "tiger", true),
        new("replacement/fixed-utf8", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.ReplaceUtf8,
            Repeat("cat fox cat dog ", 2_048), "tiger", true),
        new("replacement/subn-string", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnString,
            Repeat("cat fox cat dog ", 1_024), "tiger", true),
        new("replacement/subn-utf8", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnUtf8,
            Repeat("cat fox cat dog ", 1_024), "tiger", true),
        new("replacement/evaluator-string", "([a-z]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnEvaluatorString,
            Repeat("cat fox dog ", 512), "token", true),
        new("replacement/evaluator-utf8", "([a-z]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnEvaluatorUtf8,
            Repeat("cat fox dog ", 512), "token", true),
        new("split/no-captures", "[,;]", PythonReCompileOptions.None, PythonReBenchmarkOperation.SplitStrings,
            Repeat("alpha,beta;gamma,delta;", 512), string.Empty, true),
        new("split/captures", "([,;])", PythonReCompileOptions.None, PythonReBenchmarkOperation.SplitStrings,
            Repeat("alpha,beta;gamma,delta;", 512), string.Empty, true),
    ];

    private static string Repeat(string value, int count)
    {
        var builder = new StringBuilder(value.Length * count);
        for (var index = 0; index < count; index++)
        {
            builder.Append(value);
        }

        return builder.ToString();
    }
}

internal sealed class PythonReBenchmarkContext
{
    private readonly PythonReBenchmarkCase _case;
    private readonly Utf8PythonRegex _pythonRegex;
    private readonly Regex _regex;
    private readonly Regex _fullRegex;
    private readonly string _decoded;
    private readonly byte[] _replacementBytes;
    private readonly int _captureCount;
    private int _callbackChecksum;

    internal PythonReBenchmarkContext(PythonReBenchmarkCase benchmarkCase)
    {
        _case = benchmarkCase;
        InputBytes = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        _decoded = benchmarkCase.Input;
        _replacementBytes = Encoding.UTF8.GetBytes(benchmarkCase.Replacement);
        _pythonRegex = new Utf8PythonRegex(benchmarkCase.Pattern, benchmarkCase.Options);
        var regexOptions = ToRegexOptions(benchmarkCase.Options);
        _regex = new Regex(benchmarkCase.Pattern, regexOptions, Regex.InfiniteMatchTimeout);
        _fullRegex = new Regex($@"\A(?:{benchmarkCase.Pattern})\z", regexOptions, Regex.InfiniteMatchTimeout);
        _captureCount = _regex.GetGroupNumbers().Length - 1;
    }

    internal byte[] InputBytes { get; }

    internal int ExecutePythonRe() => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => _pythonRegex.IsMatch(InputBytes) ? 1 : 0,
        PythonReBenchmarkOperation.Search => Checksum(_pythonRegex.Search(InputBytes)),
        PythonReBenchmarkOperation.Match => Checksum(_pythonRegex.Match(InputBytes)),
        PythonReBenchmarkOperation.FullMatch => Checksum(_pythonRegex.FullMatch(InputBytes)),
        PythonReBenchmarkOperation.SearchDetailed => Checksum(_pythonRegex.SearchDetailedData(InputBytes)),
        PythonReBenchmarkOperation.Count => _pythonRegex.Count(InputBytes),
        PythonReBenchmarkOperation.FindAllStrings => Checksum(_pythonRegex.FindAllToStrings(InputBytes)),
        PythonReBenchmarkOperation.FindAllUtf8 => Checksum(_pythonRegex.FindAllToUtf8(InputBytes)),
        PythonReBenchmarkOperation.FindIterDetailed => Checksum(_pythonRegex.FindIterDetailed(InputBytes)),
        PythonReBenchmarkOperation.ReplaceString => Checksum(_pythonRegex.ReplaceToString(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.ReplaceUtf8 => Checksum(_pythonRegex.Replace(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnString => Checksum(_pythonRegex.SubnToString(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnUtf8 => Checksum(_pythonRegex.Subn(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnEvaluatorString => ExecutePythonReEvaluatorString(),
        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => ExecutePythonReEvaluatorUtf8(),
        PythonReBenchmarkOperation.SplitStrings => Checksum(_pythonRegex.SplitToStrings(InputBytes)),
        _ => throw new InvalidOperationException(),
    };

    internal int ExecuteDecodeThenRegex()
    {
        var decoded = Encoding.UTF8.GetString(InputBytes);
        return ExecuteRegex(decoded);
    }

    internal int ExecutePredecodedRegex() => ExecuteRegex(_decoded);

    private int ExecutePythonReEvaluatorString()
    {
        _callbackChecksum = 0;
        var result = _pythonRegex.SubnToString(
            InputBytes,
            this,
            static (context, match) =>
            {
                context._callbackChecksum = Combine(context._callbackChecksum, Checksum(match));
                return context._case.Replacement;
            });
        return Combine(Checksum(result), _callbackChecksum);
    }

    private int ExecutePythonReEvaluatorUtf8()
    {
        _callbackChecksum = 0;
        var result = _pythonRegex.Subn(
            InputBytes,
            this,
            static (context, match) =>
            {
                context._callbackChecksum = Combine(context._callbackChecksum, Checksum(match));
                return context._replacementBytes;
            });
        return Combine(Checksum(result), _callbackChecksum);
    }

    private int ExecuteRegex(string input) => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => _regex.IsMatch(input) ? 1 : 0,
        PythonReBenchmarkOperation.Search => Checksum(_regex.Match(input)),
        PythonReBenchmarkOperation.Match => ChecksumAtStart(_regex.Match(input)),
        PythonReBenchmarkOperation.FullMatch => Checksum(_fullRegex.Match(input)),
        PythonReBenchmarkOperation.SearchDetailed => Checksum(MaterializeDetailed(_regex.Match(input), input, GetUtf8Offsets(input))),
        PythonReBenchmarkOperation.Count => _regex.Count(input),
        PythonReBenchmarkOperation.FindAllStrings => Checksum(MaterializeFindAllStrings(input)),
        PythonReBenchmarkOperation.FindAllUtf8 => Checksum(MaterializeFindAllUtf8(input)),
        PythonReBenchmarkOperation.FindIterDetailed => Checksum(MaterializeFindIterDetailed(input)),
        PythonReBenchmarkOperation.ReplaceString => Checksum(_regex.Replace(input, _case.Replacement)),
        PythonReBenchmarkOperation.ReplaceUtf8 => Checksum(Encoding.UTF8.GetBytes(_regex.Replace(input, _case.Replacement))),
        PythonReBenchmarkOperation.SubnString => Checksum(ReplaceAndCount(input, encodeUtf8: false, materializeCallback: false)),
        PythonReBenchmarkOperation.SubnUtf8 => Checksum(ReplaceAndCount(input, encodeUtf8: true, materializeCallback: false)),
        PythonReBenchmarkOperation.SubnEvaluatorString => Checksum(ReplaceAndCount(input, encodeUtf8: false, materializeCallback: true)),
        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => Checksum(ReplaceAndCount(input, encodeUtf8: true, materializeCallback: true)),
        PythonReBenchmarkOperation.SplitStrings => Checksum(_regex.Split(input)),
        _ => throw new InvalidOperationException(),
    };

    private BclFindAllResult MaterializeFindAllStrings(string input)
    {
        if (_captureCount <= 1)
        {
            var values = new List<string>();
            foreach (Match match in _regex.Matches(input))
            {
                values.Add(_captureCount == 0 ? match.Value : match.Groups[1].Value);
            }

            return new BclFindAllResult(_captureCount == 0 ? Utf8PythonFindAllShape.FullMatch : Utf8PythonFindAllShape.SingleGroup, values.ToArray(), []);
        }

        var tuples = new List<string[]>();
        foreach (Match match in _regex.Matches(input))
        {
            var tuple = new string[_captureCount];
            for (var group = 0; group < tuple.Length; group++)
            {
                tuple[group] = match.Groups[group + 1].Value;
            }

            tuples.Add(tuple);
        }

        return new BclFindAllResult(Utf8PythonFindAllShape.GroupTuple, [], tuples.ToArray());
    }

    private BclFindAllUtf8Result MaterializeFindAllUtf8(string input)
    {
        var strings = MaterializeFindAllStrings(input);
        if (strings.Shape != Utf8PythonFindAllShape.GroupTuple)
        {
            var values = new byte[strings.ScalarValues.Length][];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = Encoding.UTF8.GetBytes(strings.ScalarValues[index]);
            }

            return new BclFindAllUtf8Result(strings.Shape, values, []);
        }

        var tuples = new byte[strings.TupleValues.Length][][];
        for (var match = 0; match < tuples.Length; match++)
        {
            tuples[match] = new byte[strings.TupleValues[match].Length][];
            for (var group = 0; group < tuples[match].Length; group++)
            {
                tuples[match][group] = Encoding.UTF8.GetBytes(strings.TupleValues[match][group]);
            }
        }

        return new BclFindAllUtf8Result(Utf8PythonFindAllShape.GroupTuple, [], tuples);
    }

    private BclDetailedMatch[] MaterializeFindIterDetailed(string input)
    {
        var utf8Offsets = GetUtf8Offsets(input);
        var matches = new List<BclDetailedMatch>();
        for (var match = _regex.Match(input); match.Success; match = match.NextMatch())
        {
            matches.Add(MaterializeDetailed(match, input, utf8Offsets));
        }

        return matches.ToArray();
    }

    private BclSubnResult ReplaceAndCount(string input, bool encodeUtf8, bool materializeCallback)
    {
        var count = 0;
        var callbackChecksum = 0;
        var utf8Offsets = materializeCallback ? GetUtf8Offsets(input) : null;
        var result = _regex.Replace(input, match =>
        {
            count++;
            if (materializeCallback)
            {
                callbackChecksum = Combine(callbackChecksum, Checksum(MaterializeDetailed(match, input, utf8Offsets)));
            }

            return _case.Replacement;
        });
        return new BclSubnResult(
            result,
            encodeUtf8 ? Encoding.UTF8.GetBytes(result) : null,
            count,
            materializeCallback ? callbackChecksum : null);
    }

    private static BclDetailedMatch MaterializeDetailed(Match match, string input, int[]? utf8Offsets)
    {
        if (!match.Success)
        {
            return new BclDetailedMatch([]);
        }

        var groups = new BclDetailedGroup[match.Groups.Count];
        for (var index = 0; index < groups.Length; index++)
        {
            var group = match.Groups[index];
            groups[index] = group.Success
                ? new BclDetailedGroup(
                    true,
                    utf8Offsets is null ? group.Index : utf8Offsets[group.Index],
                    utf8Offsets is null ? group.Index + group.Length : utf8Offsets[group.Index + group.Length],
                    group.Index,
                    group.Index + group.Length,
                    group.Value)
                : new BclDetailedGroup(false, 0, 0, 0, 0, string.Empty);
        }

        return new BclDetailedMatch(groups);
    }

    private static int[] BuildUtf8Offsets(string input)
    {
        var offsets = new int[input.Length + 1];
        var utf16 = 0;
        var utf8 = 0;
        while (utf16 < input.Length)
        {
            offsets[utf16] = utf8;
            var value = input[utf16];
            if (char.IsHighSurrogate(value) && utf16 + 1 < input.Length && char.IsLowSurrogate(input[utf16 + 1]))
            {
                offsets[utf16 + 1] = utf8;
                utf16 += 2;
                utf8 += 4;
                offsets[utf16] = utf8;
                continue;
            }

            utf8 += value <= 0x7f ? 1 : value <= 0x7ff ? 2 : 3;
            utf16++;
            offsets[utf16] = utf8;
        }

        return offsets;
    }

    private int[]? GetUtf8Offsets(string input) => InputBytes.Length == input.Length
        ? null
        : BuildUtf8Offsets(input);

    private static int Checksum(Utf8PythonValueMatch match) => match.Success
        ? Combine(1, match.StartOffsetInUtf16, match.EndOffsetInUtf16)
        : 0;

    private static int Checksum(Match match) => match.Success
        ? Combine(1, match.Index, match.Index + match.Length)
        : 0;

    private static int ChecksumAtStart(Match match) => match.Success && match.Index == 0
        ? Checksum(match)
        : 0;

    private static int Checksum(Utf8PythonDetailedMatchData match)
    {
        var checksum = match.Success ? 1 : 0;
        foreach (var group in match.Groups ?? [])
        {
            checksum = Combine(checksum, group.Success ? 1 : 0, group.StartOffsetInUtf16, group.EndOffsetInUtf16, Checksum(group.ValueText));
        }

        return checksum;
    }

    private static int Checksum(BclDetailedMatch match)
    {
        var checksum = match.Groups.Length == 0 ? 0 : 1;
        foreach (var group in match.Groups)
        {
            checksum = Combine(checksum, group.Success ? 1 : 0, group.StartOffsetInUtf16, group.EndOffsetInUtf16, Checksum(group.Value));
        }

        return checksum;
    }

    private static int Checksum(Utf8PythonDetailedMatchData[] matches)
    {
        var checksum = matches.Length;
        foreach (var match in matches)
        {
            checksum = Combine(checksum, Checksum(match));
        }

        return checksum;
    }

    private static int Checksum(BclDetailedMatch[] matches)
    {
        var checksum = matches.Length;
        foreach (var match in matches)
        {
            checksum = Combine(checksum, Checksum(match));
        }

        return checksum;
    }

    private static int Checksum(Utf8PythonFindAllResult result) =>
        Checksum(result.Shape, result.ScalarValues, result.TupleValues);

    private static int Checksum(BclFindAllResult result) =>
        Checksum(result.Shape, result.ScalarValues, result.TupleValues);

    private static int Checksum(Utf8PythonFindAllUtf8Result result) =>
        Checksum(result.Shape, result.ScalarValues, result.TupleValues);

    private static int Checksum(BclFindAllUtf8Result result) =>
        Checksum(result.Shape, result.ScalarValues, result.TupleValues);

    private static int Checksum(Utf8PythonFindAllShape shape, string[] scalarValues, string[][] tupleValues)
    {
        var checksum = (int)shape;
        foreach (var value in scalarValues)
        {
            checksum = Combine(checksum, Checksum(value));
        }

        foreach (var tuple in tupleValues)
        {
            checksum = Combine(checksum, tuple.Length);
            foreach (var value in tuple)
            {
                checksum = Combine(checksum, Checksum(value));
            }
        }

        return checksum;
    }

    private static int Checksum(Utf8PythonFindAllShape shape, byte[][] scalarValues, byte[][][] tupleValues)
    {
        var checksum = (int)shape;
        foreach (var value in scalarValues)
        {
            checksum = Combine(checksum, Checksum(value));
        }

        foreach (var tuple in tupleValues)
        {
            checksum = Combine(checksum, tuple.Length);
            foreach (var value in tuple)
            {
                checksum = Combine(checksum, Checksum(value));
            }
        }

        return checksum;
    }

    private static int Checksum(Utf8PythonSubnResult result) =>
        Combine(Checksum(result.ResultText), result.ReplacementCount);

    private static int Checksum(Utf8PythonSubnUtf8Result result) =>
        Combine(Checksum(result.ResultBytes), result.ReplacementCount);

    private static int Checksum(BclSubnResult result)
    {
        var checksum = result.ResultBytes is null
            ? Combine(Checksum(result.ResultText), result.ReplacementCount)
            : Combine(Checksum(result.ResultBytes), result.ReplacementCount);
        return result.CallbackChecksum is int callbackChecksum
            ? Combine(checksum, callbackChecksum)
            : checksum;
    }

    private static int Checksum(string?[] values)
    {
        var checksum = values.Length;
        foreach (var value in values)
        {
            checksum = Combine(checksum, value is null ? -1 : Checksum(value));
        }

        return checksum;
    }

    private static int Checksum(string value)
    {
        var checksum = value.Length;
        foreach (var character in value)
        {
            checksum = Combine(checksum, character);
        }

        return checksum;
    }

    private static int Checksum(byte[] value)
    {
        var checksum = value.Length;
        foreach (var item in value)
        {
            checksum = Combine(checksum, item);
        }

        return checksum;
    }

    private static int Combine(int seed, int value) => unchecked((seed * 31) + value);

    private static int Combine(int seed, int value1, int value2) =>
        Combine(Combine(seed, value1), value2);

    private static int Combine(int seed, int value1, int value2, int value3, int value4) =>
        Combine(Combine(Combine(Combine(seed, value1), value2), value3), value4);

    private static RegexOptions ToRegexOptions(PythonReCompileOptions options)
    {
        var result = RegexOptions.CultureInvariant;
        if ((options & PythonReCompileOptions.IgnoreCase) != 0)
        {
            result |= RegexOptions.IgnoreCase;
        }
        if ((options & PythonReCompileOptions.Multiline) != 0)
        {
            result |= RegexOptions.Multiline;
        }
        if ((options & PythonReCompileOptions.DotAll) != 0)
        {
            result |= RegexOptions.Singleline;
        }
        if ((options & PythonReCompileOptions.Verbose) != 0)
        {
            result |= RegexOptions.IgnorePatternWhitespace;
        }
        if ((options & PythonReCompileOptions.Ascii) != 0)
        {
            result |= RegexOptions.ECMAScript;
        }

        return result;
    }
}

internal sealed record BclFindAllResult(Utf8PythonFindAllShape Shape, string[] ScalarValues, string[][] TupleValues);

internal sealed record BclFindAllUtf8Result(Utf8PythonFindAllShape Shape, byte[][] ScalarValues, byte[][][] TupleValues);

internal sealed record BclDetailedMatch(BclDetailedGroup[] Groups);

internal readonly record struct BclDetailedGroup(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int StartOffsetInUtf16,
    int EndOffsetInUtf16,
    string Value);

internal sealed record BclSubnResult(
    string ResultText,
    byte[]? ResultBytes,
    int ReplacementCount,
    int? CallbackChecksum);

internal sealed class PythonReBenchmarkSnapshot
{
    public required int SchemaVersion { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required PythonReCorpusProvenance Corpus { get; init; }
    public required SortedDictionary<string, PythonReCaseMeasurement> Cases { get; init; }
}

internal sealed class PythonReCorpusProvenance
{
    public required string SourceFile { get; init; }
    public required string Sha256 { get; init; }
    public required int VectorCount { get; init; }
    public required string UpstreamCpythonRevision { get; init; }
    public required string Limitation { get; init; }
}

internal sealed class PythonReCaseMeasurement
{
    public required string Pattern { get; init; }
    public required string Options { get; init; }
    public required string Operation { get; init; }
    public required int InputUtf8Bytes { get; init; }
    public required int EffectiveIterations { get; init; }
    public required int Samples { get; init; }
    public required bool IncludesResultMaterialization { get; init; }
    public required PythonReBenchmarkEnvironment Environment { get; init; }
    public required PythonReOperationMeasurement PythonRe { get; init; }
    public required PythonReOperationMeasurement DecodeThenRegex { get; init; }
    public required PythonReOperationMeasurement PredecodedRegex { get; init; }
}

internal sealed class PythonReOperationMeasurement
{
    public required double MedianMicroseconds { get; init; }
    public required double MinimumMicroseconds { get; init; }
    public required double MaximumMicroseconds { get; init; }
    public required long MedianAllocatedBytes { get; init; }
    public required int WarmupCalls { get; init; }
    public required double WarmupMilliseconds { get; init; }
}

internal sealed class PythonReBenchmarkEnvironment
{
    public required string SourceCommit { get; init; }
    public required bool TrackedDirty { get; init; }
    public required bool HasUntrackedFiles { get; init; }
    public required string Runtime { get; init; }
    public required string OperatingSystem { get; init; }
    public required string Processor { get; init; }
}
