namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static class Utf8RuntimeTreeProvider
{
    // Strangler seam: still legacy-only. An owned-first attempt here degraded
    // 22 search-plan classifications because owned trees carry no find
    // optimizations yet; it returns once owned find optimizations land.
    // TryParseLegacy stays as the differential-battery oracle.
    public static RuntimeFrontEnd.RegexTree? TryParse(string pattern, RegexOptions options)
    {
        return TryParseLegacy(pattern, options);
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
