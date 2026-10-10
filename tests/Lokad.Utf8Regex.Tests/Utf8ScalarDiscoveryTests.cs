using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarDiscoveryTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void BooleanAndValueDiscoveryDoNotAllocateWholeSubjectMaps(bool compiled, bool value)
    {
        var regex = new Utf8Regex("(?s:.)", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀𐐀a", 20000)));
        if (value) Assert.Equal(4, regex.Match(input).LengthInBytes);
        else Assert.True(regex.IsMatch(input));
        var before = GC.GetAllocatedBytesForCurrentThread();
        if (value)
        {
            var match = regex.Match(input);
            Assert.Equal((0, 4, 0, 2), (match.IndexInBytes, match.LengthInBytes, match.IndexInUtf16, match.LengthInUtf16));
        }
        else Assert.True(regex.IsMatch(input));
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0L, 65536L);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DetailedResultsDecodeOnlyRequestedValues(bool compiled)
    {
        var regex = new Utf8Regex("(?s:.)", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀𐐀a", 20000)));
        Assert.Equal(4, regex.MatchDetailed(input).LengthInBytes);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var match = regex.MatchDetailed(input);
        // The current context retains its derived interoperability map; a whole
        // decoded subject is unnecessary. Allow map payload plus bounded scratch.
        var mapBytes = (100001L + (100001L + 31) / 32) * sizeof(int);
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0L, mapBytes + 65536);
        before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal("😀", match.GetValueString());
        Assert.Equal("😀", match.GetGroup(0).GetValueString());
        Assert.Equal("😀", match.GetGroup(0).GetCapture(0).GetValueString());
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0L, 4096L);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedDetailedDiscoveryNeedsNeitherMapNorDecodedSubject(bool compiled)
    {
        var regex = new Utf8Regex("z(?=\\z)", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀𐐀a", 20000)));
        using var source = new CancellationTokenSource();
        Assert.False(regex.MatchDetailed(input, source.Token).Success);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var match = regex.MatchDetailed(input, source.Token);
        Assert.False(match.Success);
        Assert.Equal(1, match.GroupCount);
        Assert.Equal(string.Empty, match.GetValueString());
        Assert.True(match.TryGetByteRange(out var start, out var length));
        Assert.Equal((0, 0), (start, length));
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0L, 65536L);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void DetailedAndEvaluatorHistoriesUseCompleteScalars(bool compiled, bool reverse)
    {
        var regex = new Utf8Regex("(?<word>(?<scalar>.)+)", RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : 0) | (reverse ? RegexOptions.RightToLeft : 0));
        var input = "é😀𐐀a"u8.ToArray();
        var match = regex.MatchDetailed(input);
        AssertHistory(match, reverse);
        var seen = 0;
        var actual = regex.Replace(input, reverse, (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref bool direction) =>
        {
            seen++;
            AssertHistory(context, direction);
            writer.Append("["u8);
            Assert.True(context.TryGetValueBytes(out var value));
            writer.Append(value);
            writer.Append("]"u8);
        });
        Assert.Equal(1, seen);
        Assert.Equal("[é😀𐐀a]", Encoding.UTF8.GetString(actual));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    private static void AssertHistory(Utf8MatchContext match, bool reverse)
    {
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal("é😀𐐀a", match.GetValueString());
        Assert.Equal("é😀𐐀a", match.GetGroup("word").GetValueString());
        var group = match.GetGroup("scalar");
        Assert.Equal(4, group.CaptureCount);
        (int Byte, int Length, int Unit, int Units, string Value)[] expected =
        [(0, 2, 0, 1, "é"), (2, 4, 1, 2, "😀"), (6, 4, 3, 2, "𐐀"), (10, 1, 5, 1, "a")];
        for (var index = 0; index < expected.Length; index++)
        {
            var row = expected[reverse ? expected.Length - 1 - index : index];
            var capture = group.GetCapture(index);
            Assert.True(capture.IsByteAligned);
            Assert.Equal((row.Byte, row.Length, row.Unit, row.Units),
                (capture.IndexInBytes, capture.LengthInBytes, capture.IndexInUtf16, capture.LengthInUtf16));
            Assert.Equal(row.Value, capture.GetValueString());
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void ExplicitStartsKeepOriginalLookbehindContext(bool compiled, bool reverse)
    {
        var regex = new Utf8Regex("(?<=x)(.)", RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : 0) | (reverse ? RegexOptions.RightToLeft : 0));
        var input = "x😀y"u8.ToArray();
        var start = reverse ? 3 : 1;
        using var source = new CancellationTokenSource();
        foreach (var live in new[] { false, true })
        {
            var token = live ? source.Token : default;
            Assert.True(regex.IsMatchFromUtf16Offset(input, start, token));
            var value = regex.MatchFromUtf16Offset(input, start, token);
            Assert.Equal((1, 4, 1, 2), (value.IndexInBytes, value.LengthInBytes, value.IndexInUtf16, value.LengthInUtf16));
            var detail = regex.MatchDetailedFromUtf16Offset(input, start, token);
            Assert.Equal((1, 4), (detail.IndexInBytes, detail.LengthInBytes));
            Assert.Equal("😀", detail.GetGroup(1).GetValueString());
            var cursor = regex.EnumerateMatchesFromUtf16Offset(input, start, token);
            Assert.True(cursor.MoveNext());
            Assert.Equal((1, 4), (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
            Assert.False(cursor.MoveNext());
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
