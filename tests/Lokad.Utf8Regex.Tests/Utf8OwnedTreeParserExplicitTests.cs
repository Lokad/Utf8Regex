using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser ExplicitCapture stage: the differential battery pins scoped
// and toggled n identical to the provider trees (unnamed groups in
// parse-time n become slotless Group nodes with dense renumbering after
// them, top-level n demotes unnamed groups the same way, backreferences
// resolve surviving slots (unbound digits fail), simple named definitions
// register dense slots under top-level n with cross-scope guard,
// simple named definitions under scoped n stay slotless with mapped slots),
// plus decline agreement on references into skipped slots; cross-scope named,
// top-level n, pop, and loop-wrapped residuals still route to the provider.
public sealed class Utf8OwnedTreeParserExplicitTests
{
    public static IEnumerable<object?[]> ExplicitAcceptCases()
    {
        yield return new object?[] { "(?n:a)", 512 };
        yield return new object?[] { "(?n:(a))", 512 };
        yield return new object?[] { "(?n)", 512 };
        yield return new object?[] { "(?n)a", 512 };
        yield return new object?[] { "(?n:(a)(b))", 512 };
        yield return new object?[] { "(?n:(a)b(c))", 512 };
        yield return new object?[] { "(a)(?n:b)", 512 };
        yield return new object?[] { "(?<x>a)(?n:(b))", 512 };
        yield return new object?[] { "(a)(?n)\\1", 512 };
        yield return new object?[] { "(?n:(a|b))", 512 };
        yield return new object?[] { "(?<x>(?n:a))", 512 };
        yield return new object?[] { "(?n:(?i:a))", 512 };
        yield return new object?[] { "(?i:(?n:a))", 512 };
        yield return new object?[] { "(?n)(a)", 512 };
        yield return new object?[] { "(?n:(a){0})", 512 };
        yield return new object?[] { "((?n:a)(x))", 512 };
        yield return new object?[] { "(?n:(?m:(a)))", 512 };
        yield return new object?[] { "(?n-i:x)", 512 };
        yield return new object?[] { "(?m-n:a)", 512 };
        yield return new object?[] { "(a)(b)", 512 };
        yield return new object?[] { "(a)(b)", 528 };
        yield return new object?[] { "[ab]", 528 };
        yield return new object?[] { "(?n:(a))", 528 };
        yield return new object?[] { "(?n)(a)", 528 };
        yield return new object?[] { "(?n:a)", 528 };
        yield return new object?[] { "(?-n:a)", 528 };
        yield return new object?[] { "((?n:a)(x)\\2)", 512 };
        yield return new object?[] { "(?n:(?m:(a))(b))", 512 };
        yield return new object?[] { "(?n:(?<x>a))", 512 };
        yield return new object?[] { "(?n:(?<x>a))", 528 };
        yield return new object?[] { "(?n:(?<1>a))", 512 };
        yield return new object?[] { "(?n:(?<1>a))", 528 };
        yield return new object?[] { "(?n:(a)(?<y>b))", 512 };
        yield return new object?[] { "(?n:(a)(?<y>b))", 528 };
        yield return new object?[] { "(?n:(?<x>a)(?<y>b))", 512 };
        yield return new object?[] { "(?n:(?<x>a)?)", 512 };
        yield return new object?[] { "(?n:(?<x>a)*)", 512 };
        yield return new object?[] { "(?n:(?m:(?<x>a)))", 512 };
        yield return new object?[] { "(?n)(?<x>a)", 512 };
        yield return new object?[] { "(a)(b)", 516 };
        yield return new object?[] { "(?n:a)", 516 };
        yield return new object?[] { "(a)(?-n:(b))", 516 };
        yield return new object?[] { "((a)(b))", 516 };
        yield return new object?[] { "(a)+", 516 };
        yield return new object?[] { "(a)|(b)", 516 };
        yield return new object?[] { "(?<x>a)", 516 };
        yield return new object?[] { "(?<1>a)", 516 };
        yield return new object?[] { "(a)(?<x>b)", 516 };
        yield return new object?[] { "(?<x>a)(b)", 516 };
        yield return new object?[] { "(?<x>a)\\k<x>", 516 };
        yield return new object?[] { "(a)(?<x>b)\\1", 516 };
        yield return new object?[] { "(?<x>a)", 517 };
        yield return new object?[] { "(?<x>a)(?<y>b)", 516 };
    }

    public static IEnumerable<object?[]> ExplicitDeclineCases()
    {
        yield return new object?[] { "(?n)\\1", 512 };
        yield return new object?[] { "(a)\\1", 516 };
        yield return new object?[] { "(?n:(a)\\1)", 512 };
        yield return new object?[] { "(?(1)a|b)", 516 };
        yield return new object?[] { "(?(x)a|b)", 516 };
    }

    public static IEnumerable<object?[]> ExplicitResidualCases()
    {
        yield return new object?[] { "(?n:(a))(b)", 512 };
        yield return new object?[] { "(?n:(a))(b)", 528 };
        yield return new object?[] { "(?n:(?<-x>a))", 512 };
        yield return new object?[] { "(?n:a)+", 512 };
        yield return new object?[] { "(?n:(?<x>a))(b)", 512 };
        yield return new object?[] { "(?n:(a)(?<y>b))(c)", 512 };
        yield return new object?[] { "(?n:(?<x>a)(?-n:(b)))", 512 };
        yield return new object?[] { "(?<x>a)(?(x)b|c)", 516 };
    }

    [Theory]
    [MemberData(nameof(ExplicitAcceptCases))]
    public void OwnedExplicitMatchesProviderTree(string pattern, int options)
    {
        var expected = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options);
        Assert.NotNull(expected);
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.Equal(expected.CaptureCount, actual.CaptureCount);
        Assert.Equal(expected.CaptureNames ?? [], actual.CaptureNames ?? []);
        Assert.Equal(expected.Options, actual.Options);
        Assert.Equal(expected.Culture, actual.Culture);
        AssertHashtableEqual(expected.CaptureNameToNumberMapping, actual.CaptureNameToNumberMapping);
        AssertHashtableEqual(expected.CaptureNumberSparseMapping, actual.CaptureNumberSparseMapping);
        Assert.True(TreesEqual(expected.Root, actual.Root));
    }

    [Theory]
    [MemberData(nameof(ExplicitDeclineCases))]
    public void OwnedExplicitDeclinesUnsupported(string pattern, int options)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            (RegexOptions)options,
            RuntimeFrontEnd.RegexParser.GetTargetCulture((RegexOptions)options)));
    }

    [Theory]
    [MemberData(nameof(ExplicitResidualCases))]
    public void OwnedExplicitDeclinesResidual(string pattern, int options)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        Assert.NotNull(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options));
    }

    private static void AssertHashtableEqual(System.Collections.Hashtable? left, System.Collections.Hashtable? right)
    {
        if (left is null || right is null)
        {
            Assert.True(left is null && right is null);
            return;
        }

        Assert.Equal(left.Count, right.Count);
        foreach (var key in left.Keys)
        {
            Assert.True(right.ContainsKey(key));
            Assert.Equal(left[key], right[key]);
        }
    }

    private static bool TreesEqual(RuntimeFrontEnd.RegexNode left, RuntimeFrontEnd.RegexNode right)
    {
        return right.Kind == left.Kind &&
            right.Options == left.Options &&
            right.Ch == left.Ch &&
            right.Str == left.Str &&
            right.Str2 == left.Str2 &&
            right.M == left.M &&
            right.N == left.N &&
            right.ChildCount == left.ChildCount &&
            Enumerable.Range(0, left.ChildCount).All(index => TreesEqual(left.Child(index), right.Child(index)));
    }
}