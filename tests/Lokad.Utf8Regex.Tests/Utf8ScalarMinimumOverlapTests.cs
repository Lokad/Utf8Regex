using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// Broad minimum/capture/output grammar checks use BMP subjects where scalar
// and BCL units overlap. Utf8ScalarSubjectTests supplies direct plane-wide witnesses.
public sealed class Utf8ScalarMinimumOverlapTests
{
    public static IEnumerable<object[]> GreedyMinimums()
    {
        yield return [@"([^x]{2,})", "\u2603yy", "<$1>"];
        yield return [@"(?i)([^x]{2,})", "\u2603yy", "<$1>"];
        yield return [@"(?i)([^k]{2,})", "\u2603\u212ay\u2603", "<$1>"];
        yield return [@"([^x]{2,})", "\u2603x\u0661x", "<$1>"];
        yield return [@"([^x]{3,})", "\u2603xy\u2603x", "<$1>"];
        yield return [@"([^x]{4,})", "\u2603\u0661x\u2603y", "<$1>"];
        yield return [@"(.{2,})", "\u2603\nx\n\u0661", "<$1>"];
        yield return [@"(.{3,})", "\u2603\nx\u2603\n", "<$1>"];
        yield return [@"(?s)(.{3,})", "\u2603\nx", "<$1>"];
        yield return [@"^(?<run>[^x]{2,})$", "\u2603", "<${run}>"];
        yield return [@"^(?<run>[^x]{3,})$", "\u2603", "<${run}>"];
        yield return [@"^(a[^x]{2,}b)$", "a\u2603b", "<$1>"];
        yield return [@"^(a[^x]{3,}b)$", "a\u2603b", "<$1>"];
        yield return [@"a[^x]{3,}b", "xa\u2603ybx a\u2603b", "Q"];
        yield return [@"(?i)(a[^x]{2,}b)", "xA\u2603B", "<$1>"];
        yield return [@"(a[^x]{2,}b)", "xa\u2603b", "<$1>"];
        yield return [@"(a[^x]{2,}b)", "xa\u2603yb", "<$1>"];
        yield return [@"a[^x]{2,}b", "xa\u2603b", "Q"];
        yield return [@"a.{2,}b", "xa\u2603b", "Q"];
        yield return [@"a[^\n]{2,}b", "xa\u2603b", "Q"];
        yield return [@"([^x]{2,})", "\u00e9yx\u03bbx", "<$1>"];
        yield return [@"([^x]{2,})", "yxyyx", "<$1>"];
        yield return [@"([^x]{2,})", "", "<$1>"];
        yield return [@"a\D{2,}", "xa\u2603", "Q"];
        yield return [@"a\D{2,}", "xa\u0660\u06601a\u26031", "Q"];
        yield return [@"a\D{3,}", "xa\u26031", "Q"];
        yield return [@"(a\D{2,}b)", "xa\u2603b", "<$1>"];
        yield return [@"a\D{2,}b", "xa\u0660\u0660b", "Q"];
        yield return [@"a\D{2,}b", "xa\u2603c", "Q"];
        yield return [@"(\D{2,})", "\u0660\u06601\u00c0a2", "<$1>"];
        yield return [@"(\D{3,})", "\u26031y\u06612", "<$1>"];
        yield return [@"(\W{2,})", "\u00c0\u00c01a\u2603", "<$1>"];
        yield return [@"(\S{2,})", "\u00c0\u2603 \u00a0\u0661", "<$1>"];
        yield return [@"a\S{2,}b", "xa\u2603b", "Q"];
        yield return [@"(\P{Nd}{2,})", "\u0661\u06611\u26032", "<$1>"];
        yield return [@"a\P{Nd}{2,}b", "xa\u0661b", "Q"];
        yield return [@"a\P{Lu}{2,}b", "xa\u00c0b", "Q"];
        yield return [@"(?i)(\P{Lu}{3,})", "\u00c0\u2603a", "<$1>"];
        yield return [@"(\p{L}{2,})", "xa\u2603bc", "<$1>"];
        yield return [@"(\p{Nd}{2,})", "x\u0661y\u0661\u0662", "<$1>"];
        yield return [@"([\s\S]{2,})", "\u00c0\n\u0661", "<$1>"];
        yield return [@"a[\s\S]{2,}b", "xa\u2603b", "Q"];
        yield return [@"([^\d]{2,})", "\u06611\u2603a", "<$1>"];
        yield return [@"([\D\s]{3,})", "\u2603 1a\u0661", "<$1>"];
        yield return [@"([a\p{L}]{2,})", "ab\u00c0cd", "<$1>"];
        yield return [@"([\p{Cs}]{2,})", "\u00c0a\u2603", "<$1>"];
        yield return [@"([^\p{Cs}]{2,})", "ab\u00c0cd", "<$1>"];
        // Exact subjects migrated from the former verifier/shaping route pins.
        yield return [@"(?i)a\p{L}{2,}b", "xa\u00c0b", "Q"];
        yield return [@"\D{2,}b", "x\u2603b", "Q"];
        yield return [@"(?i)A\D{2,}B", "xa\u2603b", "Q"];
        yield return [@"a\p{L}{2,}b", "xa\u00c0b", "Q"];
        yield return [@"a\p{Nd}{2,}b", "xa\u0660\u0660b", "Q"];
        yield return [@"a\D{2,}b", "xa\u2603b", "Q"];
        yield return [@"a\P{Nd}{2,}b", "xa\u2603b", "Q"];
        yield return [@"a\W{2,}b", "xa\u2603b", "Q"];
        yield return [@"a\P{L}{2,}b", "xa\u2603b", "Q"];
        yield return [@"(?i)a\W{2,}b", "xa\u2603b", "Q"];
        yield return [@"\p{Nd}{2,}", "x\u0661y", "Q"];
        yield return [@"\p{L}{2,}", "xa\u2603b", "Q"];
        yield return [@"(\d{2,})", "x\u0661\u0661\u0662y", "<$1>"];
        yield return [@"(\w{2,})", "a\u00c0bc\u2603", "<$1>"];
        yield return [@"(\s{2,})", "x\u2603 \u00a0y", "<$1>"];
    }

    [Theory]
    [MemberData(nameof(GreedyMinimums))]
    public void GreedyMinimumKeepsAllOperationsOnBmpOverlap(string pattern, string text, string template)
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
    public void OffsetEnumerationCountsRemainingBmpScalars(string pattern)
    {
        const string text = "\u2603yx\u0661x\u2603\u2603";
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
    [InlineData(@"((.){2,})", "\u2603", "<$2>")]
    [InlineData(@"(.{2,}?)", "a\u2603b", "<$1>")]
    [InlineData(@"(.{2,3})", "\u2603\u2603", "<$1>")]
    [InlineData(@"(\D{2,})", "\u0660\u0660", "<$1>")]
    public void CapturesLazyStopsAndFiniteBoundsKeepBmpOverlap(string pattern, string text, string template)
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
    public void SurrogateRangeMembersCannotMatchSupplementaryScalars(string pattern)
    {
        const string text = "a\U0001f600a\U0001f600a";
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options);
            Assert.False(regex.IsMatch(input));
            Assert.Equal(0, regex.Count(input));
            Assert.False(regex.MatchDetailed(input).Success);
            Assert.False(regex.EnumerateMatches(input).GetEnumerator().MoveNext());
            Assert.Equal(text, regex.ReplaceToString(input, "<$1>"));
            AssertOwned(regex);
        }
    }

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
}
