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
            (@"^\p{Lu}$", "\U00010400", [0, 4]),
            (@"^\p{Lu}$", "\U00010428", []),
            (@"^\p{Ll}$", "\U00010428", [0, 4]),
            (@"^\p{Ll}$", "\U00010400", []),
            (@"(?i)^\p{Lu}$", "\U00010428", [0, 4]),
            (@"\p{L}+", "\U0001f600\U00010400\U00010428\u4e2d", [4, 11]),
            (@"^\p{Nd}+$", "\U000104a0\U0001d7ce", [0, 8]),
            (@"^\p{Co}$", "\U000f0000", [0, 4]),
            (@"^\p{So}$", "\U0001f600", [0, 4]),
            (@"\p{Cs}+", "a\U0001f600\U00010400", []),
            (@"\P{Cs}+", "a\U0001f600\U00010400", [0, 9]),
            (@"^\d+$", "\U000104a0\U0001d7ce", [0, 8]),
            (@"^\D+$", "\U000104a0", []),
            (@"^\w+$", "\U00010400\U00010428", [0, 8]),
            (@"^\W+$", "\U00010400", []),
            (@"^\W+$", "\U0001f600", [0, 4]),
            (@"^\s$", "\U0001f600", []),
            (@"^\S$", "\U0001f600", [0, 4]),
            (@"\b\w+\b", "\U0001f600\U00010400\U0001f600", [4, 4]),
            (@"\b", "\U00010400", [0, 0, 4, 0]),
            (@"\B", "\U0001f600", [0, 0, 4, 0]),
            (@"[\w!]+", "\U0001f600\U00010400!", [4, 5]),
            (@"(?i)[k\w]+", "\u212a\U00010400", [0, 7]),
            (@"[^\p{L}0]", "\U00010400", []),
            (@"[^\p{L}0]", "\U0001f600", [0, 4]),
            (@"[\p{Lu}\d]+", "\U00010400\U000104a0\U00010428", [0, 8]),
            (@"[\p{L}-[\p{Lu}]]", "\U00010400\U00010428", [4, 4]),
            (@"[\w-[\d]]", "\U00010400\U000104a0", [0, 4]),
            (@"[\p{L}-[\p{Lu}-[\p{Ll}]]]", "\U00010400\U00010428", [4, 4]),
            (@"[^\u0000-\uffff]", "a\U0001f600", [1, 4]),
            (@"\p{IsHighSurrogates}", "\U0001f600", []),
            (@"\P{IsHighSurrogates}", "\U0001f600", [0, 4]),
            (@"[^\P{IsHighSurrogates}]", "\U0001f600", []),
            (@"[\P{IsGreek}-[\p{L}]]", "\u03b1\U00010400\U0001f600", [6, 4]),
            (@"^\D{2,}$", "\U0001f600", []),
            (@"^\D{2,}$", "\U0001f600\U00010400", [0, 8]),
            (@"^\p{L}{2,}$", "\U00010400", []),
            (@"^\p{L}{2,}$", "\U00010400\U00010428", [0, 8]),
            (@"(?<=\w)x", "\U00010400x", [4, 1]),
            (@"(?<=\d)x", "\U000104a0x", [4, 1]),
            (@"(?<=\p{Lu})x", "\U00010400x", [4, 1]),
            (@"(?<=[\w!])x", "\U00010400x", [4, 1]),
            (@"(?<=[\p{Lu}0])x", "\U00010400x", [4, 1]),
            ("(?<=\U0001f600)x", "\U0001f600x", [4, 1]),
            ("(?i)(?<=\U00010400)x", "\U00010428x", [4, 1]),
            ("^[\U0001f600]$", "\U0001f600", [0, 4]),
            ("^[\U0001f600]$", "\U0001f601", []),
            ("[a\U0001f600]", "a\U0001f600\U0001f601", [0, 1, 1, 4]),
            ("^[^\U0001f600]$", "\U0001f600", []),
            ("^[^\U0001f600]$", "\U00010400", [0, 4]),
            ("^[\U0001f600-\U0001f64f]$", "\U0001f64f", [0, 4]),
            ("^[\U0001f600-\U0001f64f]$", "\U0001f650", []),
            ("^[\U0001f600-\U0001f64f-[\U0001f601-\U0001f602]]$", "\U0001f600", [0, 4]),
            ("^[\U0001f600-\U0001f64f-[\U0001f601-\U0001f602]]$", "\U0001f601", []),
            ("[\U0001f600\\p{Lu}]+", "\U0001f600\U00010400\U00010428", [0, 8]),
            ("(?i)^[\U00010400-\U00010401]$", "\U00010428", [0, 4]),
            ("(?i)^[\U00010400-\U00010401]$", "\U0001042a", []),
            ("(?i)[\U00010400-\U00010402-[\U00010401]]", "\U00010428\U00010429\U0001042a", [0, 4, 8, 4]),
            ("^[\uffff-\U00010001]$", "\U00010000", [0, 4]),
            ("^[\uffff-\U00010001]$", "\U00010002", []),
            ("^[\U0010ffff]$", "\U0010ffff", [0, 4]),
            ("^[\u0000-\U0010ffff]$", "\U0001f600", [0, 4]),
            (@"^[\u0000-\uffff]$", "\U0001f600", []),
            (@"^[\u0000-\uffff]$", "\uffff", [0, 3]),
            ("(?<=([\U0001f600-\U0001f64f]))x", "\U0001f600x", [4, 1]),
            (@"^[\uD83D\uDE00-\uD83D\uDE01]$", "\U0001f600", [0, 4]),
            (@"^[\uD83D\uDE00-\uD83D\uDE01]$", "\U0001f602", []),
            (@"^\uD83D\uDE00+$", "\U0001f600\U0001f600", [0, 8]),
            (@"^\uD800$", "\U0001f600", []),
            (@"^[\uD800]$", "\U0001f600", []),
            (@"^[^\uD800]$", "\U0001f600", [0, 4]),
            (@"\uD800*", "\U0001f600", [0, 0, 4, 0]),
            ("(?s)^.$", "\U0001f600", [0, 4]),
            (@"(?i)^([\s\S])\1$", "\U00010400\U00010428", [0, 8]),
            (@"(?i)^([\s\S])\1$", "\U00010400\U00010429", []),
            (@"(?i)^([\s\S])\1$", "\U0001f600\U0001f600", [0, 8]),
            (@"(?i)^([\s\S])\1$", "k\u212a", [0, 4]),
            (@"(?i)^([\s\S])\1$", "\u212ak", [0, 4]),
            (@"(?i)^([\s\S])\1$", "\u03c2\u03c3", []),
            (@"(?i)^([\s\S]{2})\1$", "\U00010400\u212a\U00010428k", [0, 12]),
            (@"(?i)^s$", "\u017f", []),
            (@"(?i)^[s]$", "\u017f", []),
            (@"(?i)^[^s]$", "\u017f", [0, 2]),
            (@"^\uD83D(?:)\uDE00$", "\U0001f600", []),
            (@"^(\uD83D)\uDE00$", "\U0001f600", []),
            (@"^[\uD83D][\uDE00]$", "\U0001f600", []),
            (@"^[\uD83D](?:)[\uDE00]$", "\U0001f600", []),
            (@"^[\uD83D\uDE00]$", "\U0001f600", [0, 4]),
            (@"(?i)^\uD800*$", "", [0, 0]),
        ];
        foreach (var item in cases)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        {
            // In RTL, the capture must execute before the reference, as in LTR.
            // Keeping the same spelling would intentionally test an unset group.
            var pattern = item.Pattern;
            if (rtl && pattern.EndsWith(@"\1$", StringComparison.Ordinal))
            {
                var groupStart = pattern.IndexOf("^(", StringComparison.Ordinal) + 1;
                var referenceStart = pattern.Length - 3;
                pattern = pattern[..groupStart] + @"\1" + pattern[groupStart..referenceStart] + "$";
            }
            yield return [pattern, item.Text, item.Ranges,
                RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
        }
        foreach (var compiled in new[] { false, true })
        foreach (var pattern in new[] { @"\D", @"\W", @"\S", @"[\D0]", @"[^\w]" })
            yield return [pattern, "\U00010400", new[] { 0, 4 },
                RegexOptions.ECMAScript | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PublicOperationsUseCompleteScalars(string pattern, string text, int[] coordinates, RegexOptions options)
    {
        var expected = Enumerable.Range(0, coordinates.Length / 2)
            .Select(index => (Index: coordinates[index * 2], Length: coordinates[index * 2 + 1])).ToArray();
        if ((options & RegexOptions.RightToLeft) != 0) Array.Reverse(expected);
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern, options: options);
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            Assert.Equal(expected.Length != 0, regex.IsMatch(input, token));
            Assert.Equal(expected.Length, regex.Count(input, token));
            var match = regex.Match(input, cancellationToken: token);
            Assert.Equal(expected.Length != 0, match.Success);
            if (match.Success)
            {
                var index = match.StartOffsetInBytes;
                var length = (match.EndOffsetInBytes - match.StartOffsetInBytes);
                Assert.True(match.Success);
                Assert.Equal(expected[0], (index, length));
            }

            var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token));
            Assert.Equal(expected.Length != 0, detailed.Success);
            if (detailed.Success)
            {
                Assert.Equal(expected[0], (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
                Utf8MatchSequence.AssertScalarCaptures(detailed, input);
                if (pattern.Contains("(.)", StringComparison.Ordinal))
                {
                    Assert.True(detailed.TryGetGroup(1, out var group));
                    Assert.Equal("\U0001f600", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
                    Assert.Equal(pattern == @"^\1(.)$" ? (4, 4) : (0, 4),
                        (group.StartOffsetInBytes, (group.EndOffsetInBytes - group.StartOffsetInBytes)));
                }
            }

            var ranges = new List<(int, int)>();
            foreach (var value in regex.EnumerateMatches(input, cancellationToken: token))
            {
                var index = value.StartOffsetInBytes;
                var length = (value.EndOffsetInBytes - value.StartOffsetInBytes);
                Assert.True(value.Success);
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
            foreach (var split in regex.EnumerateSplits(input, cancellationToken: token)) Assert.Equal(split.GetValueBytes().ToArray(), Encoding.UTF8.GetBytes(split.GetValueString()));
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Theory]
    [InlineData(".")]
    [InlineData(@"\B")]
    [InlineData("(?:)")]
    public void RunnerRejectsInteriorAndFormerHalfStartsBeforeScratchAdmission(string pattern)
    {
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var input = Encoding.UTF8.GetBytes("\U0001f600");
        foreach (var position in new[] { (0, false), (1, true), (3, true) })
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
            var outcome = Utf8SliceRunner.TryMatchAt(program, input, position.Item1, [], [], position.Item2, ref invocation);
            Assert.False(outcome.IsMatch);
            Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
            Assert.Equal(0UL, invocation.Budget.Steps);
        }
    }

    [Fact]
    public void RepeatedCaptureHistoryRetainsWholeScalarRanges()
    {
        var input = Encoding.UTF8.GetBytes("a\U0001f600\u0301");
        var regex = new Utf8Regex("^((.)+)$", options: RegexOptions.CultureInvariant);
        var context = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(context.TryGetGroup(2, out var group));
        Assert.Equal(3, context.GetCaptures(2).Length);
        (int Index, int Length)[] expected = [(0, 1), (1, 4), (5, 2)];
        for (var index = 0; index < expected.Length; index++)
        {
            var capture = context.GetCaptures(2)[index];
            Assert.Equal(expected[index], (capture.StartOffsetInBytes, (capture.EndOffsetInBytes - capture.StartOffsetInBytes)));
        }
    }

    [Fact]
    public void Utf16StartsRejectAnInteriorSurrogatePosition()
    {
        var regex = new Utf8Regex("(.)", options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("\U0001f600a");
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match(input, 1, []).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => Utf8MatchSequence.Count(regex.EnumerateMatches(input, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor(input, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.EnumerateMatches(input, 1));
        var selected = regex.Match(input, 4, []);
        Assert.True(selected.Success);
        Assert.Equal((4, 5), (selected.StartOffsetInBytes, selected.EndOffsetInBytes));
    }

    [Fact]
    public void FailedScalarVmStorageAdmissionReleasesScratch()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\U0001f600", 1024)));
        var program = Utf8SliceParser.TryParse("^(.)+$", RegexOptions.CultureInvariant)!;
        var invocation = new Utf8SliceInvocation(new(0, 0, 128), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var outcome = Utf8SliceRunner.TryFindFirst(program, input, 0,
            new int[program.GroupCount], new int[program.GroupCount], false, ref invocation);
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
    [InlineData("offset-captures")]
    [InlineData("offset-enumerate")]
    [InlineData("split-count")]
    public void CancellationDuringScalarVmReachesEveryOwnedOperation(string operation)
    {
        // Cancellation occurs during owned scalar matching, after input admission.
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\U0001f600", 256)));
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex("((.)+)", options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout
            });
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
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }

    private static void Invoke(Utf8Regex regex, byte[] input, string operation, CancellationToken token)
    {
        switch (operation)
        {
            case "bool": _ = regex.IsMatch(input, token); break;
            case "count": _ = regex.Count(input, token); break;
            case "value": _ = regex.Match(input, cancellationToken: token); break;
            case "detailed": _ = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token)); break;
            case "enumerate": _ = regex.EnumerateMatches(input, cancellationToken: token).MoveNext(); break;
            case "split": _ = regex.EnumerateSplits(input, cancellationToken: token).MoveNext(); break;
            case "offset-bool": _ = regex.Match(input, 4, [], token).Success; break;
            case "offset-captures": _ = Utf8MatchSequence.First(regex.CreateMatchCursor(input, 4, token)); break;
            case "offset-enumerate": _ = regex.EnumerateMatches(input, 4, token).MoveNext(); break;
            case "split-count": _ = regex.EnumerateSplits(input, 4, token).MoveNext(); break;
            default: throw new InvalidOperationException(operation);
        }
    }
}
