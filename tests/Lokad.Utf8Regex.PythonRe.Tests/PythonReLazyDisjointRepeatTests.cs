using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReLazyDisjointRepeatTests
{
    [Theory]
    [InlineData("a+?b", "xxaaaabzz", "aaaab")]
    [InlineData("(a+?)b", "xxaaaabzz", "aaaab")]
    [InlineData("[ab]+?c", "xxababczz", "ababc")]
    [InlineData("[^x]*?x", "é😀xZ", "é😀x")]
    [InlineData("(?:😀)+?X", "é😀😀X", "😀😀X")]
    [InlineData(@"\d+?X", "é١٢X", "١٢X")]
    [InlineData("a{2,3}?b", "xaaaab aab", "aaab")]
    [InlineData("a+?a", "aaaa", "aa")]
    [InlineData("a+?", "aaaa", "a")]
    [InlineData("[^x]*?", "ab", "")]
    public void SearchRetainsScalarValuesAndEarliestEndpoints(string pattern, string text, string expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern);
        var match = regex.Search(input);
        Assert.True(match.Success);
        Assert.Equal(expected, match.GetValueString());
        Assert.Equal(expected, Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, match.StartOffsetInUtf16)), match.StartOffsetInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(expected), match.EndOffsetInBytes - match.StartOffsetInBytes);
        _ = regex.Count(input);
        Assert.Equal(expected, match.GetValueString());
    }

    [Fact]
    public void FullValidationAndUsefulLimitsRemain()
    {
        var regex = new Utf8PythonRegex("a+?b");
        Assert.Throws<ArgumentException>(() => regex.Search(new byte[] { (byte)'a', (byte)'b', 255 }));
        var working = new Utf8PythonRegex("a+?b", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => working.Search("aaab"u8)).ResourceKind);
        var work = new Utf8PythonRegex("a+?b", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => work.Search("aaab"u8)).ResourceKind);
        var results = new Utf8PythonRegex("a+?b", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => results.FindAllToStrings("aaab aab"u8)).ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionDuringTheRepeatDoesNotPoisonTheNextCall(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 8192) + "b");
        var validation = new Utf8SliceInvocation(new(1_000_000, 0, 0), Timeout.InfiniteTimeSpan, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var cutoff = validation.Budget.Steps + 128;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8PythonRegex("a+?b", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20),
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1 << 20 });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= cutoff) return;
            entered = true;
            if (expire) ticks = 50;
            else cancellation.Cancel();
        });
        try
        {
            if (expire)
                Assert.Equal("a+?b", Assert.Throws<RegexMatchTimeoutException>(() => regex.Search(input)).Pattern);
            else
                Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                    regex.Search(input, 0, cancellation.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered);
        ticks = 0;
        Assert.True(regex.Search("ab"u8).Success);
    }
}
