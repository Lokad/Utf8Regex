using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// R03 right-to-left: BCL parses every main-stream concatenation back-to-front
// and executes it with mirrored movement (scan starts right, attempts consume
// left, anchors stay absolute, unmatched backreferences fail in execution
// order, lookahead bodies run left-to-right). The slice mirrors that design:
// main-stream reversal at parse, mirrored runner arms, sub-program lookahead
// bodies, shared-slot lookbehind machinery, normalized capture spans. Bare
// shapes keep native precedence; quantified shapes route owned. Balancing,
// exotic assertion mixes, and right-to-left splits decline to legacy-exact
// routing with identical behavior.
public sealed class Utf8SliceRtlTests
{
    public static IEnumerable<object?[]> SliceRtlShapes()
    {
        yield return new object?[] { "(a+)(b+)" };
        yield return new object?[] { "a|ab" };
        yield return new object?[] { "a(?=b)" };
        yield return new object?[] { "(?<=a)b" };
        yield return new object?[] { "(a)\\1" };
        yield return new object?[] { "(?:ab)+" };
        yield return new object?[] { "(?<n>a)b" };
        yield return new object?[] { "(?>a+)b" };
        yield return new object?[] { "a{2,3}" };
        yield return new object?[] { "(a)+" };
        yield return new object?[] { "\\d+" };
        yield return new object?[] { "^abc$" };
    }

    [Theory]
    [MemberData(nameof(SliceRtlShapes))]
    public void RtlShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.RightToLeft,
            RegexOptions.RightToLeft | RegexOptions.CultureInvariant,
            RegexOptions.RightToLeft | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, options.ToString() + " pattern=" + pattern);
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("a")]
    public void RtlBareShapesKeepNativePrecedence(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var input = Encoding.UTF8.GetBytes("xxabcxx");
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.IsMatch("xxabcxx"), regex.IsMatch(input));
    }

    public static IEnumerable<object?[]> RtlDeclinedShapes()
    {
        // Balancing intervals assume forward positions.
        yield return new object?[] { "(?<open>a+)(?<-open>b+)", "aabb" };
        // Mirrored lookbehind bodies run backreference-first (always missing).
        yield return new object?[] { "(?<=(a)\\1)b", "aab" };
        // Nested assertions inside lookbehind bodies take different positions.
        yield return new object?[] { "(?<=(?=a)b)ab", "ab" };
        yield return new object?[] { "(?<=(?<=a)b)ab", "aab" };
        // Lookahead bodies run left-to-right with order-sensitive content.
        yield return new object?[] { "(?=(a)\\1)aa", "aa" };
        yield return new object?[] { "(?=(?<=a)b)ab", "ab" };
    }

    [Theory]
    [MemberData(nameof(RtlDeclinedShapes))]
    public void RtlExoticShapesDeclineToLegacy(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
    }

    public static IEnumerable<object?[]> RtlParserDeclines()
    {
        yield return new object?[] { "(?<open>a+)(?<-open>b+)" };
        yield return new object?[] { "(?<=(a)\\1)b" };
        yield return new object?[] { "(?<=(?=a)b)ab" };
        yield return new object?[] { "(?<=(?<=a)b)ab" };
        yield return new object?[] { "(?=(a)\\1)aa" };
        yield return new object?[] { "(?=(?<=a)b)ab" };
    }

    [Theory]
    [MemberData(nameof(RtlParserDeclines))]
    public void RtlExoticShapesDeclineAtParse(string pattern)
    {
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> RtlParserShapes()
    {
        yield return new object?[] { "abc" };
        yield return new object?[] { "a|ab" };
        yield return new object?[] { "(a+)(b+)" };
        yield return new object?[] { "^a$" };
        yield return new object?[] { "\\Aa\\z" };
        yield return new object?[] { "a(?=b)" };
        yield return new object?[] { "(?<=a)b" };
        yield return new object?[] { "(a)\\1" };
        yield return new object?[] { "\\Ga" };
        yield return new object?[] { "a\\G" };
        yield return new object?[] { "(?<n>a)" };
        yield return new object?[] { "(?>a+)" };
        yield return new object?[] { "(?:ab)+" };
        yield return new object?[] { "[a-z]+" };
        yield return new object?[] { "\\d+" };
        yield return new object?[] { "a.c" };
    }

    [Theory]
    [MemberData(nameof(RtlParserShapes))]
    public void RtlOrdinaryShapesParse(string pattern)
    {
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> ControlRtlShapes()
    {
        yield return new object?[] { "\\t+" };
        yield return new object?[] { "\\cA+" };
        yield return new object?[] { "(?<=\\n)x" };
    }

    [Theory]
    [MemberData(nameof(ControlRtlShapes))]
    public void ControlRtlShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.RightToLeft,
            RegexOptions.RightToLeft | RegexOptions.CultureInvariant,
            RegexOptions.RightToLeft | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "abc", "xxabcxxabcxx", true };
        yield return new object?[] { "abc", "ab", false };
        yield return new object?[] { "a+", "aba", true };
        yield return new object?[] { "a+?", "aab", true };
        yield return new object?[] { "^abc", "abc", true };
        yield return new object?[] { "^abc", "xxabcxxabcxx", false };
        yield return new object?[] { "abc$", "abc", true };
        yield return new object?[] { "abc$", "xxabcxxabcxx", false };
        yield return new object?[] { "^b", "ba", true };
        yield return new object?[] { "^b", "ab", false };
        yield return new object?[] { "b$", "ab", true };
        yield return new object?[] { "b$", "ba", false };
        yield return new object?[] { "(?<=\\p{L})x", "axb", true };
        yield return new object?[] { "(?<=\\p{IsBasicLatin})x", "axb", true };
        yield return new object?[] { "a$", "aaa", true };
        yield return new object?[] { "^a", "abx", true };
        yield return new object?[] { "\\Aabc", "abc", true };
        yield return new object?[] { "abc\\z", "abc", true };
        yield return new object?[] { "abc\\Z", "abc", true };
        yield return new object?[] { "\\Z", "ab", true };
        yield return new object?[] { "$", "ab", true };
        yield return new object?[] { "^", "ab", true };
        yield return new object?[] { "a|ab", "aab", true };
        yield return new object?[] { "a|ab", "xaab\U0001F600x", true };
        yield return new object?[] { "a+", "xaay\U0001F600", true };
        yield return new object?[] { "x*", "ax\U0001F600b", true };
        yield return new object?[] { "ab|a", "aab", true };
        yield return new object?[] { "(a)(b)", "ab", true };
        yield return new object?[] { "(a+)(b+)", "aab", true };
        yield return new object?[] { "(a)\\1", "aa", false };
        yield return new object?[] { "(a*)\\1b", "aab", false };
        yield return new object?[] { "a(?=b)", "ab", true };
        yield return new object?[] { "a(?!b)", "aaa", true };
        yield return new object?[] { "a(?!b)", "ab", false };
        yield return new object?[] { "(?<=a)b", "ab", true };
        yield return new object?[] { "(?<!a)b", "ba", true };
        yield return new object?[] { "(?<=a)b", "ba", false };
        yield return new object?[] { "a\\b", "ab", false };
        yield return new object?[] { "\\ba", "xab", false };
        yield return new object?[] { "\\Gabc", "abc", false };
        yield return new object?[] { "\\Ga", "aa", false };
        yield return new object?[] { "a\\G", "aa", true };
        yield return new object?[] { "\\Ga*", "aa", true };
        yield return new object?[] { "\\G", "aa", true };
        yield return new object?[] { "a*", "b", true };
        yield return new object?[] { "(?=a)a", "aa", true };
        yield return new object?[] { "(a)(?=b)", "ab", true };
        yield return new object?[] { "(?=(a)b)\\1", "ab", false };
        yield return new object?[] { "(?<=a|ab)b", "abb", true };
        yield return new object?[] { "(?>a+)b", "aab", true };
        yield return new object?[] { "(?>a+)", "aaa", true };
        yield return new object?[] { "(?:ab)+", "abab", true };
        yield return new object?[] { "(?#c)ab", "xab", true };
        yield return new object?[] { "(a)", "a", true };
        yield return new object?[] { ".", "\u00e9", true };
        yield return new object?[] { "\\b", "a\U0001F600b", true };
        yield return new object?[] { "[ab]+", "\u00e9ab", true };
        yield return new object?[] { "[^ab]+", "\u00e9ab", true };
        yield return new object?[] { "\\d+", "12\u066334", true };
        yield return new object?[] { "x$", "x\U0001F600", false };
        yield return new object?[] { "a+", "a\U0001F600a", true };
        yield return new object?[] { "(?<=\\w)b", "ab", true };
        yield return new object?[] { "(?<!\\W)b", "ab", true };
        yield return new object?[] { "(?<=a*)b", "aaab", true };
        yield return new object?[] { "(?<=a+)b", "aaab", true };
        yield return new object?[] { "(?<=a+?)b", "aaab", true };
        yield return new object?[] { "\\t+", "\ta\t", true };
        yield return new object?[] { "\\t+", "abc", false };
        yield return new object?[] { "\\cA+", "\u0001x\u0001", true };
        yield return new object?[] { "(?<=\\n)x", "a\nx", true };
        yield return new object?[] { "(?<=\\n)x", "ax", false };
        yield return new object?[] { "x*", "\U0001F600", true };
        yield return new object?[] { "a*", "a\U0001F600a", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedRtlIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.RightToLeft,
            RegexOptions.RightToLeft | RegexOptions.CultureInvariant,
            RegexOptions.RightToLeft | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            if (regex.Inspection.DebugUsesOwnedSliceProgram)
            {
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, options.ToString() + " pattern=" + pattern);
            }
        }
    }

    [Theory]
    [InlineData("a b", "ab", true)]
    [InlineData("(a)", "a", true)]
    [InlineData("a.c", "axc", true)]
    [InlineData("ab+", "AB", true)]
    public void OwnedRtlOptionShapesMatchOracle(string pattern, string inputText, bool expected)
    {
        var options = RegexOptions.RightToLeft | RegexOptions.CultureInvariant;
        if (pattern == "a b")
        {
            options |= RegexOptions.IgnorePatternWhitespace;
        }

        if (pattern == "(a)")
        {
            options |= RegexOptions.ExplicitCapture;
        }

        if (pattern == "a.c")
        {
            options |= RegexOptions.Singleline;
        }

        if (pattern == "ab+")
        {
            options |= RegexOptions.IgnoreCase;
        }

        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a")]
    [InlineData("a{3,2}")]
    [InlineData("[z-a]")]
    public void RtlMalformedShapesFailConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(
            () => new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData(".", "\U0001F600", true)]
    [InlineData("a.c", "a\U0001F600c", false)]
    [InlineData("^.", "\U0001F600x", true)]
    public void RtlAstralDivergentShapesDeclineToLegacy(string pattern, string inputText, bool expected)
    {
        // Astral inputs decline dot shapes to the fallback engine at routing
        // (BCL unit semantics split astral scalars while scalar profiles do
        // not). Construction stays lazy; correctness rides the oracle.
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "construction must stay lazy");
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
}
public sealed class Utf8SliceRtlOperationTests
{
    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "a|ab", "aab", 2 };
        yield return new object?[] { "a|ab", "xaab\U0001F600x", 2 };
        yield return new object?[] { "a+", "xaay\U0001F600", 1 };
        yield return new object?[] { "x*", "ax\U0001F600b", 6 };
        yield return new object?[] { "a*", "aaa", 2 };
        yield return new object?[] { "a*", "b", 2 };
        yield return new object?[] { "x*", "abx", 4 };
        yield return new object?[] { "a+", "aba", 2 };
        yield return new object?[] { "\\G", "aa", 1 };
        yield return new object?[] { "a\\G", "aa", 2 };
        yield return new object?[] { "^a", "aa", 1 };
        yield return new object?[] { "(?<=a)b", "aab", 1 };
        yield return new object?[] { "(a+)(b+)", "aab", 1 };
        yield return new object?[] { "(a)\\1", "aa", 0 };
        yield return new object?[] { "x*", "\U0001F600", 3 };
        yield return new object?[] { "a*", "a\U0001F600a", 5 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedRtlCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        foreach (var options in new[]
        {
            RegexOptions.RightToLeft | RegexOptions.CultureInvariant,
            RegexOptions.RightToLeft | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.Equal(expected, regex.Count(input));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> FirstMatchCases()
    {
        yield return new object?[] { "a+?", "aab", 1, 1 };
        yield return new object?[] { "a|ab", "aba", 2, 1 };
        yield return new object?[] { "(a+)(b+)", "aab", 0, 3 };
        yield return new object?[] { "a(?=b)", "ab", 0, 1 };
        yield return new object?[] { "(?<=a)b", "aab", 2, 1 };
        yield return new object?[] { "(?<=\\p{L})x", "axb", 1, 1 };
        yield return new object?[] { "(?<=\\p{IsBasicLatin})x", "axb", 1, 1 };
        yield return new object?[] { "a\\G", "aa", 1, 1 };
        yield return new object?[] { "\\Ga*", "aa", 2, 0 };
        yield return new object?[] { "^b", "ba", 0, 1 };
        yield return new object?[] { "b$", "xab", 2, 1 };
    }

    [Theory]
    [MemberData(nameof(FirstMatchCases))]
    public void OwnedRtlMatchMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        Assert.Equal(expectedStart, expected.Index);
        Assert.Equal(expectedLength, expected.Length);
        foreach (var options in new[]
        {
            RegexOptions.RightToLeft | RegexOptions.CultureInvariant,
            RegexOptions.RightToLeft | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            var found = regex.Match(input);
            Assert.True(found.Success);
            Assert.Equal(expectedStart, found.IndexInUtf16);
            Assert.Equal(expectedLength, found.LengthInUtf16);
            Assert.Equal(expectedStart, found.IndexInBytes);
            Assert.Equal(expectedLength, found.LengthInBytes);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, options.ToString() + " pattern=" + pattern);
        }
    }

    [Theory]
    [InlineData("(a)\\1", "aa")]
    [InlineData("(a*)\\1b", "aab")]
    [InlineData("\\Gabc", "abc")]
    public void OwnedRtlMissesMatchOracle(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.False(oracle.Match(inputText).Success);
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.False(regex.Match(input).Success);
    }

    public static IEnumerable<object?[]> DetailedCases()
    {

        yield return new object?[] { "(a)(b)", "ab", 0, 2, new int[] { 0, 1 }, new int[] { 1, 1 } };
        yield return new object?[] { "(?=(a)b)ab", "ab", 0, 2, new int[] { 0 }, new int[] { 1 } };
        yield return new object?[] { "(?<n>a)(?<m>b)", "ab", 0, 2, new int[] { 0, 1 }, new int[] { 1, 1 } };
        yield return new object?[] { "(a+)(b+)", "aab", 0, 3, new int[] { 0, 2 }, new int[] { 2, 1 } };
        yield return new object?[] { "a|ab", "xaab\U0001F600x", 2, 2, new int[] { }, new int[] { } };
        yield return new object?[] { "(a)(b)", "xaby", 1, 2, new int[] { 1, 2 }, new int[] { 1, 1 } };
        yield return new object?[] { "x*", "ax\U0001F600b", 5, 0, new int[] { }, new int[] { } };
        yield return new object?[] { "(x*)", "ax\U0001F600b", 5, 0, new int[] { 5 }, new int[] { 0 } };
        yield return new object?[] { "(a|ab){2,3}", "xaabab", 1, 5, new int[] { 1 }, new int[] { 1 } };
        yield return new object?[] { "(a|ab){2,3}", "xaabab\U0001F600", 1, 5, new int[] { 1 }, new int[] { 1 } };
    }

    [Theory]
    [MemberData(nameof(DetailedCases))]
    public void OwnedRtlDetailedMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength, int[] groupStarts, int[] groupLengths)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var detailed = regex.MatchDetailed(input);
        Assert.True(detailed.Success);
        Assert.Equal(expectedStart, detailed.IndexInUtf16);
        Assert.Equal(expectedLength, detailed.LengthInUtf16);
        Assert.Equal(groupStarts.Length, detailed.GroupCount - 1);
        for (var group = 1; group <= groupStarts.Length; group++)
        {
            var actual = detailed.GetGroup(group);
            var wanted = expected.Groups[group];
            Assert.Equal(wanted.Success, actual.Success);
            Assert.Equal(groupStarts[group - 1], actual.IndexInUtf16);
            Assert.Equal(groupLengths[group - 1], actual.LengthInUtf16);
            Assert.Equal(wanted.Value, actual.GetValueString());
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?=(a)\\1)", "aa", 0, 0, 0, 1)]
    public void OwnedRtlLookaheadCaptureMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength, int groupStart, int groupLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var detailed = regex.MatchDetailed(input);
        Assert.True(detailed.Success);
        Assert.Equal(expectedStart, detailed.IndexInUtf16);
        Assert.Equal(expectedLength, detailed.LengthInUtf16);
        Assert.Equal(groupStart, detailed.GetGroup(1).IndexInUtf16);
        Assert.Equal(groupLength, detailed.GetGroup(1).LengthInUtf16);
    }

    public static IEnumerable<object?[]> EnumerateCases()
    {
        yield return new object?[] { "a*", "aaa", new int[] { 0, 3, 0, 0 } };
        yield return new object?[] { "a*", "b", new int[] { 1, 0, 0, 0 } };
        yield return new object?[] { "x*", "abx", new int[] { 2, 1, 2, 0, 1, 0, 0, 0 } };
        yield return new object?[] { "a|ab", "aab", new int[] { 1, 2, 0, 1 } };
        yield return new object?[] { "a|ab", "xaab\U0001F600x", new int[] { 2, 2, 1, 1 } };
        yield return new object?[] { "a+", "xaay\U0001F600", new int[] { 1, 2 } };
        yield return new object?[] { "x*", "ax\U0001F600b", new int[] { 5, 0, 4, 0, 3, 0, 1, 1, 1, 0, 0, 0 } };
        yield return new object?[] { "\\G", "aa", new int[] { 2, 0 } };
        yield return new object?[] { "a+?", "aaa", new int[] { 2, 1, 1, 1, 0, 1 } };
        yield return new object?[] { "a\\G", "aa", new int[] { 1, 1, 0, 1 } };
        yield return new object?[] { "x*", "\U0001F600", new int[] { 2, 0, 1, 0, 0, 0 } };
        yield return new object?[] { "a*", "a\U0001F600a", new int[] { 3, 1, 3, 0, 2, 0, 0, 1, 0, 0 } };
    }

    [Theory]
    [MemberData(nameof(EnumerateCases))]
    public void OwnedRtlEnumerateMatchesOracle(string pattern, string inputText, int[] expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var wanted = new List<string>();
        foreach (Match match in oracle.Matches(inputText))
        {
            wanted.Add(match.Index + ":" + match.Length);
        }

        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var actual = new List<string>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            actual.Add(match.IndexInUtf16 + ":" + match.LengthInUtf16);
        }

        Assert.Equal(wanted, actual);
        var flat = new List<int>();
        foreach (var item in actual)
        {
            var parts = item.Split(':');
            flat.Add(int.Parse(parts[0]));
            flat.Add(int.Parse(parts[1]));
        }

        Assert.Equal(expected, flat.ToArray());
    }

    public static IEnumerable<object?[]> ReplaceCases()
    {
        yield return new object?[] { "a|ab", "xaabx", "x<a><ab>x" };
        yield return new object?[] { "a+", "aaXaa", "<aa>X<aa>" };
        yield return new object?[] { "x*", "ab", "<>a<>b<>" };
        yield return new object?[] { "\\b", "a b", "<>a<> <>b<>" };
        yield return new object?[] { "(a+)(b+)", "xaaby", "x<aa-b>y" };
        yield return new object?[] { "(a|ab){2,3}", "xaabab", "x<aabab>" };
        yield return new object?[] { "(a|ab){2,3}", "xaabab\U0001F600", "x<aabab>\U0001F600" };
    }

    [Theory]
    [MemberData(nameof(ReplaceCases))]
    public void OwnedRtlReplaceMatchesOracle(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var template = pattern == "(a+)(b+)" ? "<$1-$2>" : "<$&>";
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Replace(inputText, template));
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, Encoding.UTF8.GetString(regex.Replace(input, template)));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> OwnedRtlSplitCases()
    {
        (string pattern, string text, int count)[] rows =
        [
            ("a|ab", "xaabx", 0),
            ("a|ab", "xaabx", 2),
            ("a|ab", "xaabx", 3),
            ("a|ab", "xaab\U0001F600x", 0),
            ("a+", "xaa", 0),
            ("a+", "xaay\U0001F600", 0),
            ("(a+)(b+)", "xaaby", 0),
            ("a*", "ba", 0),
            ("a*", "ba", 2),
            ("a*", "ba", 3),
            ("x*", "ax\U0001F600b", 0),
            ("x*", "\U0001F600", 0),
            ("(a|ab){2,3}", "xaabab", 0),
            ("(a|ab){2,3}", "xaabab\U0001F600", 0),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, row.text, row.count };
            yield return new object?[] { row.pattern, RegexOptions.RightToLeft | RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.count };
        }
    }

    [Theory]
    [MemberData(nameof(OwnedRtlSplitCases))]
    public void OwnedRtlSplitsMatchOracle(string pattern, RegexOptions options, string text, int count)
    {
        // R05: right-to-left slice shapes split owned through the descending
        // cursor with UTF-16 indexes exactly like the BCL oracle (encounter
        // order, tail piece first), including half-scalar empty matches.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], start, end - start));
        }
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }
        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> RtlEmptyReplaceDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("x*", "ax\U0001F600b", "Q"),
            ("x*", "\U0001F600", "Q"),
            ("a*", "a\U0001F600a", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.RightToLeft | RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(RtlEmptyReplaceDeclineCases))]
    public void RtlEmptyReplaceDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: right-to-left empty-capable replacement keeps BCL
        // routing on astral input (the unit machinery serving the left-to-right
        // lanes is direction-locked, and slice programs keep the astral flag for
        // right-to-left shapes); parity holds and construction is pinned per lane.
        // Discovery, enumeration, and splits for these shapes already serve owned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        Assert.Equal(expectedBytes, bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(expected, stringRegex.ReplaceToString(input, template));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> FromOffsetCases()
    {
        yield return new object?[] { "a+", "aabaa", 0, false, -1, 0 };
        yield return new object?[] { "a+", "aabaa", 1, true, 0, 1 };
        yield return new object?[] { "a+", "aabaa", 2, true, 0, 2 };
        yield return new object?[] { "a+", "aabaa", 4, true, 3, 1 };
        yield return new object?[] { "x*", "ax\U0001F600b", 5, true, 5, 0 };
        yield return new object?[] { "a*", "a\U0001F600a", 4, true, 3, 1 };
        yield return new object?[] { "(a|ab){2,3}", "xaabab", 3, true, 1, 2 };
        yield return new object?[] { "(a|ab){2,3}", "xaabab", 6, true, 1, 5 };
        yield return new object?[] { "(a|ab){2,3}", "xaabab\U0001F600", 5, true, 1, 4 };
        yield return new object?[] { "(a|ab){2,3}", "xaabab\U0001F600", 8, true, 1, 5 };
    }

    [Theory]
    [MemberData(nameof(FromOffsetCases))]
    public void OwnedRtlFromOffsetMatchesOracle(string pattern, string inputText, int startAt, bool expectedSuccess, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var wanted = oracle.Match(inputText, startAt);
        Assert.Equal(expectedSuccess, wanted.Success);
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var found = regex.MatchFromUtf16Offset(input, startAt);
        Assert.Equal(expectedSuccess, found.Success);
        Assert.Equal(oracle.IsMatch(inputText, startAt), regex.IsMatchFromUtf16Offset(input, startAt));
        Assert.Equal(oracle.Matches(inputText, startAt).Count, regex.CountFromUtf16Offset(input, startAt));
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, wanted.Index);
            Assert.Equal(expectedLength, wanted.Length);
            Assert.Equal(expectedStart, found.IndexInUtf16);
            Assert.Equal(expectedLength, found.LengthInUtf16);
            var detailed = regex.MatchDetailedFromUtf16Offset(input, startAt);
            Assert.True(detailed.Success);
            Assert.Equal(expectedStart, detailed.IndexInUtf16);
            Assert.Equal(expectedLength, detailed.LengthInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> RtlFromOffsetEnumerateCases()
    {
        yield return new object?[] { "a+", "xaayaaa", 7, new int[] { 4, 3, 1, 2 } };
        yield return new object?[] { "a+", "xaayaaa", 4, new int[] { 1, 2 } };
        yield return new object?[] { "a+", "aabaa", 2, new int[] { 0, 2 } };
        yield return new object?[] { "a+", "xaay\U0001F600aa", 0, new int[] { } };
        yield return new object?[] { "a|ab", "xaab\U0001F600x", 0, new int[] { } };
        yield return new object?[] { "a*", "baa", 3, new int[] { 1, 2, 1, 0, 0, 0 } };
        yield return new object?[] { "a*", "baa", 1, new int[] { 1, 0, 0, 0 } };
        yield return new object?[] { "a+", "xaay\U0001F600aa", 5, new int[] { 1, 2 } };
        yield return new object?[] { "x*", "ax\U0001F600b", 5, new int[] { 5, 0, 4, 0, 3, 0, 1, 1, 1, 0, 0, 0 } };
        yield return new object?[] { "a*", "a\U0001F600a", 4, new int[] { 3, 1, 3, 0, 2, 0, 0, 1, 0, 0 } };
        yield return new object?[] { "(a|ab){2,3}", "xaabab", 3, new int[] { 1, 2 } };
        yield return new object?[] { "(a|ab){2,3}", "xaabab", 6, new int[] { 1, 5 } };
        yield return new object?[] { "(a|ab){2,3}", "xaabab\U0001F600", 5, new int[] { 1, 4 } };
        yield return new object?[] { "(a|ab){2,3}", "xaabab\U0001F600", 8, new int[] { 1, 5 } };
    }

    [Theory]
    [MemberData(nameof(RtlFromOffsetEnumerateCases))]
    public void OwnedRtlFromOffsetEnumerateMatchesOracle(string pattern, string inputText, int startAt, int[] expectedFlat)
    {
        // R05: right-to-left FromOffset enumeration descends closing edges from
        // the caller bound (matches ending at or below it, rightmost first),
        // exactly like the BCL oracle.
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        var wanted = new List<string>();
        foreach (Match match in oracle.Matches(inputText, startAt))
        {
            wanted.Add(match.Index + ":" + match.Length);
        }
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var actual = new List<string>();
        foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, startAt))
        {
            actual.Add(match.IndexInUtf16 + ":" + match.LengthInUtf16);
        }
        Assert.Equal(wanted, actual);
        var flat = new List<int>();
        foreach (var item in actual)
        {
            var parts = item.Split(':');
            flat.Add(int.Parse(parts[0]));
            flat.Add(int.Parse(parts[1]));
        }
        Assert.Equal(expectedFlat, flat.ToArray());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedRtlCatastrophicInputTimesOut()
    {
        const string pattern = "b(a+)+";
        var inputText = new string('a', 100);
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        Assert.Throws<RegexMatchTimeoutException>(() => oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500));
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
