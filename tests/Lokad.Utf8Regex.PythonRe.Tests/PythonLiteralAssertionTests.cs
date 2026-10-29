using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonLiteralAssertionTests
{
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
            var retained = regex.SearchDetailedData(input);
            Assert.Equal("bar", retained.Value.ValueText);
            if (pattern.Contains("?P<x>", StringComparison.Ordinal)) Assert.Equal("foo", retained.Groups[1].ValueText);
            Assert.Equal("bar", retained.Value.ValueText);
            Assert.Throws<ArgumentException>(() => regex.Search([.. input, 0xff]));
        }
    }
}
