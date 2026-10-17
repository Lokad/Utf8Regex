using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReConditionalNameAdmissionTests
{
    [Theory]
    [InlineData("(?(x")]
    [InlineData("(?P<x>a)(?(x")]
    public void UnterminatedHeadersKeepTheirSyntaxFailure(string pattern)
    {
        var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Contains("missing ), unterminated name", error.Message);
        Assert.DoesNotContain("unknown group name", error.Message);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData("(?(x)a|b)(?P<x>c)", "x")]
    [InlineData("(?(é)a|b)(?P<é>c)", "é")]
    [InlineData("(?(missing)a|b)", "missing")]
    public void ConditionalNamesMustExistAtTheirLexicalPoint(string pattern, string name)
    {
        foreach (var bytes in new[] { false, true })
        {
            var error = Assert.Throws<PythonRePatternException>(() =>
            {
                if (bytes) _ = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern));
                else _ = new Utf8PythonRegex(pattern);
            });
            Assert.Contains("unknown group name '" + name + "'", error.Message);
            Assert.Equal(3, error.Position);
            Assert.False(Utf8ConstructionScope.IsActive);
        }
        Assert.True(new Utf8PythonRegex("(?P<x>a)?(?(x)b|c)").FullMatch("ab"u8).Success);
    }

    [Theory]
    [InlineData("(?P<x>a)?(?(x)b|c)", "ab", true)]
    [InlineData("(?P<x>a)?(?(x)b|c)", "c", true)]
    [InlineData("(?P<x>a)?(?(x)b|c)", "ac", false)]
    [InlineData("(?P<x>(?(x)a|b))", "b", true)]
    [InlineData("(?P<x>(?(x)a|b))", "a", false)]
    [InlineData("(?(1)a|b)(c)", "bc", true)]
    [InlineData("(?(1)a|b)(c)", "ac", false)]
    [InlineData("(?P<é>a)?(?(é)b|c)", "ab", true)]
    [InlineData("(?P<é>a)?(?(é)b|c)", "c", true)]
    [InlineData("(?P<x>😀)?(?(x)é|c)", "😀é", true)]
    [InlineData("(?P<x>😀)?(?(x)é|c)", "c", true)]
    public void ExistingOpenAndNumericForwardConditionsKeepScalarResults(string pattern, string text, bool success)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var bytes in new[] { false, true })
        {
            var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 128,
                MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 };
            var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy)
                : new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
            var match = regex.FullMatchDetailedData(input);
            Assert.Equal(success, match.Success);
            if (success)
            {
                Assert.Equal((0, input.Length), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
                Assert.Equal(text, match.Value.ValueText);
                if (text == "😀é")
                {
                    Assert.Equal("😀", match.Groups[1].ValueText);
                    Assert.Equal(1, match.LastIndex);
                    Assert.Equal("x", match.LastGroup);
                }
            }
        }
    }
}
