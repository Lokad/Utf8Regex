using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8FallbackRegexFamilyAnalyzer
{
    public static bool TryParseAnchoredAsciiDigitsQueryWhole(string pattern, RegexOptions options, out Utf8FallbackUrlPayload payload)
    {
        payload = default;
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        if (!IsAsciiIgnoreCaseOptionSet(options))
        {
            return false;
        }

        var index = 0;
        if (!(TryConsume(pattern, ref index, "^") &&
            TryConsumeNamedCapturedOrRaw(pattern, ref index, ".*") &&
            TryReadAsciiPrefixAlternation(pattern, ref index, out var primaryPrefixUtf8, out var secondaryPrefixUtf8, out var relativePrefixUtf8) &&
            TryConsumeNamedOptional(pattern, ref index, "/[a-zA-Z0-9]+") &&
            TryReadEscapedAsciiLiteral(pattern, ref index, out var routeMarkerUtf8) &&
            TryConsumeNamedCapturedOrRaw(pattern, ref index, "\\d+") &&
            TryConsume(pattern, ref index, "/?") &&
            TryReadRequiredQuestionParameterMarker(pattern, ref index, out var requiredParameterUtf8) &&
            TryConsumeNamedCapturedOrRaw(pattern, ref index, "[^ ?]+") &&
            TryConsumeNamedCapturedOrRaw(pattern, ref index, ".*") &&
            TryConsume(pattern, ref index, "$") &&
            index == pattern.Length))
        {
            return false;
        }

        payload = new Utf8FallbackUrlPayload(
            primaryPrefixUtf8,
            secondaryPrefixUtf8,
            relativePrefixUtf8,
            routeMarkerUtf8,
            requiredParameterUtf8,
            null);
        return true;
    }

    public static bool TryParseAnchoredAsciiHexQueryWhole(string pattern, RegexOptions options, out Utf8FallbackUrlPayload payload)
    {
        payload = default;
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        if (!IsAsciiIgnoreCaseOptionSet(options))
        {
            return false;
        }

        var index = 0;
        if (!(TryConsume(pattern, ref index, "^") &&
            TryConsumeNamedCapturedOrRaw(pattern, ref index, ".*") &&
            TryReadAsciiPrefixAlternation(pattern, ref index, out var primaryPrefixUtf8, out var secondaryPrefixUtf8, out var relativePrefixUtf8) &&
            TryConsumeNamedOptional(pattern, ref index, "/[a-zA-Z0-9]+") &&
            TryReadEscapedAsciiLiteral(pattern, ref index, out var routeMarkerUtf8) &&
            TryConsumeNamedCapturedOrRaw(pattern, ref index, "[a-fA-F0-9]*") &&
            TryReadNamedOptionalParameterValue(pattern, ref index, out var optionalParameterUtf8) &&
            TryReadQueryParameterMarker(pattern, ref index, out var requiredParameterUtf8) &&
            TryConsumeNamedCapturedOrRaw(pattern, ref index, "[^& ]+") &&
            TryReadNamedOptionalParameterValue(pattern, ref index, out var secondOptionalParameterUtf8) &&
            optionalParameterUtf8.AsSpan().SequenceEqual(secondOptionalParameterUtf8) &&
            TryConsumeNamedCapturedOrRaw(pattern, ref index, ".*") &&
            TryConsume(pattern, ref index, "$") &&
            index == pattern.Length))
        {
            return false;
        }

        payload = new Utf8FallbackUrlPayload(
            primaryPrefixUtf8,
            secondaryPrefixUtf8,
            relativePrefixUtf8,
            routeMarkerUtf8,
            requiredParameterUtf8,
            optionalParameterUtf8);
        return true;
    }

    private static bool TryReadAsciiPrefixAlternation(
        string pattern,
        ref int index,
        out byte[]? primaryPrefixUtf8,
        out byte[]? secondaryPrefixUtf8,
        out byte[]? relativePrefixUtf8)
    {
        primaryPrefixUtf8 = null;
        secondaryPrefixUtf8 = null;
        relativePrefixUtf8 = null;
        var original = index;
        if (!TryConsume(pattern, ref index, "(") ||
            !TryReadAsciiLiteralWithOptionalSegment(pattern, ref index, out primaryPrefixUtf8, out secondaryPrefixUtf8) ||
            !TryConsume(pattern, ref index, "|") ||
            !TryReadEscapedAsciiLiteral(pattern, ref index, out relativePrefixUtf8) ||
            !TryConsume(pattern, ref index, ")"))
        {
            index = original;
            primaryPrefixUtf8 = null;
            secondaryPrefixUtf8 = null;
            relativePrefixUtf8 = null;
            return false;
        }

        return primaryPrefixUtf8 is { Length: > 0 } &&
            secondaryPrefixUtf8 is { Length: > 0 } &&
            relativePrefixUtf8 is { Length: > 0 };
    }

    private static bool TryReadAsciiLiteralWithOptionalSegment(
        string pattern,
        ref int index,
        out byte[]? primaryUtf8,
        out byte[]? secondaryUtf8)
    {
        primaryUtf8 = null;
        secondaryUtf8 = null;
        var original = index;
        var builder = new StringBuilder();
        while ((uint)index < (uint)pattern.Length)
        {
            if (pattern[index] == '(')
            {
                if (!TryReadOptionalLiteralGroup(pattern, ref index, out var optionalUtf8) ||
                    !TryReadEscapedAsciiLiteral(pattern, ref index, out var suffixUtf8))
                {
                    index = original;
                    return false;
                }

                var prefixUtf8 = Encoding.ASCII.GetBytes(builder.ToString());
                primaryUtf8 = [.. prefixUtf8, .. suffixUtf8];
                secondaryUtf8 = [.. prefixUtf8, .. optionalUtf8!, .. suffixUtf8];
                return true;
            }

            if (pattern[index] is '|' or ')')
            {
                index = original;
                return false;
            }

            if (!TryReadEscapedAsciiChar(pattern, ref index, out var ch))
            {
                index = original;
                return false;
            }

            builder.Append(ch);
        }

        index = original;
        return false;
    }

    private static bool TryReadOptionalLiteralGroup(string pattern, ref int index, out byte[]? optionalUtf8)
    {
        optionalUtf8 = null;
        var original = index;
        if (!TryConsume(pattern, ref index, "(") && !TryConsume(pattern, ref index, "(?:"))
        {
            return false;
        }

        if (!TryReadEscapedAsciiLiteral(pattern, ref index, out optionalUtf8) ||
            !TryConsume(pattern, ref index, ")?"))
        {
            index = original;
            optionalUtf8 = null;
            return false;
        }

        return true;
    }

    private static bool TryReadNamedOptionalParameterValue(string pattern, ref int index, out byte[]? parameterUtf8)
    {
        parameterUtf8 = null;
        var original = index;
        if (!TryConsume(pattern, ref index, "(?<"))
        {
            return false;
        }

        var closeIndex = pattern.IndexOf('>', index);
        if (closeIndex < 0)
        {
            index = original;
            return false;
        }

        index = closeIndex + 1;
        if (!TryReadParameterNameMarker(pattern, ref index, out parameterUtf8) ||
            !TryConsumeNamedCapturedOrRaw(pattern, ref index, "[^& \\n]+") ||
            !TryConsume(pattern, ref index, ")?"))
        {
            index = original;
            parameterUtf8 = null;
            return false;
        }

        return true;
    }

    private static bool TryReadRequiredQuestionParameterMarker(string pattern, ref int index, out byte[]? parameterUtf8)
    {
        parameterUtf8 = null;
        var original = index;
        if (!TryConsume(pattern, ref index, "\\?"))
        {
            return false;
        }

        if (!TryReadEscapedAsciiLiteral(pattern, ref index, out parameterUtf8) ||
            parameterUtf8.Length == 0 ||
            parameterUtf8[^1] != (byte)'=')
        {
            index = original;
            parameterUtf8 = null;
            return false;
        }

        parameterUtf8 = parameterUtf8[..^1];
        return true;
    }

    private static bool TryReadQueryParameterMarker(string pattern, ref int index, out byte[]? parameterUtf8)
        => TryReadParameterNameMarker(pattern, ref index, out parameterUtf8);

    private static bool TryReadParameterNameMarker(string pattern, ref int index, out byte[]? parameterUtf8)
    {
        parameterUtf8 = null;
        var original = index;
        if (!TryConsume(pattern, ref index, "[?&]"))
        {
            return false;
        }

        if (!TryReadEscapedAsciiLiteralUntil(pattern, ref index, out parameterUtf8, '[') ||
            parameterUtf8.Length == 0 ||
            parameterUtf8[^1] != (byte)'=')
        {
            index = original;
            parameterUtf8 = null;
            return false;
        }

        parameterUtf8 = parameterUtf8[..^1];
        return true;
    }

    private static bool TryReadEscapedAsciiLiteralUntil(string pattern, ref int index, out byte[] bytes, char stopChar)
    {
        bytes = [];
        var builder = new StringBuilder();
        while ((uint)index < (uint)pattern.Length &&
               pattern[index] is not '|' and not ')' and not '(' &&
               pattern[index] != stopChar)
        {
            if (!TryReadEscapedAsciiChar(pattern, ref index, out var ch))
            {
                bytes = [];
                return false;
            }

            builder.Append(ch);
        }

        if (builder.Length == 0)
        {
            return false;
        }

        bytes = Encoding.ASCII.GetBytes(builder.ToString());
        return true;
    }

    private static bool TryReadEscapedAsciiLiteral(string pattern, ref int index, out byte[] bytes)
    {
        bytes = [];
        var builder = new StringBuilder();
        while ((uint)index < (uint)pattern.Length && pattern[index] is not '|' and not ')' and not '(')
        {
            if (!TryReadEscapedAsciiChar(pattern, ref index, out var ch))
            {
                bytes = [];
                return false;
            }

            builder.Append(ch);
        }

        if (builder.Length == 0)
        {
            return false;
        }

        bytes = Encoding.ASCII.GetBytes(builder.ToString());
        return true;
    }

    private static bool TryReadEscapedAsciiChar(string pattern, ref int index, out char ch)
    {
        ch = '\0';
        if ((uint)index >= (uint)pattern.Length)
        {
            return false;
        }

        if (pattern[index] == '\\')
        {
            index++;
            if ((uint)index >= (uint)pattern.Length)
            {
                return false;
            }
        }

        ch = pattern[index++];
        return ch <= 0x7F;
    }
}
