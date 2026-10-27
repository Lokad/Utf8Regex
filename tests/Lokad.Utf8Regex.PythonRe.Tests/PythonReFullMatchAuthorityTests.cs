using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFullMatchAuthorityTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    public void OriginalInvocationInterruptsRepeatedCallsAndFreshCallRecovers(int result, bool expire)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀a", 256)));
        var retained = regex.FullMatchDetailedData("😀a"u8);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            using var cancellation = new CancellationTokenSource();
            var entered = false;
            var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
            {
                entered = true;
                if (expire) ticks = 25; else cancellation.Cancel();
            });
            Exception? failure;
            try { failure = Record.Exception(() => Run(regex, input, result, cancellation.Token)); }
            finally { diagnostics.Complete(); }
            Assert.True(entered);
            if (expire)
            {
                var timeout = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(TimeSpan.FromMilliseconds(20), timeout.MatchTimeout);
                Assert.Equal(Encoding.UTF8.GetString(input), timeout.Input);
            }
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            ticks = 0;
            Assert.True(regex.FullMatch(input).Success);
            Assert.Equal("x", retained.LastGroup);
            Assert.Equal("😀a", retained.Value.ValueText);
        }
    }

    [Fact]
    public async Task ConcurrentFirstCallsHaveIndependentCancellationAndRetainCaptures()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+");
        using var ready = new Barrier(2);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Task Run(bool cancel) => Task.Factory.StartNew(() =>
        {
            Assert.True(ready.SignalAndWait(TimeSpan.FromSeconds(10)));
            if (cancel)
            {
                var error = Assert.Throws<OperationCanceledException>(() => regex.FullMatch("😀a"u8, 0, cancellation.Token));
                Assert.Equal(cancellation.Token, error.CancellationToken);
            }
            else
            {
                var retained = regex.FullMatchDetailedData("😀a"u8);
                Assert.True(regex.FullMatch("aa"u8).Success);
                Assert.Equal("😀a", retained.Value.ValueText);
                Assert.Equal("x", retained.LastGroup);
                Assert.Equal(1, retained.LastIndex);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(Run(true), Run(false));
        Assert.True(regex.FullMatch("😀a"u8).Success);
    }

    [Fact]
    public void FullMatchNeedsNoConstructionAuthorityAndRetainsExecutionWorkAdmission()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 16384, MaxResultBytes = 1 });
        using var construction = new Utf8ConstructionScope(0, 0, 0, 0);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FullMatch("😀a"u8));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
            Assert.Equal(1ul, error.Limit);
            Assert.Equal(2ul, error.Requested);
        }
        Assert.Equal(0ul, construction.Budget.WorkSteps);
        Assert.Equal(0ul, construction.Budget.ReservedBytes);
    }

    private static void Run(Utf8PythonRegex regex, byte[] input, int result, CancellationToken token)
    {
        if (result == 0) _ = regex.FullMatch(input, 0, token);
        else if (result == 1) _ = regex.FullMatchDetailed(input, 0, token);
        else if (result == 2) _ = regex.FullMatchDetailedData(input, 0, token);
        else _ = regex.FullMatchToString(input, 0, token);
    }
}
