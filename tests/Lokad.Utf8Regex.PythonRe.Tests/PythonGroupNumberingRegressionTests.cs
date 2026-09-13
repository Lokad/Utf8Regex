using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// PLAN.md C07: Python numbers groups strictly by opening paren while .NET numbers
// unnamed groups first, so numeric backreferences, conditionals, replacement
// references, and several result builders disagreed with CPython whenever a named
// group preceded an unnamed one. Every surface below now indexes Python slots;
// managed numbers appear only at the engine boundary. Expectations are CPython
// 3.13.2 ground truth (see ground-c07*.py).
public sealed class PythonGroupNumberingRegressionTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Theory]
    [InlineData("(?P<x>a)(b)\\1", "aba", true, 0, 3)]
    [InlineData("(?P<x>a)(b)\\2", "abb", true, 0, 3)]
    [InlineData("(?P<x>a)(b)\\2", "aba", false, 0, 0)]
    [InlineData("(a)(?P<x>b)\\1", "aba", true, 0, 3)]
    [InlineData("(a)(?P<x>b)\\2", "abb", true, 0, 3)]
    [InlineData("(?P<x>a)(?P<y>b)\\1", "aba", true, 0, 3)]
    [InlineData("(?P<x>a)(?P<y>b)\\2", "abb", true, 0, 3)]
    [InlineData("(a)(b)(c)\\3", "abcc", true, 0, 4)]
    public void NumericBackreferencesFollowPythonSlots(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8PythonRegex(pattern);
        AssertSearch(regex, input, expectedSuccess, expectedStart, expectedEnd);
        Assert.Equal(expectedSuccess, regex.Match(Utf8(input)).Success);
        Assert.Equal(expectedSuccess, regex.FullMatch(Utf8(input)).Success);
    }

    [Theory]
    [InlineData("(?P<x>a)(b)(?(2)y|n)", "aby", true, 0, 3)]
    [InlineData("(?P<x>a)(b)(?(2)y|n)", "abn", false, 0, 0)]
    [InlineData("(?P<x>a)(b)(?(x)y|n)", "aby", true, 0, 3)]
    [InlineData("(?P<x>a)?(?(1)y|n)", "ay", true, 0, 2)]
    [InlineData("(a)(b)(?(1)y|n)", "aby", true, 0, 3)]
    public void NumericAndNamedConditionalsFollowPythonSlots(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Fact]
    public void DetailedGroupsFollowPythonSlotOrder()
    {
        var detailed = new Utf8PythonRegex("(?P<x>a)(b)").SearchDetailedData(Utf8("ab"));
        Assert.True(detailed.Success);
        Assert.Equal(["ab", "a", "b"], detailed.Groups.Select(static group => group.ValueText).ToArray());
        Assert.Equal([0, 1, 2], detailed.Groups.Select(static group => group.Number).ToArray());
        var named = detailed.Groups.Single(group => group.Number == 1);
        Assert.Equal("a", named.ValueText);
    }

    [Fact]
    public void NamedLookupUsesPythonSlots()
    {
        var regex = new Utf8PythonRegex("(?P<x>a)(b)");
        Assert.Equal(["0", "x", "2"], regex.GetGroupNames());
        var context = regex.MatchDetailed(Utf8("ab"));
        Assert.True(context.Success);
        Assert.True(context.TryGetGroup(1, out var first));
        Assert.Equal("a", first.GetValueString());
        Assert.True(context.TryGetGroup(2, out var second));
        Assert.Equal("b", second.GetValueString());
        Assert.True(context.TryGetFirstSetGroup("x", out var named));
        Assert.Equal("a", named.GetValueString());
        Assert.True(context.TryGetFirstSetGroupValueString("x", out var namedValue));
        Assert.Equal("a", namedValue);
    }

    [Theory]
    [InlineData("(?P<x>a)(b)", "\\2\\1", "ab", "ba")]
    [InlineData("(?P<x>a)(b)", "\\g<1>\\g<2>", "ab", "ab")]
    [InlineData("(?P<x>a)(b)", "\\g<x>!", "ab", "a!")]
    [InlineData("(?P<x>a)(b)(?(2)y|n)", "\\2\\1", "aby", "ba")]
    [InlineData("(a)(?P<x>b)", "\\2\\1", "ab", "ba")]
    public void ReplacementReferencesFollowPythonSlots(string pattern, string replacement, string input, string expected)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expected, regex.SubnToString(Utf8(input), replacement, 0).ResultText);
        Assert.Equal(expected, Encoding.UTF8.GetString(regex.Subn(Utf8(input), replacement, 0).ResultBytes));
    }

    [Fact]
    public void SplitCapturesFollowPythonSlotOrder()
    {
        var regex = new Utf8PythonRegex("(?P<x>a)(-)");
        Assert.Equal(["x", "a", "-", "b"], regex.SplitToStrings(Utf8("xa-b"), 0, 0));
        var detailed = regex.SplitDetailed(Utf8("xa-b"), 0, 0);
        Assert.Equal(["x", "a", "-", "b"], detailed.Select(static item => item.ValueText).ToArray());
        Assert.Equal([0, 1, 2, 0], detailed.Select(static item => item.IsCapture ? item.CaptureGroupNumber : 0).ToArray());
    }

    [Fact]
    public void FindAllTuplesFollowPythonSlotOrder()
    {
        var tuples = new Utf8PythonRegex("(?P<x>a)(b)").FindAllToStrings(Utf8("abab")).TupleValues;
        Assert.Equal(2, tuples.Length);
        Assert.Equal(["a", "b"], tuples[0]);
        Assert.Equal(["a", "b"], tuples[1]);
        var matches = new Utf8PythonRegex("(?P<x>a)(b)").FindAll(Utf8("abab"));
        Assert.Equal(2, matches.Length);
        Assert.Equal([(0, 2), (2, 4)], matches.Select(static match => (match.StartOffsetInUtf16, match.EndOffsetInUtf16)).ToArray());
    }

    [Fact]
    public void FindIterDetailedFollowsPythonSlotOrder()
    {
        var found = new Utf8PythonRegex("(?P<x>a)(b)").FindIterDetailed(Utf8("xaby"));
        var single = Assert.Single(found);
        Assert.Equal((1, 3), (single.Value.StartOffsetInUtf16, single.Value.EndOffsetInUtf16));
        Assert.Equal(["ab", "a", "b"], single.Groups.Select(static group => group.ValueText).ToArray());
    }

    [Fact]
    public void EvaluatorSnapshotsFollowPythonSlotOrder()
    {
        var regex = new Utf8PythonRegex("(?P<x>a)(b)");
        Utf8PythonUtf8MatchEvaluator<string> evaluator = static (prefix, match) =>
            Encoding.UTF8.GetBytes(prefix + match.Groups[1].ValueText + match.Groups[2].ValueText);
        Assert.Equal("-ab", Encoding.UTF8.GetString(regex.Subn(Utf8("ab"), "-", evaluator).ResultBytes));
    }

    private static void AssertSearch(Utf8PythonRegex regex, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var match = regex.Search(Encoding.UTF8.GetBytes(input));
        Assert.Equal(expectedSuccess, match.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, match.StartOffsetInUtf16);
            Assert.Equal(expectedEnd, match.EndOffsetInUtf16);
            Assert.Equal(input.Substring(expectedStart, expectedEnd - expectedStart), match.GetValueString());
        }
    }
}
