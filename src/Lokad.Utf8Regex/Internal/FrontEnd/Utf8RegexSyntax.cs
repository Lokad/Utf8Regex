namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8RegexSyntax
{
    public static RegexOptions NormalizeNonSemanticOptions(RegexOptions options)
    {
        return options & ~RegexOptions.Compiled;
    }

    /// <summary>
    /// Owned option validation matching <c>System.Text.RegularExpressions.Regex.ValidateOptions</c>
    /// at <c>external/runtime</c> <c>9ffface2f3f</c>. .NET 11 runtime support
    /// retains this admitted set; its new AnyNewLine option is unsupported. Throws <see cref="ArgumentOutOfRangeException"/> with
    /// <c>ParamName == "options"</c> for undefined bits and for the two documented
    /// incompatible combinations (ECMAScript scope and NonBacktracking scope).
    /// This replaces the former BCL-construction probe without changing observable behavior.
    /// </summary>
    public static void ValidateOptions(RegexOptions options)
    {
        const int MaxOptionShift = 11;
        if (((((uint)options) >> MaxOptionShift) != 0) ||
            ((options & RegexOptions.ECMAScript) != 0 && (options & ~(RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant)) != 0) ||
            ((options & RegexOptions.NonBacktracking) != 0 && (options & (RegexOptions.ECMAScript | RegexOptions.RightToLeft)) != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    public static bool IsRegexMetaCharacter(char ch)
    {
        return ch is '\\' or '.' or '$' or '^' or '{' or '[' or '(' or '|' or ')' or '*' or '+' or '?';
    }

    public static string? ClassifyUnsupportedOptions(RegexOptions options)
    {
        options = NormalizeNonSemanticOptions(options);

        var allowedOptions =
            RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase |
            RegexOptions.Multiline |
            RegexOptions.Singleline |
            RegexOptions.ExplicitCapture |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.RightToLeft;
        if ((options & ~allowedOptions) != 0)
        {
            return "unsupported_options";
        }

        if ((options & RegexOptions.IgnoreCase) != 0 &&
            (options & RegexOptions.CultureInvariant) == 0)
        {
            return "culture_sensitive_ignore_case";
        }

        return null;
    }
}
