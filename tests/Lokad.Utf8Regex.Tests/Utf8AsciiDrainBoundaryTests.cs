using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiDrainBoundaryTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(15)] [InlineData(16)] [InlineData(17)]
    [InlineData(31)] [InlineData(32)] [InlineData(33)] [InlineData(63)] [InlineData(64)]
    [InlineData(65)] [InlineData(127)] [InlineData(128)] [InlineData(129)]
    public void WholeScalarStopsAndTailsKeepCoordinatesOverBorrowedOffsets(int length)
    {
        foreach (var offset in new[] { 0, 1, 7, 31 })
        foreach (var scalar in new[] { "", "é", "中", "😀" })
        {
            var text = "😀" + new string('a', length) + scalar + new string('b', length);
            var encoded = Encoding.UTF8.GetBytes(text);
            byte[] storage = [.. Enumerable.Repeat((byte)0xff, offset), .. encoded, 0xff];
            var subject = storage.AsSpan(offset, encoded.Length);
            Assert.True(Utf8ValidationCore.TryValidate(subject, true, out var expected, out var error));
            Assert.Equal(-1, error);
            Assert.Equal(encoded.Length, expected.ByteLength);
            Assert.Equal(text.Length, expected.Utf16Length);
            Assert.True(expected.ContainsSupplementaryScalars);
            Assert.False(expected.IsAscii);

            var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 13), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var copy = invocation;
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8Validation.TryValidate(subject, ref invocation, out var actual));
            Assert.Equal(expected, actual);
            Assert.Equal(invocation.Budget.Steps, copy.Budget.Steps);
            Assert.Equal(13UL, copy.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(15)] [InlineData(16)] [InlineData(17)] [InlineData(31)] [InlineData(32)]
    [InlineData(33)] [InlineData(63)] [InlineData(64)] [InlineData(65)]
    public void FirstMalformedByteAndScalarLeadRemainAuthoritativeAfterAsciiRuns(int length)
    {
        var prefix = Encoding.UTF8.GetBytes("é" + new string('a', length));
        foreach (var malformed in new[] { new byte[] { 0xff }, new byte[] { 0xe2, 0x82 },
            new byte[] { 0xc2, (byte)'x' }, new byte[] { 0xed, 0xa0, 0x80 }, new byte[] { 0xf4, 0x90, 0x80, 0x80 } })
        {
            byte[] subject = [.. prefix, .. malformed];
            Assert.False(Utf8ValidationCore.TryValidate(subject, true, out _, out var error));
            Assert.Equal(prefix.Length, error);
            var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 13), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var failure = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(subject, ref invocation));
            Assert.Contains($"byte offset {prefix.Length}", failure.Message);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void CrossBlockValidationRetainsExactWorkDenialAndCallerLoans()
    {
        var input = Encoding.UTF8.GetBytes("😀" + new string('a', 2043) + "中" + new string('b', 2047) + "é");
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 13), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8Validation.TryValidate(input, ref baseline, out var expected));
        for (var cap = 1UL; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 13), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8Validation.TryValidate(input, ref invocation, out var denied));
            Assert.Equal(default, denied);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        var admitted = new Utf8SliceInvocation(new(baseline.Budget.Steps, 0, 13), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8Validation.TryValidate(input, ref admitted, out var actual));
        Assert.Equal(expected, actual);
    }
}
