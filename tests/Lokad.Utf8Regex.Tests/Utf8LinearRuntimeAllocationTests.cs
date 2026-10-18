using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8LinearRuntimeAllocationTests
{
    [Fact]
    public void WarmNestedRuntimeHasNoTemporaryAllocation()
    {
        var program = Utf8LinearRuntimeContinuationTests.Nested(65, true);
        var input = Encoding.ASCII.GetBytes("ababac" + new string('c', 65) + "x");
        // Warm the same call site and allocation counter used by measurement.
        // Keep this boundary separate from the test host's own tiered method.
        _ = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(100, MatchMany(program, input, 100));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var hits = MatchMany(program, input, 1000);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1000, hits);
        Assert.Equal(0, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int MatchMany(Utf8ByteSafeLinearVerifierProgram program, ReadOnlySpan<byte> input, int iterations)
    {
        var hits = 0;
        for (var index = 0; index < iterations; index++) if (program.TryMatch(input, 0, out _)) hits++;
        return hits;
    }
}
