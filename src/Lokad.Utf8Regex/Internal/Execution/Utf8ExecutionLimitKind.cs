namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Identifies which execution budget a charge exceeded, if any.
/// </summary>
internal enum Utf8ExecutionLimitKind : byte
{
    None = 0,
    WorkSteps = 1,
    Depth = 2,
    WorkingBytes = 3,
    ResultBytes = 4,
}
