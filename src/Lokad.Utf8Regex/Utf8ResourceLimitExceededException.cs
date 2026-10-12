namespace Lokad.Utf8Regex;

/// <summary>Identifies the resource whose admission was denied.</summary>
public enum Utf8ResourceLimitKind
{
    /// <summary>Combined capacity of live scalar-program instruction arrays during one construction.</summary>
    ConstructionInstructionCapacity,
    /// <summary>Admitted construction capacity, including covered arrays, decoded pattern text and copy overlap.</summary>
    ConstructionBytes,
    /// <summary>Cumulative admitted units of covered construction scans, loop iterations and array work.</summary>
    ConstructionWorkSteps,
    /// <summary>Combined live explicit traversal frames in covered construction walkers.</summary>
    ConstructionDepth,
    /// <summary>Cumulative bounded execution/checkpoint steps in one invocation.</summary>
    ExecutionWorkSteps,
    /// <summary>Cumulative modeled major result and staging capacity in one invocation.</summary>
    ExecutionResultBytes,
    /// <summary>Live backtracking/continuation frames or pending linear alternatives in one invocation.</summary>
    ExecutionDepth,
}

/// <summary>Reports an owned resource-admission failure, distinct from invalid syntax, no match and timeout.</summary>
public sealed class Utf8ResourceLimitExceededException : InvalidOperationException
{
    internal Utf8ResourceLimitExceededException(Utf8ResourceLimitKind resourceKind, ulong limit, ulong requested)
        : base($"The {resourceKind} resource limit of {limit} was exceeded by a request for {requested}.")
    {
        ResourceKind = resourceKind;
        Limit = limit;
        Requested = requested;
    }

    /// <summary>Gets the resource whose admission was denied.</summary>
    public Utf8ResourceLimitKind ResourceKind { get; }

    /// <summary>Gets the configured maximum in the units of <see cref="ResourceKind"/>.</summary>
    public ulong Limit { get; }

    /// <summary>Gets the combined capacity, work or depth requested before the denied operation.
    /// An execution work or result-capacity request beyond ulong.MaxValue is reported as ulong.MaxValue.</summary>
    public ulong Requested { get; }
}
