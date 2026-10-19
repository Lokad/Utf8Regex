using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Internal.Execution;
using OwnedRegex = Lokad.Utf8Regex.Utf8Regex;

var preflight = args[0] == "--preflight";
using var writer = new StreamWriter(File.Open(preflight ? args[1] : args[0], FileMode.CreateNew)) { AutoFlush = true };
void Emit(object value) => writer.WriteLine(JsonSerializer.Serialize(value));
Emit(new { Kind = "identity", Runtime = Environment.Version.ToString(),
    Core = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(OwnedRegex).Assembly.Location))),
    Driver = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))) });

if (preflight)
{
    var method = typeof(OwnedRegex).GetMethod("GetOwnedSliceProgram", BindingFlags.NonPublic | BindingFlags.Instance,
        null, [typeof(ReadOnlySpan<byte>)], null)!;
    foreach (var pattern in new[] { "needle", "(needle)", "(a)(b)", "é😀", "needle|other", @"\bneedle\b", "(?i:needle)", "ab[0-9]{2}" })
    foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
    foreach (var text in new[] { "needle ab12 other", "😀é needle ab12 other", "needle" + new string('x', 131072), new string('x', 131072) })
    {
        var regex = new OwnedRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        var select = method.CreateDelegate<Select>(regex);
        var owned = select(input) is not null;
        var expected = new Regex(pattern, options).Match(text);
        var result = regex.MatchDetailed(input);
        if (result.Success != expected.Success || result.Success &&
            (result.IndexInUtf16 != expected.Index || result.LengthInUtf16 != expected.Length ||
             !result.TryGetByteRange(out var offset, out var length) ||
             offset != Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)) || length != Encoding.UTF8.GetByteCount(expected.Value)))
            throw new Exception("Public detailed preflight mismatch");
        Emit(new { Kind = "preflight", Pattern = pattern, Compiled = options.HasFlag(RegexOptions.Compiled),
            Bytes = input.Length, Owned = owned, Execution = regex.Inspection.ExecutionKind.ToString(),
            Runtime = regex.Inspection.DebugCompiledEngineRuntimeType, result.Success });
    }
    Emit(new { Kind = "complete" });
    return;
}

var policy = new Utf8ResourcePolicy { MaxWorkSteps = 50000000, MaxDepth = 1024,
    MaxWorkingBytes = 64UL * 1048576, MaxResultBytes = 128UL * 1048576 };
var lanes = new List<Lane>();
foreach (var compiled in new[] { false, true })
{
    var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
    var regex = new OwnedRegex("needle", options);
    var finite = new OwnedRegex("needle", options, Regex.InfiniteMatchTimeout, policy);
    var small = Encoding.UTF8.GetBytes("needle" + new string('x', 4096 - 6));
    var tiny = Encoding.UTF8.GetBytes("needle" + new string('x', 32 - 6));
    var large = Encoding.UTF8.GetBytes("needle" + new string('x', 131072 - 6));
    var miss = Encoding.UTF8.GetBytes(new string('x', 131072));
    var prefix = compiled ? "compiled/" : "ordinary/";
    lanes.Add(new(prefix + "detailed-32b", () => Detail(regex, tiny), 6));
    lanes.Add(new(prefix + "value-32b", () => Value(regex, tiny), 6));
    lanes.Add(new(prefix + "count-32b", () => regex.Count(tiny), 1));
    lanes.Add(new(prefix + "detailed-4k", () => Detail(regex, small), 6));
    lanes.Add(new(prefix + "detailed-128k", () => Detail(regex, large), 6));
    lanes.Add(new(prefix + "detailed-miss-128k", () => Detail(regex, miss), -1));
    lanes.Add(new(prefix + "value-128k", () => Value(regex, large), 6));
    lanes.Add(new(prefix + "count-128k", () => regex.Count(large), 1));
    lanes.Add(new(prefix + "finite-control", () => Detail(finite, large), 6));
    var structural = new OwnedRegex("ab[0-9]{2}", options);
    var structuralInput = "ab12x"u8.ToArray();
    lanes.Add(new(prefix + "structural-control", () => Detail(structural, structuralInput), 4));
    var bcl = new Regex("needle", options);
    var text = Encoding.UTF8.GetString(large);
    lanes.Add(new(prefix + "dotnet-predecoded", () => Bcl(bcl, text), 6));
    lanes.Add(new(prefix + "dotnet-decode", () => Bcl(bcl, Encoding.UTF8.GetString(large)), 6));
}
foreach (var lane in lanes)
    if (lane.Call() != lane.Expected) throw new Exception("Measurement route mismatch: " + lane.Name);
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
    Emit(new { Kind = "sample", Lane = lane.Name, Ordinal = ordinal, sample.Ns, sample.Bytes, sample.Ms, Iterations = counts[lane.Name] });
}
Emit(new { Kind = "complete" });

static int Detail(OwnedRegex regex, byte[] input)
{
    var match = regex.MatchDetailed(input);
    return match.Success && match.TryGetByteRange(out var start, out var length) ? start + length : -1;
}
static int Value(OwnedRegex regex, byte[] input)
{
    var match = regex.Match(input);
    return match.Success ? match.IndexInBytes + match.LengthInBytes : -1;
}
static int Bcl(Regex regex, string input)
{
    var match = regex.Match(input);
    return match.Success ? match.Index + match.Length : -1;
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
delegate Utf8SliceProgram? Select(ReadOnlySpan<byte> input);
record Lane(string Name, Func<int> Call, int Expected);
static class Sink { internal static int Value; }
