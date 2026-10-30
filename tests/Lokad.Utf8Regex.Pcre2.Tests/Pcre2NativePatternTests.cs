using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2NativePatternTests
{
    [Theory]
    [InlineData("é😀\0", "xé😀\0z")]
    [InlineData(@"(?<名>😀){1,2}\k<名>", "a😀😀😀b")]
    [InlineData(@"(?|(a)|(b))(c)", "zbcac")]
    [InlineData(@"(?J)(?<x>a)|(?<x>b)", "zab")]
    [InlineData(@"(?<w>ab+)(?&w)", "zabbabbbz")]
    [InlineData(@"(a)?(?(1)b|c)", "abcz")]
    [InlineData(@"(?(?=a)a|b)", "abz")]
    [InlineData(@"(?>ab+)c", "xxabbbdzz")]
    [InlineData(@"a++a", "aaaa")]
    [InlineData(@"(?x:é\ 😀)", "xé 😀z")]
    [InlineData(@"(?<=é)(😀|x)", "é😀éx")]
    [InlineData(@"[é😀]{1,2}", "é😀éx")]
    [InlineData(@"[\é\😀]+", "xé😀z")]
    [InlineData(@"\N{U+1F600}+", "a😀😀b")]
    [InlineData(@"\x{1F600}\o{351}", "😀é")]
    [InlineData(@"\p{L}+", "éHello😀")]
    [InlineData(@"[[:alpha:]]+", "éHello😀")]
    [InlineData(@"(*MARK:é)😀", "x😀")]
    [InlineData(@"\Qé😀(?C)\E", "xé😀(?C)z")]
    [InlineData(@"(?:ab|cd)+", "xxabcd!cd")]
    [InlineData(@"(?=)", "a😀b")]
    public void PublicByteCompilationOwnsSourceAndPreservesGrammarAndDiscovery(string pattern, string subject)
    {
        var source = Encoding.UTF8.GetBytes(pattern);
        var native = Utf8Pcre2Regex.Compile(source, default, default, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default);
        source.AsSpan().Fill((byte)'!');
        var text = new Utf8Pcre2Regex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(text.IsMatch(input), native.IsMatch(input));
        Assert.Equal(text.Count(input), native.Count(input));
        using var expected = text.CreateMatchCursor(input);
        using var actual = native.CreateMatchCursor(input);
        while (expected.MoveNext(out var leftMatch))
        {
            Assert.True(actual.MoveNext(out var rightMatch));
            Assert.Equal(leftMatch.Groups.Length, rightMatch.Groups.Length);
            Assert.Equal(leftMatch.NameEntries.ToArray(), rightMatch.NameEntries.ToArray());
            for (var slot = 0; slot < leftMatch.Groups.Length; slot++)
            {
                Assert.True(leftMatch.TryGetGroup(slot, out var left));
                Assert.True(rightMatch.TryGetGroup(slot, out var right));
                Assert.Equal(left.Success, right.Success);
                Assert.Equal(left.StartOffsetInBytes, right.StartOffsetInBytes);
                Assert.Equal(left.EndOffsetInBytes, right.EndOffsetInBytes);
            }
        }
        Assert.False(actual.MoveNext(out _));
        Assert.Equal(pattern, native.Pattern); // Explicit interoperability does not replace byte ownership.
        var program = native.DebugCompiledProgram;
        var retained = Assert.IsType<byte[]>(program.Request.PatternSource);
        Assert.NotSame(source, retained);
        Assert.Equal(Encoding.UTF8.GetBytes(pattern), retained);
    }

    [Fact]
    public void QuotedClassMembersConsumeCompleteScalars()
    {
        var match = new Utf8Pcre2Regex("[\\é\\😀]+"u8).MatchDetailed("xé😀z"u8);
        Assert.True(match.Success);
        Assert.Equal((1, 7), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
    }

    [Fact]
    public void NativeSourceAdmissionDoesNotRequireDecodedWholePatternCapacity()
    {
        var pattern = "(?#" + new string('x', 100000) + ")😀";
        var regex = Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(pattern), default, default,
            Timeout.InfiniteTimeSpan, default, new() { MaxBytes = 160000, MaxWorkSteps = 16000000 },
            Timeout.InfiniteTimeSpan, default);
        Assert.Equal(1, regex.Count("a😀b"u8));
        Assert.Equal(pattern, regex.Pattern);
    }

    [Fact]
    public void NativeByteStartsPreservePrefixContextAndScalarBoundaries()
    {
        var regex = new Utf8Pcre2Regex("(?<=a)😀+"u8);
        var match = regex.MatchDetailed("a😀😀b"u8, 1);
        Assert.True(match.Success);
        Assert.Equal((1, 9), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchDetailed("a😀😀b"u8, 2));
        var literal = new Utf8Pcre2Regex("b"u8).MatchDetailed("a😀b"u8, 5);
        Assert.Equal((5, 6), (literal.Value.StartOffsetInBytes, literal.Value.EndOffsetInBytes));
    }
}
