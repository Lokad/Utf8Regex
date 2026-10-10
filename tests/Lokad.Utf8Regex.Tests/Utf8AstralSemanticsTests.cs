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
        var regex = new Utf8Regex(pattern, options);
        var reverse = (options & RegexOptions.RightToLeft) != 0;
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            Assert.Equal(expected.Length != 0, regex.IsMatch(input, token));
            Assert.Equal(expected.Length, regex.Count(input, token));
            var match = regex.Match(input, token);
            Assert.Equal(expected.Length != 0, match.Success);
            if (match.Success) AssertRange(input, expected[0], match);
            else
            {
                Assert.Equal((0, 0), (match.IndexInUtf16, match.LengthInUtf16));
                Assert.False(match.TryGetByteRange(out _, out _));
            }

            var detailed = regex.MatchDetailed(input, token);
            Assert.Equal(expected.Length != 0, detailed.Success);
            if (detailed.Success)
            {
                Assert.Equal(expected[0], (detailed.IndexInBytes, detailed.LengthInBytes));
                AssertAlignedCaptures(in detailed);
                AssertDeclaredCaptures(pattern, text, input, expected[0], in detailed);
            }

            var cursor = regex.EnumerateMatches(input, token);
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
                    Assert.True(split.IsByteAligned);
                    Assert.Equal(range, (split.IndexInBytes, split.LengthInBytes));
                    Assert.Equal(Value(input, range), split.GetValueString());
                    Assert.Equal(Utf16Range(input, range), (split.IndexInUtf16, split.LengthInUtf16));
                }
                Assert.False(splits.MoveNext());
            }

            foreach (var template in new[] { "x", "$&", "<$&>" })
            {
                var replacement = ReplaceExpected(input, expected, reverse, template == "x" ? _ => "x" :
                    template == "$&" ? range => Value(input, range) : range => "<" + Value(input, range) + ">");
                Assert.Equal(replacement, regex.ReplaceToString(input, template, token));
                var bytes = Encoding.UTF8.GetBytes(replacement);
                Assert.Equal(bytes, regex.Replace(input, template, token));
                var destination = new byte[bytes.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, token));
                Assert.Equal(bytes, destination.AsSpan(0, written).ToArray());
            }
        }
        AssertOwned(regex);
    }

    [Theory]
    [MemberData(nameof(AstralCases))]
    public void AstralEvaluatorsReceiveCompleteScalarMatchesAndCaptures(string pattern, string text, int[] coordinates, RegexOptions options)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var ranges = Ranges(coordinates);
        var expected = ReplaceExpected(input, ranges, (options & RegexOptions.RightToLeft) != 0,
            range => "[" + Value(input, range) + "]");
        var regex = new Utf8Regex(pattern, options);
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            var byteCalls = new List<(int, int)>();
            var replaced = regex.Replace(input, byteCalls,
                static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref List<(int, int)> calls) =>
                {
                    Assert.NotNull(Utf8SliceByteOutputScope.Current);
                    AssertAlignedCaptures(in context);
                    calls.Add((context.IndexInBytes, context.LengthInBytes));
                    writer.Append("["u8);
                    writer.Append(context.GetGroup(0).GetValueBytes());
                    writer.Append("]"u8);
                }, token);
            Assert.Equal(Encoding.UTF8.GetBytes(expected), replaced);
            Assert.Equal(ranges, byteCalls);
            Assert.Null(Utf8SliceByteOutputScope.Current);

            var stringCalls = new List<(int, int)>();
            var result = regex.ReplaceToString(input, stringCalls,
                static (in Utf8MatchContext context, ref List<(int, int)> calls) =>
                {
                    AssertAlignedCaptures(in context);
                    calls.Add((context.IndexInBytes, context.LengthInBytes));
                    return "[" + context.GetGroup(0).GetValueString() + "]";
                }, token);
            Assert.Equal(expected, result);
            Assert.Equal(ranges, stringCalls);
        }
        AssertOwned(regex);
    }

    private static (int Index, int Length)[] Ranges(int[] coordinates) => Enumerable.Range(0, coordinates.Length / 2)
        .Select(index => (coordinates[index * 2], coordinates[index * 2 + 1])).ToArray();

    private static (int Index, int Length) Utf16Range(byte[] input, (int Index, int Length) range) =>
        (Encoding.UTF8.GetCharCount(input.AsSpan(0, range.Index)), Encoding.UTF8.GetCharCount(input.AsSpan(range.Index, range.Length)));

    private static string Value(byte[] input, (int Index, int Length) range) => Encoding.UTF8.GetString(input.AsSpan(range.Index, range.Length));

    private static void AssertRange(byte[] input, (int Index, int Length) expected, Utf8ValueMatch actual)
    {
        Assert.True(actual.IsByteAligned);
        Assert.True(actual.TryGetByteRange(out var index, out var length));
        Assert.Equal(expected, (index, length));
        Assert.Equal(Utf16Range(input, expected), (actual.IndexInUtf16, actual.LengthInUtf16));
    }

    private static void AssertAlignedCaptures(in Utf8MatchContext context)
    {
        Assert.True(context.IsByteAligned);
        for (var number = 0; number < context.GroupCount; number++)
        {
            var group = context.GetGroup(number);
            if (!group.Success) continue;
            Assert.True(group.IsByteAligned);
            Assert.Equal(Encoding.UTF8.GetBytes(group.GetValueString()), group.GetValueBytes().ToArray());
            for (var index = 0; index < group.CaptureCount; index++)
            {
                var capture = group.GetCapture(index);
                Assert.True(capture.IsByteAligned);
                Assert.Equal(Encoding.UTF8.GetBytes(capture.GetValueString()), capture.GetValueBytes().ToArray());
            }
        }
    }

    private static void AssertDeclaredCaptures(string pattern, string text, byte[] input, (int Index, int Length) first, in Utf8MatchContext context)
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
            var group = context.GetGroup(expected.Number);
            Assert.True(group.Success);
            Assert.Equal(expected.Ranges.Length, group.CaptureCount);
            Assert.Equal(expected.Ranges[^1], (group.IndexInBytes, group.LengthInBytes));
            for (var index = 0; index < expected.Ranges.Length; index++)
            {
                var capture = group.GetCapture(index);
                var range = expected.Ranges[index];
                Assert.Equal(range, (capture.IndexInBytes, capture.LengthInBytes));
                Assert.Equal(Utf16Range(input, range), (capture.IndexInUtf16, capture.LengthInUtf16));
                Assert.Equal(Value(input, range), capture.GetValueString());
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
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
