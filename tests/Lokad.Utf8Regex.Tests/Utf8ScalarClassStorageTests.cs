using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarClassStorageTests
{
    [Theory]
    [InlineData(2, false)] [InlineData(2, true)]
    [InlineData(65, false)] [InlineData(65, true)]
    [InlineData(4098, false)] [InlineData(4098, true)]
    public void CompleteSubtractionStateLivesInItsAdmittedArray(int count, bool compiled)
    {
        var pattern = new StringBuilder();
        for (var index = 1; index < count; index++) pattern.Append(@"[\p{L}\d-");
        pattern.Append("[A]").Append(']', count - 1);
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var tree = Utf8OwnedTreeParser.TryParse(pattern.ToString(), options);
        Assert.NotNull(tree);
        using var owner = Utf8ConstructionScope.Enter();
        var program = Utf8OwnedTreeSliceLowerer.TryLower(tree, options);
        Assert.NotNull(program);
        var predicate = Assert.Single(program.CategorySetMembers[0]).Table.ClassProgram;
        Assert.NotNull(predicate);
        var field = typeof(Utf8SliceClassProgram).GetField("_segments", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var segments = Assert.IsAssignableFrom<Array>(field.GetValue(predicate));
        Assert.Equal(count, segments.Length);
        var element = segments.GetType().GetElementType();
        Assert.NotNull(element);
        Assert.True(element.IsValueType, "Subtraction segments must not introduce unadmitted heap objects.");
        var fields = element.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.Equal(5, fields.Length);
        Assert.Equal(2, fields.Count(item => item.FieldType == typeof(bool)));
        Assert.Single(fields, item => item.FieldType == typeof(byte[]));
        Assert.Single(fields, item => item.FieldType == typeof(Utf8SliceShorthand[]));
        Assert.Single(fields, item => item.FieldType == typeof(Utf8SliceNode.CategorySetMember[]));
        Assert.True(Utf8ScalarIrAdmissionTests.CountOwnedGraphArrays(program) + 128ul <= owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);

        foreach (var value in new[] { 'A', 'a', '1', 0x10428, 0x1f600 })
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
            var expected = value == 'A' ? (count & 1) == 1 : value != 0x1f600 && (count & 1) == 0;
            Assert.Equal(expected, predicate.TryMatch(new Rune(value), Utf8ScalarCaseBehavior.Invariant, ref invocation, out var breach));
            Assert.Equal(Utf8ExecutionLimitKind.None, breach);
            Assert.Equal((ulong)count, invocation.Budget.Steps);
            Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(2)] [InlineData(65)]
    public void ArrayDenialPrecedesSegmentAcquisitionAndPreservesBorrowedOperands(int count)
    {
        const ulong maximum = 16384;
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, maximum);
        using var operands = Utf8ConstructionBuffer<Utf8SliceNode>.Create(owner.Budget);
        var mask = new byte[16];
        mask['a' >> 3] = (byte)(1 << ('a' & 7));
        var operand = new Utf8SliceNode.SetNode(mask, false, [], false);
        for (var index = 0; index < count; index++) operands.Add(operand);
        using var arena = Utf8ConstructionArena.Create(owner.Budget);
        var before = owner.Budget.ReservedBytes;
        var checkpoint = owner.Budget.ReservedBytes;
        var extent = Utf8ConstructionBudget.ArrayBytes<(byte[], Utf8SliceShorthand[], Utf8SliceNode.CategorySetMember[], bool, bool)>(count);
        var padding = maximum - before - extent + 1;
        owner.Budget.ReserveBytes(padding);
        var held = owner.Budget.ReservedBytes;
        using (var child = Utf8ConstructionScope.Enter())
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SliceClassProgram.Subtract(operands, arena));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(maximum + 1, failure.Requested);
            Assert.Equal(held, owner.Budget.ReservedBytes);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        }
        Assert.Equal(held, owner.Budget.ReservedBytes);
        Assert.Equal(count, operands.Count);
        for (var index = 0; index < count; index++) Assert.Same(operand, operands[index]);
        owner.Budget.ReleaseBytes(padding);
        Assert.Equal(before, owner.Budget.ReservedBytes);
        var recovered = Assert.IsType<Utf8SliceNode.CategorySetNode>(Utf8SliceClassProgram.Subtract(operands, arena));
        var predicate = Assert.Single(recovered.Categories).Table.ClassProgram;
        Assert.NotNull(predicate);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal((count & 1) == 1, predicate.TryMatch(new Rune('a'), Utf8ScalarCaseBehavior.Invariant, ref invocation, out var breach));
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal((ulong)count, invocation.Budget.Steps);
    }
}
