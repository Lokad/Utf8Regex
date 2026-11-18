using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// Independent byte-range witnesses replace the former surrogate-half routing
// oracle. Supplementary categories, dot, captures and empty advancement all use
// complete scalars. These cases deliberately differ from BCL code-unit matching.
public sealed class Utf8AstralSemanticsTests
{
    public static IEnumerable<object[]> AstralCases()
    {
        (string Pattern, string Text, int[] Ranges)[] rows =
        [
            ("a.c", "a😀c", [0, 6]),
            ("a.c", "😀", []),
            ("..", "😀", []),
            ("...", "😀", []),
            (".*", "😀", [0, 4, 4, 0]),
            ("a.*b", "a😀b", [0, 6]),
            (".{2}", "😀", []),
            (".{2,4}", "😀😀", [0, 8]),
            ("(a.c)+", "xa😀c", [1, 6]),
            ("(a.c)+", "a😀ca😀c", [0, 12]),
            ("(a.c)", "a😀c", [0, 6]),
            ("^.$", "😀", [0, 4]),
            ("((a|.)c)", "a😀c", [1, 5]),
            ("[^x]", "😀", [0, 4]),
            ("[^x]+", "😀", [0, 4]),
            ("[^x][^y]", "😀", []),
            ("a[^x]d", "a😀d", [0, 6]),
            (@"[^\d]", "😀", [0, 4]),
            (@"[^\w]", "😀", [0, 4]),
            (@"[^a\d]", "😀", [0, 4]),
            ("[abc]+", "a😀bc😀d", [0, 1, 5, 2]),
            ("[abc]", "😀", []),
            (@"[\d]", "𝟎", [0, 4]),
            (@"[\d]", "1", [0, 1]),
            (@"[a\d]", "𝟎", [0, 4]),
            (@"[a\d]", "a1", [0, 1, 1, 1]),
            (@"\d+", "1𝟎2", [0, 6]),
            (@"\d", "𝟎", [0, 4]),
            (@"\D+", "😀", [0, 4]),
            (@"\w+", "a𐀀b", [0, 6]),
            (@"\w+", "𐀀_𐀀", [0, 9]),
            (@"\W+", "😀", [0, 4]),
            (@"\p{L}", "😀", []),
            ("^a", "a😀", [0, 1]),
            ("c$", "a😀c", [5, 1]),
            (@"\ba\b", "a 😀", [0, 1]),
            ("(?i)a[^k]b", "a😀b", [0, 6]),
            ("[abc]*", "a😀", [0, 1, 1, 0, 5, 0]),
            ("[abc]*", "😀", [0, 0, 4, 0]),
            ("a*", "a😀", [0, 1, 1, 0, 5, 0]),
            ("x*", "😀", [0, 0, 4, 0]),
            (".*?", "😀", [0, 0, 4, 0]),
            ("(a|b)*", "a😀b", [0, 1, 1, 0, 5, 1, 6, 0]),
            ("^", "😀", [0, 0]),
            ("a|", "a😀", [0, 1, 1, 0, 5, 0]),
            ("^a.*$", "a😀b", [0, 6]),
        ];
        foreach (var row in rows)
        foreach (var compiled in new[] { false, true })
            yield return [row.Pattern, row.Text, row.Ranges, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0)];

        // RTL outcomes are declared separately: greedy nullable programs can
        // choose different empty positions, so reversing the LTR list is unsafe.
        (string Pattern, string Text, int[] Ranges)[] reverseRows =
        [
            ("a.c", "a😀c", [0, 6]),
            ("^.$", "😀", [0, 4]),
            ("..", "😀", []),
            (@"\d+", "1𝟎2", [0, 6]),
            (@"\w+", "a𐀀b", [0, 6]),
            ("[abc]+", "a😀bc😀d", [5, 2, 0, 1]),
            (".*", "😀", [0, 4, 0, 0]),
            (".*?", "😀", [4, 0, 0, 0]),
            ("a*", "a😀", [5, 0, 0, 1, 0, 0]),
            ("(a|b)*", "a😀b", [5, 1, 5, 0, 0, 1, 0, 0]),
        ];
        foreach (var row in reverseRows)
        foreach (var compiled in new[] { false, true })
            yield return [row.Pattern, row.Text, row.Ranges, RegexOptions.CultureInvariant | RegexOptions.RightToLeft | (compiled ? RegexOptions.Compiled : 0)];
    }

    [Theory]
    [MemberData(nameof(AstralCases))]
    public void AstralOperationsHaveDeclaredScalarRanges(string pattern, string text, int[] coordinates, RegexOptions options)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = Ranges(coordinates);
        var regex = new Utf8Regex(pattern, options: options);
        var reverse = (options & RegexOptions.RightToLeft) != 0;
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            Assert.Equal(expected.Length != 0, regex.IsMatch(input, token));
            Assert.Equal(expected.Length, regex.Count(input, token));
            var match = regex.Match(input, cancellationToken: token);
            Assert.Equal(expected.Length != 0, match.Success);
            if (match.Success) AssertRange(input, expected[0], match);
            else
            {
                Assert.Equal((0, 0), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
                Assert.False(match.Success);
            }

            var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token));
            Assert.Equal(expected.Length != 0, detailed.Success);
            if (detailed.Success)
            {
                Assert.Equal(expected[0], (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
                AssertAlignedCaptures(input, in detailed);
                AssertDeclaredCaptures(pattern, text, input, expected[0], in detailed);
            }

            var cursor = regex.EnumerateMatches(input, cancellationToken: token);
            foreach (var range in expected)
            {
                Assert.True(cursor.MoveNext());
                AssertRange(input, range, cursor.Current);
            }
            Assert.False(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            foreach (var count in new[] { 0, 1, 2, int.MaxValue })
            {
                var splitExpected = SplitRanges(input.Length, expected, count, reverse);
                var splits = regex.EnumerateSplits(input, count, token);
                foreach (var range in splitExpected)
                {
                    Assert.True(splits.MoveNext());
                    var split = splits.Current;
                    Assert.Equal(range, (split.IndexInBytes, split.LengthInBytes));
                    Assert.Equal(Value(input, range), Encoding.UTF8.GetString(split.GetValueBytes()));
                }
                Assert.False(splits.MoveNext());
            }

            foreach (var template in new[] { "x", "$&", "<$&>" })
            {
                var replacement = ReplaceExpected(input, expected, reverse, template == "x" ? _ => "x" :
                    template == "$&" ? range => Value(input, range) : range => "<" + Value(input, range) + ">");
                Assert.Equal(replacement, Encoding.UTF8.GetString(regex.Replace(input, template, token)));
                var bytes = Encoding.UTF8.GetBytes(replacement);
                Assert.Equal(bytes, regex.Replace(input, template, token));
                var destination = new byte[bytes.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, token));
                Assert.Equal(bytes, destination.AsSpan(0, written).ToArray());
            }
        }
        AssertOwned(regex);
    }

    private static (int Index, int Length)[] Ranges(int[] coordinates) => Enumerable.Range(0, coordinates.Length / 2)
        .Select(index => (coordinates[index * 2], coordinates[index * 2 + 1])).ToArray();

    private static string Value(byte[] input, (int Index, int Length) range) => Encoding.UTF8.GetString(input.AsSpan(range.Index, range.Length));

    private static void AssertRange(byte[] input, (int Index, int Length) expected, Utf8CaptureRange actual)
    {
        Assert.True(actual.Success);
        var index = actual.StartOffsetInBytes;
        var length = (actual.EndOffsetInBytes - actual.StartOffsetInBytes);
        Assert.True(actual.Success);
        Assert.Equal(expected, (index, length));
    }

    private static void AssertAlignedCaptures(byte[] input, in Utf8RangeMatch context)
    {

        Utf8MatchSequence.AssertScalarCaptures(context, input);
    }

    private static void AssertDeclaredCaptures(string pattern, string text, byte[] input, (int Index, int Length) first, in Utf8RangeMatch context)
    {
        (int Number, (int Index, int Length)[] Ranges)[] groups = pattern switch
        {
            "(a.c)+" when text == "a😀ca😀c" => [(1, [(0, 6), (6, 6)])],
            "(a.c)+" => [(1, [(1, 6)])],
            "(a.c)" => [(1, [(0, 6)])],
            "((a|.)c)" => [(1, [(1, 5)]), (2, [(1, 4)])],
            "(a|b)*" => [(1, [first])],
            _ => [],
        };
        foreach (var expected in groups)
        {
            Assert.True(context.TryGetGroup(expected.Number, out var group));
            Assert.True(group.Success);
            Assert.Equal(expected.Ranges.Length, context.GetCaptures(expected.Number).Length);
            Assert.Equal(expected.Ranges[^1], (group.StartOffsetInBytes, (group.EndOffsetInBytes - group.StartOffsetInBytes)));
            for (var index = 0; index < expected.Ranges.Length; index++)
            {
                var capture = context.GetCaptures(expected.Number)[index];
                var range = expected.Ranges[index];
                Assert.Equal(range, (capture.StartOffsetInBytes, (capture.EndOffsetInBytes - capture.StartOffsetInBytes)));
                Assert.Equal(Value(input, range), System.Text.Encoding.UTF8.GetString(input.AsSpan().Slice(capture.StartOffsetInBytes, capture.EndOffsetInBytes - capture.StartOffsetInBytes)));
            }
        }
    }

    private static (int Index, int Length)[] SplitRanges(int inputLength, (int Index, int Length)[] matches, int count, bool reverse)
    {
        var used = count == 0 ? matches : matches.Take(count - 1);
        var result = new List<(int, int)>();
        var edge = reverse ? inputLength : 0;
        foreach (var match in used)
        {
            result.Add(reverse ? (match.Index + match.Length, edge - match.Index - match.Length) : (edge, match.Index - edge));
            edge = reverse ? match.Index : match.Index + match.Length;
        }
        result.Add(reverse ? (0, edge) : (edge, inputLength - edge));
        return result.ToArray();
    }

    private static string ReplaceExpected(byte[] input, (int Index, int Length)[] matches, bool reverse,
        Func<(int Index, int Length), string> replacement)
    {
        var output = new StringBuilder();
        var edge = 0;
        foreach (var match in reverse ? matches.Reverse() : matches)
        {
            output.Append(Encoding.UTF8.GetString(input.AsSpan(edge, match.Index - edge)));
            output.Append(replacement(match));
            edge = match.Index + match.Length;
        }
        output.Append(Encoding.UTF8.GetString(input.AsSpan(edge)));
        return output.ToString();
    }

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
