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
