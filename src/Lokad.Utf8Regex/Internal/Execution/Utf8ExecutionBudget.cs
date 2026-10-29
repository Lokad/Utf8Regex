using System.Runtime.CompilerServices;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Flavor-neutral, invocation-local execution budget: step, depth, and working-bytes
/// accounting with deadline and cancellation checkpoints. Limit breaches are reported
/// as return values so each profile maps them onto its own failure vocabulary without
/// paying exception costs on the hot path; expired deadlines and cancelled tokens
/// throw through the shared checkpoints instead. Hold by reference (as a field), never
/// copy mid-operation: counters live in this struct.
/// </summary>
internal struct Utf8ExecutionBudget
{
    private readonly Utf8ExecutionDeadline _deadline;
    private CancellationToken _token;

    internal Utf8ExecutionBudget(Utf8ExecutionLimits limits, TimeSpan timeout, CancellationToken token)
        : this(limits, Utf8ExecutionDeadline.Start(timeout), token)
    {
    }

    internal Utf8ExecutionBudget(Utf8ExecutionLimits limits, Utf8ExecutionDeadline deadline, CancellationToken token)
    {
        Limits = limits;
        _deadline = deadline;
        _token = token;
    }

    internal Utf8ExecutionLimits Limits { get; }

    internal ulong Steps { get; private set; }

    // Sticky denial distinguishes an exhausted ulong.MaxValue cap from its
    // last admitted step. Steps saturates; even an unlimited counter never wraps.
    internal Utf8ExecutionLimitKind LimitBreach { get; private set; }
    internal bool WorkLimitExceeded => LimitBreach == Utf8ExecutionLimitKind.WorkSteps;

    // Conservative cumulative result/staging capacity requests. No identities,
    // retention roots or refunds; the first resource denial stays authoritative.
    internal ulong ResultBytes { get; private set; }

    internal uint PeakDepth { get; private set; }

    internal ulong PeakWorkingBytes { get; private set; }

    internal bool IsInfiniteDeadline => _deadline.IsInfinite;

    internal Utf8ExecutionDeadline Deadline => _deadline;

    internal void ThrowIfCancellationRequested()
    {
        _token.ThrowIfCancellationRequested();
    }

    // A durable cursor may borrow interruption authority for one synchronous
    // advance. Its exclusive owner must restore the prior token in finally.
    internal CancellationToken ExchangeCancellationToken(CancellationToken token)
    {
        var previous = _token;
        _token = token;
        return previous;
    }

    internal bool IsUnmetered =>
        Limits.MaxWorkSteps == 0 &&
        Limits.MaxDepth == 0 &&
        Limits.MaxWorkingBytes == 0 &&
        Limits.MaxResultBytes == 0 &&
        _deadline.IsInfinite &&
        !_token.CanBeCanceled;

    internal void CheckDeadline() => _deadline.CheckNow();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Utf8ExecutionLimitKind TryChargeStep()
    {
        _token.ThrowIfCancellationRequested();
        var overflow = Steps == ulong.MaxValue;
        if (!overflow) Steps++;
        _deadline.Step();
        if (LimitBreach == Utf8ExecutionLimitKind.None && Limits.MaxWorkSteps != 0 && (overflow || Steps > Limits.MaxWorkSteps))
            LimitBreach = Utf8ExecutionLimitKind.WorkSteps;
        return LimitBreach;
    }

    internal Utf8ExecutionLimitKind TryChargeResultBytes(ulong bytes)
    {
        if (Limits.MaxResultBytes == 0) return LimitBreach;
        _token.ThrowIfCancellationRequested();
        _deadline.CheckNow();
        if (LimitBreach != Utf8ExecutionLimitKind.None) return LimitBreach;
        var overflow = bytes > ulong.MaxValue - ResultBytes;
        ResultBytes = overflow ? ulong.MaxValue : ResultBytes + bytes;
        if (overflow || ResultBytes > Limits.MaxResultBytes) LimitBreach = Utf8ExecutionLimitKind.ResultBytes;
        return LimitBreach;
    }

    internal Utf8ExecutionLimitKind TryNoteDepth(uint depth)
    {
        if (depth > PeakDepth)
        {
            PeakDepth = depth;
        }

        if (LimitBreach == Utf8ExecutionLimitKind.None && Limits.MaxDepth != 0 && depth > Limits.MaxDepth)
            LimitBreach = Utf8ExecutionLimitKind.Depth;
        return LimitBreach;
    }

    // Core/Python public failures share one typed conversion. Other profiles
    // keep their own mappings from the neutral admission result.
    internal readonly Utf8ResourceLimitExceededException CreateResourceFailure()
        => LimitBreach switch
        {
            Utf8ExecutionLimitKind.WorkSteps => new(Utf8ResourceLimitKind.ExecutionWorkSteps, Limits.MaxWorkSteps, Steps),
            Utf8ExecutionLimitKind.ResultBytes => new(Utf8ResourceLimitKind.ExecutionResultBytes, Limits.MaxResultBytes, ResultBytes),
            Utf8ExecutionLimitKind.Depth => new(Utf8ResourceLimitKind.ExecutionDepth, Limits.MaxDepth, PeakDepth),
            Utf8ExecutionLimitKind.WorkingBytes => new(Utf8ResourceLimitKind.ExecutionWorkingBytes, Limits.MaxWorkingBytes, PeakWorkingBytes),
            _ => throw new InvalidOperationException("No public execution resource denial is recorded."),
        };

    internal Utf8ExecutionLimitKind TryNoteWorkingBytes(ulong bytes)
    {
        if (bytes > PeakWorkingBytes)
        {
            PeakWorkingBytes = bytes;
        }

        if (LimitBreach == Utf8ExecutionLimitKind.None && Limits.MaxWorkingBytes != 0 && bytes > Limits.MaxWorkingBytes)
            LimitBreach = Utf8ExecutionLimitKind.WorkingBytes;
        return LimitBreach;
    }

    // Aggregate arithmetic/ledger admission failed before acquisition. Array
    // representability guards keep their separate capacity-failure path.
    internal Utf8ExecutionLimitKind DenyWorkingBytes(ulong requested)
    {
        if (requested > PeakWorkingBytes) PeakWorkingBytes = requested;
        if (LimitBreach == Utf8ExecutionLimitKind.None) LimitBreach = Utf8ExecutionLimitKind.WorkingBytes;
        return LimitBreach;
    }
}
