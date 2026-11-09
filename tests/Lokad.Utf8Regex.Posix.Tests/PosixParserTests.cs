using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixParserTests
{
    [Theory]
    [InlineData("(a|aa)(b)?", 2)]
    [InlineData("[é-ê]+", 0)]
    [InlineData("^(.{2,3})$", 1)]
    public void LoweredProgramKeepsTheAdmittedScalarShape(string pattern, int expectedGroups)
    {
        var root = PosixParser.Parse(pattern, default, out var groups);
        var program = Utf8SliceLowerer.Lower(root, null, false, false, false);
        Assert.Equal(expectedGroups, groups);
        Assert.NotNull(program);
        Assert.Equal(groups == 0 ? 0 : groups + 1, program.GroupCount);
        Assert.False(program.RightToLeft);
        Assert.False(program.IgnoreCase);
        Assert.Empty(program.Assertions);
    }

    [Theory]
    [InlineData("[a-b-c]")]
    [InlineData("[a-[:digit:]]")]
    [InlineData("[[:digit:]-z]")]
    [InlineData("a{,2}")]
    [InlineData("a{1,256}")]
    [InlineData("a**")]
    [InlineData("$+")]
    [InlineData("a}")]
    [InlineData("[😀-é]")]
    public void InvalidRangesAndRepetitionsFail(string pattern)
        => Assert.Throws<ArgumentException>(() => new Utf8PosixRegex(pattern));

    [Fact]
    public void MalformedPatternScalarsAndUnknownOptionsFail()
    {
        foreach (var input in new[] { "\ud800", "[\udc00]", "abc\ud800" })
            Assert.Throws<ArgumentException>(() => new Utf8PosixRegex(input));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PosixRegex("a", (PosixCompileOptions)2, Timeout.InfiniteTimeSpan, default));
    }

    [Fact]
    public void NestedGroupsUseAdmittedFramesRatherThanRecursiveParsing()
    {
        const int depth = 4096;
        var root = PosixParser.Parse(new string('(', depth) + "😀" + new string(')', depth), default, out var groups);
        Assert.Equal(depth, groups);
        for (var slot = 1; slot <= depth; slot++)
        {
            var group = Assert.IsType<Utf8SliceNode.CaptureNode>(root);
            Assert.Equal(slot, group.Slot); root = group.Body;
        }
        Assert.Equal(0x1f600, Assert.IsType<Utf8SliceNode.LiteralNode>(root).Value);
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ConstructionDepth)]
    [InlineData(Utf8ResourceLimitKind.ConstructionBytes)]
    [InlineData(Utf8ResourceLimitKind.ConstructionWorkSteps)]
    public void FailureRestoresParentCapacityAndDepthWithoutRefundingWork(Utf8ResourceLimitKind kind)
    {
        using var scope = new Utf8ConstructionScope(1024,
            kind == Utf8ResourceLimitKind.ConstructionBytes ? 128UL : 1UL << 20,
            kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? 3UL : 1UL << 20,
            kind == Utf8ResourceLimitKind.ConstructionDepth ? 2UL : 100);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => PosixParser.Parse("(((abcdef)))", default, out _));
        Assert.Equal(kind, error.ResourceKind);
        Assert.Equal(0UL, scope.Budget.Depth);
        Assert.Equal(0UL, scope.Budget.ReservedBytes);
        Assert.Equal(0UL, scope.Budget.Reserved);
        if (kind != Utf8ResourceLimitKind.ConstructionBytes) Assert.True(scope.Budget.WorkSteps > 0);
    }

    [Fact]
    public void CancellationDuringAdmissionKeepsTheOriginalTokenAndRestoresTheParent()
    {
        using var canceled = new CancellationTokenSource();
        using var scope = Utf8ConstructionScope.Enter(default, Timeout.InfiniteTimeSpan, canceled.Token);
        scope.Budget.CheckpointHook = steps => { if (steps >= 32) canceled.Cancel(); };
        var error = Assert.Throws<OperationCanceledException>(() => PosixParser.Parse(new string('a', 2048), default, out _));
        Assert.Equal(canceled.Token, error.CancellationToken);
        Assert.Equal(0UL, scope.Budget.Depth);
        Assert.Equal(0UL, scope.Budget.ReservedBytes);
    }
}
