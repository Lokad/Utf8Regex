namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static class Utf8RuntimeTreeProvider
{
    public static RuntimeFrontEnd.RegexTree? TryParse(string pattern, RegexOptions options)
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
