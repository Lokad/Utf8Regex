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
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        using var token = new CancellationTokenSource();
        var input = "aaaa"u8.ToArray();
        Assert.False(regex.Match(input, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search).Success);
        Assert.False(regex.Match(input, 0, cancellationToken: token.Token).Success);
        Assert.False(regex.IsMatch(input, 0, token.Token));
        Assert.Equal(0, regex.Count(input, 0, token.Token));
        // Eager full preparation avoids testing its separate cold construction cap.
        using var full = Utf8PythonRegex.Compile(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = regex.ResourcePolicy,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default);
        Assert.False(full.Match(input, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.FullMatch).Success);
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
        Assert.Equal(expected, regex.Match(Encoding.UTF8.GetBytes(text), mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void ARequiredLiteralDoesNotProveAMatchOrBypassAdmission()
    {
        var ordinary = new Utf8PythonRegex("a+b");
        Assert.False(ordinary.Match("baaaa"u8, mode: Utf8PythonMatchMode.Search).Success);
        var limited = new Utf8PythonRegex("a+b", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.True(limited.Match("ab"u8, mode: Utf8PythonMatchMode.Search).Success);
        var overlapping = new Utf8PythonRegex("a+ab", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => overlapping.Match("aab"u8, mode: Utf8PythonMatchMode.Search)).ResourceKind);
    }

    [Fact]
    public void WholeSubjectAndPrefixContextSurviveOffsetsAndMissingTails()
    {
        var lookbehind = new Utf8PythonRegex("(?<=foo)b+!");
        var hit = lookbehind.Match("foobb!"u8, 3, mode: Utf8PythonMatchMode.Search);
        Assert.True(hit.Success); Assert.Equal(3, hit.StartOffsetInBytes); Assert.Equal(6, hit.EndOffsetInBytes);
        Assert.False(lookbehind.Match("foobb!"u8, 6, mode: Utf8PythonMatchMode.Search).Success);
        var regex = new Utf8PythonRegex("a+b");
        Assert.True(regex.Match("é😀aab"u8, 6, mode: Utf8PythonMatchMode.Search).Success);
        Assert.False(regex.Match("aab😀"u8, 3, mode: Utf8PythonMatchMode.Search).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("é😀aab"u8, 3, mode: Utf8PythonMatchMode.Search));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("aaa"u8, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search)).CancellationToken);
    }

    [Fact]
    public void FundedMissNeedsNoNewLoanAndCapturedSnapshotsRemainValid()
    {
        var allowance = new Allowance(16 << 20);
        using var regex = Utf8PythonRegex.Compile("(?P<x>é+)😀", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = allowance
        }, cancellationToken: default);
        var held = allowance.Used; allowance.Maximum = held;
        Assert.False(regex.Match("aaaa"u8, mode: Utf8PythonMatchMode.Search).Success); Assert.Equal(held, allowance.Used);
        allowance.Maximum += 1 << 20;
        Utf8PythonRangeMatch snapshot = default;
        using (var captureCursor1 = regex.CreateMatchCursor(("xxéé😀"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out snapshot);
        }
        Assert.True(snapshot.Success); Assert.Equal("éé", System.Text.Encoding.UTF8.GetString((snapshot.Groups[1].Success ? snapshot.GetGroupMemory(snapshot.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(held, allowance.Used);
        Assert.False(regex.Match("zzzz"u8, mode: Utf8PythonMatchMode.Search).Success); Assert.Equal("éé", System.Text.Encoding.UTF8.GetString((snapshot.Groups[1].Success ? snapshot.GetGroupMemory(snapshot.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        regex.Dispose(); Assert.Equal(0UL, allowance.Used); Assert.Equal("éé", System.Text.Encoding.UTF8.GetString((snapshot.Groups[1].Success ? snapshot.GetGroupMemory(snapshot.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Throws<ObjectDisposedException>(() => regex.Match("aaaa"u8, mode: Utf8PythonMatchMode.Search));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequiredSearchKeepsOriginalCancellationAndDeadline(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var input = Enumerable.Repeat((byte)'a', 262144).ToArray();
        var regex = new Utf8PythonRegex("a+b", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            }
        });
        var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 64) return;
            reached = true; if (timeout) ticks = 100; else token.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => regex.Match(input, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search)); Assert.True(reached);
            if (timeout) { var error = Assert.IsType<RegexMatchTimeoutException>(failure); Assert.Empty(error.Input); Assert.Equal(regex.Pattern, error.Pattern); }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void SearchCannotOutrunFiniteWork()
    {
        var input = Enumerable.Repeat((byte)'a', 32768).ToArray();
        var measured = new Utf8SliceInvocation(new(ulong.MaxValue, 1, 1, 1), Timeout.InfiniteTimeSpan, default);
        _ = Utf8InputAnalyzer.Describe(input, ref measured);
        var limited = new Utf8PythonRegex("a+b", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = measured.Budget.Steps + 1,
                MaxWorkingBytes = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Match(input, mode: Utf8PythonMatchMode.Search)).ResourceKind);
    }
}
