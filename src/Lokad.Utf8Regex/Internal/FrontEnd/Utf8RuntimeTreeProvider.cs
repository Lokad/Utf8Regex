namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static class Utf8RuntimeTreeProvider
{
    // Strangler seam: owned-first with legacy fallback. The owned parser covers
    // the pinned subset and declines everything else, so uncovered shapes keep
    // exact legacy behavior including construction throws. Search analysis now
    // consumes owned find facts, which is what the reverted attempt was missing.
    // TryParseLegacy stays as the differential-battery oracle.
    public static RuntimeFrontEnd.RegexTree? TryParse(string pattern, RegexOptions options)
    {
        return Utf8OwnedTreeParser.TryParse(pattern, options) ?? TryParseLegacy(pattern, options);
    }

    public static RuntimeFrontEnd.RegexTree? TryParseLegacy(string pattern, RegexOptions options)
    {
        try
        {
            return RuntimeFrontEnd.RegexParser.Parse(
                pattern,
                options,
                RuntimeFrontEnd.RegexParser.GetTargetCulture(options));
        }
        catch (RuntimeFrontEnd.RegexParseException)
        {
            return null;
        }
    }
}
