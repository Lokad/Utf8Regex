using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BacktrackingConstructionBoundsTests
{
    private static string NestedPattern(int depth) => "(?J)" + new string('(', depth) + "😀+" + new string(')', depth) + @"\1";

    private static Utf8Pcre2Regex Compile(string pattern, Utf8ConstructionPolicy policy, bool bytes)
        => bytes
            ? Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(pattern), default, default,
                Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, policy, Timeout.InfiniteTimeSpan, default)
            : Utf8Pcre2Regex.Compile(pattern, default, default,
                Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, policy, Timeout.InfiniteTimeSpan, default);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ProfileNativeRecursionAndInstructionsCannotBypassPublicCaps(bool bytes)
    {
        var pattern = NestedPattern(64);
        var depth = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Compile(pattern, new() { MaxDepth = 8 }, bytes));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, depth.ResourceKind);
        Assert.Equal(8ul, depth.Limit);
        var instructions = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Compile(pattern, new() { MaxInstructionCapacity = 1 }, bytes));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity, instructions.ResourceKind);
        Assert.Equal(1ul, instructions.Limit);
        Assert.False(Utf8ConstructionScope.IsActive);
        var regex = Compile(pattern, Utf8ConstructionPolicy.Default, bytes);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.Equal("😀😀", regex.Match("😀😀"u8).GetValueString());
        Assert.Equal(1, regex.Count("😀😀"u8));
    }

    [Fact]
    public void RecursiveCompilerHasAFixedBackstopBeforeClrStackExhaustion()
    {
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Compile(NestedPattern(300), Utf8ConstructionPolicy.Default, false));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(256ul, failure.Limit);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void ConditionalRecursionCannotBypassTheAtomDepthGuard()
    {
        var pattern = "(?J)(a)" + string.Concat(Enumerable.Repeat("(?(1)", 64)) + "b" + new string(')', 64);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Compile(pattern, new() { MaxDepth = 8 }, false));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal("ab", Compile(pattern, Utf8ConstructionPolicy.Default, false).Match("ab"u8).GetValueString());
    }

    [Fact]
    public void ShallowSubroutineDependencyChainsHaveDepthAdmissionDuringAnalysis()
    {
        var definitions = string.Concat(Enumerable.Range(0, 24).Select(index =>
            $"(?<n{index}>" + (index == 23 ? "a" : $"(?{index + 2})") + ")"));
        var pattern = "(?J)(?(DEFINE)" + definitions + ")(?1)";
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Compile(pattern, new() { MaxDepth = 8 }, false));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.True(Compile(pattern, Utf8ConstructionPolicy.Default, false).IsMatch("a"u8));
    }

    [Fact]
    public void MajorMetadataAdmissionPrecedesParsingAndFailureRestoresParent()
    {
        using var owner = new Utf8ConstructionScope(1024, 64, ulong.MaxValue, 256);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Pcre2BacktrackingCompiler.Compile(new(NestedPattern(64), default, default, default, Timeout.InfiniteTimeSpan)));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);
        GC.KeepAlive(parent);
    }

    [Fact]
    public void LoweringObservesTheOriginalCancellationTokenAndRollsBack()
    {
        using var cancellation = new CancellationTokenSource();
        using var owner = new Utf8ConstructionScope(1024);
        owner.Budget.CancellationToken = cancellation.Token;
        owner.Budget.CheckpointHook = _ =>
        {
            if (owner.Budget.Reserved > 0) cancellation.Cancel();
        };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Pcre2BacktrackingCompiler.Compile(new(NestedPattern(8), default, default, default, Timeout.InfiniteTimeSpan)));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
    }
}
