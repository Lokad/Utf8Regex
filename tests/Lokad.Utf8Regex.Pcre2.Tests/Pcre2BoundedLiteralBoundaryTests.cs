using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BoundedLiteralBoundaryTests
{
    private const string Date = @"\b\d{1,2}/\d{1,2}/\d{2,4}\b";

    [Theory]
    [InlineData(Date, "😀1/2/2024é 11/18/201A 12/3/2025😀")]
    [InlineData(@"\b\d{1,2}/", "x12/ é12/ 😀12/")]
    [InlineData(@"\B\d{1,2}/", "a1/ 2/ _12/ 😀34/")]
    [InlineData(@"\b([0-9]{1,2})/(?<tail>[0-9]{1,2})", "é1/2😀 123/45 6/78")]
    [InlineData(@"\b[\x00-\x7f]{1,3}foo", "😀xfoo\0afooé abfoo")]
    [InlineData(@"\b[\x00-\x7f]{1,3}aaa", "😀aaaaaaX aaaaa! éaaa!")]
    [InlineData(@"\b[0-9]{1,2}\B/", "1/ a12/ 😀3/")]
    [InlineData(@"\b[0-9]{1,64}/", "é" + "12345678901234567890/ 1/")]
    public void WindowPreservesPublicResultsAtEveryScalarOrigin(string pattern, string text)
    {
        using var regex = new Utf8Pcre2Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 100_000, MaxDepth = 256, MaxWorkingBytes = 1 << 20 }
        });
        using var source = new CancellationTokenSource();
        Assert.Equal(Pcre2CandidateSearchKind.BoundedLiteralWindow, regex.DebugCompiledProgram.CandidateSearch.Kind);
        var bytes = Encoding.UTF8.GetBytes(text);
        // Each pattern consumes input. NotEmpty disables candidate search without
        // changing its results, providing the production VM as the control.
        for (var start = 0; start <= bytes.Length; start++)
        {
            if (start < bytes.Length && (bytes[start] & 0xc0) == 0x80) continue;
            Assert.Equal(regex.IsMatch(bytes, start, Pcre2MatchOptions.NotEmpty),
                regex.IsMatch(bytes, start, cancellationToken: source.Token));
            Assert.Equal(regex.Count(bytes, start, Pcre2MatchOptions.NotEmpty),
                regex.Count(bytes, start, cancellationToken: source.Token));
            var expectedCaptures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
            var expected = regex.Match(bytes.AsSpan(), startOffsetInBytes: start, captures: expectedCaptures,
                matchOptions: Pcre2MatchOptions.NotEmpty);
            var actualCaptures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
            var actual = regex.Match(bytes.AsSpan(), startOffsetInBytes: start, captures: actualCaptures,
                cancellationToken: source.Token);
            Assert.Equal(expected.Success, actual.Success);
            if (expected.Success) Assert.Equal(expectedCaptures, actualCaptures);
        }
    }

    [Theory]
    [InlineData(@"\b[0-9]{1,65}/", Pcre2CompileOptions.None)]
    [InlineData(@"\b[0-9]+/", Pcre2CompileOptions.None)]
    [InlineData(@"\b[é]{1,2}/", Pcre2CompileOptions.None)]
    [InlineData(Date, Pcre2CompileOptions.Ucp)]
    [InlineData(Date, Pcre2CompileOptions.Caseless)]
    public void ExistingWindowEligibilityLimitsRemain(string pattern, Pcre2CompileOptions options)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: options);
        Assert.Equal(Pcre2CandidateSearchKind.None, regex.DebugCompiledProgram.CandidateSearch.Kind);
    }

    [Fact]
    public void ControlledWindowRejectsAbsentDelimiterWithinWorkLimit()
    {
        using var regex = new Utf8Pcre2Regex(Date, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 16, MaxWorkingBytes = 1 }
        });
        var input = Encoding.UTF8.GetBytes(new string('1', 8192));
        using var source = new CancellationTokenSource();
        Assert.False(regex.IsMatch(input, cancellationToken: source.Token));
        Assert.False(regex.Match(input.AsSpan(), cancellationToken: source.Token).Success);
        Assert.Equal(0, regex.Count(input, cancellationToken: source.Token));

        using var tight = new Utf8Pcre2Regex(Date, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 2, MaxWorkingBytes = 1 }
        });
        var failure = Assert.Throws<Pcre2MatchException>(() => tight.IsMatch(input, cancellationToken: source.Token));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, failure.ErrorKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlledWindowChecksInterruptionBetweenSearchBlocks(bool cancel)
    {
        using var regex = new Utf8Pcre2Regex(Date, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var input = Encoding.UTF8.GetBytes(new string('1', 65_536));
        using var source = new CancellationTokenSource();
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads < 8) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 20;
        });
        var failure = Record.Exception(() => regex.IsMatch(input, cancellationToken: source.Token));
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }
}
