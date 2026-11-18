using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SplitInvocationTests
{
    private static Utf8SliceProgram Program(Utf8Regex regex) =>
        (Utf8SliceProgram)typeof(Utf8Regex).GetField("_ownedSliceProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_matchCursor")]
    private static extern ref Utf8OperationMatchCursor MatchCursor(ref Utf8ValueSplitEnumerator cursor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_sliceInvocation")]
    private static extern ref Utf8SliceInvocation Invocation(ref Utf8OperationMatchCursor cursor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_startTickCount64")]
    private static extern ref long StartedAt(Utf8ExecutionDeadline deadline);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PreparationWorkCutoffsPreserveBorrowedCapacityWithoutCoordinateStorage(int count)
    {
        var regex = new Utf8Regex("(ab)", options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("é😀 abab ab");
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(7));
        var prepared = Utf8OwnedSplitPreparation.Create(Program(regex), input, count, ref baseline);
        var steps = prepared.DebugInvocationSteps;
        var total = prepared.DebugInvocationReservedBytes;
        Assert.Equal(0UL, prepared.DebugPeakWorkingBytes);
        Assert.Equal(7UL, total);
        for (ulong cap = 1; cap < steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var denied = false;
            try { _ = Utf8OwnedSplitPreparation.Create(Program(regex), input, count, ref invocation); }
            catch (InvalidOperationException) { denied = true; }
            Assert.True(denied);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
        foreach (var cap in new[] { total, total + 1 })
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var denied = false;
            try
            {
                var cursor = Utf8OwnedSplitPreparation.Create(Program(regex), input, count, ref invocation);
                Assert.Equal(total, cursor.DebugInvocationReservedBytes);
            }
            catch (InvalidOperationException) { denied = true; }
            Assert.Equal(cap < total, denied);
            if (denied) Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    public static IEnumerable<object[]> LifetimeCases()
    {
        foreach (var pattern in new[] { "ab", "", "(.)", @"(ab)\1" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var count in new[] { 0, 1, 2 }) yield return [pattern, compiled, rtl, count];
    }

    [Theory]
    [MemberData(nameof(LifetimeCases))]
    public void CountOneFinalTailAndTerminalMovesObserveTheOriginalDeadlineAndExactToken(string pattern, bool compiled, bool rtl, int count)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        var input = Encoding.UTF8.GetBytes("é😀 abab ab");
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(30)
        });
        var cursor = regex.EnumerateSplits(input, count);
        if (count == 2) Assert.True(cursor.MoveNext());
        if (count == 0) while (cursor.MoveNext()) { }
        var prior = (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes);
        // Expire the actual deadline retained by the public factory after its
        // successful advances, without depending on scheduler or GC latency.
        var deadline = Invocation(ref MatchCursor(ref cursor)).Budget.Deadline;
        Assert.False(deadline.IsInfinite);
        StartedAt(deadline) = Environment.TickCount64 - 30_001;
        var expired = false;
        try { _ = cursor.MoveNext(); }
        catch (RegexMatchTimeoutException exception)
        {
            Assert.Equal(regex.Pattern, exception.Pattern);
            Assert.Equal(regex.MatchTimeout, exception.MatchTimeout);
            Assert.Equal(Encoding.UTF8.GetString(input), exception.Input);
            expired = true;
        }
        Assert.True(expired);
        Assert.Equal(prior, (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
        using var source = new CancellationTokenSource();
        var token = new Utf8Regex(pattern, options: options).EnumerateSplits(input, count, source.Token);
        if (count == 2) Assert.True(token.MoveNext());
        if (count == 0) while (token.MoveNext()) { }
        prior = (token.Current.IndexInBytes, token.Current.LengthInBytes);
        source.Cancel();
        var canceled = false;
        try { _ = token.MoveNext(); }
        catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); canceled = true; }
        Assert.True(canceled);
        Assert.Equal(prior, (token.Current.IndexInBytes, token.Current.LengthInBytes));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public void EofAndInterruptedMovesKeepByteViewsWithoutCoordinateStorage(bool rtl, int count)
    {
        var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex("(ab)", options: options);
        var input = Encoding.UTF8.GetBytes("é😀 abab ab");
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var view = FinishSplit(regex, input, count, ref invocation, out var cursor);
        var value = view.GetValueString();
        const ulong held = 7;
        Assert.Equal(held, cursor.DebugInvocationReservedBytes);
        Assert.Equal(value, view.GetValueString());
        source.Cancel();
        var canceled = false;
        try { _ = cursor.MoveNext(); }
        catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); canceled = true; }
        Assert.True(canceled);
        Assert.Equal(held, cursor.DebugInvocationReservedBytes);
        Assert.Equal(value, view.GetValueString());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8ValueSplit FinishSplit(Utf8Regex regex, byte[] input, int count,
        ref Utf8SliceInvocation invocation, out Utf8ValueSplitEnumerator cursor)
    {
        cursor = Utf8OwnedSplitPreparation.Create(Program(regex), input, count, ref invocation);
        Assert.True(cursor.MoveNext());
        var view = cursor.Current;
        while (cursor.MoveNext()) { }
        return view;
    }
}
