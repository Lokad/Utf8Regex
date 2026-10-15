using System.Text;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValidationPrefixTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(15)] [InlineData(16)] [InlineData(17)]
    [InlineData(31)] [InlineData(32)] [InlineData(33)] [InlineData(63)] [InlineData(64)]
    [InlineData(65)] [InlineData(127)] [InlineData(128)] [InlineData(129)]
    [InlineData(2047)] [InlineData(2048)] [InlineData(4095)]
    public void AsciiPrefixKeepsWholeInputCoordinatesAndUnicodeFlags(int prefixLength)
    {
        foreach (var offset in new[] { 0, 1, 17, 31 })
        foreach (var suffix in new[] { "", "é", "K", "😀", "éxKy😀" })
        {
            var text = new string('a', prefixLength) + suffix + new string('b', 31);
            var bytes = Encoding.UTF8.GetBytes(text);
            byte[] storage = [.. Enumerable.Repeat((byte)0xff, offset), .. bytes, 0xff];
            var result = Utf8InputAnalyzer.ValidateOnly(storage.AsSpan(offset, bytes.Length), out var kelvin);
            Assert.Equal(bytes.Length, result.ByteLength);
            Assert.Equal(text.Length, result.Utf16Length);
            Assert.Equal(suffix.Length == 0, result.IsAscii);
            Assert.Equal(suffix.Contains("😀", StringComparison.Ordinal), result.ContainsSupplementaryScalars);
            Assert.Equal(suffix.Contains('K'), kelvin);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(15)] [InlineData(16)] [InlineData(17)]
    [InlineData(31)] [InlineData(32)] [InlineData(33)] [InlineData(63)] [InlineData(64)]
    [InlineData(65)] [InlineData(127)] [InlineData(128)] [InlineData(129)]
    [InlineData(2047)] [InlineData(2048)] [InlineData(4095)]
    public void MalformedSuffixReportsTheOriginalByteOffset(int prefixLength)
    {
        foreach (var offset in new[] { 0, 1, 17, 31 })
        foreach (var scalarPrefix in new[] { "", "K😀z" })
        foreach (var malformed in new[] { new byte[] { 0xff }, new byte[] { 0xc2 },
            new byte[] { 0xc2, (byte)'x' }, new byte[] { 0xe2, 0x82 },
            new byte[] { 0xed, 0xa0, 0x80 }, new byte[] { 0xf4, 0x90, 0x80, 0x80 } })
        {
            var prefix = Encoding.UTF8.GetBytes(new string('a', prefixLength) + scalarPrefix);
            byte[] storage = [.. Enumerable.Repeat((byte)0xff, offset), .. prefix, .. malformed, 0xff];
            var failure = Assert.Throws<ArgumentException>(() =>
                Utf8InputAnalyzer.ValidateOnly(storage.AsSpan(offset, prefix.Length + malformed.Length)));
            Assert.Equal("byteOffset", failure.ParamName);
            Assert.Contains($"byte offset {prefix.Length}.", failure.Message);
        }
    }
}
