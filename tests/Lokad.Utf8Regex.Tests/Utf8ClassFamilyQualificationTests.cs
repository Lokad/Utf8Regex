using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ClassFamilyQualificationTests
{
    private static void ScalarRange(string pattern, string text, RegexOptions options, int index, int length)
    {
        var regex = new Utf8Regex(pattern, options: options); var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input)); var match = regex.Match(input); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal((index, length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))); Assert.Equal((index, length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
    }
    private static void TrustedPublicCount(string pattern, ReadOnlySpan<byte> input, int expected)
    {
        Assert.Equal(expected, new Utf8Regex(pattern, options: RegexOptions.CultureInvariant).Count(input));
    }

    [Fact]
    public void AnchoredIdentifierEscapesUseCanonicalFoldedAsciiIntersections()
    {
        const string pattern = @"\G[\x61-\x7A][\x61-\x7A0-9_]*";
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant;
        foreach (var compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            ScalarRange(pattern, "Ab_2Ω", options | compiled, 0, 4);
            ScalarRange(pattern, "K2", options | compiled, 0, 4);
            ScalarRange(@"\G(?i:[a-z])[a-z0-9_]*", "aB", RegexOptions.CultureInvariant | compiled, 0, 1);
        }
    }

    [Fact]
    public void OperatorEscapesAndIdentifierAliasesKeepTheirQualifiedNativeFamilies()
    {
        const string operators = @"\G[\x3D+\-\^\x2A\/\.\x3C\x3E~!&|?]+";
        ScalarRange(operators, "=+-^*/.<>~!&|?", RegexOptions.CultureInvariant, 0, 14);
        const string identifier = @"[\x41-\x5A\x61-\x7A][A-Za-z0-9]*";
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
        { ScalarRange(identifier, "😀 A2 é", options, 5, 2); Assert.Equal(1, new Utf8Regex(identifier, options: options).Count("😀 A2 é"u8)); }
    }

    [Fact]
    public void NegatedRunsRetainEscapedScalarExclusionsAndAdmittedLiteralBytes()
    {
        const string pattern = @"(?<head>[^\n]+)\x3A(?<tail>[^\r]+)";
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled }) ScalarRange(pattern, "😀 a:b", options, 0, 8);
    }

    [Theory]
    [InlineData("\"[^\"\\n]*\"", "é\"skip\n😀\"\"", true)]
    [InlineData("\"[^\"\\n]*\"", "é\"skip\n😀\"", false)]
    [InlineData("x[^!\\n]*!", "xéx\nxx😀!", true)]
    [InlineData("x[^!\\n]*!", "xéx\n!", false)]
    [InlineData("\\n[^!\\n]*!", "\nx\n😀!", true)]
    [InlineData("\\n[^!\\n]*!", "\nx\n", false)]
    public void DelimitedRepeatResumesAtARejectedStop(string pattern, string text, bool expected)
    {
        using var regex = new Utf8Regex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxDepth = 1, MaxWorkingBytes = 1, MaxWorkSteps = 100 }
        });
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(Utf8SliceTerminalRootKind.DelimitedClassRepeat, program.TerminalRoot);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(expected ? 1 : 0, regex.Count(input));
        var reference = Regex.Match(text, pattern);
        Assert.Equal(expected, reference.Success);
        var range = reference.Success
            ? new Utf8CaptureRange(Encoding.UTF8.GetByteCount(text.AsSpan(0, reference.Index)),
                Encoding.UTF8.GetByteCount(text.AsSpan(0, reference.Index + reference.Length)))
            : default;
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[1];
        Assert.Equal(range, regex.Match(input, 0, slots));
        Assert.Equal(range, slots[0]);
    }

    [Fact]
    public void DelimitedRangesKeepTheSelectedEndAndTheNextCursorStart()
    {
        const string pattern = "\"[^\"\\n]*\"";
        const string text = "⚑\"skip\n😀\"\" \"é\"";
        var input = Encoding.UTF8.GetBytes(text);
        var empty = "⚑\"skip\n😀"u8.Length;
        var body = empty + "\"\" "u8.Length;
        using var regex = new Utf8Regex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxDepth = 1, MaxWorkingBytes = 128, MaxWorkSteps = 128 }
        });

        Assert.False(regex.Match(input, body, endOffsetInBytes: input.Length - 1).Success);
        using var cursor = regex.CreateMatchCursor(input, "⚑"u8.Length);
        Assert.True(cursor.MoveNext(out var first));
        Assert.Equal(new Utf8CaptureRange(empty, empty + 2), first.Value);
        Assert.True(cursor.MoveNext(out var second));
        Assert.Equal(new Utf8CaptureRange(body, input.Length), second.Value);
        Assert.False(cursor.MoveNext(out _));
        Assert.True(first.GetValueMemory().Span.SequenceEqual("\"\""u8));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DelimitedRepeatKeepsWorkAdmissionAndInterruptionThroughLongBody(bool expire, bool count)
    {
        const string pattern = "\"[^\"\\n]*\"";
        var bytes = Encoding.UTF8.GetBytes("\"" + new string('a', 140_000) + "\"");
        using var limited = new Utf8Regex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 8 }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Execute(limited, default)).ResourceKind);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var timeout = TimeSpan.FromMilliseconds(50);
        using var regex = new Utf8Regex(pattern, policy: new() { MatchTimeout = timeout });
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (expire) ticks = 100;
            else source.Cancel();
        });
        try
        {
            if (expire)
                Assert.Equal(timeout, Assert.Throws<RegexMatchTimeoutException>(
                    () => Execute(regex, source.Token)).MatchTimeout);
            else
                Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(
                    () => Execute(regex, source.Token)).CancellationToken);
        }
        finally { session.Complete(); }

        int Execute(Utf8Regex engine, CancellationToken token)
            => count ? engine.Count(bytes, token) : engine.IsMatch(bytes, token) ? 1 : 0;
    }

    [Fact]
    public void DelimitedRepeatCountsAcrossWindowSeams()
    {
        const string pattern = "\"[^\"\\n]*\"";
        // Split the first pair and a later four-byte scalar across windows.
        var text = new string('!', 2047) + "\"\"\"" + new string('x', 2045) + "😀\"\n\"odd\n\"\"";
        var input = Encoding.UTF8.GetBytes(text);
        using var regex = new Utf8Regex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 24, MaxDepth = 1, MaxWorkingBytes = 1 }
        });
        using var live = new CancellationTokenSource();
        Assert.Equal(3, Regex.Count(text, pattern));
        Assert.Equal(3, regex.Count(input, live.Token));
    }

    [Fact]
    public void NegatedRunFinderPreservesGreedyEndpointsAndTheCallerFloor()
    {
        var fixedRegex = new Utf8Regex("[^x]+:[^y]+");
        Assert.Equal((0, 7), (fixedRegex.Match("a:bya:c"u8).StartOffsetInBytes, fixedRegex.Match("a:bya:c"u8).EndOffsetInBytes));
        var next = fixedRegex.Match("a:bya:c"u8, 2, Span<Utf8CaptureRange>.Empty);
        Assert.Equal((2, 7), (next.StartOffsetInBytes, next.EndOffsetInBytes));
        foreach (var head in "xy:") foreach (var tail in "xy:")
        {
            var pattern = "[^" + head + "]+:[^" + tail + "]+"; var oracle = new Regex(pattern); var regex = new Utf8Regex(pattern);
            foreach (var text in Subjects(5, ""))
            {
                var input = Encoding.UTF8.GetBytes(text);
                for (var floor = 0; floor <= text.Length; floor++)
                {
                    var expected = oracle.Match(text, floor); var actual = regex.Match(input, floor, Span<Utf8CaptureRange>.Empty);
                    Assert.Equal(expected.Success, actual.Success);
                    if (actual.Success) Assert.Equal((expected.Index, expected.Length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes - actual.StartOffsetInBytes));
                }
            }
        }
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled }) ScalarRange("[^x]+:[^y]+", "a:bya:c", options, 0, 7);
        foreach (var padding in new[] { 16, 32, 64 })
        {
            var text = new string('a', padding) + ":b"; var input = Encoding.UTF8.GetBytes(text); var oracle = new Regex("[^x]+:[^y]+");
            foreach (var floor in new[] { 0, padding - 1, padding, padding + 1, padding + 2 })
            {
                var expected = oracle.Match(text, floor); var actual = fixedRegex.Match(input, floor, Span<Utf8CaptureRange>.Empty);
                Assert.Equal(expected.Success, actual.Success);
                if (actual.Success) Assert.Equal((expected.Index, expected.Length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes - actual.StartOffsetInBytes));
            }
        }
    }private static IEnumerable<string> Subjects(int remaining, string prefix)
    { yield return prefix; if (remaining == 0) yield break; foreach (var value in "a:xy") foreach (var text in Subjects(remaining - 1, prefix + value)) yield return text; }

    [Fact]
    public void SingleUnicodeCategoriesBorrowCompleteScalarPredicatesInsteadOfPatternSpellings()
    {
        foreach (var pattern in new[] { @"[\p{Lu}]", @"(?:\p{Lu})", @"\p{Lu}{1}" })
        {
foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
                Assert.Equal(2, new Utf8Regex(pattern, options: options).Count("𐐀A😀é"u8));
        }
        Assert.Equal(2, new Utf8Regex(@"(?i:\p{Lu})", options: RegexOptions.CultureInvariant).Count("aA"u8));
        TrustedPublicCount(@"[\p{Lu}]", "𐐀A😀é"u8, 2);
    }

    [Fact]
    public void LetterRepeatAliasesCountSupplementaryScalarsWithTheirParsedBounds()
    {
        foreach (var pattern in new[] { @"(?:\p{L}){2,3}", @"[\p{L}]{2,3}", @"(\p{L}){2,3}" })
        {
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
                Assert.Equal(2, new Utf8Regex(pattern, options: options).Count("𐐀AéB😀CΩ"u8));
        }
        Assert.Equal(3, new Utf8Regex(@"[\p{L}]").Count("𐐀Aé😀"u8));
        TrustedPublicCount(@"[\p{L}]", "𐐀Aé😀"u8, 3);
        Assert.Equal(0, new Utf8Regex(@"(?:[\p{L}]{2}){2,3}").Count("𐐀𐐀𐐀"u8));
    }

    [Fact]
    public void NegatedStarAliasesTreatNulAsACompleteScalarAndAdvanceEmptyMatchesByScalar()
    {
        foreach (var pattern in new[] { @"[^\x00]*", @"[^\0]{0,}", @"(?:[^\u0000])*" })
        {
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
            { var regex = new Utf8Regex(pattern, options: options); Assert.Equal(4, regex.Count("😀\0A"u8)); Assert.Equal(2, regex.Count("😀"u8)); }
        }
    }

    [Fact]
    public void WordRepeatAliasesPreserveCompleteRunsAndOnlyUseAsciiCountingOnAsciiSubjects()
    {
        const string pattern = @"(?:\b)(?:[\w]){10,}(?:\b)";
        Assert.Equal(1, new Utf8Regex(pattern).Count("abcdefghijk"u8));
        Assert.Equal(1, new Utf8Regex(pattern).Count(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("𐐀", 10)))));
        const string nestedToken = @"[a]+:[b]+(?:[c]{2})+";
        Assert.Equal(0, new Utf8Regex(nestedToken).Count("a:bc"u8));
        Assert.Equal(1, new Utf8Regex(nestedToken).Count("a:bcc"u8));
    }
}
