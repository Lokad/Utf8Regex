using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReParserFrameAdmissionTests
{
    [Fact]
    public void ProductionParserDescriptorLoanCoversWarmedAcquisitionAndItsTrackingObject()
    {
        using (var warm = Utf8ConstructionScope.Enter())
            for (var index = 0; index < 1000; index++) _ = PythonReParser.Create("a");
        using var owner = Utf8ConstructionScope.Enter();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var parser = PythonReParser.Create("a");
        var allocated = (ulong)(GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.InRange(allocated, 1ul, owner.Budget.ReservedBytes);
        Assert.Same(parser, owner.Budget.ObjectCheckpoint?.Value);
        Assert.Equal(2ul, owner.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ParserDescriptorDenialAcquiresNoObjectAndPreservesParentLoans(bool workDenial)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue,
            workDenial ? ulong.MaxValue : 48, workDenial ? 2ul : ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => PythonReParser.Create("(?P<x>😀)"));
        Assert.Equal(workDenial ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Null(owner.Budget.ObjectCheckpoint);
        Assert.Equal(1ul, owner.Budget.WorkSteps);
        owner.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData("((a))")]
    [InlineData("(?P<x>😀)(?(x)(?:a|b)|(?s:.))")]
    [InlineData("(?x:(?:a # comment\n|(?i:b)))")]
    public void FrameGrowthDenialRestoresParentObjectsAndSuccessfulBoundaryPreservesTree(string pattern)
    {
        ulong peak;
        PythonReParseResult expected;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            expected = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
            peak = measured.Budget.PeakBytes;
        }
        foreach (var allowance in new[] { 0ul, 128ul, peak / 2, peak - 1, peak })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, allowance + 112, ulong.MaxValue, ulong.MaxValue);
            var parent = PythonReAstFactory.Literal('p');
            var checkpoint = owner.Budget.ObjectCheckpoint;
            PythonReParseResult actual = default;
            var failure = Record.Exception(() => actual = new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.True(owner.Budget.PeakBytes <= allowance + 112);
            if (failure is not null)
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                Assert.Equal(112ul, owner.Budget.ReservedBytes);
                Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                Assert.Same(parent, checkpoint?.Value);
            }
            else
            {
                Assert.Equal(expected.Root.ToString(), actual.Root.ToString());
                Assert.Equal(expected.CaptureGroupCount, actual.CaptureGroupCount);
            }
            if (allowance == peak) Assert.Null(failure);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RootFrameDenialOccursBeforeConsumingPatternAndParserCanRetry(bool depthDenial)
    {
        var parser = new PythonReParser("(?P<x>😀)"); // Caller-owned descriptor.
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue,
            depthDenial ? ulong.MaxValue : 48, ulong.MaxValue, depthDenial ? 0ul : ulong.MaxValue))
        {
            var parent = owner.Budget.AllocateArray<byte>(1, false);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => parser.Parse(PythonReCompileOptions.None));
            Assert.Equal(depthDenial ? Utf8ResourceLimitKind.ConstructionDepth : Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(48ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Null(owner.Budget.ObjectCheckpoint);
            Assert.Equal(1ul, owner.Budget.WorkSteps);
            owner.Budget.ReleaseArray(parent, false);
        }
        var result = parser.Parse(PythonReCompileOptions.None);
        Assert.Equal(1, result.CaptureGroupCount);
        Assert.Equal(1, result.NamedGroups["x"]);
        Assert.Equal("😀", Assert.IsType<PythonReRawNode>(Assert.IsType<PythonReGroupNode>(result.Root).Inner).Text);
    }

    [Fact]
    public void PublicSupplementaryDotKeepsScalarExecutionAfterParserDescriptorAndFramesAreAdmitted()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var regex = new Utf8PythonRegex("(?P<x>.)");
        var match = regex.SearchDetailed("😀"u8);
        Assert.True(match.Success);
        Assert.Equal("😀", match.GetValueString());
        Assert.Equal(4, match.Value.GetValueBytes().Length);
        Assert.True(regex.DebugUsesOwnedSlice);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.NotNull(owner.Budget.ObjectCheckpoint);
    }
}
