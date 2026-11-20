using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ControlledDecimalBooleanTests
{
    private const string Pattern = @"^[-+]?\d*\.?\d*$";

    [Theory]
    [InlineData("", true)]
    [InlineData("+", true)]
    [InlineData("-", true)]
    [InlineData(".", true)]
    [InlineData("-.", true)]
    [InlineData("+.1", true)]
    [InlineData("12.", true)]
    [InlineData("-12.34\n", true)]
    [InlineData("-3.123\n", true)]
    [InlineData("-3.1234\n", true)]
    [InlineData("-3.1234𝟙\n", true)]
    [InlineData("-3.1234𝟙x2", false)]
    [InlineData("\n", true)]
    [InlineData("12\n\n", false)]
    [InlineData("1.2.3", false)]
    [InlineData("x12", false)]
    [InlineData("+١٢.٣", true)]
    [InlineData("𝟙.٢", true)]
    [InlineData("1é", false)]
    public void ControlledBooleanKeepsTheScalarGrammar(string text, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        foreach (var compiled in new[] { false, true })
        {
            var regex = Create(compiled, 100000, 256, 1 << 20, 1 << 20);
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
        }
    }

    [Theory]
    [InlineData("-3.14159")]
    [InlineData("3.14159")]
    [InlineData("-314159")]
    [InlineData("-")]
    [InlineData(".\n")]
    public void DisjointTerminalOptionalsNeedNoRewindStorage(string text)
    {
        using var source = new CancellationTokenSource();
        using var regex = Create(false, 128, 1, 1, 1);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(text), source.Token));
    }

    [Theory]
    [InlineData("-3.14159", true)]
    [InlineData("-1234567890123456789012345678901", true)]
    [InlineData("-3.14x1", false)]
    public void ShortProvedDecimalRunsKeepTheirEndpointsWithinTheWorkLimit(string text, bool expected)
    {
        using var source = new CancellationTokenSource();
        using var regex = Create(true, 8, 1, 1, 1);
        Assert.Equal(expected, regex.IsMatch(Encoding.ASCII.GetBytes(text), source.Token));
    }

    [Theory]
    [InlineData("12/12/2001")]
    [InlineData("1/2/2001\n")]
    [InlineData("12/12/20٠1")]
    [InlineData("12/12/20𝟙1")]
    [InlineData("١٢/12/٢٠٠١\n")]
    [InlineData("𝟙/٢/٢٠٠١")]
    public void TerminalAsciiDelimitersProveUnicodeDigitRepeats(string text)
    {
        using var source = new CancellationTokenSource();
        using var regex = new Utf8Regex(@"^\d{1,2}/\d{1,2}/\d{4}$", options: RegexOptions.Compiled,
            policy: new() { ResourcePolicy = new() { MaxWorkSteps = 128, MaxDepth = 1, MaxWorkingBytes = 1 } });
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(3, Enumerable.Range(0, program.Instructions.Length).Count(program.SkipsTerminalRewind));
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(text), source.Token));
    }

    [Theory]
    [InlineData(@"\b(\d{1,2})/\d{1,2}/(\d{2,4})\b")]
    [InlineData(@"\b(\d{1,2}?)/\d{1,2}?/(\d{2,4}?)\b")]
    public void TerminalUnicodeBoundaryProvesMandatoryDecimalRuns(string pattern)
    {
        using var regex = new Utf8Regex(pattern, options: RegexOptions.Compiled,
            policy: new() { ResourcePolicy = new() { MaxWorkSteps = 128, MaxWorkingBytes = 1 << 20 } });
        using var source = new CancellationTokenSource();
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(3, Enumerable.Range(0, program.Instructions.Length).Count(program.SkipsTerminalRewind));
        Assert.False(regex.IsMatch(" 𝟙/٢/٢٠٢٦x"u8, source.Token));

        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[3];
        var match = regex.Match(" 𝟙/٢/٢٠٢٦!"u8, captures: captures, cancellationToken: source.Token);
        Assert.Equal(new Utf8CaptureRange(1, 17), match);
        Assert.Equal(new Utf8CaptureRange(1, 5), captures[1]);
        Assert.Equal(new Utf8CaptureRange(9, 17), captures[2]);
    }

    [Fact]
    public void DecimalWindowsSkipImpossibleContinuationsWithinWorkLimit()
    {
        using var source = new CancellationTokenSource();
        using var regex = new Utf8Regex(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", options: RegexOptions.Compiled,
            policy: new() { ResourcePolicy = new() { MaxWorkSteps = 55, MaxWorkingBytes = 1 } });

        Assert.False(regex.IsMatch("Today is 11/18/201A and tomorrow is 11/19/201A."u8, source.Token));
        Assert.Equal(new Utf8CaptureRange(7, 22), regex.Match(" 1/2x! 1/𝟙/٢٠٢٦!"u8, cancellationToken: source.Token));
    }

    [Fact]
    public void ShortDecimalWindowReusesACompleteBooleanProof()
    {
        const string pattern = @"\b\d{1,2}/\d{1,2}/\d{2,4}\b";
        using var source = new CancellationTokenSource();
        using var limited = new Utf8Regex(pattern, RegexOptions.Compiled,
            new() { ResourcePolicy = new() { MaxWorkSteps = 8, MaxWorkingBytes = 1 } });
        Assert.True(limited.IsMatch("Today is 11/18/2019 and tomorrow is 11/19/2019."u8, source.Token));

        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled);
        Assert.False(regex.IsMatch("11/18/2019A"u8));
        Assert.False(regex.IsMatch("é11/18/2019"u8));
        Assert.True(regex.IsMatch("😀11/18/2019😀"u8));
        Assert.True(regex.IsMatch(" 1/𝟙/٢٠٢٦!"u8));
        Assert.Equal(new Utf8CaptureRange(4, 14), regex.Match("😀11/18/2019😀"u8));
    }

    [Theory]
    [InlineData(@"\d{1,2}[0-9]{0,2}", "😀1234!", 4, 8)]
    [InlineData(@"\d{1,2}[0-9]{0,2}?", "😀1234!", 4, 6)]
    [InlineData(@"\d{1,2}[0-9]??", "1234!", 0, 2)]
    [InlineData(@"\d{1,2}[0-9]{0,2}?1", "12311!", 0, 4)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "😀11/18/2019😀", 4, 14)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "😀𝟙/٢/２０２６!", 4, 24)]
    public void ShortDecimalProofPreservesThePreferredWholeRange(string pattern, string text, int start, int end)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var tokenSource = new CancellationTokenSource();
        using var ordinary = new Utf8Regex(pattern, RegexOptions.Compiled);
        using var limited = new Utf8Regex(pattern, RegexOptions.Compiled,
            new() { ResourcePolicy = new() { MaxWorkSteps = 512, MaxWorkingBytes = 4096 } });
        var expected = new Utf8CaptureRange(start, end);
        Assert.Equal(expected, ordinary.Match(input));
        Assert.Equal(expected, limited.Match(input, cancellationToken: tokenSource.Token));
    }

    [Fact]
    public void ShortDecimalProofKeepsRepeatedCaptureHistory()
    {
        const string pattern = @"(\d{4}[- ]){3}\d{3,4}";
        using var token = new CancellationTokenSource();
        using var limited = new Utf8Regex(pattern, RegexOptions.Compiled,
            new() { ResourcePolicy = new() { MaxWorkSteps = 12, MaxWorkingBytes = 1 } });
        Assert.True(limited.IsMatch("1234-5678-1234-456"u8, token.Token));
        Assert.False(limited.IsMatch("1234-5678-1234-45X"u8, token.Token));
        Assert.True(limited.IsMatch("1𝟚34-5678-1234-456"u8, token.Token));
        Assert.True(limited.IsMatch("𝟙234-5678-1234-456"u8, token.Token));
        Assert.False(limited.IsMatch("1²34-5678-1234-456"u8, token.Token));

        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled);
        const string text = "😀١٢٣٤-5678 1234-456";
        var bytes = Encoding.UTF8.GetBytes(text);
        var expected = Regex.Match(text, pattern);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(bytes, cancellationToken: token.Token));
        Utf8MatchSequence.AssertRange(regex.Match(bytes), text, expected.Index, expected.Length);
        Utf8MatchSequence.AssertCaptures(expected, actual, bytes, text);
    }

    [Fact]
    public void DecimalWindowCanStillConsumeAnOverlappingDelimiter()
    {
        using var regex = new Utf8Regex(@"\b\d{1,2}1\d{1,2}/\d{2,4}\b", options: RegexOptions.Compiled);
        Assert.Equal(new Utf8CaptureRange(1, 10), regex.Match(" 1212/2026!"u8));
    }

    [Theory]
    [InlineData(@"\A\p{Nd}{1,3}\b", "1٢!", RegexOptions.ECMAScript, 1)]
    [InlineData(@"\A[\d!]{1,3}\b", "1!!", RegexOptions.None, 1)]
    [InlineData(@"\A\d{0,3}\b", "12x", RegexOptions.None, 0)]
    [InlineData(@"\A\d{1,3}\B", "12!", RegexOptions.None, 1)]
    public void OtherWordBoundaryTailsCanStillRewind(string pattern, string text, RegexOptions options, int end)
    {
        using var regex = new Utf8Regex(pattern, options: options);
        Assert.Equal(new Utf8CaptureRange(0, end), regex.Match(Encoding.UTF8.GetBytes(text)));
    }

    [Theory]
    [InlineData(@"\A(?<run>\d{1,3})٢\z", "١٢")]
    [InlineData(@"\A(?<run>\d{1,3})2\z", "١2")]
    public void TerminalDigitSuffixCanStillRewindAnOverlappingRepeat(string pattern, string text)
    {
        using var regex = new Utf8Regex(pattern);
        var bytes = Encoding.UTF8.GetBytes(text);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        var match = regex.Match(bytes, 0, captures);
        Assert.Equal((0, bytes.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.True(captures[1].Success);
        Assert.Equal((0, 2), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
    }

    [Fact]
    public void DigitDiscoverySpendsTheOriginalWorkAllowance()
    {
        var input = Encoding.ASCII.GetBytes(new string('1', 4096));
        const ulong workLimit = 1;
        var regex = Create(false, workLimit, 65536, 8 << 20, 1);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(workLimit, error.Limit);
        Assert.True(error.Requested > error.Limit);
        Assert.True(Create(false, 1000000, 65536, 8 << 20, 1).IsMatch(input));
    }

    [Theory]
    [InlineData(false, 128)]
    [InlineData(true, 128)]
    [InlineData(false, 80)]
    [InlineData(true, 80)]
    public void ScalarDiscoveryKeepsOriginalCancellationAndDeadline(bool deadline, int blocks)
    {
        // The diagnostic hook fires every 64 modeled steps. A long ASCII
        // digit run now charges one step per checked block.
        var input = Encoding.ASCII.GetBytes(new string('1', blocks * 2048));
        var regex = Create(false, 100000000, 65536, 8 << 20, 1);
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var triggered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (triggered) return;
            triggered = true;
            if (deadline) clock.Advance(2000); else source.Cancel();
        });
        try
        {
            if (deadline)
            {
                var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input, source.Token));
                Assert.Empty(error.Input);
            }
            else
            {
                var error = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, source.Token));
                Assert.Equal(source.Token, error.CancellationToken);
            }
            Assert.True(triggered);
        }
        finally { diagnostics.Complete(); }
        Assert.True(regex.IsMatch(input));
        source.Cancel();
        var canceled = Assert.Throws<OperationCanceledException>(() => regex.IsMatch([0xff], source.Token));
        Assert.Equal(source.Token, canceled.CancellationToken);
    }

    private static Utf8Regex Create(bool compiled, ulong work, uint depth, ulong working, ulong result) =>
        new(Pattern, options: RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None), policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1),
            ResourcePolicy = new()
            {
                MaxWorkSteps = work,
                MaxDepth = depth,
                MaxWorkingBytes = working,
                MaxResultBytes = result
            }
        });
}
