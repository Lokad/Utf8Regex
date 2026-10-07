namespace Lokad.Utf8Regex;

/// <summary>Identifies the resource whose admission was denied.</summary>
public enum Utf8ResourceLimitKind
{
    /// <summary>Combined capacity of live scalar-program instruction arrays during one construction.</summary>
    ConstructionInstructionCapacity,
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

    /// <summary>Gets the combined capacity requested before the denied acquisition.</summary>
    public ulong Requested { get; }
}
