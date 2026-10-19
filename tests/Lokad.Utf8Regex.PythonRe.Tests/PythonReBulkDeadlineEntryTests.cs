using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReBulkDeadlineEntryTests
{
    [Theory]
    [InlineData(0, 0)] [InlineData(0, 1)]
    [InlineData(1, 0)] [InlineData(1, 1)]
    [InlineData(2, 0)] [InlineData(2, 1)]
    public void ExpiredEntryPrecedesMalformedPrefixAndTail(int operation, int start)
    {
        foreach (var pattern in new[] { "cat|horse", "cat" })
        foreach (var input in new[] { new byte[] { 0xFF, 99, 97, 116 }, new byte[] { 120, 99, 97, 116, 0xFF } })
        {
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
            long reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => reads++ == 0 ? 0 : 1000);
            var error = Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, input, start, operation));
            Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
            Assert.Equal(regex.Pattern, error.Pattern);
            Assert.True(reads >= 2);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void RetainedValuesAndScalarCoordinatesSurviveTimeoutAndFreshCalls(int operation)
    {
        var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        var input = Encoding.UTF8.GetBytes("π😀 cat horse cat");
        using var steadyClock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => 0);
        var detailed = regex.FindAll(input, 7);
        Assert.Equal(new[] { "cat", "horse", "cat" }, detailed.Select(x => x.ValueText));
        Assert.Equal(new[] { (7, 10, 4, 7), (11, 16, 8, 13), (17, 20, 14, 17) }, detailed.Select(x =>
            (x.StartOffsetInBytes, x.EndOffsetInBytes, x.StartOffsetInUtf16, x.EndOffsetInUtf16)));
        var retained = Run(regex, input, 7, operation);
        long reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => reads++ == 0 ? 0 : 1000))
            Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, input, 7, operation));
        Assert.Equal(retained, Run(regex, input, 7, operation));
        Assert.Equal(new[] { "cat", "horse", "cat" }, detailed.Select(x => x.ValueText));
        Assert.Empty(regex.FindAll(input, input.Length));

        var malformed = input.Concat(new byte[] { 0xFF }).ToArray();
        Assert.Throws<ArgumentException>(() => Run(regex, malformed, 7, operation));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => Run(regex, malformed, 7, operation, canceled.Token));
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
        for (var operation = 0; operation < 3; operation++)
            Assert.Equal(expected, Run(regex, input, start, operation));
    }

    private static string Run(Utf8PythonRegex regex, byte[] input, int start, int operation,
        CancellationToken token = default) => operation switch
    {
        0 => string.Join("|", regex.FindAll(input, start, token).Select(x => x.ValueText)),
        1 => string.Join("|", regex.FindAllToStrings(input, start, token).ScalarValues),
        _ => string.Join("|", regex.FindAllToUtf8(input, start, token).ScalarValues.Select(Encoding.UTF8.GetString)),
    };
}
