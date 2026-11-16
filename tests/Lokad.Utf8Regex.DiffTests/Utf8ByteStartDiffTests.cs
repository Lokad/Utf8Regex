using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

/// <summary>
/// Public byte-start cursors preserve original subject context and complete scalar
/// ranges. Oracle UTF-16 positions are converted only within these tests.
/// </summary>
public sealed class Utf8ByteStartDiffTests
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
        yield return new object?[] { "(?<= )word(?= )", RegexOptions.CultureInvariant, "a word z", new[] { 0, 1, 2, 6, 8 } };
        yield return new object?[] { "\\G[a-z]+", RegexOptions.CultureInvariant, "ab cd", new[] { 0, 1, 3, 5 } };
        yield return new object?[] { "(a)\\1", RegexOptions.CultureInvariant, "xaaaya", new[] { 0, 1, 2, 6 } };
        yield return new object?[] { "(?<w>\\w+)", RegexOptions.CultureInvariant, "hi there", new[] { 0, 3, 8 } };
        yield return new object?[] { "(?<x>a)+(?<tail>b)?", RegexOptions.CultureInvariant, "x aab ab", new[] { 0, 2, 5, 8 } };
        yield return new object?[] { "a*", RegexOptions.CultureInvariant, "bba", new[] { 0, 1, 2, 3 } };
        yield return new object?[] { "x", RegexOptions.CultureInvariant, "x", new[] { 0, 1 } };
        yield return new object?[] { "Шерлок", RegexOptions.None, "аШерлокб", new[] { 0, 1, 2, 7, 8 } };
        yield return new object?[] { "y", RegexOptions.CultureInvariant, "x\U0001F600y", new[] { 0, 1, 2, 3, 4 } };
        yield return new object?[] { "abc", RegexOptions.RightToLeft | RegexOptions.CultureInvariant, "xxabcxx", new[] { 0, 2, 5, 7 } };
        yield return new object?[] { "\\d+-\\d+", RegexOptions.CultureInvariant, "ab12-34cd", new[] { 0, 2, 8 } };
    }

    [Theory]
    [MemberData(nameof(OffsetCases))]
    public void ByteStartCursorMatchesRuntimeRangesAndCaptures(string pattern, RegexOptions options, string text, int[] offsets)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var runtime = new Regex(pattern, options);
        foreach (var compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
        foreach (var offset in offsets)
        {
            var utf8 = new Utf8Regex(pattern, options | compiled);
            if (!IsScalarBoundary(text, offset))
            {
                // This oracle position splits a surrogate pair. Exercise the
                // corresponding UTF-8 scalar's interior byte, rather than a
                // removed UTF-16 entry-point contract.
                var interiorByte = Encoding.UTF8.GetByteCount(text.AsSpan(0, offset - 1)) + 1;
                using var source = new CancellationTokenSource();
                foreach (var token in new[] { CancellationToken.None, source.Token })
                {
                    Assert.Equal("startOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(
                        () => { using var cursor = utf8.CreateMatchCursor(input, interiorByte, token); }).ParamName);
                }
                continue;
            }
            var startByte = Encoding.UTF8.GetByteCount(text.AsSpan(0, offset));
            using var matches = utf8.CreateMatchCursor(input, startByte);
            foreach (Match expected in runtime.Matches(text, offset))
            {
                Assert.True(matches.MoveNext(out var actual));
                AssertRange(text, expected, actual.Value.StartOffsetInBytes, actual.Value.EndOffsetInBytes);
                foreach (var number in runtime.GetGroupNumbers())
                {
                    Assert.True(actual.TryGetGroup(number, out var group));
                    var expectedGroup = expected.Groups[number];
                    Assert.Equal(expectedGroup.Success, group.Success);
                    if (group.Success)
                        AssertRange(text, expectedGroup, group.StartOffsetInBytes, group.EndOffsetInBytes);
                    var captures = actual.GetCaptures(number);
                    Assert.Equal(expectedGroup.Captures.Count, captures.Length);
                    for (var index = 0; index < captures.Length; index++)
                        AssertRange(text, expectedGroup.Captures[index], captures[index].StartOffsetInBytes,
                            captures[index].EndOffsetInBytes);
                }
            }
            Assert.False(matches.MoveNext(out _));
        }
    }

    private static void AssertRange(string text, Capture expected, int startByte, int endByte)
    {
        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), startByte);
        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index + expected.Length)), endByte);
    }

    [Fact]
    public void ByteStartCursorPreservesTheMatchingDeadline()
    {
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';
        var utf8 = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var runtime = new Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var text = Encoding.UTF8.GetString(input);

        var runtimeException = Assert.Throws<RegexMatchTimeoutException>(() => runtime.Match(text, 0));
        var utf8Exception = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            using var cursor = utf8.CreateMatchCursor(input, 0);
            _ = cursor.MoveNext(out _);
        });
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
