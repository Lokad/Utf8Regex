using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NegatedPairSpanTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(15)] [InlineData(31)]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    [InlineData(4095)] [InlineData(4096)]
    public void ScalarTailsAndOffsetCapturesStayIntact(int prefix)
    {
        var value = new string('x', prefix) + "é😀𝒜";
        var text = "skip!" + value + "a tail";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(@"!(?<run>[^ab]*)a");
        var match = regex.MatchDetailed(input);
        Assert.True(match.Success);
        Assert.Equal("!" + value + "a", match.GetValueString());
        Assert.Equal(value, match.GetGroup(regex.GroupNumberFromName("run")).GetValueString());
        Assert.Equal(1, regex.Count(input));
        Assert.True(regex.IsMatch(input));
        Assert.True(regex.Match(input).Success);
    }

    [Theory]
    [InlineData(@"([^ab]*)a", "a", "")]
    [InlineData(@"([^ab]+)a", "é😀a", "é😀")]
    [InlineData(@"([^ab]+)a", "a", null)]
    [InlineData(@"([^AB]*)A", "é😀A", "é😀")]
    [InlineData(@"([^Aa]*)a", "é😀a", "é😀")]
    [InlineData(@"([^ab]{2,})a", "😀a", null)]
    [InlineData(@"([^ab]{1,2})a", "é😀a", "é😀")]
    [InlineData(@"([^ab]*)x", "xx", "x")]
    [InlineData(@"([^abc]*)a", "é😀a", "é😀")]
    [InlineData(@"((?i:[^ab])*)a", "xAa", "")]
    [InlineData(@"((?>[^ab]*))a", "é😀a", "é😀")]
    [InlineData(@"([^ab]*?)a", "é😀a", "é😀")]
    public void MinimaMasksAndRewindKeepTheirSemantics(string pattern, string text, string? expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var match = new Utf8Regex(pattern).MatchDetailed(input);
        Assert.Equal(expected is not null, match.Success);
        if (match.Success) Assert.Equal(expected, match.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void ExecutionOptionsPreserveCapturedValues(RegexOptions options)
    {
        var regex = new Utf8Regex(@"([^ab]+)a", options);
        var input = "é😀a 𝒜a"u8.ToArray();
        var match = regex.MatchDetailed(input);
        Assert.Equal(options == RegexOptions.RightToLeft ? " 𝒜" : "é😀", match.GetGroup(1).GetValueString());
        Assert.Equal(2, regex.Count(input));
    }

    [Fact]
    public void WholeSubjectValidationRejectsMalformedBytesAfterAnEarlyStop()
    {
        Assert.Throws<ArgumentException>(() => new Utf8Regex(@"([^ab]*)a").Count(new byte[] { (byte)'a', 0xFF }));
    }

    [Fact]
    public void EveryWorkCutoffReleasesTheEnclosingLoan()
    {
        var program = new Utf8Regex(@"\A[^ab]*a").Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(new string('é', 10000) + "a");
        var baseline = new Utf8SliceInvocation(new(10000, 1000, 1 << 20), Regex.InfiniteMatchTimeout, default);
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref baseline);
        Assert.Equal(1, result.Count);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 1000, 1 << 20), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var denied = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BoundedScansPreserveOriginalInterruption(bool expire)
    {
        var program = new Utf8Regex(@"\A[^ab]*a").Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(new string('é', 100000) + "a");
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (expire) ticks = 25; else token.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(100000, 1000, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var failure = Record.Exception(() => Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation));
            if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }
    }
}
