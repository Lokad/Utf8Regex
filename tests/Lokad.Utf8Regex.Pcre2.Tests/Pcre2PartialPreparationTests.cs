using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2PartialPreparationTests
{
    [Theory]
    [InlineData(false, false)]     [InlineData(true, false)]
    public void PartialPreparationUsesRemainingParentBytesAndWork(bool character, bool work)
    {
        var request = Request(character ? "^" + new string('a', 4096) + @"\b" : "(a)b+");
        var tree = Pcre2BacktrackingCompiler.Compile(request)!.Root;
        using var parent = new Utf8ConstructionScope(ulong.MaxValue, work ? 1_000_000ul : 64ul, 256);
        var held = parent.Budget.AllocateArray<byte>(8, false);
        var before = parent.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            _ = Pcre2PartialProbeCompiler.Compile(tree, request);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(before, parent.Budget.ReservedBytes);
        Assert.Equal(0ul, parent.Budget.Reserved);
        Assert.Equal(0ul, parent.Budget.Depth);
        Assert.Equal(8, held.Length);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void OriginalInterruptionIsObservedInsideBothPreparationForms(bool character, bool timeout)
    {
        var request = Request(character ? "^" + new string('a', 4096) + @"\b" : "(a)b+");
        var tree = Pcre2BacktrackingCompiler.Compile(request)!.Root;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        using var parent = new Utf8ConstructionScope(ulong.MaxValue, 1_000_000, 256);
        parent.Budget.CancellationToken = cancellation.Token;
        parent.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10));
        var checkpoints = 0;
        parent.Budget.CheckpointHook = () =>
        {
            if (++checkpoints != 3) return;
            if (timeout) ticks = 20; else cancellation.Cancel();
        };
        var failure = Record.Exception(() =>
        {
            _ = Pcre2PartialProbeCompiler.Compile(tree, request);
        });
        Assert.Equal(3, checkpoints);
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(0ul, parent.Budget.ReservedBytes);
        Assert.Equal(0ul, parent.Budget.Depth);
    }

    [Fact]
    public void DecliningAnUnrelatedLargeTreeDoesNotSerializeIt()
    {
        var request = Request(string.Concat(Enumerable.Repeat("a?", 4096)) + "x");
        var tree = Pcre2BacktrackingCompiler.Compile(request)!.Root;
        using var parent = new Utf8ConstructionScope(ulong.MaxValue, 1, 1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = Pcre2PartialProbeCompiler.Compile(tree, request);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Pcre2PartialProbeProgramKind.None, result.Kind);
        Assert.Equal(0ul, parent.Budget.ReservedBytes);
        Assert.Equal(0ul, parent.Budget.Depth);
        Assert.True(allocated < 8192, $"Unexpected unrelated-tree staging: {allocated} B.");
    }

    [Fact]
    public void LiteralOperandIsOwnedAndAdmittedBeforePublication()
    {
        var request = Request("^" + new string('a', 4096) + @"\b");
        var tree = Pcre2BacktrackingCompiler.Compile(request)!.Root;
        using var parent = new Utf8ConstructionScope(ulong.MaxValue, 1_000_000, 256);
        var result = Pcre2PartialProbeCompiler.Compile(tree, request);
        Assert.Equal(Pcre2PartialProbeProgramKind.AnchoredExactLiteral, result.Kind);
        var operand = Assert.Single(result.Operands);
        Assert.Equal(new string('a', 4096), Encoding.UTF8.GetString(operand));
        Assert.True(parent.Budget.ReservedBytes >= Utf8ConstructionBudget.ArrayBytes<byte>(operand.Length));
        Assert.Equal(0ul, parent.Budget.Depth);
        parent.Dispose();
        var regex = new Utf8Pcre2Regex(request.Pattern);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe(operand, Pcre2PartialMode.Soft).Kind);
    }

    [Theory]
    [InlineData("(c)d+")] [InlineData("cat|pony")] [InlineData("xyz(?=uvw)")]
    public void NeighboringUnsupportedShapesKeepExplicitPartialDecline(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.Equal(Pcre2PartialProbeProgramKind.None, regex.DebugCompiledProgram.PartialProbe.Kind);
        Assert.Throws<NotSupportedException>(() => { _ = regex.Probe("xyz"u8, Pcre2PartialMode.Soft); });
    }

    private static Pcre2CompileRequest Request(string pattern) => new(pattern, default, default, default, Timeout.InfiniteTimeSpan);
}
