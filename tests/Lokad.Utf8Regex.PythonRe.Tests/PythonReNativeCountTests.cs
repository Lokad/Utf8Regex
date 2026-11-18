using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNativeCountTests
{
    [Theory]
    [InlineData("(a)(b)", "é😀ab ab", 0, 2)]
    [InlineData("(a)(b)", "é😀ab ab", 8, 1)]
    [InlineData("(a)(b)", "é😀ab ab", 11, 0)]
    [InlineData("(?P<x>a)(b)", "é😀ab ab", 6, 2)]
    [InlineData("(ab)|(a)", "aba", 0, 2)]
    [InlineData("(a)|(ab)", "aba", 0, 2)]
    [InlineData("(😀😀)|(😀)|(中)", "é😀😀😀中", 2, 3)]
    [InlineData("(😀😀)|(😀)|(中)", "é😀😀😀中", 6, 2)]
    [InlineData("(?P<x>😀)|中", "é😀中😀中", 2, 4)]
    public void NativeCountNeedsNoCaptureSlotsOrResultStorage(string pattern, string text, int start, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input, start));
        Assert.Equal(expected, regex.Count(input, start, token.Token));
        Assert.Equal(expected, new Utf8PythonRegex(pattern).Count(input, start));
    }

    [Theory]
    [InlineData("(a+)(b+)", "aaabbb ab", 2)]
    [InlineData("(?P<x>😀)(中)", "é😀中😀中", 2)]
    [InlineData("(cat)(?=!)|dog", "cat? dogcat!", 2)]
    [InlineData(@"(cat)\b|dog", "catch dog cat", 2)]
    [InlineData("(cat)$|dog", "catx dog", 1)]
    [InlineData("(?i:(k))|dog", "Kdog", 2)]
    [InlineData("(a)?", "a", 2)]
    [InlineData(@"(a)\1", "aa a", 1)]
    [InlineData("(?P<x>a)?(?(x)b|c)", "ab c", 2)]
    [InlineData("(a)|(ab)|(b)|(ba)", "aba", 3)]
    public void OtherProgramsKeepProfileSemantics(string pattern, string text, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 1024,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            }
        });
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void CountDoesNotChangeLaterCapturesAndGenericProgramsStillNeedVmStorage()
    {
        var input = "é😀ab ab"u8.ToArray();
        var regex = new Utf8PythonRegex("(?P<x>a)(b)");
        Assert.Equal(2, regex.Count(input));
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var matches = collectedRanges1.ToArray();
        Assert.Equal(2, matches.Length);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(matches[0].GetGroupMemory(1).Span));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(matches[0].GetGroupMemory(2).Span));
        Assert.Equal(2, matches[0].LastIndex);
        Assert.Equal(6, matches[0].Value.StartOffsetInBytes);
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        var tuples = snapshots1.Select(captureSnapshot1 => Enumerable.Range(1, captureSnapshot1.Groups.Length - 1).Select(captureNumber1 => captureSnapshot1.GetGroupMemory(captureNumber1).ToArray()).ToArray()).ToArray();
        Assert.Equal("a"u8.ToArray(), tuples[0][0]);
        Assert.Equal("b"u8.ToArray(), tuples[0][1]);
        var generic = new Utf8PythonRegex("(a+)(b+)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => generic.Count(input)).ResourceKind);
    }

    [Fact]
    public void StartCancellationAndWorkFailureKeepPrecedence()
    {
        var regex = new Utf8PythonRegex("(a)(b)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀ab"u8, 1));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count("ab"u8)).ResourceKind);
        using var token = new CancellationTokenSource();
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count("😀ab"u8, 1, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInterruptionAppliesDuringCount(bool timeout)
    {
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8PythonRegex("(a)(b)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        var input = Encoding.UTF8.GetBytes(new string('x', 262144) + "ab");
        var interrupted = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 64)
            {
                return;
            }
            interrupted = true;
            if (timeout)
            {
                ticks = 25;
            }
            else
            {
                token.Cancel();
            }
        });
        try
        {
            var failure = Record.Exception(() => regex.Count(input, 0, token.Token));
            Assert.True(interrupted);
            if (timeout)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(TimeSpan.FromMilliseconds(20), error.MatchTimeout);
                Assert.Equal("(a)(b)", error.Pattern);
                Assert.Empty(error.Input);
            }
            else
            {
                Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            }
        }
        finally
        {
            diagnostics.Complete();
        }
        Assert.Equal(1, regex.Count(input));
    }
}
