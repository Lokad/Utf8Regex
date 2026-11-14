using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSplitInterruptionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CapturedSplitChecksOriginalAuthorityBeforePublication(bool detailed, bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        const string pattern = "([,;])|(😀)";
        var timeout = TimeSpan.FromMilliseconds(20);
        using var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, timeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("éa,b😀c;", 8)));
        var checkpoints = new List<ulong>();
        object retained;
        var observation = Utf8SearchDiagnosticsSession.Start(steps => checkpoints.Add(steps));
        try { retained = Split(default); }
        finally { observation.Complete(); }
        Assert.NotEmpty(checkpoints);
        var expected = Values(retained);
        Assert.Contains(null, expected);
        Assert.Contains("😀", expected);

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
            object? published = null;
            Exception? failure;
            try { failure = Record.Exception(() => published = Split(token.Token)); }
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
            Assert.Equal(expected, Values(retained));
        }
        ticks = 0;
        Assert.Equal(expected, Values(Split(default)));

        object Split(CancellationToken token) => detailed
            ? regex.SplitDetailed(input, 0, 0, token)
            : regex.SplitToStrings(input, 0, 0, token);

        static string?[] Values(object result) => result is string?[] strings ? strings
            : ((Utf8PythonSplitItem[])result).Select(item => item.ValueText).ToArray();
    }
}
