using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// R03 inline options: scoped (?i/m/s/n:) groups bake flags into child nodes
// through a transparent wrapper while toggles (?i) mutate the enclosing scope
// (restored at each closing paren, never across alternatives); literals,
// classes, shorthand hybrids, and backreferences fold per node, dots and
// anchors already carry per-node flags. The authoritative tree also preserves
// regional (?x) lexing. Option constructs directly inside
// expression-conditional branches. Bare shapes keep native precedence;
// quantified shapes route owned.
public sealed class Utf8SliceInlineTests
{
    public static IEnumerable<object?[]> SliceInlineShapes()
    {
        yield return new object?[] { "(?i:ab)+" };
        yield return new object?[] { "(?i)a+" };
        yield return new object?[] { "(?m:^a)+" };
        yield return new object?[] { "(?s:a.c)+" };
        yield return new object?[] { "(a)(?i:b)+" };
        yield return new object?[] { "(?i:(a)(b))+" };
        yield return new object?[] { "a(?i:b|c)+" };
        yield return new object?[] { "(?i)a+b+" };
        yield return new object?[] { "((?i)a)+" };
        yield return new object?[] { "(?im:ab)+" };
        yield return new object?[] { "(?i:(?m:^a)b)+" };
        yield return new object?[] { "(?-i:ab)+" };
        yield return new object?[] { "(a)?(?(1)(?i)b|c)" };
        yield return new object?[] { "(?i:k)+" };
        yield return new object?[] { "(?m:a$)+" };
    }

    [Theory]
    [MemberData(nameof(SliceInlineShapes))]
    public void InlineShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> NativeShapes()
    {
        yield return new object?[] { "(?i:ab)", "xxABxx", true };
        yield return new object?[] { "(?i)ab", "xxABxx", true };
        yield return new object?[] { "a(?i:b)c", "xxaBcxx", true };
        yield return new object?[] { "(?n:(a)(b))", "xxabxx", true };
    }

    [Theory]
    [MemberData(nameof(NativeShapes))]
    public void InlineBareShapesKeepNativePrecedence(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
    }

    public static IEnumerable<object?[]> WhitespaceShapes()
    {
        yield return new object?[] { "(?x: a)+", "aa", RegexOptions.None };
        yield return new object?[] { "(?x)a+", "aa", RegexOptions.None };
    }

    [Theory]
    [MemberData(nameof(WhitespaceShapes))]
    public void InlineWhitespaceShapesUseOwnedTree(string pattern, string inputText, RegexOptions extra)
    {
        var options = RegexOptions.CultureInvariant | extra;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData("(?-x: a)+", " a a", RegexOptions.IgnorePatternWhitespace)]
    public void InlineWhitespaceOffUsesOwnedTreeUnderGlobalWhitespace(string pattern, string inputText, RegexOptions extra)
    {
        var options = RegexOptions.CultureInvariant | extra;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static IEnumerable<object?[]> ParserDeclines()
    {
        yield return new object?[] { "(?z:ab)" };
        yield return new object?[] { "(?i:ab" };
        yield return new object?[] { "(?iab)" };
        yield return new object?[] { "(?#c)*" };
        yield return new object?[] { "(?i)+a" };
        yield return new object?[] { "(?n:(a)(b))\\1" };
        yield return new object?[] { "(?(?=a)(?i)b|c)" };
    }

    [Theory]
    [MemberData(nameof(ParserDeclines))]
    public void InlineInvalidShapesDeclineAtParse(string pattern)
    {
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    [Theory]
    [MemberData(nameof(ParserDeclines))]
    public void InlineInvalidShapesFailOwnedConstruction(string pattern)
    {
        // Owned grammar admission stays stable when the runtime adds syntax.
        Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> ParserShapes()
    {
        yield return new object?[] { "(?i:ab)" };
        yield return new object?[] { "(?i)ab" };
        yield return new object?[] { "(?-i:ab)" };
        yield return new object?[] { "(?+i:ab)" };
        yield return new object?[] { "(?ii:ab)" };
        yield return new object?[] { "(?i-i:ab)" };
        yield return new object?[] { "(?-:ab)" };
        yield return new object?[] { "(?m:^a)" };
        yield return new object?[] { "(?s:a.c)" };
        yield return new object?[] { "(?n:(a)(b))" };
        yield return new object?[] { "(?-x: a)" };
        yield return new object?[] { "(?-x)" };
        yield return new object?[] { "((?i))" };
        yield return new object?[] { "(a)?(?(1)(?i)b|c)" };
    }

    [Theory]
    [MemberData(nameof(ParserShapes))]
    public void InlineOrdinaryShapesParse(string pattern)
    {
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(?i:ab)+", "AAB", true };
        yield return new object?[] { "(?i:ab)+", "aab", true };
        yield return new object?[] { "(?i)a+", "AAA", true };
        yield return new object?[] { "a(?i:b|c)+d", "aBd", true };
        yield return new object?[] { "a(?i:b|c)+d", "aCd", true };
        yield return new object?[] { "a(?i:b|c)+d", "abd", true };
        yield return new object?[] { "(?i:a(?-i:b)c)+", "ABC", false };
        yield return new object?[] { "(?i:a(?-i:b)c)+", "AbC", true };
        yield return new object?[] { "((?i)a)+", "A", true };
        yield return new object?[] { "(?i:(a))+\\1", "AA", true };
        yield return new object?[] { "(?m:^a)+", "x\na", true };
        yield return new object?[] { "(?m:^a)+", "xa", false };
        yield return new object?[] { "(?s:a.c)+", "a\nc", true };
        yield return new object?[] { "(?s:a.c)+", "abc", true };
        yield return new object?[] { "(?im:ab)+", "AB", true };
        yield return new object?[] { "(?i-m:^a)+", "A", true };
        yield return new object?[] { "(?i:(?m:^a)b)+", "\nAB", true };
        yield return new object?[] { "(?s:(?i:a.c))+", "A\nC", true };
        yield return new object?[] { "(?i)a+b+", "AABB", true };
        yield return new object?[] { "(?-i:ab)+", "AB", false };
        yield return new object?[] { "(?-i:ab)+", "ab", true };
        yield return new object?[] { "(?i:(a))(b)\\2", "ABB", false };
        yield return new object?[] { "(?i:(a))(b)\\2", "Abb", true };
        yield return new object?[] { "(?i:(a))(b)\\2", "AB", false };
        yield return new object?[] { "(a)(?i:b)\\1", "aBa", true };
        yield return new object?[] { "(a)(?i:b)\\1", "aBA", false };
        yield return new object?[] { "(?i:k)+", "k\u212ak", true };
        yield return new object?[] { "(?m:a$)+", "a\nb", true };
        yield return new object?[] { "(?:(?i)a|b)+", "B", true };
        yield return new object?[] { "(?:(?i)a|b)+", "b", true };
        yield return new object?[] { "(a)?(?(1)(?i)b|c)", "ab", true };
        yield return new object?[] { "(a)?(?(1)(?i)b|c)", "aB", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedInlineIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, options.ToString() + " pattern=" + pattern);
        }
    }

    [Theory]
    [InlineData("(?i)ab", "AB", true)]
    public void NativeInlineGlobalInterplayMatchesOracle(string pattern, string inputText, bool expected)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
    }

    [Theory]
    [InlineData("(?-i:ab)+", "AB", false)]
    [InlineData("(?-i:ab)+", "ab", true)]
    [InlineData("(?-i:ab)", "AB", false)]
    [InlineData("(?-i:ab)", "ab", true)]
    public void OwnedInlineGlobalInterplayMatchesOracle(string pattern, string inputText, bool expected)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void InlineCaseFoldingIgnoresCurrentCultureLikeRuntime()
    {
        var saved = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            foreach (var (pattern, inputText) in new[] { ("(?i:I)+", "i"), ("(?i:i)+", "I") })
            {
                var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
                Assert.True(oracle.IsMatch(inputText));
                var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
                Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
                Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(inputText)));
            }
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = saved;
        }
    }
}
public sealed class Utf8SliceInlineOperationTests
{
    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "(?i:ab)+", "ABab", 1 };
        yield return new object?[] { "(?i)a+", "AaBb", 1 };
        yield return new object?[] { "(?m:^a)+", "x\na\na", 2 };
        yield return new object?[] { "(a)(?i:b)+", "aBB", 1 };
        yield return new object?[] { "(?i:(a)(b))+", "ABab", 1 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedInlineCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        foreach (var options in new[]
        {
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.Equal(expected, regex.Count(input));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> FirstMatchCases()
    {
        yield return new object?[] { "(?i:ab)+", "xxABxx", 2, 2 };
        yield return new object?[] { "(?m:^a)+", "x\na", 2, 1 };
        yield return new object?[] { "(a)(?i:b)+", "xaBB", 1, 3 };
        yield return new object?[] { "(?s:a.c)+", "xa\ncy", 1, 3 };
    }

    [Theory]
    [MemberData(nameof(FirstMatchCases))]
    public void OwnedInlineMatchMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        Assert.Equal(expectedStart, expected.Index);
        Assert.Equal(expectedLength, expected.Length);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var found = regex.Match(input);
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.IndexInUtf16);
        Assert.Equal(expectedLength, found.LengthInUtf16);
        Assert.Equal(expectedStart, found.IndexInBytes);
        Assert.Equal(expectedLength, found.LengthInBytes);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> DetailedCases()
    {
        yield return new object?[] { "(?i:(a)(b))+", "AB", 0, 2, new int[] { 0, 1 }, new int[] { 1, 1 } };
        yield return new object?[] { "(a)(?i:b)+", "aBB", 0, 3, new int[] { 0 }, new int[] { 1 } };
        yield return new object?[] { "(?n:(a)(?<x>b))+", "ab", 0, 2, new int[] { 1 }, new int[] { 1 } };
    }

    [Theory]
    [MemberData(nameof(DetailedCases))]
    public void OwnedInlineDetailedMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength, int[] groupStarts, int[] groupLengths)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        Assert.Equal(expected.Groups.Count - 1, groupStarts.Length);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
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

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void InlineExplicitCaptureGroupsShapeLikeRuntime()
    {
        const string pattern = "(?n:(a)(b))";
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(1, oracle.GetGroupNumbers().Length);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(1, regex.GetGroupNumbers().Length);
        var foundInline = regex.Match(Encoding.UTF8.GetBytes("ab"));
        Assert.True(foundInline.Success);
        Assert.Equal(0, foundInline.IndexInUtf16);
        Assert.Equal(2, foundInline.LengthInUtf16);
    }

    public static IEnumerable<object?[]> EnumerateCases()
    {
        yield return new object?[] { "(?i)a+", "aAa", new int[] { 0, 3 } };
        yield return new object?[] { "(?m:^a)+", "b\na\na", new int[] { 2, 1, 4, 1 } };
    }

    [Theory]
    [MemberData(nameof(EnumerateCases))]
    public void OwnedInlineEnumerateMatchesOracle(string pattern, string inputText, int[] expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var wanted = new List<string>();
        foreach (Match match in oracle.Matches(inputText))
        {
            wanted.Add(match.Index + ":" + match.Length);
        }

        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
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
        yield return new object?[] { "(?i:ab)+", "xABx", "x<AB>x" };
        yield return new object?[] { "(?m:^a)+", "x\na", "x\n<a>" };
    }

    [Theory]
    [MemberData(nameof(ReplaceCases))]
    public void OwnedInlineReplaceMatchesOracle(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Replace(inputText, "<$&>"));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, Encoding.UTF8.GetString(regex.Replace(input, "<$&>")));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> SplitCases()
    {
        yield return new object?[] { "(?i:ab)+", "xABx", 0, new string[] { "x", "x" } };
    }

    [Theory]
    [MemberData(nameof(SplitCases))]
    public void OwnedInlineSplitMatchesOracle(string pattern, string inputText, int count, string[] expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Split(inputText, count));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var actual = new List<string>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add(split.GetValueString());
        }

        Assert.Equal(expected, actual.ToArray());
    }

    [Theory]
    [InlineData("(?i:ab)+", "xxABxx", 2, true, 2, 2)]
    [InlineData("(?m:^a)+", "x\na", 2, true, 2, 1)]
    public void OwnedInlineFromOffsetMatchesOracle(string pattern, string inputText, int startAt, bool expectedSuccess, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var wanted = oracle.Match(inputText, startAt);
        Assert.Equal(expectedSuccess, wanted.Success);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
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
        }

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }
}
