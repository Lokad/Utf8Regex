using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// Owned find-optimization facts: the pinned battery checks owned lengths,
// anchors, alternate prefixes, fixed-distance literals, and fixed-distance sets
// against hardcoded expectations across the probed shape catalog. The expectations
// were differentially pinned against legacy instances before the cutover; owned
// representation checks remain. Widths now bound scalar UTF-8 bytes, and fixed
// distances require a constant byte prefix. Old code-unit/minimum-distance pins
// were retargeted without dropping their patterns.
public sealed class Utf8OwnedFindOptimizationsTests
{
    public static IEnumerable<object?[]> FactsCases()
    {
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, 3, 3, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a|b", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a)", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a*", RegexOptions.CultureInvariant, 0, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a?", RegexOptions.CultureInvariant, 0, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a{2}", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a{2,4}", RegexOptions.CultureInvariant, 2, 4, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a{2,}", RegexOptions.CultureInvariant, 2, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a+)(b+)", RegexOptions.CultureInvariant, 2, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "()", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a|)", RegexOptions.CultureInvariant, 0, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "|", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(ab)+", RegexOptions.CultureInvariant, 2, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a+?", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a{0}", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { ".", RegexOptions.CultureInvariant, 1, 4, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "^", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "$", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.EndZ };
        yield return new object?[] { "^a", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a$", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.EndZ };
        yield return new object?[] { "a.*b", RegexOptions.CultureInvariant, 2, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { ".+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "ab.cd", RegexOptions.CultureInvariant, 5, 8, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "[a-z]+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "[^a]+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\bhi\\B", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\A\\z", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.End };
        yield return new object?[] { "a^b", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a$b", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "^*", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\b+", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(.)+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a{1}", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a){1}", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a{1,1}", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?m:^a)", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?s:.)", RegexOptions.CultureInvariant, 1, 4, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?#c)a", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "[ab]+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "x[yz]", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a|bc|def)", RegexOptions.CultureInvariant, 1, 3, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "^[ab]+$", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.EndZ };
        yield return new object?[] { "\\d+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\w*", RegexOptions.CultureInvariant, 0, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\s?", RegexOptions.CultureInvariant, 0, 4, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?=a)a", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?!a)a", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?>ab)c", RegexOptions.CultureInvariant, 3, 3, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a)\\1", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?<x>a)", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\p{L}+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a*)*", RegexOptions.CultureInvariant, 0, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a+)+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?:a|)", RegexOptions.CultureInvariant, 0, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?:ab){3}(?:cd){2}", RegexOptions.CultureInvariant, 10, 10, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a)(b)(c)", RegexOptions.CultureInvariant, 3, 3, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\G", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\z", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.End };
        yield return new object?[] { "\\B", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\b", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\Z", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.EndZ };
        yield return new object?[] { "a\\G", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a\\z", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.End };
        yield return new object?[] { "a\\B", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a\\b", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?m:$)", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Eol };
        yield return new object?[] { "^$", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.EndZ };
        yield return new object?[] { "$^", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?!)", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?=)", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a*)", RegexOptions.CultureInvariant, 0, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a?)?", RegexOptions.CultureInvariant, 0, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?:a{0})", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a{0,0}", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?!)a", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a{2147483647}", RegexOptions.CultureInvariant, 2147483647, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "x{0}y", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(x{0})", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\Ghi", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "hi\\G", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "[a]{2147483647}", RegexOptions.CultureInvariant, 2147483647, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?m:\\G)", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "^a|^b", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "^a|b", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "((^a))", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?:^a)b", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(^a)+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(^a)*", RegexOptions.CultureInvariant, 0, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(ab$)", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.EndZ };
        yield return new object?[] { "(a$)", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.EndZ };
        yield return new object?[] { "(a$|b$)", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a|^b", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(^a|^b)", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "((((((((((a))))))))))", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(ab$|ab)", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(a$)+", RegexOptions.CultureInvariant, 1, null, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?=^a)a", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?!^a)a", RegexOptions.CultureInvariant, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?>^a)b", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?>a$)b", RegexOptions.CultureInvariant, 2, 2, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "a", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "^", RegexOptions.CultureInvariant | RegexOptions.Multiline, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { ".", RegexOptions.CultureInvariant | RegexOptions.Singleline, 1, 4, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "[ab]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1, 1, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "\\b", RegexOptions.CultureInvariant | RegexOptions.ECMAScript, 0, 0, Utf8TreeNodeKind.Unknown };
        yield return new object?[] { "(?m:^)", RegexOptions.CultureInvariant, 0, 0, Utf8TreeNodeKind.Unknown };
    }

    [Theory]
    [MemberData(nameof(FactsCases))]
    public void OwnedFactsMatchPinnedFacts(string pattern, RegexOptions options, int min, int? max, object trail)
    {
        var expectedTrail = (Utf8TreeNodeKind)trail;
        var ownedTree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(ownedTree);
        Assert.Equal(min, Utf8OwnedFindOptimizations.ComputeByteWidth(ownedTree.Root).Minimum);
        Assert.Equal(max, Utf8OwnedFindOptimizations.ComputeByteWidth(ownedTree.Root).Maximum);
        Assert.Equal(expectedTrail, Utf8OwnedFindOptimizations.FindTrailingAnchor(ownedTree.Root));
    }

    public static IEnumerable<object?[]> SaturationCases()
    {
        yield return new object?[] { "(?:ab){2000000000}", 2147483647, null };
        yield return new object?[] { "(?:a){1500000000}(?:b){1500000000}", 2147483647, null };
        yield return new object?[] { "(?:ab){2147483647}", 2147483647, null };
        yield return new object?[] { "(?:a{2147483647}){2}", 2147483647, null };
    }

    [Theory]
    [MemberData(nameof(SaturationCases))]
    public void OwnedSaturationPinsFormulas(string pattern, int min, int? max)
    {
        // The pristine fork wraps these products and sums in 32 bits while the owned hub
        // saturates like the BCL-observed formulas, so these rows pin owned values only.
        var ownedTree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(ownedTree);
        Assert.Equal(min, Utf8OwnedFindOptimizations.ComputeByteWidth(ownedTree.Root).Minimum);
        Assert.Equal(max, Utf8OwnedFindOptimizations.ComputeByteWidth(ownedTree.Root).Maximum);
    }

    public static IEnumerable<object?[]> LiteralCases()
    {
        yield return new object?[] { "(ab)", "ab", "ab" };
        yield return new object?[] { "(ab)c", null, "c" };
        yield return new object?[] { "(a)(b)", null, null };
        yield return new object?[] { "a(b)", "a", "a" };
        yield return new object?[] { "(a)b", null, "b" };
        yield return new object?[] { "^ab", null, "ab" };
        yield return new object?[] { "(^a)", null, "a" };
        yield return new object?[] { "a*bc", null, "bc" };
        yield return new object?[] { "(ab)*c", null, "c" };
        yield return new object?[] { "a(b|c)d", "a", "a" };
        yield return new object?[] { "(ab|ac)", null, null };
        yield return new object?[] { "ab|ab", null, "ab" };
        yield return new object?[] { "a|ab", null, null };
        yield return new object?[] { "(|a)", null, null };
        yield return new object?[] { "a(?:b)", "a", "a" };
        yield return new object?[] { "(?:ab)", "ab", "ab" };
        yield return new object?[] { "((ab))", "ab", "ab" };
        yield return new object?[] { "ab?", "a", "a" };
        yield return new object?[] { "ab+", "a", "a" };
        yield return new object?[] { "a?b", null, "b" };
        yield return new object?[] { "(a)?b", null, "b" };
        yield return new object?[] { "a{2}b", null, "b" };
        yield return new object?[] { "a{2,4}b", null, "b" };
        yield return new object?[] { "[ab]c", null, "c" };
        yield return new object?[] { ".ab", null, "ab" };
        yield return new object?[] { "\\dab", null, "ab" };
        yield return new object?[] { "(?i:ab)", null, null };
        yield return new object?[] { "ab.*cd", "ab", "ab" };
        yield return new object?[] { ".*ab", null, "ab" };
        yield return new object?[] { "a[bc]d", "a", "a" };
        yield return new object?[] { "ab[cd]ef", "ab", "ab" };
        yield return new object?[] { "[ab]cd", null, "cd" };
        yield return new object?[] { "a(b|c)", "a", "a" };
        yield return new object?[] { "(ab|cd)e", null, "e" };
        yield return new object?[] { "ab$", "ab", "ab" };
        yield return new object?[] { "(?>ab)", null, null };
        yield return new object?[] { "a^b", "a", "a" };
        yield return new object?[] { "a$b", "a", "a" };
        yield return new object?[] { "(?=ab)c", null, "c" };
        yield return new object?[] { "(?!ab)c", null, "c" };
        yield return new object?[] { "(a)\\1", null, null };
        yield return new object?[] { "(ab)\\1", null, null };
        yield return new object?[] { "((ab|cd))", null, null };
        yield return new object?[] { "cd|ab", null, null };
        yield return new object?[] { "x{0}y", "y", "y" };
        yield return new object?[] { "(x{0})", null, null };
        yield return new object?[] { "(?:)ab", null, "ab" };
        yield return new object?[] { "(?:)", null, null };
        yield return new object?[] { "(a+)(b+)", null, null };
        yield return new object?[] { "ab|cd|ef", null, null };
        yield return new object?[] { "(ab)cd", null, "cd" };
        yield return new object?[] { "(ab|cd)", null, null };
        yield return new object?[] { "(ab|cd)ef", null, "ef" };
        yield return new object?[] { "(a.bcdef|gh)", null, null };
        yield return new object?[] { "(?:ab|cd)", null, null };
        yield return new object?[] { "a(bc|d)e", "a", "a" };
        yield return new object?[] { "abc", "abc", "abc" };
        yield return new object?[] { "a", "a", "a" };
        yield return new object?[] { "a|b", null, null };
        yield return new object?[] { "(a)", "a", "a" };
        yield return new object?[] { "a*", null, null };
        yield return new object?[] { "a+", null, null };
        yield return new object?[] { "a?", null, null };
        yield return new object?[] { "a{2}", null, null };
        yield return new object?[] { "a{2,4}", null, null };
        yield return new object?[] { "()", null, null };
        yield return new object?[] { "(a|)", null, null };
        yield return new object?[] { "|", null, null };
        yield return new object?[] { "ab|cd", null, null };
        yield return new object?[] { "(ab)+", null, null };
        yield return new object?[] { "a+?", null, null };
        yield return new object?[] { "a{0}", null, null };
        yield return new object?[] { ".", null, null };
        yield return new object?[] { "^", null, null };
        yield return new object?[] { "$", null, null };
        yield return new object?[] { "^a", null, "a" };
        yield return new object?[] { "a$", "a", "a" };
        yield return new object?[] { "a.*b", "a", "a" };
        yield return new object?[] { ".+", null, null };
        yield return new object?[] { "ab.cd", "ab", "ab" };
        yield return new object?[] { "[a-z]+", null, null };
        yield return new object?[] { "[^a]+", null, null };
        yield return new object?[] { "\\bhi\\B", null, "hi" };
        yield return new object?[] { "\\A\\z", null, null };
        yield return new object?[] { "^*", null, null };
        yield return new object?[] { "\\b+", null, null };
        yield return new object?[] { "(.)+", null, null };
        yield return new object?[] { "a{1}", "a", "a" };
        yield return new object?[] { "(a){1}", "a", "a" };
        yield return new object?[] { "a{1,1}", "a", "a" };
        yield return new object?[] { "(?m:^a)", "a", "a" };
        yield return new object?[] { "(?s:.)", null, null };
        yield return new object?[] { "(?#c)a", "a", "a" };
        yield return new object?[] { "[ab]+", null, null };
        yield return new object?[] { "x[yz]", "x", "x" };
        yield return new object?[] { "(?:ab){2000000000}", null, null };
        yield return new object?[] { "(a|bc|def)", null, null };
        yield return new object?[] { "^[ab]+$", null, null };
        yield return new object?[] { "\\d+", null, null };
        yield return new object?[] { "\\w*", null, null };
        yield return new object?[] { "\\s?", null, null };
        yield return new object?[] { "(?=a)a", null, "a" };
        yield return new object?[] { "(?!a)a", null, "a" };
        yield return new object?[] { "(?>ab)c", null, "c" };
        yield return new object?[] { "(?<x>a)", "a", "a" };
        yield return new object?[] { "\\p{L}+", null, null };
        yield return new object?[] { "(a*)*", null, null };
        yield return new object?[] { "(a+)+", null, null };
        yield return new object?[] { "(?:a|)", null, null };
        yield return new object?[] { "(?:ab){3}(?:cd){2}", null, null };
        yield return new object?[] { "(a)(b)(c)", null, null };
        yield return new object?[] { "(?m:^$)", null, null };
        yield return new object?[] { "(?i:^$)", null, null };
        yield return new object?[] { "(?i:^)", null, null };
        yield return new object?[] { "(?i:.)", null, null };
        yield return new object?[] { "(?i:.+)", null, null };
        yield return new object?[] { "(?i:\\b)", null, null };
        yield return new object?[] { "(?i:)", null, null };
        yield return new object?[] { "(?m:(a))", "a", "a" };
        yield return new object?[] { "(?m:a+)", null, null };
        yield return new object?[] { "(?s:.+)", null, null };
        yield return new object?[] { "(?m:^a+)", null, null };
        yield return new object?[] { "(?i:(.+))", null, null };
        yield return new object?[] { "(?i:(?m:^))", null, null };
        yield return new object?[] { "(?#c)", null, null };
        yield return new object?[] { "(?#a)b", "b", "b" };
        yield return new object?[] { "\\G", null, null };
        yield return new object?[] { "\\z", null, null };
        yield return new object?[] { "\\B", null, null };
        yield return new object?[] { "\\b", null, null };
        yield return new object?[] { "\\Z", null, null };
        yield return new object?[] { "a\\G", "a", "a" };
        yield return new object?[] { "a\\z", "a", "a" };
        yield return new object?[] { "a\\B", "a", "a" };
        yield return new object?[] { "a\\b", "a", "a" };
        yield return new object?[] { "(?m:$)", null, null };
        yield return new object?[] { "^$", null, null };
        yield return new object?[] { "$^", null, null };
        yield return new object?[] { "(?!)", null, null };
        yield return new object?[] { "(?=)", null, null };
        yield return new object?[] { "(a*)", null, null };
        yield return new object?[] { "(a?)?", null, null };
        yield return new object?[] { "(?:a{0})", null, null };
        yield return new object?[] { "a{0,0}", null, null };
        yield return new object?[] { "(?!)a", null, "a" };
        yield return new object?[] { "a{2147483647}", null, null };
        yield return new object?[] { "(?:ab){2147483647}", null, null };
        yield return new object?[] { "(?:a{2147483647}){2}", null, null };
        yield return new object?[] { "(?:a){1500000000}(?:b){1500000000}", null, null };
        yield return new object?[] { "^a|^b", null, null };
        yield return new object?[] { "^a|b", null, null };
        yield return new object?[] { "(^a)b", null, "b" };
        yield return new object?[] { "((^a))", null, "a" };
        yield return new object?[] { "(?:^a)b", null, "b" };
        yield return new object?[] { "(^a)+", null, null };
        yield return new object?[] { "(^a)*", null, null };
        yield return new object?[] { "(ab$)", "ab", "ab" };
        yield return new object?[] { "(a$)", "a", "a" };
        yield return new object?[] { "(a$|b$)", null, null };
        yield return new object?[] { "a|^b", null, null };
        yield return new object?[] { "(^a|^b)", null, null };
        yield return new object?[] { "((((((((((a))))))))))", "a", "a" };
        yield return new object?[] { "(ab$|ab)", null, "ab" };
        yield return new object?[] { "(a$)+", null, null };
        yield return new object?[] { "(?=^a)a", null, "a" };
        yield return new object?[] { "(?!^a)a", null, "a" };
        yield return new object?[] { "(?>^a)b", null, "b" };
        yield return new object?[] { "(?>a$)b", null, "b" };
        yield return new object?[] { "x", "x", "x" };
        yield return new object?[] { "(ab^cd)", "ab", "ab" };
        yield return new object?[] { "(ab$cd)", "ab", "ab" };
        yield return new object?[] { "(ab\\zcd)", "ab", "ab" };
        yield return new object?[] { "(ab\\Gcd)", "ab", "ab" };
        yield return new object?[] { "(ab\\bcd)", "ab", "ab" };
        yield return new object?[] { "(?m:^a^b)", "a", "a" };
        yield return new object?[] { "a(?m:^)b", "a", "a" };
        yield return new object?[] { "a(?m:$)b", "a", "a" };
        yield return new object?[] { "a\\Gb", "a", "a" };
        yield return new object?[] { "a\\zb", "a", "a" };
        yield return new object?[] { "(?m:^ab)", "ab", "ab" };
        yield return new object?[] { "$ab", null, "ab" };
        yield return new object?[] { "\\zab", null, "ab" };
        yield return new object?[] { "\\Gab", null, "ab" };
        yield return new object?[] { "\\Aab", null, "ab" };
        yield return new object?[] { "ax{0}yb", "ayb", "ayb" };
        yield return new object?[] { "(?m:^)", null, null };
        yield return new object?[] { "(?:^)", null, null };
        yield return new object?[] { "(?m:\\G)", null, null };
    }

    [Theory]
    [MemberData(nameof(LiteralCases))]
    public void OwnedLiteralsMatchPinnedFacts(string pattern, string? leading, string? longest)
    {
        var ownedTree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(ownedTree);
        Assert.Equal(leading, Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(ownedTree.Root));
        Assert.Equal(longest, Utf8OwnedFindOptimizations.TryExtractLongestLiteral(ownedTree.Root));
    }
    public static IEnumerable<object?[]> OptionLiteralCases()
    {
        yield return new object?[] { "^a", RegexOptions.CultureInvariant | RegexOptions.Multiline, "a", "a" };
        yield return new object?[] { ".", RegexOptions.CultureInvariant | RegexOptions.Singleline, null, null };
        yield return new object?[] { "a", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, null };
    }

    [Theory]
    [MemberData(nameof(OptionLiteralCases))]
    public void OwnedLiteralsWithOptionsMatchPinnedFacts(string pattern, RegexOptions options, string? leading, string? longest)
    {
        var ownedTree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(ownedTree);
        Assert.Equal(leading, Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(ownedTree.Root));
        Assert.Equal(longest, Utf8OwnedFindOptimizations.TryExtractLongestLiteral(ownedTree.Root));
    }
    public static IEnumerable<object?[]> PrefixCases()
    {
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, new string[] { "abc" } };
        yield return new object?[] { "a", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "a|b", RegexOptions.CultureInvariant, new string[] { "a", "b" } };
        yield return new object?[] { "(a|bc|def)", RegexOptions.CultureInvariant, new string[] { "a", "bc", "def" } };
        yield return new object?[] { "(a|)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "x[yz]", RegexOptions.CultureInvariant, new string[] { "xy", "xz" } };
        yield return new object?[] { "a{2}", RegexOptions.CultureInvariant, new string[] { "aa" } };
        yield return new object?[] { "a{2,4}", RegexOptions.CultureInvariant, new string[] { "aa" } };
        yield return new object?[] { "(a)(b)(c)", RegexOptions.CultureInvariant, new string[] { "abc" } };
        yield return new object?[] { "b|a", RegexOptions.CultureInvariant, new string[] { "b", "a" } };
        yield return new object?[] { "ab|ab", RegexOptions.CultureInvariant, new string[] { "ab", "ab" } };
        yield return new object?[] { "(a|b)(c|d)", RegexOptions.CultureInvariant, new string[] { "ac", "bc", "ad", "bd" } };
        yield return new object?[] { "(a|(b|c))", RegexOptions.CultureInvariant, new string[] { "a", "b", "c" } };
        yield return new object?[] { "^a|^b", RegexOptions.CultureInvariant, new string[] { "a", "b" } };
        yield return new object?[] { "a|^b", RegexOptions.CultureInvariant, new string[] { "a", "b" } };
        yield return new object?[] { "^ab|cd", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "ab|^cd", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "^ab|^cd", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "^abc|d", RegexOptions.CultureInvariant, new string[] { "abc", "d" } };
        yield return new object?[] { "xy|^z", RegexOptions.CultureInvariant, new string[] { "xy", "z" } };
        yield return new object?[] { "^ab|cd|e", RegexOptions.CultureInvariant, new string[] { "ab", "cd", "e" } };
        yield return new object?[] { "ab.cd", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "a\\db", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "ab+", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "ab?", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "ab*", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "a+b", RegexOptions.CultureInvariant, null };
        yield return new object?[] { ".ab", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "\\dab", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "[^ab]c", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "[ab]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "x[ab]", RegexOptions.CultureInvariant, new string[] { "xa", "xb" } };
        yield return new object?[] { "(a|b)c", RegexOptions.CultureInvariant, new string[] { "ac", "bc" } };
        yield return new object?[] { "x[a-z0-9]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "ab.*cd", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(abcdef)g", RegexOptions.CultureInvariant, new string[] { "abcdefg" } };
        yield return new object?[] { "(ab){2}", RegexOptions.CultureInvariant, new string[] { "abab" } };
        yield return new object?[] { "(ab){2}c", RegexOptions.CultureInvariant, new string[] { "ababc" } };
        yield return new object?[] { "a?b", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "a.{2}b", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "[ab]{2}", RegexOptions.CultureInvariant, new string[] { "aa", "ba", "ab", "bb" } };
        yield return new object?[] { "x[0-9]", RegexOptions.CultureInvariant, new string[] { "x0", "x1", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9" } };
        yield return new object?[] { "ab(cd)+", RegexOptions.CultureInvariant, new string[] { "abcd" } };
        yield return new object?[] { "(ab)+c", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "ab[0-9]{2}", RegexOptions.CultureInvariant, new string[] { "ab0", "ab1", "ab2", "ab3", "ab4", "ab5", "ab6", "ab7", "ab8", "ab9" } };
        yield return new object?[] { "(?=ab)cd", RegexOptions.CultureInvariant, new string[] { "cd" } };
        yield return new object?[] { "(?!ab)cd", RegexOptions.CultureInvariant, new string[] { "cd" } };
        yield return new object?[] { "(?>ab)cd", RegexOptions.CultureInvariant, new string[] { "abcd" } };
        yield return new object?[] { "^ab", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "ab$", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "\\bab", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "\\Aab", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "ab\\z", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(ab)c", RegexOptions.CultureInvariant, new string[] { "abc" } };
        yield return new object?[] { "[a-z]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "[a-z0-9]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "[ab]{2}c", RegexOptions.CultureInvariant, new string[] { "aac", "bac", "abc", "bbc" } };
        yield return new object?[] { "a{2}b", RegexOptions.CultureInvariant, new string[] { "aab" } };
        yield return new object?[] { "[ab]cd", RegexOptions.CultureInvariant, new string[] { "acd", "bcd" } };
        yield return new object?[] { "ab[cd]ef", RegexOptions.CultureInvariant, new string[] { "abcef", "abdef" } };
        yield return new object?[] { "a[bc]d", RegexOptions.CultureInvariant, new string[] { "abd", "acd" } };
        yield return new object?[] { "ab|cd|ef", RegexOptions.CultureInvariant, new string[] { "ab", "cd", "ef" } };
        yield return new object?[] { "(ab|cd)e", RegexOptions.CultureInvariant, new string[] { "abe", "cde" } };
        yield return new object?[] { "(ab|cd)ef", RegexOptions.CultureInvariant, new string[] { "abef", "cdef" } };
        yield return new object?[] { "(ab|cd)", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "(?:ab|cd)", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "cd|ab", RegexOptions.CultureInvariant, new string[] { "cd", "ab" } };
        yield return new object?[] { "a|ab", RegexOptions.CultureInvariant, new string[] { "a", "ab" } };
        yield return new object?[] { "(a.bcdef|gh)", RegexOptions.CultureInvariant, new string[] { "a", "gh" } };
        yield return new object?[] { "a(b|c)d", RegexOptions.CultureInvariant, new string[] { "abd", "acd" } };
        yield return new object?[] { "a(b|c)", RegexOptions.CultureInvariant, new string[] { "ab", "ac" } };
        yield return new object?[] { "a(bc|d)e", RegexOptions.CultureInvariant, new string[] { "abce", "ade" } };
        yield return new object?[] { "(ab)\\1", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(a)\\1", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "\\bhi\\B", RegexOptions.CultureInvariant, new string[] { "hi" } };
        yield return new object?[] { "x{0}y", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "ax{0}yb", RegexOptions.CultureInvariant, new string[] { "ayb" } };
        yield return new object?[] { ".*ab", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "a.*b", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "[ab]c", RegexOptions.CultureInvariant, new string[] { "ac", "bc" } };
        yield return new object?[] { "(?i:ab)", RegexOptions.CultureInvariant, new string[] { "ab", "Ab", "aB", "AB" } };
        yield return new object?[] { "(?i)a", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "a", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "[ab]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "[a-z]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "abc", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, new string[] { "abc" } };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, new string[] { "ab", "cd" } };
        yield return new object?[] { "abc", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "abc" } };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "ab", "cd" } };
        yield return new object?[] { "a", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, null };
        yield return new object?[] { "x[a-z]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "\\d", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "[0-9]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "a\\sb", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "\\p{L}ab", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(a+)(b+)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(a+)+", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(?:a|)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(?:ab){3}(?:cd){2}", RegexOptions.CultureInvariant, new string[] { "abababcd" } };
        yield return new object?[] { "", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "|", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "^", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "$", RegexOptions.CultureInvariant, null };
        yield return new object?[] { ".", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "[a-z]+", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(.)+", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "^", RegexOptions.CultureInvariant | RegexOptions.Multiline, null };
        yield return new object?[] { ".", RegexOptions.CultureInvariant | RegexOptions.Singleline, null };
        yield return new object?[] { "(a)(b)(c)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "abc" } };
        yield return new object?[] { "x[yz]", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "xy", "xz" } };
        yield return new object?[] { "ab", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "ab" } };
        yield return new object?[] { "a{2}", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "aa" } };
        yield return new object?[] { "(ab)c", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "abc" } };
        yield return new object?[] { "^ab", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "ab" } };
        yield return new object?[] { "ab", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, new string[] { "ab" } };
        yield return new object?[] { "(?i:abc)", RegexOptions.CultureInvariant, new string[] { "abc", "Abc", "aBc", "ABc", "abC", "AbC", "aBC", "ABC" } };
        yield return new object?[] { "(?i:ab|cd)", RegexOptions.CultureInvariant, new string[] { "ab", "Ab", "aB", "AB", "cd", "Cd", "cD", "CD" } };
        yield return new object?[] { "(?i)[ab]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "1", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "[ab]{4}", RegexOptions.CultureInvariant, new string[] { "aaaa", "baaa", "abaa", "bbaa", "aaba", "baba", "abba", "bbba", "aaab", "baab", "abab", "bbab", "aabb", "babb", "abbb", "bbbb" } };
        yield return new object?[] { "[ab]{5}", RegexOptions.CultureInvariant, new string[] { "aaaa", "baaa", "abaa", "bbaa", "aaba", "baba", "abba", "bbba", "aaab", "baab", "abab", "bbab", "aabb", "babb", "abbb", "bbbb" } };
        yield return new object?[] { "a{8}", RegexOptions.CultureInvariant, new string[] { "aaaaaaaa" } };
        yield return new object?[] { "a{9}", RegexOptions.CultureInvariant, new string[] { "aaaaaaaa" } };
        yield return new object?[] { "[0-9]{2}", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(a.cd|ef)", RegexOptions.CultureInvariant, new string[] { "a", "ef" } };
        yield return new object?[] { "(ab.cd|efgh)", RegexOptions.CultureInvariant, new string[] { "ab", "efgh" } };
        yield return new object?[] { "(ab|a.cd)", RegexOptions.CultureInvariant, new string[] { "ab", "a" } };
        yield return new object?[] { "(gh|a.bcdef)", RegexOptions.CultureInvariant, new string[] { "gh", "a" } };
        yield return new object?[] { "(?i)[a-z]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "^a|bc", RegexOptions.CultureInvariant, new string[] { "a", "bc" } };
        yield return new object?[] { "ab|^cde", RegexOptions.CultureInvariant, new string[] { "ab", "cde" } };
        yield return new object?[] { "a{2,4}b", RegexOptions.CultureInvariant, new string[] { "aa" } };
        yield return new object?[] { "a1", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, new string[] { "a1" } };
        yield return new object?[] { "ab1", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, new string[] { "ab1" } };
        yield return new object?[] { "aa|bb|cc|dd|ee|ff|gg|hh|ii|jj|kk|ll|mm|nn|oo|pp|qq", RegexOptions.CultureInvariant, new string[] { "aa", "bb", "cc", "dd", "ee", "ff", "gg", "hh", "ii", "jj", "kk", "ll", "mm", "nn", "oo", "pp", "qq" } };
        yield return new object?[] { "aa|bb|cc|dd|ee|ff|gg|hh|ii|jj|kk|ll|mm|nn|oo|pp", RegexOptions.CultureInvariant, new string[] { "aa", "bb", "cc", "dd", "ee", "ff", "gg", "hh", "ii", "jj", "kk", "ll", "mm", "nn", "oo", "pp" } };
        yield return new object?[] { "abcdefghi", RegexOptions.CultureInvariant, new string[] { "abcdefghi" } };
        yield return new object?[] { "abcdefgh", RegexOptions.CultureInvariant, new string[] { "abcdefgh" } };
        yield return new object?[] { "ab(cd){2}e", RegexOptions.CultureInvariant, new string[] { "abcdcde" } };
        yield return new object?[] { "ab(cd){2,3}e", RegexOptions.CultureInvariant, new string[] { "abcdcd" } };
        yield return new object?[] { "(ef|a.cd|gh)", RegexOptions.CultureInvariant, new string[] { "ef", "a", "gh" } };
        yield return new object?[] { "(a|b).cd", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "a(b|c).d", RegexOptions.CultureInvariant, new string[] { "ab", "ac" } };
        yield return new object?[] { "(ab)\\1c", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "[ab]{2,3}c", RegexOptions.CultureInvariant, new string[] { "aa", "ba", "ab", "bb" } };
        yield return new object?[] { "(a|b)c", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "ac", "bc" } };
        yield return new object?[] { "a{2,}b", RegexOptions.CultureInvariant, new string[] { "aa" } };
        yield return new object?[] { "((ab|cd))", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "ab", "cd" } };
        yield return new object?[] { "a(b|c)d", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "abd", "acd" } };
        yield return new object?[] { "(a|bc|def)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "a", "bc", "def" } };
        yield return new object?[] { "(a|b)(c|d)(e|f)(g|h)(i|j)", RegexOptions.CultureInvariant, new string[] { "aceg", "bceg", "adeg", "bdeg", "acfg", "bcfg", "adfg", "bdfg", "aceh", "bceh", "adeh", "bdeh", "acfh", "bcfh", "adfh", "bdfh" } };
        yield return new object?[] { "ab(cd){2}", RegexOptions.CultureInvariant, new string[] { "abcdcd" } };
        yield return new object?[] { "a^b", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "a$b", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(a|b)(c|d)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "ac", "bc", "ad", "bd" } };
        yield return new object?[] { "(?m:^a)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "a\\bb", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(a|b)(c|d)(e|f)(g|h)", RegexOptions.CultureInvariant, new string[] { "aceg", "bceg", "adeg", "bdeg", "acfg", "bcfg", "adfg", "bdfg", "aceh", "bceh", "adeh", "bdeh", "acfh", "bcfh", "adfh", "bdfh" } };
        yield return new object?[] { "a{9}[bc]", RegexOptions.CultureInvariant, new string[] { "aaaaaaaa" } };
        yield return new object?[] { "(a|b)c{2}", RegexOptions.CultureInvariant, new string[] { "acc", "bcc" } };
        yield return new object?[] { "a(?m:^)b", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(?m:$)ab", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "a{8}b", RegexOptions.CultureInvariant, new string[] { "aaaaaaaab" } };
        yield return new object?[] { "a{9}b", RegexOptions.CultureInvariant, new string[] { "aaaaaaaa" } };
        yield return new object?[] { "a{7}b", RegexOptions.CultureInvariant, new string[] { "aaaaaaab" } };
        yield return new object?[] { "aba{6}c", RegexOptions.CultureInvariant, new string[] { "abaaaaaac" } };
        yield return new object?[] { "xa{7}b", RegexOptions.CultureInvariant, new string[] { "xaaaaaaab" } };
        yield return new object?[] { "x[ab]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, new string[] { "xa", "xb" } };
        yield return new object?[] { "[ab]c", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, new string[] { "ac", "bc" } };
        yield return new object?[] { "ab[cd]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, new string[] { "abc", "abd" } };
        yield return new object?[] { "\u005Cu0428\u005Cu0435\u005Cu0440\u005Cu043B\u005Cu043E\u005Cu043A \u005Cu0425\u005Cu043E\u005Cu043B\u005Cu043C\u005Cu0441", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "\u005Cu0428\u005Cu0435\u005Cu0440\u005Cu043B\u005Cu043E\u005Cu043A", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "(?i)[\u005Cu0428]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "[\u005Cu0428]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "(?i)\u005Cu0428", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "\u005Cu0428", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "x[\u005Cu00C5]", RegexOptions.CultureInvariant, new string[] { "x\u00C5" } };
        yield return new object?[] { "[\u005Cu00C5][ab]", RegexOptions.CultureInvariant, new string[] { "\u00C5a", "\u00C5b" } };
        yield return new object?[] { "x[a\u005Cu00C5]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "x[\u005Cu00C5a]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
    }

    [Theory]
    [MemberData(nameof(PrefixCases))]
    public void OwnedCoveringPrefixesMatchScalarFacts(string pattern, RegexOptions options, string[]? expected)
    {
        var ownedTree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(ownedTree);
        var actual = Utf8OwnedFindOptimizations.FindAlternatePrefixes(ownedTree.Root, options)
            ?.Select(Encoding.UTF8.GetString).Order(StringComparer.Ordinal);
        Assert.Equal(expected?.Order(StringComparer.Ordinal), actual);
    }

    public static IEnumerable<object?[]> FdlCases()
    {
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, "abc", 0 };
        yield return new object?[] { "a", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a|b", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a|bc|def)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a|)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "x[yz]", RegexOptions.CultureInvariant, "x", 0 };
        yield return new object?[] { "a{2}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a{2,4}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a)(b)(c)", RegexOptions.CultureInvariant, "c", 2 };
        yield return new object?[] { "b|a", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "ab|ab", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a|b)(c|d)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a|(b|c))", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "^a|^b", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a|^b", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "^ab|cd", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "ab|^cd", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "^ab|^cd", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "^abc|d", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "xy|^z", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "^ab|cd|e", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "ab.cd", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "a\\db", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "ab+", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "ab?", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "ab*", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "a+b", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { ".ab", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "\\dab", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "[^ab]c", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "[ab]", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "x[ab]", RegexOptions.CultureInvariant, "x", 0 };
        yield return new object?[] { "(a|b)c", RegexOptions.CultureInvariant, "c", 1 };
        yield return new object?[] { "x[a-z0-9]", RegexOptions.CultureInvariant, "x", 0 };
        yield return new object?[] { "ab.*cd", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "(abcdef)g", RegexOptions.CultureInvariant, "abcdef", 0 };
        yield return new object?[] { "(ab){2}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(ab){2}c", RegexOptions.CultureInvariant, "c", 4 };
        yield return new object?[] { "a?b", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a.{2}b", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "[ab]{2}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "x[0-9]", RegexOptions.CultureInvariant, "x", 0 };
        yield return new object?[] { "ab(cd)+", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "(ab)+c", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "ab[0-9]{2}", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "(?=ab)cd", RegexOptions.CultureInvariant, "cd", 0 };
        yield return new object?[] { "(?!ab)cd", RegexOptions.CultureInvariant, "cd", 0 };
        yield return new object?[] { "(?>ab)cd", RegexOptions.CultureInvariant, "cd", 2 };
        yield return new object?[] { "^ab", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "ab$", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "\\bab", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "\\Aab", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "ab\\z", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "(ab)c", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "[a-z]", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "[a-z0-9]", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "[ab]{2}c", RegexOptions.CultureInvariant, "c", 2 };
        yield return new object?[] { "a{2}b", RegexOptions.CultureInvariant, "b", 2 };
        yield return new object?[] { "[ab]cd", RegexOptions.CultureInvariant, "cd", 1 };
        yield return new object?[] { "ab[cd]ef", RegexOptions.CultureInvariant, "ef", 3 };
        yield return new object?[] { "a[bc]d", RegexOptions.CultureInvariant, "d", 2 };
        yield return new object?[] { "ab|cd|ef", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(ab|cd)e", RegexOptions.CultureInvariant, "e", 2 };
        yield return new object?[] { "(ab|cd)ef", RegexOptions.CultureInvariant, "ef", 2 };
        yield return new object?[] { "(ab|cd)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(?:ab|cd)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "cd|ab", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a|ab", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a.bcdef|gh)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a(b|c)d", RegexOptions.CultureInvariant, "d", 2 };
        yield return new object?[] { "a(b|c)", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "a(bc|d)e", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "(ab)\\1", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "(a)\\1", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "\\bhi\\B", RegexOptions.CultureInvariant, "hi", 0 };
        yield return new object?[] { "x{0}y", RegexOptions.CultureInvariant, "y", 0 };
        yield return new object?[] { "ax{0}yb", RegexOptions.CultureInvariant, "ayb", 0 };
        yield return new object?[] { ".*ab", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a.*b", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "[ab]c", RegexOptions.CultureInvariant, "c", 1 };
        yield return new object?[] { "(?i:ab)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(?i)a", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "[ab]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "[a-z]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "abc", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "abc", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "abc", 0 };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, null, 0 };
        yield return new object?[] { "a", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "a", 0 };
        yield return new object?[] { "x[a-z]", RegexOptions.CultureInvariant, "x", 0 };
        yield return new object?[] { "\\d", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "[0-9]", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a\\sb", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "\\p{L}ab", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a+)(b+)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a+)+", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(?:a|)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(?:ab){3}(?:cd){2}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "|", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "^", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "$", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { ".", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "[a-z]+", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(.)+", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "^", RegexOptions.CultureInvariant | RegexOptions.Multiline, null, 0 };
        yield return new object?[] { ".", RegexOptions.CultureInvariant | RegexOptions.Singleline, null, 0 };
        yield return new object?[] { "(a)(b)(c)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "c", 2 };
        yield return new object?[] { "x[yz]", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "x", 0 };
        yield return new object?[] { "ab", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "ab", 0 };
        yield return new object?[] { "a{2}", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, null, 0 };
        yield return new object?[] { "(ab)c", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "ab", 0 };
        yield return new object?[] { "^ab", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "ab", 0 };
        yield return new object?[] { "ab", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "(?i:abc)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(?i:ab|cd)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(?i)[ab]", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "1", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, "1", 0 };
        yield return new object?[] { "[ab]{4}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "[ab]{5}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a{8}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a{9}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "[0-9]{2}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a.cd|ef)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(ab.cd|efgh)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(ab|a.cd)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(gh|a.bcdef)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(?i)[a-z]", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "^a|bc", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "ab|^cde", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a{2,4}b", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a1", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, "1", 1 };
        yield return new object?[] { "ab1", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, "1", 2 };
        yield return new object?[] { "aa|bb|cc|dd|ee|ff|gg|hh|ii|jj|kk|ll|mm|nn|oo|pp|qq", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "aa|bb|cc|dd|ee|ff|gg|hh|ii|jj|kk|ll|mm|nn|oo|pp", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "abcdefghi", RegexOptions.CultureInvariant, "abcdefghi", 0 };
        yield return new object?[] { "abcdefgh", RegexOptions.CultureInvariant, "abcdefgh", 0 };
        yield return new object?[] { "ab(cd){2}e", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "ab(cd){2,3}e", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "(ef|a.cd|gh)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a|b).cd", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a(b|c).d", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "(ab)\\1c", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "[ab]{2,3}c", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a|b)c", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "c", 1 };
        yield return new object?[] { "a{2,}b", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "((ab|cd))", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, null, 0 };
        yield return new object?[] { "a(b|c)d", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "d", 2 };
        yield return new object?[] { "(a|bc|def)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, null, 0 };
        yield return new object?[] { "(a|b)(c|d)(e|f)(g|h)(i|j)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "ab(cd){2}", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "a^b", RegexOptions.CultureInvariant, "b", 1 };
        yield return new object?[] { "a$b", RegexOptions.CultureInvariant, "b", 1 };
        yield return new object?[] { "(a|b)(c|d)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, null, 0 };
        yield return new object?[] { "(?m:^a)", RegexOptions.CultureInvariant, "a", 0 };
        yield return new object?[] { "a\\bb", RegexOptions.CultureInvariant, "b", 1 };
        yield return new object?[] { "(a|b)(c|d)(e|f)(g|h)", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a{9}[bc]", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "(a|b)c{2}", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "a(?m:^)b", RegexOptions.CultureInvariant, "b", 1 };
        yield return new object?[] { "(?m:$)ab", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "a{8}b", RegexOptions.CultureInvariant, "b", 8 };
        yield return new object?[] { "a{9}b", RegexOptions.CultureInvariant, "b", 9 };
        yield return new object?[] { "a{7}b", RegexOptions.CultureInvariant, "b", 7 };
        yield return new object?[] { "aba{6}c", RegexOptions.CultureInvariant, "ab", 0 };
        yield return new object?[] { "xa{7}b", RegexOptions.CultureInvariant, "b", 8 };
        yield return new object?[] { "x[ab]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "[ab]c", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "ab[cd]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "\u005Cu0428\u005Cu0435\u005Cu0440\u005Cu043B\u005Cu043E\u005Cu043A \u005Cu0425\u005Cu043E\u005Cu043B\u005Cu043C\u005Cu0441", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, " ", 12 };
        yield return new object?[] { "\u005Cu0428\u005Cu0435\u005Cu0440\u005Cu043B\u005Cu043E\u005Cu043A", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "(?i)[\u005Cu0428]", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "[\u005Cu0428]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "(?i)\u005Cu0428", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "\u005Cu0428", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "x[\u005Cu00C5]", RegexOptions.CultureInvariant, "x", 0 };
        yield return new object?[] { "[\u005Cu00C5][ab]", RegexOptions.CultureInvariant, null, 0 };
        yield return new object?[] { "x[a\u005Cu00C5]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
        yield return new object?[] { "x[\u005Cu00C5a]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null, 0 };
    }

    [Theory]
    [MemberData(nameof(FdlCases))]
    public void OwnedFixedDistanceLiteralMatchesPinnedFacts(string pattern, RegexOptions options, string? literal, int distance)
    {
        var ownedTree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(ownedTree);
        var owned = Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(ownedTree.Root);
        if (literal is null)
        {
            Assert.Null(owned);
        }
        else
        {
            Assert.NotNull(owned);
            Assert.Equal(literal, owned.Value.Literal);
            Assert.Equal(distance, owned.Value.Distance);
        }
    }
}
