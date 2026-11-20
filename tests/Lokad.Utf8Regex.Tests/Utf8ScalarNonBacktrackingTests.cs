using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarNonBacktrackingTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WorkspacePeakIncludesEveryCompleteStateVector(bool captured)
    {
        var regex = new Utf8Regex(captured ? "(?<x>.)+b" : "^.{15}b", options: RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();
        var states = program.Instructions.Length;
        var registers = 2 * program.GroupCount + program.NonBacktrackingCaptureOpeningCount;
        static ulong Extent(int length, int width) => length == 0 ? 0 : 32UL + ((ulong)length * (ulong)width + 15) / 16 * 16;
        var expected = 5 * Extent(states, 4) + 3 * Extent(states * registers, 4)
            + 2 * Extent(registers, 4) + Extent(states, 1);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var input = captured ? "😀😀b"u8 : "😀abcdefghijklmnb"u8;
        var outcome = Utf8SliceRunner.TryFindFirst(program, input, 0,
            new int[program.GroupCount], new int[program.GroupCount], false, ref invocation);
        Assert.True(outcome.Found);
        Assert.Equal((0, input.Length), (outcome.StartByte, outcome.EndByte));
        Assert.Equal(expected + 13, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("(ab|a)*", 7)]
    [InlineData("(a|ab)*", 5)]
    [InlineData("(a|ab)*?", 4)]
    public void CompactOrderedRangePreservesPriorityFromAByteFloor(string pattern, int expectedEnd)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.NonBacktracking);
        var input = "😀aba!"u8;
        var range = regex.Match(input, startOffsetInBytes: 4, endOffsetInBytes: 7);
        Assert.True(range.Success);
        Assert.Equal((4, expectedEnd), (range.StartOffsetInBytes, range.EndOffsetInBytes));
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        Assert.Equal(range, regex.Match(input, 4, captures, endOffsetInBytes: 7));
    }

    [Theory]
    [InlineData("a{0,13}b", 13, true)]
    [InlineData("a{0,14}b", 14, false)]
    public void OverlappingStartsKeepPriorityAtTheOrderedLeafCapacity(string pattern, int maximum, bool compact)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.NonBacktracking);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(compact, program.NonBacktrackingOrderedClosures.Length != 0);
        var input = Encoding.UTF8.GetBytes(new string('a', 96) + "b");
        var expected = new Utf8CaptureRange(96 - maximum, 97);
        Assert.Equal(expected, regex.Match(input));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var result));
        Assert.Equal(expected, result.Value);
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData(4, false, false)]
    [InlineData(4, true, false)]
    [InlineData(4, false, true)]
    [InlineData(40, false, true)]
    public void OrderedCaptureReplayUsesOriginalWordNeighbors(int length, bool wordBefore, bool wordAfter)
    {
        const string pattern = @"\b([ab]+)\b|([ab]+)";
        var prefix = wordBefore ? "é" : "!";
        var subject = prefix + new string('a', length) + (wordAfter ? "é" : "!") + new string('!', 80);
        var input = Encoding.UTF8.GetBytes(subject);
        var start = Encoding.UTF8.GetByteCount(prefix);
        var expected = new Utf8CaptureRange(start, start + length);
        var group = wordBefore || wordAfter ? 2 : 1;
        using var regex = new Utf8Regex(pattern, RegexOptions.NonBacktracking);
        Assert.NotEmpty(regex.Inspection.AvailableOwnedSliceProgram!.NonBacktrackingOrderedClosures);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[3];
        Assert.Equal(expected, regex.Match(input, start, captures));
        Assert.Equal(expected, captures[group]);
        Assert.False(captures[3 - group].Success);

        using var cursor = regex.CreateMatchCursor(input, startOffsetInBytes: start);
        Assert.True(cursor.MoveNext(out var first));
        Assert.Equal(expected, first.Value);
        Assert.True(first.TryGetGroup(group, out var found));
        Assert.Equal(expected, found);
        Assert.False(first.TryGetGroup(3 - group, out var unmatched) && unmatched.Success);
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData("[α-ω][α-ω]|[ࠀ-ࠇ][ࠀ-ࠇ]", "Ͽ", "λω")]
    [InlineData("[😀-😇][😀-😇]|[ࠀ-ࠇ][ࠀ-ࠇ]", "😈", "😀😇")]
    [InlineData(@"\b\d{1,2}q\b", "é", "𝟙q")]
    public void CompactOrderedRangeSkipsToPossibleUnicodeStarters(string pattern, string near, string value)
    {
        var prefix = new string('!', 6143) + near + near + new string('!', 2048);
        var input = Encoding.UTF8.GetBytes(prefix + value + "!");
        var start = Encoding.UTF8.GetByteCount(prefix);
        using var regex = new Utf8Regex(pattern, RegexOptions.NonBacktracking, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 128, MaxWorkingBytes = 4096 }
        });
        using var tokenSource = new CancellationTokenSource();
        Assert.True(regex.IsMatch(input, tokenSource.Token));
        var range = regex.Match(input, startOffsetInBytes: 257, cancellationToken: tokenSource.Token);
        Assert.Equal(new Utf8CaptureRange(start, input.Length - 1), range);
    }

    public static IEnumerable<object[]> OverlapCases()
    {
        (string Pattern, string Text)[] cases =
        [
            ("(a+)+b", "xxaaab aaaab"),
            ("(a|ab)*", "aab"), ("(ab|a)*", "aab"),
            ("a*?", "aab"), ("a*", "aab"),
            ("(?<x>a){2,4}a", "aaaaa"), ("(?<x>a){2,4}?a", "aaaaa"),
            ("(a*)*", ""), ("(a*)*", "aaa"), ("(a?)*", "aa"),
            ("(?<x>a)(?<x>b)", "ab"),
            ("(?<100>a)+(?<x>b)?", "aa"),
            ("(?<x>a)*b|(?<y>a)+c", "aaac"),
            ("a*b|", "aaa"), ("a*b|", "aaab"),
            ("^|(?<x>.)+$", "\nabc"),
            ("[a-z-[aeiou]]+", "xbcdyez"),
            ("[\\p{L}\\d]+", "é١!"), ("\\b\\w+\\b", "éa!"),
            ("a{0,3}?ab|b+", "aaabb"),
            ("(a?){2,4}b", "aab"), ("(a?){2,4}?b", "aab"),
            ("(?:(?<x>a)|(?<y>b))+(?<z>c)?", "abbac"),
        ];
        foreach (var row in cases)
            foreach (var compiled in new[] { false, true })
                yield return [row.Pattern, row.Text, compiled];
    }

    [Theory]
    [MemberData(nameof(OverlapCases))]
    public void OrderedRegularGraphsPreserveRangesLastCapturesAndOutputs(string pattern, string text, bool compiled)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.Inspection.AvailableOwnedSliceProgram?.IsNonBacktracking);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        Assert.Equal(oracle.Count(text), regex.Count(input));
        var expected = oracle.Match(text);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(expected.Success, actual.Success);
        if (actual.Success)
        {
            foreach (var number in oracle.GetGroupNumbers())
            {
                var wanted = expected.Groups[number];
                Assert.True(actual.TryGetGroup(number, out var found));
                Assert.Equal(wanted.Success, found.Success);
                Assert.Equal(wanted.Captures.Count, actual.GetCaptures(number).Length);
                if (found.Success) Assert.Equal(wanted.Value, System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(number).Span));
            }
        }
        var ranges = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(input)) ranges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.Equal(oracle.Matches(text).Select(match => (System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(match.Index, match.Length)))), ranges);
        Assert.Equal(oracle.Replace(text, "<$0:$1>"), Encoding.UTF8.GetString(regex.Replace(input, "<$0:$1>"u8)));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NestedSharedNamesRetainTheirOwnOpeningBeforeLastCompletion(bool compiled)
    {
        const string pattern = "(?<x>a(?<x>b)c)";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        // .NET 10 NonBacktracking overwrites the outer opening with the inner
        // one and reports "bc". Keep the correct structural capture "abc",
        // shared with ordinary owned execution; do not reproduce that defect.
        Assert.Equal("abc", new Regex(pattern, options).Match("abc").Groups["x"].Value);
        var regex = new Utf8Regex(pattern, options: options | RegexOptions.NonBacktracking);
        Assert.True(Utf8MatchSequence.First(regex.CreateMatchCursor("abc"u8.ToArray())).TryGetGroup(1, out var group));
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor("abc"u8.ToArray())).GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
        Assert.Equal(1, Utf8MatchSequence.First(regex.CreateMatchCursor("abc"u8.ToArray())).GetCaptures(regex.GroupNumberFromUtf8Name("x"u8)).Length);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SupplementaryLoopsAndEmptyProgressUseScalars(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var input = "a😀😀b"u8.ToArray();
        var regex = new Utf8Regex("(?<x>😀)+", options: options);
        Assert.Equal(1, regex.Count(input));
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal((1, 8), (match.Value.StartOffsetInBytes, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes)));
        Assert.True(match.TryGetGroup(1, out var group));
        Assert.Equal(1, match.GetCaptures(regex.GroupNumberFromUtf8Name("x"u8)).Length);
        Assert.Equal((5, 4), (group.StartOffsetInBytes, (group.EndOffsetInBytes - group.StartOffsetInBytes)));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
        Assert.Equal("aQb", Encoding.UTF8.GetString(regex.Replace(input, "Q"u8)));
        using var cursor = regex.CreateMatchCursor(input, 5);
        Assert.True(cursor.MoveNext(out var fromOffset));
        Assert.Equal((5, 9), (fromOffset.Value.StartOffsetInBytes, fromOffset.Value.EndOffsetInBytes));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(new Utf8Regex("^.$", options: options).IsMatch("😀"u8));
        Assert.False(new Utf8Regex("^..$", options: options).IsMatch("😀"u8));
        Assert.Equal(2, new Utf8Regex(".*?", options: options).Count("😀"u8));
        var dots = new Utf8Regex("(.)", options: options);
        var ranges = new List<(int, int)>();
        foreach (var item in dots.EnumerateMatches(input)) ranges.Add((item.StartOffsetInBytes, (item.EndOffsetInBytes - item.StartOffsetInBytes)));
        Assert.Equal(new[] { (0, 1), (1, 4), (5, 4), (9, 1) }, ranges);
        Assert.True(dots.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData("(a+)+b")] [InlineData("(?:a|aa)+b")]
    [InlineData("a.*b")] [InlineData("(?<x>a+)(?<y>b)")]
    public void UnanchoredMissWorkGrowsLinearly(string pattern)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("NFA missing.");
        ulong previous = 0;
        foreach (var length in new[] { 512, 1024, 2048, 4096 })
        {
            var input = Encoding.UTF8.GetBytes(new string('a', length));
            var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, 0, new int[program.GroupCount], new int[program.GroupCount], false, ref invocation);
            Assert.False(outcome.Found);
            Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
            if (previous != 0) Assert.InRange(invocation.Budget.Steps, previous, previous * 2 + 64);
            previous = invocation.Budget.Steps;
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void WorkStorageAndCancellationFailuresReleaseWorkspace()
    {
        var regex = new Utf8Regex("(?<x>.)+b", options: RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("NFA missing.");
        var input = "😀😀b"u8.ToArray();
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var measured = Utf8SliceRunner.TryFindFirst(program, input, 0, new int[1], new int[1], false, ref baseline);
        Assert.True(measured.Found);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong steps = 1; steps < baseline.Budget.Steps; steps++)
        {
            var invocation = new Utf8SliceInvocation(new(steps, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var starts = new[] { 42 }; var ends = new[] { 43 };
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, 0, starts, ends, false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            Assert.Equal(-1, starts[0]); Assert.Equal(-1, ends[0]);
        }
        for (ulong cap = 13; cap <= baseline.Budget.PeakWorkingBytes; cap++)
        {
            var storage = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(storage.Ledger.TryReserve(13));
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, 0, new int[1], new int[1], false, ref storage);
            Assert.Equal(cap == baseline.Budget.PeakWorkingBytes ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, outcome.Breach);
            Assert.Equal(13UL, storage.Ledger.ReservedBytes);
        }
        using var source = new CancellationTokenSource();
        var cancelled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(cancelled.Ledger.TryReserve(13));
        cancelled.CheckpointHook = _ => source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.TryFindFirst(program, Encoding.UTF8.GetBytes(new string('a', 4096)), 0,
                new int[1], new int[1], false, ref cancelled));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, cancelled.Ledger.ReservedBytes);
    }

    [Fact]
    public void CancellationAtTheFinalProjectionCheckpointClearsUnpublishedSlots()
    {
        var regex = new Utf8Regex("(a)", options: RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("NFA missing.");
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        var measured = Utf8SliceRunner.TryFindFirst(program, "a"u8, 0, new int[1], new int[1], false, ref baseline);
        Assert.True(measured.Found);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var padding = (64 - baseline.Budget.Steps % 64) % 64;
        for (ulong step = 0; step < padding; step++) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeStep());
        var finalStep = baseline.Budget.Steps + padding;
        invocation.CheckpointHook = steps => { if (steps == finalStep) source.Cancel(); };
        var starts = new int[1]; var ends = new int[1];
        var error = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.TryFindFirst(program, "a"u8, 0, starts, ends, false, ref invocation));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(-1, starts[0]); Assert.Equal(-1, ends[0]);
    }
}
