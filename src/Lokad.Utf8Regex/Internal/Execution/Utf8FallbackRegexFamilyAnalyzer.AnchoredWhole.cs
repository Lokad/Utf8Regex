using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8FallbackRegexFamilyAnalyzer
{
    public static bool TryParseAnchoredQuotedLineSegmentCountFamily(
        string pattern,
        RegexOptions options,
        byte[]? requiredPrefilterLiteralUtf8,
        out byte[]? linePrefixUtf8,
        out byte[]? optionalSegmentUtf8)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        linePrefixUtf8 = null;
        optionalSegmentUtf8 = null;

        if (options != RegexOptions.Multiline ||
            requiredPrefilterLiteralUtf8 is not { Length: > 0 } prefix ||
            !TryParseAnchoredQuotedLineSegmentPattern(pattern, out var requiredPrefix, out var optionalSegment) ||
            !requiredPrefix.AsSpan().SequenceEqual(prefix))
        {
            return false;
        }

        linePrefixUtf8 = prefix.ToArray();
        optionalSegmentUtf8 = optionalSegment;
        return true;
    }

    public static bool IsAnchoredAsciiSignedDecimalWhole(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        if (options is not RegexOptions.None and not RegexOptions.CultureInvariant)
        {
            return false;
        }

        if (!pattern.StartsWith("^", StringComparison.Ordinal) ||
            !pattern.EndsWith("$", StringComparison.Ordinal))
        {
            return false;
        }

        var index = 1;
        if (TryConsume(pattern, ref index, "[-+]?"))
        {
        }

        if (!TryConsume(pattern, ref index, "\\d*"))
        {
            return false;
        }

        if (TryConsume(pattern, ref index, "\\.?"))
        {
        }

        if (!TryConsume(pattern, ref index, "\\d*"))
        {
            return false;
        }

        return index == pattern.Length - 1;
    }

    public static bool IsAnchoredAsciiEmailWhole(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        if (options is not (RegexOptions.None or RegexOptions.CultureInvariant))
        {
            return false;
        }

        var index = 0;
        if (!TryConsume(pattern, ref index, "^") ||
            !TryConsumeCapturedOrRaw(pattern, ref index, "[a-zA-Z0-9_\\-\\.]+") ||
            !TryConsume(pattern, ref index, "@"))
        {
            return false;
        }

        if (!TryConsume(pattern, ref index, "(") && !TryConsume(pattern, ref index, "(?:"))
        {
            return false;
        }

        if (!TryConsumeCapturedOrRaw(pattern, ref index, @"\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.") &&
            !TryConsumeCapturedOrRaw(pattern, ref index, @"([a-zA-Z0-9\-]+\.)+"))
        {
            return false;
        }

        if (!TryConsume(pattern, ref index, "|") ||
            !TryConsumeCapturedOrRaw(pattern, ref index, @"([a-zA-Z0-9\-]+\.)+") ||
            !TryConsume(pattern, ref index, ")"))
        {
            return false;
        }

        if (!TryConsumeCapturedOrRaw(pattern, ref index, "[a-zA-Z]{2,12}|[0-9]{1,3}") &&
            !TryConsumeCapturedOrRaw(pattern, ref index, "(?:[a-zA-Z]{2,12}|[0-9]{1,3})"))
        {
            return false;
        }

        if (!TryConsumeCapturedOrRaw(pattern, ref index, @"\]?") ||
            !TryConsume(pattern, ref index, "$"))
        {
            return false;
        }

        return index == pattern.Length;
    }
}
