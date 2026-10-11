using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarResultCapacityTests
{
    private static ulong ArrayBytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);
    private static ulong StringBytes(int count) => count == 0 ? 0 : 32UL + ((2UL * ((ulong)count + 1) + 15) & ~15UL);
    private static Utf8SliceInvocation Create(ulong cap) => new(new(0, 0, 0, cap), Regex.InfiniteMatchTimeout, default);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(17)] [InlineData(257)]
    public void ArrayCapacityIsCumulativeAndWorkingReleaseNeverRefundsIt(int count)
    {
        var bytes = ArrayBytes<string>(count);
        var invocation = Create(Math.Max(1UL, bytes));
        Assert.True(invocation.Ledger.TryReserve(7));
        Utf8InvocationResultLedger results = default;
        var retained = results.AllocateArray<string>(count, ref invocation);
        Assert.Equal(count, retained.Length);
        Assert.Equal(bytes, invocation.Budget.ResultBytes);
        results.Release(ref invocation);
        results.Release(ref invocation);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(bytes, invocation.Budget.ResultBytes);
        if (count == 0) return;
        Assert.Throws<InvalidOperationException>(() => results.AllocateArray<string>(count, ref invocation));
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, invocation.Budget.LimitBreach);
        Assert.Equal(bytes * 2, invocation.Budget.ResultBytes);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(count, retained.Length);

        var denied = Create(bytes - 1);
        Assert.True(denied.Ledger.TryReserve(7));
        Utf8InvocationResultLedger rejected = default;
        Assert.Throws<InvalidOperationException>(() => rejected.AllocateArray<string>(count, ref denied));
        Assert.Equal(0UL, rejected.ReservedBytes);
        Assert.Equal(7UL, denied.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(2049)]
    public void StringCapacityPrecedesAllocationAndPreservesExactWorkingLoans(int chars)
    {
        var text = chars == 2 ? "😀" : new string('é', chars);
        var input = Encoding.UTF8.GetBytes(text);
        var bytes = StringBytes(chars);
        var invocation = new Utf8SliceInvocation(new(0, 0, 13 + bytes, Math.Max(1UL, bytes)), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        Utf8InvocationResultLedger results = default;
        var retained = results.DecodeUtf8(input, chars, ref invocation);
        Assert.Equal(text, retained);
        Assert.Equal(bytes, invocation.Budget.ResultBytes);
        Assert.Equal(13 + bytes, invocation.Ledger.ReservedBytes);
        results.Release(ref invocation);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(text, retained);
        Assert.Equal(bytes, invocation.Budget.ResultBytes);
        if (bytes == 0) return;
        var denied = Create(bytes - 1);
        Assert.True(denied.Ledger.TryReserve(13));
        Utf8InvocationResultLedger rejected = default;
        Assert.Throws<InvalidOperationException>(() => rejected.DecodeUtf8(input, chars, ref denied));
        Assert.Equal(0UL, rejected.ReservedBytes);
        Assert.Equal(13UL, denied.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, denied.Budget.LimitBreach);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(16)] [InlineData(17)]
    [InlineData(33)] [InlineData(257)] [InlineData(513)]
    public void StagingAndFinalExtentsUseOneCumulativeAllowance(int count)
    {
        ulong stagingBytes = 0;
        var capacity = 0;
        if (count > 0)
        {
            capacity = 16;
            stagingBytes = ArrayBytes<byte[]>(capacity);
            while (capacity < count) { capacity *= 2; stagingBytes += ArrayBytes<byte[]>(capacity); }
        }
        var finalBytes = ArrayBytes<byte[]>(count);
        var invocation = Create(Math.Max(1UL, stagingBytes + finalBytes));
        Assert.True(invocation.Ledger.TryReserve(7));
        Utf8InvocationResultBuffer<byte[]> staging = default;
        Utf8InvocationResultLedger results = default;
        var child = new byte[] { 0x7f };
        try
        {
            for (var i = 0; i < count; i++) staging.Add(child, ref invocation);
            Assert.Equal(stagingBytes, invocation.Budget.ResultBytes);
            Assert.Equal(7 + ArrayBytes<byte[]>(capacity), invocation.Ledger.ReservedBytes);
            var retained = staging.ToArray(ref results, ref invocation);
            Assert.Equal(count, retained.Length);
            Assert.All(retained, value => Assert.Same(child, value));
            Assert.Equal(stagingBytes + finalBytes, invocation.Budget.ResultBytes);
            staging.Release(ref invocation);
            results.Release(ref invocation);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            Assert.Equal(stagingBytes + finalBytes, invocation.Budget.ResultBytes);
            Assert.All(retained, value => Assert.Equal(new byte[] { 0x7f }, value));
        }
        finally { staging.Release(ref invocation); results.Release(ref invocation); }
    }

    [Fact]
    public void DeniedStagingGrowthKeepsThePriorBufferAndBorrowedChildrenValid()
    {
        var initial = ArrayBytes<byte[]>(16);
        var growth = ArrayBytes<byte[]>(32);
        var invocation = Create(initial + growth - 1);
        Assert.True(invocation.Ledger.TryReserve(7));
        Utf8InvocationResultBuffer<byte[]> staging = default;
        var child = new byte[] { 42 };
        try
        {
            for (var i = 0; i < 16; i++) staging.Add(child, ref invocation);
            Assert.Throws<InvalidOperationException>(() => staging.Add(child, ref invocation));
            Assert.Equal(16, staging.Count);
            Assert.Same(child, staging[15]);
            Assert.Equal(initial + growth, invocation.Budget.ResultBytes);
            Assert.Equal(7 + initial, invocation.Ledger.ReservedBytes);
            Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, invocation.Budget.LimitBreach);
        }
        finally { staging.Release(ref invocation); }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(new byte[] { 42 }, child);
        Assert.Equal(initial + growth, invocation.Budget.ResultBytes);
    }

    [Fact]
    public void DeniedFinalRootKeepsStagingFundedUntilCleanup()
    {
        var stagingBytes = ArrayBytes<byte[]>(16) + ArrayBytes<byte[]>(32);
        var total = stagingBytes + ArrayBytes<byte[]>(17);
        var invocation = Create(total - 1);
        Assert.True(invocation.Ledger.TryReserve(7));
        Utf8InvocationResultBuffer<byte[]> staging = default;
        Utf8InvocationResultLedger results = default;
        var child = new byte[] { 42 };
        try
        {
            for (var i = 0; i < 17; i++) staging.Add(child, ref invocation);
            Assert.Throws<InvalidOperationException>(() => staging.ToArray(ref results, ref invocation));
            Assert.Equal(17, staging.Count);
            Assert.Same(child, staging[16]);
            Assert.Equal(0UL, results.ReservedBytes);
            Assert.Equal(7 + ArrayBytes<byte[]>(32), invocation.Ledger.ReservedBytes);
            Assert.Equal(total, invocation.Budget.ResultBytes);
        }
        finally { staging.Release(ref invocation); results.Release(ref invocation); }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(new byte[] { 42 }, child);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LargeArrayAndTextAreDeniedBeforeActualAcquisition(bool text)
    {
        var input = new byte[1000000];
        Array.Fill(input, (byte)'x');
        var warm = Create(1);
        Utf8InvocationResultLedger warmed = default;
        Assert.Throws<InvalidOperationException>(() => warmed.AllocateArray<byte>(input.Length, ref warm));
        var invocation = Create(1);
        Assert.True(invocation.Ledger.TryReserve(7));
        Utf8InvocationResultLedger results = default;
        var before = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            if (text) results.DecodeUtf8(input, input.Length, ref invocation);
            else results.AllocateArray<byte>(input.Length, ref invocation);
            Assert.Fail("A large result must be denied before acquisition.");
        }
        catch (InvalidOperationException) { Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, invocation.Budget.LimitBreach); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 16384, $"Denied result allocated {allocated} bytes.");
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(0UL, results.ReservedBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ScalarOutputFundsInitialGrowthAndFinalCapacity(int phase)
    {
        var initial = ArrayBytes<byte>(256);
        var growth = ArrayBytes<byte>(512);
        var final = ArrayBytes<byte>(400);
        var cap = phase == 0 ? initial + growth + final : phase == 1 ? initial + growth - 1 : initial + growth + final - 1;
        var invocation = Create(cap);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.True(Utf8SliceOutputWriter.Rent(256, ref invocation, out var writer));
        var input = Enumerable.Repeat((byte)'x', 400).ToArray();
        try
        {
            Assert.Equal(initial, invocation.Budget.ResultBytes);
            if (phase == 1)
            {
                Assert.False(writer.Append(input, ref invocation));
                Assert.Equal(0, writer.Length);
                Assert.Equal(7 + initial, invocation.Ledger.ReservedBytes);
                Assert.Equal(initial + growth, invocation.Budget.ResultBytes);
            }
            else
            {
                Assert.True(writer.Append(input, ref invocation));
                Assert.Equal(initial + growth, invocation.Budget.ResultBytes);
                var success = writer.TryToArrayAndRelease(ref invocation, out var result);
                Assert.Equal(phase == 0, success);
                if (success) { Assert.Equal(input, result); Assert.Equal(7UL, invocation.Ledger.ReservedBytes); }
                else { Assert.Empty(result); Assert.Equal(400, writer.Length); Assert.Equal(7 + growth, invocation.Ledger.ReservedBytes); }
                Assert.Equal(initial + growth + final, invocation.Budget.ResultBytes);
            }
        }
        finally { writer.Release(ref invocation); }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        if (phase != 0) Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, invocation.Budget.LimitBreach);
    }

    [Fact]
    public void LargeScalarOutputRentIsDeniedBeforeAllocation()
    {
        var invocation = Create(1);
        Assert.True(invocation.Ledger.TryReserve(7));
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.False(Utf8SliceOutputWriter.Rent(1000000, ref invocation, out var writer));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 16384, $"Denied scalar output allocated {allocated} bytes.");
        writer.Release(ref invocation);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, invocation.Budget.LimitBreach);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancellationAndDeadlinePrecedeRootResultAdmission(bool deadline)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(0, 0, 0, 1),
            deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        if (deadline) clock.Advance(250); else source.Cancel();
        Utf8InvocationResultLedger results = default;
        try { results.AllocateArray<byte>(1000000, ref invocation); Assert.Fail("Interruption must precede admission."); }
        catch (Utf8ExecutionDeadlineExpiredException) { Assert.True(deadline); }
        catch (OperationCanceledException error) { Assert.False(deadline); Assert.Equal(source.Token, error.CancellationToken); }
        Assert.Equal(0UL, invocation.Budget.ResultBytes);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.Budget.LimitBreach);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }
}
