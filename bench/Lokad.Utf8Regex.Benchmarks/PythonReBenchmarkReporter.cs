using System.Diagnostics;
using System.Runtime.InteropServices;
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
            SchemaVersion = 1,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Cases = measurements,
        };
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SnapshotFileName, json + Environment.NewLine, new UTF8Encoding(false));
        Console.WriteLine();
        Console.WriteLine($"Snapshot           : {Path.GetFullPath(SnapshotFileName)}");
        return 0;
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
            EffectiveIterations = iterations,
            Samples = samples,
            IncludesResultMaterialization = benchmarkCase.IncludesResultMaterialization,
            Environment = CaptureEnvironment(),
            PythonRe = MeasureOperation(context.ExecutePythonRe, iterations, samples),
            DecodeThenRegex = MeasureOperation(context.ExecuteDecodeThenRegex, iterations, samples),
            PredecodedRegex = MeasureOperation(context.ExecutePredecodedRegex, iterations, samples),
        };
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
    Count = 1,
    FindAll = 2,
    Replace = 3,
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
        new("family/count", "cat|dog|bird", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("cat fox dog owl bird ", 4_096), string.Empty, false),
        new("class-run/count", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("alpha beta gamma 123 ", 4_096), string.Empty, false),
        new("unicode/count", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("Шерлок и Ватсон. ", 4_096), string.Empty, false),
        new("enumeration/findall", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAll,
            Repeat("alpha beta gamma 123 ", 1_024), string.Empty, true),
        new("capture/findall", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAll,
            Repeat("item-12 other-345 ", 1_024), string.Empty, true),
        new("zero-width/count", @"\b", PythonReCompileOptions.Ascii, PythonReBenchmarkOperation.Count,
            Repeat("alpha beta gamma ", 1_024), string.Empty, false),
        new("replacement/replace", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.Replace,
            Repeat("cat fox cat dog ", 2_048), "tiger", true),
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
    private readonly string _decoded;

    internal PythonReBenchmarkContext(PythonReBenchmarkCase benchmarkCase)
    {
        _case = benchmarkCase;
        InputBytes = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        _decoded = benchmarkCase.Input;
        _pythonRegex = new Utf8PythonRegex(benchmarkCase.Pattern, benchmarkCase.Options);
        _regex = new Regex(benchmarkCase.Pattern, ToRegexOptions(benchmarkCase.Options), Regex.InfiniteMatchTimeout);
    }

    internal byte[] InputBytes { get; }

    internal int ExecutePythonRe() => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => _pythonRegex.IsMatch(InputBytes) ? 1 : 0,
        PythonReBenchmarkOperation.Count => _pythonRegex.Count(InputBytes),
        PythonReBenchmarkOperation.FindAll => _pythonRegex.FindAll(InputBytes).Length,
        PythonReBenchmarkOperation.Replace => _pythonRegex.Replace(InputBytes, _case.Replacement).Length,
        _ => throw new InvalidOperationException(),
    };

    internal int ExecuteDecodeThenRegex()
    {
        var decoded = Encoding.UTF8.GetString(InputBytes);
        return ExecuteRegex(decoded, encodeReplacement: true);
    }

    internal int ExecutePredecodedRegex() => ExecuteRegex(_decoded, encodeReplacement: false);

    private int ExecuteRegex(string input, bool encodeReplacement) => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => _regex.IsMatch(input) ? 1 : 0,
        PythonReBenchmarkOperation.Count => _regex.Count(input),
        PythonReBenchmarkOperation.FindAll => _regex.Matches(input).Count,
        PythonReBenchmarkOperation.Replace when encodeReplacement =>
            Encoding.UTF8.GetByteCount(_regex.Replace(input, _case.Replacement)),
        PythonReBenchmarkOperation.Replace => _regex.Replace(input, _case.Replacement).Length,
        _ => throw new InvalidOperationException(),
    };

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

internal sealed class PythonReBenchmarkSnapshot
{
    public required int SchemaVersion { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required SortedDictionary<string, PythonReCaseMeasurement> Cases { get; init; }
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
