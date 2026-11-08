using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;

var startup = Environment.ProcessorCount;
using var process = Process.GetCurrentProcess();
if (OperatingSystem.IsLinux()) process.ProcessorAffinity = (nint)4;
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
var assembly = typeof(Utf8Regex).Assembly;
if (Hash(assembly.Location) != args[1] || AssemblyLoadContext.GetLoadContext(assembly) != AssemblyLoadContext.Default)
    throw new Exception("Wrong producer");
foreach (var name in new[] { "DOTNET_TieredCompilation", "COMPlus_TieredCompilation", "DOTNET_TieredPGO", "COMPlus_TieredPGO", "DOTNET_JitDisasm" })
    if (Environment.GetEnvironmentVariable(name) is not null) throw new Exception("Timing override: " + name);
var preflight = args.Contains("--preflight");
var clock = Stopwatch.StartNew();
using var live = new CancellationTokenSource();
const int count = 65536;
const string pattern = "^[A-Z]{3}[0-9]{4}$";
var positive = Encoding.UTF8.GetBytes("ABC0123");
var negative = Encoding.UTF8.GetBytes("abc0123");
var mixed = Enumerable.Range(0, count).Select(i => i % 2 == 0 ? positive : negative).ToArray();
var positives = Enumerable.Repeat(positive, count).ToArray();
var negatives = Enumerable.Repeat(negative, count).ToArray();
var unicode = Enumerable.Range(0, count).Select(i => Encoding.UTF8.GetBytes(i % 2 == 0 ? "é😀0123" : "é😀012x")).ToArray();
var regex = new Utf8Regex(pattern);
var rows = new List<object>();
var retainedBefore = GC.GetTotalMemory(true);
Measure("mixed", () => Batch(regex, mixed), count / 2);
Measure("positive", () => Batch(regex, positives), count);
Measure("negative", () => Batch(regex, negatives), 0);
var unicodeRegex = new Utf8Regex("^(?:[A-Z]{3}|é😀)[0-9]{4}$");
Measure("unicode", () => Batch(unicodeRegex, unicode), count / 2);
var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 4096,
    MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
var finite = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, policy);
Measure("finite-mixed", () => Batch(finite, mixed), count / 2);
var deadline = new Utf8Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(30), policy);
Measure("finite-deadline-mixed", () => Batch(deadline, mixed), count / 2);
var bcl = new Regex(pattern, RegexOptions.Compiled);
var text = Enumerable.Range(0, count).Select(i => i % 2 == 0 ? "ABC0123" : "abc0123").ToArray();
Measure("unchanged-bcl-control", () => { var hits = 0; foreach (var subject in text) if (bcl.IsMatch(subject)) hits++; return hits; }, count / 2);
object? fundedEvidence = null;
#if FUNDED
var account = new Account(1 << 20);
Measure("funded-finite-mixed", () => {
    var hits = 0;
    foreach (var subject in mixed) if (finite.IsMatch(subject, live.Token, account)) hits++;
    if (account.Used != 0) throw new Exception("Leaked working loan");
    return hits;
}, count / 2);
var refused = new Account(0);
if (!finite.IsMatch(positive, live.Token, refused) || refused.Used != 0)
    throw new Exception("Allocation-free matching must not borrow storage");
var needsStorage = new Utf8Regex("(?=(a+))a+");
try { needsStorage.IsMatch("aaaa"u8, live.Token, refused); throw new Exception("Expected admission refusal"); }
catch (Utf8MemoryAdmissionException) { }
if (refused.Used != 0) throw new Exception("Refusal leaked working loan");
fundedEvidence = new { account.Peak, account.Reservations, Outstanding = account.Used, RefusalOutstanding = refused.Used };
#endif
var malformed = new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'0', (byte)'1', (byte)'2', (byte)'3', 0xff };
try { regex.IsMatch(malformed, live.Token); throw new Exception("Expected malformed UTF-8 failure"); }
catch (ArgumentException) { }
using var canceled = new CancellationTokenSource(); canceled.Cancel();
try { regex.IsMatch(positive, canceled.Token); throw new Exception("Expected cancellation"); }
catch (OperationCanceledException error) when (error.CancellationToken == canceled.Token) { }
var denied = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, new() { MaxWorkSteps = 1 });
try { denied.IsMatch(positive, live.Token); throw new Exception("Expected work refusal"); }
catch (Utf8ResourceLimitExceededException) { }
Console.WriteLine(JsonSerializer.Serialize(new {
    Label = args[0], Preflight = preflight, ElapsedSeconds = clock.Elapsed.TotalSeconds,
    Runtime = RuntimeInformation.FrameworkDescription, StartupProcessors = startup,
    Affinity = OperatingSystem.IsLinux() ? "4" : "unrestricted-smoke",
    CoreSha256 = Hash(assembly.Location), WorkerSha256 = Hash(Assembly.GetExecutingAssembly().Location),
    Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    LoadedCoreAssemblies = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Lokad.Utf8Regex"),
    ManagedHeapDeltaAfterCollection = GC.GetTotalMemory(true) - retainedBefore,
    HeapDeltaScope = "Whole worker including prepared controls/pools; not exact regex retained storage",
    Funded = fundedEvidence, Rows = rows
}, new JsonSerializerOptions { WriteIndented = true }));

int Batch(Utf8Regex owner, byte[][] subjects) {
    var hits = 0;
    foreach (var subject in subjects) if (owner.IsMatch(subject, live.Token)) hits++;
    return hits;
}
void Measure(string name, Func<int> call, int expected) {
    if (call() != expected) throw new Exception("Result mismatch: " + name);
    if (preflight) { rows.Add(new { Name = name, Expected = expected }); return; }
    var warm = Stopwatch.StartNew(); var warmBatches = 0;
    do { if (call() != expected) throw new Exception("Warm mismatch"); warmBatches++; }
    while (warm.ElapsedMilliseconds < 500 || warmBatches < 4);
    warm.Stop();
    var iterations = 1;
    var calibration = Sample(iterations, call, expected);
    while (calibration.Milliseconds < 20 && iterations < 2048) {
        iterations *= 2; calibration = Sample(iterations, call, expected);
    }
    iterations = (int)Math.Clamp(Math.Ceiling(iterations * 75 / Math.Max(calibration.Milliseconds, .000001)), 1, 2048);
    var observations = new List<object>();
    for (var sample = 0; sample < 5; sample++) {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var value = Sample(iterations, call, expected);
        if (value.Milliseconds < 20) throw new Exception("Short sample: " + name);
        observations.Add(new { value.Milliseconds, NanosecondsPerSubject = value.Milliseconds * 1_000_000 / (iterations * (double)count),
            BytesPerSubject = value.Bytes / (iterations * (double)count) });
    }
    rows.Add(new { Name = name, SubjectsPerBatch = count, Expected = expected, Iterations = iterations,
        WarmBatches = warmBatches, WarmMilliseconds = warm.Elapsed.TotalMilliseconds, Samples = observations });
}
static (double Milliseconds, long Bytes) Sample(int iterations, Func<int> call, int expected) {
    var results = new int[iterations];
    var allocated = GC.GetAllocatedBytesForCurrentThread(); var stamp = Stopwatch.GetTimestamp();
    for (var i = 0; i < iterations; i++) results[i] = call();
    var elapsed = Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
    foreach (var result in results) if (result != expected) throw new Exception("Timed result mismatch");
    GC.KeepAlive(results); return (elapsed, allocated);
}
#if FUNDED
sealed class Account(ulong limit) : IUtf8MemoryAllowance {
    public ulong Used, Peak, Reservations;
    public bool TryReserve(ulong bytes) {
        if (bytes > limit - Used) return false;
        Used += bytes; Peak = Math.Max(Peak, Used); Reservations++; return true;
    }
    public void Release(ulong bytes) { if (bytes > Used) throw new Exception("Invalid refund"); Used -= bytes; }
}
#endif
