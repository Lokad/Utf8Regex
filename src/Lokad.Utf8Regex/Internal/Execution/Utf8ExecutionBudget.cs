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
    private readonly CancellationToken _token;

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

    internal uint PeakDepth { get; private set; }

    internal ulong PeakWorkingBytes { get; private set; }

    internal bool IsInfiniteDeadline => _deadline.IsInfinite;

    internal Utf8ExecutionDeadline Deadline => _deadline;

    internal void ThrowIfCancellationRequested()
    {
        _token.ThrowIfCancellationRequested();
    }

    internal bool IsUnmetered =>
        Limits.MaxWorkSteps == 0 &&
        Limits.MaxDepth == 0 &&
        Limits.MaxWorkingBytes == 0 &&
        _deadline.IsInfinite &&
        !_token.CanBeCanceled;

    internal void CheckDeadline() => _deadline.CheckNow();

    internal Utf8ExecutionLimitKind TryChargeStep()
    {
        _token.ThrowIfCancellationRequested();
        Steps++;
        _deadline.Step();
        return Limits.MaxWorkSteps != 0 && Steps > Limits.MaxWorkSteps
            ? Utf8ExecutionLimitKind.WorkSteps
            : Utf8ExecutionLimitKind.None;
    }

    internal Utf8ExecutionLimitKind TryNoteDepth(uint depth)
    {
        if (depth > PeakDepth)
        {
            PeakDepth = depth;
        }

        return Limits.MaxDepth != 0 && depth > Limits.MaxDepth
            ? Utf8ExecutionLimitKind.Depth
            : Utf8ExecutionLimitKind.None;
    }

    internal Utf8ExecutionLimitKind TryNoteWorkingBytes(ulong bytes)
    {
        if (bytes > PeakWorkingBytes)
        {
            PeakWorkingBytes = bytes;
        }

        return Limits.MaxWorkingBytes != 0 && bytes > Limits.MaxWorkingBytes
            ? Utf8ExecutionLimitKind.WorkingBytes
            : Utf8ExecutionLimitKind.None;
    }
}
