using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TerminalClassCountTests
{
    private static Utf8ResourcePolicy Funded => new()
    {
        MaxWorkSteps = 1_000_000, MaxDepth = 128,
        MaxWorkingBytes = 1_000_000, MaxResultBytes = 1_000_000,
    };

    public static IEnumerable<object[]> ScalarCases()
    {
        foreach (var compiled in new[] { false, true })
        foreach (var finite in new[] { false, true })
        {
            yield return [@"\p{L}+", " café 中 😀x ", 3, compiled, finite];
            yield return [@"\p{L}{2,4}", "a ab abc abcd abcde abcdefgh abcdefghi", 8, compiled, finite];
            yield return [@"\p{L}{3}", "a ab abc abcd abcdefg", 4, compiled, finite];
            yield return [@"[\p{L}-[AEIOU]]+", " ABCDE é 😀x A fgh ", 4, compiled, finite];
            yield return [@"[\p{L}-[AEIOU]]{2,3}", " A BBBB A éé A xxxxxx A ", 4, compiled, finite];
            yield return [@"(?i:[Ā-ſЀ-ӿ一-鿿])+", "āѐ中文 x Ā", 2, compiled, finite];
            yield return [@"[\p{So}]+", "😀😀 x 😀 y", 2, compiled, finite];
            yield return [@"[\p{So}]{2,3}", "😀 😀😀 😀😀😀😀 😀😀😀😀😀😀😀", 4, compiled, finite];
            yield return [@"\w+", "", 0, compiled, finite];
            yield return [@"\d{2,4}", "x1x22x333x4444x55555x66666666", 6, compiled, finite];
            yield return [@"\p{L}{1000}", new string('é', 999), 0, compiled, finite];
        }
    }

    [Theory]
    [MemberData(nameof(ScalarCases))]
    public void PublicCountPreservesGreedyScalarRuns(string pattern, string text, int expected,
        bool compiled, bool finite)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, finite ? Funded : default);
        var input = Encoding.UTF8.GetBytes(text);
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expected, regex.Count(input, cancellation.Token));
        var matches = regex.EnumerateMatches(input);
        var observed = 0;
        while (matches.MoveNext()) observed++;
        Assert.Equal(expected, observed);
    }

    [Theory]
    [InlineData(0, 2)] [InlineData(2, 2)] [InlineData(4, 1)]
    [InlineData(6, 1)] [InlineData(7, 1)] [InlineData(8, 1)]
    [InlineData(9, 1)] [InlineData(11, 0)] [InlineData(13, 0)]
    public void ByteFloorsCanBeginInsideARun(int floor, int expected)
    {
        var regex = new Utf8Regex(@"\p{L}{2,3}", RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout, Funded);
        var input = "ééé x éé"u8;
        Assert.Equal(expected, regex.Count(input[floor..]));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();
        var invocation = new Utf8SliceInvocation(new(100_000, 1, 1, 1), Regex.InfiniteMatchTimeout, default);
        var outcome = Utf8SliceRunner.CountFromBytes(program, input, floor, [], [], ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
        Assert.Equal(expected, outcome.Count);
    }

    [Fact]
    public void SmallStorageAndDepthCapsStillAdmitNoOutputCount()
    {
        var regex = new Utf8Regex(@"\p{L}+", RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout, new Utf8ResourcePolicy
            { MaxWorkSteps = 100_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(3, regex.Count("é 中 x"u8));
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { 0xc3, 0xa9, 0xff }));
    }

    [Theory]
    [InlineData(@"(\p{L})+", "abc def", 2)]
    [InlineData(@"\p{L}*", "a b", 4)]
    [InlineData(@"\p{L}+?", "abc def", 6)]
    [InlineData(@"^\p{L}+$", "abc def", 0)]
    [InlineData(@"\p{L}+x", "abx cdx", 2)]
    public void ExcludedShapesKeepTheirPublicResults(string pattern, string text, int expected)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking })
            Assert.Equal(expected, new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Funded)
                .Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void TerminalPublicationObservesCancellationFromItsLastCheckpoint()
    {
        var regex = new Utf8Regex(@"\p{L}+");
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();
        var input = Encoding.UTF8.GetBytes(new string('é', 62));
        using var cancellation = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => cancellation.Cancel());
        try
        {
            var invocation = new Utf8SliceInvocation(new(100_000, 1, 1, 1),
                Regex.InfiniteMatchTimeout, cancellation.Token);
            var error = Assert.Throws<OperationCanceledException>(() =>
                Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation));
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.Equal(64UL, invocation.Budget.Steps);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void OriginalWorkCancellationAndDeadlineStopTheWholeCount()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("éé ", 2048)));
        var limited = new Utf8Regex(@"\p{L}{2,4}", RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout, Funded with { MaxWorkSteps = 32 });
        var work = Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, work.ResourceKind);
        Assert.Equal(33UL, work.Requested);

        var regex = new Utf8Regex(@"\p{L}{2,4}", RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(20), Funded);
        using var cancellation = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => cancellation.Cancel());
        try
        {
            var error = Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
        }
        finally { session.Complete(); }
        var expired = false;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => expired ? 30_000 : 0);
        session = Utf8SearchDiagnosticsSession.Start(_ => expired = true);
        try { Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input)); }
        finally { session.Complete(); }
        expired = false;
        Assert.Equal(2048, regex.Count(input));
    }
}
