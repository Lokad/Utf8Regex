using System.Text.RegularExpressions;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned required-literal families: the pinned battery checks the owned best
// family against hardcoded expectations across the probed shape catalog.
public sealed class Utf8OwnedRequiredLiteralsTests
{
    [Fact]
    public void RequiredFamiliesPreserveDisjunctionsAndExactScalarCaseProofs()
    {
        foreach (var row in new (string Pattern, string[]? Family)[]
        {
            ("[ab](foo|bar)|[cd](foo|baz)", null),
            ("[ab](foo|bar)|[cd](foo|bar)", ["bar", "foo"]),
            ("[ab](foo|bar)|[cd]foo", ["bar", "foo"]),
            ("[ab]foo|[cd](foo|bar)", null),
            ("(?i:ab)|cd", null),
            ("😀|🙏", ["😀", "🙏"]),
            ("(?i:😀)x", ["x"]),
        })
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.RightToLeft })
        {
            var tree = Utf8OwnedTreeParser.TryParse(row.Pattern, options);
            Assert.NotNull(tree);
            Assert.Equal(row.Family, Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(tree.Root));
        }
        foreach (var text in new[] { "\ud800", "a\udfff", "ab", "K", "😀" })
        {
            var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.IgnoreCase, text);
            Assert.Null(Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(node));
        }
        var invalid = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, '\ud800');
        Assert.Null(Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(invalid));
        var identity = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.IgnoreCase, "123!");
        Assert.Equal(["123!"], Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(identity));
    }

    [Fact]
    public void RequiredFamiliesCoverIndependentScalarVmMatches()
    {
        var cases = RequiredCases().Select(row => (Pattern: Assert.IsType<string>(row[0]), Options: Assert.IsType<RegexOptions>(row[1])))
            .Concat(new (string Pattern, RegexOptions Options)[]
            {
                ("[ab](foo|bar)|[cd](foo|baz)", RegexOptions.CultureInvariant),
                ("[ab](foo|bar)|[cd](foo|bar)", RegexOptions.CultureInvariant),
                ("[ab]foo|[cd](foo|bar)", RegexOptions.CultureInvariant),
                ("(?i:ab)|cd", RegexOptions.CultureInvariant),
                ("(?i:😀)x", RegexOptions.CultureInvariant),
                ("😀|🙏", RegexOptions.CultureInvariant),
            });
        var subjects = new[] { "", "a", "b", "ab", "abc", "cd", "abcd", "ef", "abcde", "abab", "ababcd", "abababcdcd",
            "abXcd", "ac", "ad", "bd", "bc", "def", "foobar", "abar", "bbar", "cfoo", "dbar", "dbaz", "AB", "Abcd",
            "😀", "🙏", "😀x", "🙏x", "x😀y", "longliteralrun", "import \"x\"", "a\nb", "fooxbar", "!!xbar" }
            .Select(Encoding.UTF8.GetBytes).ToArray();
        var observed = 0;
        foreach (var row in cases)
        foreach (var rtl in new[] { RegexOptions.None, RegexOptions.RightToLeft })
        {
            var options = row.Options | rtl;
            var tree = Utf8OwnedTreeParser.TryParse(row.Pattern, options);
            Assert.NotNull(tree);
            var family = Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(tree.Root);
            if (family is null) continue;
            Assert.NotEmpty(family);
            var program = Utf8OwnedTreeSliceLowerer.TryLower(tree, options);
            Assert.NotNull(program);
            var starts = new int[program.GroupCount];
            var ends = new int[program.GroupCount];
            foreach (var input in subjects)
            for (var boundary = 0; boundary <= input.Length; boundary++)
            {
                if (boundary < input.Length && (input[boundary] & 0xc0) == 0x80) continue;
                var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(100_000, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
                // Probe scalar VM positions directly, bypassing every search fact.
                var match = Utf8SliceRunner.TryMatchAt(program, input, boundary, starts, ends, true, ref invocation);
                Assert.Equal(Utf8ExecutionLimitKind.None, match.Breach);
                if (!match.IsMatch) continue;
                observed++;
                var value = Encoding.UTF8.GetString(input.AsSpan(Math.Min(boundary, match.EndIndex), Math.Abs(boundary - match.EndIndex)));
                Assert.True(family.Any(member => value.Contains(member, StringComparison.Ordinal)), $"Uncovered required family: {row.Pattern} / {options}: {value}");
            }
        }
        Assert.True(observed > 100);
    }

    [Fact]
    public void PublicRequiredAndWindowSearchKeepScalarAlternativesAndScopedCase()
    {
        foreach (var row in new (string Pattern, string Subject)[]
        {
            ("[ab](foo|bar)|[cd](foo|baz)", "abar"),
            ("[ab](foo|bar)|[cd](foo|baz)", "dbaz"),
            ("[ab](foo|bar)|[cd](foo|bar)", "dbar"),
            ("[ab]foo|[cd](foo|bar)", "dbar"),
            ("(?i:ab)|cd", "AB"),
            ("(?:foo|!!).{0,2}bar", "!!xbar"),
            ("(?:foo|!!).{0,2}bar", "fooxbar"),
            ("foo.{0,2}(?i:bar)", "foo😀BAR"),
            ("(?i:foo).{0,2}bar", "FOO😀bar"),
            ("😀(foo|bar)|🙏(foo|baz)", "🙏baz"),
            ("(foo\\s+bar).{0,2}baz", "foo            barbaz"),
            ("(?:foo\\s+bar|!!).{0,2}baz", "foo\tbarbaz"),
            ("(?:foo\\s+bar|!!).{0,2}baz", "foo\u00a0barbaz"),
            ("((foo)(bar))(baz)?", "foobar"),
        })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var input = Encoding.UTF8.GetBytes(row.Subject);
            var regex = new Utf8Regex(row.Pattern, RegexOptions.CultureInvariant | mode);
            Assert.True(regex.IsMatch(input), $"{row.Pattern} / {mode}: {row.Subject}");
            Assert.Equal(1, regex.Count(input));
            var first = regex.Match(input);
            Assert.True(first.Success);
            Assert.Equal((0, input.Length), (first.IndexInBytes, first.LengthInBytes));
            var matches = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input)) matches.Add((match.IndexInBytes, match.LengthInBytes));
            Assert.Equal([(0, input.Length)], matches);
        }
    }

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
        yield return new object?[] { "(?i:ab)cd", RegexOptions.CultureInvariant, new string[] { "cd" } };
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
        yield return new object?[] { "(?i:a)b", RegexOptions.CultureInvariant, new string[] { "b" } };
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
    public void OwnedRequiredFamilyMatchesPinnedFacts(string pattern, RegexOptions options, string[]? expected)
    {
        var parsed = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(parsed);
        Assert.Equal(expected, Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(parsed.Root));
    }
}
