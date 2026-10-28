using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonRePersistentCursorTests
{
    [Theory]
    [InlineData("(?P<x>.)", "é😀z", 0)]
    [InlineData("(?P<x>.)", "é😀z", 2)]
    [InlineData("(?<=é)(?P<x>.)", "é😀z", 0)]
    [InlineData(@"(.)\1", "é😀😀z", 0)]
    [InlineData("", "é😀z", 0)]
    [InlineData(".*?", "é😀z", 0)]
    [InlineData("(?P<x>a)?", "ab", 0)]
    [InlineData("(?P<x>z)", "é😀", 0)]
    public void IncrementalDiscoveryPreservesScalarRangesCapturesAndEmptyRetries(string pattern, string subject, int start)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        var expected = regex.FindIterDetailed(input, start);
        using var cursor = regex.CreateMatchCursor(input, start);
        Assert.Throws<InvalidOperationException>(() => cursor.Current);
        foreach (var match in expected)
        {
            Assert.True(cursor.MoveNext());
            Assert.Equal(match.Groups, cursor.Current.Groups);
            Assert.Equal(match.NameEntries, cursor.Current.NameEntries);
        }
        Assert.False(cursor.MoveNext()); Assert.False(cursor.MoveNext());
        Assert.Throws<InvalidOperationException>(() => cursor.Current);
    }

    [Fact]
    public void EmptyThenNonemptyDiscoveryUsesWholeSupplementaryScalars()
    {
        using var cursor = new Utf8PythonRegex(".*?").CreateMatchCursor(Encoding.UTF8.GetBytes("😀"));
        var ranges = new List<(int, int, int, int)>();
        while (cursor.MoveNext())
        {
            var value = cursor.Current.Value;
            ranges.Add((value.StartOffsetInBytes, value.EndOffsetInBytes, value.StartOffsetInUtf16, value.EndOffsetInUtf16));
        }
        Assert.Equal([(0, 0, 0, 0), (0, 4, 0, 2), (4, 4, 2, 2)], ranges);
    }

    [Fact]
    public void AliasesSharePositionAndDisposalButSnapshotsOwnTheirArraysAndText()
    {
        var input = Encoding.UTF8.GetBytes("é😀");
        var cursor = new Utf8PythonRegex("(?P<x>.)").CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        var first = alias.Current;
        Assert.Equal("é", first.Value.ValueText);
        Assert.True(alias.MoveNext());
        var second = cursor.Current;
        Assert.Equal("😀", second.Value.ValueText);
        Assert.Equal((2, 6, 1, 3), (second.Value.StartOffsetInBytes, second.Value.EndOffsetInBytes,
            second.Value.StartOffsetInUtf16, second.Value.EndOffsetInUtf16));
        Assert.NotSame(first.Groups, second.Groups); Assert.NotSame(first.NameEntries, second.NameEntries);
        alias.Dispose(); cursor.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext());
        Assert.Throws<ObjectDisposedException>(() => alias.Current);
        input.AsSpan().Fill((byte)'a');
        Assert.True(first.TryGetFirstSetGroup("x", out var group)); Assert.Equal("é", group.ValueText);
        Assert.True(second.TryGetFirstSetGroup("x", out group)); Assert.Equal("😀", group.ValueText);
        first.Groups[0] = default; first.NameEntries[0] = default;
        Assert.Equal("😀", second.Value.ValueText);
        Assert.True(second.TryGetFirstSetGroup("x", out group)); Assert.Equal("😀", group.ValueText);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExplicitCloseReleasesScratchSynchronouslyAndKeepsBorrowedLoans(bool exhaust)
    {
        var input = Encoding.UTF8.GetBytes("é😀");
        var invocation = Invocation();
        Assert.True(invocation.Ledger.TryReserve(13));
        using var cursor = InternalCursor(input, "(.)", invocation);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        Assert.True(cursor.HasCoordinateMap); Assert.True(invocation.Ledger.ReservedBytes > 13);
        if (exhaust) { Assert.True(cursor.MoveNext()); Assert.False(cursor.MoveNext()); Assert.False(cursor.MoveNext()); }
        else cursor.Dispose();
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal("é", retained.Value.ValueText);
        cursor.Dispose(); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
        Assert.Null(typeof(Utf8PythonMatchCursor).GetMethod("Finalize", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void FirstAdvanceDoesNotMaterializeLaterMatches()
    {
        var regex = new Utf8PythonRegex("(a)");
        var input = Encoding.UTF8.GetBytes(new string('a', 1 << 20));
        using (var warm = regex.CreateMatchCursor(input)) Assert.True(warm.MoveNext());
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal((0, 1), (cursor.Current.Value.StartOffsetInBytes, cursor.Current.Value.EndOffsetInBytes));
        Assert.True(allocated < 65536, $"Unexpected eager collection: {allocated} bytes.");
    }

    [Fact]
    public void WorkLimitIsCumulativeAcrossAdvancesAndFailureCleansUp()
    {
        var input = "aa"u8.ToArray();
        var probe = Invocation();
        ulong firstSteps, secondSteps;
        using (var cursor = InternalCursor(input, "(a)", probe))
        {
            Assert.True(cursor.MoveNext()); firstSteps = probe.Budget.Steps;
            Assert.True(cursor.MoveNext()); secondSteps = probe.Budget.Steps;
        }
        Assert.True(secondSteps > firstSteps + 1);
        var invocation = new Utf8SliceInvocation(new(secondSteps - 1, 0, 1UL << 20), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        using var limited = InternalCursor(input, "(a)", invocation);
        Assert.True(limited.MoveNext()); var retained = limited.Current;
        var denial = Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.MoveNext());
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, denial.ResourceKind);
        Assert.Equal(secondSteps - 1, denial.Limit);
        Assert.Equal(secondSteps, denial.Requested);
        Assert.Throws<InvalidOperationException>(() => limited.Current);
        Assert.Throws<InvalidOperationException>(() => limited.MoveNext());
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
        Assert.Equal("a", retained.Value.ValueText);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InFlightFailureUsesOriginalTokenOrDeadlineAndReleasesLoans(bool expire)
    {
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
        Assert.True(cursor.MoveNext()); var retained = cursor.Current;
        var seed = invocation.Budget.Steps;
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = steps =>
        {
            if (steps > seed + 64) { entered = true; if (expire) ticks = 100; else token.Cancel(); }
        };
        if (expire) Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext());
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext()).CancellationToken);
        Assert.True(entered); Assert.Throws<InvalidOperationException>(() => cursor.Current);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
        Assert.Equal("z", retained.Value.ValueText);
    }

    [Fact]
    public void ProjectionCancellationPublishesNoSnapshotAndReleasesItsMapAndArrays()
    {
        using var token = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1000000, 0, 4UL << 20), Regex.InfiniteMatchTimeout, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        // Two projected long strings must cross the 64-step diagnostic hook
        // interval after map creation, independent of the discovery step phase.
        using var cursor = InternalCursor(Encoding.UTF8.GetBytes(new string('é', 65536)), "(é+)", invocation);
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = _ => { if (cursor.HasCoordinateMap) { entered = true; token.Cancel(); } };
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext()).CancellationToken);
        Assert.True(entered); Assert.Throws<InvalidOperationException>(() => cursor.Current);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13);
    }

    [Fact]
    public async Task OriginalDeadlineIncludesSequentialHostWaits()
    {
        long ticks = 0;
        Utf8PythonMatchCursor cursor;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks))
            cursor = new Utf8PythonRegex("(.)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(100))
                .CreateMatchCursor("aa"u8.ToArray());
        using (cursor)
        {
            Assert.True(cursor.MoveNext()); var retained = cursor.Current;
            await Task.Yield(); ticks = 100;
            Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext());
            Assert.Equal("a", retained.Value.ValueText);
        }
    }

    [Fact]
    public void ReentrantCallsAreRejectedWithoutInvalidatingTheOwner()
    {
        var invocation = Invocation();
        using var cursor = InternalCursor(Encoding.UTF8.GetBytes(new string('a', 262144) + "z"), "(z)", invocation);
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = _ =>
        {
            if (entered) return;
            entered = true;
            Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
            Assert.Throws<InvalidOperationException>(() => cursor.Current);
            Assert.Throws<InvalidOperationException>(() => cursor.Dispose());
        };
        Assert.True(cursor.MoveNext()); Assert.True(entered); Assert.Equal("z", cursor.Current.Value.ValueText);
        Assert.False(cursor.MoveNext()); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public async Task ConcurrentCallsAreRejectedAndSequentialOwnershipCanResume()
    {
        var invocation = Invocation();
        using var cursor = InternalCursor(Encoding.UTF8.GetBytes(new string('a', 262144) + "z"), "(z)", invocation);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var first = true;
        cursor.WorkingInvocation.CheckpointHook = _ =>
        {
            if (!first) return;
            first = false; entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Cursor witness was not resumed.");
        };
        // The caller blocks on entry, so the advancing worker must not depend on pool availability.
        var advance = Task.Factory.StartNew(cursor.MoveNext, CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
            Assert.Throws<InvalidOperationException>(() => cursor.Current);
            Assert.Throws<InvalidOperationException>(() => cursor.Dispose());
        }
        finally { resume.Set(); await advance; }
        Assert.True(advance.Result); Assert.Equal("z", cursor.Current.Value.ValueText);
        Assert.False(cursor.MoveNext()); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void FactoryValidatesWholeSubjectScalarOffsetAndCancellationBeforePublication()
    {
        var regex = new Utf8PythonRegex("a");
        Assert.Throws<ArgumentException>(() => regex.CreateMatchCursor(new byte[] { (byte)'a', 0xff }));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.CreateMatchCursor(Encoding.UTF8.GetBytes("😀"), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.CreateMatchCursor("a"u8.ToArray(), 2));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.CreateMatchCursor(new byte[] { 0xff }, -1, token.Token)).CancellationToken);
        Func<ReadOnlyMemory<byte>, Utf8PythonMatchCursor> create = regex.CreateMatchCursor;
        using var empty = create(default); Assert.False(empty.MoveNext());
        using var atEnd = regex.CreateMatchCursor("a"u8.ToArray(), 1, CancellationToken.None); Assert.False(atEnd.MoveNext());
        using var legacy = new Utf8PythonRegex("", default).CreateMatchCursor(default); Assert.True(legacy.MoveNext());
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
