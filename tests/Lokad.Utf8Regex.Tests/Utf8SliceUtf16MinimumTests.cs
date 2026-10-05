using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceUtf16MinimumTests
{
    public static IEnumerable<object[]> GreedyMinimums()
    {
        yield return [@"([^x]{2,})", "\U0001f600yy", "<$1>"];
        yield return [@"(?i)([^x]{2,})", "\U0001f600yy", "<$1>"];
        yield return [@"(?i)([^k]{2,})", "\U0001f600\u212ay\U0001f600", "<$1>"];
        yield return [@"([^x]{2,})", "\U0001f600x\U000104a0x", "<$1>"];
        yield return [@"([^x]{3,})", "\U0001f600xy\U0001f600x", "<$1>"];
        yield return [@"([^x]{4,})", "\U0001f600\U000104a0x\U0001f600y", "<$1>"];
        yield return [@"(.{2,})", "\U0001f600\nx\n\U000104a0", "<$1>"];
        yield return [@"(.{3,})", "\U0001f600\nx\U0001f600\n", "<$1>"];
        yield return [@"(?s)(.{3,})", "\U0001f600\nx", "<$1>"];
        yield return [@"^(?<run>[^x]{2,})$", "\U0001f600", "<${run}>"];
        yield return [@"^(?<run>[^x]{3,})$", "\U0001f600", "<${run}>"];
        yield return [@"^(a[^x]{2,}b)$", "a\U0001f600b", "<$1>"];
        yield return [@"^(a[^x]{3,}b)$", "a\U0001f600b", "<$1>"];
        yield return [@"a[^x]{3,}b", "xa\U0001f600ybx a\U0001f600b", "Q"];
        yield return [@"(?i)(a[^x]{2,}b)", "xA\U0001f600B", "<$1>"];
        yield return [@"(a[^x]{2,}b)", "xa\U0001f600b", "<$1>"];
        yield return [@"(a[^x]{2,}b)", "xa\U0001f600yb", "<$1>"];
        yield return [@"a[^x]{2,}b", "xa\U0001f600b", "Q"];
        yield return [@"a.{2,}b", "xa\U0001f600b", "Q"];
        yield return [@"a[^\n]{2,}b", "xa\U0001f600b", "Q"];
        yield return [@"([^x]{2,})", "\u00e9yx\u03bbx", "<$1>"];
        yield return [@"([^x]{2,})", "yxyyx", "<$1>"];
        yield return [@"([^x]{2,})", "", "<$1>"];
        yield return [@"a\D{2,}", "xa\U0001f600", "Q"];
        yield return [@"a\D{2,}", "xa\U0001d7ce\U0001d7ce1a\U0001f6001", "Q"];
        yield return [@"a\D{3,}", "xa\U0001f6001", "Q"];
        yield return [@"(a\D{2,}b)", "xa\U0001f600b", "<$1>"];
        yield return [@"a\D{2,}b", "xa\U0001d7ce\U0001d7ceb", "Q"];
        yield return [@"a\D{2,}b", "xa\U0001f600c", "Q"];
        yield return [@"(\D{2,})", "\U0001d7ce\U0001d7ce1\U00010400a2", "<$1>"];
        yield return [@"(\D{3,})", "\U0001f6001y\U000104a02", "<$1>"];
        yield return [@"(\W{2,})", "\U00010400\U000104001a\U0001f600", "<$1>"];
        yield return [@"(\S{2,})", "\U00010400\U0001f600 \u00a0\U000104a0", "<$1>"];
        yield return [@"a\S{2,}b", "xa\U0001f600b", "Q"];
        yield return [@"(\P{Nd}{2,})", "\U000104a0\U000104a01\U0001f6002", "<$1>"];
        yield return [@"a\P{Nd}{2,}b", "xa\U000104a0b", "Q"];
        yield return [@"a\P{Lu}{2,}b", "xa\U00010400b", "Q"];
        yield return [@"(?i)(\P{Lu}{3,})", "\U00010400\U0001f600a", "<$1>"];
        yield return [@"(\p{L}{2,})", "xa\U0001f600bc", "<$1>"];
        yield return [@"(\p{Nd}{2,})", "x\U000104a0y\u0661\u0662", "<$1>"];
        yield return [@"([\s\S]{2,})", "\U00010400\n\U000104a0", "<$1>"];
        yield return [@"a[\s\S]{2,}b", "xa\U0001f600b", "Q"];
        yield return [@"([^\d]{2,})", "\U000104a01\U0001f600a", "<$1>"];
        yield return [@"([\D\s]{3,})", "\U0001f600 1a\U000104a0", "<$1>"];
        yield return [@"([a\p{L}]{2,})", "ab\U00010400cd", "<$1>"];
        yield return [@"([\p{Cs}]{2,})", "\U00010400a\U0001f600", "<$1>"];
        yield return [@"([^\p{Cs}]{2,})", "ab\U00010400cd", "<$1>"];
        // Exact subjects migrated from the former verifier/shaping route pins.
        yield return [@"(?i)a\p{L}{2,}b", "xa\U00010400b", "Q"];
        yield return [@"\D{2,}b", "x\U0001f600b", "Q"];
        yield return [@"(?i)A\D{2,}B", "xa\U0001f600b", "Q"];
        yield return [@"a\p{L}{2,}b", "xa\U00010400b", "Q"];
        yield return [@"a\p{Nd}{2,}b", "xa\U0001d7ce\U0001d7ceb", "Q"];
        yield return [@"a\D{2,}b", "xa\U0001f600b", "Q"];
        yield return [@"a\P{Nd}{2,}b", "xa\U0001f600b", "Q"];
        yield return [@"a\W{2,}b", "xa\U0001f600b", "Q"];
        yield return [@"a\P{L}{2,}b", "xa\U0001f600b", "Q"];
        yield return [@"(?i)a\W{2,}b", "xa\U0001f600b", "Q"];
        yield return [@"\p{Nd}{2,}", "x\U000104a0y", "Q"];
        yield return [@"\p{L}{2,}", "xa\U0001f600b", "Q"];
        yield return [@"(\d{2,})", "x\U000104a0\u0661\u0662y", "<$1>"];
        yield return [@"(\w{2,})", "a\U00010400bc\U0001f600", "<$1>"];
        yield return [@"(\s{2,})", "x\U0001f600 \u00a0y", "<$1>"];
    }

    [Theory]
    [MemberData(nameof(GreedyMinimums))]
    public void GreedyMinimumCountsUtf16UnitsAcrossAllOperations(string pattern, string text, string template)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            var expected = oracle.Match(text);
            var actual = regex.Match(input);
            Assert.Equal(expected.Success, actual.Success);
            if (expected.Success)
            {
                Assert.Equal((expected.Index, expected.Length), (actual.IndexInUtf16, actual.LengthInUtf16));
            }

            var detailed = regex.MatchDetailed(input);
            Assert.Equal(expected.Success, detailed.Success);
            if (expected.Success)
            {
                Assert.Equal(expected.Groups.Count, detailed.GroupCount);
                for (var group = 0; group < expected.Groups.Count; group++)
                {
                    var expectedGroup = expected.Groups[group];
                    var actualGroup = detailed.GetGroup(group);
                    Assert.Equal(expectedGroup.Success, actualGroup.Success);
                    Assert.Equal(expectedGroup.Value, actualGroup.GetValueString());
                    Assert.Equal((expectedGroup.Index, expectedGroup.Length),
                        (actualGroup.IndexInUtf16, actualGroup.LengthInUtf16));
                }
            }

            var positions = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input))
            {
                Assert.True(match.IsByteAligned);
                positions.Add((match.IndexInUtf16, match.LengthInUtf16));
            }
            Assert.Equal(oracle.Matches(text).Select(m => (m.Index, m.Length)), positions);
            foreach (var count in new[] { 2, 3, int.MaxValue })
            {
                var expectedSplits = new List<(string, int, int)>();
                foreach (var range in oracle.EnumerateSplits(text, count))
                {
                    var (start, length) = range.GetOffsetAndLength(text.Length);
                    expectedSplits.Add((text.Substring(start, length), start, length));
                }
                var actualSplits = new List<(string, int, int)>();
                foreach (var split in regex.EnumerateSplits(input, count))
                {
                    actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
                }
                Assert.Equal(expectedSplits, actualSplits);
            }

            foreach (var replacement in new[] { "Q", "", template })
            {
                var expectedReplacement = oracle.Replace(text, replacement);
                var expectedBytes = Encoding.UTF8.GetBytes(expectedReplacement);
                Assert.Equal(expectedReplacement, regex.ReplaceToString(input, replacement));
                Assert.Equal(expectedBytes, regex.Replace(input, replacement));
                Assert.Equal(expectedBytes, regex.Replace(input, Encoding.UTF8.GetBytes(replacement)));
                var destination = new byte[expectedBytes.Length];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, replacement, destination, out var written));
                Assert.Equal(expectedBytes.Length, written);
                Assert.Equal(expectedBytes, destination);
            }
            AssertOwned(regex);
        }
    }

    [Theory]
    [InlineData(@"[^x]{2,}")]
    [InlineData(@"[^x]{3,}")]
    [InlineData(@".{2,}")]
    [InlineData(@".{3,}")]
    [InlineData(@"\D{2,}")]
    [InlineData(@"\D{3,}")]
    [InlineData(@"\W{2,}")]
    [InlineData(@"\S{3,}")]
    [InlineData(@"\P{Nd}{2,}")]
    [InlineData(@"[\s\S]{3,}")]
    [InlineData(@"[\p{Cs}]{2,}")]
    public void OffsetEnumerationCountsOnlyTheRemainingTrailUnit(string pattern)
    {
        const string text = "\U0001f600yx\U000104a0x\U0001f600\U0001f600";
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options);
            for (var start = 0; start <= text.Length; start++)
            {
                var expected = new List<(int, int)>();
                foreach (var match in oracle.EnumerateMatches(text, start))
                {
                    expected.Add((match.Index, match.Length));
                }
                var actual = new List<(int, int)>();
                foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, start))
                {
                    actual.Add((match.IndexInUtf16, match.LengthInUtf16));
                }
                Assert.Equal(expected, actual);
            }
            AssertOwned(regex);
        }
    }

    [Theory]
    [InlineData(@"((.){2,})", "\U0001f600", "<$2>")]
    [InlineData(@"(.{2,}?)", "a\U0001f600b", "<$1>")]
    [InlineData(@"(.{2,3})", "\U0001f600\U0001f600", "<$1>")]
    [InlineData(@"(\D{2,})", "\U0001d7ce\U0001d7ce", "<$1>")]
    public void HalfCapturesLazyStopsFiniteBoundsAndUnicodeAtomsKeepTheirUnitBehavior(string pattern, string text, string template)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Replace(text, template), regex.ReplaceToString(input, template));
        }
    }

    [Theory]
    [InlineData(@"([a\p{IsHighSurrogates}]{2,})")]
    [InlineData(@"([a\p{IsLowSurrogates}]{2,})")]
    public void SplitHalfCategoryMinimumsPreserveUnalignedUnitSpans(string pattern)
    {
        const string text = "a\U0001f600a\U0001f600a";
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            var expected = oracle.Match(text);
            var actual = regex.MatchDetailed(input);
            Assert.Equal(expected.Groups[1].Value, actual.GetGroup(1).GetValueString());
            Assert.Equal((expected.Index, expected.Length), (actual.IndexInUtf16, actual.LengthInUtf16));
            var positions = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input))
            {
                Assert.False(match.IsByteAligned);
                positions.Add((match.IndexInUtf16, match.LengthInUtf16));
            }
            Assert.Equal(oracle.Matches(text).Select(m => (m.Index, m.Length)), positions);
            Assert.Equal(oracle.Replace(text, "<$1>"), regex.ReplaceToString(input, "<$1>"));
        }
    }

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
}
