using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2DfaOverlapTests
{
    [Theory]
    [InlineData(@"^\d*1$", "11", true)]
    [InlineData(@"^\d*1$", "2111", true)]
    [InlineData(@"^\d*1$", "11\n", true)]
    [InlineData(@"^\d*1$", "112", false)]
    [InlineData(@"^\d*[12]$", "1212", true)]
    [InlineData(@"^\d*[12]$", "1213", false)]
    [InlineData(@"^\d*\.?\d*1$", "11", true)]
    [InlineData(@"^\d*\.?\d*1$", "1.21", true)]
    [InlineData(@"^\d*\.?\d*1$", "1.211", true)]
    [InlineData(@"^\d*\.?\d*1$", "1.21\n", true)]
    [InlineData(@"^\d*\.?\d*1$", "1.21x", false)]
    [InlineData(@"^\d+\d*[12]$", "1112", true)]
    public void PublicBooleanExecutionRetainsEveryOverlappingPath(string pattern, string text, bool expected)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var finite = new Utf8Pcre2Regex(pattern, default, default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1_000_000 }, Timeout.InfiniteTimeSpan);
        var selected = Assert.IsType<Pcre2AsciiAnchoredDfaIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, Regex.IsMatch(text, pattern)); // Deliberately overlapping ASCII semantics.
        Assert.Equal(expected, direct.IsMatch(bytes));
        Assert.Equal(expected, finite.IsMatch(bytes));
        Assert.Equal(expected ? 1 : 0, direct.Count(bytes));
        Assert.Equal(expected, direct.MatchDetailed(bytes).Success);

        // Inspect the actual published graph, not a parallel test compiler.
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        object graph = selected.Dfa;
        var type = graph.GetType();
        var offsets = (int[])type.GetField("_transitionOffsets", flags)!.GetValue(graph)!;
        var counts = (int[])type.GetField("_transitionCounts", flags)!.GetValue(graph)!;
        var sets = (AsciiCharClass[])type.GetField("_transitionSets", flags)!.GetValue(graph)!;
        for (var state = 0; state < counts.Length; state++)
            for (var left = 0; left < counts[state]; left++)
                for (var right = left + 1; right < counts[state]; right++)
                    Assert.True(sets[offsets[state] + left].IsDisjoint(sets[offsets[state] + right]));
    }

    [Fact]
    public void CorrectedBooleanRouteKeepsValidationAndOriginalInterruption()
    {
        var expression = new Utf8Pcre2Regex(@"^\d*1$");
        Assert.Throws<ArgumentException>(() => expression.IsMatch([0x31, 0x31, 0xff]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = Assert.ThrowsAny<OperationCanceledException>(() => expression.IsMatch("11"u8, 0, default, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        var limited = new Utf8Pcre2Regex(@"^\d*1$", default, default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("11"u8)).ErrorKind);
    }
}
