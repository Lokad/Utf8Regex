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
        var regex = new Utf8Regex(pattern, options);
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
            Utf8SliceSparseGroupTests.AssertValue(expected, regex.MatchFromUtf16Offset(input, row.Origin, source.Token));
            Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected,
                regex.MatchDetailedFromUtf16Offset(input, row.Origin, source.Token));
            Assert.True(regex.IsMatchFromUtf16Offset(input, row.Origin, source.Token));
            Assert.Equal(rows.Length - index, regex.CountFromUtf16Offset(input, row.Origin, source.Token));
            var cursor = regex.EnumerateMatchesFromUtf16Offset(input, row.Origin, source.Token);
            for (var next = index; next < rows.Length; next++)
            {
                Assert.True(cursor.MoveNext());
                Assert.True(cursor.Current.IsByteAligned);
                Assert.Equal((rows[next].Byte, rows[next].Length), (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
            }
            Assert.False(cursor.MoveNext());
        }
        foreach (var live in new[] { false, true })
        {
            var token = live ? source.Token : default;
            Utf8SliceSparseGroupTests.AssertDetailed(oracle, oracle.Match(text), regex.MatchDetailed(input, token));
            var cursor = regex.EnumerateMatches(input, token);
            foreach (var row in rows)
            {
                Assert.True(cursor.MoveNext());
                Assert.Equal((row.Byte, row.Length, row.Unit, row.Units),
                    (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes, cursor.Current.IndexInUtf16, cursor.Current.LengthInUtf16));
            }
            Assert.False(cursor.MoveNext());
            foreach (var count in new[] { 0, 1, 2 })
            {
                var pieces = new List<string>();
                foreach (var piece in regex.EnumerateSplits(input, count, token)) pieces.Add(piece.GetValueString());
                Assert.Equal(count == 1 ? [text] : count == 2 ? ["", text] : new[] { "", "😀", "", "😀", "" }, pieces);
            }
            foreach (var (template, expected) in new[] { ("Q", "Q😀QQ😀Q"), ("$&", text) })
            {
                Assert.Equal(expected, regex.ReplaceToString(input, template, token));
                var bytes = Encoding.UTF8.GetBytes(expected);
                Assert.Equal(bytes, regex.Replace(input, template, token));
                var destination = new byte[bytes.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, token));
                Assert.Equal(bytes, destination.AsSpan(0, written).ToArray());
                Assert.Equal(bytes, regex.Replace(input, Encoding.UTF8.GetBytes(template), token));
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out written, token));
                Assert.Equal(bytes, destination.AsSpan(0, written).ToArray());
            }
            Assert.Equal(reverse ? "<>😀<><a>😀<>" : "<>😀<a><>😀<>", regex.ReplaceToString(input, 0,
                static (in Utf8MatchContext match, ref int state) => "<" + match.GetValueString() + ">", token));
            var calls = new List<(int, int)>();
            Assert.Equal(reverse ? "[]😀[][a]😀[]" : "[]😀[a][]😀[]", Encoding.UTF8.GetString(regex.Replace(input, calls,
                static (in Utf8MatchContext match, ref Utf8ReplacementWriter writer, ref List<(int, int)> state) =>
                {
                    state.Add((match.IndexInBytes, match.LengthInBytes));
                    writer.Append("["u8);
                    Assert.True(match.TryGetValueBytes(out var value));
                    writer.Append(value);
                    writer.Append("]"u8);
                }, token)));
            Assert.Equal(rows.Select(row => (row.Byte, row.Length)), calls);
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
