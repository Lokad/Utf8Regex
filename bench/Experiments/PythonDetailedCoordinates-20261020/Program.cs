using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;
using OwnedRegex = Lokad.Utf8Regex.Utf8Regex;

var preflight = args[0] == "--preflight";
using var writer = new StreamWriter(File.Open(preflight ? args[1] : args[0], FileMode.CreateNew)) { AutoFlush = true };
void Emit(object value) => writer.WriteLine(JsonSerializer.Serialize(value));
static string Hash(Type type) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(type.Assembly.Location)));
Emit(new { Kind = "identity", Runtime = Environment.Version.ToString(), Core = Hash(typeof(OwnedRegex)),
    Python = Hash(typeof(Utf8PythonRegex)), Driver = Hash(typeof(Program)) });

var lanes = new List<Lane>();
void Add(string name, Func<int> call, int expected)
{
    if (call() != expected) throw new Exception("Public result mismatch: " + name);
    lanes.Add(new(name, call, expected));
}
// Compile and warm these unchanged controls before any Python shaping code.
var core = new OwnedRegex("needle", RegexOptions.CultureInvariant);
var tinyControl = Encoding.UTF8.GetBytes("needle" + new string('x', 26));
var largeControl = Encoding.UTF8.GetBytes("needle" + new string('x', 131072 - 6));
Add("core-count-32b-control", () => core.Count(tinyControl), 1);
Add("core-count-128k-control", () => core.Count(largeControl), 1);
var bcl = new Regex("needle", RegexOptions.CultureInvariant);
var decoded = Encoding.UTF8.GetString(largeControl);
Add("dotnet-predecoded-control", () => bcl.Match(decoded).Length, 6);
Add("dotnet-decode-control", () => bcl.Match(Encoding.UTF8.GetString(largeControl)).Length, 6);

var policy = new Utf8ResourcePolicy { MaxWorkSteps = 50000000, MaxDepth = 8192,
    MaxWorkingBytes = 64UL << 20, MaxResultBytes = 128UL << 20 };
foreach (var finite in new[] { false, true })
{
    var prefix = finite ? "finite/" : "unlimited/";
    var mixed = new Utf8PythonRegex("(?P<first>é)(?P<second>😀)", PythonReCompileOptions.None,
        Regex.InfiniteMatchTimeout, finite ? policy : Utf8ResourcePolicy.Unlimited);
    var ascii = new Utf8PythonRegex("(?P<first>a)(?P<second>b)", PythonReCompileOptions.None,
        Regex.InfiniteMatchTimeout, finite ? policy : Utf8ResourcePolicy.Unlimited);
    AddDetailed(prefix + "mixed-search-32b", mixed,
        Encoding.UTF8.GetBytes("xxé😀" + new string('x', 24)), 1, "é", "😀", 0, false);
    AddDetailed(prefix + "mixed-search-128k", mixed,
        Encoding.UTF8.GetBytes("xxé😀" + new string('x', 131072 - 8)), 1, "é", "😀", 0, false);
    AddDetailed(prefix + "ascii-search-32b", ascii,
        Encoding.UTF8.GetBytes("xxab" + new string('x', 28)), 1, "a", "b", 0, false);
    foreach (var count in new[] { 128, 2048 })
    {
        var mixedInput = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("xxé😀 ", count)));
        var asciiInput = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("xxab ", count)));
        AddDetailed(prefix + "mixed-finditer-" + count, mixed, mixedInput, count, "é", "😀", 6, true);
        AddDetailed(prefix + "ascii-finditer-" + count, ascii, asciiInput, count, "a", "b", 5, true);
        if (count == 128)
        {
            var values = mixed.FindAllToStrings(mixedInput);
            if (values.TupleValues.Length != count || values.TupleValues.Any(x => x.Length != 2 || x[0] != "é" || x[1] != "😀"))
                throw new Exception("Unknown-count FindAll values differ");
            Add(prefix + "mixed-findall-128-neighbor", () =>
            {
                Sink.Tuples = mixed.FindAllToStrings(mixedInput);
                return Sink.Tuples.TupleValues.Length;
            }, count);
        }
    }
}
if (preflight) { Emit(new { Kind = "complete", Lanes = lanes.Count }); return; }

var counts = new Dictionary<string, int>();
foreach (var lane in lanes)
{
    var warm = Stopwatch.StartNew();
    do { Sink.Value = lane.Call(); } while (warm.ElapsedMilliseconds < 120);
    var count = 32;
    for (var attempt = 0; attempt < 6; attempt++)
    {
        var elapsed = Measure(lane, count).Ms;
        if (elapsed >= 65 && elapsed <= 150) break;
        count = (int)Math.Clamp(Math.Ceiling(count * 80 / Math.Max(.001, elapsed)), 1, 2000000);
    }
    counts[lane.Name] = count;
}
for (var ordinal = 0; ordinal < 5; ordinal++)
foreach (var lane in ordinal % 2 == 0 ? lanes : lanes.AsEnumerable().Reverse())
{
    var sample = Measure(lane, counts[lane.Name]);
    Emit(new { Kind = "sample", Lane = lane.Name, Ordinal = ordinal, sample.Ns, sample.Bytes, sample.Ms,
        Iterations = counts[lane.Name] });
}
Emit(new { Kind = "complete" });

void AddDetailed(string name, Utf8PythonRegex regex, byte[] input, int count,
    string first, string second, int charStep, bool bulk)
{
    var all = bulk ? regex.FindIterDetailed(input) : [regex.SearchDetailedData(input)];
    if (all.Length != count) throw new Exception("Detailed result count differs: " + name);
    var byteFirst = Encoding.UTF8.GetByteCount(first);
    var byteSecond = Encoding.UTF8.GetByteCount(second);
    var byteStep = bulk ? 3 + byteFirst + byteSecond : 0;
    for (var i = 0; i < all.Length; i++)
    {
        var match = all[i];
        if (!match.Success || match.Groups.Length != 3 || match.LastIndex != 2 || match.LastGroup != "second")
            throw new Exception("Detailed capture metadata differs: " + name);
        var startByte = 2 + i * byteStep;
        var startChar = 2 + i * charStep;
        foreach (var g in new[] { (0, first + second, startByte, startChar),
                     (1, first, startByte, startChar), (2, second, startByte + byteFirst, startChar + first.Length) })
        {
            var actual = match.Groups[g.Item1];
            if (!actual.Success || actual.Number != g.Item1 || actual.ValueText != g.Item2 ||
                actual.StartOffsetInBytes != g.Item3 || actual.EndOffsetInBytes != g.Item3 + Encoding.UTF8.GetByteCount(g.Item2) ||
                actual.StartOffsetInUtf16 != g.Item4 || actual.EndOffsetInUtf16 != g.Item4 + g.Item2.Length)
                throw new Exception("Detailed scalar coordinates/value differ: " + name);
        }
    }
    Emit(new { Kind = "preflight", Lane = name, Bytes = input.Length, Matches = count });
    Add(name, bulk ? () => { Sink.All = regex.FindIterDetailed(input); return Sink.All.Length; }
        : () => { Sink.One = regex.SearchDetailedData(input); return Sink.One.Success ? 1 : 0; }, count);
}

static (double Ns, double Bytes, double Ms) Measure(Lane lane, int count)
{
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var started = Stopwatch.GetTimestamp();
    var sum = 0;
    for (var index = 0; index < count; index++) sum = unchecked(sum + lane.Call());
    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
    if (sum != unchecked(count * lane.Expected)) throw new Exception("Result changed: " + lane.Name);
    Sink.Value = sum;
    return (elapsed * 1000000 / count, (double)bytes / count, elapsed);
}

record Lane(string Name, Func<int> Call, int Expected);
static class Sink
{
    internal static int Value;
    internal static Utf8PythonDetailedMatchData One;
    internal static Utf8PythonDetailedMatchData[] All = [];
    internal static Utf8PythonFindAllResult Tuples;
}
