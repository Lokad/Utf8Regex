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
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EverySmallInstructionFactoryAdmitsItsMajorArrayAndSlots(int kind)
    {
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        var result = kind switch
        {
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
