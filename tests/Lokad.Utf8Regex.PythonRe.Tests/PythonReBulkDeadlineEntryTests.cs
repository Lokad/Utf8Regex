using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReBulkDeadlineEntryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ExpiredEntryPrecedesDiscovery(int start)
    {
        foreach (var pattern in new[] { "cat|horse", "cat" })
        foreach (var input in new[] { "xcat"u8.ToArray(), "xcat😀"u8.ToArray() })
        {
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
            long reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => reads++ == 0 ? 0 : 1000);
            var error = Assert.Throws<RegexMatchTimeoutException>(() => string.Join("|", (regex).FindAllToUtf8(input, start, default).ScalarValues.Select(Encoding.UTF8.GetString)));
            Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
            Assert.Equal(regex.Pattern, error.Pattern);
            Assert.True(reads >= 2);
        }
    }

    [Fact]
    public void RetainedValuesAndScalarCoordinatesSurviveTimeoutAndFreshCalls()
    {
        var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        var input = Encoding.UTF8.GetBytes("π😀 cat horse cat");
        using var steadyClock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => 0);
        var wholeMatches2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = regex.CreateMatchCursor(input, 7, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
            {
                wholeMatches2.Add(rangeMatch2);
            }
        }
        var detailed = wholeMatches2.ToArray();
        Assert.Equal(new[] { "cat", "horse", "cat" }, detailed.Select(x => System.Text.Encoding.UTF8.GetString(x.GetValueMemory().Span)));
        var retained = string.Join("|", (regex).FindAllToUtf8(input, 7, default).ScalarValues.Select(Encoding.UTF8.GetString));
        long reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => reads++ == 0 ? 0 : 1000))
            Assert.Throws<RegexMatchTimeoutException>(() => string.Join("|", (regex).FindAllToUtf8(input, 7, default).ScalarValues.Select(Encoding.UTF8.GetString)));
        Assert.Equal(retained, string.Join("|", (regex).FindAllToUtf8(input, 7, default).ScalarValues.Select(Encoding.UTF8.GetString)));
        Assert.Equal(new[] { "cat", "horse", "cat" }, detailed.Select(x => System.Text.Encoding.UTF8.GetString(x.GetValueMemory().Span)));
        Assert.Equal(0, regex.FindAllToUtf8(input, input.Length).Count);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => string.Join("|", (regex).FindAllToUtf8(input, 7, canceled.Token).ScalarValues.Select(Encoding.UTF8.GetString)));
        Assert.Equal(canceled.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData("ab[0-9]+", "π😀 ab123 ab4", 7, "ab123|ab4")]
    [InlineData("^cat", "π😀 cat", 7, "")]
    [InlineData("cat$", "π😀 cat", 7, "cat")]
    [InlineData("(?i)k", "π😀 k K K", 7, "k|K|K")]
    [InlineData("[α-ω]+", "π😀 αβ γδ", 7, "αβ|γδ")]
    public void NativeAnchoredAndScalarFallbacksKeepTheWholeSubject(
        string pattern, string text, int start, string expected)
    {
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => 0);
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, string.Join("|", (regex).FindAllToUtf8(input, start, default).ScalarValues.Select(Encoding.UTF8.GetString)));
    }
}
