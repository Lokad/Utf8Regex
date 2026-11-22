using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaptureSlotMatchTests
{
    [Theory]
    [InlineData(RegexOptions.None, 3, 6)]
    [InlineData(RegexOptions.RightToLeft, 12, 15)]
    public void OmittedStartUsesDirectionAndSelectedEnd(RegexOptions options, int start, int end)
    {
        var regex = new Utf8Regex("(?<word>[a-z]+)", options: options | RegexOptions.CultureInvariant);
        var input = "é one 😀 two"u8.ToArray();
        var slots = new Utf8CaptureRange[2];
        var match = regex.Match(input, captures: slots);
        Assert.Equal((start, end), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal(match, slots[0]);
        Assert.Equal(match, slots[1]);
        Assert.Equal(match, regex.Match(input));
        var bounded = regex.Match(input, captures: slots, endOffsetInBytes: 11);
        Assert.Equal((3, 6), (bounded.StartOffsetInBytes, bounded.EndOffsetInBytes));
        Assert.Equal(bounded, slots[1]);
    }

    public static IEnumerable<object[]> Cases()
    {
        string[] regular = ["", "ab", "é", "ab|a|bc|b", "ab|a|bc|b|é|d|ef|f", "(a)+(b)?", @"(?<10>a)(?<2>b)?(?<word>c)*",
            @"(?<x>a)(?<x>b)", @"(?<word>\w+)", @"(a|ab)+", @"(?:(?<x>a)|b)+"];
        string[] backtracking = [@"(?<x>a+)\k<x>", @"(?<=é)(?<x>a+)", @"(?=(?<ahead>ab))a",
            @"\G(?<x>ab)", @"(?<x>a)+(?<-x>b)+(?(x)(?!))", @"(?<x>a)+(?<y-x>b)(?(x)c|d)"];
        foreach (var pattern in regular.Concat(backtracking))
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.RightToLeft | RegexOptions.Compiled, RegexOptions.NonBacktracking })
        {
            if (mode == RegexOptions.NonBacktracking && !regular.Contains(pattern)) continue;
            foreach (var controlled in new[] { false, true }) yield return [pattern, mode, controlled];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ByteStartsAndEndsPreserveLastCaptures(string pattern, RegexOptions options, bool controlled)
    {
        options |= RegexOptions.CultureInvariant;
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = controlled ? new()
            {
                MaxWorkSteps = 1_000_000,
                MaxDepth = 512,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1
            }

            : default
        });
        using var source = new CancellationTokenSource();
        var numbers = oracle.GetGroupNumbers();
        Assert.Equal(numbers, Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot).ToArray());
        var sentinel = new Utf8CaptureRange(123, 124);
        foreach (var text in new[] { "", "abc", "éaababc d", "aaabbbd", "aaaac", "éé abc éa" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            foreach (var end in new[] { 0, text.Length / 2, text.Length }.Distinct())
            for (var start = 0; start <= end; start++)
            {
                // BMP subjects make every UTF16 position a scalar boundary.
                // Prefix-only truncation retains lookbehind and absolute origin.
                var expected = oracle.Match(text[..end], start);
                var byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, start));
                var byteEnd = Encoding.UTF8.GetByteCount(text.AsSpan(0, end));
                foreach (var length in new[] { 0, 1, numbers.Length, numbers.Length + 2 }.Distinct())
                {
                    var slots = Enumerable.Repeat(sentinel, length).ToArray();
                    var actual = regex.Match(input, byteStart, slots, controlled ? source.Token : default,
                        endOffsetInBytes: byteEnd);
                    EqualRange(text, expected, actual);
                    for (var ordinal = 0; ordinal < Math.Min(length, numbers.Length); ordinal++)
                        EqualRange(text, expected.Success ? expected.Groups[numbers[ordinal]] : null, slots[ordinal]);
                    foreach (var excess in slots.Skip(numbers.Length)) Assert.Equal(sentinel, excess);
                }
            }
        }
    }

    private static void EqualRange(string text, Group? expected, Utf8CaptureRange actual)
    {
        Assert.Equal(expected?.Success == true, actual.Success);
        Assert.Equal(expected?.Success == true ? Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)) : 0,
            actual.StartOffsetInBytes);
        Assert.Equal(expected?.Success == true ? Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index + expected.Length)) : 0,
            actual.EndOffsetInBytes);
    }

    [Theory]
    [InlineData(@"(?<=(\w))B", "😀éB", 4, 6)]
    [InlineData(@"(?<=(.))B", "😀B", 0, 4)]
    public void ForwardLookbehindPublishesOrderedCaptureEdges(string pattern, string text, int start, int end)
    {
        using var regex = Utf8Regex.Compile(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
        var match = regex.Match(input, end, slots);
        Assert.True(match.Success);
        Assert.Equal((end, end + 1), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.True(slots[1].Success);
        Assert.Equal((start, end), (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));

        using var cursor = regex.CreateMatchCursor(input, end);
        Assert.True(cursor.MoveNext(out var range));
        Assert.True(range.TryGetGroup(1, out var capture));
        Assert.Equal(slots[1], capture);
        Assert.True(range.GetGroupMemory(1).Span.SequenceEqual(input.AsSpan(start, end - start)));
    }

    [Fact]
    public void WholeMatchOperationsOnlyReserveCapturesIfExecutionReadsThem()
    {
        var policy = new Utf8RegexPolicy { ResourcePolicy = new() { MaxWorkingBytes = 1 } };
        using var valueOnly = new Utf8Regex("([A-Z])", policy: policy);
        var input = "é: BB!"u8.ToArray();
        var whole = new Utf8CaptureRange[1];
        var extent = valueOnly.Match(input, 2, whole);
        Assert.True(extent.Success);
        Assert.Equal((4, 5), (extent.StartOffsetInBytes, extent.EndOffsetInBytes));
        Assert.Equal(extent, valueOnly.Match(input, 2));
        Assert.Equal(extent, whole[0]);

        var ranges = valueOnly.EnumerateMatches(input, startOffsetInBytes: 2);
        Assert.True(ranges.MoveNext());
        Assert.Equal(extent, ranges.Current);
        Assert.True(ranges.MoveNext());
        Assert.Equal((5, 6), (ranges.Current.StartOffsetInBytes, ranges.Current.EndOffsetInBytes));
        Assert.False(ranges.MoveNext());

        var detail = new Utf8CaptureRange[2];
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => valueOnly.Match(input, 2, detail)).ResourceKind);
        using var reference = new Utf8Regex(@"([A-Z])\1", policy: policy);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => reference.Match(input, 2, whole)).ResourceKind);
        Assert.False(whole[0].Success);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => reference.EnumerateMatches(input, 2).MoveNext()).ResourceKind);
    }

    [Theory]
    [InlineData(RegexOptions.None)] [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)] [InlineData(RegexOptions.NonBacktracking)]
    public void SupplementaryCapturesAndHistoryRemainScalarRanges(RegexOptions options)
    {
        var regex = new Utf8Regex("(?<scalar>.)+", options: options | RegexOptions.CultureInvariant);
        var input = "é😀a"u8.ToArray();
        var slots = new Utf8CaptureRange[2];
        var actual = regex.Match(input, options == RegexOptions.RightToLeft ? input.Length : 0, slots);
        Assert.Equal((0, input.Length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
        Assert.Equal(actual, slots[0]);
        Assert.Equal(options == RegexOptions.RightToLeft ? (0, 2) : (6, 7),
            (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var history));
        Assert.True(history.TryGetGroup(1, out var last));
        Assert.Equal(last, slots[1]);
        Assert.Equal(options == RegexOptions.NonBacktracking ? 1 : 3, history.GetCaptures(1).Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(4096UL)]
    public void OrderedDiscoveryKeepsOrdinaryCaptureHistoryAndByteFloors(ulong? maxWorkingBytes)
    {
        const string pattern = "(?<piece>ab|ba|😀)+z";
        using var regex = new Utf8Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 4096, MaxWorkingBytes = maxWorkingBytes }
        });
        var prefix = "😀" + string.Concat(Enumerable.Repeat("ab", 160)) + " z! ";
        var start = Encoding.UTF8.GetByteCount(prefix);
        var input = Encoding.UTF8.GetBytes(prefix + "ab😀baz!");
        Assert.NotEmpty(regex.Inspection.AvailableOwnedSliceProgram!.NonBacktrackingOrderedClosures);
        Assert.True(regex.IsMatch(input));
        Assert.Equal(new Utf8CaptureRange(start, start + 9), regex.Match(input));

        var slots = new Utf8CaptureRange[2];
        Assert.Equal(new Utf8CaptureRange(start, start + 9), regex.Match(input, 4, slots));
        Assert.Equal(new Utf8CaptureRange(start + 6, start + 8), slots[1]);
        Assert.False(regex.Match(input, 4, slots, endOffsetInBytes: start + 8).Success);
        Assert.All(slots, range => Assert.False(range.Success));

        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(new Utf8CaptureRange(start, start + 9), match.Value);
        Assert.Equal(new Utf8CaptureRange[]
        {
            new(start, start + 2), new(start + 2, start + 6), new(start + 6, start + 8)
        }, match.GetCaptures(1).ToArray());
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(16_384)]
    public void OrderedFrontierObservesDeadlineDuringTraversalAndBeforePublication(int length)
    {
        using var regex = new Utf8Regex(@"[a-z]+ing|[a-z]+ed|[a-z]+ly", policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(50),
            ResourcePolicy = new() { MaxWorkSteps = 100_000, MaxWorkingBytes = 4096 }
        });
        var input = Encoding.UTF8.GetBytes(new string('a', length) + "ing");
        var output = new[] { new Utf8CaptureRange(0, 1) };
        var account = new Account();
        ulong lastCheckpoint = 0;
        using var clock = Utf8DeadlineTestClock.Start();
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            lastCheckpoint = steps;
            clock.Advance(50);
        });
        try
        {
            Assert.Throws<RegexMatchTimeoutException>(() =>
                regex.Match(input, 0, output, workingMemoryAllowance: account));
        }
        finally { session.Complete(); }
        Assert.False(output[0].Success);
        Assert.Equal(0UL, account.Held);
        Assert.InRange(lastCheckpoint, 1UL, 4096UL);
    }

    [Fact]
    public void OrderedSingleAtomRepeatsKeepCapturesAndRefundFundedSearch()
    {
        using var regex = new Utf8Regex("(?<left>[éö]+ed)|(?<right>[a-z]*ly)", policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(10),
            ResourcePolicy = new() { MaxDepth = 32, MaxWorkSteps = 4096, MaxWorkingBytes = 4096 }
        });
        var prefix = "ly 😀 " + new string('q', 320) + " ";
        var start = Encoding.UTF8.GetByteCount(prefix);
        var input = Encoding.UTF8.GetBytes(prefix + "éöed ly drolly");
        var slots = new Utf8CaptureRange[3];
        var account = new Account();
        using var source = new CancellationTokenSource();
        var floor = Encoding.UTF8.GetByteCount("ly 😀 ");
        var first = new Utf8CaptureRange(start, start + 6);
        Assert.Equal(first, regex.Match(input, floor, slots, source.Token, workingMemoryAllowance: account));
        Assert.Equal(first, slots[1]);
        Assert.False(slots[2].Success);
        Assert.Equal(0UL, account.Held);

        using (var cursor = regex.CreateMatchCursor(input, floor, source.Token, account))
        {
            Assert.True(cursor.MoveNext(out var retained));
            Assert.True(cursor.MoveNext(out var emptyRepeat));
            Assert.Equal(new Utf8CaptureRange(start + 7, start + 9), emptyRepeat.GetCaptures(2).ToArray().Single());
            Assert.True(cursor.MoveNext(out var nonemptyRepeat));
            Assert.Equal(new Utf8CaptureRange(start + 10, input.Length), nonemptyRepeat.Value);
            Assert.False(cursor.MoveNext(out _));
            Assert.Equal(first, retained.GetCaptures(1).ToArray().Single());
        }

        Assert.True(account.Peak > 0);
        Assert.Equal(0UL, account.Held);
        account.Maximum = 0;
        Assert.Throws<Utf8MemoryAdmissionException>(() =>
            regex.Match(input, floor, slots, source.Token, workingMemoryAllowance: account));
        Assert.Equal(0UL, account.Held);
        Assert.All(slots, range => Assert.False(range.Success));
    }

    [Fact]
    public void OrdinaryOrderedRangeCanUseOnlyTheOriginVectors()
    {
        const string hit = "ab😀baz";
        var prefix = string.Concat(Enumerable.Repeat("ab", 160)) + " z! ";
        var input = Encoding.UTF8.GetBytes(prefix + hit);
        using var regex = new Utf8Regex("(?:ab|ba|😀)+z", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 72, MaxWorkSteps = 4096 }
        });
        Assert.Equal(new Utf8CaptureRange(prefix.Length, input.Length), regex.Match(input));
        Assert.False(regex.Match(input.AsSpan(0, prefix.Length)).Success);
    }

    [Theory]
    [InlineData(2046, "")]
    [InlineData(2044, "è")]
    public void OrderedRightToLeftSearchUsesCompleteScalarsAcrossStarterBlocks(int padding, string otherLeadMatch)
    {
        const string hits = "aaé bb😀";
        var prefix = hits + otherLeadMatch + new string('q', padding);
        var ceiling = Encoding.UTF8.GetByteCount(prefix);
        var input = Encoding.UTF8.GetBytes(prefix + "bb😀");
        using var regex = new Utf8Regex("(a+é|b+😀)", RegexOptions.RightToLeft, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxDepth = 32, MaxWorkSteps = 256, MaxWorkingBytes = 4096 }
        });
        var slots = new Utf8CaptureRange[2];
        var account = new Account();
        using var source = new CancellationTokenSource();
        var expected = new Utf8CaptureRange(5, Encoding.UTF8.GetByteCount(hits));

        Assert.Equal(expected, regex.Match(input, ceiling, slots, source.Token, workingMemoryAllowance: account));
        Assert.Equal(expected, slots[1]);
        Assert.Equal(0UL, account.Held);
        Assert.Equal(2, regex.Count(input.AsSpan(0, ceiling), source.Token, workingMemoryAllowance: account));
        Assert.Equal(0UL, account.Held);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.RightToLeft)]
    public void OrderedGreedyOptionalPreservesBranchPriorityAndCaptures(RegexOptions options)
    {
        const string hits = "😀 z😀a";
        var gap = new string('q', 4098);
        var reverse = options == RegexOptions.RightToLeft;
        var input = Encoding.UTF8.GetBytes(reverse ? hits + gap : gap + hits);
        using var regex = new Utf8Regex("(?<first>😀a?)|(?<second>z+😀a)", options, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxDepth = 32, MaxWorkSteps = 256, MaxWorkingBytes = 4096 }
        });
        var expected = reverse
            ? new Utf8CaptureRange(Encoding.UTF8.GetByteCount("😀 z"), Encoding.UTF8.GetByteCount(hits))
            : new Utf8CaptureRange(gap.Length, gap.Length + Encoding.UTF8.GetByteCount("😀"));
        var account = new Account();
        using var source = new CancellationTokenSource();
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[3];

        Assert.Equal(expected, regex.Match(input, captures: slots, cancellationToken: source.Token, workingMemoryAllowance: account));
        Assert.Equal(expected, slots[1]);
        Assert.False(slots[2].Success);
        Assert.Equal(0UL, account.Held);
        Assert.Equal(2, regex.Count(input, source.Token, account));
        Assert.Equal(0UL, account.Held);
    }

    [Theory]
    [InlineData(RegexOptions.None, "é??")]
    [InlineData(RegexOptions.None, "é*?")]
    [InlineData(RegexOptions.None, "+?")]
    [InlineData(RegexOptions.RightToLeft, "é??")]
    [InlineData(RegexOptions.RightToLeft, "é*?")]
    [InlineData(RegexOptions.RightToLeft, "+?")]
    public void OrderedLazyScalarRepeatsPreferTheExitAndPublishItsCaptures(RegexOptions options, string repeat)
    {
        var reverse = options == RegexOptions.RightToLeft;
        var gap = new string('q', 4099);
        var hits = reverse ? "😀zéé" : "ééz😀";
        var input = Encoding.UTF8.GetBytes(reverse ? hits + gap : gap + hits);
        var alternative = reverse ? "z+é" : "éz+";
        using var regex = new Utf8Regex($"(?<first>é{repeat})|(?<second>{alternative})", options,
            new Utf8RegexPolicy
            {
                ResourcePolicy = new() { MaxDepth = 32, MaxWorkSteps = 256, MaxWorkingBytes = 4096 }
            });
        var start = reverse ? Encoding.UTF8.GetByteCount("😀zé") : gap.Length;
        var expected = new Utf8CaptureRange(start, start + Encoding.UTF8.GetByteCount("é"));
        var account = new Account();
        using var source = new CancellationTokenSource();
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[3];

        Assert.Equal(expected, regex.Match(input, captures: slots, cancellationToken: source.Token, workingMemoryAllowance: account));
        Assert.Equal(expected, slots[1]);
        Assert.False(slots[2].Success);
        Assert.Equal(0UL, account.Held);
        Assert.Equal(2, regex.Count(input, source.Token, account));
        Assert.Equal(0UL, account.Held);
    }

    [Theory]
    [InlineData("+?", 7, 8)]
    [InlineData("+", 4, 7)]
    public void OrderedCompoundRepeatReplaysThePreferredIterations(string repeat, int start, int pieceEnd)
    {
        var input = Encoding.UTF8.GetBytes("😀éaay!" + new string('q', 8192));
        using var regex = new Utf8Regex($"(?<piece>éa|a){repeat}(?<tail>[zy])|v[zy]",
            RegexOptions.RightToLeft | RegexOptions.Compiled, new Utf8RegexPolicy
            {
                ResourcePolicy = new() { MaxDepth = 32, MaxWorkSteps = 256, MaxWorkingBytes = 4096 }
            });
        using var source = new CancellationTokenSource();
        var account = new Account();
        var slots = new Utf8CaptureRange[3];
        var expected = new Utf8CaptureRange(start, 9);
        var piece = new Utf8CaptureRange(start, pieceEnd);
        Utf8CaptureRange[] history = repeat == "+" ? [new(7, 8), piece] : [piece];

        Assert.Equal(expected, regex.Match(input, captures: slots, cancellationToken: source.Token, workingMemoryAllowance: account));
        Assert.Equal(piece, slots[1]);
        Assert.Equal(new Utf8CaptureRange(8, 9), slots[2]);
        Assert.Equal(0UL, account.Held);

        using (var cursor = regex.CreateMatchCursor(input, cancellationToken: source.Token, workingMemoryAllowance: account))
        {
            Assert.True(cursor.MoveNext(out var match));
            Assert.Equal(expected, match.Value);
            Assert.Equal(history, match.GetCaptures(1).ToArray());
            Assert.False(cursor.MoveNext(out _));
        }
        Assert.Equal(0UL, account.Held);
    }

    [Theory]
    [InlineData("éa|a")]
    [InlineData("éa|")]
    public void CompoundLazyStarCanExitBeforeTakingAnIteration(string body)
    {
        var gap = new string('q', 260);
        var input = Encoding.UTF8.GetBytes(gap + "😀éay" + gap);
        using var regex = new Utf8Regex($"(?<piece>{body})*?(?<tail>[zy])|v[zy]", RegexOptions.RightToLeft);
        var slots = new Utf8CaptureRange[3];
        var expected = new Utf8CaptureRange(gap.Length + 7, gap.Length + 8);

        Assert.Equal(expected, regex.Match(input, captures: slots));
        Assert.False(slots[1].Success);
        Assert.Equal(expected, slots[2]);
        Assert.Equal(1, regex.Count(input));
    }

    [Fact]
    public void OrdinaryOrderedBooleanNeedsNoScratchLoanFromTheCaller()
    {
        var prefix = string.Concat(Enumerable.Repeat("ab", 160)) + " z! ";
        var input = Encoding.UTF8.GetBytes(prefix + "ab😀baz");
        var account = new Account { Maximum = 0 };
        using var regex = new Utf8Regex("(?:ab|ba|😀)+z", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1, MaxWorkSteps = 4096 }
        });
        using var source = new CancellationTokenSource();
        Assert.True(regex.IsMatch(input, source.Token, account));
        Assert.False(regex.IsMatch(input.AsSpan(0, prefix.Length), source.Token, account));
        Assert.Equal(0UL, account.Peak);
        Assert.Equal(0UL, account.Held);
    }

    [Fact]
    public void ByteBoundsAndCurrentInputPolicyAreEnforcedWithoutStaleOutput()
    {
        var regex = new Utf8Regex("(.)");
        var sentinel = regex.Match("a"u8, 0, []);
        foreach (var start in new[] { -1, 1, 3, 4, 5, 8, int.MaxValue })
        {
            var output = new[] { sentinel, sentinel, sentinel };
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("é😀a"u8, start, output));
            Assert.Equal("startOffsetInBytes", error.ParamName);
            Assert.All(output[..2], range => Assert.False(range.Success));
            Assert.Equal(sentinel, output[2]);
        }
        foreach (var end in new[] { -1, 1, 3, 4, 5, 8, int.MaxValue })
            Assert.Equal("endOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(() =>
                regex.Match("é😀a"u8, 0, [], endOffsetInBytes: end)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("abc"u8, 2, [], endOffsetInBytes: 1));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void DenialAndCancellationClearWrittenSlotsAndRefundWorkingLoans(int failure)
    {
        var policy = failure switch
        {
            0 => new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            1 => new Utf8ResourcePolicy { MaxWorkingBytes = 1 },
            _ => new Utf8ResourcePolicy { MaxDepth = 1 },
        };
        var regex = new Utf8Regex(@"(?=(?=(a)))a", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = policy
        });
        var sentinel = new Utf8Regex("a").Match("a"u8, 0, []);
        var output = new[] { sentinel, sentinel, sentinel };
        var account = new Account();
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            regex.Match("a"u8, 0, output, workingMemoryAllowance: account));
        Assert.Equal(failure switch { 0 => Utf8ResourceLimitKind.ExecutionWorkSteps,
            1 => Utf8ResourceLimitKind.ExecutionWorkingBytes, _ => Utf8ResourceLimitKind.ExecutionDepth }, error.ResourceKind);
        Assert.All(output[..2], range => Assert.False(range.Success));
        Assert.Equal(sentinel, output[2]);
        Assert.Equal(0UL, account.Held);
        using var source = new CancellationTokenSource();
        source.Cancel();
        output.AsSpan().Fill(sentinel);
        var canceled = Assert.Throws<OperationCanceledException>(() => regex.Match("a"u8, 0, output, source.Token));
        Assert.Equal(source.Token, canceled.CancellationToken);
        Assert.All(output, range => Assert.Equal(sentinel, range));
    }

    [Fact]
    public void MatchingUsesOriginalTokenAndAbsoluteDeadline()
    {
        var regex = new Utf8Regex("(a+)", policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(100)
        });
        var input = Encoding.UTF8.GetBytes(new string('a', 256));
        var output = new Utf8CaptureRange[2];
        using var source = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        try
        {
            var error = Assert.Throws<OperationCanceledException>(() => regex.Match(input, 0, output, source.Token));
            Assert.Equal(source.Token, error.CancellationToken);
        }
        finally { session.Complete(); }
        using var clock = Utf8DeadlineTestClock.Start();
        session = Utf8SearchDiagnosticsSession.Start(_ => clock.Advance(101));
        try
        {
            var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, 0, output));
            Assert.Equal(regex.Pattern, error.Pattern);
            Assert.Equal(regex.MatchTimeout, error.MatchTimeout);
            Assert.All(output, range => Assert.False(range.Success));
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void WorkingAdmissionRefundsOnSuccessAndRefusal()
    {
        var regex = new Utf8Regex(@"(?<word>\w+)");
        var output = new Utf8CaptureRange[2];
        var account = new Account();
        Assert.True(regex.Match("éabc"u8, 0, output, workingMemoryAllowance: account).Success);
        Assert.True(account.Peak > 0);
        Assert.Equal(0UL, account.Held);
        account.Maximum = account.Peak - 1;
        Assert.Throws<Utf8MemoryAdmissionException>(() =>
            regex.Match("éabc"u8, 0, output, workingMemoryAllowance: account));
        Assert.Equal(0UL, account.Held);
        Assert.All(output, range => Assert.False(range.Success));
    }

    [Fact]
    public void CompactReplayPreservesTheLastCapturesAfterALateByteStart()
    {
        using var regex = new Utf8Regex("(a?b|c)*(dé|😀)[xy]", RegexOptions.NonBacktracking, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 2048, MaxResultBytes = 1 }
        });
        var prefix = "😀y " + string.Concat(Enumerable.Repeat("aababc😀z é ", 64));
        var start = Encoding.UTF8.GetByteCount(prefix);
        var end = start + "abcc😀x"u8.Length;
        var input = Encoding.UTF8.GetBytes(prefix + "abcc😀x 😀y");
        var account = new Account();
        var output = new Utf8CaptureRange[3];
        var found = regex.Match(input, "😀y "u8.Length, output, workingMemoryAllowance: account, endOffsetInBytes: end);
        Assert.Equal(new Utf8CaptureRange(start, end), found);
        Assert.Equal(found, output[0]);
        Assert.Equal(new Utf8CaptureRange(start + 3, start + 4), output[1]);
        Assert.Equal(new Utf8CaptureRange(start + 4, end - 1), output[2]);
        Assert.Equal(0UL, account.Held);
    }

    [Fact]
    public void CompactReplayMissFitsAWorkingLimitBelowTheCaptureWorkspace()
    {
        using var regex = new Utf8Regex("(a?b|c)*(dé|😀)[xy]", RegexOptions.NonBacktracking, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 160 }
        });
        var prefix = string.Concat(Enumerable.Repeat("aababc😀z é ", 32));
        var account = new Account();
        var sentinel = new Utf8CaptureRange(123, 124);
        var output = new[] { sentinel, sentinel, sentinel, sentinel };
        Assert.False(regex.Match(Encoding.UTF8.GetBytes(prefix), captures: output, workingMemoryAllowance: account).Success);
        Assert.All(output[..3], range => Assert.False(range.Success));
        Assert.Equal(sentinel, output[3]);
        Assert.Equal(0UL, account.Held);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            regex.Match(Encoding.UTF8.GetBytes(prefix + "😀x"), captures: output, workingMemoryAllowance: account));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind);
        Assert.Equal(0UL, account.Held);
    }

    [Theory]
    [InlineData(2043)]
    [InlineData(2044)]
    [InlineData(2045)]
    public void CompactRegularStartsSkipGapsAndKeepAnActiveMatch(int gapBytes)
    {
        using var regex = new Utf8Regex("(?<x>😀a|b)+", RegexOptions.NonBacktracking, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 64 }
        });
        var text = "bé" + new string('z', gapBytes) + "💡😀ab!";
        var input = Encoding.UTF8.GetBytes(text);
        var floor = "bé"u8.Length;
        var start = floor + gapBytes + "💡"u8.Length;
        var end = start + "😀ab"u8.Length;
        var output = new Utf8CaptureRange[2];
        var account = new Account();

        Assert.Equal(new Utf8CaptureRange(start, end), regex.Match(input, floor, output, workingMemoryAllowance: account));
        Assert.Equal(new Utf8CaptureRange(end - 1, end), output[1]);
        Assert.False(regex.Match(input, floor, output, workingMemoryAllowance: account, endOffsetInBytes: start).Success);
        Assert.All(output, range => Assert.False(range.Success));
        Assert.Equal(0UL, account.Held);
    }

    [Theory]
    [InlineData("(?i:(?<x>😀|a)){1,2}", "", "😀A", "A")]
    [InlineData("(?i:(?<x>k)){1,2}", "", "Kk", "k")]
    [InlineData("(?i:(?<x>i)){1,2}", "tr-TR", "İi", "i")]
    [InlineData("(?i:(?<x>I)){1,2}", "tr-TR", "ıI", "I")]
    [InlineData("(?i:(?<x>𐐀)){1,2}", "", "𐐨𐐀", "𐐀")]
    public void CompactFoldedStartsRetainEveryCaseEquivalent(string pattern, string culture, string tail, string last)
    {
        const int floor = 4097;
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using var regex = new Utf8Regex(pattern, RegexOptions.NonBacktracking, new Utf8RegexPolicy
            {
                ResourcePolicy = new() { MaxWorkSteps = 64 }
            });
            var input = Encoding.UTF8.GetBytes(new string('z', floor) + tail);
            var slots = new Utf8CaptureRange[2];
            var account = new Account();

            Assert.Equal(new Utf8CaptureRange(floor, input.Length), regex.Match(input, captures: slots, workingMemoryAllowance: account));
            Assert.Equal(new Utf8CaptureRange(input.Length - Encoding.UTF8.GetByteCount(last), input.Length), slots[1]);
            using var cursor = regex.CreateMatchCursor(input, workingMemoryAllowance: account);
            Assert.True(cursor.MoveNext(out var first));
            Assert.Equal(slots[0], first.Value);
            Assert.Equal(slots[1], Assert.Single(first.GetCaptures(1).ToArray()));
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void PackedCaptureReplayUsesBoundedScratchAtTheByteThreshold(int asciiCount, bool fits)
    {
        using var regex = new Utf8Regex("(?<x>😀|a)+b", RegexOptions.NonBacktracking, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 640 }
        });
        var subject = "é " + string.Concat(Enumerable.Repeat("😀", 7)) + new string('a', asciiCount) + "b" + new string('z', 64);
        var input = Encoding.UTF8.GetBytes(subject);
        var start = "é "u8.Length;
        var groupEnd = start + 28 + asciiCount;
        var slots = new Utf8CaptureRange[2];
        var account = new Account();

        if (fits)
        {
            Assert.Equal(new Utf8CaptureRange(start, groupEnd + 1), regex.Match(input, start, slots, workingMemoryAllowance: account));
            Assert.Equal(new Utf8CaptureRange(groupEnd - 1, groupEnd), slots[1]);
        }
        else
        {
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                regex.Match(input, start, slots, workingMemoryAllowance: account));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind);
            Assert.All(slots, range => Assert.False(range.Success));
        }
        Assert.Equal(0UL, account.Held);
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Held;
        internal ulong Peak;
        internal ulong Maximum = ulong.MaxValue;
        public bool TryReserve(ulong bytes)
        {
            if (bytes > Maximum - Held) return false;
            Held = checked(Held + bytes);
            Peak = Math.Max(Peak, Held);
            return true;
        }
        public void Release(ulong bytes) { Assert.True(bytes <= Held); Held -= bytes; }
    }
}
