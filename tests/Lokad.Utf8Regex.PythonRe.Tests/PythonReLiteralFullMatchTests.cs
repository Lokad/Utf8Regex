using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLiteralFullMatchTests
{
    [Theory]
    [InlineData("cat", "cat", 0, true, 0)]
    [InlineData("cat", "xcat", 0, false, 0)]
    [InlineData("cat", "catx", 0, false, 0)]
    [InlineData("cat", "xcat", 1, true, 1)]
    [InlineData("café😀", "πcafé😀", 2, true, 1)]
    [InlineData("😀", "é😀", 2, true, 1)]
    [InlineData("😀", "é😀x", 2, false, 0)]
    [InlineData("😀", "éx😀", 2, false, 0)]
    [InlineData("", "", 0, true, 0)]
    [InlineData("", "😀", 0, false, 0)]
    [InlineData("", "😀", 4, true, 2)]
    public void ValueAndStringFullMatchKeepSuffixEndpointsAndScalarCoordinates(
        string pattern, string text, int start, bool success, int startUtf16)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var controlled in new[] { false, true })
        {
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
                controlled ? new() { MaxWorkSteps = 100000, MaxDepth = 128,
                    MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 } : default);
            using var live = new CancellationTokenSource();
            var match = regex.FullMatch(input, start, live.Token);
            Assert.Equal(success, match.Success);
            Assert.Equal(success ? pattern : null, regex.FullMatchToString(input, start, live.Token));
            if (success)
            {
                Assert.Equal((start, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
                Assert.Equal((startUtf16, text.Length), (match.StartOffsetInUtf16, match.EndOffsetInUtf16));
                Assert.Equal(pattern, match.GetValueString());
            }
            Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        }
    }

    [Theory]
    [InlineData("cat")] [InlineData("é😀")] [InlineData("")]
    public void BorrowedLiteralResultNeedsNoVmPreparationOrMajorWorkingResultCapacity(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        using var deniedPreparation = new Utf8ConstructionScope(0, 0, 0, 0);
        var input = Encoding.UTF8.GetBytes(pattern);
        using var live = new CancellationTokenSource();
        Assert.True(regex.FullMatch(input, 0, live.Token).Success);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        Assert.Equal(0ul, deniedPreparation.Budget.WorkSteps);
        Assert.Equal(0ul, deniedPreparation.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(1)] [InlineData(16)] [InlineData(17)] [InlineData(4096)]
    public void LiteralLengthBoundariesKeepAnchoredValuesAndText(int length)
    {
        var pattern = new string('x', length);
        var regex = new Utf8PythonRegex(pattern);
        foreach (var prefix in new[] { "", "π😀" })
        {
            var start = Encoding.UTF8.GetByteCount(prefix);
            var input = Encoding.UTF8.GetBytes(prefix + pattern);
            var full = regex.FullMatch(input, start);
            Assert.True(full.Success);
            Assert.Equal((start, input.Length), (full.StartOffsetInBytes, full.EndOffsetInBytes));
            Assert.Equal((prefix.Length, prefix.Length + length), (full.StartOffsetInUtf16, full.EndOffsetInUtf16));
            Assert.Equal(pattern, regex.FullMatchToString(input, start));
            var tail = Encoding.UTF8.GetBytes(prefix + pattern + "!");
            var match = regex.Match(tail, start);
            Assert.True(match.Success);
            Assert.Equal(input.Length, match.EndOffsetInBytes);
            Assert.Equal(pattern, regex.MatchToString(tail, start));
            Assert.False(regex.FullMatch(tail, start).Success);
            Assert.Null(regex.FullMatchToString(tail, start));
        }
    }

    [Theory]
    [InlineData("foo(?=bar)", "foo", false, false)]
    [InlineData("foo(?=bar)", "foobar", true, false)]
    [InlineData(@"foo\b", "foobar", false, false)]
    [InlineData(@"foo\b", "foo!", true, false)]
    [InlineData("(?i:k)", "K", true, true)]
    public void AnchoredLiteralResultsKeepAssertionsAndProfileFolding(string pattern,
        string subject, bool matched, bool fullMatched)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(matched, regex.Match(input).Success);
        Assert.Equal(fullMatched, regex.FullMatch(input).Success);
        Assert.Equal(matched, regex.MatchToString(input) is not null);
        Assert.Equal(fullMatched, regex.FullMatchToString(input) is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WorkDenialAndPreCancellationStayDistinctFromNoMatch(bool text)
    {
        var regex = new Utf8PythonRegex("cat", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1 });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(regex, "dog"u8.ToArray(), 0, text, default));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(1ul, error.Limit);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var canceled = Assert.Throws<OperationCanceledException>(() => Run(regex, [0xff], 0, text, cancellation.Token));
        Assert.Equal(cancellation.Token, canceled.CancellationToken);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
    }

    [Fact]
    public void RequiredTextStillNeedsResultAdmission()
    {
        var regex = new Utf8PythonRegex("é😀", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxResultBytes = 1 });
        Assert.True(regex.FullMatch("é😀"u8).Success);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FullMatchToString("é😀"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
        Assert.Equal(1ul, error.Limit);
        Assert.True(regex.FullMatch("é😀"u8).Success);
    }

    [Theory]
    [InlineData("^cat", "xcat", 1, false)]
    [InlineData(@"\Acat", "xcat", 1, false)]
    [InlineData(@"\bcat", "xcat", 1, false)]
    [InlineData("(?<=x)cat", "xcat", 1, true)]
    [InlineData("cat$", "cat\n", 0, false)]
    [InlineData("cat|cater", "cater", 0, true)]
    [InlineData("(?P<x>cat)", "cat", 0, true)]
    [InlineData("(?i:k)", "K", 0, true)]
    public void ExcludedShapesKeepTheirFullSubjectContextAndGeneralProgram(
        string pattern, string text, int start, bool success)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(success, regex.FullMatch(input, start).Success);
        Assert.Equal(success ? Encoding.UTF8.GetString(input.AsSpan(start)) : null, regex.FullMatchToString(input, start));
        Assert.True(regex.DebugIsScalarFullProgramValueCreated);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MalformedPrefixAndTailAreValidatedEvenWhenEndpointsCannotMatch(bool text)
    {
        var regex = new Utf8PythonRegex("cat");
        foreach (var input in new[] { new byte[] { 0xff, 99, 97, 116 }, new byte[] { 99, 97, 116, 0xff } })
            Assert.Throws<ArgumentException>(() => Run(regex, input, 1, text, default));
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FullValidationObservesInFlightCancellationAndFreshInvocationRecovers(bool text)
    {
        var regex = new Utf8PythonRegex("cat", PythonReCompileOptions.None, TimeSpan.FromSeconds(30));
        var input = Encoding.UTF8.GetBytes(new string('x', 65536) + "cat");
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == 6) cancellation.Cancel();
            return 0;
        }))
        {
            var error = Assert.Throws<OperationCanceledException>(() => Run(regex, input, 65536, text, cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.True(reads >= 6);
        }
        Run(regex, input, 65536, text, default);
        Assert.True(regex.FullMatch(input, 65536).Success);
        Assert.Equal("cat", regex.FullMatchToString(input, 65536));
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DeadlinePrecedesMalformedTailAndByteBoundedTimeoutOmitsSubject(bool text)
    {
        var regex = new Utf8PythonRegex("cat", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1),
            new() { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Enumerable.Repeat((byte)'x', 65536).Append((byte)0xff).ToArray();
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads < 6 ? 0 : 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, input, 0, text, default));
        Assert.Empty(error.Input);
        Assert.Equal(regex.Pattern, error.Pattern);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void FinalLiteralPublicationKeepsOriginalTokenAndDeadline(bool text, bool expire)
    {
        var regex = new Utf8PythonRegex("cat", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20));
        var input = "cat"u8.ToArray();
        var total = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => { total++; return 0; }))
            Run(regex, input, 0, text, default);
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == total) { if (expire) ticks = 50; else cancellation.Cancel(); }
            return ticks;
        });
        if (expire)
            Assert.Equal(regex.Pattern, Assert.Throws<RegexMatchTimeoutException>(() =>
                Run(regex, input, 0, text, cancellation.Token)).Pattern);
        else
            Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                Run(regex, input, 0, text, cancellation.Token)).CancellationToken);
    }

    private static void Run(Utf8PythonRegex regex, byte[] input, int start, bool text, CancellationToken token)
    {
        if (text) _ = regex.FullMatchToString(input, start, token);
        else _ = regex.FullMatch(input, start, token);
    }
}
