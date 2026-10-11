using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ResultExtentAdmissionTests
{
    private static ulong ArrayBytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);
    private static ulong StringBytes(int count) => count == 0 ? 0 : 32UL + ((2UL * ((ulong)count + 1) + 15) & ~15UL);

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(2049)]
    public void StringExtentIsAdmittedAndReleasedWithTheGraph(int chars)
    {
        var text = new string('é', chars);
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var allow in new[] { false, true })
        {
            if (!allow && chars == 0) continue;
            var invocation = new Utf8SliceInvocation(new(0, 0, 13 + StringBytes(chars) - (allow ? 0UL : 1UL)),
                Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.True(invocation.Ledger.TryReserve(13));
            Utf8InvocationResultLedger results = default;
            string? result = null;
            Exception? failure = null;
            try { result = results.DecodeUtf8(input, chars, ref invocation); }
            catch (InvalidOperationException error) { failure = error; }
            if (allow)
            {
                Assert.Null(failure);
                Assert.Equal(text, result);
                Assert.Equal(StringBytes(chars), results.ReservedBytes);
                Assert.Equal(13 + StringBytes(chars), invocation.Ledger.ReservedBytes);
            }
            else { Assert.NotNull(failure); Assert.Null(result); Assert.Equal(0UL, results.ReservedBytes); }
            results.Release(ref invocation);
            results.Release(ref invocation);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            if (allow) Assert.Equal(text, result);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(17)] [InlineData(257)]
    public void ReferenceRootsReserveCompleteExtentsBeforeAllocation(int count)
    {
        foreach (var allow in new[] { false, true })
        {
            if (!allow && count == 0) continue;
            var invocation = new Utf8SliceInvocation(new(0, 0, 7 + ArrayBytes<string>(count) - (allow ? 0UL : 1UL)),
                Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.True(invocation.Ledger.TryReserve(7));
            Utf8InvocationResultLedger results = default;
            string[]? root = null;
            try { root = results.AllocateArray<string>(count, ref invocation); }
            catch (InvalidOperationException) { Assert.False(allow); }
            if (allow) { Assert.NotNull(root); Assert.Equal(count, root.Length); }
            else Assert.Null(root);
            Assert.Equal(7 + (allow ? ArrayBytes<string>(count) : 0), invocation.Ledger.ReservedBytes);
            results.Release(ref invocation);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void EveryFinalRootWorkCutoffKeepsStagingAndChildrenFundedUntilCleanup()
    {
        ulong seed;
        {
            var invocation = new Utf8SliceInvocation(new(100000, 0, 1UL << 20), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Utf8InvocationResultLedger ledger = default;
            Utf8InvocationResultBuffer<byte[]> staging = default;
            var child = ledger.AllocateArray<byte>(8193, ref invocation);
            for (var i = 0; i < 513; i++) staging.Add(child, ref invocation);
            seed = invocation.Budget.Steps;
            staging.Release(ref invocation); ledger.Release(ref invocation);
        }
        for (ulong cutoff = 0; cutoff <= 6; cutoff++)
        {
            var invocation = new Utf8SliceInvocation(new(seed + cutoff, 0, 1UL << 20), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.True(invocation.Ledger.TryReserve(13));
            Utf8InvocationResultLedger ledger = default;
            Utf8InvocationResultBuffer<byte[]> staging = default;
            var child = ledger.AllocateArray<byte>(8193, ref invocation);
            for (var i = 0; i < 513; i++) staging.Add(child, ref invocation);
            Assert.Equal(seed, invocation.Budget.Steps);
            byte[][] result = [];
            Exception? failure = null;
            try { result = staging.ToArray(ref ledger, ref invocation); }
            catch (InvalidOperationException error) { failure = error; }
            if (cutoff == 6)
            {
                Assert.Null(failure); Assert.Equal(513, result.Length);
                Assert.All(result, value => Assert.Same(child, value));
            }
            else { Assert.NotNull(failure); Assert.Empty(result); }
            var held = 13 + ArrayBytes<byte>(8193) + ArrayBytes<byte[]>(1024) + (cutoff >= 2 ? ArrayBytes<byte[]>(513) : 0);
            Assert.Equal(held, invocation.Ledger.ReservedBytes);
            Assert.Equal(513, staging.Count);
            staging.Release(ref invocation); ledger.Release(ref invocation);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }
}
