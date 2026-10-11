using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TreeObjectAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    public static IEnumerable<object[]> NodeLimits()
    {
        for (var route = 0; route < 6; route++)
            foreach (var kind in new[] { Utf8ResourceLimitKind.ConstructionBytes, Utf8ResourceLimitKind.ConstructionWorkSteps })
                yield return [route, kind];
    }

    [Theory]
    [MemberData(nameof(NodeLimits))]
    public void EveryNodeFactoryAdmitsBeforeAcquisitionAndPreservesBorrowedPayloads(int route, Utf8ResourceLimitKind kind)
    {
        var borrowed = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'x');
        var text = "😀";
        Utf8TreeNode Create() => route switch
        {
            0 => Utf8TreeNode.Create(Utf8TreeNodeKind.Empty, RegexOptions.IgnoreCase),
            1 => Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.IgnoreCase, 'é'),
            2 => Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.IgnoreCase, text),
            3 => Utf8TreeNode.Create(Utf8TreeNodeKind.Loop, RegexOptions.IgnoreCase, 2, 7),
            4 => Utf8TreeNode.Create(Utf8TreeNodeKind.Capture, RegexOptions.IgnoreCase, 9, 0),
            _ => Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.None, 'é', text, 2, 7, borrowed, borrowed, true),
        };
        var loan = CompleteLoan(typeof(Utf8TreeNode), 9);
        var maximum = kind == Utf8ResourceLimitKind.ConstructionBytes ? loan : 2;
        foreach (var cutoff in new[] { 0ul, 1ul, maximum - 1, maximum, maximum + 1 }.Distinct())
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue,
                kind == Utf8ResourceLimitKind.ConstructionBytes ? 128 + cutoff : ulong.MaxValue,
                kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? 2 + cutoff : ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Utf8TreeNode? node = null;
            var failure = Record.Exception(() => node = Create());
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(0ul, owner.Budget.Reserved);
            if (cutoff < maximum)
            {
                var limit = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(kind, limit.ResourceKind);
                Assert.True(limit.Requested > limit.Limit);
                Assert.Equal(128ul, owner.Budget.PeakBytes);
                Assert.Equal(128ul, owner.Budget.ReservedBytes);
                Assert.Equal(2ul, owner.Budget.WorkSteps);
                Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                Assert.NotNull(parent);
            }
            else
            {
                Assert.Null(failure);
                Assert.NotNull(node);
                Assert.Equal(128 + loan, owner.Budget.ReservedBytes);
                Assert.Equal(4ul, owner.Budget.WorkSteps);
                Assert.True(node.MatchCaseInsensitive);
                if (route is 2 or 5) Assert.Same(text, node.Str);
                if (route == 5)
                {
                    Assert.Same(borrowed, node.Children);
                    Assert.Same(borrowed, node.Parent);
                    Assert.Equal('é', node.Ch);
                    Assert.Equal(2, node.M);
                    Assert.Equal(7, node.N);
                }
            }
        }
    }

    [Theory]
    [InlineData(false, Utf8ResourceLimitKind.ConstructionBytes)]
    [InlineData(true, Utf8ResourceLimitKind.ConstructionBytes)]
    [InlineData(false, Utf8ResourceLimitKind.ConstructionWorkSteps)]
    [InlineData(true, Utf8ResourceLimitKind.ConstructionWorkSteps)]
    public void TreeAdmissionSharesTheParentAndBorrowsItsCanonicalCatalog(bool full, Utf8ResourceLimitKind kind)
    {
        var seed = Utf8OwnedTreeParser.TryParse("(?<9>😀)(?<x>b)", RegexOptions.CultureInvariant)!;
        var loan = CompleteLoan(typeof(Utf8OwnedRegexTree), 10);
        var maximum = kind == Utf8ResourceLimitKind.ConstructionBytes ? loan : 2;
        foreach (var cutoff in new[] { 0ul, 1ul, maximum - 1, maximum, maximum + 1 }.Distinct())
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue,
                kind == Utf8ResourceLimitKind.ConstructionBytes ? 128 + cutoff : ulong.MaxValue,
                kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? 2 + cutoff : ulong.MaxValue, ulong.MaxValue);
            _ = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Utf8OwnedRegexTree? result = null;
            var failure = Record.Exception(() => result = full
                ? Utf8OwnedRegexTree.Create(seed.Root, seed.CaptureCount, seed.CaptureNames, seed.CaptureNameToNumberMapping,
                    seed.CaptureNumberSparseMapping, seed.Options, seed.Culture, seed.CaptureCatalog, true, true)
                : Utf8OwnedRegexTree.Create(seed.Root, seed.CaptureCount, seed.CaptureNames, seed.CaptureNameToNumberMapping,
                    seed.CaptureNumberSparseMapping, seed.Options, seed.Culture, null, false, false));
            Assert.Equal(0ul, owner.Budget.Depth);
            if (cutoff < maximum)
            {
                Assert.Equal(kind, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                Assert.Equal(128ul, owner.Budget.ReservedBytes);
                Assert.Equal(2ul, owner.Budget.WorkSteps);
            }
            else
            {
                Assert.Null(failure);
                Assert.NotNull(result);
                Assert.Equal(128 + loan, owner.Budget.ReservedBytes);
                Assert.Equal(4ul, owner.Budget.WorkSteps);
                Assert.Same(seed.Root, result.Root);
                Assert.Same(seed.CaptureNames, result.CaptureNames);
                Assert.Same(seed.CaptureNameToNumberMapping, result.CaptureNameToNumberMapping);
                Assert.Same(seed.CaptureNumberSparseMapping, result.CaptureNumberSparseMapping);
                Assert.Same(CultureInfo.InvariantCulture, result.Culture);
                Assert.Equal(full, result.UsesLiteralClassBracketSyntax);
                Assert.Equal(full, result.UsesIgnoreCase);
                if (full) Assert.Same(seed.CaptureCatalog, result.CaptureCatalog);
                else Assert.Null(result.CaptureCatalog);
            }
        }
    }

    [Theory]
    [InlineData("", 3, Utf8ResourceLimitKind.ConstructionBytes)]
    [InlineData("a|😀", 4, Utf8ResourceLimitKind.ConstructionBytes)]
    [InlineData("(?<x>a)\\k<x>", 4, Utf8ResourceLimitKind.ConstructionWorkSteps)]
    public void ParserBoundaryFailureRestoresCapacityAndKeepsCreationWorkSpent(string pattern, int minimumNodes, Utf8ResourceLimitKind kind)
    {
        ulong bytes, work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None)!;
            Assert.NotNull(tree.Root);
            Assert.NotNull(tree.Root.Child(0));
            Assert.True(measured.Budget.ReservedBytes >= (ulong)minimumNodes * 128ul);
            bytes = measured.Budget.ReservedBytes;
            work = measured.Budget.WorkSteps;
        }
        using var denied = new Utf8ConstructionScope(ulong.MaxValue,
            kind == Utf8ResourceLimitKind.ConstructionBytes ? bytes - 1 : ulong.MaxValue,
            kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? work - 1 : ulong.MaxValue, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None));
        Assert.Equal(kind, failure.ResourceKind);
        Assert.True(failure.Requested > failure.Limit);
        if (kind == Utf8ResourceLimitKind.ConstructionWorkSteps) Assert.Equal(work, failure.Requested);
        Assert.Equal(0ul, denied.Budget.ReservedBytes);
        Assert.Equal(0ul, denied.Budget.Depth);

        if (kind == Utf8ResourceLimitKind.ConstructionWorkSteps) Assert.Equal(work - 2, denied.Budget.WorkSteps);
        else Assert.InRange(denied.Budget.WorkSteps, 0ul, work - 2);
    }

    private static ulong CompleteLoan(Type type, int fieldCount)
    {
        var constructor = Assert.Single(type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.True(constructor.IsPrivate);
        Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        var state = Assert.Single(constructor.GetParameters()).ParameterType;
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var stateFields = state.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(fieldCount, fields.Length);
        Assert.Equal(fields.Select(static field => field.FieldType), stateFields.Select(static field => field.FieldType));
        Assert.Null(type.GetProperty("Str2"));
        Assert.Null(type.GetProperty("ChildList"));
        var method = typeof(Utf8ConstructionBudget).GetMethod("ObjectBytes", BindingFlags.Static | BindingFlags.NonPublic)!;
        return Math.Max(128ul, (ulong)method.MakeGenericMethod(state).Invoke(null, null)!);
    }
}
