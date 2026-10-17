namespace Lokad.Utf8Regex;

/// <summary>Caller-selected admission limits for covered eager pattern construction.</summary>
/// <remarks>Null keeps the fixed construction backstop; positive values may tighten it.
/// Limits cover admitted work, traversal frames, instruction capacity and major
/// storage, not every allocation, CLR/JIT metadata, lazy preparation, caller
/// retention or process memory. Temporary immutable-metadata enumerable views
/// and enumerator boxes have no individual byte/acquisition-work charge;
/// their backing tables and traversal work remain covered.
/// Construction work units can change by version.</remarks>
public readonly record struct Utf8ConstructionPolicy
{
    /// <summary>Gets the cumulative covered construction-work limit, or null for the fixed backstop.</summary>
    public ulong? MaxWorkSteps { get; init; }

    /// <summary>Gets the live covered traversal-frame limit, or null for the fixed backstop.</summary>
    public uint? MaxDepth { get; init; }

    /// <summary>Gets the covered instruction-array capacity limit, or null for the fixed backstop.</summary>
    public ulong? MaxInstructionCapacity { get; init; }

    /// <summary>Gets the conservative major construction-storage limit, including growth overlap, or null for the fixed backstop.</summary>
    public ulong? MaxBytes { get; init; }

    /// <summary>Gets the policy that keeps all fixed construction backstops.</summary>
    public static Utf8ConstructionPolicy Default => default;

    internal void Validate(string paramName)
    {
        if (MaxWorkSteps == 0 || MaxDepth == 0 || MaxInstructionCapacity == 0 || MaxBytes == 0)
            throw new ArgumentOutOfRangeException(paramName, "Construction budgets must be null (fixed backstop) or at least one.");
    }
}
