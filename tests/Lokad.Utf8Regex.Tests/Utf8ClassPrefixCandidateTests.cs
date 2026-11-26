using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8ClassPrefixCandidateTests
{
    [Fact]
    public void VerifiedCapturedClassWindowKeepsSlotsHistoryAndBackreferences()
    {
        const string pattern = @"(\w+): (\w+)";
        var prefix = "Ω " + string.Concat(Enumerable.Repeat("x:  /:; t: ! y: .; ", 64));
        var input = Encoding.UTF8.GetBytes(prefix + "clé: result");
        var start = Encoding.UTF8.GetByteCount(prefix);
        var key = new Utf8CaptureRange(start, start + 4);
        var value = new Utf8CaptureRange(start + 6, input.Length);
        using var source = new CancellationTokenSource();
        using var bounded = new Utf8Regex(pattern, policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = 800 }
        });
        Assert.True(bounded.IsMatch(input, source.Token));
        Assert.Equal(new Utf8CaptureRange(start, input.Length), bounded.Match(input, cancellationToken: source.Token));
        Assert.False(bounded.IsMatch("Ω!: value clé: ."u8, source.Token));
        Assert.False(bounded.IsMatch("clé: "u8, source.Token));
        Assert.True(bounded.IsMatch("clé: . clé: β42"u8, source.Token));

        using var regex = new Utf8Regex(pattern);
        Assert.Equal(1, regex.Count(input, source.Token));
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[3];
        Assert.Equal(new Utf8CaptureRange(start, input.Length), regex.Match(input, 0, slots, source.Token));
        Assert.Equal(key, slots[1]);
        Assert.Equal(value, slots[2]);
        using var cursor = regex.CreateMatchCursor(input, cancellationToken: source.Token);
        Assert.True(cursor.MoveNext(out var captured));
        Assert.Equal(key, Assert.Single(captured.GetCaptures(1).ToArray()));
        Assert.Equal(value, Assert.Single(captured.GetCaptures(2).ToArray()));
        Assert.False(cursor.MoveNext(out _));

        using var backreference = new Utf8Regex(@"(\w+): \1\b");
        Assert.False(backreference.IsMatch(input, source.Token));
        Assert.Equal(0, backreference.Count(input, source.Token));
        Assert.True(backreference.IsMatch("clé: clé"u8, source.Token));
    }

    [Theory]
    [InlineData(": ")]
    [InlineData(":: ")]
    public void OrdinalBackreferenceWindowKeepsLaterStartsAndWideCaptures(string delimiter)
    {
        var pattern = @"(\w+)" + delimiter + @"\1\b";
        var prefix = new string('!', 2046) + "Ω " + string.Concat(Enumerable.Repeat($"key{delimiter}value; ", 64));
        var text = prefix + $"clékey{delimiter}key; clé{delimiter}clé; key{delimiter}";
        var expected = new Regex(pattern).Matches(text);
        Assert.Equal(2, expected.Count);
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = Funded });

        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(prefix + "key" + delimiter), source.Token));
        Assert.True(regex.IsMatch(input, source.Token));
        Assert.Equal(expected.Count, regex.Count(input, source.Token));
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
        var first = Encoding.UTF8.GetByteCount(text.AsSpan(0, expected[0].Index));
        Assert.Equal(new Utf8CaptureRange(first, first + expected[0].Length), regex.Match(input, 0, slots, source.Token));
        Assert.Equal(new Utf8CaptureRange(first, first + 3), slots[1]);
        using var cursor = regex.CreateMatchCursor(input, slots[0].EndOffsetInBytes, source.Token);
        Assert.True(cursor.MoveNext(out var wide));
        var second = Encoding.UTF8.GetByteCount(text.AsSpan(0, expected[1].Index));
        Assert.Equal(new Utf8CaptureRange(second, second + Encoding.UTF8.GetByteCount(expected[1].Value)), wide.Value);
        Assert.Equal(new Utf8CaptureRange(second, second + 4), Assert.Single(wide.GetCaptures(1).ToArray()));
        Assert.False(cursor.MoveNext(out _));
    }

    [Fact]
    public void RequiredFamilyKeepsStartsBeforeAnUnalignedFirstLiteral()
    {
        const string pattern = @"(\w+)\s+(Holmes|Watson)";
        var text = new string('a', 4096) + "!ΩHolmes\u2003Watson!é\u0085Holmes";
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern).Matches(text, 1);
        Assert.Equal(2, expected.Count);
        using var source = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = new() { MaxWorkSteps = 256 } });

        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[3];
        Assert.Equal(Range(expected[0]), regex.Match(input, 1, slots, source.Token));
        Assert.Equal(Range(expected[0].Groups[1]), slots[1]);
        Assert.Equal(Range(expected[0].Groups[2]), slots[2]);
        using var cursor = regex.CreateMatchCursor(input, 1, source.Token);
        foreach (Match match in expected)
        {
            Assert.True(cursor.MoveNext(out var actual));
            Assert.Equal(Range(match), actual.Value);
            Assert.Equal(Range(match.Groups[1]), Assert.Single(actual.GetCaptures(1).ToArray()));
            Assert.Equal(Range(match.Groups[2]), Assert.Single(actual.GetCaptures(2).ToArray()));
        }
        Assert.False(cursor.MoveNext(out _));

        Utf8CaptureRange Range(Group group) => new(Encoding.UTF8.GetByteCount(text.AsSpan(0, group.Index)),
            Encoding.UTF8.GetByteCount(text.AsSpan(0, group.Index + group.Length)));
    }

    [Theory]
    [InlineData("Holmes")]
    [InlineData("Holmesx")]
    [InlineData("Watson")]
    public void AlignedFamilyKeepsTheWinningAlternativeAndCaptureHistory(string suffix)
    {
        const string pattern = @"(\w+)\s+(Holmesx|Holmes|Watson)";
        var text = "aΩ\u2003" + suffix + "!" + new string('!', 256);
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = Funded });
        Assert.True(regex.IsMatch(input, source.Token));
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("Holmes! " + text), source.Token));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("Holmes!" + new string('!', 256)), source.Token));
        const int start = 1; // The first byte and UTF-16 code unit are both ASCII.
        var expected = new Regex(pattern, RegexOptions.CultureInvariant).Match(text, start);
        Assert.True(expected.Success);
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[3];
        Assert.Equal(Range(expected), regex.Match(input, start, slots, source.Token));
        Assert.Equal(Range(expected.Groups[1]), slots[1]);
        Assert.Equal(Range(expected.Groups[2]), slots[2]);
        using var cursor = regex.CreateMatchCursor(input, start, source.Token);
        Assert.True(cursor.MoveNext(out var captured));
        Assert.Equal(slots[1], Assert.Single(captured.GetCaptures(1).ToArray()));
        Assert.Equal(slots[2], Assert.Single(captured.GetCaptures(2).ToArray()));

        Utf8CaptureRange Range(Group group) => new(Encoding.UTF8.GetByteCount(text.AsSpan(0, group.Index)),
            Encoding.UTF8.GetByteCount(text.AsSpan(0, group.Index + group.Length)));
    }

    [Fact]
    public void ProvedClassPrefixKeepsTheCaptureAtASupplementaryByteFloor()
    {
        const string pattern = @"(\w+)\s+(Holmes|Watson)";
        using var regex = new Utf8Regex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 32, MaxDepth = 128, MaxWorkingBytes = 4096 }
        });
        using var source = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(new string('!', 512) + "Ω𐐀\u2003Watson!" + new string('!', 320));
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[3];
        var match = regex.Match(input, startOffsetInBytes: 514, captures: slots, cancellationToken: source.Token);
        Assert.Equal(new Utf8CaptureRange(514, 527), match);
        Assert.Equal(match, slots[0]);
        Assert.Equal(new Utf8CaptureRange(514, 518), slots[1]);
        Assert.Equal(new Utf8CaptureRange(521, 527), slots[2]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualTerminalClassesRequireAScalarOnBothSidesOfTheDelimiter(bool captures)
    {
        var pattern = captures ? @"([\w.+-]+)@([\p{L}.]+)\.([\p{L}.]+)" : @"[\w.+-]+@[\p{L}.]+\.[\p{L}.]+";
        var reference = new Regex(pattern);
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = Funded });
        using var source = new CancellationTokenSource();
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[captures ? 4 : 1];
        foreach (var suffix in new[] { "a@.β", "a@β.", "a@β..", "a@β.γ", "a@β.γ.δ", "a@" + new string('β', 48) + ".γ" })
        {
            var text = new string('!', 260) + suffix;
            var input = Encoding.UTF8.GetBytes(text);
            var expected = reference.Match(text);
            Assert.Equal(expected.Success, regex.IsMatch(input, source.Token));
            var range = expected.Success
                ? new Utf8CaptureRange(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)),
                    Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index + expected.Length)))
                : default;
            Assert.Equal(range, regex.Match(input, cancellationToken: source.Token));
            Assert.Equal(reference.Count(text), regex.Count(input, source.Token));
            if (captures && expected.Success)
            {
                Assert.Equal(range, regex.Match(input, 0, slots, source.Token));
                for (var group = 1; group < slots.Length; group++)
                {
                    var capture = expected.Groups[group];
                    Assert.Equal(new Utf8CaptureRange(Encoding.UTF8.GetByteCount(text.AsSpan(0, capture.Index)),
                        Encoding.UTF8.GetByteCount(text.AsSpan(0, capture.Index + capture.Length))), slots[group]);
                }
            }
        }
    }

    [Fact]
    public void VerifiedClassDelimiterKeepsWorkBoundAndWideLabelEndpoints()
    {
        using var regex = new Utf8Regex(@"[\w.+-]+@[\w-]+\.[\w.-]+", policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = 800 }
        });
        using var source = new CancellationTokenSource();
        var prefix = "Ω " + string.Concat(Enumerable.Repeat("a@label! x@.bar; t@p.; ", 64));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(prefix), source.Token));
        var input = Encoding.UTF8.GetBytes(prefix + "u@b.example");
        Assert.True(regex.IsMatch(input, source.Token));
        Assert.Equal(new Utf8CaptureRange(Encoding.UTF8.GetByteCount(prefix), input.Length),
            regex.Match(input, cancellationToken: source.Token));

        // The first wide scalar can begin before or after the proof's edge.
        foreach (var length in new[] { 88, 89, 90 })
        {
            var value = "u@" + new string('a', length) + "β.γ";
            var bytes = Encoding.UTF8.GetBytes("Ω " + value + new string('!', 256));
            Assert.True(regex.IsMatch(bytes, source.Token));
            Assert.Equal(new Utf8CaptureRange(3, 3 + Encoding.UTF8.GetByteCount(value)),
                regex.Match(bytes, cancellationToken: source.Token));
        }

        Assert.True(regex.IsMatch("é@@β! λ@b.cd"u8, source.Token));
    }

    [Fact]
    public void CountAcrossAlignedDelimitersKeepsCompleteNonOverlappingEndpoints()
    {
        const string pattern = @"[\w.+-]+@[\w-]+\.[\w.-]+";
        var prefix = new string('!', 260) + "Ω x@.bar; t@p. ";
        var text = prefix + "α@β.γ.δ@b.cd " + "q@" + new string('β', 90) + ".γ; λ@b?.cd";
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = Funded });
        var expected = new Regex(pattern).Matches(text);
        Assert.Equal(2, expected.Count);
        Assert.Equal(expected.Count, regex.Count(input, source.Token));
        using var cursor = regex.CreateMatchCursor(input, cancellationToken: source.Token);
        foreach (Match match in expected)
        {
            Assert.True(cursor.MoveNext(out var observed));
            Utf8MatchSequence.AssertRange(observed.Value, text, match.Index, match.Length);
        }
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void PreparedLabelRangeKeepsItsByteFloorAndGreedyEnd(int labelLength)
    {
        const string pattern = @"[\w.+-]+@[\w-]+\.[\w.-]+";
        const string before = "😀 pré";
        var throughLabel = before + ".fix@b." + new string('c', labelLength);
        var input = Encoding.UTF8.GetBytes(throughLabel + "é-δ ! gone@no; tail@d.e");
        var floor = Encoding.UTF8.GetByteCount(before);
        var end = Encoding.UTF8.GetByteCount(throughLabel);
        var first = new Utf8CaptureRange(floor, end + "é-δ"u8.Length);
        var captures = new[] { default(Utf8CaptureRange), new Utf8CaptureRange(0, 1) };
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = Funded });

        Assert.Equal(new Utf8CaptureRange(floor, end),
            regex.Match(input, floor, captures, token.Token, endOffsetInBytes: end));
        Assert.Equal(new Utf8CaptureRange(floor, end), captures[0]);
        Assert.Equal(new Utf8CaptureRange(0, 1), captures[1]);
        Assert.Equal(first, regex.Match(input, floor, captures, token.Token));
        using var cursor = regex.CreateMatchCursor(input, floor, token.Token);
        Assert.True(cursor.MoveNext(out var range));
        Assert.Equal(first, range.Value);
        Assert.True(cursor.MoveNext(out range));
        Assert.Equal(new Utf8CaptureRange(input.Length - "tail@d.e"u8.Length, input.Length), range.Value);
        Assert.False(cursor.MoveNext(out _));
    }

    [Fact]
    public void PreparedLabelRangeMetersTheRewindBeforePublishingSlots()
    {
        using var regex = new Utf8Regex(@"[\w.+-]+@[\w-]+\.[\w.-]+", policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = 16 }
        });
        var input = Encoding.UTF8.GetBytes(new string('a', 4096) + "@b.cd");
        var sentinel = new Utf8CaptureRange(0, 1);
        Utf8CaptureRange[] slots = [sentinel, sentinel];

        Assert.True(regex.IsMatch(input));
        var denied = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match(input, captures: slots));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, denied.ResourceKind);
        Assert.False(slots[0].Success);
        Assert.Equal(sentinel, slots[1]);
    }

    [Fact]
    public void VerifiedPrefixKeepsWorkBoundAndWideRepeatEndpoints()
    {
        using var regex = new Utf8Regex(@"Sherlock\s+Holmes", policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = 400 }
        });
        using var source = new CancellationTokenSource();
        var prefix = "Ω " + string.Concat(Enumerable.Repeat("Sherlock Holmer! ", 128));
        const string matchText = "Sherlock \nHolmes";
        var input = Encoding.UTF8.GetBytes(prefix + matchText);
        Assert.True(regex.IsMatch(input, source.Token));
        Assert.Equal(new Utf8CaptureRange(Encoding.UTF8.GetByteCount(prefix), input.Length),
            regex.Match(input, cancellationToken: source.Token));

        // Move a wider whitespace scalar across the bounded proof's edge.
        foreach (var spaces in new[] { 86, 87, 88 })
        {
            var value = "Sherlock" + new string(' ', spaces) + "\u2003Holmes";
            var bytes = Encoding.UTF8.GetBytes("Ω " + value + new string('!', 256));
            Assert.True(regex.IsMatch(bytes, source.Token));
            Assert.Equal(new Utf8CaptureRange(3, 3 + Encoding.UTF8.GetByteCount(value)),
                regex.Match(bytes, cancellationToken: source.Token));
        }
    }

    [Fact]
    public void RejectedPreparedPrefixKeepsAnOverlappingLiteral()
    {
        using var regex = new Utf8Regex(@"abab\s+end", policy: new() { ResourcePolicy = Funded });
        using var source = new CancellationTokenSource();
        Assert.True(regex.IsMatch("ababab end"u8, source.Token));
        Assert.False(regex.IsMatch("ababab en"u8, source.Token));
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[1];
        Assert.Equal(new Utf8CaptureRange(2, 10), regex.Match("ababab end"u8, 0, slots, source.Token));
        Assert.False(regex.Match("ababab end"u8, 0, slots, source.Token, endOffsetInBytes: 9).Success);
        Assert.False(slots[0].Success);
        Assert.False(regex.Match("aba"u8, 0, slots, source.Token).Success);
    }

    [Fact]
    public void PreparedLeadingLiteralKeepsAnUnresolvedSuffixAtTheFirstHit()
    {
        const string prefix = "Sherlock Holmes! 😀 ";
        var first = "Sherlock" + new string(' ', 180) + "\u2003Holmes";
        const string later = "Sherlock Holmes";
        var text = prefix + first + "! " + later;
        var input = Encoding.UTF8.GetBytes(text);
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var end = floor + Encoding.UTF8.GetByteCount(first);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(@"Sherlock\s+Holmes", policy: new() { ResourcePolicy = Funded });

        Assert.Equal(new Utf8CaptureRange(floor, end), regex.Match(input, floor, cancellationToken: token.Token));
        Assert.False(regex.Match(input, floor, cancellationToken: token.Token, endOffsetInBytes: end - 1).Success);
        using var cursor = regex.CreateMatchCursor(input, floor, token.Token);
        Assert.True(cursor.MoveNext(out var result));
        Assert.Equal(new Utf8CaptureRange(floor, end), result.Value);
        Assert.True(cursor.MoveNext(out result));
        Assert.Equal(new Utf8CaptureRange(input.Length - later.Length, input.Length), result.Value);
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData("http://?query", false)]
    [InlineData("https://host/path?query", true)]
    [InlineData("http://?query https://Ω/δρομή", true)]
    [InlineData("http://\u2003?", false)]
    public void PreparedOptionalLiteralRequiresTheFirstMemberOfTheNonemptyRun(string text, bool found)
    {
        const string pattern = @"https?://[^\s/?#]+[^\s?#]*(?:\?[^\s#]*)?";
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = Funded });
        Assert.Equal(found, regex.IsMatch(input, source.Token));
        var expected = Regex.Match(text, pattern);
        Assert.Equal(found, expected.Success);
        var actual = regex.Match(input, cancellationToken: source.Token);
        Assert.Equal(found, actual.Success);
        if (found) Utf8MatchSequence.AssertRange(actual, text, expected.Index, expected.Length);
    }

    [Fact]
    public void PreparedNegatedRunsKeepGreedyEndpointsAndWideFallback()
    {
        const string pattern = @"https?://[^\s/?#]+[^\s?#]*(?:\?[^\s#]*)?";
        var text = new string(' ', 260) + "http://?missing https://host/path?first?second#stop " +
            "https://😀.test/path https://a/" + new string('v', 130) + "#tail";
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern).Matches(text);
        using var source = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = Funded });
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[1];

        Assert.Equal(3, expected.Count);
        Assert.Equal(expected.Count, regex.Count(input, source.Token));
        var first = regex.Match(input, 0, slots, source.Token);
        Utf8MatchSequence.AssertRange(first, text, expected[0].Index, expected[0].Length);
        Assert.Equal(first, slots[0]);
        using var cursor = regex.CreateMatchCursor(input, cancellationToken: source.Token);
        foreach (Match match in expected)
        {
            Assert.True(cursor.MoveNext(out var result));
            Utf8MatchSequence.AssertRange(result.Value, text, match.Index, match.Length);
        }
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData("None !Word? Valid next", true)]
    [InlineData("None !Word? Valid!", false)]
    [InlineData("😀\u2003Valid next", true)]
    [InlineData("😀 Valid\u2003next", true)]
    public void BoundedLookaroundKeepsBothScalarGuards(string text, bool found)
    {
        const string pattern = @"(?<=\s)[A-Z][a-z]+(?=\s)";
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = Funded });
        Assert.Equal(found, regex.IsMatch(input, source.Token));
        var expected = Regex.Match(text, pattern);
        Assert.Equal(found, expected.Success);
        var actual = regex.Match(input, cancellationToken: source.Token);
        Assert.Equal(found, actual.Success);
        if (found) Utf8MatchSequence.AssertRange(actual, text, expected.Index, expected.Length);
    }

    [Theory]
    [InlineData(" ", " ", 2)]
    [InlineData("\u2003", " ", 180)]
    [InlineData(" ", "\u00a0", 2)]
    public void GuardedRangeKeepsItsFirstUnresolvedCandidateAndRightContext(string left, string right, int count)
    {
        var prefix = "Wrong!Wrong work! 😀" + left;
        var value = "Valid" + new string('a', count);
        var input = Encoding.UTF8.GetBytes(prefix + value + right + " Later ");
        var start = Encoding.UTF8.GetByteCount(prefix);
        var end = start + value.Length;
        var expected = new Utf8CaptureRange(start, end);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(@"(?<=\s)[A-Z][a-z]+(?=\s)", policy: new() { ResourcePolicy = Funded });
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[1];

        Assert.Equal(expected, regex.Match(input, 0, slots, token.Token));
        Assert.Equal(expected, slots[0]);
        Assert.Equal(expected, regex.Match(input, start, slots, token.Token));
        Assert.False(regex.Match(input, start, slots, token.Token, endOffsetInBytes: end).Success);
        Assert.False(slots[0].Success);
    }

    [Theory]
    [InlineData(@"\s[a-z]{0,12}ing[\s.,;]", "\u2003inging;")]
    [InlineData(@"\s[a-z]{0,12}ing\s", " inging\u2028")]
    [InlineData(@"\s[a-z]{0,12}ing[\sx]", " ingxing ")]
    [InlineData(@"\s[ab]{0,12}ing[\s.;]", " abing;")]
    public void WhitespaceWindowPreservesGreedySuffixesAndScalarFollowers(string pattern, string value)
    {
        var prefix = new string('!', 320) + "cing!";
        var text = prefix + value + "!";
        var input = Encoding.UTF8.GetBytes(text);
        var start = Encoding.UTF8.GetByteCount(prefix);
        var end = start + Encoding.UTF8.GetByteCount(value);
        var selected = new Utf8CaptureRange(start, end);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = Funded });
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[1];

        Assert.True(regex.IsMatch(input, token.Token));
        Assert.Equal(selected, regex.Match(input, 0, slots, token.Token));
        Assert.Equal(selected, slots[0]);
        Assert.Equal(1, regex.Count(input, token.Token));
        using var cursor = regex.CreateMatchCursor(input, cancellationToken: token.Token);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(selected, match.Value);
        Assert.False(cursor.MoveNext(out _));
    }

    [Fact]
    public void CompleteWhitespaceWindowPublishesWithNoBacktrackingStorage()
    {
        var prefix = new string('!', 320);
        var input = Encoding.UTF8.GetBytes(prefix + " walking,");
        using var regex = new Utf8Regex(@"\s[a-z]{0,12}ing[\s.,;]", policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkingBytes = 1 }
        });

        Assert.True(regex.IsMatch(input));
        Assert.Equal(new Utf8CaptureRange(prefix.Length, input.Length), regex.Match(input, 0));
        Assert.False(regex.Match(input, 0, endOffsetInBytes: input.Length - 1).Success);
    }

    [Theory]
    [InlineData(@"(?:ab[0-9]|a[a-z][0-4]|a[a-z]?[0-9][0-9])\.OK", "éab2.X ab8.OK a22.OK")]
    [InlineData(@"(?:ab[0-9]|a[a-z][0-4]|a[a-z]??[0-9][0-9])\.OK", "😀ab2.X ab8.OK a22.OK")]
    [InlineData(@"(?:ab[0-9]|ab[0-9]\.[a-z])\.OK", "éab2.x.OK")]
    [InlineData(@"(?:ab[0-9]|ab[0-9][.]?)\.OK", "😀ab2..OK")]
    public void AlternativesCanRetryAtADifferentDelimiterOnlyWhenTheirAtomsAdmitIt(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern).Matches(text);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled, new() { ResourcePolicy = Funded });

        Assert.True(regex.IsMatch(input, token.Token));
        Assert.Equal(expected.Count, regex.Count(input, token.Token));
        var match = regex.Match(input, cancellationToken: token.Token);
        var start = Encoding.UTF8.GetByteCount(text.AsSpan(0, expected[0].Index));
        Assert.Equal(new Utf8CaptureRange(start, start + Encoding.UTF8.GetByteCount(expected[0].Value)), match);
    }

    [Theory]
    [InlineData("[A-Z]", 3)]
    [InlineData("[ A-Z]", 4)]
    public void CountAfterLiteralFamilyPreservesWideRunsAndOverlappingFollowers(string follower, int count)
    {
        var pattern = @"\b(?:record|struct|class)\s+" + follower + @"\w+";
        var text = new string('!', 2046) + "érecord Ab!record Ab!struct\u3000Cd!class" +
            new string(' ', 17) + "Ef!class  lower!classAb!";
        using var source = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(pattern, policy: new() { ResourcePolicy = Funded });

        Assert.Equal(count, regex.Count(Encoding.UTF8.GetBytes(text), source.Token));
    }

    [Theory]
    [InlineData("[α-ω]{2}", "Ͽ", "λω")]
    [InlineData("[ࠀ-ࠇ]{2}", "ࠈ", "ࠀࠇ")]
    [InlineData("[😀-😇]{2}", "😈", "😀😇")]
    [InlineData("[α-ω]{2}|[😀-😇]{2}", "😈", "λω")]
    [InlineData("βλ|[😀-😇]{2}", "😈", "😀😇")]
    [InlineData("β?[😀-😇]{2}", "😈", "β😀😇")]
    public void UnicodeCandidatesSkipOtherStartsAndVerifyTheCompleteScalar(string pattern, string near, string value)
    {
        var prefix = new string('!', 2047) + near + near + new string('!', 4094);
        var input = Encoding.UTF8.GetBytes(prefix + value + "#" + value + "#");
        var firstStart = Encoding.UTF8.GetByteCount(prefix);
        var length = Encoding.UTF8.GetByteCount(value);
        var secondStart = firstStart + length + 1;
        using var source = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile($"(?<value>{pattern})(?![!?])", policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = 256 }
        });

        Assert.True(regex.IsMatch(input, source.Token));
        Assert.Equal(2, regex.Count(input, source.Token));
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        var match = regex.Match(input, 0, captures, source.Token);
        Assert.Equal(new Utf8CaptureRange(firstStart, firstStart + length), match);
        Assert.Equal(match, captures[1]);

        using var cursor = regex.CreateMatchCursor(input, firstStart + length, source.Token);
        Assert.True(cursor.MoveNext(out var retained));
        Assert.Equal(new Utf8CaptureRange(secondStart, secondStart + length), retained.Value);
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(value, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData(63)] [InlineData(64)] [InlineData(65)]
    public void AlignedDelimiterRewindKeepsWideMembersAndTheRequestedFloor(int asciiLength)
    {
        const string prefix = "🚧junk://a ";
        var ascii = new string('s', asciiLength);
        var scheme = "a𐐀" + ascii;
        var input = Encoding.UTF8.GetBytes(prefix + scheme + "://é/");
        using var regex = Utf8Regex.Compile(@"(?<scheme>[\w]+)://[^/\s?#]+[^\s?#]+", policy: new() { ResourcePolicy = Funded });
        Assert.Equal(1, regex.Count(input));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var retained));
        Assert.Equal(Encoding.UTF8.GetByteCount(prefix), retained.Value.StartOffsetInBytes);
        Assert.Equal(scheme, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));

        var floor = Encoding.UTF8.GetByteCount(prefix + "a𐐀") + 1;
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        var selected = regex.Match(input, floor, captures);
        Assert.Equal((floor, input.Length), (selected.StartOffsetInBytes, selected.EndOffsetInBytes));
        Assert.Equal((floor, floor + asciiLength - 1), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
    }

    [Fact]
    public void DisprovedDelimiterSuffixesSpendNoWorkOnTheirLongPrefixes()
    {
        var prefix = string.Concat(Enumerable.Repeat(new string('s', 129) + "://a ", 16));
        const string scheme = "décor";
        var input = Encoding.UTF8.GetBytes(prefix + scheme + "://a𐐀/");
        using var regex = Utf8Regex.Compile(@"(?<scheme>[\w]+)://[^/\s?#]+[^\s?#]+", policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = 160 }
        });
        Assert.True(regex.IsMatch(input));
        Assert.Equal(1, regex.Count(input));

        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
        var found = regex.Match(input, startOffsetInBytes: 64, captures: slots);
        Assert.Equal((prefix.Length, input.Length), (found.StartOffsetInBytes, found.EndOffsetInBytes));
        Assert.Equal(scheme, Encoding.UTF8.GetString(input.AsSpan(slots[1].StartOffsetInBytes,
            slots[1].EndOffsetInBytes - slots[1].StartOffsetInBytes)));
    }

    [Theory]
    [InlineData(61)] [InlineData(62)] [InlineData(63)] [InlineData(64)]
    public void CaptureFreeRangeReusesItsRewoundUnicodePrefix(int asciiTailLength)
    {
        const string prefix = "😀 ";
        // Exercise a four-byte letter at and across the 64-byte block edge.
        var scheme = string.Concat(Enumerable.Repeat("élém", 1024)) + "𐐀" + new string('a', asciiTailLength);
        var input = Encoding.UTF8.GetBytes(prefix + scheme + "://é/lien?q=東#Ω");
        var start = Encoding.UTF8.GetByteCount(prefix);
        var floor = start + "é"u8.Length;
        using var live = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?",
            policy: new() { ResourcePolicy = Funded with { MaxWorkSteps = 256 } });

        Assert.Equal(new Utf8CaptureRange(start, input.Length), regex.Match(input, cancellationToken: live.Token));
        using var cursor = regex.CreateMatchCursor(input, floor, live.Token);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(new Utf8CaptureRange(floor, input.Length), match.Value);
    }

    [Theory]
    [InlineData(1)] [InlineData(64)] [InlineData(65)]
    public void PreparedDelimiterSuffixUsesTheAlignedNeedleLength(int length)
    {
        var delimiter = new string('!', length);
        var prefix = "😀é" + delimiter + "a ";
        const string scheme = "décor";
        var input = Encoding.UTF8.GetBytes(prefix + scheme + delimiter + "é/");
        using var regex = Utf8Regex.Compile(@"(?<scheme>[\w]+)" + delimiter + @"[^/\s?#]+[^\s?#]+",
            policy: new() { ResourcePolicy = Funded });

        var start = Encoding.UTF8.GetByteCount(prefix);
        Assert.False(regex.IsMatch(input.AsSpan(0, start)));
        Assert.True(regex.IsMatch(input));
        Assert.Equal(1, regex.Count(input));
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
        Assert.Equal(new Utf8CaptureRange(start, input.Length), regex.Match(input, captures: slots));
        Assert.Equal(new Utf8CaptureRange(start, start + Encoding.UTF8.GetByteCount(scheme)), slots[1]);
    }

    [Fact]
    public void RequiredDelimiterSkipsBroadAsciiStartsAndKeepsCaptures()
    {
        const string pattern = @"(?<local>[A-Za-z0-9._%+-]+)@[A-Za-z0-9.-]+\.[A-Za-z]{2,}";
        var prefix = "é😀 " + new string('q', 8192) + " a@b.c 😀 ";
        var input = Encoding.UTF8.GetBytes(prefix + "hunter@host.io aa@xx.io");
        var floor = Encoding.UTF8.GetByteCount("é😀 ") + 1;
        var first = Encoding.UTF8.GetByteCount(prefix);
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(pattern, policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = 512 }
        });

        using var cursor = regex.CreateMatchCursor(input, floor, token.Token);
        Assert.True(cursor.MoveNext(out var selected));
        Assert.Equal(new Utf8CaptureRange(first, first + "hunter@host.io".Length), selected.Value);
        Assert.True(selected.GetGroupMemory(1).Span.SequenceEqual("hunter"u8));
        Assert.True(cursor.MoveNext(out var later));
        Assert.True(later.GetGroupMemory(1).Span.SequenceEqual("aa"u8));
        Assert.False(cursor.MoveNext(out _));
        Assert.True(selected.GetGroupMemory(1).Span.SequenceEqual("hunter"u8));
    }

    [Theory]
    [InlineData(@"(?<value>\d+(?:\.\d+)?)", "", "𝟚.٣")]
    [InlineData(@"\b(?<value>(?:\d{1,3}\.){3}\d{1,3})\b", "é1.2.3.4,", "𝟘.١.2.３")]
    public void DecimalCandidatesPreserveScalarStartsCapturesAndBoundary(string pattern, string before, string value)
    {
        // The first wider scalar straddles a search block. It is a candidate
        // lead but not a digit. IP must also reject the ASCII digits after é.
        var prefix = new string('!', 2047) + "😀" + new string('!', 16_384) + before;
        var input = Encoding.UTF8.GetBytes(prefix + value + "!");
        var start = Encoding.UTF8.GetByteCount(prefix);
        var end = start + Encoding.UTF8.GetByteCount(value);
        using var source = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(pattern, policy: new()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with { MaxWorkSteps = 1024 }
        });
        Assert.True(regex.IsMatch(input, source.Token));
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        var found = regex.Match(input, 0, captures, source.Token);
        Assert.Equal((start, end), (found.StartOffsetInBytes, found.EndOffsetInBytes));
        Assert.Equal((start, end), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));

        using var cursor = regex.CreateMatchCursor(input, 2047, source.Token);
        Assert.True(cursor.MoveNext(out var retained));
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(value, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }

    [Fact]
    public void DecimalPrefixDoesNotSkipAnExplicitAsciiClassMember()
    {
        using var regex = Utf8Regex.Compile(@"[\dx]+(?:\.\d+)?");
        var input = Encoding.UTF8.GetBytes("!x.𝟙!");
        var result = regex.Match(input);
        Assert.Equal((1, input.Length - 1), (result.StartOffsetInBytes, result.EndOffsetInBytes));
    }

    [Theory]
    [InlineData("𝟚.٣", "𝟚.٣")]
    [InlineData("𝟚.😀", "𝟚")]
    [InlineData("𝟚.", "𝟚")]
    public void TerminalDecimalKeepsScalarRangesAndRequiresAFractionDigit(string initial, string expected)
    {
        const int start = 2047;
        var input = Encoding.UTF8.GetBytes(new string('!', start) + initial + "! ٤.5");
        var selectedEnd = start + Encoding.UTF8.GetByteCount(initial);
        var first = new Utf8CaptureRange(start, start + Encoding.UTF8.GetByteCount(expected));
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(@"\d+(?:\.\d+)?", policy: new() { ResourcePolicy = Funded });
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[1];

        Assert.True(regex.IsMatch(input, token.Token));
        Assert.Equal(first, regex.Match(input, 0, slots, token.Token, endOffsetInBytes: selectedEnd));
        Assert.Equal(first, slots[0]);
        Assert.Equal(new Utf8CaptureRange(start, start + "𝟚"u8.Length),
            regex.Match(input, start, slots, token.Token, endOffsetInBytes: start + "𝟚."u8.Length));
        using var cursor = regex.CreateMatchCursor(input, cancellationToken: token.Token);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(first, match.Value);
        Assert.True(cursor.MoveNext(out match));
        Assert.Equal(new Utf8CaptureRange(input.Length - "٤.5"u8.Length, input.Length), match.Value);
        Assert.False(cursor.MoveNext(out _));
    }

    [Fact]
    public void DecimalCandidateScanObservesTheOriginalTokenAndWorkLimit()
    {
        const string pattern = @"\d+(?:\.\d+)?";
        var input = new byte[2048 * 96];
        input.AsSpan().Fill((byte)'!');
        using var regex = Utf8Regex.Compile(pattern, policy: new()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with { MaxWorkSteps = 32 }
        });
        var limit = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, limit.ResourceKind);

        using var source = new CancellationTokenSource();
        using var interruptible = Utf8Regex.Compile(pattern, policy: new()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded
        });
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        try
        {
            var canceled = Assert.Throws<OperationCanceledException>(() => interruptible.IsMatch(input, source.Token));
            Assert.Equal(source.Token, canceled.CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(@"\p{Lu}{2,4}\s+[\s\S]{0,8}result", " 😀result! ")]
    [InlineData(@"\p{Lu}+@", "é😀@! ")]
    public void RejectedWindowsAdvanceWithoutVmWorkspace(string pattern, string tail)
    {
        var input = Encoding.UTF8.GetBytes(new string('!', 260) + string.Concat(Enumerable.Repeat(tail, 64)));
        byte[] storage = [0xff, .. input, 0xff];
        var regex = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with
            {
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        using var live = new CancellationTokenSource();
        Assert.Equal(0, regex.Count(storage.AsSpan(1, input.Length), live.Token));
    }

    [Theory]
    [InlineData(@"\p{Lu}{2,4}\s+[\s\S]{0,8}result", " 😀result! AB 😀result! ")]
    [InlineData(@"\p{Lu}+@", "é😀@! É@! ")]
    public void RejectedWindowsKeepLaterValidStarts(string pattern, string tail)
    {
        var input = Encoding.UTF8.GetBytes(new string('!', 260) + tail);
        var regex = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded
        });
        var cursor = regex.EnumerateMatches(input);
        var expected = 0;
        while (cursor.MoveNext()) expected++;
        Assert.Equal(1, expected);
        Assert.Equal(expected, regex.Count(input));
        using var live = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input, live.Token));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RejectedWindowPrefixKeepsOriginalInterruptionWithoutVmAdmission(bool expire)
    {
        const int spaces = 200000;
        const string pattern = @"\p{Lu}+\s+result";
        var input = Encoding.UTF8.GetBytes("😀" + new string(' ', spaces) + "result");
        using var regex = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with
            {
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        // The tiny cap rules out a VM attempt. Cancel or expire after the
        // delimiter search and the block-charged backwards whitespace scan.
        const ulong threshold = spaces / 2;
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.IsType<RegexMatchTimeoutException>(Record.Exception(() => regex.Count(input, token.Token)));
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(@"\w+\s+Holmes")]
    [InlineData(@"[\w]+\s*Holmes")]
    [InlineData(@"(?<w>\w+)\s+(?<h>Holmes)")]
    [InlineData(@"\w+\s+H(?<x>olmes)")]
    [InlineData(@"(?<w>\w+)\s+Holmes\k<w>")]
    [InlineData(@"\p{L}+\s+Holmes")]
    [InlineData(@"[aé😀]+\s+Holmes")]
    [InlineData(@".+\s+Holmes")]
    [InlineData(@"[a ]+\s+Holmes")]
    [InlineData(@"[^x]+\s+Holmes")]
    [InlineData(@"\w+\s+Holmes|x")]
    [InlineData(@"\w+@")]
    [InlineData(@"[aé😀]+@")]
    [InlineData(@"[^!]{1,4}@")]
    [InlineData(@"[\w@]+@")]
    [InlineData(@"[a ]+@")]
    [InlineData(@"(?<w>\w+)@\k<w>")]
    public void ByteStartEnumerationAgreesWithScalarOracleAtFloorsAndTails(string pattern)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded
            });
            var oracle = Utf8ScalarCaptureFixture.Project(new Regex(pattern, options));
            foreach (var tail in new[] { "", "Holmes", "ab Holmes", "ab Holmesb", "é\u0301\u2003Holmes",
                "𐐀\u00a0Holmes", "😀 Holmes", "aé😀\tHolmes", "aHolmes Holmes",
                "ab\nHolmes Holmes", "ab Holmesx cd Holmes", "ab H\né Holmes",
                "é@ word@", "ab@b", "😀é@ a@@a", "aa @ aaa@", "abcde@" })
            {
                var text = new string('!', 260) + tail + new string('!', 260);
                var input = Encoding.UTF8.GetBytes(text);
                var projected = new Utf8ScalarCaptureFixture.Subject(text);
                byte[] storage = [0xff, .. input, 0xff];
                using var live = new CancellationTokenSource();
                foreach (var start in new[] { 0, 1, 259, 260, char.IsLowSurrogate(text[261]) ? 262 : 261, text.Length })
                {
                    var expected = oracle.Count(projected.Projected, Array.IndexOf(projected.Units, start));
                    Assert.Equal(expected, Utf8MatchSequence.Count(regex.EnumerateMatches(storage.AsSpan(1, input.Length), Encoding.UTF8.GetByteCount(text.AsSpan(0, start)))));
                    Assert.Equal(expected, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), live.Token)));
                }
            }
        }
        // A later start inside the same class run wins after the first capture
        // makes the backreference fail. Rewinding is not match acceptance.
        var captured = new Utf8Regex(@"(?<w>\w+)\s+Holmes\k<w>");
        var witness = Encoding.UTF8.GetBytes(new string('!', 260) + "ab Holmesb");
        Assert.Equal(1, captured.Count(witness));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(captured.CreateMatchCursor(witness)).GetGroupMemory(captured.GroupNumberFromUtf8Name("w"u8)).Span));
    }

    [Fact]
    public void CandidateSearchPreservesBoundedWorkAndVmStorageAdmission()
    {
        var input = Encoding.UTF8.GetBytes(new string('!', 100000) + "é Holmes");
        var efficient = new Utf8Regex(@"\w+\s+Holmes", options: RegexOptions.Compiled, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with
            {
                MaxWorkSteps = 4096
            }
        });
        Assert.Equal(1, efficient.Count(input));
        using var live = new CancellationTokenSource();
        var match = efficient.Match(input, 0, [], live.Token);
        Assert.Equal((100000, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        using var cursor = efficient.CreateMatchCursor(input, 17, live.Token);
        Assert.True(cursor.MoveNext(out var current));
        Assert.True(current.GetValueMemory().Span.SequenceEqual("é Holmes"u8));
        Assert.False(cursor.MoveNext(out _));
        var tight = new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with { MaxWorkingBytes = 1 }
        };
        // The disjoint word and whitespace runs can complete without rewind
        // storage. A dependent capture still needs the admitted VM workspace.
        Assert.Equal(1, new Utf8Regex(@"\w+\s+Holmes", options: RegexOptions.None, policy: tight).Count(input));
        var dependentInput = Encoding.UTF8.GetBytes(new string('!', 100000) + "é Holmesé");
        const string dependent = @"(?<word>\w+)\s+Holmes\k<word>";
        Assert.Equal(1, new Utf8Regex(dependent).Count(dependentInput));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new Utf8Regex(dependent, options: RegexOptions.None, policy: tight).Count(dependentInput)).ResourceKind);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new Utf8Regex(@"\w+\s+Holmes", options: RegexOptions.None, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = Regex.InfiniteMatchTimeout,
                    ResourcePolicy = Funded with
                    {
                        MaxWorkSteps = 1
                    }
                }).Count(input)).ResourceKind);
    }

    [Fact]
    public void WhitespaceRewindCrossesBlockEdgesWithinWorkLimit()
    {
        const string prefix = "😀!";
        var text = prefix + "é\u0085" + new string(' ', 2046) + "\u3000" + new string(' ', 63) + "Holmes";
        var input = Encoding.UTF8.GetBytes(text);
        using var regex = new Utf8Regex(@"\w+\s+Holmes", policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = 256 }
        });
        using var token = new CancellationTokenSource();
        var start = Encoding.UTF8.GetByteCount(prefix);
        using var cursor = regex.CreateMatchCursor(input, start, token.Token);
        Assert.True(cursor.MoveNext(out var match));
        var complete = match.GetCaptures(0)[0];
        Assert.Equal((start, input.Length), (complete.StartOffsetInBytes, complete.EndOffsetInBytes));
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData(2045)]
    [InlineData(2046)]
    [InlineData(2047)]
    public void PreparedDelimiterKeepsByteFloorsAndLaterDependentCaptures(int delimiterOffset)
    {
        const string before = "ab://b 😀 ";
        var floor = Encoding.UTF8.GetByteCount(before);
        var delimiterStart = floor + delimiterOffset;
        var prefix = before + new string(':', delimiterOffset - "éab"u8.Length) + "éab";
        var input = Encoding.UTF8.GetBytes(prefix + "://b ! ok://ok :/");
        using var regex = Utf8Regex.Compile(@"(?<scheme>\w+)://\k<scheme>", policy: new()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded
        });
        using var live = new CancellationTokenSource();
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        var result = regex.Match(input, floor, captures, live.Token);
        Assert.True(result.Success);
        Assert.Equal((delimiterStart - 1, delimiterStart + 4), (result.StartOffsetInBytes, result.EndOffsetInBytes));
        Assert.Equal((delimiterStart - 1, delimiterStart), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.Equal(3, regex.Count(input, live.Token));

        using var cursor = regex.CreateMatchCursor(input, floor, live.Token);
        Assert.True(cursor.MoveNext(out var retained));
        Assert.True(cursor.MoveNext(out var next));
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal("b", Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        Assert.Equal("ok://ok", Encoding.UTF8.GetString(next.GetValueMemory().Span));
    }

    [Theory]
    [InlineData("Holmes")]
    [InlineData("xHolmes")]
    public void RepeatedClassPrefixRetainsStartsBeforeAnEarlierLiteral(string precedingWord)
    {
        const string pattern = @"(?<first>\w+)\s+\w+\s+Holmes";
        var first = "é\u0085" + precedingWord + "\u2028Holmes";
        const string second = "Ω\u3000東\tHolmes";
        var prefix = new string('!', 100_000) + "😀";
        var input = Encoding.UTF8.GetBytes(prefix + first + "!" + second);
        using var regex = Utf8Regex.Compile(pattern, policy: new()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with { MaxWorkSteps = 4096 }
        });
        using var source = new CancellationTokenSource();
        var match = regex.Match(input, 17, [], source.Token);
        Assert.Equal((Encoding.UTF8.GetByteCount(prefix), Encoding.UTF8.GetByteCount(prefix + first)),
            (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal(2, regex.Count(input, source.Token));

        using var cursor = regex.CreateMatchCursor(input, 17, source.Token);
        Assert.True(cursor.MoveNext(out var initial));
        Assert.True(cursor.MoveNext(out var next));
        Assert.False(cursor.MoveNext(out _));
        Assert.True(initial.GetValueMemory().Span.SequenceEqual(Encoding.UTF8.GetBytes(first)));
        Assert.True(initial.GetGroupMemory(1).Span.SequenceEqual("é"u8));
        Assert.True(next.GetValueMemory().Span.SequenceEqual("Ω\u3000東\tHolmes"u8));
    }

    [Theory]
    [InlineData(2042)]
    [InlineData(2043)]
    [InlineData(2044)]
    public void RequiredLastRepeatRejectsAnInteriorDelimiterBeforeRewinding(int delimiterStart)
    {
        var miss = "a b " + new string('x', delimiterStart - 4) + "Holmes";
        const string hit = "é\u0085東\u3000Holmes";
        var input = Encoding.UTF8.GetBytes(miss + "! " + hit);
        using var regex = Utf8Regex.Compile(@"\w+\s+\w+\s+Holmes", policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = 128 }
        });
        using var live = new CancellationTokenSource();
        Assert.False(regex.IsMatch(input.AsSpan(0, miss.Length), live.Token));
        Assert.Equal(0, regex.Count(input.AsSpan(0, miss.Length), live.Token));
        Assert.True(regex.IsMatch(input, live.Token));
        Assert.Equal(1, regex.Count(input, live.Token));
        var result = regex.Match(input, 0, [], live.Token);
        Assert.Equal((miss.Length + 2, input.Length), (result.StartOffsetInBytes, result.EndOffsetInBytes));
    }

    [Fact]
    public void OptionalLastRepeatCanMatchAnUnspacedDelimiter()
    {
        var input = Encoding.UTF8.GetBytes(new string('!', 256) + "é bHolmes");
        using var regex = Utf8Regex.Compile(@"\w+\s+\w+\s*Holmes", policy: new() { ResourcePolicy = Funded });
        Assert.Equal(1, regex.Count(input));
        var result = regex.Match(input, 0);
        Assert.Equal((256, input.Length), (result.StartOffsetInBytes, result.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(6, 6)]
    [InlineData(9, -1)]
    public void DisjointRunsNarrowTheFirstAttemptToTheRequestedFloor(int floor, int start)
    {
        var input = "é Ω 中 Sherlock Holmes"u8;
        using var regex = Utf8Regex.Compile(@"\w+\s+\w+\s+Holmes", policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = 64 }
        });
        var result = regex.Match(input, floor);
        Assert.Equal(start >= 0, result.Success);
        if (start >= 0)
            Assert.Equal((start, input.Length), (result.StartOffsetInBytes, result.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(160)]
    public void TerminalWhitespaceWindowKeepsTheScalarFloorAndFirstCompleteRun(int runLength)
    {
        var prefix = new string('!', 300) + "Holmes! ";
        var first = "𐐀" + new string('a', runLength) + "\u2003Holmes";
        const string second = "é Holmes";
        var input = Encoding.UTF8.GetBytes(prefix + first + "! " + second);
        var start = Encoding.UTF8.GetByteCount(prefix);
        var floor = start + "𐐀"u8.Length;
        var end = start + Encoding.UTF8.GetByteCount(first);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(@"\w+\s+Holmes", policy: new() { ResourcePolicy = Funded });
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[1];

        Assert.True(regex.IsMatch(input, token.Token));
        Assert.Equal(new Utf8CaptureRange(start, end), regex.Match(input, 0, slots, token.Token));
        Assert.Equal(new Utf8CaptureRange(floor, end), regex.Match(input, floor, slots, token.Token));
        using var cursor = regex.CreateMatchCursor(input, floor, token.Token);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(new Utf8CaptureRange(floor, end), match.Value);
        Assert.True(cursor.MoveNext(out match));
        Assert.Equal(new Utf8CaptureRange(input.Length - "é Holmes"u8.Length, input.Length), match.Value);
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData(@"\w+\s+\w+\s+Holmes", "word Holmes! é Ω Holmes!", "é Ω Holmes")]
    [InlineData(@"\w+\s+\w{2,}\s+Holmes", "a b Holmes! a bc Holmes!", "a bc Holmes")]
    [InlineData("a+b+bbb", "!aabbbbbb!", "aabbbbbb")]
    public void TerminalRunRangeRequiresEveryMinimumAndTheGreedyEnd(string pattern, string text, string value)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var start = Encoding.UTF8.GetByteCount(text.AsSpan(0, text.IndexOf(value, StringComparison.Ordinal)));
        var expected = new Utf8CaptureRange(start, start + Encoding.UTF8.GetByteCount(value));
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new() { ResourcePolicy = Funded });
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[1];

        Assert.True(regex.IsMatch(input, token.Token));
        Assert.Equal(expected, regex.Match(input, 0, slots, token.Token));
        Assert.Equal(expected, slots[0]);
        using var cursor = regex.CreateMatchCursor(input, cancellationToken: token.Token);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(expected, match.Value);
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData(96)]
    [InlineData(512)]
    public void DisjointRunNeedsOnlyOneTailAttemptPerDelimiter(int runLength)
    {
        var input = Encoding.UTF8.GetBytes(new string('w', runLength) + "://x ! é://ab");
        var hit = input.Length - "é://ab"u8.Length;
        using var regex = Utf8Regex.Compile(@"\w+://[a-z][a-z]", policy: new()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with { MaxWorkSteps = (ulong)(4 * runLength) }
        });
        using var live = new CancellationTokenSource();
        Assert.True(regex.IsMatch(input, live.Token));
        var found = regex.Match(input, 0, [], live.Token);
        Assert.Equal((hit, input.Length), (found.StartOffsetInBytes, found.EndOffsetInBytes));
        if (input.Length >= 256) Assert.Equal(1, regex.Count(input, live.Token));
        Assert.False(regex.IsMatch(input.AsSpan(0, hit), live.Token));
    }

    [Theory]
    [InlineData(@"(?<w>\w+)\s+Holmes\k<w>", "ab Holmesb")]
    [InlineData(@"\w+@", "é@")]
    [InlineData(@"(?<w>\w+)@\k<w>", "ab@b")]
    public void EveryWorkCutoffRetainsTheOriginalScratchLoan(string pattern, string tail)
    {
        var regex = new Utf8Regex(pattern);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(new string('!', 260) + tail);
        var plan = regex.Inspection.PreparedRegex.SearchPlan;
        var complete = new Utf8SliceInvocation(new(100000, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(complete.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
            new int[program.GroupCount], ref complete, plan);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(1, result.Count);
        Assert.Equal(13UL, complete.Ledger.ReservedBytes);
        for (ulong cap = 1; cap <= complete.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            result = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
                new int[program.GroupCount], ref invocation, plan);
            Assert.Equal(cap < complete.Budget.Steps ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None, result.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public void OriginalInterruptionCoversSearchBothRewindsAndFailedCandidates(int phase, bool expire)
    {
        var text = phase switch
        {
            // Leave enough tail search to reach a checkpoint after a fast initial word match.
            0 => "word Holmes!" + new string('!', 1 << 20),
            1 => new string('x', 200000) + " Holmes",
            2 => "word" + new string(' ', 200000) + "Holmes",
            3 => string.Concat(Enumerable.Repeat("word Holmesx ", 16000)),
            _ => string.Concat(Enumerable.Repeat("élém", 2048)) + "://host/a",
        };
        var input = Encoding.UTF8.GetBytes(text);
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var pattern = phase == 4 ? @"[\w]+://[^/\s?#]+[^\s?#]+" : @"\w+\s+Holmes" + (phase == 3 ? "(?=!)" : "");
        using var regex = new Utf8Regex(pattern, options: RegexOptions.Compiled, policy: new Utf8RegexPolicy
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded
        });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 128) return;
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        Exception? failure = null;
        try
        {
            if (phase == 4) regex.Match(input, cancellationToken: cancellation.Token);
            else regex.Count(input, cancellation.Token);
        }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(entered);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
    }

    [Fact]
    public void FinalFailedSearchCannotPublishAfterItsLastDeadlinePollCancelsTheToken()
    {
        var reads = 0;
        var cancelAt = int.MaxValue;
        using var cancellation = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == cancelAt) cancellation.Cancel();
            return 0;
        });
        var regex = new Utf8Regex(@"\w+\s+Holmes", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1),
            ResourcePolicy = Funded
        });
        var input = Encoding.UTF8.GetBytes("word Holmes" + new string('!', 8192));
        Assert.Equal(1, regex.Count(input, cancellation.Token));
        Assert.True(reads > 1);
        cancelAt = reads;
        reads = 0;
        Assert.Equal(cancellation.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }

    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 4000000, MaxDepth = 4096, MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 };
}
