using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SupplementaryCaseTests
{
    [Fact]
    public void AllSupplementaryMappingsAndGapsFollowPinnedUnicodeData()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "TestData", "supplementary-simple-case-16.json")));
        Assert.Equal("16.0.0", json.RootElement.GetProperty("UnicodeVersion").GetString());
        var pairs = json.RootElement.GetProperty("Pairs").EnumerateArray()
            .Select(row => (Upper: row[0].GetInt32(), Lower: row[1].GetInt32())).ToArray();
        Assert.Equal(282, pairs.Length);
        var lower = pairs.ToDictionary(pair => pair.Upper, pair => pair.Lower);
        var upper = pairs.ToDictionary(pair => pair.Lower, pair => pair.Upper);
        for (var scalar = 0x10000; scalar <= 0x10FFFF; scalar++)
        {
            Assert.Equal(lower.GetValueOrDefault(scalar, scalar), Utf8SupplementaryCase.Lower(scalar));
            Assert.Equal(upper.GetValueOrDefault(scalar, scalar), Utf8SupplementaryCase.Upper(scalar));
        }
    }

    [Theory]
    [InlineData(0x10D50, 0x10D70)]
    [InlineData(0x10D65, 0x10D85)]
    [InlineData(0x10400, 0x10428)]
    public void PublicScalarCaseOperationsAgreeAcrossShapesOrdersAndLimits(int upper, int lower)
    {
        var capital = char.ConvertFromUtf32(upper);
        var small = char.ConvertFromUtf32(lower);
        using var source = new CancellationTokenSource();
        foreach (var reverse in new[] { false, true })
        foreach (var compiled in new[] { false, true })
        foreach (var finite in new[] { false, true })
        {
            var policy = finite ? new Utf8ResourcePolicy { MaxWorkSteps = 100000,
                MaxDepth = 128, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 } : default;
            var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
                (reverse ? RegexOptions.RightToLeft : 0) | (compiled ? RegexOptions.Compiled : 0);
            foreach (var pattern in new[] { capital, "[" + capital + "]", "[" + capital + "-" + capital + "]",
                "[" + small + "]", "[\\p{L}-[^" + capital + "]]" })
            {
                var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, policy);
                var bytes = Encoding.UTF8.GetBytes("padding!" + small + "!padding");
                var input = bytes.AsSpan(7, 6);
                var match = regex.Match(input, source.Token);
                Assert.True(match.Success);
                Assert.Equal((1, 4), (match.IndexInBytes, match.LengthInBytes));
                Assert.Equal(1, regex.Count(input, source.Token));
                Assert.Equal(small, regex.MatchDetailed(input, source.Token).GetValueString());
                Assert.Equal("!x!", regex.ReplaceToString(input, "x", source.Token));
            }
            var reference = new Utf8Regex(reverse ? @"^\1(.)$" : @"^(.)\1$",
                options, Regex.InfiniteMatchTimeout, policy);
            Assert.Equal(8, reference.Match(Encoding.UTF8.GetBytes(capital + small), source.Token).LengthInBytes);
            var excluded = new Utf8Regex("^[^" + capital + "]$", options, Regex.InfiniteMatchTimeout, policy);
            Assert.False(excluded.IsMatch(Encoding.UTF8.GetBytes(small), source.Token));
            Assert.True(excluded.IsMatch("😀"u8, source.Token));
        }
    }
}
