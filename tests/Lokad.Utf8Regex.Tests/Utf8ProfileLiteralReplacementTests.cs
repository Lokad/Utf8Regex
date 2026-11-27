using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProfileLiteralReplacementTests
{
    [Fact]
    public void BorrowedInvocationKeepsExactUtf8ReplacementAndOriginalAdmission()
    {
        var regex = new Utf8Regex("foo");
        var input = Encoding.UTF8.GetBytes("é😀foo 中foo!");
        var invocation = new Utf8SliceInvocation(new(0, 1 << 20), Timeout.InfiniteTimeSpan, default)
;
        Assert.True(invocation.Ledger.TryReserve(17));
        Utf8InvocationResultLedger results = default;
        byte[]? retained = null;
        try
        {
            retained = regex.ReplaceExactLiteralForResult(input, "😀é"u8, 1, "é😀"u8.Length,
                ref invocation, ref results, out var count);
            Assert.Equal(1, count);
            Assert.Equal("é😀😀é 中foo!"u8.ToArray(), retained);
            Assert.Equal(17 + results.ReservedBytes, invocation.Ledger.ReservedBytes);
        }
        finally { results.Release(ref invocation); }
        Assert.Equal(17UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(17);
        input.AsSpan().Fill((byte)'x');
        Assert.Equal("é😀😀é 中foo!"u8.ToArray(), retained);
    }
}
