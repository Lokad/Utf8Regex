using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarLiteralPairTests
{
    [Theory]
    [InlineData("[,，]", "(?:,|，)", "東京，大阪,京都，")]
    [InlineData("[é-ê]", "(?:é|ê)", "éê中ê😀é")]
    [InlineData("[😀😁]", "(?:😀|😁)", "中😀😁é😁😀")]
    [InlineData("[a😀]", "(?:a|😀)", "中a😀éaa😀")]
    [InlineData("[\u007F\u0080]", "(?:\u007F|\u0080)", "a\u007F\u0080😀\u0080")]
    [InlineData("[\uD7FF\uE000]", "(?:\uD7FF|\uE000)", "中\uD7FF\uE000😀")]
    public void ClassAndAlternationHaveIdenticalScalarResults(string pattern, string equivalent, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        {
            var actual = new Utf8Regex(pattern, options);
            var expected = new Utf8Regex(equivalent, options);
            Assert.Equal(expected.Count(input), actual.Count(input));
            Assert.Equal(Ranges(expected, input), Ranges(actual, input));
            Assert.Equal(expected.IsMatch(input), actual.IsMatch(input));
        }
    }

    [Theory]
    [InlineData("[,，]")]
    [InlineData("[😀😁]")]
    public void CapturesAndWholeSubjectValidationRemainOwned(string pair)
    {
        var text = pair == "[,，]" ? "中，x,😀" : "中😀x😁，";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("(?<separator>" + pair + ")");
        var snapshot = regex.MatchDetailed(input);
        Assert.True(snapshot.Success);
        var captured = snapshot.GetGroup(1).GetValueString();
        Assert.Equal(2, regex.Count(input));
        Assert.Equal(captured, snapshot.GetGroup(1).GetValueString());
        byte[] malformed = [.. input, 0xFF];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
    }

    private static (int Index, int Length)[] Ranges(Utf8Regex regex, byte[] input)
    {
        var ranges = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(input)) ranges.Add((match.IndexInBytes, match.LengthInBytes));
        return ranges.ToArray();
    }

    [Fact]
    public void PairCountKeepsFiniteAdmissionAndOriginalCancellation()
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1,
        };
        var regex = new Utf8Regex("[,，]", RegexOptions.None, Regex.InfiniteMatchTimeout, policy);
        Assert.Equal(2, regex.Count("東京，大阪,尾"u8));
        var denied = new Utf8Regex("[,，]", RegexOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count("東京，大阪,尾"u8)).ResourceKind);
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count("，"u8, token.Token)).CancellationToken);
    }
}
