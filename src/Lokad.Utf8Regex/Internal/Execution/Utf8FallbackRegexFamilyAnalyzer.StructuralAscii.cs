using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8FallbackRegexFamilyAnalyzer
{
    public static bool IsAnchoredAsciiHexColorWhole(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        if (!IsAsciiIgnoreCaseOptionSet(options))
        {
            return false;
        }

        var index = 0;
        if (!TryConsume(pattern, ref index, "^"))
        {
            return false;
        }

        _ = TryConsume(pattern, ref index, "#?");

        if (!TryConsume(pattern, ref index, "(") && !TryConsume(pattern, ref index, "(?:"))
        {
            return false;
        }

        if (!TryConsume(pattern, ref index, "[a-f0-9]{6}") ||
            !TryConsume(pattern, ref index, "|") ||
            !TryConsume(pattern, ref index, "[a-f0-9]{3}") ||
            !TryConsume(pattern, ref index, ")") ||
            !TryConsume(pattern, ref index, "$"))
        {
            return false;
        }

        return index == pattern.Length;
    }

    public static bool IsAnchoredAsciiCellReferenceWhole(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        if ((options & RegexOptions.IgnoreCase) == 0 || pattern.Length == 0)
        {
            return false;
        }

        var index = 0;
        return TryConsume(pattern, ref index, "^") &&
            TryConsumeNamedCapturedOrRaw(pattern, ref index, "[a-z]") &&
            TryConsumeAsciiDigitLoop(pattern, ref index) &&
            TryConsume(pattern, ref index, "$") &&
            index == pattern.Length;
    }

    public static bool IsAnchoredAsciiRangeReferenceWhole(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        if ((options & RegexOptions.IgnoreCase) == 0 || pattern.Length == 0)
        {
            return false;
        }

        var index = 0;
        if (!TryConsume(pattern, ref index, "^") ||
            !TryConsumeNamedCapturedOrRaw(pattern, ref index, "[a-z]") ||
            !TryConsumeAsciiDigitLoop(pattern, ref index))
        {
            return false;
        }

        _ = TryConsume(pattern, ref index, ":?");

        return TryConsumeNamedCapturedOrRaw(pattern, ref index, "[a-z]") &&
            TryConsumeAsciiDigitLoop(pattern, ref index) &&
            TryConsume(pattern, ref index, "$") &&
            index == pattern.Length;
    }

    private static bool TryConsumeNamedCapturedOrRaw(string pattern, ref int index, string token)
    {
        if (TryConsumeCapturedOrRaw(pattern, ref index, token))
        {
            return true;
        }

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
        if (!TryConsume(pattern, ref index, token) ||
            !TryConsume(pattern, ref index, ")"))
        {
            index = original;
            return false;
        }

        return true;
    }

    private static bool TryConsumeNamedOptional(string pattern, ref int index, string token)
    {
        if (TryConsume(pattern, ref index, $"{token}?"))
        {
            return true;
        }

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
        if (!TryConsume(pattern, ref index, token) ||
            !TryConsume(pattern, ref index, ")?"))
        {
            index = original;
            return false;
        }

        return true;
    }

    private static bool TryConsumeAsciiDigitLoop(string pattern, ref int index)
    {
        return TryConsumeNamedCapturedOrRaw(pattern, ref index, "\\d+") ||
            TryConsume(pattern, ref index, "(\\d)+") ||
            TryConsumeNamedCapturedOrRaw(pattern, ref index, "(\\d)+") ||
            TryConsume(pattern, ref index, "((\\d)+)") ||
            TryConsume(pattern, ref index, "(?:\\d)+");
    }
}
