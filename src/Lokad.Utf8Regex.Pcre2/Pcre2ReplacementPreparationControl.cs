using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

// Synchronous borrowed control. No counters, buffer ownership or caller tokens
// are retained by the cache. Nested operations restore their caller's control.
internal readonly ref struct Pcre2ReplacementPreparationControl
{
    [ThreadStatic] private static Pcre2ReplacementPreparationControlState s_current;
    private readonly Pcre2ReplacementPreparationControlState _previous;

    private Pcre2ReplacementPreparationControl(Pcre2ReplacementPreparationControlState current)
    { _previous = s_current; s_current = current; }

    internal static Pcre2ReplacementPreparationControl EnterOperation(TimeSpan timeout, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return new(new(token, Utf8ExecutionDeadline.Start(timeout), true));
    }

    internal static Pcre2ReplacementPreparationControl JoinOrEnter(TimeSpan timeout)
        => s_current.Active ? new(s_current) : EnterOperation(timeout, default);

    internal static void Check()
    {
        if (!s_current.Active) return;
        s_current.Token.ThrowIfCancellationRequested();
        try { s_current.Deadline.CheckNow(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
    }

    internal static bool IsActive => s_current.Active;
    public void Dispose() => s_current = _previous;

    private readonly record struct Pcre2ReplacementPreparationControlState(
        CancellationToken Token, Utf8ExecutionDeadline Deadline, bool Active);
}

internal readonly struct Pcre2ReplacementTextControl : IUtf8ConstructionTextControl
{
    public void Check() => Pcre2ReplacementPreparationControl.Check();
}
