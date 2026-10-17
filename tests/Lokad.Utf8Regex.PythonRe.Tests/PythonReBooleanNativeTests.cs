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
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input, start));
        Assert.Equal(expected, regex.IsMatch(input, start, token.Token));
    }

    [Theory]
    [InlineData("cat|dog", 3)] [InlineData("cat|dog", 2047)]
    [InlineData("cat|dog", 2048)] [InlineData("cat|dog", 4095)]
    [InlineData("(cat)|(dog)", 3)] [InlineData("(cat)|(dog)", 2047)]
    [InlineData("(cat)|(dog)", 2048)] [InlineData("(cat)|(dog)", 4095)]
    public void EarlyMatchStillRejectsMalformedWholeSubjectAtOriginalOffset(string pattern, int offset)
    {
        var input = Encoding.UTF8.GetBytes("cat" + new string('x', offset - 3))
            .Concat(new byte[] { 0xf0, 0x80, 0x80, 0x80 }).ToArray();
        var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(input));
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited,
            new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 } })
        {
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
            Assert.Equal(expected.Message, Assert.Throws<ArgumentException>(() => regex.IsMatch(input)).Message);
        }
    }

    [Theory]
    [InlineData("cat|dog")] [InlineData("(cat)|(dog)")]
    public void InvalidStartCancellationAndWorkDenialKeepFailureContracts(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch("😀cat"u8, 1));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch("cat"u8)).ResourceKind);
        using var token = new CancellationTokenSource(); token.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.IsMatch("😀cat"u8, 1, token.Token));
        Assert.Equal(token.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData("cat|dog")] [InlineData("(cat)|(dog)")]
    public void UnmeteredTokenOnlyAndTimeoutOnlyCallsKeepBooleanOutcomes(string pattern)
    {
        using var token = new CancellationTokenSource();
        foreach (var timeout in new[] { Regex.InfiniteMatchTimeout, TimeSpan.FromSeconds(30) })
        {
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, timeout);
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
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void BooleanCallsPreserveRetainedCapturesAndGenericProgramsNeedVmStorage()
    {
        var input = "é😀ab ab"u8.ToArray();
        var regex = new Utf8PythonRegex("(?P<x>a)(b)", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        var held = regex.FindIterDetailed(input);
        using var token = new CancellationTokenSource();
        Assert.True(regex.IsMatch(input, 0, token.Token));
        Assert.False(regex.IsMatch(input, input.Length, token.Token));
        var later = regex.FindIterDetailed(input);
        foreach (var matches in new[] { held, later })
        {
            Assert.Equal(2, matches.Length);
            Assert.Equal("a", matches[0].Groups[1].ValueText);
            Assert.Equal("b", matches[0].Groups[2].ValueText);
            Assert.Equal(2, matches[0].LastIndex);
            Assert.Null(matches[0].LastGroup);
            Assert.Equal((6, 3), (matches[0].Value.StartOffsetInBytes, matches[0].Value.StartOffsetInUtf16));
        }
        var generic = new Utf8PythonRegex("(a+)(b+)", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => generic.IsMatch(input)).ResourceKind);
    }

    [Theory]
    [InlineData("cat|dog", false, false)] [InlineData("cat|dog", true, false)]
    [InlineData("cat|dog", false, true)] [InlineData("cat|dog", true, true)]
    [InlineData("(cat)|(dog)", false, false)] [InlineData("(cat)|(dog)", true, false)]
    [InlineData("(cat)|(dog)", false, true)] [InlineData("(cat)|(dog)", true, true)]
    public void OriginalInterruptionAppliesDuringValidationAndSearch(string pattern, bool timeout, bool duringSearch)
    {
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20),
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes(new string('x', 262144) + "dog");
        var interrupted = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            // Validation takes roughly130 block charges; later sampled work is search.
            if (steps <= (duringSearch ? 192UL : 64UL)) return;
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
