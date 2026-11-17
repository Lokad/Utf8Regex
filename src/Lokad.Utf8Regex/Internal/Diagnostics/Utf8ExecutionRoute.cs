namespace Lokad.Utf8Regex.Internal.Diagnostics;

internal enum Utf8ExecutionRoute : byte
{
    None = 0, CompiledFusedAsciiLiteralFamilyCount, CompiledFusedAsciiLiteralCount, CompiledFusedUtf8LiteralCount, CompiledFusedUtf8LiteralFamilyCount, RequiredPrefilterReject, NativeAsciiAnchoredValidator, FallbackDirectRegex, FallbackDirectUnicodeLetterBounded, FallbackDirectUnicodeLetter, FallbackDirectUnicodeCategory, FallbackDirectUtf8WordRun, FallbackDirectAsciiDottedDecimalQuad, FallbackDirectAsciiUntilByteStar, FallbackDirectAsciiDelimitedToken, FallbackDirectUtf8WordDelimitedToken, FallbackDirectUtf8UriToken, FallbackDirectAsciiLiteralStructuredToken, FallbackDirectAsciiIdentifierToken, FallbackDirectAsciiUriToken, NativeStructuralLinearAutomaton, LiteralFamilyHybridEarliestAutomaton, LiteralFamilyPreparedNonOverlapping, LiteralFamilyPreparedWithRequirementsLengthOnly, LiteralFamilyPreparedWithRequirementsMatch, LiteralFamilyPackedNibbleSimdCount, NativeOrderedLiteralWindow, NativeStructuralFamilyEmit, NativeStructuralFamilySuffix, NativeStructuralFamilyLinear, FallbackDirectAnchoredAsciiLiteralPrefix, FallbackDirectAnchoredAsciiLiteralSuffix, FallbackDirectAnchoredDigitsTail
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
        Utf8ExecutionRoute.NativeAsciiAnchoredValidator => "native_ascii_anchored_validator",
        Utf8ExecutionRoute.FallbackDirectRegex => "fallback_direct_regex",
        Utf8ExecutionRoute.FallbackDirectUnicodeLetterBounded => "fallback_direct_unicode_letter_bounded",
        Utf8ExecutionRoute.FallbackDirectUnicodeLetter => "fallback_direct_unicode_letter",
        Utf8ExecutionRoute.FallbackDirectUnicodeCategory => "fallback_direct_unicode_category",
        Utf8ExecutionRoute.FallbackDirectUtf8WordRun => "fallback_direct_utf8_word_run",
        Utf8ExecutionRoute.FallbackDirectAsciiDottedDecimalQuad => "fallback_direct_ascii_dotted_decimal_quad",
        Utf8ExecutionRoute.FallbackDirectAsciiUntilByteStar => "fallback_direct_ascii_until_byte_star",
        Utf8ExecutionRoute.FallbackDirectAsciiDelimitedToken => "fallback_direct_ascii_delimited_token",
        Utf8ExecutionRoute.FallbackDirectUtf8WordDelimitedToken => "fallback_direct_utf8_word_delimited_token",
        Utf8ExecutionRoute.FallbackDirectUtf8UriToken => "fallback_direct_utf8_uri_token",
        Utf8ExecutionRoute.FallbackDirectAsciiLiteralStructuredToken => "fallback_direct_ascii_literal_structured_token",
        Utf8ExecutionRoute.FallbackDirectAsciiIdentifierToken => "fallback_direct_ascii_identifier_token",
        Utf8ExecutionRoute.FallbackDirectAsciiUriToken => "fallback_direct_ascii_uri_token",
        Utf8ExecutionRoute.NativeStructuralLinearAutomaton => "native_structural_linear_automaton",
        Utf8ExecutionRoute.LiteralFamilyHybridEarliestAutomaton => "literal_family_hybrid_earliest_automaton",
        Utf8ExecutionRoute.LiteralFamilyPreparedNonOverlapping => "literal_family_prepared_non_overlapping",
        Utf8ExecutionRoute.LiteralFamilyPreparedWithRequirementsLengthOnly => "literal_family_prepared_with_requirements_length_only",
        Utf8ExecutionRoute.LiteralFamilyPreparedWithRequirementsMatch => "literal_family_prepared_with_requirements_match",
        Utf8ExecutionRoute.LiteralFamilyPackedNibbleSimdCount => "literal_family_packed_nibble_simd_count",
        Utf8ExecutionRoute.NativeOrderedLiteralWindow => "native_ordered_literal_window",
        Utf8ExecutionRoute.NativeStructuralFamilyEmit => "native_structural_family_emit",
        Utf8ExecutionRoute.NativeStructuralFamilySuffix => "native_structural_family_suffix",
        Utf8ExecutionRoute.NativeStructuralFamilyLinear => "native_structural_family_linear",
        Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix => "fallback_direct_anchored_ascii_literal_prefix",
        Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix => "fallback_direct_anchored_ascii_literal_suffix",
        Utf8ExecutionRoute.FallbackDirectAnchoredDigitsTail => "fallback_direct_anchored_digits_tail",
        _ => throw new InvalidOperationException("Unknown UTF-8 execution route.")
    };
}
