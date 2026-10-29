using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarPredicateBoundaryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AllBoundaryConsumersUseTheOwnedPredicateAndReleaseScratch(bool nonBoundary)
    {
        var input = "😀a!aa"u8.ToArray();
        var expected = nonBoundary ? new[] { 4, 7 } : new[] { 0, 5, 6, 8 };
        foreach (var nonBacktracking in new[] { false, true })
        foreach (var reverse in new[] { false, true })
        {
            if (nonBacktracking && reverse) continue;
            var root = new Utf8SliceNode.BoundaryNode(!nonBoundary)
                { WordPredicate = static value => value is 'a' or 0x1F600 };
            var program = Utf8SliceLowerer.Lower(root, null, false, false, reverse, null, false, false,
                Utf8ScalarCaseBehavior.Invariant, nonBacktracking);
            Assert.NotNull(program);
            Assert.Equal(2, program.Instructions.Length);
            Assert.Equal(1, program.Instructions[0].C);
            Assert.Single(program.CategorySetMembers);
            Assert.True(program.RequiresUnicodeWordBoundary);
            Assert.True(program.HasUnicodePatternAtoms);
            Assert.Empty(program.Assertions);
            var invocation = new Utf8SliceInvocation(new(100000, 1000, 1 << 20, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var start = reverse ? input.Length : 0;
            var found = Utf8SliceRunner.TryFindFirst(program, input, start, [], [], false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, found.Breach);
            Assert.True(found.Found);
            Assert.Equal(reverse ? expected[^1] : expected[0], found.StartByte);
            var counted = Utf8SliceRunner.CountFromBytes(program, input, start, [], [], ref invocation);
            Assert.Equal((expected.Length, Utf8ExecutionLimitKind.None), (counted.Count, counted.Breach));
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData((int)Utf8SliceWordKind.Unicode)]
    [InlineData((int)Utf8SliceWordKind.Ecma)]
    [InlineData((int)Utf8SliceWordKind.Ascii)]
    public void BuiltinEncodingsDoNotAcquirePredicateTables(int wordKind)
    {
        var kind = (Utf8SliceWordKind)wordKind;
        var program = Utf8SliceLowerer.Lower(new Utf8SliceNode.BoundaryNode(true, kind), null, false, false, false);
        Assert.NotNull(program);
        Assert.Equal((int)kind, program.Instructions[0].B);
        Assert.Equal(0, program.Instructions[0].C);
        Assert.Empty(program.CategorySetMembers);
    }
}
