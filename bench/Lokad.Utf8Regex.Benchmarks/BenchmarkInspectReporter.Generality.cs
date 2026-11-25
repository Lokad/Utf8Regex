using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    // A representative starting catalog, not exhaustive coverage of the old shape
    // catalog. Neighbours deliberately change structure rather than literal spelling.
    private static readonly (string Id, string Pattern)[] GeneralityCases =
    [
        ("word/catalog", @"\w+\s+Holmes"),
        ("word/two-runs", @"\w+\s+\w+\s+Holmes"),
        ("word/captured-alternative", @"(\w+)\s+(Holmes|Watson)"),
        ("word/flexible-literal", @"Sherlock\s+Holmes"),
        ("suffix/catalog", @"\s[a-zA-Z]{0,12}ing\s"),
        ("suffix/punctuation", @"\s[a-zA-Z]{0,12}ing[\s.,;]"),
        ("suffix/boundaries", @"\b[a-zA-Z]{0,12}ing\b"),
        ("suffix/capture", @"\s([a-zA-Z]{0,12}ing)\s"),
        ("email/catalog", @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+"),
        ("email/captures", @"([\w\.+-]+)@([\w\.-]+)\.([\w\.-]+)"),
        ("email/labels", @"[\w.+-]+@[\w-]+\.[\w.-]+"),
        ("email/ascii", @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"),
        ("everyday/number", @"\d+(?:\.\d+)?"),
        ("everyday/lookaround", @"(?<=\s)[A-Z][a-z]+(?=\s)"),
        ("everyday/lazy", ".*?,"),
        ("everyday/words", @"\b(?:the|and|that|have|for)\b"),
        ("everyday/suffix-alternatives", @"[a-z]+ing|[a-z]+ed|[a-z]+ly"),
        ("everyday/word-boundary", @"\b\w+ly\b"),
        ("everyday/multiline", @"(?m)^\s*\w+:"),
        ("everyday/backreference", @"(\w)\1"),
        ("everyday/quoted", @"""[^""\n]*"""),
        ("everyday/key-value", @"(\w+): (\w+)"),
        ("everyday/ip", @"\b(?:\d{1,3}\.){3}\d{1,3}\b"),
        ("everyday/uri", @"https?://[^\s/?#]+[^\s?#]*(?:\?[^\s#]*)?"),
    ];

    internal static int RunGenerality(string? filter, string? operationFilter, bool verifyOnly)
    {
#if DEBUG
        throw new InvalidOperationException("Generality measurements require Release binaries.");
#else
        var selected = GeneralityCases.Where(row => string.IsNullOrEmpty(filter) || row.Id.Contains(filter, StringComparison.Ordinal)).ToArray();
        if (selected.Length == 0) throw new ArgumentException("No generality case matched the filter.");
        string[] operations = ["IsMatch", "MatchSlots", "Cursor", "Count", "Construct"];
        if (!string.IsNullOrEmpty(operationFilter))
        {
            if (!operations.Contains(operationFilter)) throw new ArgumentException("Unknown generality operation.");
            operations = [operationFilter];
        }
        var results = new List<object>();
        var started = Stopwatch.StartNew();
        // Include Unicode in every haystack. The second half repeats candidates,
        // so search from a byte offset preserves prefix context and sees more hits.
        var text = string.Concat(Enumerable.Repeat(
            "é Ω 中 Sherlock Holmes and Watson were quietly walking, key: value 12.5 192.1.2.3 a@b.cd https://host/path?q=1 \"quoted\"\n", 32));
        using var token = new CancellationTokenSource();
        foreach (var row in selected)
        foreach (var operation in operations)
        {
            using var context = new GeneralityContext(row.Pattern, text, token.Token);
            var laneNames = operation == "Construct"
                ? new[] { "Core", "PredecodedRegex", "CompiledRegex" }
                : operation == "Count"
                    ? new[] { "Core", "PredecodedRegex", "CompiledRegex", "DecodeThenRegex" }
                    : new[] { "Core", "CoreToken", "CoreDeadline", "CoreBudget", "CoreMetered", "CoreCompiledMetered",
                        "PredecodedRegex", "CompiledRegex", "DecodeThenRegex" };
            Func<int>[] actions = laneNames.Select(name => (Func<int>)(() => context.Execute(operation, name))).ToArray();
            var expected = actions[0]();
            foreach (var action in actions)
                if (action() != expected) throw new InvalidOperationException($"Generality result mismatch: {row.Id}/{operation}.");
            context.VerifyCaptureTraces(operation);
            if (verifyOnly)
            {
                Console.WriteLine($"Verified {row.Id}/{operation}: {laneNames.Length} lanes, complete byte/capture trace.");
                continue;
            }
            // Use the existing independently calibrated, alternating measurement
            // loop: three samples, 75 ms target, at most one calibration correction.
            var measured = MeasureComparisonLanes(1, 1_000_000, 3, actions, laneNames);
            var qualified = measured.RawSamples.Select(samples => samples.All(us => us >= 20_000)).ToArray();
            results.Add(new { row.Id, row.Pattern, Operation = operation, LaneNames = laneNames,
                Microseconds = measured.Medians, AllocatedBytes = measured.AllocatedBytes,
                measured.Iterations, RawSampleMicroseconds = measured.RawSamples, measured.Phases, Qualified = qualified });
            Console.WriteLine($"{row.Id}/{operation}: " + string.Join("; ", laneNames.Select((lane, i) =>
                $"{lane}={measured.Medians[i].ToString("F3", CultureInfo.InvariantCulture)} us/{measured.AllocatedBytes[i]:F0} B{(qualified[i] ? "" : " (short sample)")}")));
        }
        if (!verifyOnly)
        {
            var path = Path.Combine(Path.GetDirectoryName(FindRepoFile("README.md"))!, "artifacts", "benchmark-refresh", "generality",
                $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            BenchmarkFileWriter.WriteTextAtomically(path, JsonSerializer.Serialize(new {
                Environment = CaptureBenchmarkEnvironment(), ElapsedSeconds = started.Elapsed.TotalSeconds,
                Profile = "Core", Filter = filter, OperationFilter = operationFilter,
                InputContract = "Caller-valid unchanged UTF-8 / predecoded .NET text; Core performs no subject validation. DecodeThenRegex includes decoding.",
                Scope = "24 representative patterns; structural neighbours and everyday patterns. Not exhaustive old-catalog coverage or adapter coverage.",
                OutputContract = "MatchSlots reads last captures into caller slots. Cursor reads complete capture histories. Both return byte ranges, with the BCL coordinate map prepared outside timing. No string materialization. Construction consumes the same CultureInvariant Options mask on every producer, without metadata-array copies.",
                Policy = "Ordinary Core and .NET lanes use an infinite timeout. Controlled Core lanes use a token, " +
                    "10-second deadline, 50M work steps, 4096 depth, 16 MiB working/result and a caller working allowance in the combined lane.",
                Results = results }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine($"Generality output: {path}; elapsed {started.Elapsed.TotalSeconds:F1}s.");
        }
        return 0;
#endif
    }

    private sealed class GeneralityContext : IDisposable
    {
        private readonly string _pattern, _text;
        private readonly byte[] _input;
        private readonly CancellationToken _token;
        private readonly Utf8Regex[] _core;
        private readonly Regex _oracle, _compiled;
        private readonly int[] _numbers;
        private readonly int[] _boundaries;
        private readonly int _start;
        private readonly GeneralityAllowance _allowance = new();
        private static readonly Utf8ResourcePolicy Limits = new() {
            MaxWorkSteps = 50_000_000,
            MaxDepth = 4096,
            MaxWorkingBytes = 16 << 20,
            MaxResultBytes = 16 << 20
        };

        internal GeneralityContext(string pattern, string text, CancellationToken token)
        {
            _pattern = pattern; _text = text; _input = Encoding.UTF8.GetBytes(text); _token = token;
            _boundaries = new int[text.Length + 1];
            var utf8 = 0;
            var utf16 = 0;
            foreach (var scalar in text.EnumerateRunes())
            {
                // A reference capture inside a surrogate pair has no byte boundary.
                if (scalar.Utf16SequenceLength == 2) _boundaries[utf16 + 1] = -1;
                utf16 += scalar.Utf16SequenceLength;
                utf8 += scalar.Utf8SequenceLength;
                _boundaries[utf16] = utf8;
            }
            _start = text.Length / 2;
            if (_boundaries[_start] < 0) _start--;
            _core = [Create(), Create(timeout: true), Create(limits: true), Create(timeout: true, limits: true), Create(timeout: true, limits: true, compiled: true)];
            _oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
            _compiled = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
            _numbers = _oracle.GetGroupNumbers();
        }

        private Utf8Regex Create(bool timeout = false, bool limits = false, bool compiled = false) => new(_pattern, options: RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0), policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout,
            ResourcePolicy = limits ? Limits : default
        });

        internal int Execute(string operation, string lane)
        {
            // Consume equivalent cheap metadata on every construction lane.
            // Group-number copies would measure an unrelated public operation.
            if (operation == "Construct") return lane switch {
                "Core" => (int)(Create().Options & RegexOptions.CultureInvariant),
                "PredecodedRegex" => (int)(new Regex(_pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Options & RegexOptions.CultureInvariant),
                _ => (int)(new Regex(_pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled, Regex.InfiniteMatchTimeout).Options & RegexOptions.CultureInvariant) };
            if (!lane.StartsWith("Core", StringComparison.Ordinal))
            {
                var text = lane == "DecodeThenRegex" ? Encoding.UTF8.GetString(_input) : _text;
                var regex = lane == "CompiledRegex" ? _compiled : _oracle;
                if (operation == "IsMatch") return regex.IsMatch(text) ? 1 : 0;
                if (operation == "Count") return regex.Count(text);
                var match = regex.Match(text, _start);
                var checksum = 0;
                if (operation == "MatchSlots")
                {
                    foreach (var number in _numbers)
                    {
                        var group = match.Groups[number];
                        if (group.Success)
                            checksum = AddRange(checksum, _boundaries[group.Index], _boundaries[group.Index + group.Length]);
                    }
                    return checksum;
                }
                while (match.Success)
                {
                    foreach (var number in _numbers)
                    foreach (Capture capture in match.Groups[number].Captures)
                        checksum = AddRange(checksum, _boundaries[capture.Index], _boundaries[capture.Index + capture.Length]);
                    match = match.NextMatch();
                }
                return checksum;
            }
            var index = lane switch { "CoreDeadline" => 1, "CoreBudget" => 2, "CoreMetered" => 3, "CoreCompiledMetered" => 4, _ => 0 };
            var token = lane is "CoreToken" or "CoreMetered" or "CoreCompiledMetered" ? _token : default;
            var allowance = lane is "CoreMetered" or "CoreCompiledMetered" ? _allowance : null;
            var core = _core[index];
            if (operation == "IsMatch") return (allowance is null ? core.IsMatch(_input, token) : core.IsMatch(_input, token, allowance)) ? 1 : 0;
            if (operation == "Count") return core.Count(_input);
            var sum = 0;
            if (operation == "MatchSlots")
            {
                Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[_numbers.Length];
                core.Match(_input, _boundaries[_start], slots, token, allowance);
                foreach (var range in slots)
                    if (range.Success) sum = AddRange(sum, range.StartOffsetInBytes, range.EndOffsetInBytes);
                return sum;
            }
            using var cursor = core.CreateMatchCursor(_input, _boundaries[_start], token, allowance);
            while (cursor.MoveNext(out var result))
            {
                foreach (var number in result.GroupNumbers)
                foreach (var capture in result.GetCaptures(number))
                    sum = AddRange(sum, capture.StartOffsetInBytes, capture.EndOffsetInBytes);
            }
            return sum;
        }

        internal void VerifyCaptureTraces(string operation)
        {
            if (operation == "MatchSlots")
            {
                VerifyLastCaptures();
                return;
            }
            if (operation != "Cursor") return;
            var expected = new List<(int Group, int Start, int End)>();
            var match = _oracle.Match(_text, _start);
            while (match.Success)
            {
                foreach (var number in _numbers)
                foreach (Capture capture in match.Groups[number].Captures)
                    expected.Add((number, _boundaries[capture.Index], _boundaries[capture.Index + capture.Length]));
                match = match.NextMatch();
            }
            foreach (var core in _core)
            {
                var actual = new List<(int Group, int Start, int End)>();
                using (var cursor = core.CreateMatchCursor(_input, _boundaries[_start], _token, _allowance))
                    while (cursor.MoveNext(out var result))
                    {
                        foreach (var number in result.GroupNumbers)
                        foreach (var capture in result.GetCaptures(number))
                            actual.Add((number, capture.StartOffsetInBytes, capture.EndOffsetInBytes));
                    }
                if (!actual.SequenceEqual(expected)) throw new InvalidOperationException($"Generality capture trace differs: {_pattern}/{operation}.");
                if (_allowance.Used != 0) throw new InvalidOperationException("Generality cursor failed to return working storage.");
            }
        }

        private void VerifyLastCaptures()
        {
            var reference = _oracle.Match(_text, _start);
            var compiled = _compiled.Match(_text, _start);
            Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[_numbers.Length];
            foreach (var core in _core)
            {
                var match = core.Match(_input, _boundaries[_start], slots, _token, _allowance);
                if (!MatchesRange(reference, match) || core.CaptureGroupCount + 1 != slots.Length)
                    throw new InvalidOperationException($"Generality match range differs: {_pattern}.");
                for (var slot = 0; slot < slots.Length; slot++)
                {
                    var number = _numbers[slot];
                    if (core.GroupNumberFromCaptureSlot(slot) != number || !MatchesRange(reference.Groups[number], slots[slot]) ||
                        !MatchesRange(compiled.Groups[number], slots[slot]))
                        throw new InvalidOperationException($"Generality capture slot {number} differs: {_pattern}.");
                }
                if (_allowance.Used != 0) throw new InvalidOperationException("Generality Match failed to return working storage.");
            }
        }

        private bool MatchesRange(Group expected, Utf8CaptureRange actual) => expected.Success == actual.Success &&
            (!actual.Success || (actual.StartOffsetInBytes, actual.EndOffsetInBytes) ==
                (_boundaries[expected.Index], _boundaries[expected.Index + expected.Length]));

        private static int AddRange(int checksum, int start, int end) => unchecked(checksum * 31 + start * 7 + end + 1);

        public void Dispose()
        {
            if (_allowance.Used != 0) throw new InvalidOperationException("Generality operation failed to return working storage.");
        }
    }

    private sealed class GeneralityAllowance : IUtf8MemoryAllowance
    {
        private readonly object _gate = new();
        internal ulong Used;
        public bool TryReserve(ulong bytes)
        { lock (_gate) { if (bytes > (16UL << 20) - Used) return false; Used += bytes; return true; } }
        public void Release(ulong bytes)
        { lock (_gate) { if (bytes > Used) throw new InvalidOperationException("Invalid generality benchmark refund."); Used -= bytes; } }
    }
}
