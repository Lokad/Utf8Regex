using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

void Emit(object value) => Console.WriteLine(JsonSerializer.Serialize(value));
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
Emit(new { Kind = "identity", Core = Hash(typeof(Utf8ResourcePolicy).Assembly.Location),
    Python = Hash(typeof(Utf8PythonRegex).Assembly.Location), Harness = Hash(typeof(Program).Assembly.Location),
    Runtime = Environment.Version.ToString(), Tiering = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") });
var policy = new Utf8ResourcePolicy { MaxWorkSteps = 50_000_000, MaxDepth = 1024,
    MaxWorkingBytes = 64UL * 1048576, MaxResultBytes = 128UL * 1048576 };
using var live = new CancellationTokenSource();
var lanes = new List<Lane>();
foreach (var item in new[] {
    (Id: "named", Pattern: "(?P<word>[a-z]+)", Unit: "alpha beta gamma 123 ", Repeats: 256,
        Shape: Utf8PythonFindAllShape.SingleGroup, Values: new[] { "alpha", "beta", "gamma" }),
    (Id: "tuple", Pattern: "([a-z]+)([0-9]+)", Unit: "alpha12 beta34 gamma56 ", Repeats: 128,
        Shape: Utf8PythonFindAllShape.GroupTuple, Values: new[] { "alpha", "12", "beta", "34", "gamma", "56" }),
    (Id: "empty-scalar", Pattern: "(😀)?", Unit: "😀é", Repeats: 128,
        Shape: Utf8PythonFindAllShape.SingleGroup, Values: new[] { "😀", "" }),
    (Id: "exact-control", Pattern: "alpha", Unit: "alpha ", Repeats: 768,
        Shape: Utf8PythonFindAllShape.FullMatch, Values: new[] { "alpha" }) })
{
    var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(item.Unit, item.Repeats)));
    var expected = Enumerable.Range(0, item.Repeats).SelectMany(_ => item.Values).ToList();
    if (item.Id == "empty-scalar") expected.Add("");
    foreach (var mode in new[] { "unlimited", "finite", "finite-token" })
    {
        var regex = new Utf8PythonRegex(item.Pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            mode == "unlimited" ? Utf8ResourcePolicy.Unlimited : policy);
        var token = mode == "finite-token" ? live.Token : default;
        Utf8PythonFindAllResult Call() => regex.FindAllToStrings(input, 0, token);
        var found = Call();
        var flat = found.Shape == Utf8PythonFindAllShape.GroupTuple ? found.TupleValues.SelectMany(x => x) : found.ScalarValues;
        if (found.Shape != item.Shape || !flat.SequenceEqual(expected)) throw new Exception("Values or shape differ");
        var name = $"{item.Id}/{mode}";
        Emit(new { Kind = "preflight", Lane = name, Bytes = input.Length, Count = found.Count, Shape = found.Shape.ToString(),
            Subject = HashBytes(input), Values = HashBytes(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expected))),
            UnitMinimumWork = MinimumWork(item.Pattern, item.Unit, policy) });
        lanes.Add(new(name, Call, found.Count));
    }
}
if (args.Contains("--verify")) { Emit(new { Kind = "complete", VerifyOnly = true }); return; }
var counts = new Dictionary<string, int>();
foreach (var lane in lanes)
{
    var warm = Stopwatch.StartNew();
    do { Sink.Value = lane.Call(); } while (warm.ElapsedMilliseconds < 120);
    var n = 32;
    for (var attempt = 0; attempt < 6; attempt++)
    {
        var elapsed = Measure(lane, n).Ms;
        if (elapsed >= 65 && elapsed <= 150) break;
        n = (int)Math.Clamp(Math.Ceiling(n * 80 / Math.Max(.001, elapsed)), 1, 2_000_000);
    }
    counts[lane.Name] = n;
}
for (var ordinal = 0; ordinal < 5; ordinal++)
foreach (var lane in ordinal % 2 == 0 ? lanes : lanes.AsEnumerable().Reverse())
{
    var sample = Measure(lane, counts[lane.Name]);
    Emit(new { Kind = "sample", Lane = lane.Name, Ordinal = ordinal, sample.Ns, sample.Bytes, sample.Ms, Iterations = counts[lane.Name] });
}
Emit(new { Kind = "complete" });

static string HashBytes(byte[] input) => Convert.ToHexString(SHA256.HashData(input));
static (double Ns, double Bytes, double Ms) Measure(Lane lane, int count)
{
    var bytes = GC.GetAllocatedBytesForCurrentThread();
    var start = Stopwatch.GetTimestamp();
    var sum = 0;
    for (var index = 0; index < count; index++)
    {
        var result = lane.Call();
        sum = unchecked(sum + result.Count);
        Sink.Value = result; // Retain the typed public result without boxing or an extra traversal.
    }
    var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
    if (sum != unchecked(count * lane.Expected)) throw new Exception("Count mismatch");
    return (ms * 1e6 / count, (double)allocated / count, ms);
}

static ulong MinimumWork(string pattern, string text, Utf8ResourcePolicy policy)
{
    var input = Encoding.UTF8.GetBytes(text);
    ulong low = 1, high = 1_000_000;
    while (low < high)
    {
        var middle = low + (high - low) / 2;
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            policy with { MaxWorkSteps = middle });
        try { regex.FindAllToStrings(input); high = middle; }
        catch (Utf8ResourceLimitExceededException error) when (error.ResourceKind == Utf8ResourceLimitKind.ExecutionWorkSteps)
        { low = middle + 1; }
    }
    return low;
}
record Lane(string Name, Func<Utf8PythonFindAllResult> Call, int Expected);
static class Sink { internal static Utf8PythonFindAllResult Value; }
