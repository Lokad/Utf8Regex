using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

// R03 NonBacktracking execution contract: only BCL's symbolic engine honors
// linear-time execution, so every NonBacktracking shape declines the owned
// backtracking slice to legacy-exact routing (fallback reason
// "unsupported_options"). The contract pinned here is construction validation
// (NotSupportedException for backreferences, lookarounds, balancing,
// conditionals, atomics, and \G; RegexParseException for malformed patterns),
// result parity with the backtracking oracle on supported shapes, linear-time
// completion of catastrophic inputs, and honored timeouts (whose BCL
// exceptions carry an empty pattern). R05 will revisit engine independence;
// until then these tests fail if NonBacktracking ever rides the backtracker.
public sealed class Utf8SliceNonBacktrackingTests
{
    public static IEnumerable<object?[]> LegacyShapes()
    {
        yield return new object?[] { "abc" };
        yield return new object?[] { "a+" };
        yield return new object?[] { "(a+)(b+)" };
        yield return new object?[] { "a|ab" };
        yield return new object?[] { "^a$" };
        yield return new object?[] { "\\bword\\b" };
        yield return new object?[] { "\\d+" };
        yield return new object?[] { "(?:ab)+" };
        yield return new object?[] { "(?<n>a)+" };
        yield return new object?[] { "\\t+" };
        yield return new object?[] { "\\cA+" };
    }

    [Theory]
    [MemberData(nameof(LegacyShapes))]
    public void NonBacktrackingShapesRouteLegacy(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant,
            RegexOptions.NonBacktracking | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
            Assert.Equal("unsupported_options", regex.Inspection.FallbackReason);
            Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "NonBacktracking validates eagerly like BCL");
            var legacyInput = Encoding.UTF8.GetBytes("xxaaxx");
            Assert.Equal(new Regex(pattern, options, Regex.InfiniteMatchTimeout).IsMatch("xxaaxx"), regex.IsMatch(legacyInput));
            Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> UnsupportedShapes()
    {
        yield return new object?[] { "(a)\\1" };
        yield return new object?[] { "(?<n>a)\\k<n>" };
        yield return new object?[] { "(?<=a)b" };
        yield return new object?[] { "(?<=\\n)x" };
        yield return new object?[] { "(?<!a)b" };
        yield return new object?[] { "a(?=b)" };
        yield return new object?[] { "a(?!b)" };
        yield return new object?[] { "(?<o>a)(?<-o>b)" };
        yield return new object?[] { "(a)?(?(1)b|c)" };
        yield return new object?[] { "(?(?=a)b|c)" };
        yield return new object?[] { "(?>a+)b" };
        yield return new object?[] { "\\Ga" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedShapes))]
    public void NonBacktrackingUnsupportedShapesThrowAtConstruction(string pattern)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
        Assert.Throws<NotSupportedException>(() => new Regex(pattern, options));
        Assert.Throws<NotSupportedException>(() => new Utf8Regex(pattern, options));
    }

    [Theory]
    [InlineData("(a")]
    [InlineData("a{3,2}")]
    [InlineData("[z-a]")]
    public void NonBacktrackingMalformedShapesFailConstruction(string pattern)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
        Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
        Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, options));
    }

    public static IEnumerable<object?[]> ParityCases()
    {
        yield return new object?[] { "abc", "xxabcxx", true };
        yield return new object?[] { "abc", "ab", false };
        yield return new object?[] { "a+", "aaabbb", true };
        yield return new object?[] { "a|ab", "xab", true };
        yield return new object?[] { "ab|a", "xab", true };
        yield return new object?[] { "(a+)(b+)", "aab", true };
        yield return new object?[] { "(a)+", "aaa", true };
        yield return new object?[] { "a*", "b", true };
        yield return new object?[] { "a*?", "aab", true };
        yield return new object?[] { "^|b$", "b", true };
        yield return new object?[] { "\\b", "a b", true };
        yield return new object?[] { "(a|ab)*", "aab", true };
        yield return new object?[] { "(x)?(y)?", "xy", true };
        yield return new object?[] { "(?:ab|a)+", "aba", true };
        yield return new object?[] { "(a+)+b", "aaab", true };
        yield return new object?[] { "^a$", "a", true };
        yield return new object?[] { "\\Aabc\\z", "abc", true };
        yield return new object?[] { "(?<n>a)+", "aaa", true };
    }

    [Theory]
    [MemberData(nameof(ParityCases))]
    public void NonBacktrackingIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var backtracking = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, backtracking.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        using var source = new CancellationTokenSource();
        Assert.Equal(expected, regex.IsMatch(input, source.Token));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("ab+", "AB", RegexOptions.IgnoreCase)]
    [InlineData("^a", "x\na", RegexOptions.Multiline)]
    [InlineData("a.c", "a\nc", RegexOptions.Singleline)]
    [InlineData("a b", "ab", RegexOptions.IgnorePatternWhitespace)]
    [InlineData("(a)(b)", "ab", RegexOptions.ExplicitCapture)]
    public void NonBacktrackingOptionCombosMatchOracle(string pattern, string inputText, RegexOptions extra)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | extra;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        var expected = oracle.Match(inputText);
        var found = regex.Match(input);
        Assert.Equal(expected.Success, found.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Index, found.IndexInUtf16);
            Assert.Equal(expected.Length, found.LengthInUtf16);
        }

        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void NonBacktrackingLinearExecutionCompletesCatastrophicInput()
    {
        // The execution contract: a backtracker explodes here while the
        // symbolic engine stays linear. A generous finite budget keeps the
        // test diagnostic (not hanging) if routing ever regresses.
        const string pattern = "(a+)+b";
        var inputText = new string('a', 2000);
        var input = Encoding.UTF8.GetBytes(inputText);
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
        var oracle = new Regex(pattern, options, TimeSpan.FromSeconds(5));
        Assert.False(oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(5));
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.False(regex.IsMatch(input));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Fact]
    public void NonBacktrackingTimeoutHonoredLikeRuntime()
    {
        const string pattern = "a+b+";
        var inputText = new string('a', 2000000) + new string('b', 2000000);
        var input = Encoding.UTF8.GetBytes(inputText);
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
        var budget = TimeSpan.FromTicks(1);
        var oracle = new Regex(pattern, options, budget);
        var oracleFailure = Assert.Throws<RegexMatchTimeoutException>(() => oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options, budget);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram);
        var failure = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.Equal(oracleFailure.Pattern, failure.Pattern);
        Assert.Equal(oracleFailure.MatchTimeout, failure.MatchTimeout);
    }

    [Fact]
    public void NonBacktrackingFromOffsetAndSplitMatchOracle()
    {
        const string pattern = "a+";
        const string inputText = "xaa";
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram);
        var wanted = oracle.Match(inputText, 1);
        var found = regex.MatchFromUtf16Offset(input, 1);
        Assert.Equal(wanted.Success, found.Success);
        Assert.Equal(wanted.Index, found.IndexInUtf16);
        Assert.Equal(wanted.Length, found.LengthInUtf16);
        Assert.Equal(oracle.Matches(inputText, 1).Count, regex.CountFromUtf16Offset(input, 1));
        var expectedSplits = new List<string>();
        foreach (var split in oracle.Split("baab", 0))
        {
            expectedSplits.Add(split);
        }

        var splitRegex = new Utf8Regex("a+", options);
        var actualSplits = new List<string>();
        foreach (var split in splitRegex.EnumerateSplits(Encoding.UTF8.GetBytes("baab"), 0))
        {
            actualSplits.Add(split.GetValueString());
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Fact]
    public void NonBacktrackingStaticIsMatchMatchesOracle()
    {
        const string pattern = "a+b+";
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes("xxaabbxx");
        Assert.Equal(
            Regex.IsMatch("xxaabbxx", pattern, options, Regex.InfiniteMatchTimeout),
            Utf8Regex.IsMatch(input, pattern, options, Regex.InfiniteMatchTimeout));
    }

    public static IEnumerable<object?[]> NonBacktrackingLaneParityCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("ab", "xxabxx", "Q"),
            ("a+b", "xaab", "Q"),
            ("a[^x]+b", "xayzb", "Q"),
            ("(a)(b)", "xxabxx", "<$1$2>"),
            ("a.*b", "xaayb", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(NonBacktrackingLaneParityCases))]
    public void NonBacktrackingLanesMatchOracle(string pattern, string text, string template)
    {
        // R03 NonBacktracking execution contract: every lane rides legacy-exact
        // BCL routing (only BCL symbolic engine honors linear-time execution),
        // so Count, Match, Detailed, enumeration, splits, and all three
        // replacement cores construct the fallback engine with oracle parity on
        // both lanes. IsMatch-only parity lives in NonBacktrackingIsMatchMatchesOracle.
        var input = Encoding.UTF8.GetBytes(text);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        foreach (var options in new[] { RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, RegexOptions.NonBacktracking | RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var isMatchRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
            var matchRegex = new Utf8Regex(pattern, options);
            var expectedMatch = oracle.Match(text);
            var actualMatch = matchRegex.Match(input);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
            }
            Assert.True(matchRegex.Inspection.DebugFallbackRegexIsCreated);
            var detailedRegex = new Utf8Regex(pattern, options);
            var expectedDetailed = oracle.Match(text);
            var actualDetailed = detailedRegex.MatchDetailed(input);
            Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
            if (expectedDetailed.Success)
            {
                Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
            }
            Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
            var enumerateRegex = new Utf8Regex(pattern, options);
            var expectedPositions = new System.Collections.Generic.List<string>();
            foreach (Match m in oracle.Matches(text))
            {
                expectedPositions.Add(m.Index + ":" + m.Length);
            }
            var actualPositions = new System.Collections.Generic.List<string>();
            foreach (var m in enumerateRegex.EnumerateMatches(input))
            {
                actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
            }
            Assert.Equal(expectedPositions, actualPositions);
            Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
            var splitRegex = new Utf8Regex(pattern, options);
            var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
            foreach (var range in oracle.EnumerateSplits(text, int.MaxValue))
            {
                var start = range.Start.Value;
                var end = range.End.Value;
                expectedSplits.Add((text[start..end], start, end - start));
            }
            var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
            foreach (var split in splitRegex.EnumerateSplits(input, int.MaxValue))
            {
                actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
            }
            Assert.Equal(expectedSplits, actualSplits);
            Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
            var expected = oracle.Replace(text, template);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var bytesRegex = new Utf8Regex(pattern, options);
            var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
            Assert.Equal(expectedBytes, bytesActual);
            Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
            var stringRegex = new Utf8Regex(pattern, options);
            var stringActual = stringRegex.ReplaceToString(input, template);
            Assert.Equal(expected, stringActual);
            Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
            var destinationRegex = new Utf8Regex(pattern, options);
            var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
            Assert.Equal(System.Buffers.OperationStatus.Done, status);
            Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
            Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        }
    }
}
