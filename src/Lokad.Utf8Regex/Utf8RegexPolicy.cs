namespace Lokad.Utf8Regex;

/// <summary>Immutable construction, execution and program-funding settings for a compiled expression.</summary>
/// <remarks>Profiles copy the settings at construction. Construction and execution have independent deadlines and limits. Borrowed input, caller-owned output and incidental objects remain outside modeled major-storage admission; this is not a process-memory bound. The caller owns the lifetime and thread safety of a supplied allowance.</remarks>
public sealed class Utf8RegexPolicy
{
    /// <summary>Gets invocation work, depth, working and result limits; default leaves them unlimited.</summary>
    public Utf8ResourcePolicy ResourcePolicy { get; init; }

    /// <summary>Gets the timeout for each later operation, or null for the profile's default match timeout.</summary>
    public TimeSpan? MatchTimeout { get; init; }

    /// <summary>Gets eager construction limits; default keeps the fixed backstops, with separate caller-funded byte admission when supplied.</summary>
    public Utf8ConstructionPolicy ConstructionPolicy { get; init; }

    /// <summary>Gets the independent eager construction timeout, or null for infinite.</summary>
    public TimeSpan? ConstructionTimeout { get; init; }

    /// <summary>Gets caller-owned admission for compilation/program retention and default working storage, or null for numeric controls without shared funding.</summary>
    /// <remarks>Disposing the compiled expression closes new uses and refunds after active calls/cursors finish. Explicitly funded programs prepare retained operation programs before publication. Result retention and separate per-operation working accounts keep their documented scopes; refusal never retries without admission.</remarks>
    public IUtf8MemoryAllowance? ProgramMemoryAllowance { get; init; }

    internal static readonly Utf8RegexPolicy Default = new();
}
