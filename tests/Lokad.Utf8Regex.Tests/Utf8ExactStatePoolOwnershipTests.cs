using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8ExactStatePoolOwnershipTests
{
    private readonly record struct HeaderState(long Value);

    [Fact]
    public void RetainedTypeLoanIncludesItsArrayHeaderAndRounding()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var items = Utf8ExactStatePool<HeaderState>.Rent(16, true);
            Utf8ExactStatePool<HeaderState>.Return(items, true);
            Assert.Equal(Utf8ConstructionBudget.ArrayBytes<HeaderState>(16), (ulong)Utf8ExactStatePool<HeaderState>.RetainedBytes);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void HeaderDenialPrecedesWorkingBufferAcquisition()
    {
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 128), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var buffer = new Utf8InvocationStateBuffer<long>();
        try
        {
            Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, buffer.TryPush(1, ref invocation));
            Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
        }
        finally { buffer.Release(ref invocation); }
    }

    [Fact]
    public void ExactStateRetentionHasOneNonGenericOwner()
    {
        Assert.NotNull(typeof(Utf8Regex).Assembly.GetType("Lokad.Utf8Regex.Internal.Execution.Utf8ExactStatePool"));
        Assert.DoesNotContain(typeof(Utf8ExactStatePool<HeaderState>).GetFields(BindingFlags.Static | BindingFlags.NonPublic),
            field => field.IsDefined(typeof(ThreadStaticAttribute)));
    }
}
