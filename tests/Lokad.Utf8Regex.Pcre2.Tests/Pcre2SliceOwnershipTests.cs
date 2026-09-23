using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R02a slice ownership for the PCRE2 profile: slice-shaped patterns execute
// IsMatch, Count, and first-match Match on the shared primary-library slice
// runner through Pcre2SliceDirectProgram, with PCRE2-owned global
// progression, budgets, timeout, and failure vocabulary. Detailed results,
// enumeration, and replacement stay on the backtracking fallback until their
// owned projections land.
public sealed class Pcre2SliceOwnershipTests
{
    public static IEnumerable<object?[]> SliceShapes()
    {
        yield return new object?[] { "(a+)(b+)" };
        yield return new object?[] { "(ab|cd)+" };
        yield return new object?[] { "(a|ab)+c" };
        yield return new object?[] { "(ab|a)+z" };
        yield return new object?[] { "(a{2,3}b+)|(c+d)" };
        yield return new object?[] { "a*?b" };
        yield return new object?[] { "x*|y" };
        yield return new object?[] { "a+b" };
        yield return new object?[] { "a+b?" };
        yield return new object?[] { "foo|" };
    }

    [Theory]
    [MemberData(nameof(SliceShapes))]
    public void SliceShapesRouteIsMatchCountAndMatchThroughSlice(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, " +
            "Count=Pcre2Slice, " +
            "Enumerate=Pcre2Slice, " +
            "Match=Pcre2Slice, " +
            "Replace=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void LazyBoundedRepeatMatchRoutesThroughSlice()
    {
        var regex = new Utf8Pcre2Regex("a{2,4}?a", Pcre2CompileOptions.None);
        Assert.Contains(
            "Match=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
    }

    public static IEnumerable<object?[]> DeclinedOptions()
    {
        yield return new object?[] { Pcre2CompileOptions.Caseless };
        yield return new object?[] { Pcre2CompileOptions.Anchored };
        yield return new object?[] { Pcre2CompileOptions.EndAnchored };
        yield return new object?[] { Pcre2CompileOptions.Ungreedy };
        yield return new object?[] { Pcre2CompileOptions.NoAutoCapture };
        yield return new object?[] { Pcre2CompileOptions.FirstLine };
    }

    [Theory]
    [MemberData(nameof(DeclinedOptions))]
    public void IncompatibleOptionsDeclineSliceRouting(Pcre2CompileOptions options)
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData("[a-z]+")]
    [InlineData("(a+)+$")]
    [InlineData("(?P<word>foo)-(?P=word)")]
    public void NonSliceShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    private static Regex Bcl(string pattern)
    {
        return new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);
    }

    private static byte[] Utf8(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }

    public static IEnumerable<object?[]> SearchCases()
    {
        yield return new object?[] { "(a+)(b+)", "xxaaabbyy", 0, true };
        yield return new object?[] { "(a+)(b+)", "xxaaabbyy", 2, true };
        yield return new object?[] { "(a+)(b+)", "xxaaayy", 0, false };
        yield return new object?[] { "(ab|cd)+", "xxabcdcdyy", 4, true };
        yield return new object?[] { "(ab|a)+z", "ababaz", 2, true };
        yield return new object?[] { "a*?b", "xxaaabyy", 3, true };
        yield return new object?[] { "(a+)(b+)", "zz", 0, false };
        yield return new object?[] { "(a+)(b+)", "zz", 2, false };
        yield return new object?[] { "a+b", "aaab", 0, true };
        yield return new object?[] { "a+b", "aaac", 0, false };
        yield return new object?[] { "a+b?", "aab", 0, true };
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public void OwnedIsMatchMatchesOracle(
        string pattern,
        string inputText,
        int startOffsetInBytes,
        bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText, startOffsetInBytes));
        Assert.Equal(expected, regex.IsMatch(input, startOffsetInBytes));
        Assert.False(
            regex.DebugFoundationCoreFallbackRegexIsCreated,
            "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "(a+)(b+)", "aab aabb", 0, 2 };
        yield return new object?[] { "(a+)(b+)", "aab aabb", 4, 1 };
        yield return new object?[] { "(ab|cd)+", "ab cd abcd", 0, 3 };
        yield return new object?[] { "(a|ab)+c", "xxabcabccyy", 0, 2 };
        yield return new object?[] { "(a+)(b+)", "xyz", 0, 0 };
        yield return new object?[] { "a+b", "aab aabb", 0, 2 };
        yield return new object?[] { "a+b?", "aab", 0, 1 };
        yield return new object?[] { "foo|", "xfoo", 0, 3 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedCountMatchesOracle(
        string pattern,
        string inputText,
        int startOffsetInBytes,
        int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        Assert.Equal(expected, Bcl(pattern).Matches(inputText, startOffsetInBytes).Count);
        Assert.Equal(expected, regex.Count(input, startOffsetInBytes));
        Assert.False(
            regex.DebugFoundationCoreFallbackRegexIsCreated,
            "pattern=" + pattern);
    }

    [Theory]
    [InlineData("x*|y", "y", 0, 3)]
    [InlineData("x*|y", "yy", 0, 5)]
    public void OwnedEmptyCapableCountFollowsPcre2EmptyRule(
        string pattern,
        string inputText,
        int startOffsetInBytes,
        int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.False(
            regex.DebugFoundationCoreFallbackRegexIsCreated,
            "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> FirstMatchCases()
    {
        yield return new object?[] { "(a+)(b+)", "xxaaabbyy", 0, 2, 7 };
        yield return new object?[] { "(a+)(b+)", "xxaaabbyy", 3, 3, 7 };
        yield return new object?[] { "(a+)(b+)", "xxaaayy", 0, -1, -1 };
        yield return new object?[] { "(ab|cd)+", "xxabcdcdyy", 0, 2, 8 };
        yield return new object?[] { "a{2,4}?a", "aaaaa", 0, 0, 3 };
        yield return new object?[] { "a+b", "aaab", 0, 0, 4 };
    }

    [Theory]
    [MemberData(nameof(FirstMatchCases))]
    public void OwnedFirstMatchMatchesOracle(
        string pattern,
        string inputText,
        int startOffsetInBytes,
        int expectedStart,
        int expectedEnd)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Match(inputText, startOffsetInBytes);
        Assert.Equal(expectedStart >= 0, oracle.Success);
        var found = regex.Match(input, startOffsetInBytes);
        Assert.Equal(oracle.Success, found.Success);
        if (oracle.Success)
        {
            Assert.Equal(oracle.Index, expectedStart);
            Assert.Equal(oracle.Index + oracle.Length, expectedEnd);
            Assert.Equal(expectedStart, found.StartOffsetInBytes);
            Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        }

        Assert.False(
            regex.DebugFoundationCoreFallbackRegexIsCreated,
            "pattern=" + pattern);
    }

    [Fact]
    public void OwnedOperationsMatchOracleOnAstralInput()
    {
        const string pattern = "(a+)(b+)";
        const string inputText = "\U0001F600aaabb\U0001F600";
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Match(inputText);
        Assert.True(oracle.Success);
        var expectedStart = Encoding.UTF8.GetByteCount(inputText[..oracle.Index]);
        var expectedEnd = expectedStart + Encoding.UTF8.GetByteCount(oracle.Value);
        Assert.True(regex.IsMatch(input, 0));
        Assert.Equal(1, regex.Count(input, 0));
        var found = regex.Match(input, 0);
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.StartOffsetInBytes);
        Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> DetailedCases()
    {
        yield return new object?[]
            { "(a+)(b+)", "xxaaabbyy", 0, true, "aaabb", "aaa", "bb" };
        yield return new object?[]
            { "(a+)(b+)", "xxaaabbyy", 3, true, "aabb", "aa", "bb" };
        yield return new object?[]
            { "(a+)(b+)", "xxaaayy", 0, false, "", "", "" };
        yield return new object?[]
            { "(ab|cd)+", "xxabcdcdyy", 0, true, "abcdcd", "cd", "" };
        yield return new object?[]
            { "x*|y", "y", 0, true, "", "", "" };
    }

    [Theory]
    [MemberData(nameof(DetailedCases))]
    public void OwnedDetailedProjectsGroups(
        string pattern,
        string inputText,
        int startOffsetInBytes,
        bool expectedSuccess,
        string expectedWhole,
        string expectedFirst,
        string expectedSecond)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Match(inputText, startOffsetInBytes);
        Assert.Equal(expectedSuccess, oracle.Success);
        var detailed = regex.MatchDetailed(input, startOffsetInBytes);
        Assert.Equal(oracle.Success, detailed.Success);
        if (oracle.Success)
        {
            Assert.Equal(oracle.Groups[0].Value, expectedWhole);
            Assert.Equal(expectedWhole, detailed.Value.GetValueString());
            if (oracle.Groups.Count > 1)
            {
                Assert.Equal(
                    oracle.Groups[1].Success,
                    detailed.GetGroup(1).Success);
                Assert.Equal(expectedFirst, detailed.GetGroup(1).GetValueString());
            }
            if (oracle.Groups.Count > 2)
            {
                Assert.Equal(
                    oracle.Groups[2].Success,
                    detailed.GetGroup(2).Success);
                Assert.Equal(expectedSecond, detailed.GetGroup(2).GetValueString());
            }
        }

        Assert.False(
            regex.DebugFoundationCoreFallbackRegexIsCreated,
            "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(cat)|(horse)", "cat horse", 0, 1, "cat")]
    [InlineData("(cat)|(horse)", "cat horse", 4, 2, "horse")]
    public void OwnedDetailedReportsUnmatchedGroups(
        string pattern,
        string inputText,
        int startOffsetInBytes,
        int winningGroup,
        string winningValue)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Match(inputText, startOffsetInBytes);
        var detailed = regex.MatchDetailed(input, startOffsetInBytes);
        Assert.True(oracle.Success);
        Assert.True(detailed.Success);
        Assert.True(detailed.GetGroup(winningGroup).Success);
        Assert.Equal(winningValue, detailed.GetGroup(winningGroup).GetValueString());
        Assert.Equal(
            oracle.Groups[3 - winningGroup].Success,
            detailed.GetGroup(3 - winningGroup).Success);
        Assert.False(detailed.GetGroup(3 - winningGroup).Success);
        Assert.False(
            regex.DebugFoundationCoreFallbackRegexIsCreated,
            "pattern=" + pattern);
    }

    [Fact]
    public void OwnedDetailedMatchesOracleOnAstralInput()
    {
        const string pattern = "(a+)(b+)";
        const string inputText = "\U0001F600aaabb\U0001F600";
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Match(inputText);
        var detailed = regex.MatchDetailed(input, 0);
        Assert.True(oracle.Success);
        Assert.True(detailed.Success);
        Assert.Equal(oracle.Groups[0].Value, detailed.Value.GetValueString());
        Assert.Equal(oracle.Groups[1].Value, detailed.GetGroup(1).GetValueString());
        Assert.Equal(oracle.Groups[2].Value, detailed.GetGroup(2).GetValueString());
        var expectedStart = Encoding.UTF8.GetByteCount(inputText[..oracle.Index]);
        Assert.Equal(expectedStart, detailed.Value.StartOffsetInBytes);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void AnchoredDetailedFallsBackToBacktracking()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", Pcre2CompileOptions.None);
        var hit = regex.MatchDetailed(Utf8("aaabbxx"), 0, Pcre2MatchOptions.Anchored);
        Assert.True(hit.Success);
        Assert.Equal("aaabb", hit.Value.GetValueString());
        Assert.Equal("aaa", hit.GetGroup(1).GetValueString());
        var miss = regex.MatchDetailed(Utf8("xxaaabbyy"), 0, Pcre2MatchOptions.Anchored);
        Assert.False(miss.Success);
    }

    [Fact]
    public void ExhaustedMatchLimitFailsDetailedWithMatchLimitIdentity()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)(b+)",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Utf8Pcre2Regex.DefaultMatchTimeout);
        var exception = Assert.Throws<Pcre2MatchException>(
            () => regex.MatchDetailed(Utf8("xxaaabbyy"), 0));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
    }

    [Fact]
    public void OwnedDetailedTimeoutThrowsTimeoutIdentity()
    {
        var input = Utf8(new string((char)97, 2000));
        var regex = new Utf8Pcre2Regex(
            "(a+)(b+)",
            Pcre2CompileOptions.None,
            default,
            default,
            TimeSpan.FromMilliseconds(1));
        var exception = Assert.Throws<Pcre2MatchException>(
            () => regex.MatchDetailed(input, 0));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> EnumerateCases()
    {
        yield return new object?[]
            { "(a+)(b+)", "aab aabb aaabbb", 0, new[] { 0, 3, 4, 8, 9, 15 } };
        yield return new object?[]
            { "(a+)(b+)", "aab aabb aaabbb", 4, new[] { 4, 8, 9, 15 } };
        yield return new object?[]
            { "(ab|cd)+", "ab cd abcd", 0, new[] { 0, 2, 3, 5, 6, 10 } };
        yield return new object?[]
            { "(a+)(b+)", "xyz", 0, new int[0] };
    }

    [Theory]
    [MemberData(nameof(EnumerateCases))]
    public void OwnedEnumerateYieldsOracleSequence(
        string pattern,
        string inputText,
        int startOffsetInBytes,
        int[] expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern)
            .Matches(inputText, startOffsetInBytes)
            .Select(static m => new[] { m.Index, m.Index + m.Length })
            .SelectMany(static pair => pair)
            .ToArray();
        Assert.Equal(expected, oracle);
        var actual = new List<int>();
        var enumerator = regex.EnumerateMatches(input, startOffsetInBytes);
        while (enumerator.MoveNext())
        {
            actual.Add(enumerator.Current.StartOffsetInBytes);
            actual.Add(enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(expected, actual.ToArray());
        Assert.False(
            regex.DebugFoundationCoreFallbackRegexIsCreated,
            "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerateEmptyCapableFollowsPcre2Rule()
    {
        var regex = new Utf8Pcre2Regex("x*|y", Pcre2CompileOptions.None);
        var actual = new List<int>();
        var enumerator = regex.EnumerateMatches(Utf8("y"), 0);
        while (enumerator.MoveNext())
        {
            actual.Add(enumerator.Current.StartOffsetInBytes);
            actual.Add(enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(new[] { 0, 0, 0, 1, 1, 1 }, actual.ToArray());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedEnumerateMatchesOracleOnAstralInput()
    {
        const string pattern = "(a+)(b+)";
        const string inputText = "\U0001F600aaabb\U0001F600";
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Matches(inputText);
        var actual = new List<string>();
        var enumerator = regex.EnumerateMatches(input, 0);
        while (enumerator.MoveNext())
        {
            actual.Add(enumerator.Current.GetValueString());
        }

        Assert.Equal(
            oracle.Select(static m => m.Value).ToArray(),
            actual.ToArray());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedEnumerateSupportsAbandonmentAndMatchMany()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", Pcre2CompileOptions.None);
        var input = Utf8("aab aabb aaabbb");
        var partial = regex.EnumerateMatches(input, 0);
        Assert.True(partial.MoveNext());
        Assert.Equal(0, partial.Current.StartOffsetInBytes);
        Assert.Equal(3, partial.Current.EndOffsetInBytes);

        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[8];
        Assert.Equal(
            3,
            regex.MatchMany(input, destination, out var isMore));
        Assert.False(isMore);
        Assert.Equal(9, destination[2].StartOffsetInBytes);
        Assert.Equal(15, destination[2].EndOffsetInBytes);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedEnumeration()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", Pcre2CompileOptions.None);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(
            () => regex.EnumerateMatches(
                Utf8("aab"), 0, Pcre2MatchOptions.None, canceled.Token));
    }

    [Fact]
    public void ExhaustedMatchLimitFailsEnumerationWithMatchLimitIdentity()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)(b+)",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Utf8Pcre2Regex.DefaultMatchTimeout);
        var enumerator = regex.EnumerateMatches(Utf8("aab aabb"), 0);
        try
        {
            enumerator.MoveNext();
            Assert.Fail("Expected Pcre2MatchException.");
        }
        catch (Pcre2MatchException exception)
        {
            Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
        }
    }

    [Fact]
    public void OwnedEnumerationTimeoutThrowsTimeoutIdentity()
    {
        var input = Utf8(new string((char)97, 2000));
        var regex = new Utf8Pcre2Regex(
            "(a+)(b+)",
            Pcre2CompileOptions.None,
            default,
            default,
            TimeSpan.FromMilliseconds(1));
        var enumerator = regex.EnumerateMatches(input, 0);
        try
        {
            enumerator.MoveNext();
            Assert.Fail("Expected Pcre2MatchException.");
        }
        catch (Pcre2MatchException exception)
        {
            Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
        }
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> ReplaceCases()
    {
        yield return new object?[]
            { "(a+)(b+)", "xxaaabbyy", "<$0>", "xx<aaabb>yy" };
        yield return new object?[]
            { "(a+)(b+)", "xxaaabbyy", "<$1|$2>", "xx<aaa|bb>yy" };
        yield return new object?[]
            { "(a+)(b+)", "aab aabb", "[$0]", "[aab] [aabb]" };
        yield return new object?[]
            { "(ab|cd)+", "xxabcdcdyy", "<$1>", "xx<cd>yy" };
        yield return new object?[]
            { "(a+)(b+)", "xxaaayy", "Z", "xxaaayy" };
        yield return new object?[]
            { "a+b", "aaab", "($0)", "(aaab)" };
    }

    [Theory]
    [MemberData(nameof(ReplaceCases))]
    public void OwnedReplaceMatchesExpected(
        string pattern,
        string inputText,
        string replacement,
        string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        Assert.Equal(expected, regex.ReplaceToString(input, replacement));
        Assert.Equal(
            expected,
            Encoding.UTF8.GetString(regex.Replace(input, replacement)));
        Assert.False(
            regex.DebugFoundationCoreFallbackRegexIsCreated,
            "pattern=" + pattern);
    }

    [Theory]
    [InlineData("y")]
    [InlineData("yy")]
    [InlineData("xyx")]
    public void OwnedReplaceEmptyMatchesAgreeWithBacktracking(string inputText)
    {
        var input = Utf8(inputText);
        var sliced = new Utf8Pcre2Regex("x*|y", Pcre2CompileOptions.None);
        var backtracked = new Utf8Pcre2Regex("(?:x*|y)", Pcre2CompileOptions.None);
        Assert.DoesNotContain(
            "Pcre2Slice",
            backtracked.DebugDescribeExecutionPlan());
        Assert.Equal(
            backtracked.ReplaceToString(input, "-"),
            sliced.ReplaceToString(input, "-"));
        Assert.Equal(
            backtracked.ReplaceToString(input, "<$0>"),
            sliced.ReplaceToString(input, "<$0>"));
        Assert.False(sliced.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedReplaceMatchesExpectedOnAstralInput()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", Pcre2CompileOptions.None);
        Assert.Equal(
            "\U0001F600<aaabb>\U0001F600",
            regex.ReplaceToString(Utf8("\U0001F600aaabb\U0001F600"), "<$0>"));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedReplaceHonorsStartOffset()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", Pcre2CompileOptions.None);
        Assert.Equal(
            "aab <aabb>",
            regex.ReplaceToString(Utf8("aab aabb"), "<$0>", 4));
    }

    [Fact]
    public void AnchoredReplaceFallsBackToBacktracking()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", Pcre2CompileOptions.None);
        Assert.Equal(
            "<aaabb>xx",
            regex.ReplaceToString(
                Utf8("aaabbxx"), "<$0>", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.Anchored));
        Assert.Equal(
            "xxaaabbyy",
            regex.ReplaceToString(
                Utf8("xxaaabbyy"), "<$0>", 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.Anchored));
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedReplace()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", Pcre2CompileOptions.None);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(
            () => regex.Replace(
                Utf8("xxaaabbyy"),
                "<$0>",
                0,
                Pcre2SubstitutionOptions.None,
                Pcre2MatchOptions.None,
                canceled.Token));
    }

    [Fact]
    public void ExhaustedMatchLimitFailsReplaceWithMatchLimitIdentity()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)(b+)",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Utf8Pcre2Regex.DefaultMatchTimeout);
        var exception = Assert.Throws<Pcre2MatchException>(
            () => regex.ReplaceToString(Utf8("xxaaabbyy"), "<$0>"));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
    }

    [Fact]
    public void OwnedReplaceTimeoutThrowsTimeoutIdentity()
    {
        var input = Utf8(new string((char)97, 2000));
        var regex = new Utf8Pcre2Regex(
            "(a+)(b+)",
            Pcre2CompileOptions.None,
            default,
            default,
            TimeSpan.FromMilliseconds(1));
        var exception = Assert.Throws<Pcre2MatchException>(
            () => regex.ReplaceToString(input, "<$0>"));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void AnchoredMatchOptionsFallBackToBacktracking()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", Pcre2CompileOptions.None);
        var hit = regex.Match(Utf8("aaabbxx"), 0, Pcre2MatchOptions.Anchored);
        Assert.True(hit.Success);
        Assert.Equal(0, hit.StartOffsetInBytes);
        Assert.Equal(5, hit.EndOffsetInBytes);
        var miss = regex.Match(Utf8("xxaaabbyy"), 0, Pcre2MatchOptions.Anchored);
        Assert.False(miss.Success);
        var offset = regex.Match(Utf8("xxaaabbyy"), 2, Pcre2MatchOptions.Anchored);
        Assert.True(offset.Success);
        Assert.Equal(2, offset.StartOffsetInBytes);
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedOperations()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", Pcre2CompileOptions.None);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var input = Utf8("xxaaabbyy");
        Assert.Throws<OperationCanceledException>(
            () => regex.IsMatch(input, 0, Pcre2MatchOptions.None, canceled.Token));
        Assert.Throws<OperationCanceledException>(
            () => regex.Count(input, 0, Pcre2MatchOptions.None, canceled.Token));
        Assert.Throws<OperationCanceledException>(
            () => regex.Match(input, 0, Pcre2MatchOptions.None, canceled.Token));
    }

    [Fact]
    public void GenerousMatchLimitSucceedsOnSlice()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)(b+)",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1000000 },
            Utf8Pcre2Regex.DefaultMatchTimeout);
        var input = Utf8("xxaaabbyy");
        Assert.True(regex.IsMatch(input, 0));
        Assert.Equal(1, regex.Count(input, 0));
        Assert.True(regex.Match(input, 0).Success);
    }

    [Fact]
    public void ExhaustedMatchLimitFailsWithMatchLimitIdentity()
    {
        var regex = new Utf8Pcre2Regex(
            "(a+)(b+)",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Utf8Pcre2Regex.DefaultMatchTimeout);
        var input = Utf8("xxaaabbyy");
        var countException = Assert.Throws<Pcre2MatchException>(() => regex.Count(input, 0));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, countException.ErrorKind);
        var searchException = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input, 0));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, searchException.ErrorKind);
    }

    [Fact]
    public void OwnedCountTimeoutThrowsTimeoutIdentity()
    {
        var input = Utf8(new string((char)97, 2000));
        var regex = new Utf8Pcre2Regex(
            "(a+)(b+)",
            Pcre2CompileOptions.None,
            default,
            default,
            TimeSpan.FromMilliseconds(1));
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count(input, 0));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }
}
