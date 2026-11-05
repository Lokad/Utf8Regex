using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2AdjacentSetRepeatTests
{
    [Theory]
    [InlineData(@"\Aitem(\s+)[0-9]+\z", "item   12345", Pcre2CompileOptions.None, "   ")]
    [InlineData(@"\Aitem(\s+)[0-9]+\z", "item\u2003\u3000 12345", Pcre2CompileOptions.Ucp, "\u2003\u3000 ")]
    [InlineData(@"\A([a-z]+)[0-9]+\z", "abc123", Pcre2CompileOptions.None, "abc")]
    [InlineData(@"\A([0-9 ]+)[0-9]+\z", "   12345", Pcre2CompileOptions.None, "   1234")]
    [InlineData(@"\A([a-z]+)[0-9]*[a-z]+\z", "abc", Pcre2CompileOptions.None, "ab")]
    [InlineData(@"\A([a-z]+?)[0-9]+\z", "abc123", Pcre2CompileOptions.None, "abc")]
    [InlineData(@"\A(\p{L}+)[0-9]+\z", "é中12", Pcre2CompileOptions.Ucp, "é中")]
    public void ProfilePredicatesCapturesAndOriginalTokensRemain(string pattern, string text, Pcre2CompileOptions options, string group)
    {
        var owner = new Utf8Pcre2Regex(pattern, options, default,
            new() { MatchLimit = 1_000_000, DepthLimit = 1024, HeapLimitInBytes = 1 << 24, ResultLimitInBytes = 1 << 24 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        Assert.True(owner.IsMatch(input, 0, Pcre2MatchOptions.None, token.Token));
        var retained = owner.MatchDetailed(input, 0, Pcre2MatchOptions.None, token.Token);
        Assert.Equal(text, retained.GetValueString());
        Assert.Equal(group, retained.GetGroup(1).GetValueString());
        Assert.Equal(1, owner.Count(input, 0, Pcre2MatchOptions.None, token.Token));
        Assert.Equal(group, retained.GetGroup(1).GetValueString());
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            owner.Count(input, 0, Pcre2MatchOptions.None, token.Token)).CancellationToken);
        Assert.True(owner.IsMatch(input));
    }
}
