using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OrdinalBackreferenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdjacentScalarsStillRequireTheClassAndTailAndPublishCaptures(bool compiled)
    {
        var regex = new Utf8Regex(@"(.)\1!", options: compiled ? RegexOptions.Compiled : RegexOptions.None,
            policy: new Utf8RegexPolicy { MatchTimeout = Regex.InfiniteMatchTimeout, ResourcePolicy = Limits() });
        var prefix = "aéê中世😀😃éé?"u8;
        byte[] input = [.. prefix, .. "世世!😀😀!aa!"u8];
        Assert.True(regex.IsMatch(input));
        Assert.Equal(3, regex.Count(input));
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
        Assert.True(regex.Match(input, 1, slots).Success);
        Assert.Equal((prefix.Length, prefix.Length + 7), (slots[0].StartOffsetInBytes, slots[0].EndOffsetInBytes));
        Assert.Equal((prefix.Length, prefix.Length + 3), (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));
        Assert.True(regex.Match(input, prefix.Length + 3, slots).Success);
        Assert.Equal((prefix.Length + 7, prefix.Length + 11), (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));

        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var retained));
        Assert.True(cursor.MoveNext(out _));
        Assert.True(cursor.MoveNext(out _));
        Assert.False(cursor.MoveNext(out _));
        Assert.True(retained.GetGroupMemory(1).Span.SequenceEqual("世"u8));

        var word = new Utf8Regex(@"(\w)\1");
        Assert.False(word.IsMatch("!! 😀😀 éc"u8));
        Assert.Equal(1, word.Count("!! 😀😀 éé"u8));
        var folded = new Utf8Regex(@"(.)\1", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(folded.IsMatch("kK"u8));
    }

    [Fact]
    public void RejectedScalarPairsSpendWorkAndKeepTheOriginalToken()
    {
        var regex = new Utf8Regex(@"(\w)\1", policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Limits(4)
        });
        var input = "aéê中世b"u8.ToArray();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input)).ResourceKind);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count(input)).ResourceKind);
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Equal(source.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, source.Token)).CancellationToken);
    }

    private static string Pattern(int scalars, bool rtl) => rtl
        ? $@"\A\k<g>(?<g>.{{{scalars}}})\z"
        : $@"\A(?<g>.{{{scalars}}})\k<g>\z";

    private static Utf8ResourcePolicy Limits(ulong work = 20000000) => new()
    { MaxWorkSteps = work, MaxDepth = 1024, MaxWorkingBytes = 64 << 20, MaxResultBytes = 64 << 20 };

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AlignedCapturesKeepByteCoordinatesAcrossComparisonChunks(bool rtl, bool compiled)
    {
        foreach (var segment in new[] { "", "a", new string('a', 2047), new string('a', 2048),
            new string('a', 2049), new string('a', 4097), new string('a', 2047) + "😀",
            string.Concat(Enumerable.Repeat("😀é", 1025)) })
        {
            var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0) |
                (compiled ? RegexOptions.Compiled : 0);
            var regex = new Utf8Regex(Pattern(segment.EnumerateRunes().Count(), rtl), options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Limits()
            });
            var bytes = Encoding.UTF8.GetBytes(segment + segment);
            byte[] storage = [255, 255, .. bytes, 255];
            var input = storage.AsSpan(2, bytes.Length);
            Assert.Equal(1, regex.Count(input));
            var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(((global::System.ReadOnlySpan<byte>)input).ToArray()));
            Assert.True(retained.Success);
            Assert.Equal((0, bytes.Length), (retained.Value.StartOffsetInBytes, (retained.Value.EndOffsetInBytes - retained.Value.StartOffsetInBytes)));
            Assert.True(retained.TryGetGroup(1, out var group));
            Assert.True(group.Success);
            Assert.Equal((rtl ? bytes.Length / 2 : 0, bytes.Length / 2),
                (group.StartOffsetInBytes, (group.EndOffsetInBytes - group.StartOffsetInBytes)));
            Assert.Equal(segment, System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
            var cursor = regex.EnumerateMatches(input);
            Assert.True(cursor.MoveNext());
            var value = cursor.Current;
            Assert.False(cursor.MoveNext());
            Assert.Equal((0, bytes.Length), (value.StartOffsetInBytes, (value.EndOffsetInBytes - value.StartOffsetInBytes)));
            using var source = new CancellationTokenSource();
            source.Cancel();
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Count(bytes, source.Token)).CancellationToken);
            Assert.Equal(segment, System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
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
            var regex = new Utf8Regex(Pattern(length, rtl), options:                 RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0), policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Limits()
            });
            foreach (var changed in new[] { "b" + segment[1..], segment[..^1] + "b" })
            {
                var input = Encoding.UTF8.GetBytes(rtl ? changed + segment : segment + changed);
                Assert.Equal(0, regex.Count(input));
                Assert.False(Utf8MatchSequence.First(regex.CreateMatchCursor(input)).Success);
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
        var regex = new Utf8Regex(Pattern(2049, rtl), options: RegexOptions.CultureInvariant | RegexOptions.IgnoreCase |
            (rtl ? RegexOptions.RightToLeft : 0), policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Limits()
            });
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(match.Success);
        Assert.Equal(input.Length, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes));
        Assert.Equal(captured, System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal(1, regex.Count(input));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryWorkCutoffKeepsCallerReservationAndNoPartialCount(bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex(Pattern(64, rtl), options: options);
        var program = regex.Inspection.PreparedRegex.ScalarProgram!;
        var input = Encoding.UTF8.GetBytes(new string('a', 128));
        var start = rtl ? input.Length : 0;
        var funded = new Utf8SliceInvocation(new(ulong.MaxValue, 1024, 1 << 20, 1 << 20),
            Regex.InfiniteMatchTimeout, default);
        Assert.True(funded.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, start, new int[program.GroupCount],
            new int[program.GroupCount], ref funded);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(1, result.Count);
        Assert.Equal(13UL, funded.Ledger.ReservedBytes);
        for (ulong cap = 1; cap < funded.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 1024, 1 << 20, 1 << 20),
                Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var denied = Utf8SliceRunner.CountFromBytes(program, input, start, new int[program.GroupCount],
                new int[program.GroupCount], ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            // Internal progress is invalid after denial. The public operation
            // must throw instead of publishing that partial count.
            var bounded = new Utf8Regex(Pattern(64, rtl), options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Limits(cap)
            });
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => bounded.Count(input)).ResourceKind);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NeighborRejectionChecksDeadlineBeforeVerificationOrExhaustion(bool count, bool hasHit)
    {
        using var regex = new Utf8Regex(@"(\w)\1");
        var program = regex.Inspection.PreparedRegex.ScalarProgram!;
        var text = string.Concat(Enumerable.Repeat("abç中", 24));
        var input = Encoding.UTF8.GetBytes(hasHit ? text + "éé" : text);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        using var clock = Utf8DeadlineTestClock.Start();
        var invocation = new Utf8SliceInvocation(default, TimeSpan.FromMilliseconds(200), default);
        Assert.True(invocation.Ledger.TryReserve(13));
        invocation.CheckpointHook = step => { if (step == 64) clock.Advance(250); };

        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
        {
            if (count)
                _ = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
            else
                _ = Utf8SliceRunner.TryFindFirst(program, input, 0, starts, ends, false, ref invocation);
        });
        Assert.Equal(-1, starts[0]);
        Assert.Equal(-1, ends[0]);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CompletedCaptureComparisonObservesOriginalAuthority(bool rtl, bool expire)
    {
        const int length = 600000;
        var regex = new Utf8Regex(Pattern(length, rtl), options: RegexOptions.CultureInvariant |
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
