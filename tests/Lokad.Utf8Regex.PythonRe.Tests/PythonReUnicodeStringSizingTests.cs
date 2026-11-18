using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReUnicodeStringSizingTests
{
    [Fact]
    public void SingleCharacterOutputsKeepControlsUnicodeAndCapturedSeparators()
    {
        var characters = Enumerable.Range(0, 128).Select(value => (char)value).ToArray();
        var text = new string(characters) + "é中😀";
        using var each = new Utf8PythonRegex("[\\s\\S]");
        Assert.Equal(characters.Select(value => value.ToString()).Concat(["é", "中", "😀"]),
            PythonReFindAllAssert.Decode(each.FindAllToUtf8(Encoding.UTF8.GetBytes(text))).ScalarValues);
        using var split = new Utf8PythonRegex("([,;])");
        Assert.Equal(new string?[] { "α", ",", "😀", ";", "é", ",", "" },
            PythonReSplitAssert.Decode("α,😀;é,"u8, split.SplitRanges("α,😀;é,"u8, 0, 0, default)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4086)]
    [InlineData(4087)]
    [InlineData(4088)]
    [InlineData(4092)]
    [InlineData(4093)]
    [InlineData(4094)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void InteriorUnicodeStringsKeepOffsetsCapturesAndBlockTails(int prefixLength)
    {
        var word = new string('a', prefixLength) + "é中😀";
        var input = Encoding.UTF8.GetBytes("😀" + word + "，" + word + ",尾😀");
        foreach (var funded in new[] { false, true })
        {
            var policy = funded ? new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1_000_000,
                MaxDepth = 1024,
                MaxWorkingBytes = 1_048_576,
                MaxResultBytes = 1_048_576
            } : default;
            using var plain = new Utf8PythonRegex("[,，]", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = policy
            });
            using var captured = new Utf8PythonRegex("([,，])", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = policy
            });
            Assert.Equal(new string?[] { word, word, "尾😀" }, PythonReSplitAssert.Decode(input, plain.SplitRanges(input, 0, 4, default)));
            Assert.Equal(new string?[] { word, "，", word, ",", "尾😀" }, PythonReSplitAssert.Decode(input, captured.SplitRanges(input, 0, 4, default)));
            Assert.Equal(new string?[] { word, word + ",尾😀" }, PythonReSplitAssert.Decode(input, plain.SplitRanges(input, 1, 4, default)));
            using var findall = new Utf8PythonRegex("([^,，]+)");
            Assert.Equal(new string?[] { word, word, "尾😀" },
                PythonReFindAllAssert.Decode(findall.FindAllToUtf8(Encoding.UTF8.GetBytes(word + "，" + word + ",尾😀"))).ScalarValues);
        }
    }
}
