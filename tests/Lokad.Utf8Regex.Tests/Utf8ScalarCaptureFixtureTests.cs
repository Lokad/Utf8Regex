using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarCaptureFixtureTests
{
    private const string Text = "a\U0001f600\U00010400\U00010428b";

    public static IEnumerable<object[]> Cases()
    {
        (string Pattern, int[] Ranges)[] cases =
        [
            (".", [0, 1, 1, 4, 5, 4, 9, 4, 13, 1]),
            ("(?<12>.)+", [0, 14]),
            ("[^x]+", [0, 14]),
            ("", [0, 0, 1, 0, 5, 0, 9, 0, 13, 0, 14, 0]),
            ("(?=.)", [0, 0, 1, 0, 5, 0, 9, 0, 13, 0]),
            ("(?<=.)", [1, 0, 5, 0, 9, 0, 13, 0, 14, 0]),
            (@"\p{L}", [0, 1, 5, 4, 9, 4, 13, 1]),
            (@"\p{Lu}", [5, 4]),
            (@"\p{Ll}", [0, 1, 9, 4, 13, 1]),
            (@"\p{N}", []),
            (@"\p{Cs}", []),
            (@"\P{Cs}+", [0, 14]),
            (@"\p{IsBasicLatin}+", [0, 1, 13, 1]),
            (@"\w+", [0, 1, 5, 9]),
            (@"\W+", [1, 4]),
            (@"\s+", []),
            (@"\S+", [0, 14]),
            (@"\d+", []),
            (@"\D+", [0, 14]),
            (@"\b", [0, 0, 1, 0, 5, 0, 14, 0]),
            (@"\B", [9, 0, 13, 0]),
            (@"[\uD800-\uDFFF]+", []),
            (@"[\u0000-\uFFFF]+", [0, 1, 13, 1]),
            (@"(?i:[\u0080-\uFFFF]+)", []),
            (@"[\u0000-\uFFFF-[\uD800-\uDFFF]]+", [0, 1, 13, 1]),
            ("😀", [1, 4]),
            ("(?<12>😀)+", [1, 4]),
            ("(?<x>\ud83d|\ude00){0,3}?b", [13, 1]),
            ("\ud83d", []),
            ("\ude00", []),
            (@"\W{2,}", []),
            (@"(?i:[a-z])", [0, 1, 13, 1]),
        ];
        for (var patternId = 0; patternId < cases.Length; patternId++)
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
                     RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            yield return [patternId, cases[patternId].Pattern, options | RegexOptions.CultureInvariant, cases[patternId].Ranges];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExplicitByteRangesQualifyScalarComparator(int patternId, string pattern, RegexOptions options, int[] ranges)
    {
        var subject = new Utf8ScalarCaptureFixture.Subject(Text);
        var expected = Enumerable.Range(0, ranges.Length / 2)
            .Select(index => (ranges[index * 2], ranges[index * 2 + 1])).ToArray();
        if (options.HasFlag(RegexOptions.RightToLeft)) Array.Reverse(expected);
        var original = new Regex(pattern, options & ~RegexOptions.Compiled);
        var oracle = Utf8ScalarCaptureFixture.Project(original);
        Assert.Equal(expected, oracle.Matches(subject.Projected).Cast<Match>()
            .Select(match => subject.ByteRange(match.Index, match.Length)));
        var regex = new Utf8Regex(pattern, options);
        var cursor = regex.EnumerateMatches(subject.Input);
        foreach (var range in expected)
        {
            Assert.True(cursor.MoveNext());
            Assert.True(cursor.Current.TryGetByteRange(out var index, out var length));
            Assert.Equal(range, (index, length));
        }
        Assert.False(cursor.MoveNext(), $"Unexpected extra match for witness {patternId}.");
        Assert.Equal(expected.Length, regex.Count(subject.Input));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, original, [Text]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SparseCaptureHistoriesUseCompleteScalarRanges(bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex("(?<12>.)+", options);
        var subject = new Utf8ScalarCaptureFixture.Subject(Text);
        var match = regex.MatchDetailed(subject.Input);
        Assert.Equal((0, 14), (match.IndexInBytes, match.LengthInBytes));
        Assert.Equal(new[] { 0, 12 }, regex.GetGroupNumbers());
        var group = match.GetGroup(12);
        var expected = new[] { (0, 1), (1, 4), (5, 4), (9, 4), (13, 1) };
        if (rtl) Array.Reverse(expected);
        Assert.Equal(expected.Length, group.CaptureCount);
        for (var index = 0; index < expected.Length; index++)
        {
            var capture = group.GetCapture(index);
            Assert.True(capture.IsByteAligned);
            Assert.Equal(expected[index], (capture.IndexInBytes, capture.LengthInBytes));
        }
        Assert.False(match.GetGroup(1).Success);
        Assert.False(match.TryGetGroup(1, out _));
        Assert.True(match.TryGetGroup(12, out _));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DeseretCaseFoldBackreferenceRetainsScalarIdentity(bool compiled, bool rtl)
    {
        var pattern = rtl ? @"(?i:\k<x>(?<x>.))" : @"(?i:(?<x>.)\k<x>)";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options);
        var subject = new Utf8ScalarCaptureFixture.Subject(Text);
        var match = regex.MatchDetailed(subject.Input);
        Assert.True(match.Success);
        Assert.Equal((5, 8), (match.IndexInBytes, match.LengthInBytes));
        Assert.Equal((rtl ? 9 : 5, 4), (match.GetGroup(1).IndexInBytes, match.GetGroup(1).LengthInBytes));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, options & ~RegexOptions.Compiled), [Text]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExplicitPairComparatorRetainsLookbehindCapture(bool compiled, bool rtl)
    {
        const string pattern = "(?<=.(.))x";
        const string scalar = @"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\uD800-\uDFFF\n])";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options);
        var subject = new Utf8ScalarCaptureFixture.Subject("😀\U00010400x");
        var match = regex.MatchDetailed(subject.Input);
        Assert.True(match.Success);
        Assert.Equal((8, 1), (match.IndexInBytes, match.LengthInBytes));
        Assert.Equal((4, 4), (match.GetGroup(1).IndexInBytes, match.GetGroup(1).LengthInBytes));
        var comparator = new Regex("(?<=" + scalar + "(" + scalar + "))x", options & ~RegexOptions.Compiled);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, comparator, [subject.Text]);
    }
}
