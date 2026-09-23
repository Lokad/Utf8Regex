using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

/// <summary>R02a invocation bounds: finite step/depth/storage budgets breach
/// deterministically with cleanup, pre-canceled and mid-run tokens abort with
/// cleanup, None-token parity holds, concurrent invocations share programs
/// safely, and accounting units stay pinned.</summary>
public sealed class Utf8SliceBoundsTests
{
    private static Utf8SliceProgram PlusProgram()
    {
        var program = Utf8SliceLowerer.Lower(new Utf8SliceNode.ConcatNode([
            new Utf8SliceNode.CaptureNode(0, new Utf8SliceNode.RepeatNode(new Utf8SliceNode.LiteralNode(97), 1, int.MaxValue, false)),
            new Utf8SliceNode.CaptureNode(1, new Utf8SliceNode.RepeatNode(new Utf8SliceNode.LiteralNode(98), 1, int.MaxValue, false)),
        ]), null, false);
        Assert.NotNull(program);
        return program;
    }

    private static (int[] Starts, int[] Ends) Slots(Utf8SliceProgram program)
    {
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        return (starts, ends);
    }

    private static Utf8SliceInvocation Invoke(ulong work, uint depth, ulong bytes, CancellationToken token)
    {
        return new Utf8SliceInvocation(new Utf8ExecutionLimits(work, depth, bytes), Regex.InfiniteMatchTimeout, token);
    }
    [Fact]
    public void StepBudgetBreachesDeterministically()
    {
        var program = PlusProgram();
        var input = Encoding.ASCII.GetBytes("aaabbb");
        var invocation = Invoke(10, 0, 0, CancellationToken.None);
        var (starts, ends) = Slots(program);
        var outcome = Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
        Assert.False(outcome.IsMatch);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void DepthBudgetBreachesDeterministically()
    {
        var program = PlusProgram();
        var input = Encoding.ASCII.GetBytes("aaabbb");
        var invocation = Invoke(0, 1, 0, CancellationToken.None);
        var (starts, ends) = Slots(program);
        var outcome = Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.Depth, outcome.Breach);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void StorageDenialBreachesWithCleanup()
    {
        var program = PlusProgram();
        var input = Encoding.ASCII.GetBytes("aaabbb");
        var invocation = Invoke(0, 0, 1, CancellationToken.None);
        var (starts, ends) = Slots(program);
        var outcome = Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, outcome.Breach);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void PreCanceledTokenAbortsWithCleanup()
    {
        var program = PlusProgram();
        var input = Encoding.ASCII.GetBytes("aaabbb");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var invocation = Invoke(0, 0, 0, canceled.Token);
        var (starts, ends) = Slots(program);
        var exception = Assert.Throws<OperationCanceledException>(
            () => Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation));
        Assert.Equal(canceled.Token, exception.CancellationToken);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void MidRunCancellationAbortsWithCleanup()
    {
        var program = Lower(new Utf8SliceNode.RepeatNode(new Utf8SliceNode.LiteralNode(97), 1, int.MaxValue, false));
        var input = Encoding.ASCII.GetBytes(new string((char)97, 5000));
        using var source = new CancellationTokenSource();
        var invocation = Invoke(0, 0, 0, source.Token);
        invocation.CheckpointHook = _ => source.Cancel();
        var (starts, ends) = Slots(program);
        var exception = Assert.Throws<OperationCanceledException>(
            () => Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation));
        Assert.Equal(source.Token, exception.CancellationToken);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);

        static Utf8SliceProgram Lower(Utf8SliceNode root)
        {
            var program = Utf8SliceLowerer.Lower(root, null, false);
            Assert.NotNull(program);
            return program;
        }
    }

    [Fact]
    public void NoneTokenMatchesLiveUncanceledToken()
    {
        var program = PlusProgram();
        var input = Encoding.ASCII.GetBytes("xxaaabbyy");
        using var source = new CancellationTokenSource();
        var silent = Invoke(0, 0, 0, CancellationToken.None);
        var live = Invoke(0, 0, 0, source.Token);
        var (starts0, ends0) = Slots(program);
        var (starts1, ends1) = Slots(program);
        var quiet = Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts0, ends0, ref silent);
        var loud = Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts1, ends1, ref live);
        Assert.True(quiet.IsMatch);
        Assert.True(loud.IsMatch);
        Assert.Equal(starts0, starts1);
        Assert.Equal(ends0, ends1);
        Assert.Equal(0UL, silent.Ledger.ReservedBytes);
        Assert.Equal(0UL, live.Ledger.ReservedBytes);
    }

    [Fact]
    public void ConcurrentInvocationsShareTheProgram()
    {
        var program = PlusProgram();
        var inputs = new[] { "aaab", "aabbb", "aaabbb", "xyz", "aabb", "bbaa", "abab", "aaabbb" };
        var results = new bool[inputs.Length];
        System.Threading.Tasks.Parallel.For(0, inputs.Length, index =>
        {
            var input = Encoding.ASCII.GetBytes(inputs[index]);
            var invocation = Invoke(0, 0, 0, CancellationToken.None);
            var (starts, ends) = Slots(program);
            results[index] = Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation).IsMatch;
        });
        var oracle = new Regex("(a+)(b+)", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        for (var index = 0; index < inputs.Length; index++)
        {
            Assert.Equal(oracle.IsMatch(inputs[index]), results[index]);
        }
    }

    [Fact]
    public void AccountingUnitsStayPinned()
    {
        Assert.Equal(64 * System.Runtime.CompilerServices.Unsafe.SizeOf<Utf8SliceFrame>(), Utf8SliceRunner.FrameReserveBytes);
        Assert.Equal(128 * System.Runtime.CompilerServices.Unsafe.SizeOf<Utf8SliceTrailEntry>(), Utf8SliceRunner.TrailReserveBytes);
        Assert.Equal(128 * System.Runtime.CompilerServices.Unsafe.SizeOf<Utf8SliceCountMutation>(), Utf8SliceRunner.JournalReserveBytes);
        Assert.Equal(28, System.Runtime.CompilerServices.Unsafe.SizeOf<Utf8SliceFrame>());
        Assert.Equal(16, System.Runtime.CompilerServices.Unsafe.SizeOf<Utf8SliceTrailEntry>());
        Assert.Equal(12, System.Runtime.CompilerServices.Unsafe.SizeOf<Utf8SliceCountMutation>());
    }

    [Fact]
    public void SuccessfulRunLeavesNoReservation()
    {
        var program = PlusProgram();
        var input = Encoding.ASCII.GetBytes("aaabbb");
        var invocation = Invoke(1000000, 100000, 1000000, CancellationToken.None);
        var (starts, ends) = Slots(program);
        Assert.True(Utf8SliceRunner.IsMatch(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation).IsMatch);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        (starts, ends) = Slots(program);
        Assert.Equal(1, Utf8SliceRunner.Count(program, input, Utf8BoundaryMap.Create(input), starts, ends, ref invocation).Count);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }
}
