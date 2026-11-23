using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DotTailRepeatTests
{
    [Fact]
    public void BoundedDotTailKeepsScalarBoundsAndCaptureAcrossBoundary()
    {
        using var regex = new Utf8Regex(@"\Astart(?<gap>.{8,25})\bfinish", RegexOptions.Compiled,
            new() { ResourcePolicy = Policy() });
        using var source = new CancellationTokenSource();
        var gap = new string('x', 23) + "😀 ";
        var subject = Encoding.UTF8.GetBytes("start" + gap + "finish");
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];

        Assert.True(regex.Match(subject, captures: captures, cancellationToken: source.Token).Success);
        Assert.Equal(gap, Encoding.UTF8.GetString(subject[captures[1].StartOffsetInBytes..captures[1].EndOffsetInBytes]));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("startx" + gap + "finish"), source.Token));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("start" + gap + "finley"), source.Token));
        Assert.False(regex.IsMatch("startabcdefg\n finish"u8, source.Token));
    }

    private static Utf8ResourcePolicy Policy(uint depth = 128, ulong result = 1 << 20, ulong working = 1 << 20) => new()
    {
        MaxWorkSteps = 1_000_000,
        MaxDepth = depth,
        MaxWorkingBytes = working,
        MaxResultBytes = result
    };

    [Theory]
    [InlineData(@"(?<head>.*?),", 2047, "😀,é,,\nq,")]
    [InlineData(@"(?<head>.*?),", 0, "😀é\nq,")]
    [InlineData(@"(?s:(?<head>.*?),)", 0, "😀é\nq,")]
    [InlineData(@"(?<head>.*?)\n", 2048, "😀é\n\n")]
    [InlineData(@"(?<head>.*?),X", 0, "😀no,éyes,X")]
    [InlineData(@"(?<head>.+?),", 0, ",😀,é")]
    public void LazyDotKeepsShortestReachableEndAndCapture(string pattern, int padding, string tail)
    {
        var text = new string('x', padding) + tail;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new Utf8RegexPolicy { ResourcePolicy = Policy() });
        var expected = oracle.Match(text);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token.Token));
        Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
        Assert.Equal(oracle.Count(text), regex.Count(input, token.Token));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Singleline)]
    public void LazyDotSkipsDelimiterFreeLinesAndTailWithinTheWorkLimit(RegexOptions options)
    {
        var prefix = string.Concat(Enumerable.Repeat("😀 misses\r\n", 512));
        var input = Encoding.UTF8.GetBytes(prefix + "é😀hit,tail");
        const int floor = 4; // After the first supplementary scalar.
        using var regex = new Utf8Regex("(.*?),", options, new Utf8RegexPolicy
        {
            ResourcePolicy = Policy() with { MaxWorkSteps = 128 }
        });
        using var token = new CancellationTokenSource();
        using var cursor = regex.CreateMatchCursor(input, floor, token.Token);

        Assert.True(cursor.MoveNext(out var match));
        var start = options == RegexOptions.Singleline ? floor : Encoding.UTF8.GetByteCount(prefix);
        Assert.Equal(start, match.Value.StartOffsetInBytes);
        Assert.Equal(start, Assert.Single(match.GetCaptures(1).ToArray()).StartOffsetInBytes);
        Assert.Equal(input.Length - "tail"u8.Length, match.Value.EndOffsetInBytes);
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(1, regex.Count(input, token.Token));
    }

    [Theory]
    [InlineData(RegexOptions.None)] [InlineData(RegexOptions.Compiled)]
    public void CompleteGreedySinglelineEndNeedsNoRewindDepth(RegexOptions mode)
    {
        var text = new string('a', 4096) + "😀\né\0\r\n";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("(?s:^(?<body>.+)$)", options: mode, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Policy(1)
        });
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(retained.Success);
        Assert.Equal(input.Length, retained.Value.EndOffsetInBytes - retained.Value.StartOffsetInBytes);
        Assert.Equal(text, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        using var token = new CancellationTokenSource();
        Assert.Equal(1, regex.Count(input, token.Token));
        Assert.Equal(text, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }

    [Fact]
    public void SinglelineLazyAndConsumingTailsKeepTheirEarlierEndpointChoices()
    {
        var tail = new Utf8Regex("(?sm:^(?<body>.*)$\\nX)");
        Assert.Equal("a", Encoding.UTF8.GetString(Utf8MatchSequence.First(tail.CreateMatchCursor("a\nX"u8.ToArray())).GetGroupMemory(1).Span));
        var lazy = new Utf8Regex("(?sm:^(?<body>.*?)$)");
        Assert.Equal("a", Encoding.UTF8.GetString(Utf8MatchSequence.First(lazy.CreateMatchCursor("a\nb"u8.ToArray())).GetGroupMemory(1).Span));
        Assert.Equal(2, lazy.Count("a\nb"u8));
    }

    [Theory]
    [InlineData(@"(?s:\A(?<body>.*?)\z)", "😀\na\n", "😀\na\n")]
    [InlineData(@"(?s:\A(?<body>.*?)\Z)", "😀\na\n", "😀\na")]
    [InlineData(@"(?sm:\A(?<body>.*?)$)", "😀\na\n", "😀")]
    [InlineData(@"\A(?<body>.*?)$", "😀é\r\n", "😀é\r")]
    [InlineData(@"\A(?<body>.*?)\z", "😀é\n", null)]
    [InlineData(@"\A(?<body>.*?)😀\z", "😀é😀", "😀é")]
    [InlineData(@"\A(?<body>\d*?)$", "𝟠٣7\n", "𝟠٣7")]
    [InlineData(@"(?m:\A(?<body>\d*?)$)", "𝟠٣7\n9", "𝟠٣7")]
    [InlineData(@"\A(?<body>\d*?)\z", "𝟠٣7\n", null)]
    public void LazyEndAnchorsKeepTheirFirstViableByteBoundary(string pattern, string text, string? body)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
        {
            using var regex = new Utf8Regex(pattern, mode | RegexOptions.CultureInvariant,
                new Utf8RegexPolicy { ResourcePolicy = Policy() });
            var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token.Token));
            Assert.Equal(body is not null, match.Success);
            Assert.Equal(match.Success, regex.IsMatch(input, token.Token));
            if (match.Success) Assert.Equal(body, Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        }
    }

    [Fact]
    public void LazyAbsoluteEndCanConsumeLineFeedsWithinTheWorkLimit()
    {
        var text = string.Concat(Enumerable.Repeat("😀a\n", 2048));
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(@"(?s:\A(?<body>.*?)\z)", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = Policy() with { MaxWorkSteps = 1024 }
        });
        Assert.True(regex.IsMatch(input, token.Token));
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token.Token));
        Assert.True(match.Success);
        Assert.Equal(text, Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void LongLineScanKeepsScalarCapturesAcrossBlocksAndOffsetStorage(int prefixLength)
    {
        var line = new string('a', prefixLength) + "😀é\0\r";
        var storage = Encoding.UTF8.GetBytes("pad" + line + "\nZpad");
        var memory = storage.AsMemory(3, Encoding.UTF8.GetByteCount(line) + 2);
        var input = memory.Span;
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("(?m:^(?<line>.*)$)", options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy()
            });
            var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(memory));
            Assert.True(retained.Success);
            Assert.Equal(0, retained.Value.StartOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(line), retained.Value.EndOffsetInBytes - retained.Value.StartOffsetInBytes);
            Assert.Equal(line, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
            using var token = new CancellationTokenSource();
            Assert.Equal(2, regex.Count(input, token.Token));
            Assert.Equal(line, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        }
    }

    [Theory]
    [InlineData("(.*)$", "abc\n")]
    [InlineData("(.+)$", "abc\nxyz")]
    [InlineData(@"(.*)\z", "abc\nxyz")]
    [InlineData(@"(.*)\Z", "abc\n")]
    [InlineData(@"(.*)\n", "abc\nxyz\n")]
    [InlineData("(?m:^(.{0,3})$)", "abc\nabcd\n\n")]
    [InlineData("(?m:^([a-z]+)$)", "abc\nxyz\n")]
    [InlineData("(?sm:(.{1,2})$)", "a\nb")]
    [InlineData(@"(?s:(.*)\n)", "a\nb\nc")]
    [InlineData("(.+?)$", "abc\n")]
    [InlineData("((.+))$", "abc\n")]
    [InlineData(@"(?:a(.+)\n)+Z", "abc\nade\nZ")]
    public void PublicResultsAndCaptureHistoryAgreeOnOverlappingBmpSemantics(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.RightToLeft | RegexOptions.Compiled, RegexOptions.NonBacktracking })
        {
            var options = mode | RegexOptions.CultureInvariant;
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy()
            });
            var expected = oracle.Match(text);
            var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal(expected.Success, regex.IsMatch(input));
            Assert.Equal(expected.Success, regex.Match(input).Success);
            Assert.Equal(oracle.Count(text), regex.Count(input));
            Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)] [InlineData(RegexOptions.Compiled)]
    public void DotBoundsCountScalarsAndPreserveCapturedByteRanges(RegexOptions mode)
    {
        var regex = new Utf8Regex("(?m:^(.{1,2})$)", options: mode, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Policy(1)
        });
        var input = "😀é\n中"u8.ToArray();
        Assert.Equal(2, regex.Count(input));
        var first = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal((0, 6), (first.Value.StartOffsetInBytes, first.Value.EndOffsetInBytes - first.Value.StartOffsetInBytes));
        Assert.Equal("😀é", Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.Equal(1, first.GetCaptures(1).Length);
        Assert.Equal(2, regex.Count("AB\nXY"u8));
        Assert.Equal("😀é", Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData(RegexOptions.None, 16000u)]
    [InlineData(RegexOptions.Compiled, 2u)]
    public void GuardedGreedyRepeatRewindsWithBoundedScratch(RegexOptions mode, uint depth)
    {
        const string pattern = @"\A(?<body>(?:(?!STOP|HALT)[\s\S])+)ΩSTOP\z";
        var body = new string('a', 6000) + "😀é";
        var text = body + "ΩSTOP";
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(pattern, mode, new() { ResourcePolicy = Policy(depth, working: 32768) });
        Assert.Equal(1, regex.Count(input, token.Token));
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        Assert.True(regex.Match(input, 0, captures, token.Token).Success);
        Assert.Equal((0, Encoding.UTF8.GetByteCount(body)), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token.Token));
        Utf8MatchSequence.AssertCaptures(new Regex(pattern, mode).Match(text), actual, input, text);
    }

    [Fact]
    public void CompactRepeatRewindSkipsImpossibleStartsAndRetainsTheLaterBalance()
    {
        const string pattern = @"BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\s\S]))*END(?(open)(?!))";
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        var text = "BEGIN BEGIN " + new string('x', 4096) + "😀 END";
        var bytes = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options);
        using var regex = new Utf8Regex(pattern, options,
            new() { ResourcePolicy = Policy() with { MaxWorkSteps = 512 } });
        using var source = new CancellationTokenSource();

        Assert.Equal(1, oracle.Count(text));
        Assert.Equal(1, regex.Count(bytes, source.Token));
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(bytes, cancellationToken: source.Token));
        Assert.Equal((6, bytes.Length), (actual.Value.StartOffsetInBytes, actual.Value.EndOffsetInBytes));
        Utf8MatchSequence.AssertCaptures(oracle.Match(text), actual, bytes, text);
    }

    [Fact]
    public void OverlappingDotTailAdmitsOneRewindFrame()
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 2048) + "\n");
        var policy = Policy(depth: 1, working: 1);
        Assert.Equal(1, new Utf8Regex("(?m:^(.+)$)", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        }).Count(input));
        var overlapping = new Utf8Regex("(?m:^(.+)a$)", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => overlapping.Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind);
        using var admitted = new Utf8Regex(@"\Aa*a\z", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Policy(depth: 1, working: 768)
        });
        using var token = new CancellationTokenSource();
        Array.Fill(input, (byte)'a');
        Assert.True(admitted.IsMatch(input, token.Token));
        input[^1] = (byte)'b';
        Assert.False(admitted.IsMatch(input, token.Token));
    }

    [Theory]
    [InlineData(@"\A(?<head>[aé😀]{2,4})😀éa\z", 10, "aé")]
    [InlineData(@"\A(?=(?<head>[aé😀]{2,4})😀éa\z)a", 1, "aé")]
    [InlineData(@"\A(?<head>[aé😀]{3,4})😀éa\z", -1, "")]
    [InlineData(@"\A(?>(?<head>[aé😀]{2,4}))😀éa\z", -1, "")]
    [InlineData(@"\A(?<head>(?:(?!STOP|HALT)[\s\S]){2,4})😀éa\z", 10, "aé")]
    [InlineData(@"\A(?<head>(?:(?!STOP|HALT)[\s\S]){3,4})😀éa\z", -1, "")]
    [InlineData(@"\A(?>(?<head>(?:(?!STOP|HALT)[\s\S]){2,4}))😀éa\z", -1, "")]
    public void GreedyRewindPreservesScalarMinimaAndCaptureRestoration(string pattern, int end, string head)
    {
        using var regex = new Utf8Regex(pattern);
        using var token = new CancellationTokenSource();
        var input = "aé😀éa"u8.ToArray();
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token.Token));
        Assert.Equal(end >= 0, match.Success);
        Assert.Equal(end >= 0 ? 1 : 0, regex.Count(input, token.Token));
        if (!match.Success) return;
        Assert.Equal((0, end), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Equal(head, Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Single(match.GetCaptures(1).ToArray());
    }

    [Theory]
    [InlineData("(?<=(.*))X", "😀éX", "😀é")]
    [InlineData("(?s:(?<=(.*))X)", "abc\nX", "abc\n")]
    [InlineData("(?m:(?<=(.*)$)X)", "abc\nX", null)]
    public void LookbehindStillRequiresItsExactEndpoint(string pattern, string text, string? captured)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var match = Utf8MatchSequence.First(new Utf8Regex(pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy()
            })                .CreateMatchCursor(Encoding.UTF8.GetBytes(text)));
            Assert.Equal(captured is not null, match.Success);
            if (match.Success) Assert.Equal(captured, Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        }
    }

    [Theory]
    [InlineData("(?m:^(.+)$)", "😀é\n中", 1u, 2)]
    [InlineData(@"\A(?:(?=aa|ab)a){1,4}ab\z", "aaab", 2u, 1)]
    public void EveryWorkCutoffPreservesTheBorrowedLoan(string pattern, string text, uint depth, int count)
    {
        var program = new Utf8Regex(pattern).Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(text);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(new(100000, depth, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(count, Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref baseline).Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work <= baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, depth, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
            Assert.Equal(work == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, result.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("(?m:^(.+)$)", "😀", "", 0u, false)]
    [InlineData("(?m:^(.+)$)", "😀", "", 0u, true)]
    [InlineData("(?sm:^(.+)$)", "😀", "", 0u, false)]
    [InlineData("(?sm:^(.+)$)", "😀", "", 0u, true)]
    [InlineData(@"\A[ab]*a\z", "a", "b", 1u, false)]
    [InlineData(@"\A[ab]*a\z", "a", "b", 1u, true)]
    [InlineData(@"\A(?:(?!STOP|HALT)[\s\S])+ΩSTOP\z", "😀", "ΩSTOP", 2u, false)]
    [InlineData(@"\A(?:(?!STOP|HALT)[\s\S])+ΩSTOP\z", "😀", "ΩSTOP", 2u, true)]
    public void ScalarRepeatObservesOriginalInterruptionAndReleasesScratch(
        string pattern, string atom, string suffix, uint expectedDepth, bool timeout)
    {
        using var regex = new Utf8Regex(pattern);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        // The ASCII run ends before the first diagnostic checkpoint, during rewind.
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(atom, 65536)) + suffix);
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (timeout) ticks = 25; else token.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(100000, Math.Max(1u, expectedDepth), 1 << 20),
            TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var starts = new int[program.GroupCount];
            var ends = new int[program.GroupCount];
            var failure = Record.Exception(() => Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            Assert.True(invocation.Budget.Steps >= 64);
            Assert.Equal(expectedDepth, invocation.Budget.PeakDepth);
        }
        finally { session.Complete(); }
    }
}
