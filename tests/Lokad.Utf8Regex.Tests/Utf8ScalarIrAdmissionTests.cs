using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarIrAdmissionTests
{
    [Fact]
    public void SharedTreeClassIsConvertedOnceAndRetainsOnePayloadLoan()
    {
        var tree = Utf8OwnedTreeParser.TryParse("[abc][def]", RegexOptions.None);
        Assert.NotNull(tree);
        var concat = tree.Root.Child(0);
        Assert.Equal(Utf8TreeNodeKind.Concatenate, concat.Kind);
        Assert.IsType<Utf8TreeNode[]>(concat.Children)[1] = concat.Child(0);
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity);
        var program = Utf8OwnedTreeSliceLowerer.TryLower(tree, RegexOptions.None);
        Assert.NotNull(program);
        Assert.Equal(2, program.SetMasks.Length);
        Assert.Same(program.SetMasks[0], program.SetMasks[1]);
        Assert.Equal(CountOwnedGraphArrays(program) + CountOwnedConstructionDescriptors(scope.Budget), scope.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void WideLiteralIrIsDeniedBeforeItsArrayAndNodeExpansion(bool rtl, bool supplementary)
    {
        var pattern = supplementary ? string.Concat(Enumerable.Repeat("😀", 16384)) : new string('a', 32768);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0) | (compiled ? RegexOptions.Compiled : 0);
            var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
            var recoveryTree = Utf8OwnedTreeParser.TryParse("", options);
            Assert.NotNull(tree);
            Assert.NotNull(recoveryTree);
            using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, 4096);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedTreeSliceLowerer.TryLower(tree, options));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(4096ul, failure.Limit);
            var extent = Utf8ConstructionBudget.ArrayBytes<Utf8SliceNode>(supplementary ? 16384 : 32768);
            Assert.InRange(failure.Requested, extent, extent + 4096);
            Assert.True(scope.Budget.PeakBytes <= 4096);
            Assert.Equal(0ul, scope.Budget.Peak); // No VM instruction emission occurred.
            Assert.Equal(0ul, scope.Budget.ReservedBytes);
            Assert.Equal(0ul, scope.Budget.Reserved);
            Assert.NotNull(Utf8OwnedTreeSliceLowerer.TryLower(recoveryTree, options));
        }
    }

    [Theory]
    [InlineData("\u0000\u0002\u0000abx")]
    [InlineData("\u0000\u0002\u0000ab\u0000\u0000\u0001\u7fff")]
    public void DeclinedClassConversionReleasesPayloadsAndKeepsParentLoan(string wire)
    {
        var tree = Utf8OwnedTreeParser.TryParse("a", RegexOptions.None);
        Assert.NotNull(tree);
        Assert.NotNull(tree.CaptureCatalog);
        using var parent = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity);
        var held = parent.Budget.AllocateArray<byte>(16, false);
        var before = parent.Budget.ReservedBytes;
        using (var child = Utf8ConstructionScope.Enter())
        using (var arena = new Utf8ConstructionArena(child.Budget))
        {
            var balancing = false;
            var lazy = false;
            // Keep the two historical row identities, now using malformed
            // canonical data: an incomplete segment or an invalid predicate.
            using var data = Utf8ConstructionBuffer<int>.Create(child.Budget);
            data.AddRange(wire.EndsWith('x') ? [0, 2, 0, 'a', 'b', 'x'] : [0, 2, 0, 'a', 'b', 0, 0, 1, 0x7fff]);
            var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Set, RegexOptions.None, Utf8ScalarClass.Create(data));
            Assert.Null(Utf8OwnedTreeSliceLowerer.TryConvertRoot(node, tree.CaptureCatalog, arena, ref balancing, ref lazy));
            Assert.True(parent.Budget.ReservedBytes > before);
        }
        Assert.Equal(before, parent.Budget.ReservedBytes);
        Assert.Equal(0ul, parent.Budget.Reserved);
        Assert.NotNull(Utf8SliceParser.TryParse("[a-z-[aeiou]]", RegexOptions.None));
        parent.Budget.ReleaseArray(held, false);
    }

    [Theory]
    [InlineData(0ul)] [InlineData(48ul)]
    public void RejectedPreparationFrameAcquiresNoStorage(ulong maximumBytes)
    {
        var root = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, maximumBytes);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedFindOptimizations.ComputeByteWidth(root));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.True(scope.Budget.PeakBytes <= maximumBytes);
        Assert.Equal(0ul, scope.Budget.Depth);
    }

    internal static ulong CountOwnedConstructionDescriptors(Utf8ConstructionBudget budget)
    {
        var bytes = 0ul;
        for (var allocation = budget.ObjectCheckpoint; allocation is not null; allocation = allocation.Previous)
        {
            var acquired = allocation.Value.GetType();
            var type = acquired.IsGenericType ? acquired.GetGenericTypeDefinition() : acquired;
            Assert.Contains(type, new[] { typeof(Utf8ConstructionBuffer<>), typeof(Utf8ConstructionMap<,>),
                typeof(Utf8SliceProgram), typeof(Utf8SliceAssertion), typeof(Utf8SliceCategoryTable),
                typeof(Utf8SliceNode.CategorySetMember), typeof(Utf8SliceClassProgram) });
            bytes += allocation.Bytes;
        }
        return bytes;
    }

    // Inspect all reachable arrays, including private subtraction segments,
    // to detect omitted retained payloads and IR loans that should have released.
    // The capture catalog's preexisting name/number arrays are borrowed here.
    internal static ulong CountOwnedGraphArrays(Utf8SliceProgram program)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (program.CaptureCatalog is { } catalog)
        {
            seen.Add(catalog.GroupNames);
            seen.Add(catalog.GroupNumbers);
        }
        var pending = new Stack<object>();
        pending.Push(program);
        ulong bytes = 0;
        while (pending.TryPop(out var current))
        {
            if (!seen.Add(current)) continue;
            if (current is Array array)
            {
                bytes += array switch
                {
                    Utf8SliceInstruction[] values => Utf8ConstructionBudget.ArrayBytes<Utf8SliceInstruction>(values.Length),
                    byte[] values => Utf8ConstructionBudget.ArrayBytes<byte>(values.Length),
                    int[] values => Utf8ConstructionBudget.ArrayBytes<int>(values.Length),
                    Utf8SliceShorthand[] values => Utf8ConstructionBudget.ArrayBytes<Utf8SliceShorthand>(values.Length),
                    _ when !array.GetType().GetElementType().IsValueType => Utf8ConstructionBudget.ArrayBytes<object>(array.Length),
                    _ => CountInlineArray(array),
                };
                var element = array.GetType().GetElementType();
                // Inline scalar class segments also retain arrays and category
                // holders. Inspect those fields without charging the test's boxes.
                if (!element.IsValueType || !element.IsPrimitive && !element.IsEnum && element != typeof(Utf8SliceInstruction))
                    foreach (var value in array) if (value is not null) pending.Push(value);
            }
            else if (current.GetType().Namespace == typeof(Utf8SliceProgram).Namespace)
            {
                foreach (var field in current.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (field.GetValue(current) is { } value && (value is Array || value.GetType().Namespace == typeof(Utf8SliceProgram).Namespace))
                        pending.Push(value);
            }
        }
        return bytes;
    }

    private static ulong CountInlineArray(Array array)
    {
        var method = typeof(Utf8ConstructionBudget).GetMethod(nameof(Utf8ConstructionBudget.ArrayBytes),
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<ulong>(method.MakeGenericMethod(array.GetType().GetElementType()).Invoke(null, [array.Length]));
    }

}
