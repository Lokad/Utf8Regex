using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// CPython 3.13.2 oracle receipt: artifacts/python-brace-quantifier-admission-20261017.
public sealed class PythonReBraceQuantifierTests
{
    [Theory]
    [InlineData("{", "{")]
    [InlineData("{}", "{}")]
    [InlineData("{foo}", "{foo}")]
    [InlineData("{1", "{1")]
    [InlineData("{1x", "{1x")]
    [InlineData("a{", "a{")]
    [InlineData("a{}", "a{}")]
    [InlineData("a{foo}", "a{foo}")]
    [InlineData("a{1", "a{1")]
    [InlineData("a{1x", "a{1x")]
    [InlineData("a{1,2x", "a{1,2x")]
    [InlineData("a{2147483648x", "a{2147483648x")]
    [InlineData("\ud83d\ude00{foo}", "\ud83d\ude00{foo}")]
    [InlineData("a*{foo}", "aaa{foo}")]
    [InlineData("a+{1x", "aaa{1x")]
    [InlineData("a{,2}", "aa")]
    [InlineData("a{,2}", "")]
    [InlineData("a{,}", "aaa")]
    [InlineData("\ud83d\ude00{,2}", "\ud83d\ude00\ud83d\ude00")]
    [InlineData("(\ud83d\ude00){,2}?\u00e9", "\ud83d\ude00\ud83d\ude00\u00e9")]
    [InlineData("(?P<g>a){,2}+b", "aab")]
    [InlineData("(?x)a {2}", "aa")]
    [InlineData("(?x)(a) #comment\n{2}", "aa")]
    [InlineData("(?x)a +? b", "aab")]
    [InlineData("(?x)a{ 2}", "a{2}")]
    [InlineData("(?x:a {,2})", "aa")]
    [InlineData("(?x:a) {2}", "a  ")]
    [InlineData("(?x)a* #comment\n{foo}", "aaa{foo}")]
    public void LiteralBracesAndQuantifierAttachmentFollowPython(string pattern, string text)
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 256,
            MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 };
        using var cancellation = new CancellationTokenSource();
        foreach (var bytes in new[] { false, true })
        {
            var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy)
                : new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
            var input = Encoding.UTF8.GetBytes(text);
            var actual = regex.FullMatchDetailedData(input, 0, cancellation.Token);
            Assert.True(actual.Success);
            Assert.Equal(text, actual.Value.ValueText);
            Assert.Equal(0, actual.Value.StartOffsetInBytes);
            Assert.Equal(input.Length, actual.Value.EndOffsetInBytes);
            Assert.Equal(text.Length, actual.Value.EndOffsetInUtf16);
            Assert.True(regex.FullMatch(input).Success);
        }
    }

    [Theory]
    [InlineData("{1}", "nothing to repeat", 0)]
    [InlineData("{,}", "nothing to repeat", 0)]
    [InlineData("{,2}", "nothing to repeat", 0)]
    [InlineData("a*{2}", "multiple repeat", 2)]
    [InlineData("a{2}{,3}", "multiple repeat", 4)]
    [InlineData("a{2,1}", "min repeat greater than max repeat", 2)]
    [InlineData("(?x)a* ?", "multiple repeat", 7)]
    [InlineData("(?x)a+ #comment\n*", "multiple repeat", 16)]
    public void ActualQuantifierErrorsKeepTheirMeaningAndPosition(string pattern, string message, int position)
    {
        var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Equal(message, error.Message);
        Assert.Equal(position, error.Position);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData("(😀){,2}?é", "😀😀é", "😀", null, 4)]
    [InlineData("(?P<g>a){,2}+b", "aab", "a", "g", 1)]
    public void OmittedLowerBoundsKeepLastCompleteScalarCapture(string pattern, string text, string capture, string? name, int startByte)
    {
        var regex = new Utf8PythonRegex(pattern);
        var actual = regex.FullMatchDetailedData(Encoding.UTF8.GetBytes(text));
        Assert.True(actual.Success);
        Assert.Equal(capture, actual.Groups[1].ValueText);
        Assert.Equal(1, actual.LastIndex);
        Assert.Equal(name, actual.LastGroup);
        Assert.Equal(startByte, actual.Groups[1].StartOffsetInBytes);
        Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes(text)));
        Assert.Equal(capture, actual.Groups[1].ValueText);
    }

    [Theory]
    [InlineData("😀{,2}", 2)]
    [InlineData("😀{,2}?", 5)]
    [InlineData("😀{,2}+", 2)]
    public void OmittedLowerBoundsKeepScalarGlobalEmptyProgression(string pattern, int expected)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expected, regex.Count("😀😀"u8));
        Assert.Equal(expected == 5 ? ["", "😀", "", "😀", ""] : new[] { "😀😀", "" },
            regex.FindAllToStrings("😀😀"u8).ScalarValues);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LongBraceLookaheadSpendsConstructionWorkBeforeLiteralFallback(bool bytes)
    {
        var pattern = "a{" + new string('0', 8192) + "x";
        var policy = new Utf8ConstructionPolicy { MaxWorkSteps = 256 };
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (bytes) _ = Utf8PythonRegex.Compile(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None,
                Regex.InfiniteMatchTimeout, default, policy, Regex.InfiniteMatchTimeout, default);
            else _ = Utf8PythonRegex.Compile(pattern, PythonReCompileOptions.None,
                Regex.InfiniteMatchTimeout, default, policy, Regex.InfiniteMatchTimeout, default);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.False(Utf8ConstructionScope.IsActive);
        Assert.True(new Utf8PythonRegex("a{foo}").FullMatch("a{foo}"u8).Success);
    }
}
