using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiValidityFastReturnTests
{
    [Fact]
    public void WholeAsciiSpansIncludeControlsNulAndEveryTailOffset()
    {
        for (var length = 0; length <= 257; length++)
        for (var offset = 0; offset < 17; offset++)
        {
            var storage = new byte[length + offset + 16];
            Array.Fill(storage, (byte)0xFF);
            for (var i = 0; i < length; i++) storage[offset + i] = (byte)(i & 127);
            Assert.True(Utf8InputAnalyzer.TryValidateOnly(storage.AsSpan(offset, length), out var error));
            Assert.Equal(-1, error);
        }
        for (var value = 0; value <= byte.MaxValue; value++)
        {
            byte[] input = [(byte)value];
            Assert.Equal(Utf8.IsValid(input), Utf8InputAnalyzer.TryValidateOnly(input, out var error));
            Assert.Equal(value < 128 ? -1 : 0, error);
        }
    }

    [Theory]
    [InlineData("é")]
    [InlineData("中")]
    [InlineData("\u0800\uD7FF")]
    [InlineData("😀\U0010FFFF")]
    [InlineData("K")]
    public void LateUnicodeAndMalformedSuffixKeepCompleteSubjectValidation(string suffix)
    {
        foreach (var length in new[] { 0, 15, 16, 31, 32, 63, 64, 255, 256, 2047, 2048, 65536 })
        {
            var valid = Encoding.UTF8.GetBytes("ss" + new string('x', length) + suffix);
            Assert.True(Utf8InputAnalyzer.TryValidateOnly(valid, out var error));
            Assert.Equal(-1, error);
            byte[] invalid = [.. valid, 0xF0, 0x9F];
            var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(invalid));
            Assert.False(Utf8InputAnalyzer.TryValidateOnly(invalid, out error));
            Assert.Equal(valid.Length, error);
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
            {
                var regex = new Utf8Regex(".*(ss)", options);
                Assert.True(regex.IsMatch(valid));
                Assert.Equal(expected.Message, Assert.Throws<ArgumentException>(() => regex.IsMatch(invalid)).Message);
            }
        }
    }

    [Fact]
    public void AnAsciiProofRetainsInvocationLimitsAndOriginalToken()
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 8192) + "ss");
        var limited = new Utf8Regex(".*(ss)", RegexOptions.Compiled, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.IsMatch(input)).ResourceKind);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Regex(".*(ss)");
        Assert.True(regex.IsMatch(input, token.Token));
        token.Cancel();
        Assert.Equal(token.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, token.Token)).CancellationToken);
    }
}
