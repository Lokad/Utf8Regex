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
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
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
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void CountDoesNotChangeLaterCapturesAndGenericProgramsStillNeedVmStorage()
    {
        var input = "é😀ab ab"u8.ToArray();
        var regex = new Utf8PythonRegex("(?P<x>a)(b)");
        Assert.Equal(2, regex.Count(input));
        var matches = regex.FindIterDetailed(input);
        Assert.Equal(2, matches.Length);
        Assert.Equal("a", matches[0].Groups[1].ValueText);
        Assert.Equal("b", matches[0].Groups[2].ValueText);
        Assert.Equal(2, matches[0].LastIndex); Assert.Null(matches[0].LastGroup);
        Assert.Equal((6, 3), (matches[0].Value.StartOffsetInBytes, matches[0].Value.StartOffsetInUtf16));
        var tuples = regex.FindAllToUtf8(input).TupleValues;
        Assert.Equal("a"u8.ToArray(), tuples[0][0]); Assert.Equal("b"u8.ToArray(), tuples[0][1]);
        var generic = new Utf8PythonRegex("(a+)(b+)", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => generic.Count(input)).ResourceKind);
    }

    [Theory]
    [InlineData(2)] [InlineData(2047)] [InlineData(2048)] [InlineData(4095)]
    public void CountRejectsMalformedWholeSubjectAtOriginalByteOffset(int offset)
    {
        byte[] input = [.. Encoding.UTF8.GetBytes("ab" + new string('x', offset - 2)), 0xF0, 0x80, 0x80, 0x80];
        var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(input));
        var regex = new Utf8PythonRegex("(a)(b)", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(expected.Message, Assert.Throws<ArgumentException>(() => regex.Count(input)).Message);
    }

    [Fact]
    public void StartCancellationAndWorkFailureKeepPrecedence()
    {
        var regex = new Utf8PythonRegex("(a)(b)", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀ab"u8, 1));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count("ab"u8)).ResourceKind);
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count("😀ab"u8, 1, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void OriginalInterruptionAppliesDuringValidationAndCount(bool timeout, bool duringSearch)
    {
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8PythonRegex("(a)(b)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20),
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes(new string('x', 262144) + "ab");
        var interrupted = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= (duringSearch ? 192UL : 64UL)) return;
            interrupted = true;
            if (timeout) ticks = 25; else token.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => regex.Count(input, 0, token.Token));
            Assert.True(interrupted);
            if (timeout)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(TimeSpan.FromMilliseconds(20), error.MatchTimeout);
                Assert.Equal("(a)(b)", error.Pattern); Assert.Empty(error.Input);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { diagnostics.Complete(); }
        Assert.Equal(1, regex.Count(input));
    }
}
