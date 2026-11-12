using System.Runtime.Intrinsics;
using System.Text;
using System.Text.Unicode;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstrainedTripleValidationTests
{
    [Theory]
    [InlineData(0xE0)] [InlineData(0xED)]
    public void EverySecondAndThirdByteAgreesWithIndependentValidityAndScalarErrorOffsets(byte lead)
    {
        AssertRequestedWidth();
        var prefix = Encoding.UTF8.GetBytes(new string('中', 32));
        byte[] input = [.. prefix, lead, 0, 0, .. Encoding.UTF8.GetBytes(new string('文', 32))];
        for (var second = 0; second < 256; second++)
        for (var third = 0; third < 256; third++)
        {
            input[prefix.Length + 1] = (byte)second;
            input[prefix.Length + 2] = (byte)third;
            var expected = Utf8.IsValid(input);
            Assert.Equal(expected, Utf8ValidationCore.TryValidate(input, true, out _, out var scalarError));
            Assert.Equal(expected, Utf8ValidationCore.TryValidate(input, false, out _, out var vectorError));
            Assert.Equal(scalarError, vectorError);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(7)]
    public void WideLanesBorrowedOffsetsTailsAndEarlierErrorsKeepTheOriginalFirstError(int sliceOffset)
    {
        AssertRequestedWidth();
        foreach (var lead in new byte[] { 0xE0, 0xED })
        for (var lane = 0; lane < 128; lane++)
        foreach (var tailLength in new[] { 0, 1, 2, 31, 32, 63, 64, 129 })
        {
            var prefix = Encoding.UTF8.GetBytes("中文" + new string('a', lane));
            byte[] storage = [.. Enumerable.Repeat((byte)0xFF, sliceOffset), .. prefix,
                lead, lead == 0xE0 ? (byte)0xA0 : (byte)0x9F, 0x80,
                .. Enumerable.Repeat((byte)'b', tailLength), 0xFF];
            var input = storage.AsSpan(sliceOffset, prefix.Length + 3 + tailLength);
            Check(input);
            input[prefix.Length + 1] = lead == 0xE0 ? (byte)0x9F : (byte)0xA0;
            Check(input);
            // Earlier missing continuations must win over a later constrained error.
            input[0] = 0xC2; input[1] = (byte)'x';
            Check(input);
        }
    }

    private static void Check(ReadOnlySpan<byte> input)
    {
        var expected = Utf8.IsValid(input);
        Assert.Equal(expected, Utf8ValidationCore.TryValidate(input, true, out _, out var scalarError));
        Assert.Equal(expected, Utf8ValidationCore.TryValidate(input, false, out _, out var vectorError));
        Assert.Equal(scalarError, vectorError);
    }

    private static void AssertRequestedWidth()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_EnableHWIntrinsic") == "0") Assert.False(Vector256.IsHardwareAccelerated);
        if (Environment.GetEnvironmentVariable("DOTNET_EnableAVX512") == "0") Assert.False(Vector512.IsHardwareAccelerated);
    }
}
