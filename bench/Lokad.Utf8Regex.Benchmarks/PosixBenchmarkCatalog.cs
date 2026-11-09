using Lokad.Utf8Regex.Posix;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum PosixBenchmarkOperation { Captures, Count, Cursor }

internal sealed record PosixBenchmarkCase(string Id, string Pattern, string Input,
    PosixBenchmarkOperation Operation, int Records = 1, int StartOffsetInBytes = 0, bool MeasurePolicies = false);

internal static class PosixBenchmarkCatalog
{
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
        static Utf8ValueMatch A(int start, int length) => new(true, true, start, length, start, length);
        static Utf8ValueMatch U(int start, int length, int utf16, int utf16Length) => new(true, true, utf16, utf16Length, start, length);
        return id switch
        {
            "capture/sku007" => new(4096, A(0, 6), A(0, 3), A(3, 3)),
            "capture/ambiguous" => new(1, A(0, 3), A(0, 2), A(2, 1)),
            "capture/absent" => new(1, A(0, 1), A(0, 1), default),
            "capture/empty" => new(1, A(0, 3), A(0, 3), A(3, 0)),
            "capture/nested" => new(1, A(0, 4), A(0, 4), A(2, 2), default, A(2, 2)),
            "capture/nullable" => new(1, A(0, 1), A(1, 0), default),
            "capture/unicode-longest" => new(1, U(4, 7, 2, 4), U(4, 6, 2, 3), U(10, 1, 5, 1)),
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
// projection includes the same byte and derived UTF-16 coordinates. Consume the
// returned values after the measurement clock, like the existing Python batches.
// Four captures cover the one selected nested case; no variable result allocation.
internal readonly record struct PosixBenchmarkResult(int Count, Utf8ValueMatch Whole,
    Utf8ValueMatch Group1, Utf8ValueMatch Group2, Utf8ValueMatch Group3 = default, Utf8ValueMatch Group4 = default);
