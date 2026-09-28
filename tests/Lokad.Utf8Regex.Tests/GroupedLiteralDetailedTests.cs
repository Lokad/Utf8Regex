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
        yield return new object?[] { "(a)?b(c)?", RegexOptions.CultureInvariant, "xabcx", 1 };
        yield return new object?[] { "(a)?b(c)?", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xbcx", 1 };
        yield return new object?[] { "(a)?(a)?", RegexOptions.CultureInvariant, "xaa", 1 };
        yield return new object?[] { "((a)?(b)?)", RegexOptions.CultureInvariant, "xab", 1 };
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

    public static IEnumerable<object?[]> MultiLoopHitCases()
    {
        string[] patterns =
        [
            "(a)?b(c)?",
            "(a)?(b)?c",
            "(x)?(y)?",
            "(a)?(a)?",
            "(ab)?(ab)?",
            "(a)?(a)?(a)?",
            "(a)??(b)??",
            "(a)?(a)??",
            "((a)?(b)?)",
            "(?<x>a)?(?<y>b)?",
        ];
        string[] texts = ["abc", "ab", "bc", "b", "a", "aa", "xy", "x", "y", "", "xxabcxx", "xyz"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }

        yield return new object?[] { "(a)?b(c)?", RegexOptions.CultureInvariant, "eabce" };
    }

    [Theory]
    [MemberData(nameof(MultiLoopHitCases))]
    public void MultiLoopDetailedMatchesOracle(string pattern, RegexOptions options, string text)
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

    public static IEnumerable<object?[]> LiteralEnumerationNonAsciiCases()
    {
        string[] patterns =
        [
            "abc",
            "(ab)",
            "(a)(b)",
            "a(?=b)",
            "ab",
            "a{1}",
            "(?i:ab)",
            "ab|cd",
        ];
        string[] texts = ["xxab12cdxx", "ééabcé", "xaby", "nomatchhere", ""];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }

        yield return new object?[] { "(a)(b)", RegexOptions.CultureInvariant, "x\U0001F600aby" };
        yield return new object?[] { "(a)(b)", RegexOptions.Compiled | RegexOptions.CultureInvariant, "x\U0001F600aby" };
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, "x\U0001F600abc" };
    }

    [Theory]
    [MemberData(nameof(LiteralEnumerationNonAsciiCases))]
    public void LiteralEnumerationMatchesOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expected = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((expectedMatch.Index, expectedMatch.Length));
        }

        var actual = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (var valueMatch in regex.EnumerateMatches(input))
        {
            actual.Add((valueMatch.IndexInUtf16, valueMatch.LengthInUtf16));
        }

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Index, actual[i].Index);
            Assert.Equal(expected[i].Length, actual[i].Length);
        }

        Assert.Equal(oracle.Matches(text).Count, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> GroupedLiteralZeroConstructionCases()
    {
        string[] patterns = ["(a)(b)", "(a)(b)(c)", "((a))", "(a)?b(c)?", "(a)?(b)?c", "(x)?(y)?", "(a)?(a)?", "(ab)?(ab)?", "(a)?(a)?(a)?", "((a)?(b)?)", "(?<x>a)?(?<y>b)?", "(?<x>a)(?<y>b)", "(?<a>x)(b)(?<c>y)", "(ab)", "(a)", "(1)(2)", "(a)?b", "a(b)?", "(a)?(b)", "((a)?b)", "(a(b)?)", "(?<x>a)?b", "(a)(?:b)?c", "(ab|cd)e", "(?<w>ab|cd)e", "a(b|c)d", "(a|b)c", "x(a|b)y", "((a|b)(c|d))", "(ab|c)(d)", "(a|bc)d", "(x(a)|(b)y)", "(a)(b)|(c)", "((a|b)c)", "(?<x>ab|(?<y>c))d", "(?<x>(?<y>a))", "((?<x>a)(b))", "(?<x>(a)(b))", "(?<x>a(?<y>b))", "(a(?<x>b))", "((?<x>a)|(?<y>a))b", "((a)|(a))"];
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

    public static IEnumerable<object?[]> LazyLoopOrdinaryCases()
    {
        // Lazy loops serve the ordinary lane through the slice program but construct
        // the fallback engine on the compiled lane (compiled discovery stays BCL for
        // lazy shapes); the loop-pattern map covers the compiled lane only where its
        // discovery is owned, so these rows pin ordinary-lane zero construction.
        string[] patterns = ["(a)??(b)??", "(a)?(a)??"];
        string[] texts = ["xabcy", "ab", "b", "xyz"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(LazyLoopOrdinaryCases))]
    public void LazyLoopOrdinaryOperationsConstructNoFallbackEngine(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var isMatchRegex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);

        var detailedRegex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(oracle.Match(text).Success, detailedRegex.MatchDetailed(input).Success);
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> GroupedEvaluatorCases()
    {
        string[] patterns =
        [
            "(a)(b)",
            "(a)?b",
            "(ab|c)(d)",
            "(?<x>ab|(?<y>c))d",
            "(ab|cd)e",
            "((a|b)(c|d))",
            "(?<x>(?<y>a))",
            "(a)?",
            "(a)?b(c)?",
            "(a)?(b)?c",
        ];
        string[] texts = ["xabx", "abd", "xxabdex", "ac", "xyz", "a", "b"];
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
    [MemberData(nameof(GroupedEvaluatorCases))]
    public void GroupedEvaluatorMatchesOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Groups[1].Value + "]");

        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(1).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(1).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> GroupedTemplateCases()
    {
        string[] patterns =
        [
            "(a)(b)",
            "(a)?b",
            "(ab|c)(d)",
            "(?<x>ab|(?<y>c))d",
            "(ab|cd)e",
            "((a|b)(c|d))",
            "(?<x>(?<y>a))",
            "(a)?",
            "(ab)",
            "(?<a>x)(b)(?<c>y)",
            "(a)?b(c)?",
            "(a)?(b)?c",
        ];
        string[] templates = ["<$1$2>", "$1-$2", "[$0][$1]", "$`|$'|$_", "$+", "$$", "<$9>", "x", "$&"];
        string[] texts = ["xabx", "abd", "xyz", "a", "b"];
        foreach (var pattern in patterns)
        {
            foreach (var template in templates)
            {
                foreach (var text in texts)
                {
                    yield return new object?[] { pattern, RegexOptions.CultureInvariant, template, text };
                    yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, template, text };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GroupedTemplateCases))]
    public void GroupedTemplateMatchesOracle(string pattern, RegexOptions options, string template, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> GrouplessTemplateCases()
    {
        string[] patterns =
        [
            "ab|cd",
            "cat|horse|dog",
            "a{2,4}",
            "a*",
            @"\d+",
            "[a-z]+",
            "^abc",
            "abc$",
            "ab[0-9]d",
            "a.*b",
            ".*",
            "café",
            "(?:ab)+",
            "(?i:ab)",
        ];
        string[] templates = ["x", "<$1>", "$&", "$`", "$'", "$_", "$+", "$$"];
        string[] texts = ["xxab12cdxx", "nomatchhere", "ééabcé", ""];
        foreach (var pattern in patterns)
        {
            foreach (var template in templates)
            {
                foreach (var text in texts)
                {
                    yield return new object?[] { pattern, RegexOptions.CultureInvariant, template, text };
                    yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, template, text };
                }
            }
        }
    }

    public static IEnumerable<object?[]> SingleLiteralTemplateCases()
    {
        string[] patterns = ["a.c", "abc"];
        string[] templates = ["x", "<$1>", "$&", "$`", "$'", "$_", "$+", "$$"];
        string[] texts = ["xxab12cdxx", "nomatchhere", ""];
        foreach (var pattern in patterns)
        {
            foreach (var template in templates)
            {
                foreach (var text in texts)
                {
                    yield return new object?[] { pattern, RegexOptions.CultureInvariant, template, text };
                    yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, template, text };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(SingleLiteralTemplateCases))]
    public void SingleLiteralTemplateMatchesOracle(string pattern, RegexOptions options, string template, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("a.c")]
    public void SingleLiteralNonAsciiTemplateKeepsBclShaping(string pattern)
    {
        // Declared R05 debt: non-literal single shapes (a.c here) still divert
        // to BCL verification on non-ASCII input (established R03 projection
        // rule), so templates there construct. Exact ASCII literals (abc)
        // graduated to owned enumeration with incremental projection; their
        // templates are covered owned below. Parity holds; construction is pinned.
        const string text = "ééabcé";
        foreach (var template in new[] { "x", "<$1>", "$&", "$`", "$'", "$_", "$+", "$$" })
        {
            foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
            {
                var input = Encoding.UTF8.GetBytes(text);
                var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
                var regex = new Utf8Regex(pattern, options);
                var expected = oracle.Replace(text, template);
                var actual = regex.ReplaceToString(input, template);

                Assert.Equal(expected, actual);
                Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
            }
        }
    }

    public static IEnumerable<object?[]> SingleLiteralNonAsciiTemplateOwnedCases()
    {
        string[] templates = ["x", "<$1>", "$&", "$`", "$'", "$_", "$+", "$$"];
        string[] texts = ["ééabcé", "xaby", "nomatchhere"];
        foreach (var template in templates)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { RegexOptions.CultureInvariant, template, text };
                yield return new object?[] { RegexOptions.Compiled | RegexOptions.CultureInvariant, template, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(SingleLiteralNonAsciiTemplateOwnedCases))]
    public void SingleLiteralNonAsciiTemplateMatchesOwned(RegexOptions options, string template, string text)
    {
        const string pattern = "abc";
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(GrouplessTemplateCases))]
    public void GrouplessTemplateMatchesOracle(string pattern, RegexOptions options, string template, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> MultiLoopOverCapCases()
    {
        (string Pattern, string Template, string[] Texts)[] shapes =
        [
            ("(a)?b(c)?d(e)?f(g)?h(i)?", "<$1$2$3$4$5>", ["abcdefghi", "bdfhi", "xyz"]),
            ("(a)?(b)?(c)?(d)?(e)?", "<$1$2$3$4$5>", ["abcde", "ace", "xyz"]),
            ("(a)??b(c)?d(e)??f(g)?h(i)?", "<$1$2$3$4$5>", ["abcdefghi", "bdfhi", "xyz"]),
            ("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?", "<$1$2$3$4$5$6>", ["abcdefghijk", "bdfhj", "xyz"]),
            ("(a)?(b)?(c)?(d)?(e)?(f)?", "<$1$2$3$4$5$6>", ["abcdef", "ace", "xyz"]),
        ];
        foreach (var shape in shapes)
        {
            // Mixed greedy/lazy shapes stay BCL-backed on the compiled lane
            // (lazy-compiled discovery debt); greedy shapes cover both lanes.
            var compiledLaneIsOwned = shape.Pattern.IndexOf(")??", System.StringComparison.Ordinal) < 0;
            foreach (var text in shape.Texts)
            {
                yield return new object?[] { shape.Pattern, shape.Template, RegexOptions.CultureInvariant, text };
                if (compiledLaneIsOwned)
                {
                    yield return new object?[] { shape.Pattern, shape.Template, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(MultiLoopOverCapCases))]
    public void MultiLoopOverCapDetailedMatchesOwned(string pattern, string template, RegexOptions options, string text)
    {
        _ = template;
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
                Assert.Equal(expected.Groups[i].Length, actual.GetGroup(i).LengthInUtf16);
            }
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(MultiLoopOverCapCases))]
    public void MultiLoopOverCapTemplateMatchesOwned(string pattern, string template, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("((a)?b)?", "ab", "<$1>")]
    [InlineData("((a)?b)?", "éabé", "<$1>")]
    [InlineData("(a|b)?(c)?(d)?(e)?(f)?", "ace", "<$1$2>")]
    [InlineData("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?", "abcdefghijklm", "<$1$7>")]
    public void NestedComboTemplateKeepsBclShaping(string pattern, string text, string template)
    {
        // Declared R05 debt: nested loops, loop-plus-alternation combos, and
        // seven-loop shapes expand group-reference templates through the fallback
        // engine (the span maps decline nesting/combos; the loop cap stops at six).
        // Parity holds; construction is pinned.
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);
            var expected = oracle.Replace(text, template);
            var actual = regex.ReplaceToString(input, template);

            Assert.Equal(expected, actual);
            Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

}
