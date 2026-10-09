using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaptureProducerAdmissionTests
{
    public static IEnumerable<object[]> Limits()
    {
        foreach (var (pattern, options) in Patterns)
            foreach (var kind in new[] { Utf8ResourceLimitKind.ConstructionBytes,
                Utf8ResourceLimitKind.ConstructionWorkSteps, Utf8ResourceLimitKind.ConstructionDepth })
                yield return [pattern, options, kind];
    }

    private static readonly (string Pattern, RegexOptions Options)[] Patterns =
    [
        ("(?<x>😀)\\k<x>", RegexOptions.None),
        ("(?<9>😀)\\k<9>", RegexOptions.None),
        ("(?<x>😀)(?<x>b)\\k<x>", RegexOptions.None),
        ("(?<2147483647>a)\\k<2147483647>", RegexOptions.None),
        ("(?<01>😀)(a)", RegexOptions.None),
        ("(?<x>😀)(?<-x>b)", RegexOptions.None),
        ("(?<x>a)(?(x)b|c)", RegexOptions.None),
        ("(?(missing)a|b)", RegexOptions.None),
        ("(a)\\1", RegexOptions.ECMAScript),
        ("\\1(a)", RegexOptions.ECMAScript),
        ("(?<9>a)\\9", RegexOptions.ECMAScript),
        ("(?<x>😀)\\k<x>", RegexOptions.RightToLeft),
    ];

    [Theory]
    [MemberData(nameof(Limits))]
    public void ProducerCutoffsRestoreEveryParentLoanAndRetainFundedCatalogs(string pattern, RegexOptions options, Utf8ResourceLimitKind kind)
    {
        ulong bytes, work, depth, retained;
        int[] expectedNumbers;
        string[] expectedNames;
        using (var funded = Utf8ConstructionScope.Enter())
        {
            _ = funded.Budget.AllocateObject(17, static value => new Parent(value));
            var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
            Assert.NotNull(tree);
            Assert.NotNull(tree.CaptureCatalog);
            bytes = funded.Budget.PeakBytes - 112;
            work = funded.Budget.WorkSteps - 2;
            depth = funded.Budget.PeakDepth;
            retained = funded.Budget.ReservedBytes - 112;
            expectedNumbers = tree.CaptureCatalog.GroupNumbers;
            expectedNames = tree.CaptureCatalog.GroupNames;
            Assert.Equal(0ul, funded.Budget.Depth);
        }
        var maximum = kind == Utf8ResourceLimitKind.ConstructionBytes ? bytes
            : kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? work : depth;
        // Exhaust the smaller work/depth spaces and sample array-growth boundaries.
        var cutoffs = kind == Utf8ResourceLimitKind.ConstructionBytes
            ? new[] { 0ul, 1ul, maximum / 4, maximum / 2, maximum - 1, maximum, maximum + 1 }.Distinct()
            : Enumerable.Range(0, checked((int)maximum + 1)).Select(static value => (ulong)value);
        var rejected = 0;
        foreach (var cutoff in cutoffs)
        {
            using var owner = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity,
                kind == Utf8ResourceLimitKind.ConstructionBytes ? 112 + cutoff : Utf8ConstructionScope.DefaultBufferBytes,
                kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? 2 + cutoff : Utf8ConstructionScope.DefaultWorkSteps,
                kind == Utf8ResourceLimitKind.ConstructionDepth ? cutoff : Utf8ConstructionScope.DefaultDepth);
            var budget = owner.Budget;
            var parent = budget.AllocateObject(17, static value => new Parent(value));
            var checkpoint = budget.ObjectCheckpoint;
            Utf8OwnedRegexTree? tree = null;
            var failure = Record.Exception(() => tree = Utf8OwnedTreeParser.TryParse(pattern, options));
            Assert.Equal(0ul, budget.Depth);
            Assert.Equal(0ul, budget.Reserved);
            Assert.True(budget.WorkSteps <= budget.MaximumWorkSteps);
            Assert.True(budget.PeakBytes <= budget.MaximumBytes);
            if (failure is null)
            {
                Assert.NotNull(tree);
                Assert.Equal(expectedNumbers, tree.CaptureCatalog!.GroupNumbers);
                Assert.Equal(expectedNames, tree.CaptureCatalog.GroupNames);
                Assert.Equal(112 + retained, budget.ReservedBytes);
            }
            else
            {
                var limit = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(kind, limit.ResourceKind);
                Assert.True(limit.Requested > limit.Limit);
                Assert.Equal(112ul, budget.ReservedBytes);
                Assert.Same(checkpoint, budget.ObjectCheckpoint);
                Assert.Same(parent, checkpoint!.Value);
                rejected++;
            }
            if (cutoff >= maximum) Assert.Null(failure);
            if (kind == Utf8ResourceLimitKind.ConstructionDepth && cutoff == 0)
                Assert.Equal(2ul, budget.WorkSteps);
        }
        Assert.True(rejected > 0);
    }

    [Fact]
    public void DeclarationsAndFixupsAreReadonlyValuesInOwnedScratchArrays()
    {
        var parser = typeof(Utf8OwnedTreeParser);
        foreach (var (name, fields) in new[] { ("OwnedGroupDefinition", 5), ("OwnedBackrefFixup", 7) })
        {
            var type = parser.GetNestedType(name, BindingFlags.NonPublic)!;
            Assert.True(type.IsValueType);
            var actual = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.Equal(fields, actual.Length);
            Assert.All(actual, static field => Assert.True(field.IsInitOnly));
            Assert.DoesNotContain(actual, static field => field.Name == "Slot");
        }
        var state = parser.GetNestedType("ParserState", BindingFlags.NonPublic)!;
        foreach (var name in new[] { "GroupDefinitions", "BackrefFixups", "RequiredSlots" })
            Assert.Equal(typeof(Utf8ConstructionBuffer<>), state.GetField(name)!.FieldType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Utf8ConstructionMap<,>), state.GetField("CapturePositions")!.FieldType.GetGenericTypeDefinition());
    }

    [Fact]
    public void APatternWithoutCapturesOrReferencesAcquiresNoCaptureScratch()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var tree = Utf8OwnedTreeParser.TryParse("", RegexOptions.None);
        Assert.NotNull(tree);
        Assert.Same(tree, owner.Budget.ObjectCheckpoint!.Value);
        var roots = new List<object>();
        ulong loans = 0;
        for (var root = owner.Budget.ObjectCheckpoint; root is not null; root = root.Previous)
        {
            roots.Add(root.Value);
            loans += root.Bytes;
        }
        Assert.Single(roots.OfType<Utf8OwnedRegexTree>());
        Assert.Single(roots.OfType<Utf8CaptureCatalog>());
        Assert.Single(roots.Where(static value => value.GetType().IsGenericType &&
            value.GetType().GetGenericTypeDefinition() == typeof(Utf8ConstructionBuffer<>)));
        Assert.All(roots, static value => Assert.True(value is Utf8TreeNode or Utf8OwnedRegexTree or Utf8CaptureCatalog ||
            value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(Utf8ConstructionBuffer<>)));
        Assert.Equal(loans + 96, owner.Budget.ReservedBytes); // The two one-element catalog arrays.
    }

    [Theory]
    [InlineData("(?<x>a)\\k<missing>")]
    [InlineData("(?<01>a)")]
    public void UnboundFixupsAndAliasesReleaseTheirScratchWithoutRefundingWork(string pattern)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateObject(17, static value => new Parent(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None));
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Same(parent, checkpoint!.Value);
        Assert.True(owner.Budget.WorkSteps > 2);
        Assert.NotNull(Utf8OwnedTreeParser.TryParse("(?<x>😀)\\k<x>", RegexOptions.None));
    }

    private sealed class Parent(int value) { internal readonly int Value = value; }
}
