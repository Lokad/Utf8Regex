namespace Lokad.Utf8Regex;

/// <summary>Invocation resource budgets shared by all three regex profiles.</summary>
/// <remarks>Every budget is nullable with <see langword="null"/> meaning unlimited. Zero is never a valid budget: use <see langword="null"/> (or <see cref="Unlimited"/>) for no cap. Profiles enforce only the budgets they implement and reject the rest loudly instead of silently relaxing them.</remarks>
public readonly record struct Utf8ResourcePolicy
{
    /// <summary>Gets the maximum cumulative execution step count (units and scope depend on the profile), or <see langword="null"/> for no cap.</summary>
    public ulong? MaxWorkSteps { get; init; }

    /// <summary>Gets the maximum backtracking or recursive execution depth, or <see langword="null"/> for no cap.</summary>
    public uint? MaxDepth { get; init; }

    /// <summary>Gets the maximum metered workspace size in bytes, or <see langword="null"/> for no cap.</summary>
    public ulong? MaxWorkingBytes { get; init; }

    /// <summary>Gets the maximum result-materialization size in bytes, or <see langword="null"/> for no cap. Reserved: no profile enforces it yet.</summary>
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
