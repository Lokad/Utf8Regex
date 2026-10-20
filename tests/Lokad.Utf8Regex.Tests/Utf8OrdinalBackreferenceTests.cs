using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OrdinalBackreferenceTests
{
    private static string Pattern(int scalars, bool rtl) => rtl
        ? $@"\A\k<g>(?<g>.{{{scalars}}})\z"
        : $@"\A(?<g>.{{{scalars}}})\k<g>\z";

    private static Utf8ResourcePolicy Limits(ulong work = 20000000) => new()
        { MaxWorkSteps = work, MaxDepth = 1024, MaxWorkingBytes = 64 << 20, MaxResultBytes = 64 << 20 };

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void AlignedCapturesKeepByteCoordinatesAcrossComparisonChunks(bool rtl, bool compiled)
    {
        foreach (var segment in new[] { "", "a", new string('a', 2047), new string('a', 2048),
            new string('a', 2049), new string('a', 4097), new string('a', 2047) + "😀",
            string.Concat(Enumerable.Repeat("😀é", 1025)) })
        {
            var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0) |
                (compiled ? RegexOptions.Compiled : 0);
            var regex = new Utf8Regex(Pattern(segment.EnumerateRunes().Count(), rtl), options,
                Regex.InfiniteMatchTimeout, Limits());
            var bytes = Encoding.UTF8.GetBytes(segment + segment);
            byte[] storage = [255, 255, .. bytes, 255];
            var input = storage.AsSpan(2, bytes.Length);
            Assert.Equal(1, regex.Count(input));
            var retained = regex.MatchDetailed(input);
            Assert.True(retained.Success);
            Assert.Equal((0, bytes.Length), (retained.IndexInBytes, retained.LengthInBytes));
            var group = retained.GetGroup(1);
            Assert.True(group.Success);
            Assert.Equal((rtl ? bytes.Length / 2 : 0, bytes.Length / 2),
                (group.IndexInBytes, group.LengthInBytes));
            Assert.Equal(segment, group.GetValueString());
            var cursor = regex.EnumerateMatches(input);
            Assert.True(cursor.MoveNext());
            var value = cursor.Current;
            Assert.False(cursor.MoveNext());
            Assert.Equal((0, bytes.Length), (value.IndexInBytes, value.LengthInBytes));
            using var source = new CancellationTokenSource();
            source.Cancel();
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Count(bytes, source.Token)).CancellationToken);
            Assert.Throws<ArgumentException>(() => regex.Count([.. bytes, 255]));
            Assert.Equal(segment, retained.GetGroup(1).GetValueString());
            Assert.Equal(1, regex.Count(input));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FirstAndLastDifferencesFailWithoutPartialMatches(bool rtl)
    {
        foreach (var length in new[] { 1, 2047, 2048, 2049, 8193 })
        {
            var segment = new string('a', length);
            var regex = new Utf8Regex(Pattern(length, rtl),
                RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0),
                Regex.InfiniteMatchTimeout, Limits());
            foreach (var changed in new[] { "b" + segment[1..], segment[..^1] + "b" })
            {
                var input = Encoding.UTF8.GetBytes(rtl ? changed + segment : segment + changed);
                Assert.Equal(0, regex.Count(input));
                Assert.False(regex.MatchDetailed(input).Success);
                Assert.False(regex.EnumerateMatches(input).MoveNext());
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FoldedReferencesKeepDifferentScalarWidths(bool rtl)
    {
        var captured = new string('k', 2049);
        var reference = new string('K', 2049);
        var input = Encoding.UTF8.GetBytes(rtl ? reference + captured : captured + reference);
        var regex = new Utf8Regex(Pattern(2049, rtl), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase |
            (rtl ? RegexOptions.RightToLeft : 0), Regex.InfiniteMatchTimeout, Limits());
        var match = regex.MatchDetailed(input);
        Assert.True(match.Success);
        Assert.Equal(input.Length, match.LengthInBytes);
        Assert.Equal(captured, match.GetGroup(1).GetValueString());
        Assert.Equal(1, regex.Count(input));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryWorkCutoffKeepsCallerReservationAndNoPartialCount(bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex(Pattern(64, rtl), options);
        var program = regex.Inspection.PreparedRegex.ScalarProgram!;
        var input = Encoding.UTF8.GetBytes(new string('a', 128));
        var funded = new Utf8SliceInvocation(new(ulong.MaxValue, 1024, 1 << 20, 1 << 20),
            Regex.InfiniteMatchTimeout, default);
        Assert.True(funded.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.Count(program, input, new int[program.GroupCount],
            new int[program.GroupCount], ref funded);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(1, result.Count);
        Assert.Equal(13UL, funded.Ledger.ReservedBytes);
        for (ulong cap = 1; cap < funded.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 1024, 1 << 20, 1 << 20),
                Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var denied = Utf8SliceRunner.Count(program, input, new int[program.GroupCount],
                new int[program.GroupCount], ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            // Internal progress is invalid after denial. The public operation
            // must throw instead of publishing that partial count.
            var bounded = new Utf8Regex(Pattern(64, rtl), options, Regex.InfiniteMatchTimeout, Limits(cap));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => bounded.Count(input)).ResourceKind);
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void CompletedCaptureComparisonObservesOriginalAuthority(bool rtl, bool expire)
    {
        const int length = 600000;
        var regex = new Utf8Regex(Pattern(length, rtl), RegexOptions.CultureInvariant |
            (rtl ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.PreparedRegex.ScalarProgram!;
        var input = Encoding.UTF8.GetBytes(new string('a', length * 2));
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(20000000, 1024, 64 << 20, 64 << 20),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var entered = false;
        ulong completedAt = 0;
        invocation.CheckpointHook = step =>
        {
            if (starts[0] < 0 || ends[0] < 0) return;
            if (completedAt == 0) { completedAt = step; return; }
            if (step < completedAt + 64) return;
            entered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        };
        Exception? observed = null;
        try
        {
            _ = Utf8SliceRunner.TryMatchAt(program, input, rtl ? input.Length : 0,
                starts, ends, true, ref invocation);
        }
        catch (Exception error) { observed = error; }
        Assert.True(entered);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(observed);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
