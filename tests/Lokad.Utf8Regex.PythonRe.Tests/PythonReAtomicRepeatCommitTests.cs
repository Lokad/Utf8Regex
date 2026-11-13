using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReAtomicRepeatCommitTests
{
    [Theory]
    [InlineData("a++b", "xxaaaabzz", "aaaab")]
    [InlineData("(?>ab+)c", "xxabbbczz", "abbbc")]
    [InlineData("(?>ab+)c", "xxabbbdzz", null)]
    [InlineData("(?>(a+))b", "xxaaaabzz", "aaaab")]
    [InlineData("(?>x(?>a+))b", "xxaaaabzz", "xaaaab")]
    [InlineData("a{2,3}+a", "aaaa", "aaaa")]
    [InlineData("a{2,3}+b", "ab aaab", "aaab")]
    [InlineData("a*+b", "b", "b")]
    [InlineData("a*+", "b", "")]
    [InlineData("(?>[^x]*)x", "é😀xZ", "é😀x")]
    [InlineData("😀++X", "é😀😀X", "😀😀X")]
    [InlineData("(?>.+)X", "😀X", null)]
    [InlineData("(?>.+)X|😀", "😀X", "😀")]
    [InlineData("(?>a+?)b", "xxaaaabzz", "ab")]
    [InlineData("(?>a+?)a", "aaaa", "aa")]
    [InlineData("(a)++b", "aaab", "aaab")]
    public void SearchRetainsAtomicScalarEndpoints(string pattern, string text, string? expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern);
        var match = regex.Search(input);
        Assert.Equal(expected is not null, match.Success);
        if (expected is null) return;
        Assert.Equal(expected, match.GetValueString());
        Assert.Equal(expected, Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, match.StartOffsetInUtf16)), match.StartOffsetInBytes);
        _ = regex.Count(input);
        Assert.Equal(expected, match.GetValueString());
    }

    [Fact]
    public void FullSubjectValidationAndMeaningfulLimitsRemain()
    {
        var regex = new Utf8PythonRegex("a++b");

        foreach (var (policy, expected) in new[]
        {
            (new Utf8ResourcePolicy { MaxWorkingBytes = 1 }, Utf8ResourceLimitKind.ExecutionWorkingBytes),
            (new Utf8ResourcePolicy { MaxWorkSteps = 1 }, Utf8ResourceLimitKind.ExecutionWorkSteps),
        })
        {
            var denied = new Utf8PythonRegex("a++b", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
            Assert.Equal(expected, Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Search("aaab"u8)).ResourceKind);
        }
        var results = new Utf8PythonRegex("a++b", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => results.FindAllToStrings("aaab aab"u8)).ResourceKind);
        var depth = new Utf8PythonRegex("(?>x(?>a+))b", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxDepth = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => depth.Search("xaaab"u8)).ResourceKind);
    }

    [Fact]
    public void FundedInvocationReleasesLoansAndRetainedCapturesSurviveDispose()
    {
        var allowance = new Allowance(16 << 20);
        var regex = Utf8PythonRegex.Compile("(?>(a+))b", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, allowance);
        var held = allowance.Used;
        var retained = regex.SearchDetailedData("xxaaaabzz"u8);
        Assert.True(retained.Success);
        Assert.Equal("aaaa", retained.Groups[1].ValueText);
        Assert.Equal(held, allowance.Used);
        Assert.False(regex.FullMatch("aaaabX"u8).Success);
        Assert.Equal(held, allowance.Used);
        regex.Dispose();
        Assert.Equal(0UL, allowance.Used);
        Assert.Equal("aaaa", retained.Groups[1].ValueText);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionDuringGreedyRunDoesNotPoisonReuse(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 8192) + "b");
        var validation = new Utf8SliceInvocation(new(1_000_000, 0, 0), Timeout.InfiniteTimeSpan, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var cutoff = validation.Budget.Steps + 128;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8PythonRegex("a++b", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20),
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 8, MaxWorkingBytes = 1 << 20 });
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
                Assert.Equal("a++b", Assert.Throws<RegexMatchTimeoutException>(() => regex.Search(input)).Pattern);
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
