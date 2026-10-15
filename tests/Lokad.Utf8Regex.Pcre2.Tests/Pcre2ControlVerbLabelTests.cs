using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2ControlVerbLabelTests
{
    // Native PCRE2 10.47 UTF/UCP, without ALT_VERBNAMES: names are literal text.
    [Theory]
    [InlineData("(*MARK:(?<100>a)a", "(?<100>a")]
    [InlineData("(*:😀(name)a", "😀(name")]
    [InlineData("(*MARK:[tag)a", "[tag")]
    [InlineData(@"(*MARK:\C)a", @"\C")]
    [InlineData(@"(*MARK:(?=\K)a", @"(?=\K")]
    [InlineData("(*MARK:(?C)a", "(?C")]
    [InlineData("(*MARK:{5,4})a", "{5,4}")]
    [InlineData("(*PRUNE:(tag)a", "(tag")]
    [InlineData("(*THEN:(tag)a", "(tag")]
    [InlineData("(*COMMIT:(tag)a", "(tag")]
    [InlineData("(*SKIP:(tag)a", null)]
    public void LabelsStayLiteralAcrossTextByteAndFiniteEntries(string pattern, string? mark)
    {
        foreach (var finite in new[] { false, true })
        {
            var limits = finite ? new Utf8Pcre2ExecutionLimits
            {
                MatchLimit = 1_000_000, DepthLimit = 256,
                HeapLimitInBytes = 1_048_576, ResultLimitInBytes = 1_048_576,
            } : default;
            var text = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Ucp, default,
                limits, Timeout.InfiniteTimeSpan);
            var bytes = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), Pcre2CompileOptions.Ucp,
                default, limits, Timeout.InfiniteTimeSpan);
            foreach (var regex in new[] { text, bytes })
            {
                var match = regex.MatchDetailed("a"u8);
                Assert.True(match.Success);
                Assert.Equal("a", match.GetValueString());
                Assert.Equal(1, match.CaptureSlotCount);
                Assert.Equal(mark, match.Mark);
            }
        }
    }

    [Theory]
    [InlineData("(*atomic:(?<100>a))", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData(@"(*atomic:\C)", Pcre2ErrorKind.BackslashCDisabled)]
    [InlineData("(*MARK:(tag)(?<100>a)", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData(@"(*MARK:(tag)\C", Pcre2ErrorKind.BackslashCDisabled)]
    [InlineData("(*MARK:(tag)(?C)a", Pcre2ErrorKind.CalloutUnsupported)]
    [InlineData("(*MARK:(tag)a(", Pcre2ErrorKind.MissingClosingParenthesis)]
    [InlineData("(*MARK:unterminated", Pcre2ErrorKind.MissingClosingParenthesis)]
    public void RealRegexBodiesAndFollowingSyntaxRemainValidated(string pattern, Pcre2ErrorKind kind)
    {
        Assert.Equal(kind, Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern)).ErrorKind);
    }

    [Fact]
    public void RetainedMarkAndCapturesKeepTheirScalarRangesAndOriginalAuthority()
    {
        var regex = Utf8Pcre2Regex.Create("(*MARK:(?<100>😀)(é)(😀)", Pcre2CompileOptions.Ucp, default,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 256,
                MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes("xé😀xé😀");
        var retained = regex.MatchDetailed(input);
        Assert.Equal("(?<100>😀", retained.Mark);
        Assert.Equal(3, retained.CaptureSlotCount);
        Assert.Equal((1, 7), (retained.GetGroup(0).StartOffsetInBytes, retained.GetGroup(0).EndOffsetInBytes));
        Assert.Equal("é", retained.GetGroup(1).GetValueString());
        Assert.Equal("😀", retained.GetGroup(2).GetValueString());
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("(?<100>😀", retained.Mark);
        Assert.Equal("x<(?<100>😀:😀>x<(?<100>😀:😀>",
            regex.ReplaceToString(input, "<${*MARK}:$2>"));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LabelSearchBlocksKeepTheOriginalConstructionOwner(bool timeout)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 37;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
        owner.Budget.Deadline = deadline;
        owner.Budget.CancellationToken = cancellation.Token;
        var polls = 0;
        owner.Budget.CheckpointHook = _ =>
        {
            if (++polls != 2) return;
            if (timeout) ticks = 30000;
            else cancellation.Cancel();
        };
        var pattern = "(*MARK:" + new string('a', 4096) + "(?<100>a)";
        var offset = 0;
        var failure = Record.Exception(() => Pcre2PatternCodeScan.TrySkipControlVerbLabel(pattern, ref offset));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(37, parent[0]);
        Assert.Equal(0, offset);
        Assert.Equal(2, polls);
    }
}
