namespace Lokad.Utf8Regex;

/// <summary>Coarse admission for fixed construction metadata.</summary>
public sealed partial class Utf8Regex
{
    // One conservative allowance covers borrowed immutable empty metadata and
    // the four literal-sized frontend arrays; no per-helper collector is needed.
    internal const ulong ProgramFixedMetadataBytes = 16 * 1024;
}
