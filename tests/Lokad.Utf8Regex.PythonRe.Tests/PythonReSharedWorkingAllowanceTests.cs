using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSharedWorkingAllowanceTests
{
    [Fact]
    public void ConcurrentCursorsShareAdmissionAndReleaseOnlyTheirOwnStorage()
    {
        var allowance = new Allowance(1 << 20); Assert.True(allowance.TryReserve(13));
        var regex = new Utf8PythonRegex("(a)");
        using var first = regex.CreateMatchCursor("a"u8.ToArray(), 0, default, allowance);
        Assert.True(first.MoveNext(out var retained)); var one = allowance.Used;
        using var second = regex.CreateMatchCursor("a"u8.ToArray(), 0, default, allowance);
        Assert.True(second.MoveNext(out _)); Assert.True(allowance.Used > one);
        first.Dispose(); first.Dispose(); Assert.True(allowance.Used > 13);
        allowance.Maximum = allowance.Used;
        var failure = Assert.Throws<Utf8MemoryAdmissionException>(() => regex.CreateMatchCursor("a"u8.ToArray(), 0, default, allowance));
        Assert.True(failure.RequestedBytes > 0); Assert.Equal(allowance.Maximum, allowance.Used);
        Assert.False(second.MoveNext(out _)); Assert.Equal(13UL, allowance.Used);
        allowance.Release(13); Assert.Equal(0UL, allowance.Used);
        Assert.Equal("a", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public void RefusalHasNoFallbackAndFailedConstructionRefundsItsPartialLoans()
    {
        var none = new Allowance(0); var regex = new Utf8PythonRegex("(a)");
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.CreateMatchCursor("a"u8.ToArray(), 0, default, none));
        Assert.Equal(0UL, none.Used); Assert.Equal(1, none.Refusals);

        var partial = new Allowance(1 << 20);
        partial.AfterGrant = () => partial.Maximum = partial.Used;
        Assert.Throws<Utf8MemoryAdmissionException>(() =>
        {
            using var cursor = regex.CreateMatchCursor("a"u8.ToArray(), 0, default, partial);
            cursor.MoveNext(out _);
        });
        Assert.True(partial.Peak > 0); Assert.Equal(0UL, partial.Used); Assert.Equal(1, partial.Refusals);
    }

    [Fact]
    public void SharedAdmissionDoesNotRelaxNumericWorkingOrResultLimits()
    {
        var allowance = new Allowance(ulong.MaxValue);
        var working = new Utf8PythonRegex("(a)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => working.CreateMatchCursor("a"u8.ToArray(), 0, default, allowance));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        Assert.Equal(0, allowance.Reservations);
        var result = new Utf8PythonRegex("(a)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxResultBytes = 1 });
        using var cursor = result.CreateMatchCursor("a"u8.ToArray(), 0, default, allowance);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext(out _)).ResourceKind);
        Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public void HalfAMillionDiscardedRangesRefundSharedWorkingCapacityWithoutATrafficQuota()
    {
        var allowance = new Allowance(16 << 10);
        using var cursor = new Utf8PythonRegex("(.)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan).CreateMatchCursor(
            Enumerable.Repeat((byte)'a', 500000).ToArray(), 0, default, allowance);
        var count = 0; while (cursor.MoveNext(out _)) count++;
        Assert.Equal(500000, count); Assert.True(allowance.Peak > 0); Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public void FailureAndPerAdvanceCancellationReleaseScratchButKeepRetainedRangesValid()
    {
        using var token = new CancellationTokenSource();
        var allowance = new Allowance(1 << 20);
        using var cursor = new Utf8PythonRegex("(z)").CreateMatchCursor(
            Encoding.UTF8.GetBytes("z" + new string('a', 262144)), 0, default, allowance);
        Assert.True(cursor.MoveNext(out var retained)); Assert.True(allowance.Used > 0);
        cursor.WorkingInvocation.CheckpointHook = _ => token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _, token.Token)).CancellationToken);
        Assert.Equal(0UL, allowance.Used); Assert.Equal("z", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
    }

    [Fact]
    public void CancellationAfterAGrantRefundsBeforeConstructorFailureReturns()
    {
        using var token = new CancellationTokenSource();
        var allowance = new Allowance(1 << 20) { AfterGrant = token.Cancel };
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            new Utf8PythonRegex("(a)").CreateMatchCursor("a"u8.ToArray(), 0, token.Token, allowance)).CancellationToken);
        Assert.True(allowance.Peak > 0); Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public void IndependentCallersCanReuseOneExpressionAndAllowance()
    {
        var allowance = new Allowance(1 << 20); var regex = new Utf8PythonRegex("(?P<x>.)");
        Parallel.For(0, 16, worker =>
        {
            using var cursor = regex.CreateMatchCursor("é😀a"u8.ToArray(), 0, default, allowance);
            var count = 0; while (cursor.MoveNext(out _)) count++;
            Assert.Equal(3, count);
        });
        Assert.Equal(0UL, allowance.Used); Assert.True(allowance.Peak <= allowance.Maximum);
    }

    [Fact]
    public void OwnedProjectionRefundsItsMapAndScratchWithoutInvalidatingItsSnapshot()
    {
        var allowance = new Allowance(1 << 20);
        using var cursor = new Utf8PythonRegex("(?P<x>.)").CreateMatchCursor("é😀"u8.ToArray(), 0, default, allowance);
        Assert.True(cursor.MoveNext()); var retained = cursor.Current;
        Assert.True(cursor.HasCoordinateMap); Assert.True(allowance.Used > 0);
        cursor.Dispose(); Assert.Equal(0UL, allowance.Used);
        Assert.Equal("é", retained.Groups[1].ValueText);
    }

    [Fact]
    public void PreCancellationWinsBeforeAllowanceOrInputValidation()
    {
        using var token = new CancellationTokenSource(); token.Cancel();
        var regex = new Utf8PythonRegex("a");
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.CreateMatchCursor(new byte[] { 0xff }, 0, token.Token, null!)).CancellationToken);
        Assert.Throws<ArgumentNullException>(() => regex.CreateMatchCursor("a"u8.ToArray(), 0, default, null!));
    }

    [Fact]
    public void SharedWorkingTimeoutDoesNotDecodeAnUnadmittedDiagnosticSubject()
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var allowance = new Allowance(1 << 20);
        using var cursor = new Utf8PythonRegex("(z)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(100))
            .CreateMatchCursor(Encoding.UTF8.GetBytes("z" + new string('a', 262144)), 0, default, allowance);
        Assert.True(cursor.MoveNext(out var retained)); ticks = 100;
        var failure = Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _));
        Assert.Empty(failure.Input); Assert.Equal("(z)", failure.Pattern);
        Assert.Equal(0UL, allowance.Used); Assert.Equal("z", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    private sealed class Allowance(ulong maximum) : IUtf8MemoryAllowance
    {
        private readonly object _gate = new();
        private ulong _maximum = maximum;
        private ulong _used;
        private ulong _peak;
        private int _reservations;
        private int _refusals;
        internal Action? AfterGrant { get; set; }
        internal ulong Maximum { get { lock (_gate) return _maximum; } set { lock (_gate) _maximum = value; } }
        internal ulong Used { get { lock (_gate) return _used; } }
        internal ulong Peak { get { lock (_gate) return _peak; } }
        internal int Reservations { get { lock (_gate) return _reservations; } }
        internal int Refusals { get { lock (_gate) return _refusals; } }
        public bool TryReserve(ulong bytes)
        {
            lock (_gate)
            {
                if (bytes > _maximum - _used) { _refusals++; return false; }
                _used += bytes; _peak = Math.Max(_peak, _used); _reservations++;
            }
            AfterGrant?.Invoke(); return true;
        }
        public void Release(ulong bytes)
        {
            lock (_gate)
            {
                if (bytes > _used) throw new InvalidOperationException("Refund exceeds the caller's funded storage.");
                _used -= bytes;
            }
        }
    }
}
