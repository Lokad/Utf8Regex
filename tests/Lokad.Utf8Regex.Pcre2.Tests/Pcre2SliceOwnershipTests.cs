using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R02a slice ownership for the PCRE2 profile: slice-shaped patterns execute
// all five operations on the shared primary-library slice runner through
// Pcre2SliceDirectProgram, with PCRE2-owned projection, global progression,
// budgets, timeout and failure vocabulary. Options outside that admitted route
// retain the owned backtracking program.
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
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
        var regex = new Utf8Pcre2Regex("a{2,4}?a", options: Pcre2CompileOptions.None);
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
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: options);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData("(?P<word>foo)-(?P=word)")]
    public void NonSliceShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText, startOffsetInBytes));
        Assert.Equal(expected, regex.IsMatch(input, startOffsetInBytes));
        Assert.False(
            regex.DebugCompiledProgram is null,
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        Assert.Equal(expected, Bcl(pattern).Matches(inputText, startOffsetInBytes).Count);
        Assert.Equal(expected, regex.Count(input, startOffsetInBytes));
        Assert.False(
            regex.DebugCompiledProgram is null,
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.False(
            regex.DebugCompiledProgram is null,
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Match(inputText, startOffsetInBytes);
        Assert.Equal(expectedStart >= 0, oracle.Success);
        var found = regex.Match(input, startOffsetInBytes: startOffsetInBytes);
        Assert.Equal(oracle.Success, found.Success);
        if (oracle.Success)
        {
            Assert.Equal(oracle.Index, expectedStart);
            Assert.Equal(oracle.Index + oracle.Length, expectedEnd);
            Assert.Equal(expectedStart, found.StartOffsetInBytes);
            Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        }

        Assert.False(
            regex.DebugCompiledProgram is null,
            "pattern=" + pattern);
    }

    [Fact]
    public void OwnedOperationsMatchOracleOnAstralInput()
    {
        const string pattern = "(a+)(b+)";
        const string inputText = "\U0001F600aaabb\U0001F600";
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Match(inputText);
        Assert.True(oracle.Success);
        var expectedStart = Encoding.UTF8.GetByteCount(inputText[..oracle.Index]);
        var expectedEnd = expectedStart + Encoding.UTF8.GetByteCount(oracle.Value);
        Assert.True(regex.IsMatch(input, 0));
        Assert.Equal(1, regex.Count(input, 0));
        var found = regex.Match(input);
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.StartOffsetInBytes);
        Assert.Equal(expectedEnd, found.EndOffsetInBytes);
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Match(inputText, startOffsetInBytes);
        Assert.Equal(expectedSuccess, oracle.Success);
        ReadOnlySpan<byte> detailed1Input = input;
        var detailed1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var detailed = regex.Match(detailed1Input, startOffsetInBytes: startOffsetInBytes, captures: detailed1Captures);
        Assert.Equal(oracle.Success, detailed.Success);
        if (oracle.Success)
        {
            Assert.Equal(oracle.Groups[0].Value, expectedWhole);
            Assert.Equal(expectedWhole, System.Text.Encoding.UTF8.GetString(detailed.GetValueBytes()));
            if (oracle.Groups.Count > 1)
            {
                Assert.Equal(
                    oracle.Groups[1].Success,
                    detailed1Captures[1].Success);
                Assert.Equal(expectedFirst, System.Text.Encoding.UTF8.GetString((detailed1Captures[1].Success ? detailed1Input.Slice(detailed1Captures[1].StartOffsetInBytes, detailed1Captures[1].EndOffsetInBytes - detailed1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
            }
            if (oracle.Groups.Count > 2)
            {
                Assert.Equal(
                    oracle.Groups[2].Success,
                    detailed1Captures[2].Success);
                Assert.Equal(expectedSecond, System.Text.Encoding.UTF8.GetString((detailed1Captures[2].Success ? detailed1Input.Slice(detailed1Captures[2].StartOffsetInBytes, detailed1Captures[2].EndOffsetInBytes - detailed1Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
            }
        }

        Assert.False(
            regex.DebugCompiledProgram is null,
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Match(inputText, startOffsetInBytes);
        ReadOnlySpan<byte> detailed2Input = input;
        var detailed2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var detailed = regex.Match(detailed2Input, startOffsetInBytes: startOffsetInBytes, captures: detailed2Captures);
        Assert.True(oracle.Success);
        Assert.True(detailed.Success);
        Assert.True(detailed2Captures[winningGroup].Success);
        Assert.Equal(winningValue, System.Text.Encoding.UTF8.GetString((detailed2Captures[winningGroup].Success ? detailed2Input.Slice(detailed2Captures[winningGroup].StartOffsetInBytes, detailed2Captures[winningGroup].EndOffsetInBytes - detailed2Captures[winningGroup].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(
            oracle.Groups[3 - winningGroup].Success,
            detailed2Captures[3 - winningGroup].Success);
        Assert.False(detailed2Captures[3 - winningGroup].Success);
        Assert.False(
            regex.DebugCompiledProgram is null,
            "pattern=" + pattern);
    }

    [Fact]
    public void OwnedDetailedMatchesOracleOnAstralInput()
    {
        const string pattern = "(a+)(b+)";
        const string inputText = "\U0001F600aaabb\U0001F600";
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Match(inputText);
        ReadOnlySpan<byte> detailed3Input = input;
        var detailed3Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var detailed = regex.Match(detailed3Input, captures: detailed3Captures);
        Assert.True(oracle.Success);
        Assert.True(detailed.Success);
        Assert.Equal(oracle.Groups[0].Value, System.Text.Encoding.UTF8.GetString(detailed.GetValueBytes()));
        Assert.Equal(oracle.Groups[1].Value, System.Text.Encoding.UTF8.GetString((detailed3Captures[1].Success ? detailed3Input.Slice(detailed3Captures[1].StartOffsetInBytes, detailed3Captures[1].EndOffsetInBytes - detailed3Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(oracle.Groups[2].Value, System.Text.Encoding.UTF8.GetString((detailed3Captures[2].Success ? detailed3Input.Slice(detailed3Captures[2].StartOffsetInBytes, detailed3Captures[2].EndOffsetInBytes - detailed3Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        var expectedStart = Encoding.UTF8.GetByteCount(inputText[..oracle.Index]);
        Assert.Equal(expectedStart, detailed.StartOffsetInBytes);
    }

    [Fact]
    public void AnchoredDetailedFallsBackToBacktracking()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        ReadOnlySpan<byte> hit4Input = Utf8("aaabbxx");
        var hit4Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var hit = regex.Match(hit4Input, captures: hit4Captures, matchOptions: Pcre2MatchOptions.Anchored);
        Assert.True(hit.Success);
        Assert.Equal("aaabb", System.Text.Encoding.UTF8.GetString(hit.GetValueBytes()));
        Assert.Equal("aaa", System.Text.Encoding.UTF8.GetString((hit4Captures[1].Success ? hit4Input.Slice(hit4Captures[1].StartOffsetInBytes, hit4Captures[1].EndOffsetInBytes - hit4Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        var miss = regex.Match(Utf8("xxaaabbyy"), matchOptions: Pcre2MatchOptions.Anchored);
        Assert.False(miss.Success);
    }

    [Fact]
    public void OwnedDetailedTimeoutThrowsTimeoutIdentity()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cursor = regex.CreateMatchCursor("aaabb"u8.ToArray());
        ticks = 2;
        var exception = Assert.Throws<Pcre2MatchException>(() => cursor.MoveNext(out _));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
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
            regex.DebugCompiledProgram is null,
            "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerateEmptyCapableFollowsPcre2Rule()
    {
        var regex = new Utf8Pcre2Regex("x*|y", options: Pcre2CompileOptions.None);
        var actual = new List<int>();
        var enumerator = regex.EnumerateMatches(Utf8("y"), 0);
        while (enumerator.MoveNext())
        {
            actual.Add(enumerator.Current.StartOffsetInBytes);
            actual.Add(enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(new[] { 0, 0, 0, 1, 1, 1 }, actual.ToArray());
    }

    [Fact]
    public void OwnedEnumerateMatchesOracleOnAstralInput()
    {
        const string pattern = "(a+)(b+)";
        const string inputText = "\U0001F600aaabb\U0001F600";
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        var oracle = Bcl(pattern).Matches(inputText);
        var actual = new List<string>();
        var enumerator = regex.EnumerateMatches(input, 0);
        while (enumerator.MoveNext())
        {
            actual.Add(System.Text.Encoding.UTF8.GetString(enumerator.Current.GetValueBytes()));
        }

        Assert.Equal(
            oracle.Select(static m => m.Value).ToArray(),
            actual.ToArray());
    }

    [Fact]
    public void OwnedEnumerateSupportsAbandonmentAndFreshTraversal()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        var input = Utf8("aab aabb aaabbb");
        var partial = regex.EnumerateMatches(input, 0);
        Assert.True(partial.MoveNext());
        Assert.Equal(0, partial.Current.StartOffsetInBytes);
        Assert.Equal(3, partial.Current.EndOffsetInBytes);

        var fresh = regex.EnumerateMatches(input);
        var ranges = new List<(int Start, int End)>();
        while (fresh.MoveNext())
            ranges.Add((fresh.Current.StartOffsetInBytes, fresh.Current.EndOffsetInBytes));
        Assert.Equal([(0, 3), (4, 8), (9, 15)], ranges);
        Assert.Equal((0, 3), (partial.Current.StartOffsetInBytes, partial.Current.EndOffsetInBytes));
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedEnumeration()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(
            () => regex.EnumerateMatches(
                Utf8("aab"), 0, Pcre2MatchOptions.None, canceled.Token));
    }

    [Fact]
    public void OwnedEnumerationTimeoutThrowsTimeoutIdentity()
    {
        var input = Utf8(new string('a', 8192));
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });

        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads >= 3 ? 2 : 0);
        try
        {
            var enumerator = regex.EnumerateMatches(input, 0);
            enumerator.MoveNext();
            Assert.Fail("Expected Pcre2MatchException.");
        }
        catch (Pcre2MatchException exception)
        {
            Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
        }
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        var input = Utf8(inputText);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(regex.Replace(input, Encoding.UTF8.GetBytes(replacement))));
        Assert.Equal(
            expected,
            Encoding.UTF8.GetString(regex.Replace(input, Encoding.UTF8.GetBytes(replacement))));
        Assert.False(
            regex.DebugCompiledProgram is null,
            "pattern=" + pattern);
    }

    [Theory]
    [InlineData("y")]
    [InlineData("yy")]
    [InlineData("xyx")]
    public void OwnedReplaceEmptyMatchesAgreeWithBacktracking(string inputText)
    {
        var input = Utf8(inputText);
        var sliced = new Utf8Pcre2Regex("x*|y", options: Pcre2CompileOptions.None);
        // The transparent wrapper now rides the slice, so the backtracking side spells its group Python-style (the core slice declines it).
        var backtracked = new Utf8Pcre2Regex("(?P<g>x*|y)", options: Pcre2CompileOptions.None);
        Assert.DoesNotContain(
            "Pcre2Slice",
            backtracked.DebugDescribeExecutionPlan());
        Assert.Equal(
            System.Text.Encoding.UTF8.GetString(backtracked.Replace(input, "-"u8)),
            System.Text.Encoding.UTF8.GetString(sliced.Replace(input, "-"u8)));
        Assert.Equal(
            System.Text.Encoding.UTF8.GetString(backtracked.Replace(input, "<$0>"u8)),
            System.Text.Encoding.UTF8.GetString(sliced.Replace(input, "<$0>"u8)));
    }

    [Fact]
    public void OwnedReplaceMatchesExpectedOnAstralInput()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        Assert.Equal(
            "\U0001F600<aaabb>\U0001F600",
            System.Text.Encoding.UTF8.GetString(regex.Replace(Utf8("\U0001F600aaabb\U0001F600"), "<$0>"u8)));
    }

    [Fact]
    public void OwnedReplaceHonorsStartOffset()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        Assert.Equal(
            "aab <aabb>",
            System.Text.Encoding.UTF8.GetString(regex.Replace(Utf8("aab aabb"), "<$0>"u8, 4)));
    }

    [Fact]
    public void TinyHeapLimitFailsShapingAdmissionWithHeapLimitIdentity()
    {
        var input = Utf8("xxaaabbyy");
        var starved = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        var rentException = Assert.Throws<Pcre2MatchException>(
            () => System.Text.Encoding.UTF8.GetString(starved.Replace(input, "<$0>"u8)));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, rentException.ErrorKind);

        var grown = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 20
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        var growthException = Assert.Throws<Pcre2MatchException>(
            () => System.Text.Encoding.UTF8.GetString(grown.Replace(input, "<$0>"u8)));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, growthException.ErrorKind);
    }

    [Fact]
    public void SubstituteLiteralTreatsTemplateLiterally()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        Assert.Equal(
            "xx<$0>yy",
            System.Text.Encoding.UTF8.GetString(regex.Replace(
                Utf8("xxaaabbyy"),
                "<$0>"u8,
                0,
                Pcre2SubstitutionOptions.SubstituteLiteral)));
    }

    [Theory]
    [InlineData("xxaaabbyy", "<aaabb>")]
    [InlineData("xyz", "")]
    public void SubstituteReplacementOnlyOmitsUnmatchedText(
        string inputText,
        string expected)
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        Assert.Equal(
            expected,
            System.Text.Encoding.UTF8.GetString(regex.Replace(
                Utf8(inputText),
                "<$0>"u8,
                0,
                Pcre2SubstitutionOptions.SubstituteReplacementOnly)));
    }

    [Fact]
    public void FullTemplateExpansionAgreesWithBacktracking()
    {
        var input = Utf8("xxaaabbyy");
        var sliced = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        var backtracked = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.Caseless);
        Assert.DoesNotContain(
            "Pcre2Slice",
            backtracked.DebugDescribeExecutionPlan());
        Assert.Equal("xx<aaa|bb>yy", System.Text.Encoding.UTF8.GetString(backtracked.Replace(input, "<$1|$2>"u8)));
        Assert.Equal(
            System.Text.Encoding.UTF8.GetString(backtracked.Replace(input, "<$1|$2>"u8)),
            System.Text.Encoding.UTF8.GetString(sliced.Replace(input, "<$1|$2>"u8)));
    }

    [Fact]
    public void AnchoredReplaceFallsBackToBacktracking()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        Assert.Equal(
            "<aaabb>xx",
            System.Text.Encoding.UTF8.GetString(regex.Replace(
                Utf8("aaabbxx"), "<$0>"u8, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.Anchored)));
        Assert.Equal(
            "xxaaabbyy",
            System.Text.Encoding.UTF8.GetString(regex.Replace(
                Utf8("xxaaabbyy"), "<$0>"u8, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.Anchored)));
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedReplace()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(
            () => regex.Replace(
                Utf8("xxaaabbyy"),
                "<$0>"u8,
                0,
                Pcre2SubstitutionOptions.None,
                Pcre2MatchOptions.None,
                canceled.Token));
    }

    [Fact]
    public void OwnedReplaceTimeoutThrowsTimeoutIdentity()
    {
        var input = Utf8(new string('a', 8192));
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads >= 3 ? 2 : 0);
        var exception = Assert.Throws<Pcre2MatchException>(
            () => System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }

    [Fact]
    public void AnchoredMatchOptionsFallBackToBacktracking()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        var hit = regex.Match(Utf8("aaabbxx"), matchOptions: Pcre2MatchOptions.Anchored);
        Assert.True(hit.Success);
        Assert.Equal(0, hit.StartOffsetInBytes);
        Assert.Equal(5, hit.EndOffsetInBytes);
        var miss = regex.Match(Utf8("xxaaabbyy"), matchOptions: Pcre2MatchOptions.Anchored);
        Assert.False(miss.Success);
        var offset = regex.Match(Utf8("xxaaabbyy"), startOffsetInBytes: 2, matchOptions: Pcre2MatchOptions.Anchored);
        Assert.True(offset.Success);
        Assert.Equal(2, offset.StartOffsetInBytes);
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedOperations()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var input = Utf8("xxaaabbyy");
        Assert.Throws<OperationCanceledException>(
            () => regex.IsMatch(input, 0, Pcre2MatchOptions.None, canceled.Token));
        Assert.Throws<OperationCanceledException>(
            () => regex.Count(input, 0, Pcre2MatchOptions.None, canceled.Token));
        Assert.Throws<OperationCanceledException>(
            () => regex.Match(input, cancellationToken: canceled.Token));
    }

    [Fact]
    public void FiniteDepthSucceedsOnSlice()
    {
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 4096
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        var input = Utf8("xxaaabbyy");
        Assert.True(regex.IsMatch(input, 0));
        Assert.Equal(1, regex.Count(input, 0));
        Assert.True(regex.Match(input).Success);
    }

    [Fact]
    public void OwnedCountTimeoutThrowsTimeoutIdentity()
    {
        var input = Utf8(new string('a', 8192));
        var regex = new Utf8Pcre2Regex("(a+)(b+)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads >= 3 ? 2 : 0);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count(input, 0));
        Assert.Equal(Pcre2ErrorKind.Timeout, exception.ErrorKind);
    }
}
