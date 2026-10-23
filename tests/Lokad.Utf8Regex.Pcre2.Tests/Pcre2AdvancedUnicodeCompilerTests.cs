using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2AdvancedUnicodeCompilerTests
{
    [Fact]
    public void ExtendedGraphemeAtomFollowsSelectedUnicodeClusterRules()
    {
        var regex = new Utf8Pcre2Regex(@"\X");
        var input = Encoding.UTF8.GetBytes("a\u0301🇫🇷👩🏽‍💻\r\n한");

        Assert.Equal(5, regex.Count(input));

        var matches = regex.EnumerateMatches(input);
        Assert.True(matches.MoveNext());
        Assert.Equal("a\u0301", matches.Current.GetValueString());
        Assert.True(matches.MoveNext());
        Assert.Equal("🇫🇷", matches.Current.GetValueString());
        Assert.True(matches.MoveNext());
        Assert.Equal("👩🏽‍💻", matches.Current.GetValueString());
        Assert.True(matches.MoveNext());
        Assert.Equal("\r\n", matches.Current.GetValueString());
        Assert.True(matches.MoveNext());
        Assert.Equal("한", matches.Current.GetValueString());
        Assert.False(matches.MoveNext());
    }

    [Theory]
    [InlineData("각", 1)]
    [InlineData("क्\u200Dष", 1)]
    [InlineData("1️⃣", 1)]
    [InlineData("🇫🇷🇩🇪🇮", 3)]
    public void ExtendedGraphemeAtomCoversHangulIndicKeycapAndRegionalIndicators(
        string inputText,
        int expectedCount)
    {
        Assert.Equal(expectedCount, new Utf8Pcre2Regex(@"\X").Count(Encoding.UTF8.GetBytes(inputText)));
    }

    public static IEnumerable<object[]> PropertyBoundaries()
    {
        yield return [new[] { "a\u094D", "b" }];
        yield return [new[] { "\u0915\u094D", "a" }];
        yield return [new[] { "😀\u200D", "🇦" }];
        yield return [new[] { "🇦\u200D", "😀" }];
        yield return [new[] { "a\u0E33" }];
        yield return [new[] { "a\u0EB3" }];
        yield return [new[] { "a", "\u102B" }];
        yield return [new[] { "😀\u200D\u200D", "😀" }];
        yield return [new[] { "a\u200D", "😀" }];
        yield return [new[] { "\u0915\u094D\u200D\u0937" }];
        yield return [new[] { "\u0915\u094D\u034F\u0915" }];
        yield return [new[] { "\u0915\u094D\u200C", "\u0915" }];
        yield return [new[] { "\u0915\u0BCD", "\u0915" }];
        yield return [new[] { "a\u0301", "\r\n", "각", "🇫🇷", "🇩" }];
        yield return [new[] { "\u0600a", "x" }];
    }

    [Theory] [MemberData(nameof(PropertyBoundaries))]
    public void GraphemePropertiesPreservePublicRangesCapturesAndReplacement(string[] segments)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(segments));
        foreach (var finite in new[] { false, true })
        foreach (var pattern in new[] { @"\X", @"(?=\X)\X", @"(?<g>\X)" })
        {
            var regex = new Utf8Pcre2Regex(pattern, default, default,
                finite ? new() { MatchLimit = 1_000_000, DepthLimit = 1024, HeapLimitInBytes = 16 << 20, ResultLimitInBytes = 16 << 20 } : default,
                Timeout.InfiniteTimeSpan);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(segments.Length, regex.Count(input));
            Assert.Equal(segments[0], regex.Match(input).GetValueString());
            var cursor = regex.EnumerateMatches(input);
            var offset = 0;
            foreach (var segment in segments)
            {
                Assert.True(cursor.MoveNext());
                Assert.Equal(offset, cursor.Current.StartOffsetInBytes);
                offset += Encoding.UTF8.GetByteCount(segment);
                Assert.Equal(offset, cursor.Current.EndOffsetInBytes);
                Assert.Equal(segment, cursor.Current.GetValueString());
            }
            Assert.False(cursor.MoveNext());
            Assert.Equal(input.Length, offset);
            Assert.Equal(string.Concat(segments.Select(text => $"<{text}>")), regex.ReplaceToString(input, "<$0>"));
            var destination = new byte[segments.Length];
            Assert.Equal(System.Buffers.OperationStatus.Done, regex.TryReplace(input, "G"u8, destination, out var written));
            Assert.Equal(segments.Length, written);
            Assert.Equal(new string('G', segments.Length), Encoding.UTF8.GetString(destination));
            if (pattern.Contains("?<g>"))
            {
                Assert.True(regex.MatchDetailed(input).TryGetFirstSetGroup("g", out var group));
                Assert.Equal(segments[0], group.GetValueString());
            }
        }
        // The existing Unicode Indic-conjunct rule remains supported even where
        // native PCRE2 10.47 splits a conjunct; property predicates are now exact.
        var repeated = new Utf8Pcre2Regex(@"^\X+$");
        Assert.True(repeated.IsMatch(input));
        Assert.Equal(1, repeated.Count(input));
    }

    [Fact]
    public void ExtendedGraphemeAtomComposesWithBacktrackingAndReplacement()
    {
        var regex = new Utf8Pcre2Regex(@"^(?:\X){3}$");
        var input = Encoding.UTF8.GetBytes("a\u0301🇫🇷👩🏽‍💻");

        Assert.True(regex.IsMatch(input));
        Assert.Equal($"<{Encoding.UTF8.GetString(input)}>", regex.ReplaceToString(input, "<$0>"));
        Assert.Equal(
            "GG",
            new Utf8Pcre2Regex(@"\X").ReplaceToString(Encoding.UTF8.GetBytes("a\u0301🇫🇷"), "G"));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData("a+b?[x]")]
    [InlineData("other")]
    public void QuoteModeComposesInsideGenericAlternation(string inputText)
    {
        var regex = new Utf8Pcre2Regex(@"^(?:\Qa+b?[x]\E|other)$");

        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(inputText)));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void QuantifierAfterQuoteModeAppliesToTheFinalQuotedLiteral()
    {
        var regex = new Utf8Pcre2Regex(@"^(?:\Qab\E+|x)$");

        Assert.True(regex.IsMatch("abbb"u8));
        Assert.False(regex.IsMatch("abab"u8));
    }

    [Theory]
    [InlineData("π")]
    [InlineData("😀")]
    public void NamedCodePointEscapesComposeInsideGenericAlternation(string inputText)
    {
        var regex = new Utf8Pcre2Regex(@"^(?:\N{U+03C0}|\N{U+1F600})$");

        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(inputText)));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void AdvancedUnicodeProgramsAreSafeForConcurrentCalls()
    {
        var regex = new Utf8Pcre2Regex(@"^(?:\X){3}$");
        var input = Encoding.UTF8.GetBytes("a\u0301🇫🇷👩🏽‍💻");
        var failures = 0;

        Parallel.For(0, 1_000, _ =>
        {
            if (!regex.IsMatch(input))
            {
                Interlocked.Increment(ref failures);
            }
        });

        Assert.Equal(0, failures);
    }

    [Fact]
    public void GraphemeSearchSkipsWholeClustersAndAllocatesNothingAfterWarmup()
    {
        var regex = new Utf8Pcre2Regex(@"\Xz");
        var input = Encoding.UTF8.GetBytes("a" + new string('\u0301', 2_048));
        _ = regex.IsMatch(input);
        _ = regex.Count(input);

        var matched = false;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            matched |= regex.IsMatch(input);
            matched |= regex.Count(input) != 0;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.False(matched);
        Assert.Equal(0, allocated);
    }
}
