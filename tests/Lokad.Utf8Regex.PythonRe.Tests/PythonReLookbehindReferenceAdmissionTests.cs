using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLookbehindReferenceAdmissionTests
{
    [Theory]
    [InlineData("(?<=(?P<x>a)(?(x)b|c))d", "same lookbehind subpattern", 17)]
    [InlineData("(?<=(a)(?(1)b|c))d", "same lookbehind subpattern", 12)]
    [InlineData("(?P<x>(?<=(?(x)a|b)))", "open group", 15)]
    [InlineData("(?<=(?(1)a|b))(a)", "open group", 9)]
    [InlineData("(?<=(?!(a))(?(1)b|c))d", "same lookbehind subpattern", 16)]
    [InlineData(@"(?<=(a)\1)b", "same lookbehind subpattern", 9)]
    [InlineData("(?<=(?P<x>a)(?P=x))b", "same lookbehind subpattern", 18)]
    [InlineData("(?<=(a)(?<=(?(1)a|b)))c", "same lookbehind subpattern", 16)]
    [InlineData("(?<!(a)(?(1)b|c))d", "same lookbehind subpattern", 12)]
    [InlineData("(?<=(a)(?<=a)(?(1)a|b))c", "same lookbehind subpattern", 18)]
    public void LookbehindReferencesRequireAnAlreadyClosedEarlierGroup(string pattern, string message, int position)
    {
        foreach (var bytes in new[] { false, true })
        {
            var error = Assert.Throws<PythonRePatternException>(() =>
            {
                if (bytes) _ = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern));
                else _ = new Utf8PythonRegex(pattern);
            });
            Assert.Contains(message, error.Message);
            Assert.Equal(position, error.Position);
            Assert.False(Utf8ConstructionScope.IsActive);
        }
        Assert.True(new Utf8PythonRegex("(a)(?<=(?(1)a|b))b").FullMatch("ab"u8).Success);
    }

    [Theory]
    [InlineData("(?P<x>a)(?<=(?(x)a|b))b", "ab", "a")]
    [InlineData("(a)(?<=(?(1)a|b))b", "ab", "a")]
    [InlineData("(?P<x>😀)(?<=(?(x)😀|a))é", "😀é", "😀")]
    [InlineData("(a)(?<=(?<=a)(?(1)a|b))b", "aab", "a")]
    [InlineData("(?<=(a))(?(1)b|c)", "ab", "a")]
    [InlineData("(?P<x>(?(x)a|b))", "b", "b")]
    [InlineData("(?(1)a|b)(c)", "bc", "c")]
    [InlineData("(?<=(a))(?<=(?(1)a|b))c", "ac", "a")]
    [InlineData("(?P<x>😀)(?<=(?P=x))é", "😀é", "😀")]
    [InlineData(@"(a)(?<=\1)b", "ab", "a")]
    public void EarlierGroupsNestedAssertionsAndReferencesOutsideLookbehindRemainValid(
        string pattern, string text, string capture)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var bytes in new[] { false, true })
        {
            var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 128,
                MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 };
            var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy)
                : new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
            var match = regex.SearchDetailedData(input);
            Assert.True(match.Success);
            Assert.Equal(capture, match.Groups[1].ValueText);
            Assert.Equal(Encoding.UTF8.GetByteCount(capture), match.Groups[1].EndOffsetInBytes - match.Groups[1].StartOffsetInBytes);
            Assert.Equal(1, match.LastIndex);
        }
    }
}
