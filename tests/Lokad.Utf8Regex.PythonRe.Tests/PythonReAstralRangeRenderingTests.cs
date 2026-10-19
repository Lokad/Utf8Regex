using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAstralRangeRenderingTests
{
    public static TheoryData<int, int> Ranges => new()
    {
        { 0x10000, 0x10000 }, { 0x10001, 0x103fe },
        { 0x103fe, 0x10401 }, { 0x103ff, 0x10400 },
        { 0x10000, 0x107ff }, { 0x10001, 0x10800 },
        { 0x10000, 0x10801 }, { 0x10000, 0x10ffff },
        { 0x10001, 0x10fffe }, { 0x10001, 0x10ffff },
        { 0x10ff00, 0x10ffff }, { 0xffff, 0x10401 },
        { 0xd7ff, 0x10400 },
    };

    private static Utf8PythonRegex Compile(string pattern)
        => Utf8PythonRegex.Compile(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 },
            new() { MaxWorkSteps = 20000 }, Timeout.InfiniteTimeSpan, CancellationToken.None);

    [Theory]
    [MemberData(nameof(Ranges))]
    public void PositiveAndNegativeRangesPreserveEveryEdgeAndCompleteScalar(int low, int high)
    {
        var samples = new[]
        {
            0, 0x7f, 0x80, 0xd7ff, 0xe000, 0xffff,
            0x10000, 0x10001, 0x103fe, 0x103ff, 0x10400,
            0x10401, 0x107ff, 0x10800, 0x10fffe, 0x10ffff,
            low - 1, low, low + 1, high - 1, high, high + 1,
        }.Where(Rune.IsValid).Distinct().Order().ToArray();
        var texts = samples.Select(value => new Rune(value).ToString()).ToArray();
        var subject = string.Concat(texts);
        using var cancellation = new CancellationTokenSource();
        foreach (var negated in new[] { false, true })
        {
            var pattern = $"[{(negated ? "^" : "")}\\U{low:X8}-\\U{high:X8}]";
            var regex = Compile(pattern);
            var managed = new Regex($"\\A(?:{PythonReDotNetReference.Render(regex)})\\z", RegexOptions.CultureInvariant);
            for (var index = 0; index < samples.Length; index++)
            {
                var expected = (samples[index] >= low && samples[index] <= high) != negated;
                Assert.Equal(expected, regex.FullMatch(Encoding.UTF8.GetBytes(texts[index])).Success);
                Assert.Equal(expected, managed.IsMatch(texts[index]));
            }
            var expectedValues = texts.Where((_, index) =>
                (samples[index] >= low && samples[index] <= high) != negated).ToArray();
            var input = Encoding.UTF8.GetBytes(subject);
            Assert.Equal(expectedValues, regex.FindAllToUtf8(input, 0, cancellation.Token)
                .ScalarValues.Select(Encoding.UTF8.GetString));
            Assert.Equal(expectedValues.Length, regex.Count(input, 0, cancellation.Token));
            Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { (byte)'a', 0xff }));
        }
    }

    [Fact]
    public void QuantifiedCapturesAndManagedComparatorKeepWholePairs()
    {
        var regex = Compile(@"(?P<x>[\U00010001-\U0010fffe]{2})");
        var first = "\U00010001\U0010fffe";
        var second = "\U00010400\U00100000";
        var text = first + "X" + second;
        var matches = regex.FindIterDetailed(Encoding.UTF8.GetBytes(text));
        Assert.Equal(new[] { first, second }, matches.Select(match => match.Groups[1].ValueText));
        Assert.Equal(new[] { 0, 9 }, matches.Select(match => match.Value.StartOffsetInBytes));
        Assert.Equal(new[] { 8, 17 }, matches.Select(match => match.Value.EndOffsetInBytes));
        Assert.All(matches, match => { Assert.Equal(1, match.LastIndex); Assert.Equal("x", match.LastGroup); });
        Assert.Equal(new[] { first, second }, new Regex(PythonReDotNetReference.Render(regex), RegexOptions.CultureInvariant)
            .Matches(text).Select(match => match.Value));
    }

    [Fact]
    public void FixedLookbehindStillCountsOneScalarAndCapturesForward()
    {
        var regex = Compile(@"(?<=([\U00010001-\U0010fffe]))X");
        var match = regex.SearchDetailedData("😀X"u8);
        Assert.True(match.Success);
        Assert.Equal(4, match.Value.StartOffsetInBytes);
        Assert.Equal("😀", match.Groups[1].ValueText);
        Assert.Equal(1, match.LastIndex);
        var managed = new Regex(PythonReDotNetReference.Render(regex), RegexOptions.CultureInvariant).Match("😀X");
        Assert.True(managed.Success);
        Assert.Equal("😀", managed.Groups[1].Value);
    }

    [Fact]
    public void LargeFoldedPartialRangesRetainTheirExpansionBackstop()
    {
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Compile(@"(?i)[\U00010001-\U0010fffe]"));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
    }
}
