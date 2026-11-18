using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProjectedClassScalarLoopTests
{
    public static IEnumerable<object[]> ProjectionCases()
    {
        foreach (var category in new[] { @"\W", @"\D" })
        foreach (var shape in new[] { 0, 1, 2 })
        foreach (var compiled in new[] { false, true }) yield return [category, shape, compiled];
    }

    private static string Pattern(string category, int shape, bool capture)
    {
        var body = category + (shape == 0 ? "{0,}" : "{2,}");
        if (capture) body = "(?<x>" + body + ")";
        return shape == 2 ? "^a" + body + "7$" : "^" + body + "$";
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OverlappingSuffixStillDeclinesTheDeterministicProjection(bool compiled)
    {
        const string pattern = @"^a\D{2,}b$";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options: options);
        Assert.True(regex.IsMatch("a??b"u8));
        Assert.False(regex.IsMatch("a😀b"u8));
        Assert.Equal(10, (regex.Match("a😀😀b"u8).EndOffsetInBytes - regex.Match("a😀😀b"u8).StartOffsetInBytes));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var row in ProjectionCases())
        foreach (var capture in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [.. row, capture, rtl];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicLoopMinimumCountsCompleteScalarsAcrossOperations(string category, int shape, bool compiled, bool capture, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex(Pattern(category, shape, capture), options: options);
        foreach (var body in new[] { "", "?", "??", "😀", "😀😀", "?😀", "😀?" })
        {
            var text = shape == 2 ? "a" + body + "7" : body;
            var input = Encoding.UTF8.GetBytes(text);
            var expected = shape == 0 || body.EnumerateRunes().Count() >= 2;
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.Equal(expected ? 1 : 0, regex.Count(input));
            var match = regex.Match(input);
            Assert.Equal(expected, match.Success);
            var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal(expected, detailed.Success);
            if (expected)
            {
                Assert.Equal((0, input.Length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
                Assert.Equal((0, input.Length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
                if (capture)
                {
                    Assert.True(detailed.TryGetGroup("x", out var group));
                    Assert.True(group.Success);
                    Assert.Equal((shape == 2 ? 1 : 0, Encoding.UTF8.GetByteCount(body)), (group.StartOffsetInBytes, (group.EndOffsetInBytes - group.StartOffsetInBytes)));
                    Assert.Equal(body, System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
                }
            }
            var cursor = regex.EnumerateMatches(input);
            Assert.Equal(expected, cursor.MoveNext());
            if (expected)
            {
                Assert.Equal((0, input.Length), (cursor.Current.StartOffsetInBytes, (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes)));
                Assert.False(cursor.MoveNext());
            }
            Assert.Equal(expected ? "<" + (capture ? body : text) + ">" : text,
                Encoding.UTF8.GetString(regex.Replace(input, capture ? "<${x}>" : "<$&>")));
            var splits = new List<string>();
            foreach (var split in regex.EnumerateSplits(input)) splits.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
            Assert.Equal(expected ? new[] { "", "" } : new[] { text }, splits);
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
