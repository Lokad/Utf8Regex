using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralComparisonBlockTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2047, false)] [InlineData(2047, true)]
    [InlineData(2048, false)] [InlineData(2048, true)]
    [InlineData(2049, false)] [InlineData(2049, true)]
    [InlineData(4097, false)] [InlineData(4097, true)]
    public void OffsetPrefixesAndLastByteMismatchKeepBoundedComparisonWork(int length, bool folded)
    {
        var literal = Enumerable.Repeat((byte)'A', length).ToArray();
        foreach (var offset in new[] { 0, 7 })
        {
            var input = Enumerable.Repeat((byte)'!', offset + length + 3).ToArray();
            input.AsSpan(offset, length).Fill(folded ? (byte)'a' : (byte)'A');
            var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryCompareLiteral(input.AsSpan(offset), literal,
                folded, ref invocation, out var equal));
            Assert.True(equal);
            var blocks = (ulong)((length + 2047) / 2048);
            Assert.Equal(blocks, invocation.Budget.Steps);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
            if (length == 0) continue;

            input[offset + length - 1] = (byte)'!';
            var mismatch = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryCompareLiteral(input.AsSpan(offset), literal,
                folded, ref mismatch, out equal));
            Assert.False(equal);
            Assert.Equal(blocks, mismatch.Budget.Steps);
            Assert.Equal(0UL, mismatch.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DeniedNextBlockCannotPublishEqualityOrReleaseCallerStorage(bool folded)
    {
        var literal = Enumerable.Repeat((byte)'A', 4097).ToArray();
        var input = Enumerable.Repeat(folded ? (byte)'a' : (byte)'A', literal.Length).ToArray();
        foreach (var cap in new[] { 1UL, 2UL })
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 13), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8SearchExecutor.TryCompareLiteral(input, literal,
                folded, ref invocation, out var equal));
            Assert.False(equal);
            Assert.Equal(cap + 1, invocation.Budget.Steps);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, invocation.Budget.LimitBreach);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ShorterInputDeclinesWithoutChargingAndAsciiFoldingDoesNotFoldOtherBytes(bool folded)
    {
        var invocation = new Utf8SliceInvocation(new(1, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryCompareLiteral("a"u8, "ab"u8,
            folded, ref invocation, out var equal));
        Assert.False(equal);
        Assert.Equal(0UL, invocation.Budget.Steps);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryCompareLiteral("é"u8, "É"u8,
            folded, ref invocation, out equal));
        Assert.False(equal);
        Assert.Equal(1UL, invocation.Budget.Steps);
    }
}
