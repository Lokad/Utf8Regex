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
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(21)]
    public void ByteStartEnumerationMatchesRuntime(int utf16Offset)
    {
        const string pattern = @"(?:café|niño|résumé)";
        const string input = "café niño résumé café";
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var expected = new Regex(pattern, options);
            var actual = new Utf8Regex(pattern, options: options);
            var bytes = Encoding.UTF8.GetBytes(input);
            var expectedMatches = new List<(int Index, int Length)>();
            var actualMatches = new List<(int Index, int Length)>();

            foreach (var match in expected.EnumerateMatches(input, utf16Offset))
            {
                expectedMatches.Add((System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, match.Index)), System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(match.Index, match.Length))));
            }

            foreach (var match in actual.EnumerateMatches(bytes, Encoding.UTF8.GetByteCount(input.AsSpan(0, utf16Offset))))
            {
                actualMatches.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
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
            var actual = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.FromSeconds(1)
            });
            var bytes = Encoding.UTF8.GetBytes(input);
            var expectedMatches = new List<(int Index, int Length)>();
            var actualMatches = new List<Utf8CaptureRange>();
            foreach (var match in expected.EnumerateMatches(input))
            {
                expectedMatches.Add((System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, match.Index)), System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(match.Index, match.Length))));
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
                Assert.True(actualMatch.Success);
                Assert.Equal((expectedMatch.Index, expectedMatch.Length),
                    (actualMatch.StartOffsetInBytes, actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
                Assert.True(actualMatch.StartOffsetInBytes == bytes.Length || (bytes[actualMatch.StartOffsetInBytes] & 0xc0) != 0x80);
                Assert.True(actualMatch.EndOffsetInBytes == bytes.Length || (bytes[actualMatch.EndOffsetInBytes] & 0xc0) != 0x80);
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
        var actual = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout
        });
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(expected.IsMatch(input), actual.IsMatch(bytes));
        Assert.Equal(expected.Count(input), actual.Count(bytes));

        var expectedFirst = expected.Match(input);
        var actualFirst = actual.Match(bytes);
        Assert.Equal(expectedFirst.Success, actualFirst.Success);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedFirst.Index)), actualFirst.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(expectedFirst.Index, expectedFirst.Length)), (actualFirst.EndOffsetInBytes - actualFirst.StartOffsetInBytes));

        var expectedMatches = new List<(int ByteIndex, int ByteLength)>();
        foreach (var match in expected.EnumerateMatches(input))
        {
            expectedMatches.Add((
                Encoding.UTF8.GetByteCount(input.AsSpan(0, match.Index)),
                Encoding.UTF8.GetByteCount(input.AsSpan(match.Index, match.Length))));
        }

        var actualMatches = new List<(int ByteIndex, int ByteLength)>();
        foreach (var match in actual.EnumerateMatches(bytes))
        {
            actualMatches.Add((
                match.StartOffsetInBytes,
                (match.EndOffsetInBytes - match.StartOffsetInBytes)));
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

}
