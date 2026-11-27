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
        ReadOnlyMemory<byte> snapshots1Input = Encoding.UTF8.GetBytes(text);
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = each.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(characters.Select(value => value.ToString()).Concat(["é", "中", "😀"]),
                    snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
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
            ReadOnlyMemory<byte> snapshots2Input = Encoding.UTF8.GetBytes(word + "，" + word + ",尾😀");
            var snapshots2 = new List<Utf8PythonRangeMatch>();
            using (var snapshots2Cursor = findall.CreateMatchCursor(snapshots2Input, 0, default))
            {
                while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                    snapshots2.Add(snapshots2Match);
            }
            Assert.Equal(new string?[] { word, word, "尾😀" },
                            snapshots2.Select(captureSnapshot2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureSnapshot2.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        }
    }
}
