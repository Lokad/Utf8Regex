using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFullRangeConstructionTests
{
    private static readonly Utf8ResourcePolicy Limits = new()
    {
        MaxWorkSteps = 100000, MaxDepth = 64,
        MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
    };

    private static Utf8PythonRegex Compile(string pattern, PythonReCompileOptions options = PythonReCompileOptions.None)
        => Utf8PythonRegex.Compile(pattern, options, Timeout.InfiniteTimeSpan, Limits,
            new() { MaxWorkSteps = 20000 }, Timeout.InfiniteTimeSpan, CancellationToken.None);

    [Theory]
    [InlineData(@"[\x00-\U0010ffff]", PythonReCompileOptions.None, true)]
    [InlineData(@"[\x00-\U0010ffff]", PythonReCompileOptions.IgnoreCase, true)]
    [InlineData(@"(?i)[\x00-\U0010ffff]", PythonReCompileOptions.None, true)]
    [InlineData(@"(?ai:[\x00-\U0010ffff])", PythonReCompileOptions.None, true)]
    [InlineData(@"(?i)[A-Z\x00-\U0010ffff\w]", PythonReCompileOptions.None, true)]
    [InlineData(@"[^\x00-\U0010ffff]", PythonReCompileOptions.None, false)]
    [InlineData(@"(?i)[^\x00-\U0010ffff]", PythonReCompileOptions.None, false)]
    [InlineData(@"(?ai:[^\x00-\U0010ffff])", PythonReCompileOptions.None, false)]
    public void CompleteClassesCompileWithinSmallWorkCapAndPreserveScalars(string pattern, PythonReCompileOptions options, bool accepts)
    {
        var regex = Compile(pattern, options);
        var text = "a\0é中😀\U0010ffff\n";
        var scalars = text.EnumerateRunes().Select(rune => rune.ToString()).ToArray();
        var input = Encoding.UTF8.GetBytes(text);
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(accepts ? scalars.Length : 0, regex.Count(input, 0, cancellation.Token));
        Assert.Equal(accepts ? scalars : [], regex.FindAllToUtf8(input, 0, cancellation.Token)
            .ScalarValues.Select(Encoding.UTF8.GetString));
        foreach (var scalar in scalars)
            Assert.Equal(accepts, regex.FullMatch(Encoding.UTF8.GetBytes(scalar)).Success);
        Assert.False(regex.FullMatch([]).Success);
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { 0xff }));

        // This rendering remains live for comparators and timeout pattern text.
        // Its surrogate-pair atom must still consume a supplementary scalar once.
        var managed = new Regex(PythonReDotNetReference.Render(regex), RegexOptions.CultureInvariant);
        Assert.Equal(accepts ? scalars : [], managed.Matches(text).Select(match => match.Value));
    }

    [Fact]
    public void BytePatternCapturesKeepCompleteScalarCoordinatesAndCompletion()
    {
        const string pattern = @"(?i)([\x00-\U0010ffff])";
        var regex = Utf8PythonRegex.Compile(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, Limits, new() { MaxWorkSteps = 20000 },
            Timeout.InfiniteTimeSpan, CancellationToken.None);
        var matches = regex.FindIterDetailed("é😀\n"u8);
        Assert.Equal(new[] { "é", "😀", "\n" }, matches.Select(match => match.Groups[1].ValueText));
        Assert.Equal(new[] { 0, 2, 6 }, matches.Select(match => match.Value.StartOffsetInBytes));
        Assert.Equal(new[] { 2, 6, 7 }, matches.Select(match => match.Value.EndOffsetInBytes));
        Assert.Equal(new[] { 0, 1, 3 }, matches.Select(match => match.Value.StartOffsetInUtf16));
        Assert.All(matches, match => Assert.Equal(1, match.LastIndex));
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void EmptyComplementRepeatKeepsCapturesAndScalarEmptyProgression()
    {
        var regex = Compile(@"(?i)([^\x00-\U0010ffff]*)");
        Assert.Equal(new[] { "", "", "" }, regex.FindAllToUtf8("é😀"u8).ScalarValues.Select(Encoding.UTF8.GetString));
        var empty = regex.FullMatchDetailedData([]);
        Assert.True(empty.Success);
        Assert.Equal(1, empty.LastIndex);
        Assert.Equal("", empty.Groups[1].ValueText);
    }

    [Theory]
    [InlineData(@"(?i)[\x00-\U0010ffffz-a]")]
    [InlineData(@"(?i)[\x00-\U0010ffff\g]")]
    [InlineData(@"(?i)[\x00-\U0010ffff")]
    public void CompleteMemberDoesNotHideLaterSyntaxErrors(string pattern)
        => Assert.Throws<PythonRePatternException>(() => Compile(pattern));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OtherRangesAndSmallByteCapsRetainConstructionBackstops(bool partialRange)
    {
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8PythonRegex.Compile(
            partialRange ? @"(?i)[\x00-\U0010fffe]" : @"(?i)[\x00-\U0010ffff]",
            PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, Limits,
            partialRange ? new() { MaxWorkSteps = 20000 } : new() { MaxBytes = 1 },
            Timeout.InfiniteTimeSpan, CancellationToken.None));
        Assert.Equal(partialRange ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes,
            error.ResourceKind);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void NonconstantClassesStillUsePythonCasePartners()
        => Assert.Equal(new[] { "K", "ſ", "İ", "ı" }, Compile(@"(?i)[a-z]")
            .FindAllToStrings("Kſİı"u8).ScalarValues);

    [Fact]
    public void TimeoutKeepsOriginalPatternAndDeadline()
    {
        var regex = Utf8PythonRegex.Compile(@"(?i)[\x00-\U0010ffff]+", PythonReCompileOptions.None,
            TimeSpan.FromMilliseconds(20), Limits with { MaxDepth = 4096 }, new() { MaxWorkSteps = 20000 },
            Timeout.InfiniteTimeSpan, CancellationToken.None);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var reached = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ => { reached = true; ticks = 50; });
        try
        {
            var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(new byte[65536]));
            Assert.True(reached);
            Assert.Equal(regex.Pattern, error.Pattern);
            Assert.Equal(TimeSpan.FromMilliseconds(20), error.MatchTimeout);
            Assert.Equal(string.Empty, error.Input);
        }
        finally { diagnostics.Complete(); }
    }
}
