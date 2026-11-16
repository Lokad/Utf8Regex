using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex.Posix;
using PCRE;
using PCRE.Dfa;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private static int MeasurePosixLongestExtents(string id, int samples)
    {
#if DEBUG
        throw new InvalidOperationException("POSIX extent measurements require Release binaries.");
#else
        var (pattern, text) = id switch {
            "extent/ambiguous-haystack" => ("(a|aa)", "é😀 " + new string('a', 256)),
            "extent/word-haystack" => ("([a-z]|[a-z]+)", "é😀 " + string.Concat(Enumerable.Repeat("cat 123 other 45 ", 64))),
            _ => throw new ArgumentException("Unknown POSIX extent case.") };
        if (samples is < 3 or > 31) throw new ArgumentOutOfRangeException(nameof(samples));
        var input = Encoding.UTF8.GetBytes(text);
        var byteAtUtf16 = Enumerable.Repeat(-1, text.Length + 1).ToArray();
        byteAtUtf16[0] = 0;
        var utf16 = 0; var bytes = 0;
        foreach (var scalar in text.EnumerateRunes())
        {
            utf16 += scalar.Utf16SequenceLength; bytes += scalar.Utf8SequenceLength;
            byteAtUtf16[utf16] = bytes;
        }
        var unlimited = new Utf8PosixRegex(pattern);
        var finite = new Utf8PosixRegex(pattern, resourcePolicy: new() {
            MaxWorkSteps = 50_000_000, MaxDepth = 4096, MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 },
            matchTimeout: TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        var account = new GeneralityAllowance();
        // The DFA API exposes only whole-match extents, with longest first. It
        // does not implement POSIX subgroup priority. It consumes predecoded
        // UTF-16 through PCRE.NET; its wrapper allocations remain in the cost.
        var native = new PcreRegex(pattern, PcreOptions.Utf | PcreOptions.Singleline |
            PcreOptions.DollarEndOnly | PcreOptions.NoAutoPossess);
        List<(int Start, int End)> ReadNative()
        {
            var result = new List<(int, int)>();
            var start = 0;
            while (start <= text.Length)
            {
                var matches = native.Dfa.Match(text, start, PcreDfaMatchOptions.NoUtfCheck);
                if (matches.Count == 0) break;
                var match = matches[0];
                result.Add((byteAtUtf16[match.Index], byteAtUtf16[match.Index + match.Length]));
                start = match.Index + match.Length;
                if (match.Length == 0)
                {
                    if (start == text.Length) break;
                    start += char.IsHighSurrogate(text[start]) ? 2 : 1;
                }
            }
            return result;
        }
        var expected = ReadNative();
        foreach (var regex in new[] { unlimited, finite })
        {
            var trace = new List<(int Start, int End)>();
            using (var cursor = regex.CreateMatchCursor(input, cancellationToken: cancellation.Token, workingMemoryAllowance: account))
                while (cursor.MoveNext(out var match)) trace.Add((match.StartOffsetInBytes, match.StartOffsetInBytes + (match.EndOffsetInBytes - match.StartOffsetInBytes)));
            if (!trace.SequenceEqual(expected)) throw new InvalidOperationException("POSIX and native DFA whole-match extents disagree.");
        }
        if (id == "extent/ambiguous-haystack" && (expected.Count != 128 || expected.Any(range => range.End - range.Start != 2)))
            throw new InvalidOperationException("Longest reference selected the first one-byte alternative.");

        int ManagedCursor(Utf8PosixRegex regex, CancellationToken token, IUtf8MemoryAllowance? allowance = null)
        {
            var checksum = 0;
            using var cursor = regex.CreateMatchCursor(input, cancellationToken: token, workingMemoryAllowance: allowance);
            while (cursor.MoveNext(out var match)) checksum = unchecked(checksum * 31 + match.StartOffsetInBytes * 7 + (match.EndOffsetInBytes - match.StartOffsetInBytes));
            return checksum;
        }
        int Native(bool count)
        {
            var start = 0; var checksum = 0; var total = 0;
            while (start <= text.Length)
            {
                var matches = native.Dfa.Match(text, start, PcreDfaMatchOptions.NoUtfCheck);
                if (matches.Count == 0) break;
                var match = matches[0]; total++;
                if (!count) checksum = unchecked(checksum * 31 + byteAtUtf16[match.Index] * 7 +
                    byteAtUtf16[match.Index + match.Length] - byteAtUtf16[match.Index]);
                start = match.Index + match.Length;
                if (match.Length == 0)
                {
                    if (start == text.Length) break;
                    start += char.IsHighSurrogate(text[start]) ? 2 : 1;
                }
            }
            return count ? total : checksum;
        }
        var results = new List<object>();
        foreach (var operation in new[] { "Count", "Cursor" })
        {
            var count = operation == "Count";
            string[] names = ["Posix", "PosixToken", "PosixFiniteTokenDeadline", "PosixFundedFiniteTokenDeadline", "NativeLongestDfaPredecoded"];
            Func<int>[] actions = count
                ? [() => unlimited.Count(input), () => unlimited.Count(input, cancellationToken: cancellation.Token),
                    () => finite.Count(input, cancellationToken: cancellation.Token),
                    () => finite.Count(input, cancellationToken: cancellation.Token, workingMemoryAllowance: account), () => Native(true)]
                : [() => ManagedCursor(unlimited, default), () => ManagedCursor(unlimited, cancellation.Token),
                    () => ManagedCursor(finite, cancellation.Token), () => ManagedCursor(finite, cancellation.Token, account), () => Native(false)];
            var measured = MeasureComparisonLanes(1, 1_000_000, samples, actions, names);
            if (account.Used != 0) throw new InvalidOperationException("POSIX extent comparison retained working storage.");
            var qualified = measured.RawSamples.Select(raw => raw.All(us => us >= 20_000)).ToArray();
            results.Add(new { Operation = operation, LaneNames = names, Microseconds = measured.Medians, measured.AllocatedBytes,
                measured.Iterations, RawSampleMicroseconds = measured.RawSamples, Qualified = qualified });
            for (var lane = 0; lane < names.Length; lane++)
                Console.WriteLine($"{id}/{operation}/{names[lane]}: {measured.Medians[lane]:F3} us; {measured.AllocatedBytes[lane]:F0} B; qualified={qualified[lane]}");
        }
        var path = Path.Combine(Path.GetDirectoryName(FindRepoFile("README.md"))!, "artifacts", "benchmark-refresh", "generality",
            $"posix-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        BenchmarkFileWriter.WriteTextAtomically(path, JsonSerializer.Serialize(new { Id = id, Pattern = pattern,
            InputBytes = input.Length, Environment = CaptureBenchmarkEnvironment(), NativeVersion = PcreBuildInfo.Version,
            Comparator = "PCRE2 DFA, longest whole match; predecoded UTF-16, NO_UTF_CHECK; no POSIX subgroup/collation equivalence.",
            OutputContract = "Complete nonoverlapping byte extents verified before timing; native byte-coordinate map prepared outside timing.",
            Results = results }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"POSIX longest-extent evidence: {path}");
        GC.KeepAlive(native);
        return 0;
#endif
    }
}
