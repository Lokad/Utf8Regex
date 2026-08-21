namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Flavor-neutral validation for the managed regex timeout domain. The limits
/// mirror .NET 10: infinite, or a positive duration no greater than
/// <c>TimeSpan.FromMilliseconds(int.MaxValue - 1)</c>.
/// PCRE2-INTEGRATION-POINT
/// PYTHONRE-INTEGRATION-POINT
/// </summary>
internal static class Utf8MatchTimeout
{
    private const long InfiniteMatchTimeoutTicks = -10_000;
    private const ulong MaximumMatchTimeoutTicks = 10_000UL * (int.MaxValue - 1);

    internal static TimeSpan Validate(TimeSpan matchTimeout, string parameterName)
    {
        var ticks = matchTimeout.Ticks;
        if (ticks != InfiniteMatchTimeoutTicks && (ulong)(ticks - 1) >= MaximumMatchTimeoutTicks)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return matchTimeout;
    }
}
