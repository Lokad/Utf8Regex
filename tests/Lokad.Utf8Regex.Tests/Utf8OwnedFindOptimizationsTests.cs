using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned find-optimization facts, first slice: the differential battery pins owned
// lengths and anchors identical to legacy instances (oracle first, then owned)
// across the probed shape catalog.
public sealed class Utf8OwnedFindOptimizationsTests
{
    public static IEnumerable<object?[]> FactsCases()
    {
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, 3, 3, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a|b", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a)", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a*", RegexOptions.CultureInvariant, 0, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a?", RegexOptions.CultureInvariant, 0, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a{2}", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a{2,4}", RegexOptions.CultureInvariant, 2, 4, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a{2,}", RegexOptions.CultureInvariant, 2, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a+)(b+)", RegexOptions.CultureInvariant, 2, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "()", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a|)", RegexOptions.CultureInvariant, 0, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "|", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(ab)+", RegexOptions.CultureInvariant, 2, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a+?", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a{0}", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { ".", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "^", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Beginning, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "$", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.EndZ, RuntimeFrontEnd.RegexNodeKind.EndZ };
        yield return new object?[] { "^a", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Beginning, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a$", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.EndZ };
        yield return new object?[] { "a.*b", RegexOptions.CultureInvariant, 2, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { ".+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "ab.cd", RegexOptions.CultureInvariant, 5, 5, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "[a-z]+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "[^a]+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\bhi\\B", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\A\\z", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Beginning, RuntimeFrontEnd.RegexNodeKind.End };
        yield return new object?[] { "a^b", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a$b", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "^*", RegexOptions.CultureInvariant, 0, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\b+", RegexOptions.CultureInvariant, 0, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(.)+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a{1}", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a){1}", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a{1,1}", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?m:^a)", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Bol, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?s:.)", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?#c)a", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "[ab]+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "x[yz]", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?:ab){2000000000}", RegexOptions.CultureInvariant, 2147483646, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a|bc|def)", RegexOptions.CultureInvariant, 1, 3, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "^[ab]+$", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Beginning, RuntimeFrontEnd.RegexNodeKind.EndZ };
        yield return new object?[] { "\\d+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\w*", RegexOptions.CultureInvariant, 0, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\s?", RegexOptions.CultureInvariant, 0, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?=a)a", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?!a)a", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?>ab)c", RegexOptions.CultureInvariant, 3, 3, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a)\\1", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?<x>a)", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\p{L}+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a*)*", RegexOptions.CultureInvariant, 0, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a+)+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?:a|)", RegexOptions.CultureInvariant, 0, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?:ab){3}(?:cd){2}", RegexOptions.CultureInvariant, 10, 10, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a)(b)(c)", RegexOptions.CultureInvariant, 3, 3, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\G", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Start, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\z", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.End, RuntimeFrontEnd.RegexNodeKind.End };
        yield return new object?[] { "\\B", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\b", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\Z", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.EndZ, RuntimeFrontEnd.RegexNodeKind.EndZ };
        yield return new object?[] { "a\\G", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a\\z", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.End };
        yield return new object?[] { "a\\B", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a\\b", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?m:$)", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Eol };
        yield return new object?[] { "^$", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Beginning, RuntimeFrontEnd.RegexNodeKind.EndZ };
        yield return new object?[] { "$^", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.EndZ, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?!)", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?=)", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a*)", RegexOptions.CultureInvariant, 0, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a?)?", RegexOptions.CultureInvariant, 0, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?:a{0})", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a{0,0}", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?!)a", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a{2147483647}", RegexOptions.CultureInvariant, 2147483647, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?:ab){2147483647}", RegexOptions.CultureInvariant, 2147483646, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?:a{2147483647}){2}", RegexOptions.CultureInvariant, 2147483646, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "x{0}y", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(x{0})", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\Ghi", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Start, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "hi\\G", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "[a]{2147483647}", RegexOptions.CultureInvariant, 2147483647, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?m:\\G)", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Start, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?:a){1500000000}(?:b){1500000000}", RegexOptions.CultureInvariant, 2147483646, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "^a|^b", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "^a|b", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(^a)b", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Beginning, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "((^a))", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Beginning, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?:^a)b", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Beginning, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(^a)+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(^a)*", RegexOptions.CultureInvariant, 0, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(ab$)", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.EndZ };
        yield return new object?[] { "(a$)", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.EndZ };
        yield return new object?[] { "(a$|b$)", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a|^b", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(^a|^b)", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "((((((((((a))))))))))", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(ab$|ab)", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(a$)+", RegexOptions.CultureInvariant, 1, null, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?=^a)a", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?!^a)a", RegexOptions.CultureInvariant, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?>^a)b", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Beginning, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?>a$)b", RegexOptions.CultureInvariant, 2, 2, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "a", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "^", RegexOptions.CultureInvariant | RegexOptions.Multiline, 0, 0, RuntimeFrontEnd.RegexNodeKind.Bol, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { ".", RegexOptions.CultureInvariant | RegexOptions.Singleline, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "[ab]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1, 1, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "\\b", RegexOptions.CultureInvariant | RegexOptions.ECMAScript, 0, 0, RuntimeFrontEnd.RegexNodeKind.Unknown, RuntimeFrontEnd.RegexNodeKind.Unknown };
        yield return new object?[] { "(?m:^)", RegexOptions.CultureInvariant, 0, 0, RuntimeFrontEnd.RegexNodeKind.Bol, RuntimeFrontEnd.RegexNodeKind.Unknown };
    }

    [Theory]
    [MemberData(nameof(FactsCases))]
    public void OwnedFactsMatchLegacy(string pattern, RegexOptions options, int min, int? max, object lead, object trail)
    {
        var expectedLead = (RuntimeFrontEnd.RegexNodeKind)lead;
        var expectedTrail = (RuntimeFrontEnd.RegexNodeKind)trail;
        var tree = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, options);
        Assert.NotNull(tree);
        var expected = new RuntimeFrontEnd.RegexFindOptimizations(tree.Root, options, tree.Culture);
        Assert.Equal(min, expected.MinRequiredLength);
        Assert.Equal(max, expected.MaxPossibleLength);
        Assert.Equal(expectedLead, expected.LeadingAnchor);
        Assert.Equal(expectedTrail, expected.TrailingAnchor);
        Assert.Equal(min, Utf8OwnedFindOptimizations.ComputeMinLength(tree.Root));
        Assert.Equal(max, Utf8OwnedFindOptimizations.ComputeMaxLength(tree.Root));
        Assert.Equal(expectedLead, Utf8OwnedFindOptimizations.FindLeadingAnchor(tree.Root));
        Assert.Equal(expectedTrail, Utf8OwnedFindOptimizations.FindTrailingAnchor(tree.Root));
    }
}
