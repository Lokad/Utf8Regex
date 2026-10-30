using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReRequiredLiteralScreenTests
{
    [Theory]
    [InlineData("a+b")]
    [InlineData("a++b")]
    [InlineData("(?>a+)b")]
    [InlineData("(?P<x>a+)b")]
    [InlineData("(?i:a+)b")]
    [InlineData("é+😀")]
    public void AbsentRequiredLiteralFinishesBeforeScalarScratch(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        using var token = new CancellationTokenSource();
        var input = "aaaa"u8.ToArray();
        Assert.False(regex.Search(input, 0, token.Token).Success);
        Assert.False(regex.Match(input, 0, token.Token).Success);
        Assert.False(regex.IsMatch(input, 0, token.Token));
        Assert.Equal(0, regex.Count(input, 0, token.Token));
        // Eager full preparation avoids testing its separate cold construction cap.
        using var full = Utf8PythonRegex.Compile(pattern, PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, regex.ResourcePolicy, default, Timeout.InfiniteTimeSpan, default);
        Assert.False(full.FullMatch(input, 0, token.Token).Success);
    }

    [Theory]
    [InlineData("a+b", "aaaa", false)]
    [InlineData("a+b", "xxaaaabzz", true)]
    [InlineData("a++b", "xxaaaabzz", true)]
    [InlineData("(?>a+)b", "xxaaaabzz", true)]
    [InlineData("a++a", "aaaa", false)]
    [InlineData("(?>ab+)c", "xxabbbdzz", false)]
    [InlineData("(?>ab+)c", "xxabbbczz", true)]
    [InlineData("(a+)?", "xxxx", true)]
    [InlineData("a*b?", "xxxx", true)]
    [InlineData("a+(?!b)", "aaaa", true)]
    [InlineData("(?!b)a+", "aaaa", true)]
    [InlineData("a+(?<=b)", "aaaa", false)]
    [InlineData("(?i)(a+)b", "AAAAB", true)]
    [InlineData("(a+)(?i:b)", "aaaB", true)]
    [InlineData("(a+)(?i:K)", "aaaK", true)]
    [InlineData("(?i:a+)b", "AAAAB", false)]
    public void PresenceAndUnprovedShapesStillUseProfileSemantics(string pattern, string text, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expected, regex.Search(Encoding.UTF8.GetBytes(text)).Success);
    }

    [Fact]
    public void ARequiredLiteralDoesNotProveAMatchOrBypassAdmission()
    {
        var ordinary = new Utf8PythonRegex("a+b");
        Assert.False(ordinary.Search("baaaa"u8).Success);
        var limited = new Utf8PythonRegex("a+b", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkingBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Search("ab"u8)).ResourceKind);
    }

    [Fact]
    public void WholeSubjectAndPrefixContextSurviveOffsetsAndMissingTails()
    {
        var lookbehind = new Utf8PythonRegex("(?<=foo)b+!");
        var hit = lookbehind.Search("foobb!"u8, 3);
        Assert.True(hit.Success); Assert.Equal(3, hit.StartOffsetInBytes); Assert.Equal(6, hit.EndOffsetInBytes);
        Assert.False(lookbehind.Search("foobb!"u8, 6).Success);
        var regex = new Utf8PythonRegex("a+b");
        Assert.True(regex.Search("é😀aab"u8, 6).Success);
        Assert.False(regex.Search("aab😀"u8, 3).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Search("é😀aab"u8, 3));
        byte[] malformed = [.. "aaa"u8, 0xf0, 0x9f];
        Assert.Throws<ArgumentException>(() => regex.Search(malformed));
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Search(malformed, 0, token.Token)).CancellationToken);
    }

    [Fact]
    public void FundedMissNeedsNoNewLoanAndCapturedSnapshotsRemainValid()
    {
        var allowance = new Allowance(16 << 20);
        using var regex = Utf8PythonRegex.Compile("(?P<x>é+)😀", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, allowance);
        var held = allowance.Used; allowance.Maximum = held;
        Assert.False(regex.Search("aaaa"u8).Success); Assert.Equal(held, allowance.Used);
        allowance.Maximum += 1 << 20;
        var snapshot = regex.SearchDetailedData("xxéé😀"u8);
        Assert.True(snapshot.Success); Assert.Equal("éé", snapshot.Groups[1].ValueText);
        Assert.Equal(held, allowance.Used);
        Assert.False(regex.Search("zzzz"u8).Success); Assert.Equal("éé", snapshot.Groups[1].ValueText);
        regex.Dispose(); Assert.Equal(0UL, allowance.Used); Assert.Equal("éé", snapshot.Groups[1].ValueText);
        Assert.Throws<ObjectDisposedException>(() => regex.Search("aaaa"u8));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void RequiredSearchKeepsOriginalCancellationAndDeadline(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var input = Enumerable.Repeat((byte)'a', 262144).ToArray();
        var measured = new Utf8SliceInvocation(new(ulong.MaxValue, 1, 1, 1), Timeout.InfiniteTimeSpan, default);
        _ = Utf8Validation.Validate(input, ref measured); var validatedSteps = measured.Budget.Steps;
        var regex = new Utf8PythonRegex("a+b", PythonReCompileOptions.None,
            timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan, new() { MaxWorkingBytes = 1 });
        var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= validatedSteps) return;
            reached = true; if (timeout) ticks = 100; else token.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => regex.Search(input, 0, token.Token)); Assert.True(reached);
            if (timeout) { var error = Assert.IsType<RegexMatchTimeoutException>(failure); Assert.Empty(error.Input); Assert.Equal(regex.Pattern, error.Pattern); }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void SearchCannotOutrunFiniteWorkAfterValidation()
    {
        var input = Enumerable.Repeat((byte)'a', 32768).ToArray();
        var measured = new Utf8SliceInvocation(new(ulong.MaxValue, 1, 1, 1), Timeout.InfiniteTimeSpan, default);
        _ = Utf8Validation.Validate(input, ref measured);
        var limited = new Utf8PythonRegex("a+b", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = measured.Budget.Steps + 1, MaxWorkingBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Search(input)).ResourceKind);
    }
}
