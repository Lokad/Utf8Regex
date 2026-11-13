using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLeadingAsciiProjectionTests
{
    [Theory]
    [InlineData(@"\d+", false)] [InlineData(@"\d+", true)]
    [InlineData(@"(?P<n>\d+)", false)] [InlineData(@"(?P<n>\d+)", true)]
    public void ImmediateSearchNeedsNoUnrelatedSuffixClassification(string pattern, bool unicodeSuffix)
    {
        var input = Encoding.UTF8.GetBytes("1" + new string('a', 1 << 20) + (unicodeSuffix ? "😀" : ""));
        using var token = new CancellationTokenSource();
        using var regex = new Utf8PythonRegex(pattern, resourcePolicy: new()
        {
            MaxWorkSteps = 200, MaxWorkingBytes = 65536, MaxResultBytes = 65536
        });
        Assert.Equal("1", regex.SearchToString(input, 0, token.Token));
        var owned = regex.SearchDetailedData(input, 0, token.Token);
        Assert.Equal("1", owned.Value.ValueText);
        Assert.Equal((0, 1, 0, 1), (owned.Value.StartOffsetInBytes, owned.Value.EndOffsetInBytes,
            owned.Value.StartOffsetInUtf16, owned.Value.EndOffsetInUtf16));
    }

    [Theory]
    [InlineData(@"\d+", " é ١ 123", "١")]
    [InlineData(@"\d+", " x 𐒠 123", "𐒠")]
    [InlineData(@"\w+", "[]é abc", "é")]
    [InlineData(@"\s+", "x\u2003 123", "\u2003 ")]
    [InlineData(@"(?:\d+|a+)", "!!١ aaa", "١")]
    [InlineData(@"(?<=!)\d+(?!x)", "? ١x !٢ 123", "٢")]
    [InlineData(@"(?i:s+)", "!ſ SS", "ſ")]
    [InlineData(@"(?i:k+)", "!K KK", "K")]
    [InlineData(@"\D+", "١23é", "é")]
    [InlineData(@"\d*", "١x", "١")]
    public void ProjectedStartsDoNotSkipUnicodeOrAcceptAsciiHoles(string pattern, string subject, string expected)
    {
        using var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(expected, regex.SearchToString(input));
        Assert.Equal(expected, regex.SearchDetailedData(input).Value.ValueText);
    }

    [Theory]
    [InlineData(31)] [InlineData(32)] [InlineData(63)] [InlineData(64)]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4095)]
    public void BlockEdgesKeepScalarCandidatesAndDurableCaptureResults(int prefixLength)
    {
        var input = Encoding.UTF8.GetBytes(new string('!', prefixLength) + "😀١ 123");
        using var regex = new Utf8PythonRegex(@"(?P<n>\d+)");
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        Assert.Equal("١", retained.Groups[1].ValueText);
        Assert.Equal(prefixLength + 4, retained.Value.StartOffsetInBytes);
        Assert.Equal(prefixLength + 2, retained.Value.StartOffsetInUtf16);
        Assert.True(cursor.MoveNext()); Assert.Equal("123", cursor.Current.Value.ValueText);
        Assert.False(cursor.MoveNext()); cursor.Dispose();
        Assert.Equal("١", retained.Groups[1].ValueText); Assert.Equal(1, retained.LastIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.SearchToString(input, prefixLength + 1));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SkippedAsciiPrefixKeepsOriginalInterruption(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8PythonRegex(@"\d+", default, TimeSpan.FromMilliseconds(20));
        var input = Encoding.UTF8.GetBytes(new string('!', 200000) + "١");
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            entered = true;
            if (expire) ticks = 25; else token.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => regex.SearchToString(input, 0, token.Token));
            Assert.True(entered);
            if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.Equal("١", regex.SearchToString("١"u8));
    }

    [Fact]
    public void RemovingClassificationDoesNotBypassRealAdmission()
    {
        using var work = new Utf8PythonRegex(@"\d+", resourcePolicy: new() { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, Assert.Throws<Utf8ResourceLimitExceededException>(
            () => work.SearchToString("1"u8)).ResourceKind);
        using var working = new Utf8PythonRegex(@"(?P<n>\d+)", resourcePolicy: new() { MaxWorkingBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, Assert.Throws<Utf8ResourceLimitExceededException>(
            () => working.SearchToString("1"u8)).ResourceKind);
        using var result = new Utf8PythonRegex(@"\d+", resourcePolicy: new() { MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, Assert.Throws<Utf8ResourceLimitExceededException>(
            () => result.SearchToString("1"u8)).ResourceKind);
    }
}
