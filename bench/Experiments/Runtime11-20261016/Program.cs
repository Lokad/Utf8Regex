using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using OwnedRegex = Lokad.Utf8Regex.Utf8Regex;

if (args.Length < 2 || args[0] is not ("--verify" or "--sample")) throw new ArgumentException();
var verifyOnly = args[0] == "--verify";
var ordinal = verifyOnly ? 0 : int.Parse(args[1]);
var outputPath = verifyOnly ? args[1] : args[2];
using var output = new StreamWriter(File.Open(outputPath, FileMode.CreateNew)) { AutoFlush = true };
void Emit(object value) => output.WriteLine(JsonSerializer.Serialize(value));
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
var productHash = Hash(typeof(OwnedRegex).Assembly.Location);
if (productHash != "9453ACFFFC0E1C18A30FE8B1096481E48503A8089134A2EE5B1BBFC2CC99FBE9")
    throw new InvalidOperationException("Wrong frozen product DLL.");
var metadata = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
    .ToDictionary(item => item.Key, item => item.Value);
Emit(new { Kind = "identity", Framework = RuntimeInformation.FrameworkDescription,
    Runtime = Environment.Version.ToString(), RuntimeCommit = typeof(object).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    RegexVersion = typeof(Regex).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    Metadata = metadata, ProductSha256 = productHash, HarnessSha256 = Hash(Assembly.GetExecutingAssembly().Location),
    Tiering = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
    ReadyToRun = Environment.GetEnvironmentVariable("DOTNET_ReadyToRun"), Ordinal = ordinal });
Emit(new { Kind = "protocol", Samples = 5, WarmMilliseconds = 120, TargetMilliseconds = 80,
    MinimumMilliseconds = 50, MaximumIterations = 2_000_000,
    Work = 50_000_000, Depth = 1024, WorkingBytes = 64UL * 1048576, ResultBytes = 128UL * 1048576,
    Contract = "Public Count; ASCII overlapping semantics; fresh per-SDK generated comparators; identical product bytes; constructors/startup excluded; raw process order retained; no Status/snapshot or full-runtime qualification" });

var cases = new[]
{
    new Case("literal-late", "needle", new string('x', 65536) + "needle", 1, Definitions.Literal),
    new Case("fixed-width", "ab[0-9]{2}", string.Concat(Enumerable.Repeat(" xx ab12 ab34 ", 1000)), 2000, Definitions.FixedWidth),
    new Case("optional-prefix", "a?b[0-9]{2}", string.Concat(Enumerable.Repeat(" xx b12 ab34 ", 1000)), 2000, Definitions.OptionalPrefix),
    new Case("shadowed-family", "foo|foobar", string.Concat(Enumerable.Repeat(" foo foobar ", 1000)), 2000, Definitions.Family),
};
var policy = new Utf8ResourcePolicy { MaxWorkSteps = 50_000_000, MaxDepth = 1024,
    MaxWorkingBytes = 64UL * 1048576, MaxResultBytes = 128UL * 1048576 };
var strictUtf8 = new UTF8Encoding(false, true);
using var tokens = new CancellationTokenSource();

// The supplementary scalar is deliberately outside code-unit parity rows.
var scalar = new OwnedRegex(".");
if (scalar.Count("😀"u8) != 1 || new Regex(".").Count("😀") != 2) throw new Exception("Scalar sentinel failed.");
var scalarMatches = scalar.EnumerateMatches("😀"u8);
if (!scalarMatches.MoveNext() || scalarMatches.Current.IndexInBytes != 0 || scalarMatches.Current.LengthInBytes != 4)
    throw new Exception("Scalar byte coordinates failed.");
try { scalar.Count(new byte[] { 255 }); throw new Exception("Malformed subject admitted."); }
catch (ArgumentException) { }
using (var canceled = new CancellationTokenSource())
{
    canceled.Cancel();
    try { scalar.Count("😀"u8, canceled.Token); throw new Exception("Cancellation ignored."); }
    catch (OperationCanceledException error) when (error.CancellationToken == canceled.Token) { }
}
Emit(new { Kind = "exclusion", Pattern = ".", Subject = "😀", OwnedCount = 1, BclCount = 2,
    Reason = "Complete scalar versus UTF-16 code units; not a timing/parity row", StrictMalformedRejected = true, SynchronousCancellation = true });

IEnumerable<Case> orderedCases = ordinal % 2 == 0 ? cases : cases.Reverse();
foreach (var item in orderedCases)
{
    var input = strictUtf8.GetBytes(item.Text);
    if (input.Any(value => value >= 128)) throw new Exception("Comparison row is not ASCII.");
    var conventional = new Regex(item.Pattern, RegexOptions.CultureInvariant);
    var compiled = new Regex(item.Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
    var generated = item.Generated();
    var nonbacktracking = new Regex(item.Pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    var expectedRanges = conventional.EnumerateMatches(item.Text).SelectRanges();
    foreach (var baseline in new[] { compiled, generated, nonbacktracking })
        if (baseline.EnumerateMatches(item.Text).SelectRanges() != expectedRanges) throw new Exception("BCL range digest mismatch.");
    var lanes = new List<Lane>();
    foreach (var ownedOptions in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
    {
        var name = (ownedOptions & RegexOptions.Compiled) == 0 ? "core/ordinary" : "core/compiled";
        var unlimited = new OwnedRegex(item.Pattern, ownedOptions, Regex.InfiniteMatchTimeout);
        var finite = new OwnedRegex(item.Pattern, ownedOptions, Regex.InfiniteMatchTimeout, policy);
        foreach (var owner in new[] { unlimited, finite })
        {
            var cursor = owner.EnumerateMatches(input, tokens.Token);
            var ranges = new StringBuilder();
            while (cursor.MoveNext()) ranges.Append(cursor.Current.IndexInBytes).Append(':').Append(cursor.Current.LengthInBytes).Append(';');
            if (ranges.ToString() != expectedRanges) throw new Exception("Owned range digest mismatch.");
        }
        lanes.Add(new(name + "/unlimited", () => unlimited.Count(input)));
        lanes.Add(new(name + "/finite", () => finite.Count(input)));
        lanes.Add(new(name + "/finite+token", () => finite.Count(input, tokens.Token)));
    }
    foreach (var (name, baseline) in new[] { ("bcl/ordinary", conventional), ("bcl/compiled", compiled),
        ("bcl/generated", generated), ("bcl/nonbacktracking", nonbacktracking) })
    {
        lanes.Add(new(name + "/predecoded", () => baseline.Count(item.Text)));
        lanes.Add(new(name + "/decode", () => baseline.Count(strictUtf8.GetString(input))));
    }
    foreach (var lane in lanes)
        if (lane.Call() != item.Expected) throw new Exception($"Wrong Count for {item.Id}/{lane.Name}.");
    Emit(new { Kind = "preflight", item.Id, item.Pattern, InputBytes = input.Length, item.Expected,
        SubjectSha256 = Convert.ToHexString(SHA256.HashData(input)),
        RangeDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedRanges))),
        Lanes = lanes.Select(lane => lane.Name).ToArray() });
    if (verifyOnly) continue;
    if (ordinal % 2 != 0) lanes.Reverse();
    foreach (var lane in lanes)
    {
        var watch = Stopwatch.StartNew();
        do { for (var warm = 0; warm < 16; warm++) Sink.Value = lane.Call(); } while (watch.ElapsedMilliseconds < 120);
        var iterations = 32;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var calibration = Measure(lane.Call, iterations);
            if (calibration.Milliseconds is >= 65 and <= 150) break;
            iterations = (int)Math.Clamp(Math.Ceiling(iterations * 80 / Math.Max(.001, calibration.Milliseconds)), 1, 2_000_000);
        }
        var sample = Measure(lane.Call, iterations);
        Emit(new { Kind = "sample", item.Id, Lane = lane.Name, Ordinal = ordinal, sample.Nanoseconds,
            sample.AllocatedBytes, sample.Milliseconds, sample.Iterations });
    }
}
Emit(new { Kind = "complete" });

static Sample Measure(Func<int> call, int count)
{
    var bytes = GC.GetAllocatedBytesForCurrentThread();
    var start = Stopwatch.GetTimestamp();
    var sum = 0;
    for (var i = 0; i < count; i++) sum = unchecked(sum + call());
    var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
    Sink.Value = sum;
    return new(elapsed * 1e6 / count, (double)allocated / count, elapsed, count);
}
record Case(string Id, string Pattern, string Text, int Expected, Func<Regex> Generated);
record Lane(string Name, Func<int> Call);
record Sample(double Nanoseconds, double AllocatedBytes, double Milliseconds, int Iterations);
static class Sink { internal static int Value; }
static class RangeDigest
{
    internal static string SelectRanges(this Regex.ValueMatchEnumerator matches)
    {
        var ranges = new StringBuilder();
        foreach (var match in matches) ranges.Append(match.Index).Append(':').Append(match.Length).Append(';');
        return ranges.ToString();
    }
}
static partial class Definitions
{
    [GeneratedRegex("needle", RegexOptions.CultureInvariant)] internal static partial Regex Literal();
    [GeneratedRegex("ab[0-9]{2}", RegexOptions.CultureInvariant)] internal static partial Regex FixedWidth();
    [GeneratedRegex("a?b[0-9]{2}", RegexOptions.CultureInvariant)] internal static partial Regex OptionalPrefix();
    [GeneratedRegex("foo|foobar", RegexOptions.CultureInvariant)] internal static partial Regex Family();
}
