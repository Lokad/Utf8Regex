using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarProgramMetadataTests
{
    [Theory]
    [InlineData("(?<=a).", "Utf8SliceAssertion")]
    [InlineData("\\p{L}", "Utf8SliceCategoryTable")]
    [InlineData("\\p{L}", "Utf8SliceNode+CategorySetMember")]
    [InlineData("[\\p{L}-[AE]]", "Utf8SliceClassProgram")]
    [InlineData("(?:ab[0-9]{2})+", "Utf8SliceProgram")]
    public void PublishedScalarMetadataObjectsBelongToTheConstructionOwner(string pattern, string type)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var regex = new Utf8Regex(pattern);
        Assert.True(regex.Pattern.Length > 0);
        var admitted = new List<string>();
        for (var allocation = owner.Budget.ObjectCheckpoint; allocation is not null; allocation = allocation.Previous)
            admitted.Add(allocation.Value.GetType().FullName ?? "");
        Assert.Contains("Lokad.Utf8Regex.Internal.Execution." + type, admitted);
    }

    [Fact]
    public void CategoryEndpointConversionIncludesObjectAndArrayLoans()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var table = Utf8SliceCategoryTable.Create(3, [0, 2, ushort.MaxValue]);
        Assert.Equal(new[] { 0, 2, 65535, 65536 }, table.Ranges);
        Assert.Equal(3u, table.Mask); Assert.Null(table.ClassProgram);
        Assert.Equal(Utf8SliceCategoryTable.ObjectBytes + Descriptor + Utf8ConstructionBudget.ArrayBytes<int>(4), owner.Budget.ReservedBytes);
        Assert.Same(table, Assert.Single(Objects(owner.Budget)));
    }

    [Theory]
    [InlineData("category")] [InlineData("member")] [InlineData("assertion")] [InlineData("program")]
    public void CompleteMetadataLoansHaveExactByteBoundariesAndRollback(string kind)
    {
        var body = Utf8SliceParser.TryParse("", System.Text.RegularExpressions.RegexOptions.None);
        Assert.NotNull(body);
        var category = Utf8SliceCategoryTable.ForScalarRanges([128, 256]);
        var loan = (kind switch
        {
            "category" => Utf8SliceCategoryTable.ObjectBytes,
            "member" => Utf8ConstructionBudget.ObjectBytes<(Utf8SliceCategoryTable, bool)>(),
            "assertion" => Utf8SliceAssertion.ObjectBytes,
            _ => Utf8SliceProgram.ObjectBytes,
        }) + Descriptor;
        foreach (var available in new[] { 0ul, loan - 1, loan })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 112 + available, ulong.MaxValue, ulong.MaxValue);
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Acquire(kind, body, category));
                if (available < loan) Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else { Assert.Null(failure); Assert.Equal(112 + loan, owner.Budget.ReservedBytes); }
            }
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
        }
    }

    [Theory]
    [InlineData("category")] [InlineData("member")] [InlineData("assertion")] [InlineData("program")]
    public void EveryMetadataWorkCutoffRestoresParentLoansAndKeepsSpentWork(string kind)
    {
        var body = Utf8SliceParser.TryParse("", System.Text.RegularExpressions.RegexOptions.None);
        Assert.NotNull(body);
        var category = Utf8SliceCategoryTable.ForScalarRanges([128, 256]);
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter()) { _ = Acquire(kind, body, category); work = measured.Budget.WorkSteps; }
        for (var available = 0ul; available <= work; available++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 2 + available, ulong.MaxValue);
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Acquire(kind, body, category));
                if (available < work) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, 2 + available);
        }
    }

    [Fact]
    public void ProgramStateCoversEveryPublishedFieldWithoutMutableInitializers()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        var fields = typeof(Utf8SliceProgram).GetFields(flags);
        var state = typeof(Utf8SliceProgram).GetNestedType("ConstructionState", System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(state); Assert.Equal(33, fields.Length); Assert.Equal(33, state.GetFields(flags).Length);
        Assert.Equal(fields.Select(static field => field.FieldType.ToString()).Order(), state.GetFields(flags).Select(static field => field.FieldType.ToString()).Order());
        Assert.All(fields, static field => Assert.True(field.IsInitOnly));
        Assert.All(typeof(Utf8SliceProgram).GetProperties(flags), static property => Assert.Null(property.SetMethod));
    }

    private static ulong Descriptor => Utf8ConstructionBudget.ObjectBytes<(object, Utf8ConstructionBudget.ObjectAllocation?, ulong)>();
    private static IEnumerable<object> Objects(Utf8ConstructionBudget budget)
    {
        for (var allocation = budget.ObjectCheckpoint; allocation is not null; allocation = allocation.Previous)
            yield return allocation.Value;
    }

    private static object Acquire(string kind, Utf8SliceProgram body, Utf8SliceCategoryTable category) => kind switch
    {
        "category" => Utf8SliceCategoryTable.ForScalarRanges(category.Ranges),
        "member" => Utf8SliceNode.CategorySetMember.Create(category, true),
        "assertion" => Utf8SliceAssertion.Create(body, 0, 0, false),
        _ => Utf8SliceProgram.Create(body.Instructions, body.SetMasks, body.CategoryTables, body.CategorySetMembers,
            body.RepeatCount, body.GroupCount, body.RequiresUnicodeWordBoundary, body.RequiresUnicodeShorthand,
            body.RequiresUnicodeCategory, body.Assertions, body.HasNonFixedLookbehind, body.GroupNames, body.HasMixedNamedGroups,
            body.HasBalancing, body.IgnoreCase, body.HasContiguousAnchor, body.HasConditionalTest, body.RightToLeft,
            body.SawLookaround, body.SawCapturingLookbehind, body.CanMatchEmpty, body.CaptureCatalog,
            body.UsesInterpretedLazyLoopSemantics, body.UsesLiteralClassBracketSyntax, body.CaseBehavior,
            body.IsNonBacktracking, body.NonBacktrackingCaptureOpeningCount, body.NonBacktrackingEpsilonOrder, body.BackreferenceLowerings),
    };
}
