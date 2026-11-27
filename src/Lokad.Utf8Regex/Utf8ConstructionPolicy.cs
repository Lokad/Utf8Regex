namespace Lokad.Utf8Regex;

/// <summary>Caller-selected admission limits for covered eager pattern construction.</summary>
/// <remarks>Null keeps the fixed construction backstop; positive values may tighten it.
/// Entry points with a caller-owned program allowance instead use that allowance
/// for byte admission: null MaxBytes adds no private byte cap, and a positive
/// MaxBytes is an additional limit. Other construction backstops remain.
/// Limits cover live traversal frames, instruction capacity and major storage.
/// Parser scans and construction loops observe cooperative interruption.</remarks>
public readonly record struct Utf8ConstructionPolicy
{
    /// <summary>Gets the live covered traversal-frame limit, or null for the fixed backstop.</summary>
    public uint? MaxDepth { get; init; }

    /// <summary>Gets the covered instruction-array capacity limit, or null for the fixed backstop.</summary>
    public ulong? MaxInstructionCapacity { get; init; }

    /// <summary>Gets the conservative major construction-storage limit, including growth overlap. Null keeps the fixed backstop except on caller-funded program entry points, where it adds no byte cap.</summary>
    public ulong? MaxBytes { get; init; }

    /// <summary>Gets the policy that keeps all fixed construction backstops.</summary>
    public static Utf8ConstructionPolicy Default => default;

    internal void Validate(string paramName)
    {
        if (MaxDepth == 0 || MaxInstructionCapacity == 0 || MaxBytes == 0)
            throw new ArgumentOutOfRangeException(paramName, "Construction budgets must be null (fixed backstop) or at least one.");
    }
}
