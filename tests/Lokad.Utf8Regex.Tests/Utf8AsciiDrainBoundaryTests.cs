using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiDrainBoundaryTests
{
    [Theory]
    [InlineData(255)] [InlineData(256)] [InlineData(257)]
    [InlineData(511)] [InlineData(512)] [InlineData(513)]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void LongAsciiIslandsPreserveTheFirstMalformedByte(int length)
    {
        var ascii = new string(Enumerable.Range(0, length).Select(index => (char)(index & 127)).ToArray());
        var text = "中文" + ascii + "éK😀" + ascii + "尾";
        var encoded = Encoding.UTF8.GetBytes(text);
        foreach (var offset in new[] { 0, 1, 31 })
        {
            byte[] storage = [.. Enumerable.Repeat((byte)0xFF, offset), .. encoded, 0xFF];
            var input = storage.AsSpan(offset, encoded.Length);
            Assert.True(Utf8Validation.TryValidateOnly(input, out var error));
            Assert.Equal(-1, error);
            var invocation = new Utf8SliceInvocation(new(0, 17), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(17));
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8Validation.TryValidateOnly(input, ref invocation));
            Assert.Equal(17UL, invocation.Ledger.ReservedBytes);

            var malformedAt = 6 + length / 2;
            input[malformedAt] = 0xFF;
            input[^1] = 0xFF;
            Assert.False(Utf8Validation.TryValidateOnly(input, out var vectorError));
            Assert.Equal(malformedAt, vectorError);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(15)] [InlineData(16)] [InlineData(17)]
    [InlineData(31)] [InlineData(32)] [InlineData(33)] [InlineData(63)] [InlineData(64)]
    [InlineData(65)] [InlineData(127)] [InlineData(128)] [InlineData(129)]
    public void WholeScalarStopsAndTailsKeepCoordinatesOverBorrowedOffsets(int length)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        foreach (var offset in new[] { 0, 1, 7, 31 })
        foreach (var scalar in new[] { "", "é", "中", "😀" })
        {
            var text = "😀" + new string('a', length) + scalar + new string('b', length);
            var encoded = Encoding.UTF8.GetBytes(text);
            byte[] storage = [.. Enumerable.Repeat((byte)0xff, offset), .. encoded, 0xff];
            var subject = storage.AsSpan(offset, encoded.Length);
            var invocation = new Utf8SliceInvocation(new(0, 13), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var copy = invocation;
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8Validation.TryValidateOnly(subject, ref invocation));
            Assert.Equal(invocation.Budget.DiagnosticCheckpoints, copy.Budget.DiagnosticCheckpoints);
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
            var invocation = new Utf8SliceInvocation(new(0, 13), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var failure = Assert.Throws<ArgumentException>(() => Utf8Validation.TryValidateOnly(subject, ref invocation));
            Assert.Contains($"byte offset {prefix.Length}", failure.Message);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void CrossBlockValidationRetainsExactWorkDenialAndCallerLoans()
    {
        var input = Encoding.UTF8.GetBytes("😀" + new string('a', 2043) + "中" + new string('b', 2047) + "é");
        var baseline = new Utf8SliceInvocation(new(0, 13), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8Validation.TryValidateOnly(input, ref baseline));
        var admitted = new Utf8SliceInvocation(new(0, 13), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8Validation.TryValidateOnly(input, ref admitted));
    }
}
