using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReBooleanNativeTests
{
    [Theory]
    [InlineData("cat|dog", "é😀dog", 0, true)]
    [InlineData("cat|dog", "é😀dog", 9, false)]
    [InlineData("😀|中", "é😀中", 2, true)]
    [InlineData("😀|中", "é😀中", 6, true)]
    [InlineData("needle", "needleé😀", 6, false)]
    [InlineData("needle", "é😀needle", 6, true)]
    [InlineData("ab|a", "éab", 2, true)]
    [InlineData("(a)(b)", "é😀ab", 6, true)]
    [InlineData("(a)(b)", "é😀ab", 8, false)]
    [InlineData("(?P<x>a)(b)", "é😀ab", 2, true)]
    [InlineData("(ab)|(a)", "éab", 2, true)]
    [InlineData("(ab)|(cd)", "é😀xy", 2, false)]
    [InlineData("(?P<x>😀)|中", "é😀中", 2, true)]
    [InlineData("(?P<x>😀)|中", "é😀中", 6, true)]
    [InlineData("(?P<x>😀)|中", "é😀中", 9, false)]
    public void BooleanNativeSearchKeepsOffsetsAndNeedsNoVmOrResultStorage(
        string pattern, string text, int start, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input, start));
        Assert.Equal(expected, regex.IsMatch(input, start, token.Token));
    }



    [Theory]
    [InlineData("cat|dog")]
    [InlineData("(cat)|(dog)")]
    public void InvalidStartCancellationAndWorkDenialKeepFailureContracts(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch("😀cat"u8, 1));
        using var token = new CancellationTokenSource(); token.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.IsMatch("😀cat"u8, 1, token.Token));
        Assert.Equal(token.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData("cat|dog")]
    [InlineData("(cat)|(dog)")]
    public void UnmeteredTokenOnlyAndTimeoutOnlyCallsKeepBooleanOutcomes(string pattern)
    {
        using var token = new CancellationTokenSource();
        foreach (var timeout in new[] { Regex.InfiniteMatchTimeout, TimeSpan.FromSeconds(30) })
        {
            var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = timeout
            });
            Assert.True(regex.IsMatch("é😀dog"u8, 2));
            Assert.True(regex.IsMatch("é😀dog"u8, 2, token.Token));
            Assert.False(regex.IsMatch("é😀dog"u8, 9, token.Token));
            Assert.False(regex.IsMatch("é😀dog"u8, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch("é😀dog"u8, 3));
        }
    }

    [Theory]
    [InlineData("cat(?=!)|dog", "cat?", false)]
    [InlineData(@"cat\b|dog", "catch", false)]
    [InlineData("cat$|dog", "catx", false)]
    [InlineData("(?i:k)|dog", "K", true)]
    [InlineData("(?:cat)?|dog", "x", true)]
    [InlineData("(cat)|dog", "cat", true)]
    public void OtherProgramsKeepAssertionsFoldingEmptyAndCaptures(string pattern, string text, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 1024,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            }
        });
        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(text)));
    }

    [Theory]
    [InlineData(@"\d{2,}")]
    [InlineData(@"(?P<digits>\d{2,})")]
    public void TerminalRepeatBooleanNeedsOnlyItsMinimum(string pattern)
    {
        var input = Encoding.UTF8.GetBytes("😀" + string.Concat(Enumerable.Repeat("٣𐒡", 1024)));
        using var token = new CancellationTokenSource();
        using var limited = new Utf8PythonRegex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxDepth = 4096 }
        });
        Assert.True(limited.IsMatch(input, "😀"u8.Length, token.Token));
        Assert.False(limited.IsMatch(input, input.Length - "𐒡"u8.Length, token.Token));
        using var unrestricted = new Utf8PythonRegex(pattern);
        var result = unrestricted.Match(input, "😀"u8.Length, mode: Utf8PythonMatchMode.Search);
        Assert.True(result.Success);
        Assert.Equal(("😀"u8.Length, input.Length), (result.StartOffsetInBytes, result.EndOffsetInBytes));
    }

    [Fact]
    public void BooleanCallsPreserveRetainedCapturesAndGenericProgramsNeedVmStorage()
    {
        var input = "é😀ab ab"u8.ToArray();
        var regex = new Utf8PythonRegex("(?P<x>a)(b)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 1024,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            }
        });
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var held = collectedRanges1.ToArray();
        using var token = new CancellationTokenSource();
        Assert.True(regex.IsMatch(input, 0, token.Token));
        Assert.False(regex.IsMatch(input, input.Length, token.Token));
        var collectedRanges2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = regex.CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
                collectedRanges2.Add(rangeMatch2);
        }
        var later = collectedRanges2.ToArray();
        foreach (var matches in new[] { held, later })
        {
            Assert.Equal(2, matches.Length);
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString(matches[0].GetGroupMemory(1).Span));
            Assert.Equal("b", System.Text.Encoding.UTF8.GetString(matches[0].GetGroupMemory(2).Span));
            Assert.Equal(2, matches[0].LastIndex);
            Assert.Equal(6, matches[0].Value.StartOffsetInBytes);
        }
        var generic = new Utf8PythonRegex("(a+)(b+)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => generic.IsMatch(input)).ResourceKind);
    }

    [Theory]
    [InlineData("cat|dog", false)]
    [InlineData("cat|dog", true)]
    [InlineData("(cat)|(dog)", false)]
    [InlineData("(cat)|(dog)", true)]
    public void OriginalInterruptionAppliesDuringSearch(string pattern, bool timeout)
    {
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new()
            {
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        var input = Encoding.UTF8.GetBytes(new string('x', 262144) + "dog");
        var interrupted = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 64) return;
            interrupted = true;
            if (timeout) ticks = 25; else token.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => regex.IsMatch(input, 0, token.Token));
            Assert.True(interrupted);
            if (timeout)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(TimeSpan.FromMilliseconds(20), error.MatchTimeout);
                Assert.Empty(error.Input);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { diagnostics.Complete(); }
    }
}
