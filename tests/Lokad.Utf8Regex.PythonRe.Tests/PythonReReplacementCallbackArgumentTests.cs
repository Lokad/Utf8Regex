namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReReplacementCallbackArgumentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullCallbacksRejectBeforeDiscoveryAndOutputButAfterPreCancellation(bool finite)
    {
        var regex = new Utf8PythonRegex("a", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            finite ? new Utf8ResourcePolicy { MaxWorkSteps = 1, MaxResultBytes = 1 } : default);
        foreach (var input in new[] { "a"u8.ToArray(), "b"u8.ToArray() })
        {
            Assert.Equal("evaluator", Assert.Throws<ArgumentNullException>(() =>
                regex.Subn<int>(input, 0, (Utf8PythonUtf8MatchEvaluator<int>)null!, 0, 0)).ParamName);
            Assert.Equal("evaluator", Assert.Throws<ArgumentNullException>(() =>
                regex.Subn<int>(input, 0, (Utf8PythonMatchEvaluator<int>)null!, 0, 0)).ParamName);
            Assert.Equal("evaluator", Assert.Throws<ArgumentNullException>(() =>
                regex.SubnToString<int>(input, 0, null!, 0, 0)).ParamName);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Subn<int>(input, 0, (Utf8PythonUtf8MatchEvaluator<int>)null!, 0, 0, canceled.Token)).CancellationToken);
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.SubnToString<int>(input, 0, null!, 0, 0, canceled.Token)).CancellationToken);
        }
    }

    [Fact]
    public void AValidCallbackCanStillReturnEmptyText()
    {
        var regex = new Utf8PythonRegex(".");
        Assert.Equal("", regex.SubnToString("😀"u8, 0, static (state, match) => "").ResultText);
    }
}
