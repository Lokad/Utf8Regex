using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2UnsupportedPartialDecisionTests
{
    private static Utf8Pcre2Regex Create(string pattern, int policy = 0) =>
        Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.None, default, new()
        {
            MaxWorkSteps = policy is 1 or 5 ? 1_000_000UL : null,
            MaxDepth = policy is 2 or 5 ? 1024U : null,
            MaxWorkingBytes = policy is 3 or 5 ? 1_048_576UL : null,
            MaxResultBytes = policy is 4 or 5 ? 1_048_576UL : null,
        }, Timeout.InfiniteTimeSpan);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void OrdinaryResultsCannotStandInForUnsupportedPartialDecisions(int policy)
    {
        var cases = new (string Pattern, string Text, Pcre2PartialMode Mode)[]
        {
            ("(?<x>a+)", "aaa", Pcre2PartialMode.Hard),
            ("(?<x>a+)b|a", "a", Pcre2PartialMode.Hard),
            ("(abc)?abc", "abc", Pcre2PartialMode.Hard),
            ("(*MARK:tag)(?<x>a+)b", "aaa", Pcre2PartialMode.Soft),
            ("(*MARK:tag)(?<x>a+)b", "aaa", Pcre2PartialMode.Hard),
        };
        foreach (var item in cases)
        {
            var regex = Create(item.Pattern, policy);
            Assert.Equal(Pcre2PartialProbeProgramKind.None, regex.DebugCompiledProgram.PartialProbe.Kind);
            var input = Encoding.UTF8.GetBytes(item.Text);
            Assert.Throws<NotSupportedException>(() => { _ = regex.Probe(input, item.Mode); });
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GenericSoftFullMatchKeepsCapturesAndMark(bool finite)
    {
        var regex = Create("(*MARK:tag)(?<x>a+)", finite ? 5 : 0);
        var probe = regex.Probe("éaaa"u8, Pcre2PartialMode.Soft);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, probe.Kind);
        Assert.Equal("tag", probe.Mark);
        var match = probe.GetMatch();
        Assert.Equal("aaa", match.GetValueString());
        Assert.Equal("aaa", match.GetGroup(1).GetValueString());
        Assert.Equal((2, 5), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
    }

    [Fact]
    public void NoneModeKeepsMarkedMissAndCuratedPartialRemainsAvailable()
    {
        var miss = Create("(*MARK:tag)(?<x>a+)b", 5).Probe("aaa"u8, Pcre2PartialMode.None);
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, miss.Kind);
        Assert.Equal("tag", miss.Mark);
        var partial = Create("(a)b+", 5).Probe("abbb"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, partial.Kind);
        Assert.Equal((0, 4), (partial.Value.StartOffsetInBytes, partial.Value.EndOffsetInBytes));
        Assert.Throws<NotSupportedException>(() =>
        { _ = Create("(?<x>a+)", 5).Probe("zzz"u8, Pcre2PartialMode.Soft); });
    }

    [Fact]
    public void PreCancellationAndStrictValidationPrecedeUnsupportedDecision()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var regex = Create("(?<x>a+)", 5);
        var malformed = new byte[] { (byte)'a', 0xFF };
        var failure = Assert.Throws<OperationCanceledException>(() =>
        { _ = regex.Probe(malformed, Pcre2PartialMode.Hard, 0, Pcre2MatchOptions.None, source.Token); });
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Throws<ArgumentException>(() => { _ = regex.Probe(malformed, Pcre2PartialMode.Hard); });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GenericSoftDiscoveryKeepsOriginalDeadlineAndToken(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(?<x>a+)", Pcre2CompileOptions.None, default, default,
            TimeSpan.FromMilliseconds(1));
        var armed = false;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!armed) { armed = true; return 0; }
            if (cancel) { source.Cancel(); return 0; }
            return 2;
        });
        var input = Encoding.UTF8.GetBytes(new string('a', 8192));
        var failure = Assert.ThrowsAny<Exception>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.None, source.Token); });
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }
}
