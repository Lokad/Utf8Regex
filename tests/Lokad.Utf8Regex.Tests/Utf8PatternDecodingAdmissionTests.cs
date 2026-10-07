using System.Text;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PatternDecodingAdmissionTests
{
    [Theory]
    [InlineData("")] [InlineData("cat")] [InlineData("é中")]
    [InlineData("😀")] [InlineData("a😀é中bcdefghijklmnop")]
    public void DecodedStringChargeCoversActualAllocationAndItsTerminator(string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var encoding = new UTF8Encoding(false, true);
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, ulong.MaxValue);
        _ = budget.DecodeUtf8(input, encoding);
        budget.ReleaseBytes(budget.ReservedBytes);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var decoded = budget.DecodeUtf8(input, encoding);
        var allocated = (ulong)(GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(text, decoded);
        Assert.True(allocated <= budget.ReservedBytes);
        Assert.Equal(0ul, budget.Reserved);
        if (text.Length == 0)
        {
            Assert.Same(string.Empty, decoded);
            Assert.Equal(0ul, allocated);
            Assert.Equal(0ul, budget.ReservedBytes);
        }
        else
        {
            Assert.True(allocated > 0);
            Assert.True(budget.ReservedBytes >= (ulong)(2 * (text.Length + 1)) + 32);
        }
    }

    [Theory]
    [InlineData("ascii")] [InlineData("é中")]
    [InlineData("😀")] [InlineData("a😀b😀c")]
    public void OutputAcquisitionIsDeniedBeforeAllocatingAndPreservesParentLoan(string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var bytes = Utf8ConstructionBudget.StringBytes(text.Length);
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, bytes + 47);
        var parent = budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            budget.DecodeUtf8(input, Encoding.UTF8));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(bytes + 48, failure.Requested);
        Assert.Equal(48ul, budget.ReservedBytes);
        Assert.Equal(48ul, budget.PeakBytes);
        budget.ReleaseArray(parent, false);
        Assert.Equal(text, budget.DecodeUtf8(input, Encoding.UTF8));
        Assert.Equal(bytes, budget.ReservedBytes);
    }

    [Fact]
    public void StrictValidationDoesNotCreateAStringLoan()
    {
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, ulong.MaxValue);
        var parent = budget.AllocateArray<byte>(1, false);
        Assert.Throws<DecoderFallbackException>(() =>
            budget.DecodeUtf8(new byte[] { 0xf0, 0x9f, 0x98 }, new UTF8Encoding(false, true)));
        Assert.Equal(48ul, budget.ReservedBytes);
        Assert.Equal("cat", budget.DecodeUtf8("cat"u8, Encoding.UTF8));
        Assert.Equal(48ul + Utf8ConstructionBudget.StringBytes(3), budget.ReservedBytes);
        GC.KeepAlive(parent);
    }

    [Fact]
    public void LargestStringExtentUsesCheckedWidthWithoutWrapping()
    {
        Assert.Equal(4_294_967_328ul, Utf8ConstructionBudget.StringBytes(int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => Utf8ConstructionBudget.StringBytes(-1));
    }

}
