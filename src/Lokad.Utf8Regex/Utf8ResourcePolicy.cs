namespace Lokad.Utf8Regex;

/// <summary>Invocation resource budgets shared by all three regex profiles.</summary>
/// <remarks>Every budget is nullable with <see langword="null"/> meaning unlimited. Zero is never a valid budget: use <see langword="null"/> (or <see cref="Unlimited"/>) for no cap. Profiles enforce only the budgets they implement and reject the rest loudly instead of silently relaxing them.</remarks>
public readonly record struct Utf8ResourcePolicy
{
    /// <summary>Gets the maximum cumulative execution step count (units and scope depend on the profile), or <see langword="null"/> for no cap.</summary>
    public ulong? MaxWorkSteps { get; init; }

    /// <summary>Gets the maximum live backtracking/continuation frames or pending linear alternatives, or <see langword="null"/> for no cap.</summary>
    /// <remarks>Depth is measured by the selected execution route. A linear replay without a pending stack uses no depth allowance.
    /// This bounds execution state nesting, not pattern nesting, total work, workspace/table width or process memory.
    /// The core and Python-compatible adapter report <see cref="Utf8ResourceLimitKind.ExecutionDepth"/> on denial; other profiles keep their failure vocabulary.</remarks>
    public uint? MaxDepth { get; init; }

    /// <summary>Gets the maximum modeled active working capacity in bytes, or <see langword="null"/> for no cap.</summary>
    /// <remarks>The core and Python-compatible adapter admit major arrays and output controls before acquisition, including old/new growth overlap.
    /// Exclusive scratch releases synchronously; published copied-cursor maps/tables remain charged for the invocation even after collection.
    /// Borrowed subjects/programs, incidental owner/object metadata, preparation and caller-retained results have separate scope.
    /// This is a conservative covered-capacity bound, not exact GC traffic, live caller retention or process memory. Other profiles retain their own working scope.</remarks>
    public ulong? MaxWorkingBytes { get; init; }

    /// <summary>Gets the maximum cumulative modeled result/staging capacity in bytes, or <see langword="null"/> for no cap.</summary>
    /// <remarks>The core and Python-compatible adapter charge major array/text and staging extents before acquisition, including growth and repeated materialization. Core output controls are included.
    /// Charges are conservative and are not refunded. Borrowed subjects, fixed value results and later standalone getters are outside this scope.
    /// This is not a live-retention or process-memory cap. The remaining optional profile currently rejects this field.</remarks>
    public ulong? MaxResultBytes { get; init; }

    /// <summary>Gets the policy with every budget unlimited.</summary>
    public static Utf8ResourcePolicy Unlimited => default;

    /// <summary>Rejects zero budgets, which cannot express a cap.</summary>
    /// <param name="paramName">The name of the policy parameter to report.</param>
    /// <exception cref="ArgumentOutOfRangeException">A budget is zero. Use <see langword="null"/> for unlimited.</exception>
    internal void Validate(string paramName)
    {
        if (MaxWorkSteps == 0 || MaxDepth == 0 || MaxWorkingBytes == 0 || MaxResultBytes == 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Resource budgets must be null (unlimited) or at least one.");
        }
    }
}
