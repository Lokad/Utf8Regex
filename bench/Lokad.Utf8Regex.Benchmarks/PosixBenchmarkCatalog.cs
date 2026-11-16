using Lokad.Utf8Regex.Posix;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum PosixBenchmarkOperation { Captures, Count, Cursor }

internal sealed record PosixBenchmarkCase(string Id, string Pattern, string Input,
    PosixBenchmarkOperation Operation, int Records = 1, int StartOffsetInBytes = 0, bool MeasurePolicies = false,
    PosixCompileOptions Options = PosixCompileOptions.None);

internal static class PosixBenchmarkCatalog
{
    private static readonly string s_projectionPrefix = string.Concat(Enumerable.Repeat("é😀", 16384));
    internal static readonly PosixBenchmarkCase[] Cases =
    [
        // Historical consumer/adapter control, not a POSIX selection headline.
        new("capture/sku007", "^([[:alpha:]]+)([0-9]+)$", "sku007", PosixBenchmarkOperation.Captures, 4096, MeasurePolicies: true),
        new("capture/ambiguous", "(a|aa)(a*)", "aaa", PosixBenchmarkOperation.Captures),
        new("capture/absent", "(a)|(a)", "a", PosixBenchmarkOperation.Captures),
        new("capture/empty", "(a*)(a*)", "aaa", PosixBenchmarkOperation.Captures, MeasurePolicies: true),
        new("capture/nested", "(((a)|(aa))*)", "aaaa", PosixBenchmarkOperation.Captures),
        new("capture/nullable", "(a?){2,4}", "a", PosixBenchmarkOperation.Captures),
        new("capture/unicode-longest", "(é|é😀)(.*)", "😀é😀x", PosixBenchmarkOperation.Captures, StartOffsetInBytes: 4),
        new("capture/ignorecase-disjoint", "(k+)([0-9]+)", "KK007", PosixBenchmarkOperation.Captures,
            MeasurePolicies: true, Options: PosixCompileOptions.IgnoreCase),
        new("capture/newline-rewind", "^([a-z]+)([[:space:]]+)$", "a \nb", PosixBenchmarkOperation.Captures,
            Options: PosixCompileOptions.Newline),
        // Byte capture publication scaling, not Core's generic search/validation lane.
        // Short controls distinguish tag costs from rescanning a shared prefix.
        new("projection/short-1", "(a)", "a", PosixBenchmarkOperation.Captures),
        new("projection/short-4", "((((a))))", "a", PosixBenchmarkOperation.Captures),
        new("projection/unicode-prefix-1", "(a)", s_projectionPrefix + "a", PosixBenchmarkOperation.Captures, StartOffsetInBytes: 98304),
        new("projection/unicode-prefix-4", "((((a))))", s_projectionPrefix + "a", PosixBenchmarkOperation.Captures, StartOffsetInBytes: 98304),
        // Bounded ambiguity growth: longest-first history, not ordered captures.
        new("history/length-4", "(a|aa)*", new('a', 4), PosixBenchmarkOperation.Captures),
        new("history/length-8", "(a|aa)*", new('a', 8), PosixBenchmarkOperation.Captures),
        new("history/length-12", "(a|aa)*", new('a', 12), PosixBenchmarkOperation.Captures),
        new("count/ambiguous", "a|aa", "aaaa", PosixBenchmarkOperation.Count),
        new("cursor/ambiguous", "(a|aa)", "aaaaa", PosixBenchmarkOperation.Cursor, MeasurePolicies: true),
    ];

    internal static PosixBenchmarkCase Get(string id) => Cases.SingleOrDefault(row => row.Id == id)
        ?? throw new ArgumentException($"Unknown POSIX benchmark case: {id}");

    internal static PosixBenchmarkResult Expected(string id)
    {
        static Utf8CaptureRange A(int start, int length) => new(start, start + length);
        return id switch
        {
            "capture/sku007" => new(4096, A(0, 6), A(0, 3), A(3, 3)),
            "capture/ambiguous" => new(1, A(0, 3), A(0, 2), A(2, 1)),
            "capture/absent" => new(1, A(0, 1), A(0, 1), default),
            "capture/empty" => new(1, A(0, 3), A(0, 3), A(3, 0)),
            "capture/nested" => new(1, A(0, 4), A(0, 4), A(2, 2), default, A(2, 2)),
            "capture/nullable" => new(1, A(0, 1), A(1, 0), default),
            "capture/unicode-longest" => new(1, A(4, 7), A(4, 6), A(10, 1)),
            "capture/ignorecase-disjoint" => new(1, A(0, 7), A(0, 4), A(4, 3)),
            "capture/newline-rewind" => new(1, A(0, 2), A(0, 1), A(1, 1)),
            "projection/short-1" => new(1, A(0, 1), A(0, 1), default),
            "projection/short-4" => new(1, A(0, 1), A(0, 1), A(0, 1), A(0, 1), A(0, 1)),
            "projection/unicode-prefix-1" => new(1, A(98304, 1), A(98304, 1), default),
            "projection/unicode-prefix-4" => new(1, A(98304, 1), A(98304, 1), A(98304, 1), A(98304, 1), A(98304, 1)),
            "history/length-4" => new(1, A(0, 4), A(2, 2), default),
            "history/length-8" => new(1, A(0, 8), A(6, 2), default),
            "history/length-12" => new(1, A(0, 12), A(10, 2), default),
            "count/ambiguous" => new(2, default, default, default),
            "cursor/ambiguous" => new(3, A(4, 1), A(4, 1), default),
            _ => throw new ArgumentException($"Unknown POSIX benchmark expectation: {id}"),
        };
    }
}

// The fixed caller-owned destinations cover every catalog capture slot. Native
// results include the same byte ranges. Consume the
// returned values after the measurement clock, like the existing Python batches.
// Four captures cover the one selected nested case; no variable result allocation.
internal readonly record struct PosixBenchmarkResult(int Count, Utf8CaptureRange Whole,
    Utf8CaptureRange Group1, Utf8CaptureRange Group2, Utf8CaptureRange Group3 = default, Utf8CaptureRange Group4 = default);
