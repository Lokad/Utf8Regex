using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// R02b reusable output storage: the ledger-charged writer round-trips text,
// grows with overlap admission, fail-closes on finite heaps keeping the old
// buffer, and balances to zero on every path.
public sealed class Utf8SliceOutputWriterTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_buffer")]
    private static extern ref byte[]? Buffer(ref Utf8SliceOutputWriter writer);
    private static Utf8SliceInvocation Invoke(uint depth, ulong bytes)
    {
        return new Utf8SliceInvocation(
            new Utf8ExecutionLimits(depth, bytes),
            Regex.InfiniteMatchTimeout,
            CancellationToken.None);
    }

    [Fact]
    public void AsciiAndAstralRoundTrip()
    {
        var invocation = Invoke(0, 0);
        Assert.True(Utf8SliceOutputWriter.Rent(16, ref invocation, out var writer));
        Assert.True(writer.Append("ab"u8, ref invocation));
        Assert.True(writer.AppendUtf16("caf\u00E9\U0001F600Z".AsSpan(), ref invocation));
        Assert.True(writer.TryToArrayAndRelease(ref invocation, out var result));
        Assert.Equal("abcaf\u00E9\U0001F600Z", Encoding.UTF8.GetString(result));
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void GrowthKeepsContentAndBalances()
    {
        var invocation = Invoke(0, 0);
        Assert.True(Utf8SliceOutputWriter.Rent(4, ref invocation, out var writer));
        var payload = new byte[100];
        Array.Fill(payload, (byte)120);
        Assert.True(writer.Append(payload, ref invocation));
        Assert.True(writer.AppendUtf16("yz".AsSpan(), ref invocation));
        Assert.Equal(102, writer.Length);
        Assert.True(writer.TryToArrayAndRelease(ref invocation, out var result));
        Assert.Equal(102, result.Length);
        Assert.Equal((byte)120, result[0]);
        Assert.Equal((byte)122, result[101]);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void FiniteHeapFailClosesGrowthKeepingOldBuffer()
    {
        var invocation = Invoke(0, 96);
        Assert.True(Utf8SliceOutputWriter.Rent(8, ref invocation, out var writer));
        Assert.True(writer.Append("abcd"u8, ref invocation));
        var original = Buffer(ref writer);
        Assert.False(writer.Append("0123456789abcdef"u8, ref invocation));
        Assert.Equal(4, writer.Length);
        Assert.Same(original, Buffer(ref writer));
        Assert.Equal("abcd", Encoding.UTF8.GetString(original!.AsSpan(0, writer.Length)));
        Assert.False(writer.TryToArrayAndRelease(ref invocation, out var result));
        Assert.Empty(result);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.Budget.LimitBreach);
        writer.Release(ref invocation);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void RentBeyondHeapFailsWithoutReservation()
    {
        var invocation = Invoke(0, 47);
        Assert.False(Utf8SliceOutputWriter.Rent(8, ref invocation, out var writer));
        Assert.False(writer.Append("x"u8, ref invocation));
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void EmptyAppendsSucceedWithoutGrowth()
    {
        var invocation = Invoke(0, 0);
        Assert.True(Utf8SliceOutputWriter.Rent(0, ref invocation, out var writer));
        Assert.True(writer.Append(ReadOnlySpan<byte>.Empty, ref invocation));
        Assert.True(writer.AppendUtf16(ReadOnlySpan<char>.Empty, ref invocation));
        Assert.Equal(0, writer.Length);
        Assert.True(writer.TryToArrayAndRelease(ref invocation, out var result));
        Assert.Empty(result);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ReleaseIsIdempotent()
    {
        var invocation = Invoke(0, 0);
        Assert.True(Utf8SliceOutputWriter.Rent(8, ref invocation, out var writer));
        Assert.True(writer.Append("ab"u8, ref invocation));
        Assert.True(writer.TryToArrayAndRelease(ref invocation, out var result));
        Assert.Equal(2, result.Length);
        writer.Release(ref invocation);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void TextEncodingPreservesContentAcrossCapacityAndBlockBoundaries()
    {
        foreach (var text in new[] { "", "word", "é😀中", new string((char)0xd800, 1),
            new string((char)0xdc00, 1), new string('€', 19), new string('€', 20),
            new string('x', 2048), new string('x', 2049), new string('x', 2047) + "😀" })
        foreach (var capacity in new[] { 64, 8192 })
        {
            var invocation = Invoke(64, 1 << 20);
            Assert.True(Utf8SliceOutputWriter.Rent(capacity, ref invocation, out var writer));
            try
            {
                Assert.True(writer.Append("seed:"u8, ref invocation));
                Assert.True(writer.AppendUtf16(text, ref invocation));
                Assert.True(writer.Append(":tail"u8, ref invocation));
                Assert.Equal(Encoding.UTF8.GetBytes("seed:" + text + ":tail"), writer.WrittenBytes.ToArray());
            }
            finally { writer.Release(ref invocation); }
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void EveryShortEncodingWorkCutoffKeepsThePriorLogicalOutput()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var measured = Invoke(64, 1 << 20);
        Assert.True(Utf8SliceOutputWriter.Rent(64, ref measured, out var baseline));
        Assert.True(baseline.Append("seed:"u8, ref measured));
        var seed = measured.Budget.DiagnosticCheckpoints;
        Assert.True(baseline.AppendUtf16("é😀", ref measured));
        var work = measured.Budget.DiagnosticCheckpoints - seed;
        baseline.Release(ref measured);
        Assert.True(work > 0);
    }
}
