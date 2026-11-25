using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TerminalClassRepeatTests
{
    [Theory]
    [InlineData(@"\w+", "é²a\u0301𐐀", 2)]
    [InlineData(@"\d+", "\U000104A0\U000104A1!12", 2)]
    [InlineData(@"\D{2,3}", "😀éab12😀Z", 2)]
    [InlineData(@"\p{L}{2,3}", "éééé!AB", 2)]
    [InlineData(@"[^\p{L}]+", "ab😀²!Z", 1)]
    [InlineData(@"(?i)\p{Ll}+", "aÉ𐐀!11", 1)]
    [InlineData(@"[\uD800-\uDFFF]+", "😀", 0)]
    [InlineData(@"[^\uD800]+", "😀é", 1)]
    [InlineData("x*", "xx😀x", 4)]
    [InlineData("(?:😀)*", "😀😀é😀", 4)]
    [InlineData("é{0,2}", "ééé😀", 4)]
    [InlineData(@"\w*", "a😀", 3)]
    [InlineData(@"[^\n]*", "", 1)]
    [InlineData(@"[^\n]*", "a\n\n😀", 5)]
    [InlineData(@"[^\r\n]*", "a\r\né", 5)]
    [InlineData(@"[^!]*", "😀!é!", 5)]
    [InlineData(@"[^\n]{0,2}", "é😀\n猫猫", 4)]
    [InlineData(@"(?s:.)*", "😀\n", 2)]
    [InlineData(@"([^\n]{0,2})", "é😀\n猫猫", 4)]
    public void CompleteScalarsKeepCoreClassSemantics(string pattern, string text, int expected)
    {
        var regex = Create(pattern, 256);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.Count(input));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input, token.Token));
    }

    [Theory]
    [InlineData(@"(\w+)", "abc def", 2)]
    [InlineData(@"\w+?", "abc def", 6)]
    [InlineData(@"\w*", "a😀", 3)]
    [InlineData(@"\w+Z", "abcZ", 1)]
    [InlineData(@"(?:\w+|a)b", "ab", 1)]
    [InlineData(@"(?<=\w{2})\w+", "abXYZ", 1)]
    [InlineData(@"(?=\w+)\w+Z", "abcZ", 1)]
    public void GeneralRewindCaptureLazyEmptyAndAssertionPathsRemainValid(string pattern, string text, int expected)
        => Assert.Equal(expected, Create(pattern, 256).Count(Encoding.UTF8.GetBytes(text)));

    [Theory]
    [InlineData(" ", "doc😀", "")]
    [InlineData("\u2003", "doc😀", "")]
    [InlineData(" ", "", "")]
    [InlineData("\u2003", "doc😀", "\n")]
    public void OptionalScalarBeforeTerminalDotKeepsGreedyCaptureWithoutRetryStorage(
        string whitespace, string body, string suffix)
    {
        const string pattern = @"^///\s?(?<doc>.*)$";
        var input = Encoding.UTF8.GetBytes("///" + whitespace + body + suffix);
        using var funded = Utf8Regex.Compile(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 128, MaxWorkingBytes = 1 }
        });
        using var token = new CancellationTokenSource();
        var end = input.Length - Encoding.UTF8.GetByteCount(suffix);
        var wholeRange = new Utf8CaptureRange(0, end);
        Assert.Equal(wholeRange, funded.Match(input, cancellationToken: token.Token));

        using var regex = Utf8Regex.Compile(pattern);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        Assert.Equal(wholeRange, regex.Match(input, captures: captures));
        Assert.Equal(new Utf8CaptureRange(3 + Encoding.UTF8.GetByteCount(whitespace), end), captures[1]);
    }

    [Theory]
    [InlineData("!é😀 rest\n", "$", 12, 7)]
    [InlineData("!é😀 rest\n", @"\z", -1, -1)]
    [InlineData("!é😀 rest\nx", "$", -1, -1)]
    [InlineData("!é😀\n", "$", 8, 8)]
    [InlineData("!é😀\n", @"\z", 8, 8)]
    public void GreedyRepeatBeforeTerminalDotPreservesFinalNewlineAndCaptures(
        string text, string anchor, int end, int headEnd)
    {
        using var regex = Utf8Regex.Compile(@"\A!(?<head>[^ ]+)(?<rest>.*)" + anchor, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 256, MaxDepth = 32, MaxWorkingBytes = 4096 }
        });
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(text);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[3];
        var expected = end < 0 ? default : new Utf8CaptureRange(0, end);

        Assert.Equal(expected, regex.Match(input, captures: captures, cancellationToken: token.Token));
        Assert.Equal(expected, captures[0]);
        Assert.Equal(headEnd < 0 ? default : new Utf8CaptureRange(1, headEnd), captures[1]);
        Assert.Equal(headEnd < 0 ? default : new Utf8CaptureRange(headEnd, end), captures[2]);
    }

    [Theory]
    [InlineData("Af029C", true)]
    [InlineData("Af029C\n", true)]
    [InlineData("Af029C\n\n", false)]
    [InlineData("Af029", false)]
    [InlineData("Af029G", false)]
    [InlineData("Af02😀", false)]
    public void ShortAnchoredSetRepeatsKeepTheRangeAndCaptureSlots(string text, bool success)
    {
        const string pattern = @"^(?<r>[a-f0-9]{2})(?<g>[a-f0-9]{2})(?<b>[a-f0-9]{2})$";
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(text);
        using var regex = new Utf8Regex(pattern, options);
        using var funded = new Utf8Regex(pattern, options, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 4, MaxDepth = 1, MaxWorkingBytes = 1 }
        });
        using var cancellation = new CancellationTokenSource();
        Utf8CaptureRange expected = success ? new(0, 6) : default;
        Assert.Equal(expected, funded.Match(input, cancellationToken: cancellation.Token));

        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[4];
        Assert.Equal(expected, regex.Match(input, captures: captures));
        Assert.Equal(expected, captures[0]);
        for (var i = 0; i < 3; i++)
            Assert.Equal(success ? new Utf8CaptureRange(i * 2, i * 2 + 2) : default, captures[i + 1]);

        using var limited = new Utf8Regex(pattern, options, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 1 }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Match(input)).ResourceKind);
    }

    [Theory]
    [InlineData(@"\A\s*(?<value>[a ])!\z", "a!", 0)]
    [InlineData(@"\A\s*(?<value>[a ])!\z", " a!", 1)]
    [InlineData(@"\A\s*(?<value>[a ])!\z", "\u2003a!", 3)]
    [InlineData(@"\A\S*(?<value>[a ])!\z", " !", 0)]
    [InlineData(@"\A\S*(?<value>[a ])!\z", "a !", 1)]
    public void GreedyHybridKeepsTheCaptureAfterAnEmptyOrConsumingRun(string pattern, string text, int captureStart)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var funded = Utf8Regex.Compile(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 128, MaxWorkingBytes = captureStart == 0 ? 1ul : 4096ul }
        });
        using var cancellation = new CancellationTokenSource();
        var whole = new Utf8CaptureRange(0, input.Length);
        Assert.Equal(whole, funded.Match(input, cancellationToken: cancellation.Token));

        using var regex = Utf8Regex.Compile(pattern);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        Assert.Equal(whole, regex.Match(input, captures: captures));
        Assert.Equal(new Utf8CaptureRange(captureStart, captureStart + 1), captures[1]);
    }

    [Fact]
    public void NullableTerminalCountKeepsRunsAcrossBlockAndScalarEdges()
    {
        const string pattern = @"[^\r\n]*";
        var input = Encoding.UTF8.GetBytes(new string('a', 2047) + "😀\r\né");
        using var token = new CancellationTokenSource();
        using var regex = Create(pattern, 1);
        Assert.Equal(5, regex.Count(input, token.Token));
        using var limited = Utf8Regex.Compile(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 2, MaxWorkingBytes = 1 }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count(input, token.Token)).ResourceKind);
    }

    [Theory]
    [InlineData(@"\AX(.+)", "\n", false)]
    [InlineData(@"\AX((?s:.+))", "\n", true)]
    [InlineData(@"\AX([^!\n]+)", "!", false)]
    public void ByteStopRepeatPublishesScalarAlignedCaptureAcrossBlocks(string pattern, string stop, bool crossesStop)
    {
        var run = "😀" + new string('a', 2042) + "𐐀";
        var input = Encoding.UTF8.GetBytes("X" + run + stop + "é");
        using var regex = Utf8Regex.Compile(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 128, MaxDepth = 16, MaxWorkingBytes = 4096 }
        });
        using var source = new CancellationTokenSource();
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        var end = crossesStop ? input.Length : 1 + Encoding.UTF8.GetByteCount(run);

        Assert.Equal(new Utf8CaptureRange(0, end), regex.Match(input, captures: captures, cancellationToken: source.Token));
        Assert.Equal(new Utf8CaptureRange(1, end), captures[1]);
        Assert.Equal(crossesStop, regex.Match(Encoding.UTF8.GetBytes("X" + stop), cancellationToken: source.Token).Success);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    public void AsciiWordRunKeepsTheScalarEndpointAndCanRewind(int length)
    {
        using var regex = Create(@"\A([A-Za-z0-9_]+)9😀\z", 256);
        var bytes = Encoding.UTF8.GetBytes("!" + new string('_', length - 1) + "9😀");
        var input = bytes.AsSpan(1);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        Assert.Equal(new Utf8CaptureRange(0, input.Length), regex.Match(input, captures: captures));
        Assert.Equal(new Utf8CaptureRange(0, length - 1), captures[1]);
    }

    [Fact]
    public void PositiveAsciiMaskStopsAtAsciiAndWideBytesAcrossBlocks()
    {
        using var regex = Create(@"\G([-/A~]+)", 128);
        using var cancellation = new CancellationTokenSource();
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        foreach (var length in new[] { 15, 16, 17, 31, 32, 63, 64, 65 })
        foreach (var stop in new[] { "Z", "é", "😀" })
        {
            var run = new string('~', length - 3) + "-/A";
            var input = Encoding.UTF8.GetBytes("😀" + run + stop + "~/");
            var expected = new Utf8CaptureRange(4, 4 + length);
            Assert.Equal(expected, regex.Match(input, 4, captures, cancellation.Token));
            Assert.Equal(expected, captures[1]);
        }
    }

    [Theory]
    [InlineData(@"\AX[A-Za-z0-9_]+!", 2048)]
    [InlineData(@"\AX[^\s!]+!", 64)]
    [InlineData(@"\AX[-A~]+!", 64)]
    public void AsciiClassRunIsAdmittedInBoundedWorkBlocks(string pattern, int blockLength)
    {
        var input = Encoding.UTF8.GetBytes("X" + new string('A', 12 * blockLength) + "!");
        using var admitted = RunWithWork(64);
        Assert.Equal(1, admitted.Count(input));
        using var limited = RunWithWork(8);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(8UL, error.Limit);

        Utf8Regex RunWithWork(ulong work) => Utf8Regex.Compile(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = work, MaxWorkingBytes = 1 }
        });
    }

    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    public void AsciiMaskRunKeepsWiderDelimiterAndByteCapture(int length)
    {
        var bytes = Encoding.UTF8.GetBytes("padX" + new string('_', length) + "é");
        using var regex = Utf8Regex.Compile(@"\AX([A-C_]+)é\z", policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 40 }
        });
        using var token = new CancellationTokenSource();
        var input = bytes.AsSpan(3);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        Assert.Equal(new Utf8CaptureRange(0, input.Length), regex.Match(input, captures: captures, cancellationToken: token.Token));
        Assert.Equal(new Utf8CaptureRange(1, length + 1), captures[1]);
    }

    [Theory]
    [InlineData(@"\AX[A-C_]+!", false)]
    [InlineData(@"\AX[A-C_]+!", true)]
    [InlineData(@"\AX[^\s!]+!", false)]
    [InlineData(@"\AX[^\s!]+!", true)]
    public void AsciiMaskBlocksObserveOriginalInterruption(string pattern, bool deadline)
    {
        var input = Encoding.ASCII.GetBytes("X" + new string('_', 64 * 130) + "!");
        using var regex = Utf8Regex.Compile(pattern, policy: new()
        {
            MatchTimeout = deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout,
            ResourcePolicy = new() { MaxWorkSteps = 512, MaxWorkingBytes = 1 }
        });
        using var clock = Utf8DeadlineTestClock.Start();
        using var token = new CancellationTokenSource();
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (deadline) clock.Advance(250); else token.Cancel();
        });
        try
        {
            if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input, token.Token));
            else Assert.Equal(token.Token,
                Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, token.Token)).CancellationToken);
        }
        finally { diagnostics.Complete(); }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BooleanAcceptsBeforeAProvedOptionalTailWhileMatchKeepsItsFullExtent(RegexOptions options)
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        var input = Encoding.UTF8.GetBytes("https://ab/" + new string('x', 65_536) + "?key=😀#fragment");
        using var limited = Utf8Regex.Compile(pattern, options, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 128 }
        });
        using var full = Utf8Regex.Compile(pattern, options);
        using var token = new CancellationTokenSource();
        Assert.True(limited.IsMatch(input, token.Token));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Match(input.AsSpan(), cancellationToken: token.Token)).ResourceKind);
        Assert.Equal(input.Length, full.Match(input.AsSpan(), cancellationToken: token.Token).EndOffsetInBytes);
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => limited.IsMatch(input, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(@"X[^\s]{2,}(?:\sZ)?", "X😀", false)]
    [InlineData(@"X[^\s]{2,}(?:\sZ)?", "X😀é", true)]
    [InlineData(@"^X[^\s]+(?:\sZ)?$", "Xa x", false)]
    [InlineData(@"^(?>a+)a$", "aaa", false)]
    [InlineData(@"X(?<word>[a-z]+)(?:-)?\k<word>", "Xabc", false)]
    [InlineData(@"X(?<word>[a-z]+)(?:-)?\k<word>", "Xabab", true)]
    [InlineData(@"X[abc]+(?=Z)", "Xabc", false)]
    public void BooleanRepeatMinimumKeepsScalarBoundsAndRequiredContinuation(string pattern, string text, bool expected)
    {
        using var regex = Create(pattern, 256);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(expected, regex.Match(input.AsSpan()).Success);
    }

    [Fact]
    public void TerminalCountsElideDepthWhileObservedCapturesAndReferencesKeepTheirLimit()
    {
        Assert.Equal(2, Create(@"\w+", 1).Count("abc ééé"u8));
        Assert.Equal(2, Create(@"(\w+)", 1).Count("abc ééé"u8));
        Assert.Equal(2, Create(@"(\w)+", 1).Count("abc ééé"u8));
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(Create(@"(\w)+", 1).CreateMatchCursor("abc ééé"u8.ToArray())));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth, failure.ResourceKind);
        var dependent = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(@"(\w)+\1", 1).Count("aaa"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth, dependent.ResourceKind);
        var reverse = new Utf8Regex(@"\w+", options: RegexOptions.CultureInvariant | RegexOptions.RightToLeft);
        Assert.Equal(2, reverse.Count("abc ééé"u8));
    }

    [Theory]
    [InlineData(@"\p{L}+", "éé A𐐀!", 2)]
    [InlineData("x*", "xxé😀x", 5)]
    [InlineData(@"\w*", "a😀", 3)]
    public void EveryWorkCutoffPreservesTheBorrowedLoanAndKeepsScalarWorkMetered(string pattern, string text, int expected)
    {
        var regex = new Utf8Regex(pattern);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Missing scalar program.");
        var input = Encoding.UTF8.GetBytes(text);
        var baseline = new Utf8SliceInvocation(new(100000, 1, 1 << 20, 1), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(expected, result.Count);
        Assert.Equal(0U, baseline.Budget.PeakDepth);
        Assert.InRange(baseline.Budget.Steps, 8UL, 100UL);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong limit = 1; limit <= baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 1, 1 << 20, 1), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var actual = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
            Assert.Equal(limit == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, actual.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void APlainRunBorrowsNoWorkingCapacityFromItsParent()
    {
        var regex = new Utf8Regex(@"\p{L}+");
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Missing scalar program.");
        var invocation = new Utf8SliceInvocation(new(100000, 1, 13, 1), Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, "éé A𐐀!"u8, 0, [], [], ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(2, result.Count);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(13UL, invocation.Budget.PeakWorkingBytes);
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 10000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };
        var plain = new Utf8Regex(@"\w+", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        Assert.Equal(2, plain.Count("abc ééé"u8));
        var captured = new Utf8Regex(@"(\w+)", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        Assert.Equal(2, captured.Count("abc ééé"u8));
        var detailed = new Utf8Regex(@"(\w+)", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy with
            {
                MaxResultBytes = 1 << 20
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(detailed.CreateMatchCursor("abc ééé"u8.ToArray()))).ResourceKind);
    }

    [Theory]
    [InlineData(false, @"\p{L}+")] [InlineData(true, @"\p{L}+")]
    [InlineData(false, "x*")] [InlineData(true, "x*")]
    [InlineData(false, @"[^\n]*")] [InlineData(true, @"[^\n]*")]
    [InlineData(false, @"\AX[A-Za-z0-9_]+!")] [InlineData(true, @"\AX[A-Za-z0-9_]+!")]
    public void OriginalInterruptionDuringTheRunReleasesScratch(bool timeout, string pattern)
    {
        var regex = new Utf8Regex(pattern);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Missing scalar program.");
        var input = Encoding.UTF8.GetBytes(pattern switch
        {
            @"\AX[A-Za-z0-9_]+!" => "X" + new string('A', 80 * 2048) + "!",
            @"[^\n]*" => new string('é', 65_536),
            _ => new string('é', 2048),
        });
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (timeout) ticks = 25; else token.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(100000, 1, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var failure = Record.Exception(() => Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            Assert.True(invocation.Budget.Steps >= 64);
        }
        finally { session.Complete(); }
    }

    private static Utf8Regex Create(string pattern, uint depth) => new(pattern, options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
    {
        MatchTimeout = Timeout.InfiniteTimeSpan,
        ResourcePolicy = new Utf8ResourcePolicy
        {
            MaxDepth = depth,
            MaxWorkSteps = 1000000,
            MaxWorkingBytes = 1 << 20,
            MaxResultBytes = 1
        }
    });
}
