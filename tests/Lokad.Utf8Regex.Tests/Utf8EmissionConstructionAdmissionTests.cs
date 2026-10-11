using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8EmissionConstructionAdmissionTests
{
    private static Delegate CompileCount()
    {
        var method = typeof(Utf8EmittedSearchGuidedFallback).GetMethod("CompileCount", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException("The production emitted Count compiler is missing.");
        try { return Assert.IsType<Utf8EmittedDelegate<Utf8EmittedSearchGuidedFallback.CountDelegate>>(method.Invoke(null, null)).Callback; }
        catch (TargetInvocationException error) when (error.InnerException is Exception cause)
        {
            ExceptionDispatchInfo.Capture(cause).Throw();
            throw;
        }
    }

    [Fact]
    public void EmittedCodeReachesTheEnclosingConstructionOwner()
    {
        using var parent = Utf8ConstructionScope.Enter();
        var result = CompileCount();
        Assert.Equal(typeof(int), result.Method.ReturnType);
        Assert.True(parent.Budget.ReservedBytes > 0);
        Assert.True(parent.Budget.Reserved > 0);
        Assert.True(parent.Budget.WorkSteps > 0);
        Assert.True(parent.Budget.PeakDepth > 0);
        Assert.Equal(0UL, parent.Budget.Depth);
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ConstructionBytes)]
    [InlineData(Utf8ResourceLimitKind.ConstructionWorkSteps)]
    [InlineData(Utf8ResourceLimitKind.ConstructionDepth)]
    [InlineData(Utf8ResourceLimitKind.ConstructionInstructionCapacity)]
    public void SharedEmissionFailuresPropagateWithoutPublishingOrLeaking(Utf8ResourceLimitKind kind)
    {
        using var parent = new Utf8ConstructionScope(
            kind == Utf8ResourceLimitKind.ConstructionInstructionCapacity ? 0 : Utf8ConstructionScope.DefaultInstructionCapacity,
            kind == Utf8ResourceLimitKind.ConstructionBytes ? 1 : Utf8ConstructionScope.DefaultBufferBytes,
            kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? 0 : Utf8ConstructionScope.DefaultWorkSteps,
            kind == Utf8ResourceLimitKind.ConstructionDepth ? 0 : Utf8ConstructionScope.DefaultDepth);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => CompileCount());
        Assert.Equal(kind, error.ResourceKind);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.Null(parent.Budget.ObjectCheckpoint);
    }

    [Fact]
    public void EmissionInheritsTheParentCancellationAuthority()
    {
        using var parent = Utf8ConstructionScope.Enter();
        using var source = new CancellationTokenSource();
        parent.Budget.CancellationToken = source.Token;
        source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => CompileCount());
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.Null(parent.Budget.ObjectCheckpoint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProductionCountUnwindsAtEveryWorkAndDepthCutoff(bool depth)
    {
        ulong required;
        using (var funded = Utf8ConstructionScope.Enter())
        {
            _ = CompileCount();
            required = depth ? funded.Budget.PeakDepth : funded.Budget.WorkSteps;
        }
        for (var cutoff = 0UL; cutoff < required; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue,
                depth ? ulong.MaxValue : cutoff + 8, depth ? cutoff : ulong.MaxValue);
            var parent = owner.Budget.AllocateArray<byte>(8, true);
            parent[0] = 37;
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => CompileCount());
            Assert.Equal(depth ? Utf8ResourceLimitKind.ConstructionDepth : Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
            Assert.Equal(48UL, owner.Budget.ReservedBytes);
            Assert.Equal(8UL, owner.Budget.Reserved);
            Assert.Equal(0UL, owner.Budget.Depth);
            Assert.Null(owner.Budget.ObjectCheckpoint);
            Assert.Equal(37, parent[0]);
        }
        using var recovered = Utf8ConstructionScope.Enter();
        Assert.Equal(typeof(int), CompileCount().Method.ReturnType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryEmissionCheckpointRetainsParentCancellationAndDeadline(bool timeout)
    {
        var count = 0;
        using (var funded = Utf8ConstructionScope.Enter())
        {
            funded.Budget.CheckpointHook = _ => count++;
            _ = CompileCount();
        }
        // Count hook invocations, including the zero-work checkpoint after the
        // opaque CLR call. Work totals alone would miss that final interruption.
        for (var target = 1; target <= count; target++)
        {
            using var owner = Utf8ConstructionScope.Enter();
            var parent = owner.Budget.AllocateArray<byte>(8, true);
            parent[0] = 37;
            using var cancellation = new CancellationTokenSource();
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
            owner.Budget.Deadline = deadline;
            owner.Budget.CancellationToken = cancellation.Token;
            var checkpoint = 0;
            owner.Budget.CheckpointHook = _ =>
            {
                if (++checkpoint != target) return;
                if (timeout) ticks = 30000;
                else cancellation.Cancel();
            };
            var error = Record.Exception(() => _ = CompileCount());
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
            Assert.Same(deadline, owner.Budget.Deadline);
            Assert.Equal(48UL, owner.Budget.ReservedBytes);
            Assert.Equal(8UL, owner.Budget.Reserved);
            Assert.Equal(0UL, owner.Budget.Depth);
            Assert.Null(owner.Budget.ObjectCheckpoint);
            Assert.Equal(37, parent[0]);
        }
    }

    [Fact]
    public void BuilderTransferStateCoversItsCompleteFieldExtent()
    {
        var type = typeof(Utf8DynamicIlBuilder);
        var state = type.GetNestedType("ConstructionState", BindingFlags.NonPublic)!;
        Assert.Equal(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(f => f.FieldType.FullName).Order(),
            state.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(f => f.FieldType.FullName).Order());
    }

    private delegate int RefInt(ref int value);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProductionCountPreservesParentLoansAcrossCapacityCutoffs(bool instructions)
    {
        ulong required, bytes, slots;
        using (var funded = Utf8ConstructionScope.Enter())
        {
            _ = funded.Budget.AllocateArray<byte>(8, true);
            _ = CompileCount();
            required = instructions ? funded.Budget.Peak - 8 : funded.Budget.PeakBytes - 48;
            bytes = funded.Budget.ReservedBytes; slots = funded.Budget.Reserved;
        }
        var successes = 0;
        // Every slot, and every distinct rounded byte extent. Capacity growth
        // may shrink under a tight owner; success need not have one threshold.
        for (var cutoff = 0UL; cutoff <= required; cutoff += instructions ? 1UL : 16UL)
        {
            using var owner = new Utf8ConstructionScope(instructions ? cutoff + 8 : ulong.MaxValue,
                instructions ? ulong.MaxValue : cutoff + 48, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateArray<byte>(8, true);
            parent[0] = 37;
            var error = Record.Exception(() => _ = CompileCount());
            if (error is null)
            {
                successes++;
                Assert.Equal(bytes, owner.Budget.ReservedBytes);
                Assert.Equal(slots, owner.Budget.Reserved);
            }
            else
            {
                Assert.Equal(instructions ? Utf8ResourceLimitKind.ConstructionInstructionCapacity : Utf8ResourceLimitKind.ConstructionBytes,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(48UL, owner.Budget.ReservedBytes);
                Assert.Equal(8UL, owner.Budget.Reserved);
                Assert.Null(owner.Budget.ObjectCheckpoint);
            }
            Assert.Equal(0UL, owner.Budget.Depth);
            Assert.Equal(37, parent[0]);
        }
        Assert.True(successes > 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TypedLocalsAndClosedMethodTokensExecuteAfterTheBuilderCloses(int shape)
    {
        Delegate result;
        using (var il = Utf8DynamicIlBuilder.Create("OwnedTypedLocal", typeof(int), typeof(Utf8EmissionConstructionAdmissionTests),
            shape == 2 ? typeof(int).MakeByRefType() : typeof(int)))
        {
            if (shape == 0)
            {
                var type = typeof(KeyValuePair<int, int>);
                var local = il.DeclareLocal(type);
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4, 3);
                il.Emit(OpCodes.Newobj, type.GetConstructor([typeof(int), typeof(int)])!);
                il.Emit(OpCodes.Stloc, local); il.Emit(OpCodes.Ldloca_S, local);
                il.Emit(OpCodes.Call, type.GetProperty(nameof(KeyValuePair<int, int>.Key))!.GetMethod!);
            }
            else if (shape == 1)
            {
                var local = il.DeclareLocal(typeof(StringBuilder));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, typeof(StringBuilder).GetConstructor([typeof(int)])!);
                il.Emit(OpCodes.Stloc, local); il.Emit(OpCodes.Ldloc, local); il.Emit(OpCodes.Ldc_I4, 12);
                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetMethod(nameof(StringBuilder.Append), [typeof(int)])!);
                il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ldloc, local);
                il.Emit(OpCodes.Callvirt, typeof(StringBuilder).GetProperty(nameof(StringBuilder.Length))!.GetMethod!);
            }
            else
            {
                var local = il.DeclareLocal(typeof(int).MakeByRefType());
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Stloc, local);
                il.Emit(OpCodes.Ldloc, local); il.Emit(OpCodes.Ldc_I4, 37); il.Emit(OpCodes.Stind_I4);
                il.Emit(OpCodes.Ldloc, local); il.Emit(OpCodes.Ldind_I4);
            }
            il.Emit(OpCodes.Ret);
            result = shape == 2 ? il.CreateDelegate<RefInt>().Callback : il.CreateDelegate<Func<int, int>>().Callback;
        }
        if (shape == 2)
        {
            var value = 9; Assert.Equal(37, ((RefInt)result)(ref value)); Assert.Equal(37, value);
        }
        else Assert.Equal(shape == 0 ? 9 : 2, ((Func<int, int>)result)(9));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InvalidControlFlowFailsBeforePublicationAndRestoresTheOwner(int shape)
    {
        using var parent = Utf8ConstructionScope.Enter();
        Assert.Throws<InvalidProgramException>(() =>
        {
            using var il = Utf8DynamicIlBuilder.Create("InvalidOwnedFlow", typeof(int), typeof(Utf8EmissionConstructionAdmissionTests), typeof(int));
            if (shape == 0) il.Emit(OpCodes.Br, il.DefineLabel());
            else if (shape == 1) il.Emit(OpCodes.Pop);
            else
            {
                var join = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Brtrue, join);
                il.Emit(OpCodes.Ldc_I4, 1); il.MarkLabel(join);
            }
            il.Emit(OpCodes.Ldc_I4, 0); il.Emit(OpCodes.Ret);
            _ = il.CreateDelegate<Func<int, int>>();
        });
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(0UL, parent.Budget.Reserved);
        Assert.Equal(0UL, parent.Budget.Depth);
        Assert.Null(parent.Budget.ObjectCheckpoint);
        Assert.True(parent.Budget.WorkSteps > 0);
    }
}
