using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Posix;
using Lokad.Utf8Regex.Benchmarks;

var startupProcessors = Environment.ProcessorCount;
using var process = Process.GetCurrentProcess();
if (OperatingSystem.IsLinux()) process.ProcessorAffinity = (nint)4;
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
var core = typeof(Utf8Regex).Assembly;
var posix = typeof(Utf8PosixRegex).Assembly;
if (Hash(core.Location) != args[1] || Hash(posix.Location) != args[2] ||
    AssemblyLoadContext.GetLoadContext(core) != AssemblyLoadContext.Default ||
    AssemblyLoadContext.GetLoadContext(posix) != AssemblyLoadContext.Default) throw new Exception("Wrong producer");
foreach (var name in new[] { "DOTNET_TieredCompilation", "COMPlus_TieredCompilation", "DOTNET_TieredPGO", "COMPlus_TieredPGO", "DOTNET_JitDisasm" })
    if (Environment.GetEnvironmentVariable(name) is not null) throw new Exception("Timing override: " + name);
var preflight = args.Contains("--preflight");
var clock = Stopwatch.StartNew();
using var token = new CancellationTokenSource();
var finite = new Utf8ResourcePolicy { MaxWorkSteps = 10_000_000, MaxDepth = 4096, MaxWorkingBytes = 16 << 20, MaxResultBytes = 1 };
var rows = new List<object>();
foreach (var id in new[] { "capture/empty", "capture/sku007", "capture/ambiguous", "capture/repeated", "capture/unicode", "is-match/hit", "count/ambiguous" })
    MeasurePosix(id, "unlimited");
foreach (var lane in new[] { "finite", "funded", "deadline" }) MeasurePosix("capture/sku007", lane);
var coreControl = new Utf8Regex("abc");
var controlInput = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abc ", 64)));
Measure("unchanged/Core-literal-count", () => new(coreControl.Count(controlInput), default, default, default), new(64, default, default, default), null);
Console.WriteLine(JsonSerializer.Serialize(new {
    Label = args[0], Preflight = preflight, Seconds = clock.Elapsed.TotalSeconds,
    Runtime = RuntimeInformation.FrameworkDescription, StartupProcessors = startupProcessors,
    Affinity = OperatingSystem.IsLinux() ? "4" : "unrestricted-smoke",
    CoreSha256 = Hash(core.Location), PosixSha256 = Hash(posix.Location), WorkerSha256 = Hash(Assembly.GetExecutingAssembly().Location),
    CoreVersion = core.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    PosixVersion = posix.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    LoadedCoreAssemblies = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Lokad.Utf8Regex"),
    LoadedPosixAssemblies = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Lokad.Utf8Regex.Posix"), Rows = rows,
}, new JsonSerializerOptions { WriteIndented = true }));

void MeasurePosix(string id, string lane)
{
    var row = PosixBenchmarkCatalog.Get(id);
    var regex = lane == "unlimited" ? new Utf8PosixRegex(row.Pattern) :
        new Utf8PosixRegex(row.Pattern, default, lane == "deadline" ? TimeSpan.FromSeconds(30) : Timeout.InfiniteTimeSpan, finite);
    var input = Encoding.UTF8.GetBytes(row.Input);
    var groups = new Utf8ValueMatch[regex.GroupCount + 1];
    var account = lane == "funded" ? new Account() : null;
    var cancellation = lane == "unlimited" ? default : token.Token;
    PosixBenchmarkResult Call()
    {
        if (row.Operation == PosixBenchmarkOperation.IsMatch)
            return new(regex.IsMatch(input, cancellation, account) ? 1 : 0, default, default, default);
        if (row.Operation == PosixBenchmarkOperation.Count)
            return new(regex.Count(input, row.StartOffsetInBytes, cancellation, account), default, default, default);
        Utf8ValueMatch whole = default;
        for (var record = 0; record < row.Records; record++) whole = regex.Match(input, groups, row.StartOffsetInBytes, cancellation, account);
        return new(whole.Success ? row.Records : 0, whole, groups.Length > 1 ? groups[1] : default, groups.Length > 2 ? groups[2] : default);
    }
    Measure(id + "/" + lane, Call, PosixBenchmarkCatalog.Expected(id), account);
}

void Measure(string name, Func<PosixBenchmarkResult> call, PosixBenchmarkResult expected, Account? account)
{
    void Check(PosixBenchmarkResult value) { if (value != expected || account?.Used > 0) throw new Exception("Result/loan mismatch: " + name); }
    Check(call());
    if (preflight) { rows.Add(new { Name = name, ExpectedCount = expected.Count, FullResultVerified = true }); return; }
    var warm = Stopwatch.StartNew();
    do { Check(call()); } while (warm.ElapsedMilliseconds < 500);
    var iterations = 1;
    var calibration = Sample(iterations);
    while (calibration.Milliseconds < 10 && iterations < 1_048_576) { iterations *= 2; calibration = Sample(iterations); }
    iterations = (int)Math.Clamp(Math.Ceiling(iterations * 75 / Math.Max(calibration.Milliseconds, .000001)), 1, 1_048_576);
    var confirmation = Sample(iterations);
    if (confirmation.Milliseconds < 60) iterations = (int)Math.Clamp(Math.Ceiling(iterations * 75 / Math.Max(confirmation.Milliseconds, .000001)), 1, 1_048_576);
    var observations = new List<object>();
    for (var sample = 0; sample < 5; sample++)
    {
        var value = Sample(iterations);
        if (value.Milliseconds < 20) throw new Exception("Short sample: " + name);
        observations.Add(new { value.Milliseconds, Microseconds = value.Milliseconds * 1000 / iterations, Bytes = value.Bytes / (double)iterations });
    }
    rows.Add(new { Name = name, Iterations = iterations, Samples = observations, PeakWorking = account?.Peak ?? 0, Outstanding = account?.Used ?? 0 });
    (double Milliseconds, long Bytes) Sample(int count)
    {
        PosixBenchmarkResult result = default;
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var stamp = Stopwatch.GetTimestamp();
        for (var iteration = 0; iteration < count; iteration++) result = call();
        var elapsed = Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Check(result); GC.KeepAlive(result);
        return (elapsed, bytes);
    }
}

sealed class Account : IUtf8MemoryAllowance
{
    private readonly object gate = new();
    public ulong Used, Peak;
    public bool TryReserve(ulong bytes)
    { lock (gate) { if (bytes > (16UL << 20) - Used) return false; Used += bytes; Peak = Math.Max(Peak, Used); return true; } }
    public void Release(ulong bytes) { lock (gate) { if (bytes > Used) throw new Exception("Invalid refund"); Used -= bytes; } }
}
