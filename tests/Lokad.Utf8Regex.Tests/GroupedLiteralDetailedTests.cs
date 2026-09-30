using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R05 grouped-literal detailed shaping: single exact literals with fixed-span captures
/// shape detailed matches from the owned discovery hit without constructing the fallback
/// engine. Single-optional shapes project present or absent spans by hit length.
/// Divergent single alternations project the winning branch identified by ordinal
/// text comparison. Nested divergent alternations and loops inside alternation
/// branches expand to one flat BCL-ordered entry list and shape the same way.
/// A lone capture wrapping the whole tree projects group 1 from the whole span.
/// Nested loops, loop-plus-alternation combos, and seven-loop shapes keep BCL shaping.
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
            "(a|ab|abc|abcd|abcde)",
            "(a|ab|abc|abcd|abcde|abcdef|abcdefg|abcdefgh)",
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
        // Single-maximum lazies (??) serve both lanes through the slice program
        // (probe-mapped lane agreement), so these rows pin ordinary-lane zero
        // construction while LazyOptionalCompiledDiscoveryStaysOwned covers
        // the compiled lane.
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
    public void SingleNonLiteralTemplateOwnsNonAsciiInput(string pattern)
    {
        // R05: groupless non-literal single shapes expand every template form
        // owned through the scalar enumeration above, including group references
        // that project empty on groupless shapes exactly like the oracle.
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
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
                Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated, "pattern=" + pattern);
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
            // Mixed greedy/lazy shapes serve owned on both lanes: single-maximum
            // lazies agree on both oracle lanes (probe-mapped), so the compiled
            // lane runs the same slice program as the ordinary lane. Finite lazy
            // maxima above one stay BCL-backed on the compiled lane and are pinned
            // in LazyFallbackConstructionTests.
            foreach (var text in shape.Texts)
            {
                yield return new object?[] { shape.Pattern, shape.Template, RegexOptions.CultureInvariant, text };
                yield return new object?[] { shape.Pattern, shape.Template, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
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

    public static System.Collections.Generic.IEnumerable<object?[]> SliceComboTemplateOwnedCases()
    {
        (string pattern, string template, string text)[] rows =
        [
            ("((a)?b)?", "x", "ab"),
            ("((a)?b)?", "x", "xyz"),
            ("((a)?b)?", "x", "éabé"),
            ("((a)?b)?", "<$1>", "ab"),
            ("((a)?b)?", "<$1>", "xyz"),
            ("((a)?b)?", "<$1>", "éabé"),
            ("((a)?b)?", "$&", "ab"),
            ("((a)?b)?", "$+", "ab"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "x", "ace"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "x", "xyz"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "x", "éaé"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "<$1$2>", "ace"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "<$1$2>", "xyz"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "<$1$2>", "éaé"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "$&", "ace"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "$+", "ace"),
            ("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?", "x", "abcdefghijklm"),
            ("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?", "x", "xyz"),
            ("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?", "<$1$7>", "abcdefghijklm"),
            ("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?", "<$1$7>", "xyz"),
            ("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?", "$&", "abcdefghijklm"),
            ("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?", "$+", "abcdefghijklm"),
            ("(a|b)?(c)?", "x", "ac"),
            ("(a|b)?(c)?", "x", "xyz"),
            ("(a|b)?(c)?", "<$1>", "ac"),
            ("(a|b)?(c)?", "$&", "ac"),
            ("(a)?b(c)?", "x", "abc"),
            ("(a)?b(c)?", "x", "xyz"),
            ("(a)?b(c)?", "<$1>", "abc"),
            ("(a)?b(c)?", "$&", "abc"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.template, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.template, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(SliceComboTemplateOwnedCases))]
    public void SliceComboTemplateStringCoreOwnsInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: slice shapes expand group-reference templates through the owned
        // slice replacement on the string core, mirroring the byte core.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = regex.ReplaceToString(input, template);

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(SliceComboTemplateOwnedCases))]
    public void SliceComboTemplateByteCoreOwnsInput(string pattern, RegexOptions options, string template, string text)
    {
        // Control: the byte core already serves slice shapes owned; these rows
        // pin the shared behavior while the string and destination cores graduate.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = Encoding.UTF8.GetString(regex.Replace(input, template));

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(SliceComboTemplateOwnedCases))]
    public void SliceComboTemplateDestinationCoreOwnsInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: the destination core serves slice shapes owned through the same
        // slice replacement instead of the BCL-backed shared plan.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var destination = new byte[4096];
        var status = regex.TryReplace(input, template, destination, out var bytesWritten);

        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination.AsSpan(0, bytesWritten)));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SliceComboEvaluatorOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("((a)?b)?", "ab"),
            ("((a)?b)?", "xyz"),
            ("((a)?b)?", "éabé"),
            ("(a|b)?(c)?", "ac"),
            ("(a|b)?(c)?", "xyz"),
            ("(a|b)?(c)?", "éaé"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "ace"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "xyz"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "éaé"),
            ("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?", "abcdefghijklm"),
            ("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?", "xyz"),
            ("(a)?b(c)?d(e)?f(g)?h(i)?j(k)?l(m)?", "éabcdefghijklmé"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(SliceComboEvaluatorOwnedCases))]
    public void SliceComboEvaluatorOwnsInput(string pattern, RegexOptions options, string text)
    {
        // R05: evaluator callbacks over slice shapes project every callback
        // argument from a deterministic detailed re-query at the enumerated hit,
        // without constructing the fallback engine.
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
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(1).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SliceComboEvaluatorTimeoutCases()
    {
        (string pattern, string text)[] rows =
        [
            ("((a)?b)?", "ab"),
            ("(a|b)?(c)?(d)?(e)?(f)?", "ace"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(SliceComboEvaluatorTimeoutCases))]
    public void SliceComboEvaluatorFiniteTimeoutOwnsInput(string pattern, RegexOptions options, string text)
    {
        // Finite timeouts keep the owned slice routing with deadline mapping;
        // the evaluator worker carries the invocation deadline, unlike the
        // group-reference template worker which diverts to BCL.
        var timeout = System.TimeSpan.FromSeconds(5);
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Groups[1].Value + "]");

        var byteRegex = new Utf8Regex(pattern, options, timeout);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(1).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options, timeout);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(1).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> FiniteTimeoutTemplateCases()
    {
        string[] patterns = ["(a)(b)", "(a)?b(c)?", "(?<w>a)(b)", "((a)(b))", "(a)(b)(c)"];
        string[] templates = ["x", "q$&q", "q$1q", "<$1$2>", "$$", "$+", "$`", "$'", "$_", "${w}", "$2$1", "x$1y$2z"];
        string[] texts = ["xxabxx"];
        foreach (var pattern in patterns)
        {
            foreach (var template in templates)
            {
                foreach (var text in texts)
                {
                    yield return new object?[] { pattern, template, text };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(FiniteTimeoutTemplateCases))]
    public void FiniteTimeoutTemplateRoutingMatchesContract(string pattern, string template, string text)
    {
        // Finite timeouts keep BCL timeout enforcement for group-reference expansion
        // (the owned template worker carries no deadline mapping); all other template
        // forms stay owned. Parity holds everywhere; construction follows the route.
        var timeout = System.TimeSpan.FromSeconds(5);
        var options = RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var regex = new Utf8Regex(pattern, options, timeout);
        var actual = regex.ReplaceToString(input, template);
        Assert.Equal(expected, actual);

        // LastGroup and named references need the same group machinery as numbered
        // references; whole-match, before/after/input, escaped-dollar, and literal
        // forms stay owned. The rule covers only the template forms used in this battery.
        var expectConstruction = template.Contains("$1") || template.Contains("$2") ||
            template == "$+" || template.Contains("${");
        Assert.Equal(expectConstruction, regex.Inspection.DebugFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> NestedInvariantAlternationCases()
    {
        string[] patterns = ["((a|b)c)", "(a(b|c)d)"];
        string[] texts = ["xacx", "xbcx", "xabdx", "xyz"];
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
    [MemberData(nameof(NestedInvariantAlternationCases))]
    public void NestedInvariantAlternationDetailedOwnsInput(string pattern, RegexOptions options, string text)
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
                Assert.Equal(expected.Groups[i].Length, actual.GetGroup(i).LengthInUtf16);
            }
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> NestedDivergentOwnedCases()
    {
        string[] patterns =
        [
            "((a|b)|(c|d))",
            "((a)?b|c)",
            "(((a|b)|c)|d)",
            "((a)|(a))",
            "((a|ab)|(c))",
        ];
        string[] texts = ["xacx", "xbcx", "xabx", "xbx", "xcx", "xdx", "xyz", "Ã©acÃ©", "xabdx", "xac\U0001F600", "\U0001F600acx", "x\U0001F600cx"];
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
    [MemberData(nameof(NestedDivergentOwnedCases))]
    public void NestedDivergentDetailedOwnsInput(string pattern, RegexOptions options, string text)
    {
        // R05: nested divergent alternations and loops inside alternation branches
        // expand to one flat BCL-ordered entry list, so detailed shaping projects
        // the winning entry identified by ordinal text comparison without the
        // fallback engine.
        // Astral-adjacent texts join this routing (candidate search crossing or abutting a scalar still resolves the winning entry without the fallback engine).
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

    public static System.Collections.Generic.IEnumerable<object?[]> NestedDivergentFromOffsetCases()
    {
        (string pattern, string text, int offset)[] rows =
        [
            ("((a|b)|(c|d))", "xacx", 0),
            ("((a|b)|(c|d))", "xacx", 2),
            ("((a|b)|(c|d))", "xyz", 1),
            ("((a)?b|c)", "xabx", 0),
            ("((a)?b|c)", "xabx", 2),
            ("((a)?b|c)", "xcx", 1),
            ("(((a|b)|c)|d)", "xadx", 0),
            ("(((a|b)|c)|d)", "xadx", 2),
            ("((a)|(a))", "xaa", 0),
            ("((a)|(a))", "xaa", 2),
            ("((a|ab)|(c))", "xabx", 0),
            ("((a|ab)|(c))", "xabx", 1),
            ("((a|b)|(c|d))", "xac\U0001F600", 0),
            ("((a|b)|(c|d))", "\U0001F600acx", 0),
            ("((a|b)|(c|d))", "x\U0001F600cx", 0),
            ("((a)?b|c)", "xac\U0001F600", 0),
            ("((a)?b|c)", "\U0001F600acx", 0),
            ("((a)?b|c)", "x\U0001F600cx", 0),
            ("(((a|b)|c)|d)", "xac\U0001F600", 0),
            ("(((a|b)|c)|d)", "\U0001F600acx", 0),
            ("(((a|b)|c)|d)", "x\U0001F600cx", 0),
            ("((a)|(a))", "xac\U0001F600", 0),
            ("((a)|(a))", "\U0001F600acx", 0),
            ("((a)|(a))", "x\U0001F600cx", 0),
            ("((a|ab)|(c))", "xac\U0001F600", 0),
            ("((a|ab)|(c))", "\U0001F600acx", 0),
            ("((a|ab)|(c))", "x\U0001F600cx", 0),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(NestedDivergentFromOffsetCases))]
    public void NestedDivergentFromOffsetDetailedOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {
        // FromOffset keeps absolute-origin semantics through the shared
        // first-of-enumeration arm and projects the winning nested entry.
        // Boundary starts on astral-adjacent input serve owned; half-scalar starts keep BCL routing, see NestedDivergentHalfOffsetDeclineCases.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text, offset);
        var actual = regex.MatchDetailedFromUtf16Offset(input, offset);

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

    public static System.Collections.Generic.IEnumerable<object?[]> NestedDivergentTemplateCases()
    {
        string[] patterns =
        [
            "((a|b)|(c|d))",
            "((a)?b|c)",
            "(((a|b)|c)|d)",
            "((a)|(a))",
            "((a|ab)|(c))",
        ];
        string[] templates = ["x", "<$1>", "$&", "$+"];
        string[] texts = ["xacx", "xabx", "xyz", "Ã©abÃ©", "xac\U0001F600", "\U0001F600acx", "x\U0001F600cx"];
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
    [MemberData(nameof(NestedDivergentTemplateCases))]
    public void NestedDivergentTemplateOwnsInput(string pattern, RegexOptions options, string template, string text)
    {
        // Group-reference templates expand through the same nested entry spans
        // via the shared owned template worker.
        // Astral-adjacent texts expand through the same spans with zero fallback construction.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = regex.ReplaceToString(input, template);

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> NestedDivergentEvaluatorCases()
    {
        string[] patterns =
        [
            "((a|b)|(c|d))",
            "((a)?b|c)",
            "(((a|b)|c)|d)",
        ];
        string[] texts = ["xacx", "xabx", "xyz", "xac\U0001F600", "\U0001F600acx", "x\U0001F600cx"];
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
    [MemberData(nameof(NestedDivergentEvaluatorCases))]
    public void NestedDivergentEvaluatorMatchesOracle(string pattern, RegexOptions options, string text)
    {
        // Evaluator callbacks observe the same nested entry spans.
        // Astral-adjacent texts project the same spans on both evaluator cores.
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

    public static System.Collections.Generic.IEnumerable<object?[]> NestedDivergentHalfOffsetDeclineCases()
    {
        (string pattern, string text, int offset)[] rows =
        [
            ("((a|b)|(c|d))", "x\U0001F600cx", 2),
            ("((a)?b|c)", "x\U0001F600cx", 2),
            ("(((a|b)|c)|d)", "x\U0001F600cx", 2),
            ("((a)|(a))", "x\U0001F600cx", 2),
            ("((a|ab)|(c))", "x\U0001F600cx", 2),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(NestedDivergentHalfOffsetDeclineCases))]
    public void NestedDivergentHalfOffsetDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, int offset)
    {
        // R05: a Detailed FromOffset query starting mid-scalar keeps BCL routing
        // with oracle parity on both lanes (the shared shaper only resolves
        // entries from scalar starts); boundary starts serve owned, see
        // NestedDivergentFromOffsetCases.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text, offset);
        var actual = regex.MatchDetailedFromUtf16Offset(input, offset);
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
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SingleWrapDetailedCases()
    {
        string[] patterns =
        [
            "(a.c)",
            "(ab|cd)",
            "([0-9]+)",
            "(\\d{2,4})",
            "(?<x>a.c)",
            "((?>a.c))",
            "(|)",
        ];
        string[] texts = ["xacx", "xxabxx", "éabcé", "éab12é", "xyz", ""];
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
    [MemberData(nameof(SingleWrapDetailedCases))]
    public void SingleWrapDetailedOwnsInput(string pattern, RegexOptions options, string text)
    {
        // R05: a lone capture wrapping the whole tree spans the hit exactly, so
        // detailed shaping projects group 1 from the whole span without the
        // fallback engine, no matter what the wrap encloses.
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

    public static System.Collections.Generic.IEnumerable<object?[]> SingleWrapFromOffsetCases()
    {
        (string pattern, string text, int offset)[] rows =
        [
            ("(a.c)", "xacx", 0),
            ("(a.c)", "xacx", 2),
            ("(a.c)", "éabcé", 2),
            ("(ab|cd)", "xxcdxx", 2),
            ("([0-9]+)", "ab123cd", 2),
            ("(\\d{2,4})", "é12é34", 2),
            ("(?<x>a.c)", "éabcé", 0),
            ("((?>a.c))", "xacx", 1),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(SingleWrapFromOffsetCases))]
    public void SingleWrapFromOffsetDetailedOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {
        // FromOffset keeps absolute-origin semantics through the shared
        // first-of-enumeration arm and projects the single wrap the same way.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text, offset);
        var actual = regex.MatchDetailedFromUtf16Offset(input, offset);

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

    public static System.Collections.Generic.IEnumerable<object?[]> SingleWrapEvaluatorCases()
    {
        string[] patterns =
        [
            "(a.c)",
            "(ab|cd)",
            "([0-9]+)",
            "(?<x>a.c)",
        ];
        string[] texts = ["xacx", "xxabxx", "éabcé", "xyz"];
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
    [MemberData(nameof(SingleWrapEvaluatorCases))]
    public void SingleWrapEvaluatorOwnsInput(string pattern, RegexOptions options, string text)
    {
        // Evaluator callbacks observe the same single-wrap spans on both cores.
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
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(1).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SingleWrapTemplateAllCoresCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(a.c)", "xacx"),
            ("(a.c)", "éabcé"),
            ("(a.c)", "xyz"),
            ("(?<x>a.c)", "xacx"),
            ("(?<x>a.c)", "éabcé"),
            ("(?<x>a.c)", "xyz"),
            ("(\\d{2,4})", "xx12"),
            ("(\\d{2,4})", "é12é"),
            ("(\\d{2,4})", "xyz"),
            ("(ab|cd)", "xabx"),
            ("(ab|cd)", "xyz"),
        ];
        string[] templates = ["<$1>", "x"];
        foreach (var row in rows)
        {
            foreach (var template in templates)
            {
                yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, template, row.text };
                yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, template, row.text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(SingleWrapTemplateAllCoresCases))]
    public void SingleWrapTemplateStringCoreOwnsInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: single-wrap group references expand owned on the string core
        // through the shared single-wrap shaping.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = regex.ReplaceToString(input, template);

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(SingleWrapTemplateAllCoresCases))]
    public void SingleWrapTemplateByteCoreOwnsInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: the byte core rides the same single-wrap shaping.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = Encoding.UTF8.GetString(regex.Replace(input, template));

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(SingleWrapTemplateAllCoresCases))]
    public void SingleWrapTemplateDestinationCoreOwnsInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: the destination core serves the same single-wrap templates owned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var destination = new byte[4096];
        var status = regex.TryReplace(input, template, destination, out var bytesWritten);

        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination.AsSpan(0, bytesWritten)));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
}
