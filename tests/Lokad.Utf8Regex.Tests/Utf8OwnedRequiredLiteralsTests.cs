using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned required-literal families: the differential battery pins the owned best
// family identical to the legacy instance (oracle first, then owned) across the
// probed shape catalog.
public sealed class Utf8OwnedRequiredLiteralsTests
{
    public static IEnumerable<object?[]> RequiredCases()
    {
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, new string[] { "abc" } };
        yield return new object?[] { "a", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "a|b|c", RegexOptions.CultureInvariant, new string[] { "a", "b", "c" } };
        yield return new object?[] { "(a|bc|def)", RegexOptions.CultureInvariant, new string[] { "a", "bc", "def" } };
        yield return new object?[] { "(a|)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "ab|ab", RegexOptions.CultureInvariant, new string[] { "ab", "ab" } };
        yield return new object?[] { "a|ab", RegexOptions.CultureInvariant, new string[] { "a", "ab" } };
        yield return new object?[] { "(a|b)(c|d)", RegexOptions.CultureInvariant, new string[] { "a", "b" } };
        yield return new object?[] { "(ab|cd)ef", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "(ab|cd)e", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "a(b|c)d", RegexOptions.CultureInvariant, new string[] { "b", "c" } };
        yield return new object?[] { "(a)(b)(c)", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "(ab)", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "((ab))", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(?:ab)cd", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(?>ab)cd", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(?<x>ab)cd", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "a*", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "a+", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "ab+", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "ab*", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "(ab)+", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(ab)*c", RegexOptions.CultureInvariant, new string[] { "c" } };
        yield return new object?[] { "a{2}b", RegexOptions.CultureInvariant, new string[] { "b" } };
        yield return new object?[] { "(ab){2}c", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "a{2,4}b", RegexOptions.CultureInvariant, new string[] { "b" } };
        yield return new object?[] { "^ab", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "ab$", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "\u005Cbab\u005CB", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(?=ab)cd", RegexOptions.CultureInvariant, new string[] { "cd" } };
        yield return new object?[] { "(?!ab)cd", RegexOptions.CultureInvariant, new string[] { "cd" } };
        yield return new object?[] { "(a)\u005C1", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "[ab]cd", RegexOptions.CultureInvariant, new string[] { "cd" } };
        yield return new object?[] { "ab[cd]", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "\u005Cdab", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { ".ab", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "ab.*cd", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { ".*ab", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "x[yz]", RegexOptions.CultureInvariant, new string[] { "x" } };
        yield return new object?[] { "(a|b)*c", RegexOptions.CultureInvariant, new string[] { "c" } };
        yield return new object?[] { "(ab|cd)?ef", RegexOptions.CultureInvariant, new string[] { "ef" } };
        yield return new object?[] { "(a|bc)(d|ef)g", RegexOptions.CultureInvariant, new string[] { "a", "bc" } };
        yield return new object?[] { "abc|def|ghi", RegexOptions.CultureInvariant, new string[] { "abc", "def", "ghi" } };
        yield return new object?[] { "ab|abc|abcd", RegexOptions.CultureInvariant, new string[] { "ab", "abc", "abcd" } };
        yield return new object?[] { "abcd|abc|ab", RegexOptions.CultureInvariant, new string[] { "ab", "abc", "abcd" } };
        yield return new object?[] { "(ab|ab)", RegexOptions.CultureInvariant, new string[] { "ab", "ab" } };
        yield return new object?[] { "(a|(b|c))", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "a(b|c)?d", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "a(bc|d)e", RegexOptions.CultureInvariant, new string[] { "bc", "d" } };
        yield return new object?[] { "(a.bc|def)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(ab|a)", RegexOptions.CultureInvariant, new string[] { "a", "ab" } };
        yield return new object?[] { "(a|ab)", RegexOptions.CultureInvariant, new string[] { "a", "ab" } };
        yield return new object?[] { "^(ab|cd)", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "(ab|cd)$", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "(ab|cd)+", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "((ab|cd))", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "(?:ab|cd)", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "ab", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "(?i:ab)cd", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, null };
        yield return new object?[] { "abc", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "abc" } };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, new string[] { "ab", "cd" } };
        yield return new object?[] { "^", RegexOptions.CultureInvariant | RegexOptions.Multiline, null };
        yield return new object?[] { "a\u005Cbb", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "\u005CAab\u005Cz", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(ab)\u005C1c", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "x{0}y", RegexOptions.CultureInvariant, new string[] { "y" } };
        yield return new object?[] { "(?:a|)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "longliteralrun", RegexOptions.CultureInvariant, new string[] { "longliteralrun" } };
        yield return new object?[] { "a(bcdef|gh)", RegexOptions.CultureInvariant, new string[] { "bcdef", "gh" } };
        yield return new object?[] { "(ab|cd|ef|gh)", RegexOptions.CultureInvariant, new string[] { "ab", "cd", "ef", "gh" } };
        yield return new object?[] { "pre(ab|cd)post", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "pre(a|bc)post", RegexOptions.CultureInvariant, new string[] { "post" } };
        yield return new object?[] { "(ab)?cd", RegexOptions.CultureInvariant, new string[] { "cd" } };
        yield return new object?[] { "ab(cd)?ef", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(ab|cd){2}", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "[a-z]+x", RegexOptions.CultureInvariant, new string[] { "x" } };
        yield return new object?[] { "x\u005Cd+y", RegexOptions.CultureInvariant, new string[] { "x" } };
        yield return new object?[] { "(ab.cd|ef)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(a[bc]|def)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "((a|b)c|d)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(a|b|c|d)", RegexOptions.CultureInvariant, new string[] { "a", "b", "c", "d" } };
        yield return new object?[] { "(abcd|abc|ab)", RegexOptions.CultureInvariant, new string[] { "ab", "abc", "abcd" } };
        yield return new object?[] { "ab|a", RegexOptions.CultureInvariant, new string[] { "a", "ab" } };
        yield return new object?[] { "cd|ab", RegexOptions.CultureInvariant, new string[] { "ab", "cd" } };
        yield return new object?[] { "(a|b)?c", RegexOptions.CultureInvariant, new string[] { "c" } };
        yield return new object?[] { "(a|b)+c", RegexOptions.CultureInvariant, new string[] { "a", "b" } };
        yield return new object?[] { "(ab){0}", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "a[b-d]e", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "(?i:a)b", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "1a", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, new string[] { "1" } };
        yield return new object?[] { "ab(c|d)", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "(ab|cd){0}", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(ab|cd){0,1}", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(a)(b)|cd", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(ab)(cd)|ef", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(ab|cd)|ef", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "x(a|b)y", RegexOptions.CultureInvariant, new string[] { "a", "b" } };
        yield return new object?[] { "a{0,2}b", RegexOptions.CultureInvariant, new string[] { "b" } };
        yield return new object?[] { "a{2,}b", RegexOptions.CultureInvariant, new string[] { "b" } };
        yield return new object?[] { "[ab]{2}c", RegexOptions.CultureInvariant, new string[] { "c" } };
        yield return new object?[] { "a{2}bc", RegexOptions.CultureInvariant, new string[] { "bc" } };
        yield return new object?[] { "a{2}b{3}c", RegexOptions.CultureInvariant, new string[] { "c" } };
        yield return new object?[] { "(ab){0}c", RegexOptions.CultureInvariant, new string[] { "c" } };
        yield return new object?[] { "ab?", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "ab??", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "ab+?", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "ab*?", RegexOptions.CultureInvariant, new string[] { "a" } };
        yield return new object?[] { "a{1}b", RegexOptions.CultureInvariant, new string[] { "ab" } };
        yield return new object?[] { "a{0}b", RegexOptions.CultureInvariant, new string[] { "b" } };
        yield return new object?[] { "[ab]?c", RegexOptions.CultureInvariant, new string[] { "c" } };
        yield return new object?[] { ".[ab]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "[ab][cd]", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "x[ab]", RegexOptions.CultureInvariant, new string[] { "x" } };
        yield return new object?[] { "[ab](cd)", RegexOptions.CultureInvariant, new string[] { "cd" } };
        yield return new object?[] { "(?:foo((?:ASIA|AKIA|AROA|AIDA)[A-Z0-7]{16}).*?[a-zA-Z0-9+/]{40}|[a-zA-Z0-9+/]{40}.*?bar((?:ASIA|AKIA|AROA|AIDA)[A-Z0-7]{16}))", RegexOptions.CultureInvariant, new string[] { "AIDA", "AKIA", "AROA", "ASIA" } };
        yield return new object?[] { "^import\\s+(?<shared>shared\\s+)?\"(?<path>(\\.|[^\\\\\"]*))\".*$", RegexOptions.Multiline, new string[] { "import" } };
        yield return new object?[] { "(foo(ab|cd)|ef)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(x(ab|cd)|ef)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "((ab|cd)ef|gh)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(a|bcdef|gh)", RegexOptions.CultureInvariant, new string[] { "a", "bcdef", "gh" } };
        yield return new object?[] { "(x(ab|cd)y{2}|ef)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(x(ab|cd)y|ef)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(x(ab|cd).*|ef)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "((ab|cd){2}|ef)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "((ab|cd)+|ef)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "((ab|cd)*|ef)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "(x(ab|cd)[yz]|ef)", RegexOptions.CultureInvariant, null };
        yield return new object?[] { "((ab|cd)y|ef)", RegexOptions.CultureInvariant, null };
    }

    [Theory]
    [MemberData(nameof(RequiredCases))]
    public void OwnedRequiredFamilyMatchesLegacy(string pattern, RegexOptions options, string[]? expected)
    {
        var tree = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, options);
        Assert.NotNull(tree);
        var legacy = RuntimeFrontEnd.RegexRequiredLiteralAnalyzer.FindBestRequiredLiteralFamily(tree.Root);
        Assert.Equal(expected, legacy);
        Assert.Equal(expected, Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(tree.Root));
    }
}
