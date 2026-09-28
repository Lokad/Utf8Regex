using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Anchored ASCII literal suffixes (literal$ and literal\Z covering the whole
// pattern, plus the strict literal\z twin) verify by a byte-exact tail compare:
// only the input end (or right before a single trailing line feed for the lax
// forms) can match, so IsMatch, Match, Count, detailed matches, and enumeration
// never construct the fallback engine, at any start offset. Coordinates map the
// match start through UTF-16 on non-ASCII input.
public sealed class Utf8AnchoredLiteralSuffixTests
{
    public static IEnumerable<object?[]> AcceptCases()
    {
        yield return new object?[] { "ab$", "AnchoredAsciiLiteralSuffix" };
        yield return new object?[] { "ab\\Z", "AnchoredAsciiLiteralSuffix" };
        yield return new object?[] { "ab\\z", "AnchoredAsciiLiteralSuffixStrictEnd" };
        yield return new object?[] { "a$", "AnchoredAsciiLiteralSuffix" };
        yield return new object?[] { "Test_123$", "AnchoredAsciiLiteralSuffix" };
        yield return new object?[] { "a-b$", "AnchoredAsciiLiteralSuffix" };
    }

    [Theory]
    [MemberData(nameof(AcceptCases))]
    public void AnchoredLiteralShapesClassifyToSuffixFamily(string pattern, string expectedKind)
    {
        var plan = Utf8FallbackRegexFamilyAnalyzer.ClassifyPattern(
            pattern, pattern, RegexOptions.CultureInvariant, null);

        Assert.Equal(expectedKind, plan.Kind.ToString());
        Assert.Equal(Utf8FallbackFindModeKind.MatchAtEnd, plan.FindMode);
    }

    public static IEnumerable<object?[]> FoldedAcceptCases()
    {
        yield return new object?[] { "ab$", "AnchoredFoldedAsciiLiteralSuffix" };
        yield return new object?[] { "ab\\z", "AnchoredFoldedAsciiLiteralSuffixStrictEnd" };
    }

    [Theory]
    [MemberData(nameof(FoldedAcceptCases))]
    public void FoldedAnchoredLiteralShapesClassifyToFoldedSuffixFamily(string pattern, string expectedKind)
    {
        var plan = Utf8FallbackRegexFamilyAnalyzer.ClassifyPattern(
            pattern, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, null);

        Assert.Equal(expectedKind, plan.Kind.ToString());
        Assert.Equal(Utf8FallbackFindModeKind.MatchAtEnd, plan.FindMode);
    }

    public static IEnumerable<object?[]> DeclineCases()
    {
        yield return new object?[] { "ab$|cd", RegexOptions.CultureInvariant };
        yield return new object?[] { "a.c$", RegexOptions.CultureInvariant };
        yield return new object?[] { "^(ab)$", RegexOptions.CultureInvariant };
        yield return new object?[] { "^ab$", RegexOptions.CultureInvariant };
        yield return new object?[] { "ab", RegexOptions.CultureInvariant };
        yield return new object?[] { "$", RegexOptions.CultureInvariant };
        yield return new object?[] { "a$b", RegexOptions.CultureInvariant };
        yield return new object?[] { "ab$cd", RegexOptions.CultureInvariant };
        yield return new object?[] { "ab$$", RegexOptions.CultureInvariant };
        yield return new object?[] { "ab$", RegexOptions.Multiline | RegexOptions.CultureInvariant };
        yield return new object?[] { "(?m:ab$)", RegexOptions.CultureInvariant };
        yield return new object?[] { "(?i:ab$)", RegexOptions.CultureInvariant };
        yield return new object?[] { "ab\\$", RegexOptions.CultureInvariant };
        yield return new object?[] { "\\d+$", RegexOptions.CultureInvariant };
        yield return new object?[] { "[ab]$", RegexOptions.CultureInvariant };
        yield return new object?[] { "a{2}$", RegexOptions.CultureInvariant };
        yield return new object?[] { "ab$", RegexOptions.RightToLeft | RegexOptions.CultureInvariant };
        yield return new object?[] { "ab$", RegexOptions.ECMAScript };
    }

    [Theory]
    [MemberData(nameof(DeclineCases))]
    public void OtherShapesKeepExistingClassification(string pattern, RegexOptions options)
    {
        var plan = Utf8FallbackRegexFamilyAnalyzer.ClassifyPattern(
            pattern, pattern, options, null);

        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix, plan.Kind);
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffixStrictEnd, plan.Kind);
    }

    public static IEnumerable<object?[]> BehaviorCases()
    {
        yield return new object?[] { "ab$", "abxx" };
        yield return new object?[] { "ab$", "xxab" };
        yield return new object?[] { "ab$", "ab" };
        yield return new object?[] { "ab$", "x" };
        yield return new object?[] { "ab$", "" };
        yield return new object?[] { "ab$", "a" };
        yield return new object?[] { "ab$", "abcab" };
        yield return new object?[] { "ab$", "ab\n" };
        yield return new object?[] { "ab$", "xxab\n" };
        yield return new object?[] { "ab\\z", "ab\n" };
        yield return new object?[] { "ab\\z", "xxab" };
        yield return new object?[] { "ab\\Z", "ab\n" };
        yield return new object?[] { "a$", "xa" };
        yield return new object?[] { "a$", "ax" };
        yield return new object?[] { "Test_123$", "xTest_123" };
        yield return new object?[] { "Test_123$", "Test_123x" };
        yield return new object?[] { "a-b$", "xa-b" };
        yield return new object?[] { "a-b$", "a-bx" };
    }

    [Theory]
    [MemberData(nameof(BehaviorCases))]
    public void AnchoredLiteralMatchesWithoutFallbackConstruction(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

            var isMatchRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(inputText), isMatchRegex.IsMatch(input));
            Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);

            var matchRegex = new Utf8Regex(pattern, options);
            var expected = oracle.Match(inputText);
            var actual = matchRegex.Match(input);
            Assert.Equal(expected.Success, actual.Success);
            Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(inputText).Count, countRegex.Count(input));
            Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
            if (expected.Success)
            {
                Assert.Equal(expected.Index, actual.IndexInUtf16);
                Assert.Equal(expected.Length, actual.LengthInUtf16);
            }
        }
    }

    public static IEnumerable<object?[]> NonAsciiBehaviorCases()
    {
        // The tail compare is byte-exact on every input: astral scalars never end
        // with ASCII bytes, and the match start maps through UTF-16, so non-ASCII
        // inputs stay zero-construction too.
        yield return new object?[] { "ab$", "éab" };
        yield return new object?[] { "ab$", "abé" };
    }

    [Theory]
    [MemberData(nameof(BehaviorCases))]
    [MemberData(nameof(NonAsciiBehaviorCases))]
    public void AnchoredLiteralDetailedNeverConstructsFallback(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var expected = oracle.Match(inputText);
            var regex = new Utf8Regex(pattern, options);
            var actual = regex.MatchDetailed(input);
            Assert.Equal(expected.Success, actual.Success);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
            if (expected.Success)
            {
                Assert.Equal(expected.Index, actual.GetGroup(0).IndexInUtf16);
                Assert.Equal(expected.Length, actual.GetGroup(0).LengthInUtf16);
            }
        }
    }

    [Theory]
    [MemberData(nameof(BehaviorCases))]
    [MemberData(nameof(NonAsciiBehaviorCases))]
    public void AnchoredLiteralEnumerationNeverConstructsFallback(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var expectedCount = 0;
            foreach (System.Text.RegularExpressions.Match _ in oracle.Matches(inputText))
            {
                expectedCount++;
            }

            var regex = new Utf8Regex(pattern, options);
            var actualCount = 0;
            var enumerator = regex.EnumerateMatches(input);
            while (enumerator.MoveNext())
            {
                actualCount++;
                // Suffix matches end at the input end (or right before a single
                // trailing line feed for the lax `$`/`\Z` forms).
                var endInBytes = enumerator.Current.IndexInBytes + enumerator.Current.LengthInBytes;
                Assert.True(endInBytes == input.Length || (endInBytes == input.Length - 1 && input[input.Length - 1] == (byte)'\n'));
            }

            Assert.Equal(expectedCount, actualCount);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }

    [Theory]
    [MemberData(nameof(BehaviorCases))]
    [MemberData(nameof(NonAsciiBehaviorCases))]
    public void AnchoredLiteralFromOffsetMatchesOracleWithoutFallback(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            foreach (var offset in new[] { 0, 1, 2 })
            {
                if (offset > inputText.Length)
                {
                    continue;
                }

                var expectedMatch = oracle.Match(inputText, offset);
                var regex = new Utf8Regex(pattern, options);
                var actualMatch = regex.MatchFromUtf16Offset(input, offset);
                Assert.Equal(expectedMatch.Success, actualMatch.Success);
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
                var regex2 = new Utf8Regex(pattern, options);
                Assert.Equal(oracle.IsMatch(inputText, offset), regex2.IsMatchFromUtf16Offset(input, offset));
                Assert.False(regex2.Inspection.DebugFallbackRegexIsCreated);
                var regex3 = new Utf8Regex(pattern, options);
                Assert.Equal(expectedMatch.Success, regex3.MatchDetailedFromUtf16Offset(input, offset).Success);
                Assert.False(regex3.Inspection.DebugFallbackRegexIsCreated);
                var regex4 = new Utf8Regex(pattern, options);
                var actualEnum = 0;
                var enumerator = regex4.EnumerateMatchesFromUtf16Offset(input, offset);
                while (enumerator.MoveNext())
                {
                    actualEnum++;
                }

                var expectedEnum = 0;
                foreach (System.Text.RegularExpressions.Match _ in oracle.Matches(inputText, offset))
                {
                    expectedEnum++;
                }

                Assert.Equal(expectedEnum, actualEnum);
                Assert.False(regex4.Inspection.DebugFallbackRegexIsCreated);
            }
        }
    }

    [Theory]
    [MemberData(nameof(BehaviorCases))]
    [MemberData(nameof(NonAsciiBehaviorCases))]
    public void AnchoredLiteralOperationsMatchOracle(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var options = RegexOptions.CultureInvariant;
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        Assert.Equal(oracle.Match(inputText).Success, regex.MatchDetailed(input).Success);
        var expectedCount = 0;
        foreach (System.Text.RegularExpressions.Match _ in oracle.Matches(inputText))
        {
            expectedCount++;
        }

        var actualCount = 0;
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            actualCount++;
        }

        Assert.Equal(expectedCount, actualCount);
    }
}
