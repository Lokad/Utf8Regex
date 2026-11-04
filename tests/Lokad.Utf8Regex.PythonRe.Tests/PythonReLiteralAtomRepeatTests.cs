using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLiteralAtomRepeatTests
{
    [Theory]
    [InlineData("x*", "yy", new[] { "", "", "" })]
    [InlineData("x*", "xxyx", new[] { "xx", "", "x", "" })]
    [InlineData("(?:😀)*", "😀😀é😀", new[] { "😀😀", "", "😀", "" })]
    [InlineData("é{0,2}", "éééx", new[] { "éé", "é", "", "" })]
    [InlineData("x*", "yé😀x", new[] { "", "", "", "x", "" })]
    [InlineData("x{0,2}", "xxx", new[] { "xx", "x", "" })]
    [InlineData("x*", "", new[] { "" })]
    [InlineData("(x*)", "xxy", new[] { "xx", "", "" })]
    [InlineData("(x)*", "xxy", new[] { "x", "", "" })]
    [InlineData("x*?y", "xxy", new[] { "xxy" })]
    [InlineData("x*x", "xxx", new[] { "xxx" })]
    [InlineData("(?i)x*", "XXy", new[] { "XX", "", "" })]
    [InlineData("(?:x*|y)", "y", new[] { "", "y", "" })]
    [InlineData("(?<=x{2})y", "xxy", new[] { "y" })]
    [InlineData("x{2}x", "xxxx", new[] { "xxx" })]
    [InlineData("\0*", "\0\0x", new[] { "\0\0", "", "" })]
    public void PublicFindAllKeepsValuesCapturesAndEmptyProgression(string pattern, string text, string[] expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var policy in new[] { default(Utf8ResourcePolicy), new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000, MaxDepth = 256,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
        } })
        {
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None,
                Timeout.InfiniteTimeSpan, policy);
            using var token = new CancellationTokenSource();
            Assert.Equal(expected, regex.FindAllToStrings(input, 0, token.Token).ScalarValues);
            Assert.Equal(expected, regex.FindAllToUtf8(input, 0, token.Token).ScalarValues.Select(Encoding.UTF8.GetString));
            Assert.Equal(expected.Length, regex.Count(input, 0, token.Token));
            Assert.Equal(expected.Length, regex.FindIterDetailed(input, 0, token.Token).Length);
        }
    }

    [Fact]
    public void UnusedRepeatJournalsDoNotRemoveRequiredAdmission()
    {
        var input = Encoding.UTF8.GetBytes(new string('y', 512));
        var small = new Utf8PythonRegex("x*", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 256 });
        Assert.Equal(513, small.Count(input));
        Assert.Equal(513, new Utf8PythonRegex("x*", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkingBytes = 1 }).Count(input));
        var denied = new Utf8PythonRegex("(x*)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkingBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input)).ResourceKind);
        var result = new Utf8PythonRegex("x*", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => result.FindAllToStrings(input)).ResourceKind);
        Assert.Throws<ArgumentException>(() => small.Count(new byte[] { (byte)'y', 255 }));
    }
}
