using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R03 from-offset origin semantics: suffix slicing loses absolute-origin anchors
/// and left context, so those shapes keep full-input BCL verification. Each test asserts
/// BCL-correct values. MatchFromUtf16Offset keeps BCL semantics on these
/// shapes meanwhile, and the R03 differential suite covers the shapes that agree.
/// </summary>
public sealed class Utf8FromOffsetOriginTests
{
    [Fact]
    public void AnchoredEnumerationFromOffsetMatchesRuntime()
    {
        const string text = "abcabc";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("^abc", RegexOptions.CultureInvariant);
        var runtime = new Regex("^abc", RegexOptions.CultureInvariant);

        foreach (var offset in new[] { 1, 3 })
        {
            var expected = runtime.Match(text, offset);
            Utf8ValueMatch? first = null;
            foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, offset))
            {
                first = match;
                break;
            }

            Assert.Equal(expected.Success, first?.Success ?? false);
        }
    }

    [Fact]
    public void WordBoundaryEnumerationFromInsideWordMatchesRuntime()
    {
        const string text = "aword here";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("\\bword\\b", RegexOptions.CultureInvariant);
        var runtime = new Regex("\\bword\\b", RegexOptions.CultureInvariant);

        var expected = runtime.Match(text, 1);
        Utf8ValueMatch? first = null;
        foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, 1))
        {
            first = match;
            break;
        }

        Assert.Equal(expected.Success, first?.Success ?? false);
    }

    [Fact]
    public void LookbehindEnumerationFromOffsetMatchesRuntime()
    {
        const string text = "zab";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("(?<=a)b", RegexOptions.CultureInvariant);
        var runtime = new Regex("(?<=a)b", RegexOptions.CultureInvariant);

        var expected = runtime.Match(text, 2);
        Utf8ValueMatch? first = null;
        foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, 2))
        {
            first = match;
            break;
        }

        Assert.True(expected.Success);
        Assert.True(first?.Success ?? false);
        Assert.Equal(expected.Index, first?.IndexInUtf16 ?? -1);
    }

    public static IEnumerable<object?[]> FromOffsetParityCases()
    {
        yield return new object?[] { "^abc", RegexOptions.CultureInvariant, "abcabc" };
        yield return new object?[] { "abc$", RegexOptions.CultureInvariant, "xxabc" };
        yield return new object?[] { "\\bword\\b", RegexOptions.CultureInvariant, "aword here" };
        yield return new object?[] { "(?<=a)b", RegexOptions.CultureInvariant, "zab" };
        yield return new object?[] { "a(?=b)", RegexOptions.CultureInvariant, "ababa" };
        yield return new object?[] { "^abc", RegexOptions.Multiline | RegexOptions.CultureInvariant, "ab\nabc" };
        yield return new object?[] { "\\Aabc", RegexOptions.CultureInvariant, "xxabc" };
        yield return new object?[] { "\\Gabc", RegexOptions.CultureInvariant, "abcabc" };
        yield return new object?[] { "a|b", RegexOptions.CultureInvariant, "xayb" };
        yield return new object?[] { "[a-z]+", RegexOptions.CultureInvariant, "11ab22" };
        yield return new object?[] { "(a)\\1", RegexOptions.CultureInvariant, "xaaaya" };
        yield return new object?[] { "a*", RegexOptions.CultureInvariant, "bba" };
        yield return new object?[] { "ABC", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxABCxx" };
        yield return new object?[] { "\\d+", RegexOptions.CultureInvariant, "ab12cd" };
    }

    [Theory]
    [MemberData(nameof(FromOffsetParityCases))]
    public void FromOffsetFirstMatchMatchesRuntime(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern, options);
        var runtime = new Regex(pattern, options);

        for (var offset = 0; offset <= Math.Min(3, text.Length); offset++)
        {
            var expected = runtime.Match(text, offset);
            Utf8ValueMatch? first = null;
            foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, offset))
            {
                first = match;
                break;
            }

            Assert.Equal(expected.Success, first?.Success ?? false);
            if (!expected.Success)
            {
                continue;
            }

            Assert.True(first?.Success ?? false);
            Assert.Equal(expected.Index, first?.IndexInUtf16 ?? -1);
            Assert.Equal(expected.Length, first?.LengthInUtf16 ?? -1);
            var aligned = IsScalarBoundary(text, expected.Index) &&
                IsScalarBoundary(text, expected.Index + expected.Length);
            Assert.Equal(aligned, first?.IsByteAligned ?? false);
            if (aligned)
            {
                Assert.Equal(Encoding.UTF8.GetByteCount(text.Substring(0, expected.Index)), first?.IndexInBytes ?? -1);
                Assert.Equal(Encoding.UTF8.GetByteCount(text.Substring(expected.Index, expected.Length)), first?.LengthInBytes ?? -1);
            }
        }
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
