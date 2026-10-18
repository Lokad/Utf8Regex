using System.Diagnostics;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReParserCollectionAdmissionTests
{
    [Fact]
    public void SparsePagesDeduplicateAndEnumerateEveryWordPlaneAndPageBoundaryInOrder()
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var scalars = PythonReScalarSet.Create(owner.Budget);
        var expected = new[] { 0, 31, 32, 255, 256, 0xffff, 0x10000, 0x1f600, 0x10ffff };
        foreach (var value in expected.Reverse()) { scalars.Add(value); scalars.Add(value); }
        var actual = new List<int>();
        foreach (var value in scalars) actual.Add(value);
        Assert.Equal(expected, actual);
        Assert.True(owner.Budget.PeakBytes < 8192);
    }

    [Fact]
    public void DuplicateSparseMembersAcquireNoAdditionalStorageOrGcObjects()
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var scalars = PythonReScalarSet.Create(owner.Budget);
        for (var warm = 0; warm < 1000; warm++) scalars.Add(0x1f600);
        // This checks hot duplicate insertion. One helper call can finish before
        // tiered compilation settles, so use a fixed warmup duration independent
        // of its allocation results. Measure once and retain the exact zero gate.
        var warmupStart = Stopwatch.GetTimestamp();
        do { _ = MeasureDuplicateSparseAdds(scalars); }
        while (Stopwatch.GetElapsedTime(warmupStart) < TimeSpan.FromMilliseconds(250));
        var held = owner.Budget.ReservedBytes;
        var allocated = MeasureDuplicateSparseAdds(scalars);
        Assert.Equal(0, allocated);
        Assert.Equal(held, owner.Budget.ReservedBytes);
    }

    // Keep assertion initialization and caller instrumentation outside the
    // allocation interval. The measured loop and exact zero limit stay intact.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureDuplicateSparseAdds(PythonReScalarSet scalars)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var repeat = 0; repeat < 10000; repeat++) scalars.Add(0x1f600);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void LargeNonconstantClassExpansionUsesBoundedSparseStorageAndPreservesPlaneRuns()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var parse = new PythonReParser("[\\u0000-\\U0010FFFE]").Parse(PythonReCompileOptions.IgnoreCase);
        var node = Assert.IsType<PythonReCharacterClassNode>(parse.Root);
        Assert.Collection(node.Items,
            item => Assert.Equal((0, 0xffff), (Assert.IsType<PythonReCharacterClassRangeItem>(item).StartScalar, ((PythonReCharacterClassRangeItem)item).EndScalar)),
            item => Assert.Equal((0x10000, 0x10fffe), (Assert.IsType<PythonReCharacterClassRangeItem>(item).StartScalar, ((PythonReCharacterClassRangeItem)item).EndScalar)));
        Assert.True(owner.Budget.PeakBytes < 1024 * 1024);
        Assert.True(owner.Budget.ReservedBytes < 8192);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [InlineData("(?P<x>😀)(?(x)[é-🙏]|a)", 1)]
    [InlineData("(?P<x>a)(?P<y>b)(?P=x)(?P=y)", 2)]
    [InlineData("(a)(b)(?(2)b|a)", 0)]
    public void ParserMetadataRemainsUsableAfterScratchOwnerCloses(string pattern, int names)
    {
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        Assert.Equal(names, parse.NamedGroups.Count);
        Assert.Equal(parse.CaptureGroupCount, parse.GroupBodies.Length);
        var translation = PythonReTranslator.Translate(parse);
        Assert.NotEmpty(translation.NativePattern);
        foreach (var entry in parse.NamedGroups) Assert.NotNull(parse.GroupBodies[entry.Value - 1]);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData("ab|cd")]
    [InlineData("(?P<x>a)(?(x)b|c)")]
    [InlineData("(?i:[a-z😀-🙏])")]
    public void ParserArrayAndMapGrowthDenialsRecoverWithoutDroppingParentNodes(string pattern)
    {
        ulong peak;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            _ = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
            peak = measured.Budget.PeakBytes;
        }
        foreach (var limit in new[] { 0ul, 512ul, peak / 2, peak - 1, peak })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, limit + 128, ulong.MaxValue, ulong.MaxValue);
            var parent = PythonReAstFactory.Literal('p');
            var checkpoint = owner.Budget.ReservedBytes;
            var failure = Record.Exception(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.True(owner.Budget.PeakBytes <= limit + 128);
            if (failure is not null)
            {
                Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(128ul, owner.Budget.ReservedBytes);
                Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                Assert.NotNull(parent);
            }
            else Assert.True(owner.Budget.ReservedBytes > 128);
            if (limit == peak) Assert.Null(failure);
        }
    }

    [Theory]
    [InlineData(0ul)] [InlineData(1ul)] [InlineData(7ul)] [InlineData(31ul)]
    public void SparsePageWorkDenialReleasesPagesWithoutSpendingCleanupWork(ulong extra)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, extra + 1, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        using (var child = Utf8ConstructionScope.Enter())
        {
            var failure = Record.Exception(() =>
            {
                using var scalars = PythonReScalarSet.Create(child.Budget);
                for (var value = 0; value < 1024; value++) scalars.Add(value);
            });
            var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
        }
        Assert.Equal(48ul, owner.Budget.ReservedBytes);

        Assert.True(owner.Budget.WorkSteps <= extra + 1);
        owner.Budget.ReleaseArray(parent, false);
    }
}
