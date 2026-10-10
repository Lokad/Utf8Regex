using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeadlineClockTests
{
    [Fact]
    public void ControlledClockPreservesExactThresholdAndPollingCadence()
    {
        using var clock = Utf8DeadlineTestClock.Start();
        foreach (var milliseconds in new[] { 20, 100, 200 })
        {
            var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(milliseconds));
            var reads = clock.Reads;
            clock.Advance(milliseconds - 1);
            deadline.CheckNow();
            Assert.Equal(reads + 1, clock.Reads);
            clock.Advance(1);
            reads = clock.Reads;
            for (var i = 0; i < 2047; i++) Assert.False(deadline.Poll());
            Assert.Equal(reads, clock.Reads);
            Assert.True(deadline.Poll());
            Assert.Equal(reads + 1, clock.Reads);
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(deadline.CheckNow);
        }
    }

    [Fact]
    public void EachDeadlineRetainsItsClockAcrossNestedScopeRestoration()
    {
        using var outer = Utf8DeadlineTestClock.Start();
        var original = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(200));
        var inner = Utf8DeadlineTestClock.Start();
        var nested = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        inner.Dispose();
        inner.Advance(20);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(nested.CheckNow);
        original.CheckNow();
        var restored = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(200));
        outer.Advance(200);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(original.CheckNow);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(restored.CheckNow);
        inner.Dispose();
    }

    [Fact]
    public void InfiniteDeadlineNeverReadsTheClockAndScopesRequireLexicalDisposal()
    {
        var outer = Utf8DeadlineTestClock.Start();
        var inner = Utf8DeadlineTestClock.Start();
        try
        {
            Assert.Throws<InvalidOperationException>(outer.Dispose);
            var infinite = Utf8ExecutionDeadline.Start(Regex.InfiniteMatchTimeout);
            Assert.Same(Utf8ExecutionDeadline.Infinite, infinite);
            inner.Advance(10000);
            infinite.CheckNow();
            for (var i = 0; i < 4096; i++) Assert.False(infinite.Poll());
            Assert.Equal(0, inner.Reads);
            Assert.Equal(0, outer.Reads);
        }
        finally { inner.Dispose(); outer.Dispose(); }
    }

    [Fact]
    public void DefaultClockStillExpiresWithElapsedWallTime()
    {
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1));
        // TickCount64 may advance in 16 ms quanta on Windows.
        Thread.Sleep(50);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(deadline.CheckNow);
    }
}

/// <summary>Controls time only in synchronous diagnostic tests. A captured
/// deadline keeps this authority when a nested scope is restored.</summary>
internal sealed class Utf8DeadlineTestClock : IDisposable
{
    private readonly IDisposable _scope;
    private long _milliseconds;
    internal int Reads { get; private set; }
    private Utf8DeadlineTestClock()
    {
        // Reflection keeps the missing-clock baseline buildable.
        var factory = typeof(Utf8ExecutionDeadline).GetMethod("UseClockForDiagnostics", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("The diagnostic deadline clock is unavailable.");
        _scope = (IDisposable)factory.Invoke(null, [(Func<long>)(() => { Reads++; return _milliseconds; })])!;
    }
    internal static Utf8DeadlineTestClock Start() => new();
    internal void Advance(int milliseconds) => _milliseconds = checked(_milliseconds + milliseconds);
    public void Dispose() => _scope.Dispose();
}
