using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonRePersistentCursorTests
{

    [Fact]
    public void EmptyThenNonemptyDiscoveryUsesWholeSupplementaryScalars()
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var cursor = new Utf8PythonRegex(".*?").CreateMatchCursor(Encoding.UTF8.GetBytes("😀"));
        var ranges = new List<(int, int)>();
        while (cursor.MoveNext(out cursorRange))
        {
            var value = cursorRange.Value;
            ranges.Add((value.StartOffsetInBytes, value.EndOffsetInBytes));
        }
        Assert.Equal([(0, 0), (0, 4), (4, 4)], ranges);
    }

    [Fact]
    public void AliasesSharePositionAndDisposalWhileRangeMetadataRemainsImmutable()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = Encoding.UTF8.GetBytes("é😀");
        var cursor = new Utf8PythonRegex("(?P<x>.)").CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out cursorRange));
        var first = cursorRange;
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString(first.GetValueMemory().Span));
        Assert.True(alias.MoveNext(out cursorRange));
        var second = cursorRange;
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(second.GetValueMemory().Span));
        Assert.Equal((2,6),(second.Value.StartOffsetInBytes,second.Value.EndOffsetInBytes));
        alias.Dispose(); cursor.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));

        Assert.True(first.TryGetFirstSetGroup("x", out var group)); Assert.Equal("é", System.Text.Encoding.UTF8.GetString((group.Success ? first.GetGroupMemory(group.Number) : default).Span));
        Assert.True(second.TryGetFirstSetGroup("x", out group)); Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((group.Success ? second.GetGroupMemory(group.Number) : default).Span));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(second.GetValueMemory().Span));
        Assert.True(second.TryGetFirstSetGroup("x", out group)); Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((group.Success ? second.GetGroupMemory(group.Number) : default).Span));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExplicitCloseReleasesScratchSynchronouslyAndKeepsBorrowedLoans(bool exhaust)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = Encoding.UTF8.GetBytes("é😀");
        var invocation = Invocation();
        Assert.True(invocation.Ledger.TryReserve(13));
        using var cursor = InternalCursor(input, "(.)", invocation);
        Assert.True(cursor.MoveNext(out cursorRange));
        var retained = cursorRange;
        Assert.True(invocation.Ledger.ReservedBytes > 13);
        if (exhaust) { Assert.True(cursor.MoveNext(out cursorRange)); Assert.False(cursor.MoveNext(out cursorRange)); Assert.False(cursor.MoveNext(out cursorRange)); }
        else cursor.Dispose();
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        cursor.Dispose(); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
        Assert.Null(typeof(Utf8PythonMatchCursor).GetMethod("Finalize", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void FirstAdvanceDoesNotMaterializeLaterMatches()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var regex = new Utf8PythonRegex("(a)");
        var input = Encoding.UTF8.GetBytes(new string('a', 1 << 20));
        using (var warm = regex.CreateMatchCursor(input)) Assert.True(warm.MoveNext(out _));
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out cursorRange));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal((0, 1), (cursorRange.Value.StartOffsetInBytes, cursorRange.Value.EndOffsetInBytes));
        Assert.True(allocated < 65536, $"Unexpected eager collection: {allocated} bytes.");
    }

    [Fact]
    public void WorkLimitIsCumulativeAcrossAdvancesAndFailureCleansUp()
    {
        Utf8PythonRangeMatch cursorRange_limited = default;
        var input = "aa"u8.ToArray();
        var probe = Invocation();
        ulong firstSteps, secondSteps;
        using (var cursor = InternalCursor(input, "(a)", probe))
        {
            Assert.True(cursor.MoveNext(out _)); firstSteps = probe.Budget.Steps;
            Assert.True(cursor.MoveNext(out _)); secondSteps = probe.Budget.Steps;
        }
        Assert.True(secondSteps > firstSteps + 1);
        var invocation = new Utf8SliceInvocation(new(secondSteps - 1, 0, 1UL << 20), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        using var limited = InternalCursor(input, "(a)", invocation);
        Assert.True(limited.MoveNext(out cursorRange_limited)); var retained = cursorRange_limited;
        var denial = Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.MoveNext(out _));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, denial.ResourceKind);
        Assert.Equal(secondSteps - 1, denial.Limit);
        Assert.Equal(secondSteps, denial.Requested);
        Assert.Throws<InvalidOperationException>(() => limited.MoveNext(out _));
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InFlightFailureUsesOriginalTokenOrDeadlineAndReleasesLoans(bool expire)
    {
        Utf8PythonRangeMatch cursorRange = default;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var timeout = expire ? TimeSpan.FromMilliseconds(100) : Regex.InfiniteMatchTimeout;
        var invocation = new Utf8SliceInvocation(new(1000000, 0, 4UL << 20), timeout, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        // Candidate filtering meters 2048-byte windows, so this suffix still
        // crosses the diagnostic hook's 64-step interval after the first hit.
        var input = Encoding.UTF8.GetBytes("z" + new string('a', 262144));
        using var cursor = InternalCursor(input, "(z)", invocation, timeout);
        Assert.True(cursor.MoveNext(out cursorRange)); var retained = cursorRange;
        var seed = invocation.Budget.Steps;
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = steps =>
        {
            if (steps > seed + 64) { entered = true; if (expire) ticks = 100; else token.Cancel(); }
        };
        if (expire) Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _));
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _)).CancellationToken);
        Assert.True(entered); Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
        Assert.Equal("z", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public void ProjectionCancellationPublishesNoSnapshotAndReleasesItsWorkingLoans()
    {
        using var token = new CancellationTokenSource();
        var account = new PythonReSharedWorkingAllowanceTests.Allowance(1 << 20);
        Assert.True(account.TryReserve(13));
        using var cursor = new Utf8PythonRegex("(é)").CreateMatchCursor(
            Encoding.UTF8.GetBytes("é" + new string('x', 2048) + "é"), 0, token.Token, account);
        Assert.True(cursor.MoveNext(out var retained));
        var entered = false;
        account.AfterGrant = () => { entered = true; token.Cancel(); };
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _)).CancellationToken);
        Assert.True(entered); Assert.Equal(13UL, account.Used); account.Release(13);
        Assert.Equal("é", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public async Task OriginalDeadlineIncludesSequentialHostWaits()
    {
        Utf8PythonRangeMatch cursorRange = default;
        long ticks = 0;
        Utf8PythonMatchCursor cursor;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks))
            cursor = new Utf8PythonRegex("(.)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(100))
                .CreateMatchCursor("aa"u8.ToArray());
        using (cursor)
        {
            Assert.True(cursor.MoveNext(out cursorRange)); var retained = cursorRange;
            await Task.Yield(); ticks = 100;
            Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _));
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        }
    }

    [Fact]
    public void ReentrantCallsAreRejectedWithoutInvalidatingTheOwner()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var invocation = Invocation();
        using var cursor = InternalCursor(Encoding.UTF8.GetBytes(new string('a', 262144) + "z"), "(z)", invocation);
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = steps =>
        {
            if (entered) return;
            entered = true;
            Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
            Assert.Throws<InvalidOperationException>(() => cursor.Dispose());
        };
        Assert.True(cursor.MoveNext(out cursorRange)); Assert.True(entered); Assert.Equal("z", System.Text.Encoding.UTF8.GetString(cursorRange.GetValueMemory().Span));
        Assert.False(cursor.MoveNext(out cursorRange)); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public async Task ConcurrentCallsAreRejectedAndSequentialOwnershipCanResume()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var invocation = Invocation();
        using var cursor = InternalCursor(Encoding.UTF8.GetBytes(new string('a', 262144) + "z"), "(z)", invocation);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var first = true;
        cursor.WorkingInvocation.CheckpointHook = steps =>
        {
            if (!first) return;
            first = false; entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Cursor witness was not resumed.");
        };
        // The caller blocks on entry, so the advancing worker must not depend on pool availability.
        var advance = Task.Factory.StartNew(() => cursor.MoveNext(out cursorRange), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
            Assert.Throws<InvalidOperationException>(() => cursor.Dispose());
        }
        finally { resume.Set(); await advance; }
        Assert.True(advance.Result); Assert.Equal("z", System.Text.Encoding.UTF8.GetString(cursorRange.GetValueMemory().Span));
        Assert.False(cursor.MoveNext(out cursorRange)); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void FactoryChecksScalarOffsetAndCancellationBeforePublication()
    {
        var regex = new Utf8PythonRegex("a");

        Assert.Throws<ArgumentOutOfRangeException>(() => regex.CreateMatchCursor(Encoding.UTF8.GetBytes("😀"), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.CreateMatchCursor("a"u8.ToArray(), 2));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.CreateMatchCursor(new byte[] { 0xff }, -1, token.Token)).CancellationToken);
        Func<ReadOnlyMemory<byte>, Utf8PythonMatchCursor> create = subject => regex.CreateMatchCursor(subject);
        using var empty = create(default); Assert.False(empty.MoveNext(out _));
        using var atEnd = regex.CreateMatchCursor("a"u8.ToArray(), 1, CancellationToken.None); Assert.False(atEnd.MoveNext(out _));
        using var legacy = new Utf8PythonRegex("", default).CreateMatchCursor(default); Assert.True(legacy.MoveNext(out _));
    }

    private static Utf8SliceInvocation Invocation()
        => new(new(1000000, 0, 4UL << 20), Regex.InfiniteMatchTimeout, default);

    private static Utf8PythonMatchCursor InternalCursor(ReadOnlyMemory<byte> input, string pattern,
        Utf8SliceInvocation invocation, TimeSpan? timeout = null)
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex(pattern);
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        return new Utf8PythonMatchCursor(input, program, [], 0,
            timeout ?? Regex.InfiniteMatchTimeout, pattern, invocation, null);
    }
}
