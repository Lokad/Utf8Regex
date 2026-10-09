using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

/// <summary>
/// R03 first engine slice: MatchFromUtf16Offset answers scalar-boundary starts from the
/// owned byte-range search. Interior offsets reject. Every valid row compares
/// success, UTF-16 coordinates, and byte coordinates (when scalar-aligned) against
/// the BCL oracle across literal, alternation, class, anchor, lookaround, backreference,
/// capture, empty-match, Unicode, and right-to-left shapes, including starts inside a
/// supplementary scalar and at the end of input.
/// </summary>
public sealed class Utf8MatchFromOffsetDiffTests
{
    public static IEnumerable<object?[]> OffsetCases()
    {
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, "xxabcxxab", new[] { 0, 1, 2, 5, 8, 9 } };
        yield return new object?[] { "a|b", RegexOptions.CultureInvariant, "xxaxxb", new[] { 0, 3, 6 } };
        yield return new object?[] { "[a-z]+", RegexOptions.CultureInvariant, "11ab22", new[] { 0, 2, 4, 6 } };
        yield return new object?[] { "^abc", RegexOptions.CultureInvariant, "abcabc", new[] { 0, 1, 3 } };
        yield return new object?[] { "abc$", RegexOptions.CultureInvariant, "xxabc", new[] { 0, 1, 5 } };
        yield return new object?[] { "\\bword\\b", RegexOptions.CultureInvariant, "a word here", new[] { 0, 2, 5, 11 } };
        yield return new object?[] { "a(?=b)", RegexOptions.CultureInvariant, "ababa", new[] { 0, 1, 2, 5 } };
        yield return new object?[] { "(a)\\1", RegexOptions.CultureInvariant, "xaaaya", new[] { 0, 1, 2, 6 } };
        yield return new object?[] { "(?<w>\\w+)", RegexOptions.CultureInvariant, "hi there", new[] { 0, 3, 8 } };
        yield return new object?[] { "a*", RegexOptions.CultureInvariant, "bba", new[] { 0, 1, 2, 3 } };
        yield return new object?[] { "x", RegexOptions.CultureInvariant, "x", new[] { 0, 1 } };
        yield return new object?[] { "Шерлок", RegexOptions.None, "аШерлокб", new[] { 0, 1, 2, 7, 8 } };
        yield return new object?[] { "y", RegexOptions.CultureInvariant, "x\U0001F600y", new[] { 0, 1, 2, 3, 4 } };
        yield return new object?[] { "abc", RegexOptions.RightToLeft | RegexOptions.CultureInvariant, "xxabcxx", new[] { 0, 2, 5, 7 } };
        yield return new object?[] { "\\d+-\\d+", RegexOptions.CultureInvariant, "ab12-34cd", new[] { 0, 2, 8 } };
    }

    [Theory]
    [MemberData(nameof(OffsetCases))]
    public void MatchFromUtf16OffsetMatchesRuntime(string pattern, RegexOptions options, string text, int[] offsets)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var utf8 = new Utf8Regex(pattern, options);
        var runtime = new Regex(pattern, options);

        foreach (var offset in offsets)
        {
            if (!IsScalarBoundary(text, offset))
            {
                using var source = new CancellationTokenSource();
                foreach (var token in new[] { CancellationToken.None, source.Token })
                {
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(
                        () => { _ = utf8.MatchFromUtf16Offset(input, offset, token); }).ParamName);
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(
                        () => { _ = utf8.IsMatchFromUtf16Offset(input, offset, token); }).ParamName);
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(
                        () => { _ = utf8.CountFromUtf16Offset(input, offset, token); }).ParamName);
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(
                        () => { _ = utf8.MatchDetailedFromUtf16Offset(input, offset, token); }).ParamName);
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(
                        () => { _ = utf8.EnumerateMatchesFromUtf16Offset(input, offset, token); }).ParamName);
                }
                Assert.False(utf8.Inspection.DebugFallbackRegexIsCreated);
                Assert.False(utf8.Inspection.DebugAnchoredFallbackRegexIsCreated);
                continue;
            }
            var expected = runtime.Match(text, offset);
            var actual = utf8.MatchFromUtf16Offset(input, offset);

            Assert.Equal(expected.Success, actual.Success);
            Assert.Equal(expected.Success ? expected.Index : 0, actual.IndexInUtf16);
            Assert.Equal(expected.Success ? expected.Length : 0, actual.LengthInUtf16);
            if (!actual.Success)
            {
                continue;
            }

            var start = actual.IndexInUtf16;
            var end = start + actual.LengthInUtf16;
            var aligned = IsScalarBoundary(text, start) && IsScalarBoundary(text, end);
            Assert.Equal(aligned, actual.IsByteAligned);
            if (aligned)
            {
                Assert.Equal(Encoding.UTF8.GetByteCount(text.Substring(0, start)), actual.IndexInBytes);
                Assert.Equal(Encoding.UTF8.GetByteCount(text.Substring(start, actual.LengthInUtf16)), actual.LengthInBytes);
            }
        }
    }

    [Fact]
    public void MatchFromUtf16OffsetTimeoutMatchesRuntime()
    {
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';
        var utf8 = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var runtime = new Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var text = Encoding.UTF8.GetString(input);

        var runtimeException = Assert.Throws<RegexMatchTimeoutException>(() => runtime.Match(text, 0));
        var utf8Exception = Assert.Throws<RegexMatchTimeoutException>(() => utf8.MatchFromUtf16Offset(input, 0));
        Assert.Equal(runtimeException.Pattern, utf8Exception.Pattern);
    }

    private static bool IsScalarBoundary(string text, int utf16Offset)
    {
        if ((uint)utf16Offset > (uint)text.Length)
        {
            return false;
        }

        if (utf16Offset == 0 || utf16Offset == text.Length)
        {
            return true;
        }

        return !(char.IsHighSurrogate(text[utf16Offset - 1]) && char.IsLowSurrogate(text[utf16Offset]));
    }
}
