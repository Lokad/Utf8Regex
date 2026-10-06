using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public static class Utf8BmpGrammarPatternCatalog
{
    public static IEnumerable<object[]> Patterns()
    {
        string[] patterns = [
            "([^x]b)+", "(.+?)", "((.)+)", "(.{2,3})", "(.{2,}?)", "((.){2})",
            "((.){2})\\1", "(\\D{2,}?)", "(?=(.))", "(?<=(.))a", "(?<=.+)a",
            "([a\\p{IsHighSurrogates}]{2,})", "([a\\p{IsLowSurrogates}]{2,})",
            "([^\\p{Cs}]{2,})", "([\\p{Cs}]{2,})", "(\\W+?)", "(\\D*)",
            "(?i)([^x]+?)", "(a|.)+", "(?<x>.)(?<-x>.)", "(?<=.)\\b",
            "\\G(.)", "(?>.{1,2}).", "(?<x>.)(?(x).|a)",
        ];
        foreach (var pattern in patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [pattern, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

}
