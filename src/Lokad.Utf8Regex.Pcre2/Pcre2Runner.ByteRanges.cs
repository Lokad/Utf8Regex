using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal static partial class Pcre2Runner
{
    // Same compiled programs and native finders; only final capture shaping
    // differs from MatchDetailed. No UTF-16 coordinate projection is requested.
    internal static Pcre2BacktrackingMatch MatchByteRanges(Pcre2CompiledProgram compiled,
        ref Utf8ValidatedInput input, Utf8BytePosition start, Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions options, ref Pcre2ResourceBudget budget)
    {
        Pcre2CharacterMatch match;
        switch (compiled.Operations.Match)
        {
            case Pcre2LiteralDirectProgram literal:
                var literalMatch = Pcre2LiteralRunner.Match(literal.Program, ref input, start, options, ref budget);
                match = literalMatch.Success ? new(true, literalMatch.StartOffsetInBytes, literalMatch.EndOffsetInBytes,
                    literalMatch.StartOffsetInBytes, literalMatch.ConsumedEndOffsetInBytes, false) : default;
                break;
            case Pcre2CharacterDirectProgram character:
                match = Pcre2CharacterRunner.Match(character.Program, ref input, start, options, ref budget);
                break;
            case Pcre2SingleTokenRepeatDirectProgram repeat:
                match = ExecuteSingleTokenRepeatOrFallback(repeat.Program, compiled.CandidateSearch, ref input, start, options, ref budget);
                break;
            case Pcre2LiteralFamilyDirectProgram family when options == Pcre2MatchOptions.None &&
                family.Regex.TryFindNativeLiteralForResult(input.Bytes, start.Value, ref budget.WorkingInvocation,
                    out var found, out var begin, out var end):
                match = found ? Pcre2CharacterMatch.Create(begin, end) : default;
                break;
            default:
                var program = compiled.Operations.Match switch
                {
                    Pcre2BacktrackingDirectProgram vm => vm.Program,
                    Pcre2SliceDirectProgram slice => slice.Fallback,
                    Pcre2LiteralFamilyDirectProgram literals => literals.Fallback,
                    _ => compiled.Operations.Enumerate switch
                    {
                        Pcre2MultilinePrefixDirectProgram prefix => prefix.Fallback,
                        Pcre2SameStartLiteralDirectProgram same => same.Fallback,
                        Pcre2LiteralPrefixRepeatDirectProgram literalPrefix => literalPrefix.Fallback,
                        _ => throw new NotSupportedException("SPEC-PCRE2 does not support this pattern for range discovery.")
                    }
                };
                return Pcre2BacktrackingRunner.MatchByteRanges(program, compiled.CandidateSearch, ref input,
                    start, firstMatchingPosition, options, ref budget);
        }
        if (!match.Success) return default;
        var groups = Pcre2BacktrackingRunner.MaterializeByteGroups(1, match.StartOffsetInBytes,
            match.EndOffsetInBytes, [], [], input.Bytes, ref budget);
        return new(true, match.StartOffsetInBytes, match.EndOffsetInBytes,
            match.ConsumedStartOffsetInBytes, match.ConsumedEndOffsetInBytes, match.MatchBoundaryWasReset,
            Pcre2BacktrackingCaptureResult.FromByteGroups(groups), null);
    }
}
