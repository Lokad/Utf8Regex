using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSplitInterruptionTests
{
    [Theory]
    [InlineData("([,;])|(😀)", false)]
    [InlineData("([,;])|(😀)", true)]
    [InlineData("[,;]", false)]
    [InlineData("[,;]", true)]
    public void SplitChecksOriginalAuthorityBeforePublication(string pattern, bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var timeout = TimeSpan.FromMilliseconds(20);
        using var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = timeout,
            ResourcePolicy = new()
            {
                MaxDepth = 1024,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("éa.,b😀c;", 8)));
        var checkpoints = new List<ulong>();
        Utf8PythonCaptureRange[] retained;
        var observation = Utf8SearchDiagnosticsSession.Start(steps => checkpoints.Add(steps));
        try { retained = regex.SplitRanges(input, 0, 0, default); }
        finally { observation.Complete(); }
        Assert.NotEmpty(checkpoints);
        var expected = PythonReSplitAssert.Decode(input, retained);
        Assert.Equal("éa.", expected[0]);
        if (regex.CaptureGroupCount == 0)
        {
            Assert.Equal(17, expected.Length);
            Assert.Equal("b😀c", expected[1]);
        }
        else
        {
            Assert.Contains(null, expected);
            Assert.Contains("😀", expected);
        }

        foreach (var stop in checkpoints.Distinct())
        {
            ticks = 0;
            using var token = new CancellationTokenSource();
            var entered = false;
            var injection = Utf8SearchDiagnosticsSession.Start(steps =>
            {
                if (steps != stop) return;
                entered = true;
                if (expire) ticks = 50;
                else token.Cancel();
            });
            Utf8PythonCaptureRange[]? published = null;
            Exception? failure;
            try { failure = Record.Exception(() => published = regex.SplitRanges(input, 0, 0, token.Token)); }
            finally { injection.Complete(); }
            Assert.True(entered);
            Assert.Null(published);
            if (expire)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(pattern, error.Pattern);
                Assert.Equal(timeout, error.MatchTimeout);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(expected, PythonReSplitAssert.Decode(input, retained));
        }
        ticks = 0;
        Assert.Equal(expected, PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 0, 0, default)));
    }
}
