namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8RegexSyntax
{
    public static RegexOptions NormalizeNonSemanticOptions(RegexOptions options)
    {
        return options & ~RegexOptions.Compiled;
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
