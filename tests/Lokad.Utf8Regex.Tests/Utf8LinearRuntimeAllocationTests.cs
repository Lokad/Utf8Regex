using System.Text;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8LinearRuntimeAllocationTests
{
    [Fact]
    public void WarmNestedRuntimeHasNoTemporaryAllocation()
    {
        var program = Utf8LinearRuntimeContinuationTests.Nested(65, true);
        var input = Encoding.ASCII.GetBytes("ababac" + new string('c', 65) + "x");
        for (var index = 0; index < 100; index++) Assert.True(program.TryMatch(input, 0, out _));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var hits = 0;
        for (var index = 0; index < 1000; index++) if (program.TryMatch(input, 0, out _)) hits++;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1000, hits);
        Assert.Equal(0, allocated);
    }
}
