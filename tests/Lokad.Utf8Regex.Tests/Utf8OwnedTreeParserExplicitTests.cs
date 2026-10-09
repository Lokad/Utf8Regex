using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser ExplicitCapture stage: the BCL-oracled battery pins scoped
// and toggled n with BCL metadata (unnamed groups in
// parse-time n become slotless Group nodes with dense renumbering after
// them, top-level n demotes unnamed groups the same way, backreferences
// resolve surviving slots (unbound digits fail), unnamed-first numbering holds
// across explicit scopes (auto groups take the lowest free slots while canonical
// numbers claim their literal slots, collisions included); named definitions
// capture under scoped n exactly like BCL (only unnamed groups go slotless);
// decline agreement on references into
// skipped slots holds; only the flip-decision balance-pop residual still routes
// to the provider.
public sealed class Utf8OwnedTreeParserExplicitTests
{
    public static IEnumerable<object?[]> ExplicitAcceptCases()
    {
        yield return new object?[] { "(?n:a)", 512, 516 };
        yield return new object?[] { "(?n:(a))", 512, 516 };
        yield return new object?[] { "(?n)", 512, 516 };
        yield return new object?[] { "(?n)a", 512, 516 };
        yield return new object?[] { "(?n:(a)(b))", 512, 516 };
        yield return new object?[] { "(?n:(a)b(c))", 512, 516 };
        yield return new object?[] { "(a)(?n:b)", 512, 512 };
        yield return new object?[] { "(?<x>a)(?n:(b))", 512, 512 };
        yield return new object?[] { "(a)(?n)\\1", 512, 512 };
        yield return new object?[] { "(?n:(a|b))", 512, 516 };
        yield return new object?[] { "(?<x>(?n:a))", 512, 512 };
        yield return new object?[] { "(?n:(?i:a))", 512, 516 };
        yield return new object?[] { "(?i:(?n:a))", 512, 513 };
        yield return new object?[] { "(?n)(a)", 512, 516 };
        yield return new object?[] { "(?n:(a){0})", 512, 516 };
        yield return new object?[] { "((?n:a)(x))", 512, 512 };
        yield return new object?[] { "(?n:(?m:(a)))", 512, 516 };
        yield return new object?[] { "(?n-i:x)", 512, 516 };
        yield return new object?[] { "(?m-n:a)", 512, 514 };
        yield return new object?[] { "(a)(b)", 512, 512 };
        yield return new object?[] { "(a)(b)", 528, 528 };
        yield return new object?[] { "[ab]", 528, 528 };
        yield return new object?[] { "(?n:(a))", 528, 532 };
        yield return new object?[] { "(?n)(a)", 528, 532 };
        yield return new object?[] { "(?n:a)", 528, 532 };
        yield return new object?[] { "(?-n:a)", 528, 528 };
        yield return new object?[] { "((?n:a)(x)\\2)", 512, 512 };
        yield return new object?[] { "(?n:(?m:(a))(b))", 512, 516 };
        yield return new object?[] { "(?n:(?<x>a))", 512, 516 };
        yield return new object?[] { "(?n:(?<x>a))", 528, 532 };
        yield return new object?[] { "(?n:(?<1>a))", 512, 516 };
        yield return new object?[] { "(?n:(?<1>a))", 528, 532 };
        yield return new object?[] { "(?n:(a)(?<y>b))", 512, 516 };
        yield return new object?[] { "(?n:(a)(?<y>b))", 528, 532 };
        yield return new object?[] { "(?n:(?<x>a)(?<y>b))", 512, 516 };
        yield return new object?[] { "(?n:(?<x>a)?)", 512, 516 };
        yield return new object?[] { "(?n:(?<x>a)*)", 512, 516 };
        yield return new object?[] { "(?n:(?m:(?<x>a)))", 512, 516 };
        yield return new object?[] { "(?n)(?<x>a)", 512, 516 };
        yield return new object?[] { "(a)(b)", 516, 516 };
        yield return new object?[] { "(?n:a)", 516, 516 };
        yield return new object?[] { "(a)(?-n:(b))", 516, 516 };
        yield return new object?[] { "((a)(b))", 516, 516 };
        yield return new object?[] { "(a)+", 516, 516 };
        yield return new object?[] { "(a)|(b)", 516, 516 };
        yield return new object?[] { "(?<x>a)", 516, 516 };
        yield return new object?[] { "(?<1>a)", 516, 516 };
        yield return new object?[] { "(a)(?<x>b)", 516, 516 };
        yield return new object?[] { "(?<x>a)(b)", 516, 516 };
        yield return new object?[] { "(?<x>a)\\k<x>", 516, 516 };
        yield return new object?[] { "(a)(?<x>b)\\1", 516, 516 };
        yield return new object?[] { "(?<x>a)", 517, 517 };
        yield return new object?[] { "(?<x>a)(?<y>b)", 516, 516 };
        yield return new object?[] { "(?n:(?<x>a)(?-n:(b)))", 512, 516 };
        yield return new object?[] { "(?n:(?<1>a)(?-n:(b)))", 512, 516 };
        yield return new object?[] { "(?n:(?-n:(b))(?<1>a))", 512, 516 };
        yield return new object?[] { "(?n:(?<2>a)(?-n:(b)))", 512, 516 };
        yield return new object?[] { "(?n:(a)(?-n:(b)))", 512, 516 };
        yield return new object?[] { "(?n:(?-n:(b))(c))", 512, 516 };
        yield return new object?[] { "(?n:(a)(?-n:(b))(c))", 512, 516 };
        yield return new object?[] { "(?n:(?<x>a)(?-n:(b))(c))", 512, 516 };
    }

    public static IEnumerable<object?[]> ExplicitDeclineCases()
    {
        yield return new object?[] { "(?n)\\1", 512, RegexParseError.UndefinedNumberedReference, 6 };
        yield return new object?[] { "(a)\\1", 516, RegexParseError.UndefinedNumberedReference, 5 };
        yield return new object?[] { "(ab)\\1", 532, RegexParseError.UndefinedNumberedReference, 6 };
        yield return new object?[] { "(a+)\\1", 532, RegexParseError.UndefinedNumberedReference, 6 };
        yield return new object?[] { "(a.+)\\1", 532, RegexParseError.UndefinedNumberedReference, 7 };
        yield return new object?[] { "(a.+)\\1", 596, RegexParseError.UndefinedNumberedReference, 7 };
        yield return new object?[] { "(a.+)\\1", 597, RegexParseError.UndefinedNumberedReference, 7 };
        yield return new object?[] { "^(ab)\\1$", 518, RegexParseError.UndefinedNumberedReference, 7 };
        yield return new object?[] { "(a+)\\1$", 518, RegexParseError.UndefinedNumberedReference, 6 };
        yield return new object?[] { "(a.+)\\1", 518, RegexParseError.UndefinedNumberedReference, 7 };
        yield return new object?[] { "(?n:(a)\\1)", 512, RegexParseError.UndefinedNumberedReference, 9 };
        yield return new object?[] { "(?(1)a|b)", 516, RegexParseError.AlternationHasUndefinedReference, 5 };
        yield return new object?[] { "(?n:(?<-x>a))", 512, RegexParseError.UndefinedNamedReference, 9 };
    }

    [Theory]
    [MemberData(nameof(ExplicitAcceptCases))]
    public void OwnedExplicitServesWithBclMetadata(string pattern, int options, int expectedOptions)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)expectedOptions, actual.Options);
        Assert.Equal(CultureInfo.InvariantCulture.Name, actual.Culture?.Name);
        AssertGroupNameMapping(oracle, actual.CaptureNameToNumberMapping);
        AssertGroupNumberMapping(oracle, actual.CaptureNumberSparseMapping);
    }

    [Theory]
    [MemberData(nameof(ExplicitDeclineCases))]
    public void OwnedExplicitDeclinesUnsupported(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }

    public static IEnumerable<object?[]> ExplicitNarrowingCases()
    {
        yield return new object?[] { "(?n:(a))(b)", 512, new string[] { "ab", "a", "b", "" } };
        yield return new object?[] { "(?n:(a))(b)", 528, new string[] { "ab", "a", "b", "" } };
        yield return new object?[] { "(?n:a)+", 512, new string[] { "a", "aa", "" } };
        yield return new object?[] { "(?n:a){2}([ab])", 512, new string[] { "aaab", "aab", "ab" } };
        yield return new object?[] { "(?n:a){2}(\\d){1,2}", 512, new string[] { "aa12", "aa1", "aa" } };
        yield return new object?[] { "(?n:a){2}|([ab])", 512, new string[] { "aa", "b", "ab" } };
        yield return new object?[] { "(?n:a){2}|(\\d){1,2}", 512, new string[] { "aa", "1", "12" } };
        yield return new object?[] { "(?n:(?<x>a))(b)", 512, new string[] { "ab", "a", "b", "" } };
        yield return new object?[] { "(?n:(a)(?<y>b))(c)", 512, new string[] { "abc", "ab", "bc", "" } };
        yield return new object?[] { "(b)(?n:(?<x>a))", 512, new string[] { "ba", "b", "a", "" } };
        yield return new object?[] { "(?n:(?<x>a))(b)(c)", 512, new string[] { "abc", "ab", "bc", "" } };
    }

    [Theory]
    [MemberData(nameof(ExplicitNarrowingCases))]
    public void OwnedExplicitServesCrossScopeNumbering(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNames(), product.GetGroupNames());
        Assert.Equal(oracle.GetGroupNumbers(), product.GetGroupNumbers());
        foreach (var inputText in inputs)
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), product.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
        }
    }

    private static void AssertGroupNameMapping(Regex oracle, IReadOnlyDictionary<string, int>? actual)
    {
        var names = oracle.GetGroupNames();
        if (actual is null)
        {
            Assert.All(names, static name => Assert.True(int.TryParse(name, out _), "Non-numeric group name without a mapping: " + name));
            return;
        }

        Assert.Equal(names.Length, actual.Count);
        foreach (var name in names)
        {
            Assert.True(actual.ContainsKey(name), "Missing name mapping: " + name);
            Assert.Equal(oracle.GroupNumberFromName(name), actual[name]);
        }
    }

    private static void AssertGroupNumberMapping(Regex oracle, IReadOnlyDictionary<int, int>? actual)
    {
        var numbers = oracle.GetGroupNumbers();
        if (actual is null)
        {
            for (var index = 0; index < numbers.Length; index++)
            {
                Assert.Equal(index, numbers[index]);
            }

            return;
        }

        Assert.Equal(numbers.Length, actual.Count);
        foreach (var entry in actual)
        {
            Assert.Equal(entry.Key, numbers[entry.Value]);
        }
    }

}
