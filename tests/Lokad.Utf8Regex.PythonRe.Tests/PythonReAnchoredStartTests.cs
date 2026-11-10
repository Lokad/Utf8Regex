using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAnchoredStartTests
{
    [Theory]
    [InlineData("(needle)([0-9]+)", "xneedle12")]
    [InlineData("(?=bad)(bad)([0-9]+)", "item-1234 bad42")]
    [InlineData("(?:[a-z]+|[0-9]+)end", "😀123end")]
    [InlineData("(a)?(b)", "cb")]
    [InlineData("(?=(a?))(b)", "cb")]
    [InlineData("(?<=é)(a+)", "éaa")]
    public void AnchoredRejectionDoesNotSkipToALaterValidStart(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var policy in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
        {
            using var regex = new Utf8PythonRegex(pattern, inputValidation: policy);
            Assert.False(regex.Match(input).Success);
            Assert.False(regex.FullMatch(input).Success);
            Assert.Null(regex.MatchToString(input));
            Assert.False(regex.MatchDetailedData(input).Success);
            Assert.True(regex.Search(input).Success);
        }
    }

    [Theory]
    [InlineData("(a)?(b)", "b", 0, "b")]
    [InlineData("(?=bad)(bad)([0-9]+)", "bad123", 0, "bad123")]
    [InlineData("(?i:(k))([0-9]+)", "K12", 0, "K12")]
    [InlineData("(a*)", "", 0, "")]
    [InlineData("a*(b)", "b", 0, "b")]
    [InlineData("(?=(a?))(b)", "b", 0, "b")]
    [InlineData("(?<=é)(a+)", "éaa", 2, "aa")]
    [InlineData("([a-z]+)-([0-9]+)", "é😀 item-123", 7, "item-123")]
    public void NullableFoldedAndOffsetStartsKeepValuesAndCoordinates(string pattern, string text, int start, string expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var policy in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
        {
            using var regex = new Utf8PythonRegex(pattern, inputValidation: policy);
            var match = regex.Match(input, start);
            Assert.True(match.Success);
            Assert.Equal(start, match.StartOffsetInBytes);
            Assert.Equal(start + Encoding.UTF8.GetByteCount(expected), match.EndOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetCharCount(input.AsSpan(0, start)), match.StartOffsetInUtf16);
            Assert.Equal(match.StartOffsetInUtf16 + expected.Length, match.EndOffsetInUtf16);
            Assert.Equal(expected, regex.MatchDetailedData(input, start).Value.ValueText);
            Assert.Equal(expected, regex.FullMatchToString(input, start));
        }
    }

    [Theory]
    [InlineData("(needle)")]
    [InlineData("(?=bad)(bad)([0-9]+)")]
    [InlineData("([0-9]+)end")]
    public void StrictValidationPrecedesAllAnchoredRejectionShapes(string pattern)
    {
        byte[] input = [.. "xlater needle123end"u8, 0xff];
        using var regex = new Utf8PythonRegex(pattern);
        Assert.Throws<ArgumentException>(() => regex.Match(input));
        Assert.Throws<ArgumentException>(() => regex.FullMatch(input));
        Assert.Throws<ArgumentException>(() => regex.MatchToString(input));
        Assert.Throws<ArgumentException>(() => regex.FullMatchToString(input));
        Assert.Throws<ArgumentException>(() => regex.MatchDetailedData(input));
        Assert.Throws<ArgumentException>(() => regex.FullMatchDetailedData(input));
    }

    [Fact]
    public void ProvenMismatchNeedsNoSubjectTraversalOrWorkingScratchUnderTrustedInput()
    {
        var input = Encoding.ASCII.GetBytes(new string('x', 1 << 20) + "needle");
        using var regex = new Utf8PythonRegex("(needle)", resourcePolicy: new()
        {
            MaxWorkSteps = 64, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1,
        }, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.False(regex.Match(input).Success);
        Assert.Null(regex.MatchToString(input));
        using var strict = new Utf8PythonRegex("(needle)", resourcePolicy: new() { MaxWorkSteps = 64 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => strict.Match(input)).ResourceKind);
    }

    [Fact]
    public void KnownLookaheadMismatchRejectsBeforeItsWholeSubjectScreen()
    {
        var input = Encoding.ASCII.GetBytes("item-1234 " + new string('a', 1 << 20));
        using var regex = new Utf8PythonRegex("(?=bad)(bad)([0-9]+)", resourcePolicy: new()
        {
            MaxWorkSteps = 64, MaxDepth = 64, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
        }, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.False(regex.Match(input).Success);
        Assert.False(regex.FullMatch(input).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PossibleAmbiguousStartsKeepNecessaryLiteralScreening(bool full)
    {
        var input = Encoding.ASCII.GetBytes(new string('a', 8192));
        using var regex = new Utf8PythonRegex("(a+)+missing", resourcePolicy: new()
        {
            MaxWorkSteps = 1000, MaxDepth = 64, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
        }, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.False(full ? regex.FullMatch(input).Success : regex.Match(input).Success);
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void RejectionStillObservesOriginalCancellationAndDeadline(Utf8InputValidation policy)
    {
        using var regex = new Utf8PythonRegex("(needle)", inputValidation: policy);
        using var token = new CancellationTokenSource();
        token.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => regex.Match("x"u8, 0, token.Token));
        Assert.Equal(token.Token, failure.CancellationToken);
        Assert.False(regex.Match("x"u8).Success);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks += 100);
        using var deadline = new Utf8PythonRegex("(needle)", matchTimeout: TimeSpan.FromMilliseconds(1), inputValidation: policy);
        Assert.Throws<RegexMatchTimeoutException>(() => deadline.Match("x"u8));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3)]
    public void AnchoredRejectionDoesNotHideInvalidOffsets(int start)
    {
        using var regex = new Utf8PythonRegex("(needle)", inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("é"u8, start));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.FullMatch("é"u8, start));
    }
}
