using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ResultExtentAdmissionTests
{
    private static ulong ArrayBytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResultDenialPrecedesWorkingAdmission(bool denyResult)
    {
        const ulong borrowed = 7;
        var extent = ArrayBytes<string>(1);
        var invocation = new Utf8SliceInvocation(new(0, borrowed + extent - 1, denyResult ? extent - 1 : extent),
            Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(invocation.Ledger.TryReserve(borrowed));
        Utf8InvocationResultLedger results = default;

        Assert.Throws<InvalidOperationException>(() => results.AllocateArray<string>(1, ref invocation));
        Assert.Equal(denyResult ? Utf8ExecutionLimitKind.ResultBytes : Utf8ExecutionLimitKind.WorkingBytes,
            invocation.Budget.LimitBreach);
        Assert.Equal(extent, invocation.Budget.ResultBytes);
        Assert.Equal(denyResult ? 0UL : borrowed + extent, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(borrowed, invocation.Ledger.ReservedBytes);
        Assert.Equal(0UL, results.ReservedBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(17)] [InlineData(257)]
    public void ReferenceRootsReserveCompleteExtentsBeforeAllocation(int count)
    {
        foreach (var allow in new[] { false, true })
        {
            if (!allow && count == 0) continue;
            var invocation = new Utf8SliceInvocation(new(0, 7 + ArrayBytes<string>(count) - (allow ? 0UL : 1UL)),
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
}
