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
        var owner = new Utf8Pcre2Regex(pattern, options: options, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1_000_000,
                MaxDepth = 1024,
                MaxWorkingBytes = 1 << 24,
                MaxResultBytes = 1 << 24
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        Assert.True(owner.IsMatch(input, 0, Pcre2MatchOptions.None, token.Token));
        ReadOnlySpan<byte> retained1Input = input;
        var retained1Captures = new Utf8Pcre2CaptureRange[owner.CaptureGroupCount + 1];
        var retained = owner.Match(retained1Input, captures: retained1Captures, cancellationToken: token.Token);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
        Assert.Equal(group, System.Text.Encoding.UTF8.GetString((retained1Captures[1].Success ? retained1Input.Slice(retained1Captures[1].StartOffsetInBytes, retained1Captures[1].EndOffsetInBytes - retained1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(1, owner.Count(input, 0, Pcre2MatchOptions.None, token.Token));
        Assert.Equal(group, System.Text.Encoding.UTF8.GetString((retained1Captures[1].Success ? retained1Input.Slice(retained1Captures[1].StartOffsetInBytes, retained1Captures[1].EndOffsetInBytes - retained1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            owner.Count(input, 0, Pcre2MatchOptions.None, token.Token)).CancellationToken);
        Assert.True(owner.IsMatch(input));
    }
}
