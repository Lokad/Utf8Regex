namespace Lokad.Utf8Regex.Internal.Diagnostics;

internal enum Utf8ExecutionRoute : byte
{
    None = 0,
    CompiledFusedAsciiLiteralFamilyCount,
    CompiledFusedAsciiLiteralCount,
    LiteralFamilyHybridEarliestAutomaton,
    LiteralFamilyPreparedNonOverlapping,
    LiteralFamilyPreparedWithRequirementsLengthOnly,
    LiteralFamilyPreparedWithRequirementsMatch,
    LiteralFamilyPackedNibbleSimdCount,
    FallbackDirectAnchoredAsciiLiteralPrefix,
    FallbackDirectAnchoredAsciiLiteralSuffix
}
