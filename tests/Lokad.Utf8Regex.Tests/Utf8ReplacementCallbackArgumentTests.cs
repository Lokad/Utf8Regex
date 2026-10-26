using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ReplacementCallbackArgumentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullCallbacksRejectBeforeDiscoveryAndOutputButAfterPreCancellation(bool finite)
    {
        var regex = new Utf8Regex("a", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            finite ? new Utf8ResourcePolicy { MaxWorkSteps = 1, MaxResultBytes = 1 } : default);
        foreach (var input in new[] { "a"u8.ToArray(), "b"u8.ToArray() })
        {
            Assert.Equal("evaluator", Assert.Throws<ArgumentNullException>(() =>
                regex.Replace<int>(input, 0, null!)).ParamName);
            Assert.Equal("evaluator", Assert.Throws<ArgumentNullException>(() =>
                regex.ReplaceToString<int>(input, 0, null!)).ParamName);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Replace<int>(input, 0, null!, canceled.Token)).CancellationToken);
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.ReplaceToString<int>(input, 0, null!, canceled.Token)).CancellationToken);
        }
    }

    [Fact]
    public void AValidCallbackCanStillReturnNullText()
    {
        var regex = new Utf8Regex(".");
        Assert.Equal("", regex.ReplaceToString("😀"u8, 0,
            static (in Utf8MatchContext match, ref int state) => null!));
    }
}
