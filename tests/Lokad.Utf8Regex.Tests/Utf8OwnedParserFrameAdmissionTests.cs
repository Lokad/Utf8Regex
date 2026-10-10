using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedParserFrameAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    [Fact]
    public void RawScalarPipelineDeclineRestoresTheCompletedParserLoan()
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 65536, ulong.MaxValue, 128);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Null(Utf8SliceParser.TryParse("(a)\\1", RegexOptions.NonBacktracking));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.True(owner.Budget.WorkSteps > 2);
        Assert.Equal(17, parent.Value);
        using (var recovery = Utf8ConstructionScope.Enter())
            Assert.NotNull(Utf8SliceParser.TryParse("a", RegexOptions.None));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void RootDenialPrecedesParserStateAndFrameStorage(int denial)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue,
            denial == 0 ? 112ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue,
            denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8OwnedTreeParser.TryParse("(?<x>😀)\\k<x>", RegexOptions.CultureInvariant));
        Assert.Equal(denial switch
        {
            0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps,
        }, failure.ResourceKind);
        Assert.Equal(112ul, owner.Budget.PeakBytes);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData("(", ")")]
    [InlineData("(?<x>", ")")]
    [InlineData("(?i:", ")")]
    [InlineData("(?>", ")")]
    [InlineData("(?<=", ")")]
    [InlineData("(?(?=a)", "|x)")]
    public void ChildFrameByteDenialRestoresParentAndTheSameOwnerRecovers(string open, string close)
    {
        var pattern = string.Concat(Enumerable.Repeat(open, 32)) + "a" + string.Concat(Enumerable.Repeat(close, 32));
        ulong recoveryPeak;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            Assert.NotNull(Utf8OwnedTreeParser.TryParse("a", RegexOptions.CultureInvariant));
            recoveryPeak = measured.Budget.PeakBytes;
        }
        // Lexical storage is now admitted. Use the measured complete small
        // parser footprint, while dedicated lexical controls retain 1024-byte denials.
        var cap = Math.Max(1024ul, recoveryPeak);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, cap, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(cap, failure.Limit);
        Assert.True(failure.Requested > failure.Limit);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, cap);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var spent = owner.Budget.WorkSteps;
        Assert.True(spent > 2);
        using (var recovery = Utf8ConstructionScope.Enter())
        {
            Assert.NotNull(Utf8OwnedTreeParser.TryParse("a", RegexOptions.CultureInvariant));
            Assert.Equal(0ul, owner.Budget.Depth);
        }
        Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData("(")] [InlineData("(?<x>a")]
    [InlineData("(?(?=a)b|c")] [InlineData("(?<=a")]
    [InlineData("(?i:a")] [InlineData("(a)(?(1)b|c|d)")]
    public void PrivateGrammarDeclineRestoresParentAndDoesNotRefundWork(string pattern)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 65536, ulong.MaxValue, 128);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var spent = owner.Budget.WorkSteps;
        Assert.True(spent > 2);
        using (var recovery = Utf8ConstructionScope.Enter())
        {
            var tree = Utf8OwnedTreeParser.TryParse("(?<x>😀)\\k<x>", RegexOptions.CultureInvariant);
            Assert.NotNull(tree);
            Assert.Equal(new[] { "0", "x" }, tree.CaptureNames);
        }
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(17, parent.Value);
    }
}
