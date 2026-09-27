using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R05 grouped-literal detailed shaping: single exact literals with fixed-span captures
/// shape detailed matches from the owned discovery hit without constructing the fallback
/// engine. Single-optional shapes project present or absent spans by hit length.
/// Divergent single alternations project the winning branch identified by ordinal
/// text comparison. Ambiguous duplicate-text branches and multi-loop optionals
/// keep BCL shaping.
/// </summary>
public sealed class GroupedLiteralDetailedTests
{
    public static IEnumerable<object?[]> GroupedLiteralHitCases()
    {
        string[] patterns =
        [
            "(a)(b)",
            "(a)(b)(c)",
            "((a))",
            "(?<x>a)(?<y>b)",
            "(?<a>x)(b)(?<c>y)",
            "(ab)",
            "(a)",
            "(1)(2)",
            "(?<x>(?<y>a))",
            "((?<x>a)(b))",
            "(?<x>(a)(b))",
            "(?<x>a(?<y>b))",
            "(a(?<x>b))",
        ];
        string[] texts = ["xabcy", "ab", "xa", "eab", "abe"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }

        yield return new object?[] { "(1)(2)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "x12y" };
        yield return new object?[] { "(a)(b)", RegexOptions.CultureInvariant, "éab" };
        yield return new object?[] { "(a)(b)", RegexOptions.Compiled | RegexOptions.CultureInvariant, "abé" };
        yield return new object?[] { "(a)(b)", RegexOptions.CultureInvariant, "x\U0001F600aby" };
        yield return new object?[] { "(é)(x)", RegexOptions.CultureInvariant, "aéx" };
    }

    [Theory]
    [MemberData(nameof(GroupedLiteralHitCases))]
    public void GroupedLiteralDetailedMatchesOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        if (!expected.Success)
        {
            return;
        }

        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Value, actual.GetValueString());
        for (var i = 0; i < expected.Groups.Count; i++)
        {
            var expectedGroup = expected.Groups[i];
            var actualGroup = actual.GetGroup(i);
            Assert.Equal(expectedGroup.Success, actualGroup.Success);
            Assert.Equal(expectedGroup.Index, actualGroup.IndexInUtf16);
            Assert.Equal(expectedGroup.Length, actualGroup.LengthInUtf16);
            Assert.Equal(expectedGroup.Value, actualGroup.GetValueString());
            Assert.Equal(expectedGroup.Captures.Count, actualGroup.CaptureCount);
            Assert.True(actual.TryGetGroup(i, out var tryGroup));
            Assert.Equal(expectedGroup.Success, tryGroup.Success);
            var groupName = expected.Groups[i].Name;
            Assert.True(actual.TryGetGroup(groupName, out var tryNamedGroup));
            Assert.Equal(expectedGroup.Success, tryNamedGroup.Success);
        }

        Assert.False(actual.TryGetGroup(expected.Groups.Count, out _));
        Assert.False(actual.TryGetGroup("missing-group-name", out _));
    }
    public static IEnumerable<object?[]> GroupedLiteralMissCases()
    {
        yield return new object?[] { "(a)(b)", RegexOptions.CultureInvariant, "no match here" };
        yield return new object?[] { "(a)(b)", RegexOptions.Compiled | RegexOptions.CultureInvariant, "no match here" };
        yield return new object?[] { "(a)(b)(c)", RegexOptions.CultureInvariant, "ab" };
        yield return new object?[] { "((a))", RegexOptions.CultureInvariant, "b" };
        yield return new object?[] { "(?<x>a)(?<y>b)", RegexOptions.Compiled | RegexOptions.CultureInvariant, "ax" };
    }

    [Theory]
    [MemberData(nameof(GroupedLiteralMissCases))]
    public void GroupedLiteralDetailedMissMatchesOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);

        Assert.False(expected.Success);
        Assert.False(actual.Success);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
    }

    public static IEnumerable<object?[]> GroupedLiteralFromOffsetCases()
    {
        yield return new object?[] { "(a)(b)", RegexOptions.CultureInvariant, "xabxab", 0 };
        yield return new object?[] { "(a)(b)", RegexOptions.CultureInvariant, "xabxab", 2 };
        yield return new object?[] { "(a)(b)", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xabxab", 3 };
        yield return new object?[] { "((a))", RegexOptions.CultureInvariant, "xaa", 1 };
        yield return new object?[] { "(?<x>a)(?<y>b)", RegexOptions.CultureInvariant, "xab", 1 };
        yield return new object?[] { "(a)(b)", RegexOptions.CultureInvariant, "ab", 2 };
        yield return new object?[] { "(a)?b", RegexOptions.CultureInvariant, "xabxab", 0 };
        yield return new object?[] { "(a)?b", RegexOptions.CultureInvariant, "xabxab", 2 };
        yield return new object?[] { "(a)?b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xabxab", 3 };
        yield return new object?[] { "a(b)?", RegexOptions.CultureInvariant, "xab", 1 };
        yield return new object?[] { "(a)?b", RegexOptions.CultureInvariant, "ab", 1 };
        yield return new object?[] { "(a)?b", RegexOptions.CultureInvariant, "ab", 2 };
        yield return new object?[] { "(ab|cd)e", RegexOptions.CultureInvariant, "xxabex", 2 };
        yield return new object?[] { "(ab|cd)e", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xxabex", 0 };
        yield return new object?[] { "a(b|c)d", RegexOptions.CultureInvariant, "xabd", 1 };
        yield return new object?[] { "(a|b)c", RegexOptions.CultureInvariant, "xbc", 1 };
        yield return new object?[] { "(ab|c)(d)", RegexOptions.CultureInvariant, "xxabdex", 2 };
        yield return new object?[] { "(ab|c)(d)", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xxcdx", 2 };
        yield return new object?[] { "(a|bc)d", RegexOptions.CultureInvariant, "xbcd", 1 };
        yield return new object?[] { "(x(a)|(b)y)", RegexOptions.CultureInvariant, "xby", 0 };
    }

    [Theory]
    [MemberData(nameof(GroupedLiteralFromOffsetCases))]
    public void GroupedLiteralDetailedFromOffsetMatchesOracle(string pattern, RegexOptions options, string text, int offset)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text, offset);
        var actual = regex.MatchDetailedFromUtf16Offset(input, offset);

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        if (!expected.Success)
        {
            return;
        }

        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        for (var i = 0; i < expected.Groups.Count; i++)
        {
            Assert.Equal(expected.Groups[i].Success, actual.GetGroup(i).Success);
            Assert.Equal(expected.Groups[i].Index, actual.GetGroup(i).IndexInUtf16);
            Assert.Equal(expected.Groups[i].Length, actual.GetGroup(i).LengthInUtf16);
            Assert.Equal(expected.Groups[i].Value, actual.GetGroup(i).GetValueString());
        }
    }
    public static IEnumerable<object?[]> OptionalGroupedLiteralHitCases()
    {
        string[] patterns =
        [
            "(a)?b",
            "a(b)?",
            "(a)?(b)",
            "((a)?b)",
            "(a(b)?)",
            "(?<x>a)?b",
            "(a)(?:b)?c",
        ];
        string[] texts = ["ab", "b", "xab", "abx", "a", "ac", "abc", "xxabxx", "xyz", "eab"];
        yield return new object?[] { "(a)?b", RegexOptions.CultureInvariant, "éab" };
        yield return new object?[] { "(a)?b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "abé" };
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(OptionalGroupedLiteralHitCases))]
    public void OptionalGroupedLiteralDetailedMatchesOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        if (!expected.Success)
        {
            return;
        }

        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Value, actual.GetValueString());
        for (var i = 0; i < expected.Groups.Count; i++)
        {
            var expectedGroup = expected.Groups[i];
            var actualGroup = actual.GetGroup(i);
            Assert.Equal(expectedGroup.Success, actualGroup.Success);
            Assert.Equal(expectedGroup.Index, actualGroup.IndexInUtf16);
            Assert.Equal(expectedGroup.Length, actualGroup.LengthInUtf16);
            Assert.Equal(expectedGroup.Value, actualGroup.GetValueString());
            Assert.Equal(expectedGroup.Captures.Count, actualGroup.CaptureCount);
            Assert.True(actual.TryGetGroup(i, out var tryGroup));
            Assert.Equal(expectedGroup.Success, tryGroup.Success);
            var groupName = expected.Groups[i].Name;
            Assert.True(actual.TryGetGroup(groupName, out var tryNamedGroup));
            Assert.Equal(expectedGroup.Success, tryNamedGroup.Success);
        }

        Assert.False(actual.TryGetGroup(expected.Groups.Count, out _));
        Assert.False(actual.TryGetGroup("missing-group-name", out _));
    }

    public static IEnumerable<object?[]> OptionalGroupedLiteralMissCases()
    {
        string[] patterns = ["(a)?b", "a(b)?", "(a)?(b)", "((a)?b)", "(a(b)?)", "(?<x>a)?b", "(a)(?:b)?c"];
        string[] texts = ["xyz", "qqq"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(OptionalGroupedLiteralMissCases))]
    public void OptionalGroupedLiteralDetailedMissMatchesOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);

        Assert.False(expected.Success);
        Assert.False(actual.Success);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
    }

    public static IEnumerable<object?[]> AlternationSpanHitCases()
    {
        string[] patterns =
        [
            "(ab|cd)e",
            "(?<w>ab|cd)e",
            "a(b|c)d",
            "(a|b)c",
        ];
        string[] texts = ["abe", "cde", "xxabex", "abd", "acd", "bc", "ac", "xyz"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }

        yield return new object?[] { "(ab|cd)e", RegexOptions.CultureInvariant, "éabe" };
        yield return new object?[] { "x(a|b)y", RegexOptions.CultureInvariant, "xay" };
        yield return new object?[] { "x(a|b)y", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xby" };
        yield return new object?[] { "x(a|b)y", RegexOptions.CultureInvariant, "xy" };
        yield return new object?[] { "((a|b)(c|d))", RegexOptions.CultureInvariant, "ac" };
        yield return new object?[] { "((a|b)(c|d))", RegexOptions.Compiled | RegexOptions.CultureInvariant, "bd" };
        yield return new object?[] { "((a|b)(c|d))", RegexOptions.CultureInvariant, "ab" };
        yield return new object?[] { "a(b|c)d", RegexOptions.Compiled | RegexOptions.CultureInvariant, "éabd" };
    }

    [Theory]
    [MemberData(nameof(AlternationSpanHitCases))]
    public void AlternationSpanDetailedMatchesOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        if (!expected.Success)
        {
            return;
        }

        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Value, actual.GetValueString());
        for (var i = 0; i < expected.Groups.Count; i++)
        {
            var expectedGroup = expected.Groups[i];
            var actualGroup = actual.GetGroup(i);
            Assert.Equal(expectedGroup.Success, actualGroup.Success);
            Assert.Equal(expectedGroup.Index, actualGroup.IndexInUtf16);
            Assert.Equal(expectedGroup.Length, actualGroup.LengthInUtf16);
            Assert.Equal(expectedGroup.Value, actualGroup.GetValueString());
            Assert.Equal(expectedGroup.Captures.Count, actualGroup.CaptureCount);
            Assert.True(actual.TryGetGroup(i, out var tryGroup));
            Assert.Equal(expectedGroup.Success, tryGroup.Success);
            var groupName = expected.Groups[i].Name;
            Assert.True(actual.TryGetGroup(groupName, out var tryNamedGroup));
            Assert.Equal(expectedGroup.Success, tryNamedGroup.Success);
        }

        Assert.False(actual.TryGetGroup(expected.Groups.Count, out _));
        Assert.False(actual.TryGetGroup("missing-group-name", out _));
    }

    public static IEnumerable<object?[]> DivergentBranchHitCases()
    {
        string[] patterns =
        [
            "(ab|c)(d)",
            "(a|bc)d",
            "(x(a)|(b)y)",
            "(a)(b)|(c)",
            "((a|b)c)",
            "(?<x>ab|(?<y>c))d",
            "((?<x>a)|(?<y>a))b",
            "((a)|(a))",
        ];
        string[] texts = ["abd", "cd", "xxabdex", "ad", "bcd", "xay", "xby", "ab", "c", "ac", "bc", "xyz"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }

        yield return new object?[] { "(ab|c)(d)", RegexOptions.CultureInvariant, "éabd" };
        yield return new object?[] { "(a|bc)d", RegexOptions.Compiled | RegexOptions.CultureInvariant, "éabcd" };
    }

    [Theory]
    [MemberData(nameof(DivergentBranchHitCases))]
    public void DivergentBranchDetailedMatchesOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        if (!expected.Success)
        {
            return;
        }

        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Value, actual.GetValueString());
        for (var i = 0; i < expected.Groups.Count; i++)
        {
            var expectedGroup = expected.Groups[i];
            var actualGroup = actual.GetGroup(i);
            Assert.Equal(expectedGroup.Success, actualGroup.Success);
            Assert.Equal(expectedGroup.Index, actualGroup.IndexInUtf16);
            Assert.Equal(expectedGroup.Length, actualGroup.LengthInUtf16);
            Assert.Equal(expectedGroup.Value, actualGroup.GetValueString());
            Assert.Equal(expectedGroup.Captures.Count, actualGroup.CaptureCount);
            Assert.True(actual.TryGetGroup(i, out var tryGroup));
            Assert.Equal(expectedGroup.Success, tryGroup.Success);
            var groupName = expected.Groups[i].Name;
            Assert.True(actual.TryGetGroup(groupName, out var tryNamedGroup));
            Assert.Equal(expectedGroup.Success, tryNamedGroup.Success);
        }

        Assert.False(actual.TryGetGroup(expected.Groups.Count, out _));
        Assert.False(actual.TryGetGroup("missing-group-name", out _));
    }

    public static IEnumerable<object?[]> GroupedLiteralZeroConstructionCases()
    {
        string[] patterns = ["(a)(b)", "(a)(b)(c)", "((a))", "(?<x>a)(?<y>b)", "(?<a>x)(b)(?<c>y)", "(ab)", "(a)", "(1)(2)", "(a)?b", "a(b)?", "(a)?(b)", "((a)?b)", "(a(b)?)", "(?<x>a)?b", "(a)(?:b)?c", "(ab|cd)e", "(?<w>ab|cd)e", "a(b|c)d", "(a|b)c", "x(a|b)y", "((a|b)(c|d))", "(ab|c)(d)", "(a|bc)d", "(x(a)|(b)y)", "(a)(b)|(c)", "((a|b)c)", "(?<x>ab|(?<y>c))d", "(?<x>(?<y>a))", "((?<x>a)(b))", "(?<x>(a)(b))", "(?<x>a(?<y>b))", "(a(?<x>b))", "((?<x>a)|(?<y>a))b", "((a)|(a))"];
        string[] texts = ["xabcy", "ab", "xa", "b", "ac", "xay", "no match here"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GroupedLiteralZeroConstructionCases))]
    public void GroupedLiteralOperationsConstructNoFallbackEngine(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);

        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);

        var matchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Match(text).Success, matchRegex.Match(input).Success);
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);

        var detailedRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Match(text).Success, detailedRegex.MatchDetailed(input).Success);
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);

        var enumerateRegex = new Utf8Regex(pattern, options);
        var enumerated = 0;
        foreach (var _ in enumerateRegex.EnumerateMatches(input))
        {
            enumerated++;
        }

        var expectedEnumerated = 0;
        foreach (Match _ in oracle.Matches(text))
        {
            expectedEnumerated++;
        }

        Assert.Equal(expectedEnumerated, enumerated);
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("(a)?b(c)?", "abce")]
    [InlineData("(a)?b(c)?", "bcd")]
    [InlineData("(a)?b(c)?", "xyz")]
    public void RemainingDetailedDebtKeepsBclShaping(string pattern, string text)
    {
        // Declared R05 debt: multi-loop optionals still shape detailed matches
        // through the fallback engine. Parity holds; only Detailed constructs.
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);
            var expected = oracle.Match(text);
            var actual = regex.MatchDetailed(input);

            Assert.Equal(expected.Success, actual.Success);
            Assert.Equal(expected.Groups.Count, actual.GroupCount);
            if (expected.Success)
            {
                for (var i = 0; i < expected.Groups.Count; i++)
                {
                    Assert.Equal(expected.Groups[i].Success, actual.GetGroup(i).Success);
                    Assert.Equal(expected.Groups[i].Index, actual.GetGroup(i).IndexInUtf16);
                    Assert.Equal(expected.Groups[i].Value, actual.GetGroup(i).GetValueString());
                }
            }

            Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }
}
