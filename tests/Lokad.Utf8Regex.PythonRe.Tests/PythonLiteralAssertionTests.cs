using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonLiteralAssertionTests
{
    [Theory]
    [InlineData("(?:(?P<x>foo)(?=bar)baz|(?P<y>foo)bar)")]
    [InlineData("(?:(?P<x>foo)(?!bar)bar|(?P<y>foo)bar)")]
    public void LiteralAssertionsKeepParentAlternativeCaptureRollback(string pattern)
    {
        using var token = new CancellationTokenSource();
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 128,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        var match = regex.SearchDetailed("foobar"u8, 0, token.Token);
        Assert.True(match.Success);
        Assert.Equal("foobar", System.Text.Encoding.UTF8.GetString(match.Value.GetValueBytes()));
        Assert.False(match.GetGroup(1).Success);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(match.GetGroup(2).Value.GetValueBytes()));
    }

    [Theory]
    [InlineData("(?<=foo)bar", "foobar", true, 3)]
    [InlineData("(?<=foo)bar", "fooxbar", false, 0)]
    [InlineData("(?<!foo)bar", "foobar", false, 0)]
    [InlineData("(?<!foo)bar", "fooxbar", true, 4)]
    [InlineData("(?<=foo)bar", "😀foobar", true, 7)]
    [InlineData("(?<=😀)bar", "😀bar", true, 4)]
    [InlineData("(?<=foo|goo)bar", "goobar", true, 3)]
    [InlineData("(?<=(?P<x>foo))bar", "foobar", true, 3)]
    [InlineData("(?i:(?<=foo))bar", "FOObar", true, 3)]
    public void FixedForwardAssertionsRetainPythonResults(string pattern, string text, bool success, int start)
    {
        var input = Encoding.UTF8.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 128,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        foreach (var bytes in new[] { false, true })
        {
            if (bytes && pattern.Contains("😀", StringComparison.Ordinal)) continue;
            var regex = bytes ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None,
                Timeout.InfiniteTimeSpan, policy) : new Utf8PythonRegex(pattern, PythonReCompileOptions.None,
                Timeout.InfiniteTimeSpan, policy);
            var match = regex.Search(storage.AsSpan(1, input.Length));
            Assert.Equal(success, match.Success);
            Assert.Equal(success ? 1 : 0, regex.Count(input));
            if (!success) continue;
            Assert.Equal(start, match.StartOffsetInBytes);
            Assert.True(regex.Search(input, start).Success);
            Assert.False(regex.Search(input, start + 1).Success);
            var retained = regex.SearchDetailed(input);
            Assert.Equal("bar", System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
            if (pattern.Contains("?P<x>", StringComparison.Ordinal)) Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(retained.GetGroup(1).Value.GetValueBytes()));
            Assert.Equal("bar", System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));

        }
    }
}
