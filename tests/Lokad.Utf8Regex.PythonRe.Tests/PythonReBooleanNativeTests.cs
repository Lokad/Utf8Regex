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
    [InlineData(3)] [InlineData(2047)] [InlineData(2048)] [InlineData(4095)]
    public void EarlyMatchStillRejectsMalformedWholeSubjectAtOriginalOffset(int offset)
    {
        var input = Encoding.UTF8.GetBytes("cat" + new string('x', offset - 3))
            .Concat(new byte[] { 0xf0, 0x80, 0x80, 0x80 }).ToArray();
        var regex = new Utf8PythonRegex("cat|dog", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(input));
        Assert.Equal(expected.Message, Assert.Throws<ArgumentException>(() => regex.IsMatch(input)).Message);
    }

    [Fact]
    public void InvalidStartCancellationAndWorkDenialKeepFailureContracts()
    {
        var regex = new Utf8PythonRegex("cat|dog", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch("😀cat"u8, 1));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch("cat"u8)).ResourceKind);
        using var token = new CancellationTokenSource(); token.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.IsMatch("😀cat"u8, 1, token.Token));
        Assert.Equal(token.Token, error.CancellationToken);
    }

    [Fact]
    public void UnmeteredTokenOnlyAndTimeoutOnlyCallsKeepBooleanOutcomes()
    {
        using var token = new CancellationTokenSource();
        foreach (var timeout in new[] { Regex.InfiniteMatchTimeout, TimeSpan.FromSeconds(30) })
        {
            var regex = new Utf8PythonRegex("cat|dog", PythonReCompileOptions.None, timeout);
            Assert.True(regex.IsMatch("é😀dog"u8, 2));
            Assert.True(regex.IsMatch("é😀dog"u8, 2, token.Token));
            Assert.False(regex.IsMatch("é😀dog"u8, 9, token.Token));
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

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void OriginalInterruptionAppliesDuringValidationAndSearch(bool timeout, bool duringSearch)
    {
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8PythonRegex("cat|dog", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20),
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
