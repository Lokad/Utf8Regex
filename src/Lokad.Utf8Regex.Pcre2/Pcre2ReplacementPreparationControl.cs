using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

// Synchronous borrowed control. Controlled preparation checkpoints spend the
// existing invocation's shared work; no owner or token is retained by the cache.
// Nested operations restore their caller's control.
internal readonly ref struct Pcre2ReplacementPreparationControl
{
    [ThreadStatic] private static Pcre2ReplacementPreparationControlState s_current;
    private readonly Pcre2ReplacementPreparationControlState _previous;

    private Pcre2ReplacementPreparationControl(Pcre2ReplacementPreparationControlState current)
    { _previous = s_current; s_current = current; }

    internal static Pcre2ReplacementPreparationControl EnterOperation(TimeSpan timeout, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return new(new(token, Utf8ExecutionDeadline.Start(timeout), true, default, false));
    }

    internal static Pcre2ReplacementPreparationControl Borrow(Utf8SliceInvocation invocation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return new(new(token, invocation.Budget.Deadline, true, invocation, !invocation.IsUnmetered));
    }

    internal static Pcre2ReplacementPreparationControl JoinOrEnter(TimeSpan timeout)
        => s_current.Active ? new(s_current) : EnterOperation(timeout, default);

    internal static void Check()
    {
        if (!s_current.Active) return;
        s_current.Token.ThrowIfCancellationRequested();
        try
        {
            if (s_current.HasInvocation)
                Pcre2SliceDriver.ThrowIfSliceBreached(s_current.Invocation.TryChargeInputStep());
            else s_current.Deadline.CheckNow();
            s_current.Token.ThrowIfCancellationRequested();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
    }

    // Fund length-dependent hashing/comparison before entering the cache.
    // This coarse charge complements existing parser/encoding checkpoints;
    // it does not pretend to count every construction helper or allocation.
    internal static void CheckText(int length)
    {
        if (!s_current.HasInvocation) return;
        for (var offset = 0; offset < length;)
        {
            Check();
            offset += Math.Min(2048, length - offset);
        }
    }

    internal static bool IsActive => s_current.Active;
    public void Dispose() => s_current = _previous;

    private struct Pcre2ReplacementPreparationControlState
    {
        internal CancellationToken Token;
        internal Utf8ExecutionDeadline Deadline;
        internal bool Active;
        internal bool HasInvocation;
        internal Utf8SliceInvocation Invocation;

        internal Pcre2ReplacementPreparationControlState(CancellationToken token,
            Utf8ExecutionDeadline deadline, bool active, Utf8SliceInvocation invocation, bool hasInvocation)
        {
            Token = token; Deadline = deadline; Active = active;
            Invocation = invocation; HasInvocation = hasInvocation;
        }
    }
}

internal readonly struct Pcre2ReplacementTextControl : IUtf8ConstructionTextControl
{
    public void Check() => Pcre2ReplacementPreparationControl.Check();
}
