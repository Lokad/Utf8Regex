using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Anchored ASCII literal prefixes (^literal and \Aliteral covering the whole
// pattern) verify by a byte-exact prefix compare: a miss at offset 0 is
// definitive, so IsMatch, Match, Count, detailed matches, and enumeration never
// construct the fallback engine. Astral scalars never start with ASCII bytes, so
// the prefix compare stays byte-exact on every input.
public sealed class Utf8AnchoredLiteralPrefixTests
{
    public static IEnumerable<object?[]> AcceptCases()
    {
        yield return new object?[] { "^ab" };
        yield return new object?[] { "\\Aab" };
        yield return new object?[] { "^a" };
        yield return new object?[] { "^Test_123" };
        yield return new object?[] { "^a-b" };
        yield return new object?[] { "^(ab)" };
    }

    [Theory]
    [MemberData(nameof(AcceptCases))]
    public void AnchoredLiteralShapesClassifyToPrefixFamily(string pattern)
    {
        var plan = Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant));

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix, plan.Kind);
        Assert.Equal(Utf8FallbackFindModeKind.MatchAtStart, plan.FindMode);
    }

    public static IEnumerable<object?[]> FoldedAcceptCases()
    {
        yield return new object?[] { "^ab" };
        yield return new object?[] { "\\Aab" };
    }

    [Theory]
    [MemberData(nameof(FoldedAcceptCases))]
    public void FoldedAnchoredLiteralShapesClassifyToFoldedPrefixFamily(string pattern)
    {
        var plan = Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix, plan.Kind);
        Assert.Equal(Utf8FallbackFindModeKind.MatchAtStart, plan.FindMode);
    }

    public static IEnumerable<object?[]> DeclineCases()
    {
        yield return new object?[] { "^ab|cd", RegexOptions.CultureInvariant };
        yield return new object?[] { "^a.c", RegexOptions.CultureInvariant };
        yield return new object?[] { "^", RegexOptions.CultureInvariant };
        yield return new object?[] { "^ab$", RegexOptions.CultureInvariant };
        yield return new object?[] { "^ab", RegexOptions.Multiline | RegexOptions.CultureInvariant };
        yield return new object?[] { "(?m:^ab)", RegexOptions.CultureInvariant };
        yield return new object?[] { "(?i:^ab)", RegexOptions.CultureInvariant };
        yield return new object?[] { "^\\d+", RegexOptions.CultureInvariant };
        yield return new object?[] { "^[ab]", RegexOptions.CultureInvariant };
        yield return new object?[] { "^a{2}", RegexOptions.CultureInvariant };
        yield return new object?[] { "^ab", RegexOptions.RightToLeft | RegexOptions.CultureInvariant };
        yield return new object?[] { "^ab", RegexOptions.ECMAScript };
    }

    [Theory]
    [MemberData(nameof(DeclineCases))]
    public void OtherShapesKeepExistingClassification(string pattern, RegexOptions options)
    {
        var plan = Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, options));

        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix, plan.Kind);
    }

    public static IEnumerable<object?[]> BehaviorCases()
    {
        yield return new object?[] { "^ab", "abxx" };
        yield return new object?[] { "^ab", "xxab" };
        yield return new object?[] { "^ab", "ab" };
        yield return new object?[] { "^ab", "x" };
        yield return new object?[] { "^ab", "" };
        yield return new object?[] { "^ab", "a" };
        yield return new object?[] { "^ab", "abcab" };
        yield return new object?[] { "\\Aab", "abxx" };
        yield return new object?[] { "\\Aab", "xxab" };
        yield return new object?[] { "^a", "ax" };
        yield return new object?[] { "^a", "xa" };
        yield return new object?[] { "^Test_123", "Test_123!" };
        yield return new object?[] { "^Test_123", "xTest_123" };
        yield return new object?[] { "^a-b", "a-bx" };
        yield return new object?[] { "^a-b", "xa-b" };
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
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var matchRegex = new Utf8Regex(pattern, options);
            var expected = oracle.Match(inputText);
            var actual = matchRegex.Match(input);
            Assert.Equal(expected.Success, actual.Success);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(inputText).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
            if (expected.Success)
            {
                Assert.Equal(expected.Index, actual.IndexInUtf16);
                Assert.Equal(expected.Length, actual.LengthInUtf16);
            }
        }
    }

    public static IEnumerable<object?[]> NonAsciiBehaviorCases()
    {
        // The prefix compare is byte-exact on every input: astral scalars never
        // start with ASCII bytes, so non-ASCII inputs stay zero-construction too.
        yield return new object?[] { "^ab", "\u00e9ab" };
        yield return new object?[] { "^ab", "ab\u00e9" };
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
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
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
                Assert.Equal(0, enumerator.Current.IndexInUtf16);
            }

            Assert.Equal(expectedCount, actualCount);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
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
            foreach (var offset in new[] { 0, 1 })
            {
                if (offset > inputText.Length)
                {
                    continue;
                }

                var expectedMatch = oracle.Match(inputText, offset);
                var regex = new Utf8Regex(pattern, options);
                var actualMatch = regex.MatchFromUtf16Offset(input, offset);
                Assert.Equal(expectedMatch.Success, actualMatch.Success);
                Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
                var regex2 = new Utf8Regex(pattern, options);
                Assert.Equal(oracle.IsMatch(inputText, offset), regex2.IsMatchFromUtf16Offset(input, offset));
                Assert.True(regex2.Inspection.PreparedRegex.ScalarProgram is not null);
                var regex3 = new Utf8Regex(pattern, options);
                Assert.Equal(expectedMatch.Success, regex3.MatchDetailedFromUtf16Offset(input, offset).Success);
                Assert.True(regex3.Inspection.PreparedRegex.ScalarProgram is not null);
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
                Assert.True(regex4.Inspection.PreparedRegex.ScalarProgram is not null);
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
