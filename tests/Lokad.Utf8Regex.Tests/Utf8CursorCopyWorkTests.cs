using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8CursorCopyWorkTests
{
    private static Utf8SliceProgram Program(Utf8Regex regex) =>
        (Utf8SliceProgram)typeof(Utf8Regex).GetField("_ownedSliceProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;

    public static IEnumerable<object[]> Routes()
    {
        (string Pattern, string Text)[] rows = [
            ("target", "é😀 target target"), ("(?i:target)", "é😀 TARGET target"),
            ("é", "é😀 é"), ("(?:target|needle)", "é😀 target needle target"),
            ("(?i:target|needle)", "é😀 TARGET needle target"),
            ("ab[0-9]{1,2}d", "é😀 ab1d ab12d"),
            ("(?:get|set)[A-Z][A-Za-z0-9_]*", "é😀 getAlpha setBeta"),
            ("a.{0,4}b", "é😀 a😀b a中b"), ("^target", "target é😀"),
            (@"target\z", "é😀 target"), (@"(ab)\1", "é😀 abab abab"),
            (@"(?=(.))", "a😀b"), ("", "a😀b")];
        foreach (var row in rows)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [rtl && row.Pattern == @"(ab)\1" ? @"\1(ab)" : row.Pattern,
                row.Text, compiled, rtl];
    }

    private static RegexOptions Options(bool compiled, bool rtl) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);

    [Theory]
    [InlineData(7U, 0UL, false)]
    [InlineData(0U, 64UL, false)]
    [InlineData(0U, 0UL, true)]
    public void ControlledScopesShareCounterUpdatesAndSeparateInvocationsRemainIndependent(uint depth, ulong bytes, bool deadline)
    {
        var timeout = deadline ? TimeSpan.FromHours(1) : Regex.InfiniteMatchTimeout;
        var original = new Utf8SliceInvocation(new(depth, bytes), timeout, default);
        var copy = original;
        Assert.Equal(Utf8ExecutionLimitKind.None, original.CheckInput());
        Assert.Equal(Utf8ExecutionLimitKind.None, copy.Budget.TryNoteDepth(7));
        Assert.Equal(7U, original.Budget.PeakDepth);
        Assert.Equal(Utf8ExecutionLimitKind.None, original.Budget.TryNoteWorkingBytes(64));
        Assert.Equal(64UL, copy.Budget.PeakWorkingBytes);
        var independent = new Utf8SliceInvocation(new(depth, bytes), timeout, default);
        Assert.Equal(0U, independent.Budget.PeakDepth);
        Assert.Equal(0UL, independent.Budget.PeakWorkingBytes);
    }

    [Fact]
    public void UnlimitedNoneTokenInvocationMetadataDoesNotAllocate()
    {
        // Warm the measured call site through delayed tiering.
        var warmup = Stopwatch.StartNew();
        do { _ = MeasureInvocationMetadata(out _); }
        while (warmup.ElapsedMilliseconds < 1_000);

        var allocated = MeasureInvocationMetadata(out var steps);
        Assert.Equal(512UL, steps);
        Assert.Equal(0L, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureInvocationMetadata(out ulong steps)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        steps = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 512; i++)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
            _ = invocation.CheckInput();
            steps += invocation.Budget.DiagnosticCheckpoints;
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void CheckpointCanPromoteInlineStorageAndPublishSharedAliases()
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Utf8SliceInvocation published = default;
        var checkpoints = new List<ulong>();
        invocation.CheckpointHook = steps =>
        {
            checkpoints.Add(steps);
            invocation.ShareStorageForCursor();
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
            published = invocation;
        };
        for (var i = 0; i < 64; i++)
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.CheckInput());

        Assert.Equal(new[] { 64UL }, checkpoints);
        Assert.Equal(13UL, published.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.None, published.CheckInput());
        published.Ledger.Release(13);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(new[] { 64UL }, checkpoints);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckpointPreservesOriginalCancellationAndImmediateInputDeadline(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var deadline = expire ? Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20)) : Utf8ExecutionDeadline.Infinite;
        var invocation = new Utf8SliceInvocation(new(0, 0), deadline, token.Token);
        var alias = invocation;
        var checkpoints = new List<ulong>();
        invocation.CheckpointHook = steps =>
        {
            checkpoints.Add(steps);
            invocation.ShareStorageForCursor();
            if (expire) ticks = 21;
            else token.Cancel();
        };
        for (var i = 0; i < 63; i++)
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.CheckInput());

        if (expire)
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => invocation.CheckInput());
        else
        {
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.CheckInput());
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => alias.CheckInput()).CancellationToken);
        }
        Assert.Same(deadline, alias.Budget.Deadline);
        Assert.Equal(new[] { 64UL }, checkpoints);
        Assert.Equal(Utf8ExecutionLimitKind.None, alias.Budget.LimitBreach);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void PublicCopiesKeepIndependentPositionsAndOneCumulativeWorkCounter(string pattern, string text, bool compiled, bool rtl)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var regex = new Utf8Regex(pattern, options: Options(compiled, rtl));
        using var source = new CancellationTokenSource();
        var original = regex.EnumerateMatches(Encoding.UTF8.GetBytes(text), cancellationToken: source.Token);
        var copy = original.GetEnumerator();
        var entrySteps = copy.DiagnosticCheckpointCount;
        var expected = new Regex(pattern, Options(compiled, rtl)).Matches(text).Cast<Match>()
            .Where(match => (match.Index == 0 || match.Index == text.Length || !char.IsLowSurrogate(text[match.Index])) &&
                (match.Index + match.Length == 0 || match.Index + match.Length == text.Length ||
                 !char.IsLowSurrogate(text[match.Index + match.Length]))).ToArray();
        Assert.NotEmpty(expected);
        Assert.True(original.MoveNext());
        Assert.True(original.DiagnosticCheckpointCount > entrySteps);
        Assert.Equal(original.DiagnosticCheckpointCount, copy.DiagnosticCheckpointCount);
        Assert.True(copy.MoveNext());
        Assert.Equal(original.Current, copy.Current);
        Assert.Equal(original.DiagnosticCheckpointCount, copy.DiagnosticCheckpointCount);
        var retained = copy.Current;
        var index = 1;
        while (original.MoveNext())
        {
            Assert.Equal((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected[index].Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected[index].Index, expected[index].Length))),
                (original.Current.StartOffsetInBytes, (original.Current.EndOffsetInBytes - original.Current.StartOffsetInBytes)));
            Assert.Equal(retained, copy.Current);
            Assert.Equal(original.DiagnosticCheckpointCount, copy.DiagnosticCheckpointCount);
            index++;
        }
        Assert.Equal(expected.Length, index);
        index = 1;
        // The first copy's EOF must not change the other copy's position.
        while (copy.MoveNext())
        {
            Assert.Equal((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected[index].Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected[index].Index, expected[index].Length))),
                (copy.Current.StartOffsetInBytes, (copy.Current.EndOffsetInBytes - copy.Current.StartOffsetInBytes)));
            Assert.Equal(original.DiagnosticCheckpointCount, copy.DiagnosticCheckpointCount);
            index++;
        }
        Assert.Equal(expected.Length, index);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void RepeatedFreshCopiesCannotRestartTheCancellationCheckpointCadence(string pattern, string text, bool compiled, bool rtl)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var regex = new Utf8Regex(pattern, options: Options(compiled, rtl));
        using var source = new CancellationTokenSource();
        var active = false;
        ulong checkpoint = 0;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (!active) return;
            checkpoint = steps;
            source.Cancel();
        });
        try
        {
            var original = regex.EnumerateMatches(Encoding.UTF8.GetBytes(text), cancellationToken: source.Token);
            var entrySteps = original.DiagnosticCheckpointCount;
            active = true;
            var canceled = false;
            for (var attempt = 0; attempt < 128 && !canceled; attempt++)
            {
                var copy = original.GetEnumerator();
                try { Assert.True(copy.MoveNext()); }
                catch (OperationCanceledException exception)
                {
                    Assert.Equal(source.Token, exception.CancellationToken);
                    canceled = true;
                }
            }
            Assert.True(canceled, "Fresh value copies restarted the checkpoint cadence.");
            Assert.True(checkpoint > entrySteps);
            Assert.Equal(0UL, checkpoint & 63);
            Assert.True(original.DiagnosticCheckpointCount >= checkpoint);
            Assert.False(original.Current.Success);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(0, true, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(0, false, true)]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(0, true, true)]
    [InlineData(1, true, true)]
    [InlineData(2, true, true)]
    public void SplitCopiesAccumulateWorkThroughTailAndEofWhileRetainingExistingViews(int count, bool compiled, bool rtl)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        const string text = "é😀 abab abab tail";
        var regex = new Utf8Regex(@"(ab)\1", options: Options(compiled, rtl));
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(0, 0), Regex.InfiniteMatchTimeout, source.Token);
        var original = Utf8OwnedSplitPreparation.Create(Program(regex), Encoding.UTF8.GetBytes(text), count, ref invocation);
        var copy = original.GetEnumerator();
        Assert.True(original.MoveNext());
        Assert.Equal(original.DiagnosticCheckpointCount, copy.DiagnosticCheckpointCount);
        Assert.True(copy.MoveNext());
        var retained = copy.Current;
        var retainedValue = Encoding.UTF8.GetString(retained.GetValueBytes());
        // EnumerateSplits excludes captured delimiters; compare copies directly.
        var originalValues = new List<string> { Encoding.UTF8.GetString(original.Current.GetValueBytes()) };
        while (original.MoveNext()) originalValues.Add(Encoding.UTF8.GetString(original.Current.GetValueBytes()));
        Assert.Equal(original.DiagnosticCheckpointCount, copy.DiagnosticCheckpointCount);
        var copyValues = new List<string> { Encoding.UTF8.GetString(copy.Current.GetValueBytes()) };
        while (copy.MoveNext()) copyValues.Add(Encoding.UTF8.GetString(copy.Current.GetValueBytes()));
        Assert.Equal(originalValues, copyValues);
        Assert.Equal(original.DiagnosticCheckpointCount, copy.DiagnosticCheckpointCount);
        source.Cancel();
        Assert.Equal(retainedValue, Encoding.UTF8.GetString(retained.GetValueBytes()));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
