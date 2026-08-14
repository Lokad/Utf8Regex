namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Flavor-neutral, invocation-local deadline polling state.
/// PCRE2-INTEGRATION-POINT
/// </summary>
internal sealed class Utf8ExecutionDeadline
{
    private const int CheckInterval = 2048;
    private static readonly Utf8ExecutionDeadline s_infinite = new();

    private readonly long _startTickCount64;
    private readonly long _timeoutMilliseconds;
    private int _remainingChecks;

    private Utf8ExecutionDeadline()
    {
        _startTickCount64 = 0;
        _timeoutMilliseconds = 0;
        _remainingChecks = CheckInterval;
    }

    private Utf8ExecutionDeadline(TimeSpan timeout)
    {
        _startTickCount64 = Environment.TickCount64;
        _timeoutMilliseconds = Math.Max(1L, (long)Math.Ceiling(timeout.TotalMilliseconds));
        _remainingChecks = CheckInterval;
    }

    public bool IsInfinite => _timeoutMilliseconds == 0;

    public static Utf8ExecutionDeadline Infinite => s_infinite;

    public static Utf8ExecutionDeadline Start(TimeSpan timeout) =>
        timeout == Regex.InfiniteMatchTimeout
            ? Infinite
            : new Utf8ExecutionDeadline(timeout);

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
        return Environment.TickCount64 - _startTickCount64 >= _timeoutMilliseconds;
    }

    public void Step()
    {
        if (Poll())
        {
            throw new Utf8ExecutionDeadlineExpiredException();
        }
    }
}

internal sealed class Utf8ExecutionDeadlineExpiredException : Exception
{
}
