using Lokad.Utf8Regex.Posix;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum PosixBenchmarkOperation { IsMatch, Captures, Count, Cursor }

internal sealed record PosixBenchmarkCase(string Id, string Pattern, string Input,
    PosixBenchmarkOperation Operation, int Records = 1, int StartOffsetInBytes = 0);

internal static class PosixBenchmarkCatalog
{
    internal static readonly PosixBenchmarkCase[] Cases =
    [
        new("capture/sku007", "^([[:alpha:]]+)([0-9]+)$", "sku007", PosixBenchmarkOperation.Captures, 4096),
        new("capture/ambiguous", "(a|aa)(a*)", "aaa", PosixBenchmarkOperation.Captures),
        new("capture/repeated", "(a|aa)*", "aaaa", PosixBenchmarkOperation.Captures),
        new("capture/absent", "(aa)(b)?", "aa", PosixBenchmarkOperation.Captures),
        new("capture/empty", "(a*)(a*)", "aaa", PosixBenchmarkOperation.Captures),
        new("capture/unicode", "(é😀)(x*)", "é😀x", PosixBenchmarkOperation.Captures),
        new("capture/nonzero", "(é😀)(x*)", "😀é😀x", PosixBenchmarkOperation.Captures, StartOffsetInBytes: 4),
        new("is-match/hit", "^[A-Z]{3}[0-9]{4}$", "ABC0123", PosixBenchmarkOperation.IsMatch),
        new("is-match/miss", "^[A-Z]{3}[0-9]{4}$", "abc0123", PosixBenchmarkOperation.IsMatch),
        new("count/dense", "[a-z]+[0-9]+", string.Join(" ", Enumerable.Repeat("sku007", 64)), PosixBenchmarkOperation.Count),
        new("count/ambiguous", "a|aa", "aaaa", PosixBenchmarkOperation.Count),
        new("cursor/scalars", "()", "😀x", PosixBenchmarkOperation.Cursor),
        new("cursor/absent", "(aa)(b)?", "aa aa", PosixBenchmarkOperation.Cursor),
        new("cursor/ambiguous", "a|aa", "aaaa", PosixBenchmarkOperation.Cursor),
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
            "capture/repeated" => new(1, A(0, 4), A(2, 2), default),
            "capture/absent" => new(1, A(0, 2), A(0, 2), default),
            "capture/empty" => new(1, A(0, 3), A(0, 3), A(3, 0)),
            "capture/unicode" => new(1, U(0, 7, 0, 4), U(0, 6, 0, 3), U(6, 1, 3, 1)),
            "capture/nonzero" => new(1, U(4, 7, 2, 4), U(4, 6, 2, 3), U(10, 1, 5, 1)),
            "is-match/hit" => new(1, default, default, default),
            "is-match/miss" => default,
            "count/dense" => new(64, default, default, default),
            "count/ambiguous" => new(2, default, default, default),
            "cursor/scalars" => new(3, U(5, 0, 3, 0), U(5, 0, 3, 0), default),
            "cursor/absent" => new(2, A(3, 2), A(3, 2), default),
            "cursor/ambiguous" => new(2, A(2, 2), default, default),
            _ => throw new ArgumentException($"Unknown POSIX benchmark expectation: {id}"),
        };
    }
}

// The fixed caller-owned destinations cover every catalog capture slot. Native
// projection includes the same byte and derived UTF-16 coordinates. Consume the
// returned values after the measurement clock, like the existing Python batches.
internal readonly record struct PosixBenchmarkResult(int Count, Utf8ValueMatch Whole,
    Utf8ValueMatch Group1, Utf8ValueMatch Group2);
