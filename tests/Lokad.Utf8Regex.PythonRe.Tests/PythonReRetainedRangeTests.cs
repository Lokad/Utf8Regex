using System.Text;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReRetainedRangeTests
{
    private static Utf8PythonRegex Compile(string pattern, IUtf8MemoryAllowance account, Utf8ResourcePolicy policy = default, TimeSpan? timeout = null)
        => Utf8PythonRegex.Compile(pattern, default, timeout ?? Timeout.InfiniteTimeSpan, policy, default,
            Timeout.InfiniteTimeSpan, default, account);

    [Fact]
    public async Task ReceiptsSurviveCursorProgramDisposalAndHostWaits()
    {
        var account = new Allowance(16 << 20); Assert.True(account.TryReserve(13));
        using var regex = Compile("(?P<x>.)", account);
        using var cursor = regex.CreateMatchCursor("é😀"u8.ToArray());
        Assert.True(cursor.MoveNextRetained(out var first)); var alias = first; var snapshot = first.Match;
        Assert.True(first.ChargedBytes > 0); Assert.False(cursor.HasCoordinateMap);
        Assert.True(cursor.MoveNextRetained(out var second));
        Assert.False(cursor.MoveNextRetained(out var none)); Assert.Null(none);
        cursor.Dispose(); regex.Dispose();
        Assert.Equal(13UL + first.ChargedBytes + second.ChargedBytes, account.Used);
        await Task.Yield();
        Assert.Equal("é", Encoding.UTF8.GetString(first.Match.GetGroupMemory(1).Span));
        Assert.Equal("x", first.Match.LastGroup); Assert.Equal("😀", Encoding.UTF8.GetString(second.Match.GetValueMemory().Span));
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(alias.Dispose)));
        Assert.Throws<ObjectDisposedException>(() => first.Match);
        Assert.Equal(13UL + second.ChargedBytes, account.Used);
        Assert.Equal("é", Encoding.UTF8.GetString(snapshot.GetValueMemory().Span));
        second.Dispose(); second.Dispose(); Assert.Equal(13UL, account.Used); account.Release(13);
    }

    [Fact]
    public void DisposedReceiptsDoNotKeepBorrowedSubjectsAlive()
    {
        var (receipt, subject) = DisposedReceipt();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(subject.IsAlive);
        GC.KeepAlive(receipt); // Hosts can retain the charge record after disposal.
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Utf8PythonRetainedRangeMatch Receipt, WeakReference Subject) DisposedReceipt()
    {
        var account = new Allowance(16 << 20); using var regex = Compile(".", account);
        var input = Enumerable.Repeat((byte)'a', 1048576).ToArray(); var subject = new WeakReference(input);
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNextRetained(out var receipt)); receipt.Dispose();
        return (receipt, subject);
    }

    [Fact]
    public void RetainedNameTextNeedsItsOwnAdmissionAfterProgramLifetime()
    {
        var account = new Allowance(16 << 20); var name = new string('n', 4096);
        using var regex = Compile("(?P<" + name + ">.)", account); var program = account.Used;
        account.Maximum = program + 4096;
        using (var denied = regex.CreateMatchCursor("a"u8.ToArray()))
            Assert.Throws<Utf8MemoryAdmissionException>(() => denied.MoveNextRetained(out _));
        Assert.Equal(program, account.Used); account.Maximum = 16 << 20;
        using var cursor = regex.CreateMatchCursor("a"u8.ToArray());
        Assert.True(cursor.MoveNextRetained(out var retained)); Assert.True(retained.ChargedBytes > (ulong)name.Length * 2);
        cursor.Dispose(); regex.Dispose(); Assert.Equal(retained.ChargedBytes, account.Used);
        Assert.Equal(name, retained.Match.LastGroup); retained.Dispose(); Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void PublicationTransfersWithoutAnExternalReleaseOrReacquisition()
    {
        var inner = new Allowance(16 << 20); var account = new ObservedAccount(inner);
        using var regex = Compile(".", account); using var cursor = regex.CreateMatchCursor("aaa"u8.ToArray());
        Assert.True(cursor.MoveNext(out _)); // Stabilize reusable VM workspace.
        var held = inner.Used; var releases = account.ReleaseCalls;
        Assert.True(cursor.MoveNextRetained(out var retained));
        Assert.Equal(releases, account.ReleaseCalls);
        Assert.Equal(held + retained.ChargedBytes, inner.Used);
        retained.Dispose(); Assert.Equal(held, inner.Used); Assert.Equal(releases + 1, account.ReleaseCalls);
    }

    [Fact]
    public void LiveRetainedResultsCanExhaustAdmissionAndRefundIndependently()
    {
        var account = new Allowance(16 << 20); using var regex = Compile(".", account); var program = account.Used;
        account.Maximum = program + 16384;
        using var cursor = regex.CreateMatchCursor(Enumerable.Repeat((byte)'a', 10000).ToArray());
        var retained = new List<Utf8PythonRetainedRangeMatch>();
        try
        {
            Assert.Throws<Utf8MemoryAdmissionException>(() =>
            {
                while (cursor.MoveNextRetained(out var match)) retained.Add(match);
            });
            Assert.NotEmpty(retained);
            Assert.Equal(program + (ulong)retained.Sum(match => checked((long)match.ChargedBytes)), account.Used);
            Assert.Equal("a", Encoding.UTF8.GetString(retained[0].Match.GetValueMemory().Span));
        }
        finally { foreach (var match in retained) match.Dispose(); }
        Assert.Equal(program, account.Used);
    }

    [Fact]
    public void ImmediatelyReleasedReceiptsStreamWithoutAnImplicitTrafficQuota()
    {
        var account = new Allowance(16 << 20); using var regex = Compile(".", account); var program = account.Used;
        account.Maximum = program + 16384;
        using var cursor = regex.CreateMatchCursor(Enumerable.Repeat((byte)'a', 500000).ToArray());
        var count = 0;
        while (cursor.MoveNextRetained(out var match))
        {
            using (match) { Assert.Equal(count, match.Match.Value.StartOffsetInBytes); count++; }
        }
        Assert.Equal(500000, count); Assert.Equal(program, account.Used);
    }

    [Fact]
    public void ReleaseDoesNotRefundExplicitResultTraffic()
    {
        var account = new Allowance(16 << 20); ulong charge;
        using (var probe = Compile(".", account))
        using (var cursor = probe.CreateMatchCursor("a"u8.ToArray()))
        {
            Assert.True(cursor.MoveNextRetained(out var match)); charge = match.ChargedBytes; match.Dispose();
        }
        Assert.Equal(0UL, account.Used);
        using var regex = Compile(".", account, new Utf8ResourcePolicy { MaxResultBytes = charge });
        var program = account.Used; using var limited = regex.CreateMatchCursor("aa"u8.ToArray());
        Assert.True(limited.MoveNextRetained(out var first)); first.Dispose();
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.MoveNextRetained(out _));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, failure.ResourceKind); Assert.Equal(program, account.Used);
    }

    [Theory] [InlineData(1)] [InlineData(2)]
    public void CancellationBeforePublicationRefundsOnlyUnpublishedStorage(int cancelAfterGrant)
    {
        var account = new Allowance(16 << 20); using var regex = Compile(".", account); var program = account.Used;
        using var cursor = regex.CreateMatchCursor("aaa"u8.ToArray());
        using var completedToken = new CancellationTokenSource();
        Assert.True(cursor.MoveNextRetained(out var first, completedToken.Token)); completedToken.Cancel();
        using var activeToken = new CancellationTokenSource(); var grants = 0;
        account.AfterGrant = () => { if (++grants == cancelAfterGrant) activeToken.Cancel(); };
        Assert.Equal(activeToken.Token, Assert.Throws<OperationCanceledException>(() =>
            cursor.MoveNextRetained(out _, activeToken.Token)).CancellationToken);
        account.AfterGrant = null;
        Assert.Equal(program + first.ChargedBytes, account.Used);
        Assert.Equal("a", Encoding.UTF8.GetString(first.Match.GetValueMemory().Span));
        first.Dispose(); Assert.Equal(program, account.Used);
    }

    [Fact]
    public void TimeoutDuringReceiptAcquisitionPreservesEarlierRetainedResults()
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var account = new Allowance(16 << 20); using var regex = Compile(".", account, timeout: TimeSpan.FromMilliseconds(100));
        var program = account.Used; using var cursor = regex.CreateMatchCursor("aa"u8.ToArray());
        Assert.True(cursor.MoveNextRetained(out var first));
        var grants = 0; account.AfterGrant = () => { if (++grants == 2) ticks = 100; };
        var failure = Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNextRetained(out _));
        account.AfterGrant = null;
        Assert.Empty(failure.Input); Assert.Equal(".", failure.Pattern);
        Assert.Equal(program + first.ChargedBytes, account.Used);
        Assert.Equal("a", Encoding.UTF8.GetString(first.Match.GetValueMemory().Span)); first.Dispose();
    }

    [Fact]
    public void RetainedAndLegacyAdvancesKeepTheirOriginalSnapshotContracts()
    {
        var account = new Allowance(16 << 20); using var regex = Compile(".", account);
        using var cursor = regex.CreateMatchCursor("abc"u8.ToArray());
        Assert.True(cursor.MoveNextRetained(out var retained));
        Assert.Throws<InvalidOperationException>(() => cursor.Current);
        Assert.True(cursor.MoveNext()); var ownedText = cursor.Current;
        Assert.True(cursor.MoveNext(out var range));
        Assert.False(cursor.MoveNextRetained(out _));
        Assert.Equal("a", Encoding.UTF8.GetString(retained.Match.GetValueMemory().Span));
        Assert.Equal("b", ownedText.Groups[0].ValueText); Assert.Equal("c", Encoding.UTF8.GetString(range.GetValueMemory().Span));
        retained.Dispose();
    }

    [Fact]
    public void UnfundedCursorRejectsRetainedUseWithoutConsumingItsPosition()
    {
        using var cursor = new Utf8PythonRegex(".").CreateMatchCursor("a"u8.ToArray());
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNextRetained(out _));
        Assert.True(cursor.MoveNext(out var match)); Assert.Equal(0, match.Value.StartOffsetInBytes);
    }

    internal sealed class ObservedAccount(Allowance inner) : IUtf8MemoryAllowance
    {
        private int _releases;
        internal int ReleaseCalls => Volatile.Read(ref _releases);
        public bool TryReserve(ulong bytes) => inner.TryReserve(bytes);
        public void Release(ulong bytes) { Interlocked.Increment(ref _releases); inner.Release(bytes); }
    }
}
