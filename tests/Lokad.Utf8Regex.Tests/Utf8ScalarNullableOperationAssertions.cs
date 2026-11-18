using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// Frozen supplementary controls for the deep ASCII-only nullable grammar
// families. Individual .NET matches overlap; .NET global empty progress does
// not. Expected scalar boundaries/counts are declared independently here.
internal static class Utf8ScalarNullableOperationAssertions
{
    internal static void AssertOperations(string pattern, RegexOptions options)
    {
        const string text = "😀a😀";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options);
        var reverse = (options & RegexOptions.RightToLeft) != 0;
        (int Origin, int Byte, int Length, int Unit, int Units)[] rows = reverse
            ? [(5, 9, 0, 5, 0), (3, 4, 1, 2, 1), (2, 4, 0, 2, 0), (0, 0, 0, 0, 0)]
            : [(0, 0, 0, 0, 0), (2, 4, 1, 2, 1), (3, 5, 0, 3, 0), (5, 9, 0, 5, 0)];
        using var source = new CancellationTokenSource();
        Assert.Equal(4, regex.Count(input));
        Assert.Equal(4, regex.Count(input, source.Token));
        Assert.True(regex.IsMatch(input));
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var expected = oracle.Match(text, row.Origin);
            Assert.Equal((row.Unit, row.Units), (expected.Index, expected.Length));
            var offsetValue = regex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, row.Origin)), [], source.Token);
            Assert.True(offsetValue.Success);
            Assert.Equal((row.Byte, row.Byte + row.Length), (offsetValue.StartOffsetInBytes, offsetValue.EndOffsetInBytes));
            Utf8MatchSequence.AssertCaptures(expected,
                Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, row.Origin)), source.Token)), input, text);
            Assert.Equal(rows.Length - index, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, row.Origin)), source.Token)));
            var cursor = regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, row.Origin)), source.Token);
            for (var next = index; next < rows.Length; next++)
            {
                Assert.True(cursor.MoveNext());
                Assert.True(cursor.Current.Success);
                Assert.Equal((rows[next].Byte, rows[next].Length), (cursor.Current.StartOffsetInBytes, (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes)));
            }
            Assert.False(cursor.MoveNext());
        }
        foreach (var live in new[] { false, true })
        {
            var token = live ? source.Token : default;
            Utf8MatchSequence.AssertCaptures(oracle.Match(text),
                Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token)), input, text);
            var cursor = regex.EnumerateMatches(input, cancellationToken: token);
            foreach (var row in rows)
            {
                Assert.True(cursor.MoveNext());
                Assert.Equal((row.Byte, row.Length), (cursor.Current.StartOffsetInBytes, (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes)));
            }
            Assert.False(cursor.MoveNext());
            foreach (var count in new[] { 0, 1, 2 })
            {
                var pieces = new List<string>();
                foreach (var piece in regex.EnumerateSplits(input, count, token)) pieces.Add(Encoding.UTF8.GetString(piece.GetValueBytes()));
                Assert.Equal(count == 1 ? [text] : count == 2 ? ["", text] : new[] { "", "😀", "", "😀", "" }, pieces);
            }
            foreach (var (template, expected) in new[] { ("Q", "Q😀QQ😀Q"), ("$&", text) })
            {
                var bytes = Encoding.UTF8.GetBytes(expected);
                Assert.Equal(bytes, regex.Replace(input, Encoding.UTF8.GetBytes(template), token));
                var destination = new byte[bytes.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var written, token));
                Assert.Equal(bytes, destination.AsSpan(0, written).ToArray());
                Assert.Equal(bytes, regex.Replace(input, Encoding.UTF8.GetBytes(template), token));
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out written, token));
                Assert.Equal(bytes, destination.AsSpan(0, written).ToArray());
            }
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
