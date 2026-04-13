namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8ExecutionBudget
{
    private const int CheckInterval = 2048;

    private readonly string _pattern;
    private readonly TimeSpan _timeout;
    private readonly long _startTickCount64;
    private readonly long _timeoutMilliseconds;
    private int _remainingChecks;

    private Utf8ExecutionBudget(string pattern, TimeSpan timeout)
    {
        _pattern = pattern;
        _timeout = timeout;
        _startTickCount64 = Environment.TickCount64;
        _timeoutMilliseconds = Math.Max(1L, (long)Math.Ceiling(timeout.TotalMilliseconds));
        _remainingChecks = CheckInterval;
    }

    public static Utf8ExecutionBudget? Create(string pattern, TimeSpan timeout)
    {
        if (timeout == Regex.InfiniteMatchTimeout)
        {
            return null;
        }

        return new Utf8ExecutionBudget(pattern, timeout);
    }

    public void Step(ReadOnlySpan<byte> input)
    {
        if (--_remainingChecks > 0)
        {
            return;
        }

        _remainingChecks = CheckInterval;
        if (Environment.TickCount64 - _startTickCount64 >= _timeoutMilliseconds)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), _pattern, _timeout);
        }
    }
}
