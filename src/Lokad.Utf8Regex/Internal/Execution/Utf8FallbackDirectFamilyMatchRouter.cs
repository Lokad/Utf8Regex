using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackDirectFamilyMatchRouter
{
    public static bool TryMatchAsciiWellFormedOnly(
        ReadOnlySpan<byte> input,
        in Utf8FallbackDirectFamilyPlan directFamily,
        Utf8EmittedTokenFamilyMatcher? emittedTokenFamilyMatcher,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out Utf8ValueMatch match)
    {
        if (Utf8AsciiDirectFamilyExecutor.TryFindMatch(
                input,
                directFamily,
                emittedTokenFamilyMatcher,
                delimitedTokenSearch,
                literalStructuredTokenSearch,
                out _,
                out var matchedLength) &&
            directFamily.Kind != Utf8FallbackDirectFamilyKind.AnchoredQuotedStringPrefix)
        {
            match = new Utf8ValueMatch(true, true, 0, matchedLength, 0, matchedLength);
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public static bool TryMatchWellFormedOnly(
        ReadOnlySpan<byte> input,
        in Utf8FallbackDirectFamilyPlan directFamily,
        Utf8EmittedTokenFamilyMatcher? emittedTokenFamilyMatcher,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out Utf8ValueMatch match)
    {
        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredQuotedStringPrefix &&
            Utf8AsciiPrefixTokenExecutor.TryMatchQuotedStringPrefix(input, out var stringByteLength))
        {
            var stringUtf16Length = input[..stringByteLength].IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0
                ? stringByteLength
                : Utf8Validation.Validate(input[..stringByteLength]).Utf16Length;
            match = new Utf8ValueMatch(true, true, 0, stringUtf16Length, 0, stringByteLength);
            return true;
        }

        if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0)
        {
            return TryMatchAsciiWellFormedOnly(
                input,
                directFamily,
                emittedTokenFamilyMatcher,
                delimitedTokenSearch,
                literalStructuredTokenSearch,
                out match);
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }
}
