namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Flavor-neutral, invocation-local deadline polling state.
/// PCRE2-INTEGRATION-POINT
/// </summary>
internal sealed class Utf8ExecutionDeadline
{
    private const int CheckInterval = 2048;
    private static readonly Utf8ExecutionDeadline s_infinite = new();
    [ThreadStatic] private static DiagnosticClockScope? s_diagnosticClock;

    private readonly long _startTickCount64;
    private readonly long _timeoutMilliseconds;
    private readonly Func<long>? _readTickCount64;
    private int _remainingChecks;

    private Utf8ExecutionDeadline()
    {
        _startTickCount64 = 0;
        _timeoutMilliseconds = 0;
        _remainingChecks = CheckInterval;
    }

    private Utf8ExecutionDeadline(TimeSpan timeout)
    {
        // Capture the invocation's authority once. Restoring a diagnostic scope
        // cannot change a deadline that is already used by discovery/projection.
        _readTickCount64 = s_diagnosticClock?.ReadTickCount64;
        _startTickCount64 = ReadTickCount64();
        _timeoutMilliseconds = Math.Max(1L, (long)Math.Ceiling(timeout.TotalMilliseconds));
        _remainingChecks = CheckInterval;
    }

    public bool IsInfinite => _timeoutMilliseconds == 0;

    public static Utf8ExecutionDeadline Infinite => s_infinite;

    public static Utf8ExecutionDeadline Start(TimeSpan timeout) =>
        timeout == Regex.InfiniteMatchTimeout
            ? Infinite
            : new Utf8ExecutionDeadline(timeout);

    private long ReadTickCount64() => _readTickCount64?.Invoke() ?? Environment.TickCount64;

    /// <summary>Provides a monotonic clock to synchronous diagnostic witnesses.
    /// Production invocations use Environment.TickCount64. The clock is captured
    /// by each finite deadline; the infinite singleton never reads it.</summary>
    internal static IDisposable UseClockForDiagnostics(Func<long> readTickCount64)
    {
        return new DiagnosticClockScope(readTickCount64);
    }

    private sealed class DiagnosticClockScope : IDisposable
    {
        private readonly DiagnosticClockScope? _previous;
        internal readonly Func<long> ReadTickCount64;
        private bool _disposed;

        internal DiagnosticClockScope(Func<long> readTickCount64)
        {
            _previous = s_diagnosticClock;
            ReadTickCount64 = readTickCount64;
            s_diagnosticClock = this;
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (!ReferenceEquals(s_diagnosticClock, this))
                throw new InvalidOperationException("Diagnostic deadline clocks must close in lexical order.");
            s_diagnosticClock = _previous;
            _disposed = true;
        }
    }

    /// <summary>
    /// Returns true once the deadline has expired. Non-polling cadence steps
    /// and the infinite state are allocation-free and do not read the clock.
    /// </summary>
    public bool Poll()
    {
        if (IsInfinite || --_remainingChecks > 0)
        {
            return false;
        }

        _remainingChecks = CheckInterval;
        return ReadTickCount64() - _startTickCount64 >= _timeoutMilliseconds;
    }

    public void Step()
    {
        if (Poll())
        {
            throw new Utf8ExecutionDeadlineExpiredException();
        }
    }

    // Buffer work charges one step per bounded block instead of one per VM
    // instruction. Check its deadline directly so polling cannot be diluted
    // by multiplying the VM cadence by the block size.
    internal void CheckNow()
    {
        if (!IsInfinite && ReadTickCount64() - _startTickCount64 >= _timeoutMilliseconds)
            throw new Utf8ExecutionDeadlineExpiredException();
    }
}

internal sealed class Utf8ExecutionDeadlineExpiredException : Exception
{
}
