namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8RuntimeTreeProvider
{
    // The owned parser is the sole tree source and syntax diagnostic owner.
    // Unserved class representations still enter transitional construction/
    // execution routing, which remains removal debt.
    // Pattern nesting uses explicit preparation frames rather than selecting
    // BCL execution. Test comparators apply only on actual scalar overlap.
    public static Utf8OwnedRegexTree? TryParse(string pattern, RegexOptions options)
    {
        return Utf8OwnedTreeParser.TryParse(pattern, options);
    }

    internal static Utf8OwnedRegexTree? TryParse(string pattern, RegexOptions options, out Utf8PatternDiagnostic diagnostic)
        => Utf8OwnedTreeParser.TryParse(pattern, options, out diagnostic);
}
