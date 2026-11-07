using System.Text;
using System.Text.Unicode;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValidatorSelectorTests
{
    [Fact]
    public void EveryByteClassKeepsTheEightLeadThresholdAcrossSlicesAndTails()
    {
        foreach (var offset in new[] { 0, 1, 15, 31, 32, 33 })
        foreach (var length in new[] { 0, 7, 8, 15, 16, 31, 32, 33, 63, 64, 65, 255, 256, 257 })
        for (var value = 0; value <= byte.MaxValue; value++)
        {
            var storage = new byte[offset + length + 32];
            Array.Fill(storage, (byte)0xFF);
            storage.AsSpan(offset, length).Fill((byte)value);
            var expected = length >= 8 && (value is 0xE0 or 0xED or >= 0xF0)
                ? Utf8InputAnalyzer.ThrowIfInvalidOnlyMode.Utf8IsValid
                : Utf8InputAnalyzer.ThrowIfInvalidOnlyMode.ValidationCore;
            Assert.Equal(expected, Utf8InputAnalyzer.SelectThrowIfInvalidOnlyMode(storage.AsSpan(offset, length)));
        }
    }

    [Theory]
    [InlineData(7)] [InlineData(8)]
    public void MixedLeadClassesAccumulateAcrossVectorsButStopAtByte256(int count)
    {
        byte[] tags = [0xE0, 0xED, 0xF0, 0xF4, 0xFF, 0xE0, 0xED, 0xF1];
        foreach (var start in new[] { 0, 7, 15, 31, 32, 63, 64, 224, 248, 249, 255, 256 })
        {
            var input = Enumerable.Repeat((byte)0xD0, 288).ToArray();
            for (var index = 0; index < count; index++) input[start + index] = tags[index];
            var expected = count == 8 && start + count <= 256
                ? Utf8InputAnalyzer.ThrowIfInvalidOnlyMode.Utf8IsValid
                : Utf8InputAnalyzer.ThrowIfInvalidOnlyMode.ValidationCore;
            Assert.Equal(expected, Utf8InputAnalyzer.SelectThrowIfInvalidOnlyMode(input));
        }
    }

    [Theory]
    [InlineData("éЯ")]
    [InlineData("中")]
    [InlineData("\u0800\uD7FF")]
    [InlineData("😀\U0010FFFF")]
    public void ValidatorChoicePreservesStrictValidityAndOriginalErrorOffsets(string scalars)
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
