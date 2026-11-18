using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFullRangeConstructionTests
{
    private static readonly Utf8ResourcePolicy Limits = new()
    {
        MaxWorkSteps = 100000,
        MaxDepth = 64,
        MaxWorkingBytes = 1 << 20,
        MaxResultBytes = 1 << 20
    };

    private static Utf8PythonRegex Compile(string pattern, PythonReCompileOptions options = PythonReCompileOptions.None)
        => Utf8PythonRegex.Compile(pattern, options: options, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Limits,
            ConstructionPolicy = new()
            {
                MaxWorkSteps = 20000
            },
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: CancellationToken.None);

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
            Assert.Equal(accepts, regex.Match(Encoding.UTF8.GetBytes(scalar), mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match([], mode: Utf8PythonMatchMode.FullMatch).Success);


        // This rendering remains live for comparators and timeout pattern text.
        // Its surrogate-pair atom must still consume a supplementary scalar once.
        var managed = new Regex(PythonReDotNetReference.Render(regex), RegexOptions.CultureInvariant);
        Assert.Equal(accepts ? scalars : [], managed.Matches(text).Select(match => match.Value));
    }

    [Fact]
    public void BytePatternCapturesKeepCompleteScalarCoordinatesAndCompletion()
    {
        const string pattern = @"(?i)([\x00-\U0010ffff])";
        var regex = Utf8PythonRegex.Compile(Encoding.UTF8.GetBytes(pattern), options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Limits,
            ConstructionPolicy = new()
            {
                MaxWorkSteps = 20000
            },
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: CancellationToken.None);
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(("é😀\n"u8).ToArray(), 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var matches = collectedRanges1.ToArray();
        Assert.Equal(new[] { "é", "😀", "\n" }, matches.Select(match => System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(1).Span)));
        Assert.Equal(new[] { 0, 2, 6 }, matches.Select(match => match.Value.StartOffsetInBytes));
        Assert.Equal(new[] { 2, 6, 7 }, matches.Select(match => match.Value.EndOffsetInBytes));
        Assert.All(matches, match => Assert.Equal(1, match.LastIndex));
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void EmptyComplementRepeatKeepsCapturesAndScalarEmptyProgression()
    {
        var regex = Compile(@"(?i)([^\x00-\U0010ffff]*)");
        Assert.Equal(new[] { "", "", "" }, regex.FindAllToUtf8("é😀"u8).ScalarValues.Select(Encoding.UTF8.GetString));
        Utf8PythonRangeMatch empty = default;
        using (var captureCursor1 = regex.CreateMatchCursor(ReadOnlyMemory<byte>.Empty, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out empty);
        }
        Assert.True(empty.Success);
        Assert.Equal(1, empty.LastIndex);
        Assert.Equal("", System.Text.Encoding.UTF8.GetString((empty.Groups[1].Success ? empty.GetGroupMemory(empty.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
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
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8PythonRegex.Compile(partialRange ? @"(?i)[\x00-\U0010fffe]" : @"(?i)[\x00-\U0010ffff]", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Limits,
            ConstructionPolicy = partialRange ? new()
            {
                MaxWorkSteps = 20000
            }

    : new()
    {
        MaxBytes = 1
    },
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: CancellationToken.None));
        Assert.Equal(partialRange ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes,
            error.ResourceKind);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void NonconstantClassesStillUsePythonCasePartners()
        => Assert.Equal(new[] { "K", "ſ", "İ", "ı" }, PythonReFindAllAssert.Decode(Compile(@"(?i)[a-z]")
            .FindAllToUtf8("Kſİı"u8)).ScalarValues);

    [Fact]
    public void TimeoutKeepsOriginalPatternAndDeadline()
    {
        var regex = Utf8PythonRegex.Compile(@"(?i)[\x00-\U0010ffff]+z", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Limits with
            {
                MaxDepth = 4096
            },
            ConstructionPolicy = new()
            {
                MaxWorkSteps = 20000
            },
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: CancellationToken.None);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var reached = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ => { reached = true; ticks = 50; });
        try
        {
            var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(new byte[1 << 20]));
            Assert.True(reached);
            Assert.Equal(regex.Pattern, error.Pattern);
            Assert.Equal(TimeSpan.FromMilliseconds(20), error.MatchTimeout);
            Assert.Equal(string.Empty, error.Input);
        }
        finally { diagnostics.Complete(); }
    }
}
