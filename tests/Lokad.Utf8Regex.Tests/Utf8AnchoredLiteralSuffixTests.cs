using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Anchored ASCII literal suffixes can use a single byte-exact tail comparison
// when the pattern proves only one end position can match.
public sealed class Utf8AnchoredLiteralSuffixTests
{
    [Fact]
    public void FinalScalarScreenRejectsBeforeWorkingAdmission()
    {
        const string pattern = @"\A(?:[a-z]+\.)+(?:[A-Z]{2,3}|[0-9])\]?$";
        using var regex = new Utf8Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy { MaxWorkingBytes = 1 }
        });
        using var token = new CancellationTokenSource();
        Assert.False(regex.IsMatch("abc.A#\n"u8, token.Token));
        Assert.Equal(0, regex.Count("abc.A#"u8, token.Token));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch("abc.AB\n"u8, token.Token)).ResourceKind);
    }

    [Theory]
    [InlineData(@"\A(?:[a-z]+\.)+(?:[A-Z]{2,3}|[0-9])\]?$", "abc.AB]\n", true)]
    [InlineData(@"\A(?:[a-z]+\.)+(?:[A-Z]{2,3}|[0-9])\]?\z", "abc.AB]\n", false)]
    [InlineData(@"\A(?:[a-z]+\.)+(?:AB|é)\]?$", "abc.é\n", true)]
    [InlineData(@"\A(?:[a-z]+\.)+(?:AB|é)\]?$", "abc.AB\n", true)]
    [InlineData(@"(?:cd|ab$)", "cd#", true)]
    [InlineData(@"(?m:(?:ab)+$)", "abab\n#", true)]
    [InlineData(@"(?i:(?:ab)+Z)$", "ababz", true)]
    [InlineData(@"\A(?:ab)?$", "", true)]
    public void FinalScalarScreenKeepsAllPossibleEndpoints(string pattern, string text, bool expected)
    {
        using var regex = new Utf8Regex(pattern);
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input, token.Token));
        Assert.Equal(expected ? 1 : 0, regex.Count(input, token.Token));
    }

    [Theory]
    [InlineData(@"\n$", "😀\n\n")]
    [InlineData(@"abc\n\Z", "😀abc\n")]
    [InlineData(@"(?i:ABC\n)$", "😀abc\n")]
    [InlineData(@"abc\n\z", "😀abc\n")]
    public void SuffixLiteralCanConsumeTheFinalLineFeed(string pattern, string text)
    {
        using var regex = new Utf8Regex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern, RegexOptions.CultureInvariant).Matches(text)
            .Select(hit => (Encoding.UTF8.GetByteCount(text.AsSpan(0, hit.Index)),
                Encoding.UTF8.GetByteCount(text.AsSpan(0, hit.Index + hit.Length)))).ToArray();
        Assert.Equal(expected.Length, regex.Count(input));
        foreach (var floor in new int?[] { null, 0 })
        {
            var ranges = new List<(int Start, int End)>();
            foreach (var hit in regex.EnumerateMatches(input, floor))
                ranges.Add((hit.StartOffsetInBytes, hit.EndOffsetInBytes));
            Assert.Equal(expected, ranges);
        }
    }

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
        var plan = Utf8AnchoredLiteralClassifier.Classify(Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant).SemanticRegex.RuntimeTree);

        Assert.Equal(expectedKind, plan.Kind.ToString());
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
        var plan = Utf8AnchoredLiteralClassifier.Classify(Utf8FrontEnd.Analyze(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).SemanticRegex.RuntimeTree);

        Assert.Equal(expectedKind, plan.Kind.ToString());
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
        var plan = Utf8AnchoredLiteralClassifier.Classify(Utf8FrontEnd.Analyze(pattern, options).SemanticRegex.RuntimeTree);

        Assert.NotEqual(Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffix, plan.Kind);
        Assert.NotEqual(Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffixStrictEnd, plan.Kind);
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

            var isMatchRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.IsMatch(inputText), isMatchRegex.IsMatch(input));
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var matchRegex = new Utf8Regex(pattern, options: options);
            var expected = oracle.Match(inputText);
            var actual = matchRegex.Match(input);
            Assert.Equal(expected.Success, actual.Success);
            Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var countRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.Matches(inputText).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
            if (expected.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expected.Index)), actual.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(expected.Index, expected.Length)), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
            }
        }
    }

    public static IEnumerable<object?[]> NonAsciiBehaviorCases()
    {
        // No byte in an astral scalar can be mistaken for an ASCII suffix byte.
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
            var regex = new Utf8Regex(pattern, options: options);
            var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal(expected.Success, actual.Success);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Utf8MatchSequence.AssertCaptures(expected, actual, input, inputText);
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

            var regex = new Utf8Regex(pattern, options: options);
            var actualCount = 0;
            var enumerator = regex.EnumerateMatches(input);
            while (enumerator.MoveNext())
            {
                actualCount++;
                // Suffix matches end at the input end (or right before a single
                // trailing line feed for the lax `$`/`\Z` forms).
                var endInBytes = enumerator.Current.StartOffsetInBytes + (enumerator.Current.EndOffsetInBytes - enumerator.Current.StartOffsetInBytes);
                Assert.True(endInBytes == input.Length || (endInBytes == input.Length - 1 && input[input.Length - 1] == (byte)'\n'));
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
            foreach (var offset in new[] { 0, 1, 2 })
            {
                if (offset > inputText.Length)
                {
                    continue;
                }

                var expectedMatch = oracle.Match(inputText, offset);
                var regex = new Utf8Regex(pattern, options: options);
                var actualMatch = regex.Match(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), []);
                Assert.Equal(expectedMatch.Success, actualMatch.Success);
                Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
                var regex2 = new Utf8Regex(pattern, options: options);
                Assert.Equal(oracle.IsMatch(inputText, offset), regex2.Match(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), []).Success);
                Assert.True(regex2.Inspection.PreparedRegex.ScalarProgram is not null);
                var regex3 = new Utf8Regex(pattern, options: options);
                Assert.Equal(expectedMatch.Success, Utf8MatchSequence.First(regex3.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)))).Success);
                Assert.True(regex3.Inspection.PreparedRegex.ScalarProgram is not null);
                var regex4 = new Utf8Regex(pattern, options: options);
                var actualEnum = 0;
                var enumerator = regex4.EnumerateMatches(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)));
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
        var regex = new Utf8Regex(pattern, options: options);

        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        Assert.Equal(oracle.Match(inputText).Success, Utf8MatchSequence.First(regex.CreateMatchCursor(input)).Success);
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
