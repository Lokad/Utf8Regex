using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReConditionalNumberAdmissionTests
{
    [Theory]
    [InlineData("(?(2147483648)a|b)", "invalid group reference 2147483648")]
    [InlineData("(?(0002147483648)a|b)", "invalid group reference 2147483648")]
    [InlineData("(?(999999999999999999999999)a|b)", "invalid group reference 999999999999999999999999")]
    [InlineData("(?(2147483648", "missing ), unterminated name")]
    [InlineData("(?(2147483648x)a|b)", "bad character in group name '2147483648x'")]
    public void OversizedConditionalNumbersKeepPatternErrorsAndHeaderPrecedence(string pattern, string message)
    {
        foreach (var bytes in new[] { false, true })
        {
            var failure = Assert.Throws<PythonRePatternException>(() =>
            {
                if (bytes) _ = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern));
                else _ = new Utf8PythonRegex(pattern);
            });
            Assert.Equal(message, failure.Message);
            Assert.Equal(3, failure.Position);
            Assert.False(Utf8ConstructionScope.IsActive);
        }
        Assert.True(new Utf8PythonRegex("(?(1)a|b)(c)").Match("bc"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData("(?(1)a|b)(c)", "bc")]
    [InlineData("(?(0001)a|b)(c)", "bc")]
    [InlineData("(?(000000000000000000000000000001)a|b)(c)", "bc")]
    [InlineData("(😀)?(?(0001)é|c)", "😀é")]
    public void ValidNumericConditionsKeepForwardReferencesAndScalarCaptures(string pattern, string text)
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 100000,
            MaxDepth = 128,
            MaxWorkingBytes = 1000000,
            MaxResultBytes = 1000000
        };
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        });
        var input = Encoding.UTF8.GetBytes(text);
        Utf8PythonRangeMatch result = default;
        using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out result);
        }
        Assert.True(result.Success);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(result.GetValueMemory().Span));
        Assert.Equal(text == "😀é" ? "😀" : "c", System.Text.Encoding.UTF8.GetString((result.Groups[1].Success ? result.GetGroupMemory(result.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(input.Length, result.Value.EndOffsetInBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongNumericHeadersRemainSubjectToConstructionAdmission(bool bytes)
    {
        var pattern = "(?(" + new string('9', 8192) + ")a|b)";
        var policy = new Utf8ConstructionPolicy { MaxWorkSteps = 256 };
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (bytes) _ = Utf8PythonRegex.Compile(Encoding.UTF8.GetBytes(pattern), options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = default,
                ConstructionPolicy = policy,
                ConstructionTimeout = Regex.InfiniteMatchTimeout
            }, cancellationToken: default);
            else _ = Utf8PythonRegex.Compile(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = default,
                ConstructionPolicy = policy,
                ConstructionTimeout = Regex.InfiniteMatchTimeout
            }, cancellationToken: default);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.False(Utf8ConstructionScope.IsActive);
    }
}
