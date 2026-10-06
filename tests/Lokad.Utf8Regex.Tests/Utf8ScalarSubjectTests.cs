using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarSubjectTests
{
    public static IEnumerable<object[]> Cases()
    {
        (string Pattern, string Text, int[] Ranges)[] cases =
        [
            (".", "\U0001f600", [0, 4]),
            ("^.$", "\U0001f600", [0, 4]),
            ("^..$", "\U0001f600", []),
            (".", "a\U0001f600b", [0, 1, 1, 4, 5, 1]),
            (".", "e\u0301", [0, 1, 1, 2]),
            ("^.{2,}$", "\U0001f600", []),
            ("^.{2,}$", "\U0001f600\u00e9", [0, 6]),
            ("^.{2}$", "\U0001f600", []),
            ("^.{2}$", "\U0001f600\u00e9", [0, 6]),
            ("^.{2,}?$", "\U0001f600", []),
            ("(?:)", "\U0001f600", [0, 0, 4, 0]),
            ("(?=(.))", "\U0001f600", [0, 0]),
            ("(?<=.)", "\U0001f600", [4, 0]),
            (@"^(.)\1$", "\U0001f600\U0001f600", [0, 8]),
            ("^\U0001f600+$", "\U0001f600\U0001f600", [0, 8]),
            ("^(\U0001f600){2}$", "\U0001f600\U0001f600", [0, 8]),
            ("^\U0001f600?$", "", [0, 0]),
            ("(?i)^(\U00010400)+$", "\U00010428\U00010428", [0, 8]),
        ];
        foreach (var item in cases)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        {
            // In RTL, the capture must execute before the reference, as in LTR.
            // Keeping the same spelling would intentionally test an unset group.
            var pattern = rtl && item.Pattern == @"^(.)\1$" ? @"^\1(.)$" : item.Pattern;
            yield return [pattern, item.Text, item.Ranges,
                RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PublicOperationsUseCompleteScalars(string pattern, string text, int[] coordinates, RegexOptions options)
    {
        var expected = Enumerable.Range(0, coordinates.Length / 2)
            .Select(index => (Index: coordinates[index * 2], Length: coordinates[index * 2 + 1])).ToArray();
        if ((options & RegexOptions.RightToLeft) != 0) Array.Reverse(expected);
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern, options);
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            Assert.Equal(expected.Length != 0, regex.IsMatch(input, token));
            Assert.Equal(expected.Length, regex.Count(input, token));
            var match = regex.Match(input, token);
            Assert.Equal(expected.Length != 0, match.Success);
            if (match.Success)
            {
                Assert.True(match.TryGetByteRange(out var index, out var length));
                Assert.Equal(expected[0], (index, length));
            }

            var detailed = regex.MatchDetailed(input, token);
            Assert.Equal(expected.Length != 0, detailed.Success);
            if (detailed.Success)
            {
                Assert.True(detailed.IsByteAligned);
                Assert.Equal(expected[0], (detailed.IndexInBytes, detailed.LengthInBytes));
                for (var number = 0; number < detailed.GroupCount; number++)
                {
                    var group = detailed.GetGroup(number);
                    if (!group.Success) continue;
                    Assert.True(group.IsByteAligned);
                    for (var capture = 0; capture < group.CaptureCount; capture++)
                        Assert.True(group.GetCapture(capture).IsByteAligned);
                }
                if (pattern.Contains("(.)", StringComparison.Ordinal))
                {
                    var group = detailed.GetGroup(1);
                    Assert.Equal("\U0001f600", group.GetValueString());
                    Assert.Equal(pattern == @"^\1(.)$" ? (4, 4) : (0, 4),
                        (group.IndexInBytes, group.LengthInBytes));
                }
            }

            var ranges = new List<(int, int)>();
            foreach (var value in regex.EnumerateMatches(input, token))
            {
                Assert.True(value.TryGetByteRange(out var index, out var length));
                ranges.Add((index, length));
            }
            Assert.Equal(expected, ranges);

            var output = new List<byte>();
            var edge = 0;
            foreach (var range in expected.OrderBy(value => value.Index))
            {
                output.AddRange(input.AsSpan(edge, range.Index - edge).ToArray());
                output.Add((byte)'X');
                edge = range.Index + range.Length;
            }
            output.AddRange(input.AsSpan(edge).ToArray());
            Assert.Equal(output, regex.Replace(input, "X", token));
            foreach (var split in regex.EnumerateSplits(input, token)) Assert.True(split.IsByteAligned);
        }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Fact]
    public void ScalarByteEvaluatorReceivesCompleteGroupValues()
    {
        var input = Encoding.UTF8.GetBytes("a\U0001f600\u00e9");
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
            var regex = new Utf8Regex("(.)", options);
            var seen = new List<string>();
            var output = regex.Replace(input, seen,
                static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref List<string> state) =>
                {
                    var group = context.GetGroup(1);
                    Assert.True(group.IsByteAligned);
                    state.Add(group.GetValueString());
                    writer.Append(group.GetValueBytes());
                });
            Assert.Equal(input, output);
            Assert.Equal(rtl ? new[] { "\u00e9", "\U0001f600", "a" } : new[] { "a", "\U0001f600", "\u00e9" }, seen);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }

    [Fact]
    public void RepeatedCaptureHistoryRetainsWholeScalarRanges()
    {
        var input = Encoding.UTF8.GetBytes("a\U0001f600\u0301");
        var regex = new Utf8Regex("^((.)+)$", RegexOptions.CultureInvariant);
        var context = regex.MatchDetailed(input);
        var group = context.GetGroup(2);
        Assert.Equal(3, group.CaptureCount);
        (int Index, int Length)[] expected = [(0, 1), (1, 4), (5, 2)];
        for (var index = 0; index < expected.Length; index++)
        {
            var capture = group.GetCapture(index);
            Assert.True(capture.IsByteAligned);
            Assert.Equal(expected[index], (capture.IndexInBytes, capture.LengthInBytes));
        }
    }

    [Fact]
    public void Utf16StartsRejectAnInteriorSurrogatePosition()
    {
        var regex = new Utf8Regex("(.)", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("\U0001f600a");
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatchFromUtf16Offset(input, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.CountFromUtf16Offset(input, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchFromUtf16Offset(input, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchDetailedFromUtf16Offset(input, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.EnumerateMatchesFromUtf16Offset(input, 1));
        Assert.Equal((4, 1), (regex.MatchFromUtf16Offset(input, 2).IndexInBytes,
            regex.MatchFromUtf16Offset(input, 2).LengthInBytes));
    }

    [Fact]
    public void FailedScalarVmStorageAdmissionReleasesScratch()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\U0001f600", 1024)));
        var map = Utf8BoundaryMap.Create(input);
        var program = Utf8SliceParser.TryParse("^(.)+$", RegexOptions.CultureInvariant)!;
        var invocation = new Utf8SliceInvocation(new(0, 0, 128), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var outcome = Utf8SliceRunner.TryFindFirst(program, input, map, 0,
            new int[program.GroupCount], new int[program.GroupCount], ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, outcome.Breach);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }
    [Theory]
    [InlineData("bool")]
    [InlineData("count")]
    [InlineData("value")]
    [InlineData("detailed")]
    [InlineData("enumerate")]
    [InlineData("split")]
    [InlineData("offset-bool")]
    [InlineData("offset-count")]
    [InlineData("offset-value")]
    [InlineData("offset-detailed")]
    [InlineData("offset-enumerate")]
    [InlineData("split-count")]
    public void CancellationDuringScalarVmReachesEveryOwnedOperation(string operation)
    {
        // Cancellation occurs during owned scalar matching, after input admission.
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\U0001f600", 256)));
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex("((.)+)", options, Regex.InfiniteMatchTimeout);
            using var source = new CancellationTokenSource();
            var entered = false;
            var session = Utf8SearchDiagnosticsSession.Start(steps =>
            {
                entered = true;
                source.Cancel();
                if (steps >= 128) throw new InvalidOperationException("The VM lost the caller's token.");
            });
            try
            {
                var exception = Assert.Throws<OperationCanceledException>(() => Invoke(regex, input, operation, source.Token));
                Assert.True(entered);
                Assert.Equal(source.Token, exception.CancellationToken);
            }
            finally { session.Complete(); }
            Assert.Equal(1, regex.Count("ab"u8));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
            Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
    }

    private static void Invoke(Utf8Regex regex, byte[] input, string operation, CancellationToken token)
    {
        switch (operation)
        {
            case "bool": _ = regex.IsMatch(input, token); break;
            case "count": _ = regex.Count(input, token); break;
            case "value": _ = regex.Match(input, token); break;
            case "detailed": _ = regex.MatchDetailed(input, token); break;
            case "enumerate": _ = regex.EnumerateMatches(input, token).MoveNext(); break;
            case "split": _ = regex.EnumerateSplits(input, token).MoveNext(); break;
            case "offset-bool": _ = regex.IsMatchFromUtf16Offset(input, 2, token); break;
            case "offset-count": _ = regex.CountFromUtf16Offset(input, 2, token); break;
            case "offset-value": _ = regex.MatchFromUtf16Offset(input, 2, token); break;
            case "offset-detailed": _ = regex.MatchDetailedFromUtf16Offset(input, 2, token); break;
            case "offset-enumerate": _ = regex.EnumerateMatchesFromUtf16Offset(input, 2, token).MoveNext(); break;
            case "split-count": _ = regex.EnumerateSplits(input, 4, token).MoveNext(); break;
            default: throw new InvalidOperationException(operation);
        }
    }
}
