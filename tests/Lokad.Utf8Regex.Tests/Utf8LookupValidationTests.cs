using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Unicode;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LookupValidationTests
{
    [Theory]
    [InlineData(3)] [InlineData(66)] [InlineData(67)]
    public void EveryBytePairAtVectorEdgesAgreesWithIndependentValidity(int offset)
    {
        var input = Enumerable.Repeat((byte)'a', 160).ToArray();
        for (var first = 0; first < 256; first++)
        for (var second = 0; second < 256; second++)
        {
            input[offset] = (byte)first; input[offset + 1] = (byte)second;
            var expected = Utf8.IsValid(input);
            CheckKernels(input, expected);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(7)]
    public void AllWidthsConstraintsBorrowedOffsetsAndTailsKeepValidityAndFirstErrors(int borrowedOffset)
    {
        byte[][] sequences = [
            [0xC2, 0x80], [0xDF, 0xBF], [0xE0, 0xA0, 0x80], [0xED, 0x9F, 0xBF], [0xEF, 0xBF, 0xBF],
            [0xF0, 0x90, 0x80, 0x80], [0xF4, 0x8F, 0xBF, 0xBF],
            [0x80], [0xBF], [0xC0, 0x80], [0xC1, 0xBF], [0xE0, 0x9F, 0xBF], [0xED, 0xA0, 0x80],
            [0xF0, 0x8F, 0xBF, 0xBF], [0xF4, 0x90, 0x80, 0x80], [0xF5, 0x80, 0x80, 0x80],
            [0xC2], [0xE2, 0x82], [0xF0, 0x90, 0x80], [0xFF] ];
        for (var offset = 0; offset < 140; offset++)
        foreach (var tail in new[] { 0, 1, 31, 32, 63, 64, 129 })
        foreach (var sequence in sequences)
        {
            byte[] storage = [.. Enumerable.Repeat((byte)0xFF, borrowedOffset),
                .. Enumerable.Repeat((byte)'a', offset), .. sequence, .. Enumerable.Repeat((byte)'z', tail), 0xFF];
            var input = storage.AsSpan(borrowedOffset, offset + sequence.Length + tail);
            var expected = Utf8.IsValid(input);
            CheckKernels(input, expected);
            Assert.Equal(expected, Utf8ValidationCore.TryValidate(input, out _, out var scalarError));
            Assert.Equal(expected, Utf8InputAnalyzer.TryValidateOnly(input, out var actualError));
            Assert.Equal(scalarError, actualError);
        }
    }

    [Fact]
    public void MixedAsciiAndUnicodeAcrossBlocksKeepsStrictPublicSuffixRejection()
    {
        var valid = Encoding.UTF8.GetBytes("needle " + string.Concat(Enumerable.Repeat("é中😀 " + new string('a', 129), 128)));
        CheckKernels(valid, true);
        var invalid = valid.Concat(new byte[] { 0xF0, 0x90, 0x80 }).ToArray();
        CheckKernels(invalid, false);
        var regex = new Utf8Regex("needle");
        Assert.Equal(1, regex.Count(valid));
        Assert.Throws<ArgumentException>(() => regex.Count(invalid));
        Assert.Throws<ArgumentException>(() => regex.IsMatch(invalid));
        Assert.Throws<ArgumentException>(() => regex.Match(invalid));
    }

    private static void CheckKernels(ReadOnlySpan<byte> input, bool expected)
    {
        if (Avx2.IsSupported)
            Assert.Equal(expected, Utf8ValidationCore.IsValidLookup256(input));
        if (Vector512.IsHardwareAccelerated && Avx512BW.IsSupported)
            Assert.Equal(expected, Utf8ValidationCore.IsValidLookup512(input));
    }
}
