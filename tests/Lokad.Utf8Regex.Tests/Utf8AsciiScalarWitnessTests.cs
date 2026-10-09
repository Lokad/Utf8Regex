using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiScalarWitnessTests
{
    private const string Text = "a\U0001f600\U00010400\U000104a0\U0001d7ceb";

    public static IEnumerable<object[]> Cases()
    {
        // These primary byte ranges are independent of the BMP projection.
        (string Pattern, int[] Ranges)[] cases =
        [
            (".", [0, 1, 1, 4, 5, 4, 9, 4, 13, 4, 17, 1]),
            (".+", [0, 18]),
            ("(?<x>.)+", [0, 18]),
            ("[^x]+", [0, 18]),
            ("", [0, 0, 1, 0, 5, 0, 9, 0, 13, 0, 17, 0, 18, 0]),
            ("(?=.)", [0, 0, 1, 0, 5, 0, 9, 0, 13, 0, 17, 0]),
            ("(?<=.)", [1, 0, 5, 0, 9, 0, 13, 0, 17, 0, 18, 0]),
            (@"\w+", [0, 1, 5, 13]),
            (@"\W+", [1, 4]),
            (@"\d+", [9, 8]),
            (@"\D+", [0, 9, 17, 1]),
            (@"\s+", []),
            (@"\S+", [0, 18]),
            (@"\p{L}", [0, 1, 5, 4, 17, 1]),
            (@"\p{Lu}", [5, 4]),
            (@"\p{Nd}+", [9, 8]),
            (@"\p{Cs}", []),
            (@"\P{Cs}+", [0, 18]),
            (@"\p{IsHighSurrogates}", []),
            (@"\p{IsGreek}", []),
            (@"\P{IsGreek}+", [0, 18]),
            (@"\p{IsBasicLatin}+", [0, 1, 17, 1]),
            (@"[^\p{IsBasicLatin}]+", [1, 16]),
            (@"[0-9]+", []),
            (@"[a-z]+", [0, 1, 17, 1]),
            (@"[\p{Lu}\p{Nd}]+", [5, 12]),
            (@"\b", [0, 0, 1, 0, 5, 0, 18, 0]),
            (@"\B", [9, 0, 13, 0, 17, 0]),
            ("😀", [1, 4]),
            ("(?<=😀).", [5, 4]),
            (".(?=😀)", [0, 1]),
            (@"a\W{2,}", []),
            (@"a\W{1}", [0, 5]),
            (@"(?>\w+)", [0, 1, 5, 13]),
            (@"(\d)\1", []),
        ];
        foreach (var (pattern, ranges) in cases)
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
                     RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            yield return [pattern, options, ranges];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeclaredByteRangesQualifyFixtureProjection(string pattern, RegexOptions options, int[] ranges)
    {
        var subject = new Utf8AsciiScalarOperationAssertions.Subject(Text);
        var expected = Enumerable.Range(0, ranges.Length / 2)
            .Select(index => (Index: ranges[index * 2], Length: ranges[index * 2 + 1])).ToArray();
        if (options.HasFlag(RegexOptions.RightToLeft)) Array.Reverse(expected);
        var comparator = Utf8AsciiScalarOperationAssertions.Comparator(pattern, options);
        Assert.Equal(expected, comparator.Matches(subject.Projected).Cast<Match>()
            .Select(match => subject.Bytes(match.Index, match.Length)));
        var regex = new Utf8Regex(pattern, options);
        var cursor = regex.EnumerateMatches(subject.Input);
        foreach (var range in expected)
        {
            Assert.True(cursor.MoveNext());
            Assert.True(cursor.Current.TryGetByteRange(out var index, out var length));
            Assert.Equal(range, (index, length));
        }
        Assert.False(cursor.MoveNext());
        Assert.Equal(expected.Length, regex.Count(subject.Input));
        Utf8AsciiScalarOperationAssertions.Owned(regex);
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, Text, "<$&>");
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.RightToLeft | RegexOptions.Compiled)]
    public void RepeatedCaptureHistoryUsesWholeScalars(RegexOptions options)
    {
        var subject = new Utf8AsciiScalarOperationAssertions.Subject(Text);
        var regex = new Utf8Regex("(?<x>.)+", options);
        var match = regex.MatchDetailed(subject.Input);
        Assert.True(match.Success);
        Assert.Equal((0, 18), (match.IndexInBytes, match.LengthInBytes));
        var expected = new[] { (0, 1), (1, 4), (5, 4), (9, 4), (13, 4), (17, 1) };
        if (options.HasFlag(RegexOptions.RightToLeft)) Array.Reverse(expected);
        var group = match.GetGroup(regex.GroupNumberFromName("x"));
        Assert.Equal(expected.Length, group.CaptureCount);
        for (var index = 0; index < expected.Length; index++)
        {
            var capture = group.GetCapture(index);
            Assert.True(capture.IsByteAligned);
            Assert.Equal(expected[index], (capture.IndexInBytes, capture.LengthInBytes));
        }
        Utf8AsciiScalarOperationAssertions.Owned(regex);
    }
}
