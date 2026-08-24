using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static partial class Utf8FallbackRegexFamilyAnalyzer
{
    private const string Utf8BalancedBeginEndPattern =
        "BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\\s\\S]))*END(?(open)(?!))";

    public static bool IsUtf8BalancedBeginEndCount(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        return options is RegexOptions.None or RegexOptions.CultureInvariant &&
            pattern == Utf8BalancedBeginEndPattern;
    }
}
