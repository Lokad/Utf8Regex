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
        var regex = new Utf8Regex(captured ? "(?<x>.)+b" : ".+b", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();
        var states = program.Instructions.Length;
        var registers = 2 * program.GroupCount + program.NonBacktrackingCaptureOpeningCount;
        static ulong Extent(int length, int width) => length == 0 ? 0 : 32UL + ((ulong)length * (ulong)width + 15) / 16 * 16;
        var expected = 5 * Extent(states, 4) + 3 * Extent(states * registers, 4)
            + 2 * Extent(registers, 4) + Extent(states, 1);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var outcome = Utf8SliceRunner.TryFindFirst(program, "😀😀b"u8, 0,
            new int[program.GroupCount], new int[program.GroupCount], false, ref invocation);
        Assert.True(outcome.Found);
        Assert.Equal((0, 9), (outcome.StartByte, outcome.EndByte));
        Assert.Equal(expected + 13, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
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
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.Inspection.AvailableOwnedSliceProgram?.IsNonBacktracking);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        Assert.Equal(oracle.Count(text), regex.Count(input));
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);
        Assert.Equal(expected.Success, actual.Success);
        if (actual.Success)
        {
            Assert.Equal((expected.Index, expected.Length), (actual.IndexInUtf16, actual.LengthInUtf16));
            foreach (var number in oracle.GetGroupNumbers())
            {
                var wanted = expected.Groups[number];
                var found = actual.GetGroup(number);
                Assert.Equal(wanted.Success, found.Success);
                Assert.Equal(wanted.Captures.Count, found.CaptureCount);
                if (found.Success) Assert.Equal(wanted.Value, found.GetValueString());
            }
        }
        var ranges = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(input)) ranges.Add((match.IndexInUtf16, match.LengthInUtf16));
        Assert.Equal(oracle.Matches(text).Select(match => (match.Index, match.Length)), ranges);
        Assert.Equal(oracle.Replace(text, "<$0:$1>"), regex.ReplaceToString(input, "<$0:$1>"));
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
        var regex = new Utf8Regex(pattern, options | RegexOptions.NonBacktracking);
        var group = regex.MatchDetailed("abc"u8).GetGroup("x");
        Assert.Equal("abc", group.GetValueString());
        Assert.Equal(1, group.CaptureCount);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SupplementaryLoopsAndEmptyProgressUseScalars(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var input = "a😀😀b"u8.ToArray();
        var regex = new Utf8Regex("(?<x>😀)+", options);
        Assert.Equal(1, regex.Count(input));
        var match = regex.MatchDetailed(input);
        Assert.Equal((1, 8), (match.IndexInBytes, match.LengthInBytes));
        var group = match.GetGroup("x");
        Assert.Equal(1, group.CaptureCount);
        Assert.Equal((5, 4), (group.IndexInBytes, group.LengthInBytes));
        Assert.Equal("😀", group.GetValueString());
        Assert.Equal("aQb", regex.ReplaceToString(input, "Q"));
        var fromOffset = regex.MatchDetailedFromUtf16Offset(input, 3);
        Assert.Equal((5, 4), (fromOffset.IndexInBytes, fromOffset.LengthInBytes));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(new Utf8Regex("^.$", options).IsMatch("😀"u8));
        Assert.False(new Utf8Regex("^..$", options).IsMatch("😀"u8));
        Assert.Equal(2, new Utf8Regex(".*?", options).Count("😀"u8));
        var dots = new Utf8Regex("(.)", options);
        var ranges = new List<(int, int)>();
        foreach (var item in dots.EnumerateMatches(input)) ranges.Add((item.IndexInBytes, item.LengthInBytes));
        Assert.Equal(new[] { (0, 1), (1, 4), (5, 4), (9, 1) }, ranges);
        Assert.True(dots.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData("(a+)+b")] [InlineData("(?:a|aa)+b")]
    [InlineData("a.*b")] [InlineData("(?<x>a+)(?<y>b)")]
    public void UnanchoredMissWorkGrowsLinearly(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("NFA missing.");
        ulong previous = 0;
        foreach (var length in new[] { 512, 1024, 2048, 4096 })
        {
            var input = Encoding.UTF8.GetBytes(new string('a', length));
            var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, 0, new int[program.GroupCount], new int[program.GroupCount], true, ref invocation);
            Assert.False(outcome.Found);
            Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
            if (previous != 0) Assert.InRange(invocation.Budget.Steps, previous, previous * 2 + 64);
            previous = invocation.Budget.Steps;
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WorkStorageAndCancellationFailuresReleaseWorkspace(bool histories)
    {
        var regex = new Utf8Regex("(?<x>.)+b", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("NFA missing.");
        var input = "😀😀b"u8.ToArray();
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var measured = Utf8SliceRunner.TryFindFirst(program, input, 0, new int[1], new int[1], histories, ref baseline);
        Assert.True(measured.Found);
        Utf8SliceRunner.ReleaseHistory(measured.CaptureStarts, measured.CaptureLengths, ref baseline);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong steps = 1; steps < baseline.Budget.Steps; steps++)
        {
            var invocation = new Utf8SliceInvocation(new(steps, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var starts = new[] { 42 }; var ends = new[] { 43 };
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, 0, starts, ends, histories, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            Assert.Equal(-1, starts[0]); Assert.Equal(-1, ends[0]);
        }
        for (ulong cap = 13; cap <= baseline.Budget.PeakWorkingBytes; cap++)
        {
            var storage = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(storage.Ledger.TryReserve(13));
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, 0, new int[1], new int[1], histories, ref storage);
            Assert.Equal(cap == baseline.Budget.PeakWorkingBytes ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, outcome.Breach);
            Utf8SliceRunner.ReleaseHistory(outcome.CaptureStarts, outcome.CaptureLengths, ref storage);
            Assert.Equal(13UL, storage.Ledger.ReservedBytes);
        }
        using var source = new CancellationTokenSource();
        var cancelled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(cancelled.Ledger.TryReserve(13));
        cancelled.CheckpointHook = _ => source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.TryFindFirst(program, Encoding.UTF8.GetBytes(new string('a', 4096)), 0,
                new int[1], new int[1], histories, ref cancelled));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, cancelled.Ledger.ReservedBytes);
    }

    [Fact]
    public void CancellationAtTheFinalHistoryCheckpointReleasesUnpublishedCaptures()
    {
        var regex = new Utf8Regex("(a)", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("NFA missing.");
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        var measured = Utf8SliceRunner.TryFindFirst(program, "a"u8, 0, new int[1], new int[1], true, ref baseline);
        Assert.True(measured.Found);
        Utf8SliceRunner.ReleaseHistory(measured.CaptureStarts, measured.CaptureLengths, ref baseline);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var padding = (64 - baseline.Budget.Steps % 64) % 64;
        for (ulong step = 0; step < padding; step++) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeStep());
        var finalStep = baseline.Budget.Steps + padding;
        invocation.CheckpointHook = steps => { if (steps == finalStep) source.Cancel(); };
        var starts = new int[1]; var ends = new int[1];
        var error = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.TryFindFirst(program, "a"u8, 0, starts, ends, true, ref invocation));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(-1, starts[0]); Assert.Equal(-1, ends[0]);
    }
}
