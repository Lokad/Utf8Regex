using System.Text;
using System.Text.Unicode;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MixedWidthValidationTests
{
    [Theory]
    [InlineData("éЯ")]
    [InlineData("中")]
    [InlineData("\u0800\uD7FF")]
    [InlineData("😀\U0010FFFF")]
    public void MixedWidthsPreserveStrictValidityAndOriginalErrorOffsets(string scalars)
    {
        foreach (var prefix in new[] { 0, 1, 31, 32, 33, 63, 64, 255, 256, 257, 2047, 2048 })
        {
            var valid = Encoding.UTF8.GetBytes(new string('x', prefix) + string.Concat(Enumerable.Repeat(scalars, 48)));
            Assert.True(Utf8InputAnalyzer.TryValidateOnly(valid, out var error));
            Assert.Equal(-1, error);
            foreach (var suffix in new byte[][] { [0x80], [0xF0, 0x9F], [0xED, 0xA0, 0x80], [0xC0, 0x80] })
            {
                byte[] invalid = [.. valid, .. suffix];
                Assert.False(Utf8.IsValid(invalid));
                Assert.False(Utf8InputAnalyzer.TryValidateOnly(invalid, out error));
                Assert.Equal(valid.Length, error);
            }
        }
    }
}
