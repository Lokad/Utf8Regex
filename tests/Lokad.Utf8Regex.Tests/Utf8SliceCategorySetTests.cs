using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// R03 mixed category classes: classes combining \p{...}/\P{...} members
// with literals, ranges, and shorthand members lower to owned CategorySet
// programs (BCL uniform rule: a category is always a member, never a bound;
// a following dash stays literal; categories in upper-bound position throw).
// Single-category classes keep folding to bare atoms; negated classes
// complement the whole union. Block members match under IgnoreCase through
// invariant case equivalents (candidate-side lookup, no parse expansion);
// general members fold Ll/Lu/Lt per member exactly like atoms. Class-negated
// block atoms ride the general class path so both negations survive. All
// members classify complete scalars; BMP overlap retains BCL comparisons.
public sealed class Utf8SliceCategorySetTests
{
    public static IEnumerable<object?[]> SliceCategorySetShapes()
    {
        yield return new object?[] { "[\\p{L}a]+" };
        yield return new object?[] { "[\\p{L}\\p{Nd}_]+" };
        yield return new object?[] { "[^\\p{L}a]+" };
        yield return new object?[] { "[\\P{L}-x]+" };
        yield return new object?[] { "[\\p{L}-x]+" };
        yield return new object?[] { "[\\p{L}\\D]+" };
        yield return new object?[] { "[^\\p{L}\\d]+" };
        yield return new object?[] { "[\\p{L}a-z]+" };
        yield return new object?[] { "[\\p{L}-a]+" };
        yield return new object?[] { "[\\p{IsBasicLatin}-a]+" };
        yield return new object?[] { "[\\d-\\p{L}]+" };
        yield return new object?[] { "[]\\p{L}]+" };
        yield return new object?[] { "([\\p{L}\\p{Nd}_]+)" };
        yield return new object?[] { "[\\p{L}-\\d]+" };
    }

    [Theory]
    [MemberData(nameof(SliceCategorySetShapes))]
    public void CategorySetShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> SliceBlockSetShapes()
    {
        yield return new object?[] { "[\\p{IsGreek}a]+" };
        yield return new object?[] { "[^\\p{IsGreek}a]+" };
    }

    [Theory]
    [MemberData(nameof(SliceBlockSetShapes))]
    public void BlockSetShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, options.ToString() + " pattern=" + pattern);
        }
    }


    public static IEnumerable<object?[]> ParserDeclines()
    {
        // Range-bound categories decline the slice (the vendored front end
        // throws ShorthandClassInCharacterRange, so construction fails
        // BCL-exactly; see RangeBoundEscapesFailOwnedConstruction).
        yield return new object?[] { "[a-\\p{L}]" };
        yield return new object?[] { "[a-\\P{L}]" };
    }

    [Theory]
    [MemberData(nameof(ParserDeclines))]
    public void RangeBoundCategoriesDeclineAtParse(string pattern)
    {
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }



    public static IEnumerable<object?[]> ParserShapes()
    {
        yield return new object?[] { "[\\p{L}a]+", 0 };
        yield return new object?[] { "([\\p{L}\\p{Nd}_]+)", 1 };
        yield return new object?[] { "[\\p{L}-\\d]+", 0 };
        yield return new object?[] { "[^\\p{IsGreek}a]+", 0 };
    }

    public static IEnumerable<object?[]> RangeBoundConstructionThrows()
    {
        // A shorthand or category escape in upper-bound position throws
        // BCL-exactly at construction (the vendored gatekeeper records
        // ShorthandClassInCharacterRange, closing the leniency debt).
        yield return new object?[] { "[a-\\p{L}]" };
        yield return new object?[] { "[a-\\P{L}]" };
        yield return new object?[] { "[a-\\p{IsBasicLatin}]" };
        yield return new object?[] { "[a-\\d]" };
        yield return new object?[] { "[a-\\w]" };
        yield return new object?[] { "[a-\\s]" };
    }

    [Theory]
    [MemberData(nameof(RangeBoundConstructionThrows))]
    public void RangeBoundEscapesFailOwnedConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    [Theory]
    [MemberData(nameof(ParserShapes))]
    public void CategorySetShapesParse(string pattern, int expectedGroups)
    {
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        Assert.Equal(expectedGroups, program.GroupCount);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.GetGroupNumbers().Length - 1, program.GroupCount);
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "[\\p{L}a]+", "1a\u03B1", true };
        yield return new object?[] { "[\\p{L}a]+", "129", false };
        yield return new object?[] { "[\\p{L}\\p{Nd}_]+", "a1_-", true };
        yield return new object?[] { "[^\\p{L}a]+", "a1 b", true };
        yield return new object?[] { "[^\\p{L}a]+", "ab\u03B1", false };
        yield return new object?[] { "[\\P{L}-x]+", "a-x1", true };
        yield return new object?[] { "[\\P{L}-x]+", "abc", false };
        yield return new object?[] { "[\\p{L}-x]+", "a-x1", true };
        yield return new object?[] { "[\\p{L}\\D]+", "a15 ", true };
        yield return new object?[] { "[^\\p{L}\\d]+", "a1", false };
        yield return new object?[] { "[\\p{IsGreek}a]+", "a\u03B1b1", true };
        yield return new object?[] { "[^\\p{IsGreek}a]+", "ab\u03B1", true };
        yield return new object?[] { "[\\p{L}a-z]+", "aM1", true };
        yield return new object?[] { "([\\p{L}a]+?)x", "aabx", true };
        yield return new object?[] { "[\\p{L}-a]+", "a", true };
        yield return new object?[] { "[\\p{IsBasicLatin}-a]+", "a", true };
        yield return new object?[] { "[\\d-\\p{L}]+", "5", true };
        yield return new object?[] { "[\\d-\\p{L}]+", "-", true };
        yield return new object?[] { "[\\d-\\p{L}]+", "a", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> IgnoreCaseIsMatchCases()
    {
        yield return new object?[] { "[\\p{Ll}a]+", "aAbB1", true };
        yield return new object?[] { "[\\P{Ll}a]+", "AB", true };
        yield return new object?[] { "[\\P{Ll}a]+", "aAbB1", true };
    }

    [Theory]
    [MemberData(nameof(IgnoreCaseIsMatchCases))]
    public void OwnedIgnoreCaseIsMatchMatchesOracle(string pattern, string inputText, bool expected)
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

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "[\\p{L}a]+", "1a\u03B1", 1 };
        yield return new object?[] { "[\\p{L}\\p{Nd}_]+", "a1_-", 1 };
        yield return new object?[] { "[^\\p{L}a]+", "a1 b", 1 };
        yield return new object?[] { "[\\P{L}-x]+", "a-x1", 1 };
        yield return new object?[] { "[\\p{L}-x]+", "a-x1", 1 };
        yield return new object?[] { "[\\p{L}\\D]+", "a15 ", 2 };
        yield return new object?[] { "[^\\p{L}\\d]+", "a1 ", 1 };
        yield return new object?[] { "[\\p{IsGreek}a]+", "a\u03B1b1", 1 };
        yield return new object?[] { "[^\\p{IsGreek}a]+", "a\u03B1b1", 1 };
        yield return new object?[] { "[\\d-\\p{L}]+", "5-a", 1 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> FirstMatchCases()
    {
        yield return new object?[] { "[\\p{L}a]+", "1a\u03B1", 1, 2 };
        yield return new object?[] { "[^\\p{IsGreek}a]+", "a\u03B1b1", 2, 2 };
        yield return new object?[] { "([\\p{L}\\p{Nd}_]+)", "xa1", 0, 3 };
    }

    [Theory]
    [MemberData(nameof(FirstMatchCases))]
    public void OwnedMatchMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength)
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
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> DetailedCases()
    {
        yield return new object?[] { "([\\p{L}a]+)", "1a\u03B1", 1, 2, new int[] { 1 }, new int[] { 2 } };
        yield return new object?[] { "([\\P{L}-x]+)", "a-x1", 1, 3, new int[] { 1 }, new int[] { 3 } };
    }

    [Theory]
    [MemberData(nameof(DetailedCases))]
    public void OwnedDetailedMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength, int[] groupStarts, int[] groupLengths)
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

    public static IEnumerable<object?[]> EnumerateCases()
    {
        yield return new object?[] { "[\\p{L}\\D]+", "a15 ", new int[] { 0, 1, 3, 1 } };
        yield return new object?[] { "[^\\p{L}a]+", "a1 b", new int[] { 1, 2 } };
    }

    [Theory]
    [MemberData(nameof(EnumerateCases))]
    public void OwnedEnumerateMatchesOracle(string pattern, string inputText, int[] expected)
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
            var parts = item.Split((char)58);
            flat.Add(int.Parse(parts[0]));
            flat.Add(int.Parse(parts[1]));
        }

        Assert.Equal(expected, flat.ToArray());
    }

    public static IEnumerable<object?[]> ReplaceCases()
    {
        yield return new object?[] { "[\\p{L}a]+", "1a\u03B1", "1<a\u03B1>" };
        yield return new object?[] { "[^\\p{IsGreek}a]+", "a\u03B1b1", "a\u03B1<b1>" };
    }

    [Theory]
    [MemberData(nameof(ReplaceCases))]
    public void OwnedReplaceMatchesOracle(string pattern, string inputText, string expected)
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
        yield return new object?[] { "[\\p{L},]+", "ab,cd", 0, new string[] { "", "" } };
    }

    [Theory]
    [MemberData(nameof(SplitCases))]
    public void OwnedSplitMatchesOracle(string pattern, string inputText, int count, string[] expected)
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
    [InlineData("[\\p{L}a]+", "1a\u03B1", 1, true, 1, 2)]
    [InlineData("[\\P{L}-x]+", "a-x1", 1, true, 1, 3)]
    public void OwnedFromOffsetMatchesOracle(string pattern, string inputText, int startAt, bool expectedSuccess, int expectedStart, int expectedLength)
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

    public static IEnumerable<object?[]> RtlCases()
    {
        yield return new object?[] { "[\\p{L}a]+", "1a\u03B1", true, 1, 2 };
        yield return new object?[] { "[\\P{L}a]+", "a1 ", true, 0, 3 };
    }

    [Theory]
    [MemberData(nameof(RtlCases))]
    public void OwnedRightToLeftMatchesOracle(string pattern, string inputText, bool expectedSuccess, int expectedStart, int expectedLength)
    {
        var options = RegexOptions.RightToLeft | RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var wanted = oracle.Match(inputText);
        Assert.Equal(expectedSuccess, wanted.Success);
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var found = regex.Match(input);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, wanted.Index);
            Assert.Equal(expectedLength, wanted.Length);
            Assert.Equal(expectedStart, found.IndexInUtf16);
            Assert.Equal(expectedLength, found.LengthInUtf16);
        }

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> SupplementaryCases()
    {
        yield return new object?[] { "[\\p{L}a]+", "a\U00010400b", new[] { 0, 6 } };
        yield return new object?[] { "[^\\p{L}a]+", "a\U00010400b", Array.Empty<int>() };
        yield return new object?[] { "[\\P{L}a]+", "a\U00010400b", new[] { 0, 1 } };
        yield return new object?[] { "[\\p{IsGreek}a]+", "\u03B1\U00010400", new[] { 0, 2 } };
    }

    [Theory]
    [MemberData(nameof(SupplementaryCases))]
    public void SupplementaryClassesUseScalarMembership(string pattern, string inputText, int[] ranges)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft, RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
            new Utf8ScalarSubjectTests().PublicOperationsUseCompleteScalars(pattern, inputText, ranges, options);
    }
}
