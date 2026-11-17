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
        if (value) Assert.Equal(4, regex.Match(input, 0, []).EndOffsetInBytes);
        else Assert.True(regex.IsMatch(input));
        var before = GC.GetAllocatedBytesForCurrentThread();
        if (value)
        {
            var match = regex.Match(input, 0, []);
            Assert.Equal((0, 4), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        }
        else Assert.True(regex.IsMatch(input));
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0L, 65536L);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CaptureCursorBorrowsSelectedValuesWithoutSubjectMaps(bool compiled)
    {
        var regex = new Utf8Regex("(?s:.)", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀𐐀a", 20000)));
        Assert.Equal(4, Utf8MatchSequence.First(regex.CreateMatchCursor(input)).Value.EndOffsetInBytes);
        var before = GC.GetAllocatedBytesForCurrentThread();
        // First disposes the real cursor; retained ranges still borrow the input.
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0L, 65536L);
        before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal("😀"u8.ToArray(), match.GetValueMemory().ToArray());
        Assert.Equal("😀"u8.ToArray(), match.GetGroupMemory(0).ToArray());
        var capture = Assert.Single(match.GetCaptures(0).ToArray());
        Assert.Equal((0, 4), (capture.StartOffsetInBytes, capture.EndOffsetInBytes));
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0L, 4096L);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedCaptureDiscoveryNeedsNeitherMapNorDecodedSubject(bool compiled)
    {
        var regex = new Utf8Regex("z(?=\\z)", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀𐐀a", 20000)));
        using var source = new CancellationTokenSource();
        Assert.False(Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: source.Token)).Success);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: source.Token));
        Assert.False(match.Success);
        Assert.Empty(match.GroupNumbers.ToArray());
        Assert.Empty(match.GetValueMemory().ToArray());
        Assert.Equal((0, 0), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0L, 65536L);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void RetainedCursorHistoriesUseCompleteScalars(bool compiled, bool reverse)
    {
        var regex = new Utf8Regex("(?<word>(?<scalar>.)+)", RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : 0) | (reverse ? RegexOptions.RightToLeft : 0));
        var input = "é😀𐐀a"u8.ToArray();
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        AssertHistory(match, regex.GroupNumberFromName("scalar"), input, reverse);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    private static void AssertHistory(Utf8RangeMatch match, int scalarNumber, byte[] input, bool reverse)
    {
        Assert.True(match.Success);
        Assert.Equal(input, match.GetValueMemory().ToArray());
        Assert.True(match.TryGetGroup("word", out var word));
        Assert.Equal((0, input.Length), (word.StartOffsetInBytes, word.EndOffsetInBytes));
        var captures = match.GetCaptures(scalarNumber);
        Assert.Equal(4, captures.Length);
        (int Byte, int Length, string Value)[] expected =
        [(0, 2, "é"), (2, 4, "😀"), (6, 4, "𐐀"), (10, 1, "a")];
        for (var index = 0; index < expected.Length; index++)
        {
            var row = expected[reverse ? expected.Length - 1 - index : index];
            var capture = captures[index];
            Assert.True(capture.Success);
            Assert.Equal((row.Byte, row.Byte + row.Length),
                (capture.StartOffsetInBytes, capture.EndOffsetInBytes));
            Assert.Equal(Encoding.UTF8.GetBytes(row.Value),
                input.AsSpan(capture.StartOffsetInBytes, capture.EndOffsetInBytes - capture.StartOffsetInBytes).ToArray());
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
        var start = reverse ? 5 : 1;
        using var source = new CancellationTokenSource();
        foreach (var live in new[] { false, true })
        {
            var token = live ? source.Token : default;
            var value = regex.Match(input, start, [], token);
            Assert.True(value.Success);
            Assert.Equal((1, 4), (value.StartOffsetInBytes, value.EndOffsetInBytes - value.StartOffsetInBytes));
            using var captureCursor = regex.CreateMatchCursor(input, start, token);
            Assert.True(captureCursor.MoveNext(out var detail));
            Assert.Equal((1, 5), (detail.Value.StartOffsetInBytes, detail.Value.EndOffsetInBytes));
            Assert.Equal("😀"u8.ToArray(), detail.GetGroupMemory(1).ToArray());
            var cursor = regex.EnumerateMatches(input, start, token);
            Assert.True(cursor.MoveNext());
            Assert.Equal((1, 4), (cursor.Current.StartOffsetInBytes, (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes)));
            Assert.False(cursor.MoveNext());
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
