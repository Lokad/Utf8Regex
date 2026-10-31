using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2;

// Synchronous borrowed control. Controlled preparation checkpoints spend the
// existing invocation's shared work; no owner or token is retained by the cache.
// Nested operations restore their caller's control.
internal readonly ref struct Pcre2ReplacementPreparationControl
{
    [ThreadStatic] private static Pcre2ReplacementPreparationControlState s_current;
    private readonly Pcre2ReplacementPreparationControlState _previous;
    private readonly bool _restore;

    private Pcre2ReplacementPreparationControl(Pcre2ReplacementPreparationControlState current)
    { _previous = s_current; _restore = true; s_current = current; }

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
        => s_current.Active ? default : EnterOperation(timeout, default);

    internal static Pcre2ReplacementPreparation Prepare(string replacement, Pcre2SubstitutionOptions options, bool literal)
        => Prepare(new Utf8ConstructionPattern(replacement), options, literal);

    internal static Pcre2ReplacementPreparation Prepare(Utf8ConstructionPattern replacement, Pcre2SubstitutionOptions options, bool literal)
    {
        if (!s_current.HasInvocation || s_current.Invocation.Ledger.IsUnlimited)
            return Compile(replacement, options, literal);
        Check();
        if (s_current.Invocation.Ledger.HasMemoryAllowance)
        {
            // The funded destination entry keeps this construction owner alive
            // through copying. Each major allocation already borrows the same
            // invocation ledger; do not acquire its peak a second time afterwards.
            if (!Utf8ConstructionScope.IsActive)
                throw new InvalidOperationException("Funded replacement preparation requires a live construction owner.");
            return Compile(replacement, options, literal);
        }
        var available = s_current.Invocation.Ledger.MaxLiveBytes - s_current.Invocation.Ledger.ReservedBytes;
        using var scope = Utf8ConstructionScope.RestrictAdditionalBytes(available);
        var baseline = scope.Budget.ReservedBytes;
        var ceiling = scope.Budget.MaximumBytes;
        // Map only this public ceiling; an independently tighter construction
        // backstop keeps its existing construction failure vocabulary.
        var publicCeiling = available <= ulong.MaxValue - baseline && ceiling == baseline + available;
        try
        {
            var result = Compile(replacement, options, literal);
            var peak = scope.Budget.PeakBytes - baseline;
            Pcre2SliceDriver.ThrowIfSliceBreached(s_current.Invocation.TryReserveWorking(peak));
            s_current.PreparationReservation += peak;
            scope.Complete();
            return result;
        }
        catch (Utf8ResourceLimitExceededException failure) when (publicCeiling &&
            failure.ResourceKind == Utf8ResourceLimitKind.ConstructionBytes && failure.Limit == ceiling)
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(s_current.Invocation.TryReserveWorking(failure.Requested - baseline));
            throw;
        }
    }

    private static Pcre2ReplacementPreparation Compile(Utf8ConstructionPattern replacement, Pcre2SubstitutionOptions options, bool literal)
    {
        if (literal) return new(Pcre2SimpleReplacementParser.Literal(replacement), null);
        return Pcre2SimpleReplacementParser.TryParse(replacement, options, out var simple)
            ? new(simple, null) : new(null, Pcre2PreparedReplacementParser.Parse(replacement, options));
    }

    // Native callers validate once before route selection. Copy complete scalar
    // fragments directly; only the explicit text convenience performs encoding.
    internal static byte[] EncodeSource(Utf8ConstructionSession storage,
        Utf8ConstructionPattern source, int start, int length)
    {
        if (!source.IsUtf8)
            return storage.EncodeRepairedUtf8(source.Text.AsSpan(start, length), default(Pcre2ReplacementTextControl));
        var result = storage.Allocate<byte>(length);
        var bytes = source.Utf8.Slice(start, length);
        for (var offset = 0; offset < length;)
        {
            Check();
            var count = Math.Min(2048, length - offset);
            storage.Budget.ChargeWork((ulong)count);
            bytes.Slice(offset, count).CopyTo(result.AsSpan(offset, count));
            offset += count;
        }
        Check();
        return result;
    }

    internal static byte[] FreezeNativeKey(Utf8ConstructionSession storage, Utf8ConstructionPattern source)
    {
        Check();
        var charge = Utf8ConstructionBudget.ArrayBytes<byte>(source.Length);
        // Retain the temporary key loan through parsing/cache admission and the
        // operation. The existing control refunds it together with parser peak;
        // retained cache capacity has its own separate conservative bound.
        if (s_current.HasInvocation && !s_current.Invocation.Ledger.HasMemoryAllowance)
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(s_current.Invocation.TryReserveWorking(charge));
            s_current.PreparationReservation += charge;
        }
        Check();
        return EncodeSource(storage, source, 0, source.Length);
    }

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
    public void Dispose()
    {
        if (!_restore) return;
        if (s_current.PreparationReservation != 0)
            s_current.Invocation.Ledger.Release(s_current.PreparationReservation);
        s_current = _previous;
    }

    private struct Pcre2ReplacementPreparationControlState
    {
        internal CancellationToken Token;
        internal Utf8ExecutionDeadline Deadline;
        internal bool Active;
        internal bool HasInvocation;
        internal Utf8SliceInvocation Invocation;
        internal ulong PreparationReservation;

        internal Pcre2ReplacementPreparationControlState(CancellationToken token,
            Utf8ExecutionDeadline deadline, bool active, Utf8SliceInvocation invocation, bool hasInvocation)
        {
            Token = token; Deadline = deadline; Active = active;
            Invocation = invocation; HasInvocation = hasInvocation;
            PreparationReservation = 0;
        }
    }
}

internal readonly struct Pcre2ReplacementTextControl : IUtf8ConstructionTextControl
{
    public void Check() => Pcre2ReplacementPreparationControl.Check();
}
