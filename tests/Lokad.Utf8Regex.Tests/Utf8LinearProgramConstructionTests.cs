using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LinearProgramConstructionTests
{
    private static ulong Bytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);
    private static Utf8StructuralLinearInstructionProgram Borrow(Utf8StructuralLinearInstruction[] code)
        => new(code, default, 0, [], [], false, false, false, false, false, false);
    private static AsciiSimplePatternPlan BorrowPlan(AsciiSimplePatternToken[] tokens, byte[][] literals)
        => new([tokens], 0, literals, [], false, false, false, false, true,
            default, default, default, default, default, default, default, default, false, false);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EverySmallInstructionFactoryAdmitsItsMajorArrayAndSlots(int kind)
    {
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        var result = kind switch
        {
            0 => Utf8StructuralLinearInstructionProgram.Create(default(AsciiSimplePatternRunPlan)),
            1 => Utf8StructuralLinearInstructionProgram.CreateLiteralFamilyRun(default),
            2 => Utf8StructuralLinearInstructionProgram.Create(default(AsciiStructuralRepeatedSegmentPlan)),
            3 => Utf8StructuralLinearInstructionProgram.Create(default(AsciiStructuralTokenWindowPlan)),
            4 => Utf8StructuralLinearInstructionProgram.Create(default(AsciiStructuralQuotedRelationPlan)),
            _ => Utf8StructuralLinearInstructionProgram.Create(default(AsciiOrderedLiteralWindowPlan))
        };
        Assert.Equal(2, result.Instructions.Length); Assert.Equal(Utf8StructuralLinearInstructionKind.Accept, result.Instructions[1].Kind);
        Assert.Equal(7 + Bytes<Utf8StructuralLinearInstruction>(2), owner.Budget.ReservedBytes);
        Assert.Equal(2UL, owner.Budget.Reserved); Assert.Equal(4UL, owner.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void DeterministicConversionHasExactByteInstructionAndWorkBoundaries(int dimension)
    {
        var code = Borrow([Utf8StructuralLinearInstruction.CreateLiteral((byte)'a'), Utf8StructuralLinearInstruction.CreateAnyByte(), Utf8StructuralLinearInstruction.CreateAccept()]);
        var extent = Bytes<Utf8AsciiDeterministicStep>(3) + Bytes<Utf8AsciiDeterministicFixedWidthCheck>(2);
        ulong steps;
        using (var probe = Utf8ConstructionScope.Enter())
        { var program = Utf8AsciiDeterministicProgram.Create(code); Assert.Equal(2, program.FixedWidthLength); steps = probe.Budget.WorkSteps; }
        foreach (var allow in new[] { false, true })
        {
            using var owner = new Utf8ConstructionScope(dimension == 1 ? (allow ? 3UL : 2UL) : ulong.MaxValue,
                dimension == 0 ? 7 + extent - (allow ? 0UL : 1UL) : ulong.MaxValue,
                dimension == 2 ? steps - (allow ? 0UL : 1UL) : ulong.MaxValue, ulong.MaxValue);
            owner.Budget.ReserveBytes(7);
            Utf8AsciiDeterministicProgram result = default;
            var error = Record.Exception(() => result = Utf8AsciiDeterministicProgram.Create(code));
            if (allow)
            {
                Assert.Null(error); Assert.True(result.HasValue); Assert.Equal(7 + extent, owner.Budget.ReservedBytes); Assert.Equal(3UL, owner.Budget.Reserved);
                Assert.Equal(Utf8AsciiDeterministicStepKind.Literal, result.Steps[0].Kind);
                Assert.Equal(Utf8AsciiDeterministicFixedWidthCheckKind.AnyByte, result.FixedWidthChecks[1].Kind);
            }
            else
            {
                var failure = Assert.IsType<Utf8ResourceLimitExceededException>(error);
                Assert.Equal(dimension switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionInstructionCapacity, _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
                Assert.False(result.HasValue); Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(0UL, owner.Budget.Reserved);
            }
        }
    }

    [Fact]
    public void LargeBorrowedCodeIsDeniedBeforeAllocatingItsStepArray()
    {
        var input = Borrow(new Utf8StructuralLinearInstruction[100000]);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 7 + Bytes<Utf8AsciiDeterministicStep>(100000) - 1, ulong.MaxValue, ulong.MaxValue);
        owner.Budget.ReserveBytes(7);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Record.Exception(() => Utf8AsciiDeterministicProgram.Create(input));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
        Assert.True(allocated < 32768, $"Step array acquired before admission: {allocated} bytes.");
        Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(7UL, owner.Budget.PeakBytes); Assert.Equal(0UL, owner.Budget.WorkSteps);
    }

    [Fact]
    public void UnsupportedConversionRestoresCapacityAndSpendsWork()
    {
        var code = Borrow([Utf8StructuralLinearInstruction.CreateTokenWindow(), Utf8StructuralLinearInstruction.CreateAccept()]);
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        Assert.False(Utf8AsciiDeterministicProgram.Create(code).HasValue);
        Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(0UL, owner.Budget.Reserved); Assert.True(owner.Budget.WorkSteps > 0);
        var recovery = Utf8AsciiDeterministicProgram.Create(Borrow([Utf8StructuralLinearInstruction.CreateLiteral((byte)'a'), Utf8StructuralLinearInstruction.CreateAccept()]));
        Assert.True(recovery.HasValue); Assert.Equal(1, recovery.FixedWidthChecks.Length);
    }

    [Theory]
    [InlineData("[a-z]+")] [InlineData("[a-z]{2,4}")] [InlineData("ab[0-9]")]
    public void TopLevelCreationUsesOneInstructionArrayForBothViews(string pattern)
    {
        // Unbounded runs bypass simple-plan expansion in the front end; exercise
        // that factory shape with its explicit class run. Also cover actual lowering.
        AsciiSimplePatternPlan simplePlan;
        if (pattern == "[a-z]+")
        {
            var run = new AsciiSimplePatternRunPlan(AsciiCharClass.FromBytes("abcdefghijklmnopqrstuvwxyz"u8), 1, int.MaxValue);
            simplePlan = new([], 0, [], [], false, false, false, false, true,
                run, default, default, default, default, default, default, default, false, false);
        }
        else
        {
            var analysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant);
            Assert.True(Utf8AsciiSimplePatternLowerer.TryCreatePlan(analysis.SemanticRegex, RegexOptions.CultureInvariant,
                out simplePlan, out _));
        }
        Assert.Equal(pattern.StartsWith("[a-z]", StringComparison.Ordinal), simplePlan.RunPlan.HasValue);
        using var owner = Utf8ConstructionScope.Enter();
        var result = Utf8StructuralLinearProgram.Create(NativeExecutionKind.AsciiSimplePattern, simplePlan,
            default, default, default, default, default, default, default, default);
        Assert.True(result.HasValue); Assert.True(result.DeterministicProgram.HasValue);
        var instructions = result.InstructionProgram.Instructions;
        var deterministic = result.DeterministicProgram;
        Assert.Equal((ulong)(instructions.Length + deterministic.Steps.Length), owner.Budget.Reserved);
        var expected = Bytes<Utf8StructuralLinearInstruction>(instructions.Length) + Bytes<Utf8AsciiDeterministicStep>(deterministic.Steps.Length)
            + Bytes<Utf8AsciiDeterministicFixedWidthCheck>(deterministic.FixedWidthChecks.Length);
        Assert.Equal(expected, owner.Budget.ReservedBytes);
        Assert.Same(result.InstructionProgram.FixedLiteralChecks, deterministic.FixedLiteralChecks);
        Assert.Same(result.InstructionProgram.SearchLiterals, deterministic.SearchLiterals);
        if (simplePlan.RunPlan.HasValue) Assert.Empty(deterministic.FixedWidthChecks);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LargeBorrowedBranchIsDeniedBeforeAcquiringInstructions(bool instructionLimit)
    {
        var plan = BorrowPlan(new AsciiSimplePatternToken[100000], []);
        using var owner = new Utf8ConstructionScope(instructionLimit ? 100000UL : ulong.MaxValue,
            instructionLimit ? ulong.MaxValue : 7 + Bytes<Utf8StructuralLinearInstruction>(100001) - 1,
            ulong.MaxValue, ulong.MaxValue);
        owner.Budget.ReserveBytes(7);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Record.Exception(() => Utf8StructuralLinearInstructionProgram.Create(plan));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(instructionLimit ? Utf8ResourceLimitKind.ConstructionInstructionCapacity : Utf8ResourceLimitKind.ConstructionBytes,
            Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
        Assert.True(allocated < 32768, $"Instruction array acquired before admission: {allocated} bytes.");
        Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(7UL, owner.Budget.PeakBytes);
        Assert.Equal(0UL, owner.Budget.Reserved); Assert.Equal(0UL, owner.Budget.WorkSteps);
    }

    [Fact]
    public void LiteralFamilySearchFailureRestoresItsCompletedInstructionPair()
    {
        var plan = BorrowPlan([], ["ab"u8.ToArray(), "cd"u8.ToArray()]);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 7 + Bytes<Utf8StructuralLinearInstruction>(2), ulong.MaxValue, ulong.MaxValue);
        owner.Budget.ReserveBytes(7);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8StructuralLinearInstructionProgram.CreateLiteralFamilyRun(plan));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(0UL, owner.Budget.Reserved);
        Assert.True(owner.Budget.WorkSteps >= 4);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryConversionCheckpointInheritsInterruptionAndRestoresParent(bool timeout)
    {
        var code = Borrow([Utf8StructuralLinearInstruction.CreateLiteral((byte)'a'), Utf8StructuralLinearInstruction.CreateAnyByte(), Utf8StructuralLinearInstruction.CreateAccept()]);
        var count = 0;
        using (var probe = Utf8ConstructionScope.Enter())
        {
            probe.Budget.CheckpointHook = _ => count++;
            Assert.True(Utf8AsciiDeterministicProgram.Create(code).HasValue);
        }
        Assert.True(count > 3);
        for (var target = 1; target <= count; target++)
        {
            using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
            using var token = new CancellationTokenSource();
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50));
            owner.Budget.Deadline = deadline; owner.Budget.CancellationToken = token.Token;
            var current = 0;
            owner.Budget.CheckpointHook = _ => { if (++current == target) { if (timeout) ticks = 50; else token.Cancel(); } };
            var failure = Record.Exception(() => Utf8AsciiDeterministicProgram.Create(code));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Same(deadline, owner.Budget.Deadline);
            Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(0UL, owner.Budget.Reserved);
            owner.Budget.CheckpointHook = null; owner.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
            owner.Budget.CancellationToken = default;
            Assert.True(Utf8AsciiDeterministicProgram.Create(code).HasValue);
        }
    }

    [Theory]
    [InlineData(9)] [InlineData(10)] [InlineData(99)] [InlineData(100)] [InlineData(999)] [InlineData(1000)]
    public void GeneratedScalarGroupNamesHaveAdmittedTextAtDecimalBoundaries(int slot)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var program = Utf8SliceLowerer.Lower(new Utf8SliceNode.CaptureNode(slot, new Utf8SliceNode.LiteralNode(0x1f600, false)), null, false, false, false)!;
        Assert.Equal(slot + 2, program.GroupNames.Length);
        ulong text = 0;
        for (var number = 0; number < program.GroupNames.Length; number++)
        {
            Assert.Equal(number.ToString(), program.GroupNames[number]);
            text += 32UL + ((2 * ((ulong)program.GroupNames[number].Length + 1) + 15) & ~15UL);
        }
        Assert.True(owner.Budget.ReservedBytes >= text + Bytes<string>(program.GroupNames.Length));
        var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.TryMatchAt(program, "😀"u8, 0, starts, ends, true, ref invocation).IsMatch);
        Assert.Equal(0, starts[slot]); Assert.Equal(4, ends[slot]); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }
}
