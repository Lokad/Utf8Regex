namespace Lokad.Utf8Regex.Internal.Diagnostics;

internal enum Utf8ExecutionRoute : byte
{
    None = 0, CompiledFusedAsciiLiteralFamilyCount, CompiledFusedAsciiLiteralCount, CompiledFusedUtf8LiteralCount, CompiledFusedUtf8LiteralFamilyCount, RequiredPrefilterReject, FallbackDirectRegex, LiteralFamilyHybridEarliestAutomaton, LiteralFamilyPreparedNonOverlapping, LiteralFamilyPreparedWithRequirementsLengthOnly, LiteralFamilyPreparedWithRequirementsMatch, LiteralFamilyPackedNibbleSimdCount, FallbackDirectAnchoredAsciiLiteralPrefix, FallbackDirectAnchoredAsciiLiteralSuffix, FallbackDirectAnchoredDigitsTail
}

internal static class Utf8ExecutionRouteText
{
    public static string Format(this Utf8ExecutionRoute route) => route switch
    {
        Utf8ExecutionRoute.None => "<none>",
        Utf8ExecutionRoute.CompiledFusedAsciiLiteralFamilyCount => "compiled_fused_ascii_literal_family_count",
        Utf8ExecutionRoute.CompiledFusedAsciiLiteralCount => "compiled_fused_ascii_literal_count",
        Utf8ExecutionRoute.CompiledFusedUtf8LiteralCount => "compiled_fused_utf8_literal_count",
        Utf8ExecutionRoute.CompiledFusedUtf8LiteralFamilyCount => "compiled_fused_utf8_literal_family_count",
        Utf8ExecutionRoute.RequiredPrefilterReject => "required_prefilter_reject",
        Utf8ExecutionRoute.FallbackDirectRegex => "fallback_direct_regex",
        Utf8ExecutionRoute.LiteralFamilyHybridEarliestAutomaton => "literal_family_hybrid_earliest_automaton",
        Utf8ExecutionRoute.LiteralFamilyPreparedNonOverlapping => "literal_family_prepared_non_overlapping",
        Utf8ExecutionRoute.LiteralFamilyPreparedWithRequirementsLengthOnly => "literal_family_prepared_with_requirements_length_only",
        Utf8ExecutionRoute.LiteralFamilyPreparedWithRequirementsMatch => "literal_family_prepared_with_requirements_match",
        Utf8ExecutionRoute.LiteralFamilyPackedNibbleSimdCount => "literal_family_packed_nibble_simd_count",
        Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix => "fallback_direct_anchored_ascii_literal_prefix",
        Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix => "fallback_direct_anchored_ascii_literal_suffix",
        _ => throw new InvalidOperationException("Unknown UTF-8 execution route.")
    };
}
