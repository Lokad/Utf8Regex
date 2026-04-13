using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexEnumerateMatchesDiffTests
{
    [Theory]
    [InlineData(@"\b(?!un)\w+\b", "unite one unethical ethics use untie ultimate", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData(@"(?<=\b20)\d{2}\b", "2010 1999 1861 2140 2009", RegexOptions.CultureInvariant)]
    [InlineData(@"e{2}\w\b", "needing a reed", RegexOptions.CultureInvariant)]
    public void EnumerateMatchesMatchesRuntimeForMirroredDotNetBatch(string pattern, string input, RegexOptions options)
    {
        // Mirrored and adapted from:
        // external/dotnet-runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.EnumerateMatches.Tests.cs
        AssertEnumerateMatchesParity(pattern, input, options);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForNativeLiteral()
    {
        const string input = "abcxxabc";
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "abc", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForNativeUtf8Literal()
    {
        const string input = "😀 café 😀";
        var regex = new Utf8Regex("😀", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16, match.IndexInBytes, match.LengthInBytes));
        }

        var expected = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in Regex.EnumerateMatches(input, "😀", RegexOptions.CultureInvariant))
        {
            var prefix = input[..match.Index];
            var value = input.Substring(match.Index, match.Length);
            expected.Add((match.Index, match.Length, Encoding.UTF8.GetByteCount(prefix), Encoding.UTF8.GetByteCount(value)));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForBoundaryWrappedUtf8Literal()
    {
        const string input = "é a éb é é";
        var regex = new Utf8Regex(@"\bé\b", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16, match.IndexInBytes, match.LengthInBytes));
        }

        var expected = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in Regex.EnumerateMatches(input, @"\bé\b", RegexOptions.CultureInvariant))
        {
            var prefix = input[..match.Index];
            var value = input.Substring(match.Index, match.Length);
            expected.Add((match.Index, match.Length, Encoding.UTF8.GetByteCount(prefix), Encoding.UTF8.GetByteCount(value)));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForUtf8LiteralAlternation()
    {
        const string input = "café niño cafe niño café";
        var regex = new Utf8Regex("café|niño", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16, match.IndexInBytes, match.LengthInBytes));
        }

        var expected = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in Regex.EnumerateMatches(input, "café|niño", RegexOptions.CultureInvariant))
        {
            var prefix = input[..match.Index];
            var value = input.Substring(match.Index, match.Length);
            expected.Add((match.Index, match.Length, Encoding.UTF8.GetByteCount(prefix), Encoding.UTF8.GetByteCount(value)));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForBoundaryWrappedUtf8LiteralAlternation()
    {
        const string input = "café niño caféx xniño niño";
        var regex = new Utf8Regex(@"\b(?:café|niño)\b", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16, match.IndexInBytes, match.LengthInBytes));
        }

        var expected = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in Regex.EnumerateMatches(input, @"\b(?:café|niño)\b", RegexOptions.CultureInvariant))
        {
            var prefix = input[..match.Index];
            var value = input.Substring(match.Index, match.Length);
            expected.Add((match.Index, match.Length, Encoding.UTF8.GetByteCount(prefix), Encoding.UTF8.GetByteCount(value)));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForBoundaryWrappedAsciiLiteralAlternation()
    {
        const string input = "Task ValueTask TaskFactory IAsyncEnumerable Task";
        const string pattern = @"\b(?:Task|ValueTask|IAsyncEnumerable)\b";
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16, match.IndexInBytes, match.LengthInBytes));
        }

        var expected = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in Regex.EnumerateMatches(input, pattern, RegexOptions.CultureInvariant))
        {
            var prefix = input[..match.Index];
            var value = input.Substring(match.Index, match.Length);
            expected.Add((match.Index, match.Length, Encoding.UTF8.GetByteCount(prefix), Encoding.UTF8.GetByteCount(value)));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForUtf8LiteralLookahead()
    {
        const string input = "café noir café gris café noir";
        var regex = new Utf8Regex("café(?= noir)", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16, match.IndexInBytes, match.LengthInBytes));
        }

        var expected = new List<(int Index, int Length, int ByteIndex, int ByteLength)>();
        foreach (var match in Regex.EnumerateMatches(input, "café(?= noir)", RegexOptions.CultureInvariant))
        {
            var prefix = input[..match.Index];
            var value = input.Substring(match.Index, match.Length);
            expected.Add((match.Index, match.Length, Encoding.UTF8.GetByteCount(prefix), Encoding.UTF8.GetByteCount(value)));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForFallbackRegex()
    {
        const string input = "ab abb abbb";
        var regex = new Utf8Regex("ab+", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "ab+", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForEmptyNativeLiteral()
    {
        const string input = "abc";
        var regex = new Utf8Regex(string.Empty, RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, string.Empty, RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForAnchoredAsciiSimplePattern()
    {
        const string input = "abc";
        var regex = new Utf8Regex("^abc$", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "^abc$", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForEscapedAsciiSimplePattern()
    {
        const string input = "12 34 xx";
        var regex = new Utf8Regex(@"\d\d", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, @"\d\d", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForInvariantIgnoreCaseAsciiSimplePattern()
    {
        const string input = "xxABxCDyy";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var regex = new Utf8Regex("ab.cd", options);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "ab.cd", options))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForExactQuantifiedAsciiSimplePattern()
    {
        const string input = "12 345 67";
        var regex = new Utf8Regex(@"\d{2}", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, @"\d{2}", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForEscapedAsciiCharClassPattern()
    {
        const string input = "3Z 9X zz";
        var regex = new Utf8Regex(@"[\d][A-Z]", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, @"[\d][A-Z]", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForControlEscapePattern()
    {
        const string input = "a\nb\nc";
        var regex = new Utf8Regex(@"\n", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, @"\n", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForHexEscapePattern()
    {
        const string input = "A BA";
        var regex = new Utf8Regex(@"\x41", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, @"\x41", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForAsciiSimpleAlternation()
    {
        const string input = "cat dog fish dog";
        var regex = new Utf8Regex("cat|dog", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "cat|dog", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForMixedLengthAsciiSimpleAlternation()
    {
        const string input = "cat horse fish horse";
        var regex = new Utf8Regex("cat|horse", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "cat|horse", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForBoundedAsciiQuantifierPattern()
    {
        const string input = "1 12 123 1234";
        var regex = new Utf8Regex(@"\d{2,4}", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, @"\d{2,4}", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForGroupedAsciiSimplePattern()
    {
        const string input = "cat horse fish horse";
        var regex = new Utf8Regex("(?:cat|horse)", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "(?:cat|horse)", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForDollarAnchorBeforeTrailingNewline()
    {
        const string input = "abc\n";
        var regex = new Utf8Regex("abc$", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "abc$", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForMultilineAsciiSimplePattern()
    {
        const string input = "abc\nzzz\nabc";
        var options = RegexOptions.Multiline | RegexOptions.CultureInvariant;
        var regex = new Utf8Regex("^abc", options);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "^abc", options))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    private static void AssertEnumerateMatchesParity(string pattern, string input, RegexOptions options)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = EnumerateRuntimeMatches(new Regex(pattern, options, Regex.InfiniteMatchTimeout), input);
        var expectedCompiled = EnumerateRuntimeMatches(new Regex(pattern, options | RegexOptions.Compiled, Regex.InfiniteMatchTimeout), input);
        var actual = EnumerateUtf8Matches(new Utf8Regex(pattern, options), bytes);
        var actualCompiled = EnumerateUtf8Matches(new Utf8Regex(pattern, options | RegexOptions.Compiled), bytes);

        Assert.Equal(expected, expectedCompiled);
        Assert.Equal(expected, actual);
        Assert.Equal(expected, actualCompiled);
    }

    private static List<(int Index, int Length, string Value)> EnumerateRuntimeMatches(Regex regex, string input)
    {
        var matches = new List<(int Index, int Length, string Value)>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            matches.Add((match.Index, match.Length, input.Substring(match.Index, match.Length)));
        }

        return matches;
    }

    private static List<(int Index, int Length, string Value)> EnumerateUtf8Matches(Utf8Regex regex, byte[] input)
    {
        var matches = new List<(int Index, int Length, string Value)>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            matches.Add((
                match.IndexInUtf16,
                match.LengthInUtf16,
                Encoding.UTF8.GetString(input, match.IndexInBytes, match.LengthInBytes)));
        }

        return matches;
    }
}
