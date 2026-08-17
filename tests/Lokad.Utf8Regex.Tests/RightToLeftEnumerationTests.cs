using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class RightToLeftEnumerationTests
{
    public static TheoryData<string, string, RegexOptions> Cases => new()
    {
        { @"(?:cat|dog|horse)", "cat dog horse cat", RegexOptions.None },
        { @"(?:ab|cd)", "abcdab", RegexOptions.None },
        { @"(?:café|niño|résumé)", "café niño résumé café", RegexOptions.None },
        { @"(?:foo|bar|baz)", "foo BAR baz FoO", RegexOptions.IgnoreCase },
        { "cat", "cat scatter cat", RegexOptions.None },
        { @"\b\w+\b", "alpha βeta gamma", RegexOptions.None },
        { @"(?=a)", "banana", RegexOptions.None },
        { @"^|$", "anchors", RegexOptions.None },
        { @"(?:😀|café)", "😀 café 😀", RegexOptions.None },
        { @"(?:cat|dog|horse)", "no matches here", RegexOptions.None },
        { @"(?:cat|dog|horse)", string.Empty, RegexOptions.None },
    };

    public static TheoryData<string, string> SurrogateSplitCases => new()
    {
        { "high-surrogate", "\uD83D" },
        { "low-surrogate", "\uDE00" },
        { "supplementary-scalar", "\uD83D\uDE00" },
        { "zero-width-low-surrogate", "(?=\uDE00)" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void EnumerationMatchesDotNet(
        string pattern,
        string input,
        RegexOptions extraOptions)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant |
                RegexOptions.RightToLeft |
                extraOptions |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            AssertParity(pattern, input, options, Regex.InfiniteMatchTimeout);
            AssertParity(pattern, input, options, TimeSpan.FromSeconds(1));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnumerationRejectsMalformedUtf8(bool compiled)
    {
        var options = RegexOptions.CultureInvariant |
            RegexOptions.RightToLeft |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(@"(?:café|niño|résumé)", options);
        byte[] malformed = [.. Encoding.UTF8.GetBytes("café "), 0xFF];

        Assert.Throws<ArgumentException>(() => regex.EnumerateMatches(malformed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(21)]
    public void EnumerationFromUtf16OffsetMatchesDotNet(int utf16Offset)
    {
        const string pattern = @"(?:café|niño|résumé)";
        const string input = "café niño résumé café";
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var expected = new Regex(pattern, options);
            var actual = new Utf8Regex(pattern, options);
            var bytes = Encoding.UTF8.GetBytes(input);
            var expectedMatches = new List<(int Index, int Length)>();
            var actualMatches = new List<(int Index, int Length)>();

            foreach (var match in expected.EnumerateMatches(input, utf16Offset))
            {
                expectedMatches.Add((match.Index, match.Length));
            }

            foreach (var match in actual.EnumerateMatchesFromUtf16Offset(bytes, utf16Offset))
            {
                actualMatches.Add((match.IndexInUtf16, match.LengthInUtf16));
            }

            Assert.Equal(expectedMatches, actualMatches);
        }
    }

    [Theory]
    [MemberData(nameof(SurrogateSplitCases))]
    public void EnumerationPreservesSurrogateSplitBoundaries(string caseName, string pattern)
    {
        _ = caseName;
        const string input = "x😀y😀z";
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var expected = new Regex(pattern, options, TimeSpan.FromSeconds(1));
            var actual = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(1));
            var bytes = Encoding.UTF8.GetBytes(input);
            var expectedMatches = new List<(int Index, int Length)>();
            var actualMatches = new List<Utf8ValueMatch>();
            foreach (var match in expected.EnumerateMatches(input))
            {
                expectedMatches.Add((match.Index, match.Length));
            }

            foreach (var match in actual.EnumerateMatches(bytes))
            {
                actualMatches.Add(match);
            }

            Assert.Equal(expectedMatches.Count, actualMatches.Count);
            for (var i = 0; i < expectedMatches.Count; i++)
            {
                var expectedMatch = expectedMatches[i];
                var actualMatch = actualMatches[i];
                var expectedAligned = IsScalarBoundary(input, expectedMatch.Index) &&
                    IsScalarBoundary(input, expectedMatch.Index + expectedMatch.Length);

                Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
                Assert.Equal(expectedAligned, actualMatch.IsByteAligned);
                if (expectedAligned)
                {
                    Assert.Equal(
                        Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedMatch.Index)),
                        actualMatch.IndexInBytes);
                    Assert.Equal(
                        Encoding.UTF8.GetByteCount(input.AsSpan(expectedMatch.Index, expectedMatch.Length)),
                        actualMatch.LengthInBytes);
                }
                else
                {
                    Assert.False(actualMatch.TryGetByteRange(out _, out _));
                }
            }
        }
    }

    private static void AssertParity(
        string pattern,
        string input,
        RegexOptions options,
        TimeSpan timeout)
    {
        var expected = new Regex(pattern, options, timeout);
        var actual = new Utf8Regex(pattern, options, timeout);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(expected.IsMatch(input), actual.IsMatch(bytes));
        Assert.Equal(expected.Count(input), actual.Count(bytes));

        var expectedFirst = expected.Match(input);
        var actualFirst = actual.Match(bytes);
        Assert.Equal(expectedFirst.Success, actualFirst.Success);
        Assert.Equal(expectedFirst.Index, actualFirst.IndexInUtf16);
        Assert.Equal(expectedFirst.Length, actualFirst.LengthInUtf16);

        var expectedMatches = new List<(int Utf16Index, int Utf16Length, int ByteIndex, int ByteLength)>();
        foreach (var match in expected.EnumerateMatches(input))
        {
            expectedMatches.Add((
                match.Index,
                match.Length,
                Encoding.UTF8.GetByteCount(input.AsSpan(0, match.Index)),
                Encoding.UTF8.GetByteCount(input.AsSpan(match.Index, match.Length))));
        }

        var actualMatches = new List<(int Utf16Index, int Utf16Length, int ByteIndex, int ByteLength)>();
        foreach (var match in actual.EnumerateMatches(bytes))
        {
            actualMatches.Add((
                match.IndexInUtf16,
                match.LengthInUtf16,
                match.IndexInBytes,
                match.LengthInBytes));
        }

        Assert.Equal(expectedMatches, actualMatches);
        var expectedSplits = new List<string>();
        foreach (var split in expected.EnumerateSplits(input))
        {
            expectedSplits.Add(input[split]);
        }

        var actualSplits = new List<string>();
        foreach (var split in actual.EnumerateSplits(bytes))
        {
            actualSplits.Add(split.GetValueString());
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.Equal(
            expected.Replace(input, "<$&>"),
            Encoding.UTF8.GetString(actual.Replace(bytes, "<$&>")));
    }

    private static bool IsScalarBoundary(string input, int utf16Offset)
        => utf16Offset == 0 ||
           utf16Offset == input.Length ||
           !char.IsSurrogatePair(input[utf16Offset - 1], input[utf16Offset]);
}
