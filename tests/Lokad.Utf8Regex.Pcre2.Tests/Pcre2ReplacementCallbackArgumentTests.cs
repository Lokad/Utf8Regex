namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementCallbackArgumentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullCallbacksRejectBeforeDiscoveryAndOutputButAfterPreCancellation(bool finite)
    {
        var regex = new Utf8Pcre2Regex("a", Pcre2CompileOptions.None, default,
            finite ? new Utf8Pcre2ExecutionLimits { MatchLimit = 1, ResultLimitInBytes = 1 } : default,
            Timeout.InfiniteTimeSpan);
        foreach (var input in new[] { "a"u8.ToArray(), "b"u8.ToArray() })
        {
            Assert.Equal("evaluator", Assert.Throws<ArgumentNullException>(() =>
                regex.Replace<int>(input, 0, null!)).ParamName);
            Assert.Equal("evaluator", Assert.Throws<ArgumentNullException>(() =>
                regex.ReplaceToString<int>(input, 0, null!)).ParamName);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Replace<int>(input, 0, null!, 0, Pcre2MatchOptions.None, canceled.Token)).CancellationToken);
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.ReplaceToString<int>(input, 0, null!, 0, Pcre2MatchOptions.None, canceled.Token)).CancellationToken);
        }
    }

    [Fact]
    public void AValidCallbackCanStillReturnNullText()
    {
        var regex = new Utf8Pcre2Regex(".");
        Assert.Equal("", regex.ReplaceToString("😀"u8, 0,
            static (in Utf8Pcre2MatchContext match, ref int state) => null!));
    }
}
