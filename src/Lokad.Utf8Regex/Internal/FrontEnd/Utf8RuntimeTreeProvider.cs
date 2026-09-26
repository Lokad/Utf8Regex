namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8RuntimeTreeProvider
{
    // Parser seam (cut over 2026-09-26): the owned parser is the sole tree
    // source. Declined shapes return null so construction and execution fall
    // through to the BCL-compatibility fallback with BCL-exact verdicts. The
    // vendored Runtime/* fork was removed by forward deletion (no-vendor rule);
    // BCL Regex stays a test/benchmark oracle only.
    public static Utf8OwnedRegexTree? TryParse(string pattern, RegexOptions options)
    {
        return Utf8OwnedTreeParser.TryParse(pattern, options);
    }
}
