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
    public void PublishedScalarProgramFitsNumericAdmissionAndKeepsMetadataSemantics(string pattern, string type)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var regex = new Utf8Regex(pattern);
        Assert.True(regex.Pattern.Length > 0);
        var prepared = (Lokad.Utf8Regex.Internal.Planning.Utf8PreparedRegex)typeof(Utf8Regex)
            .GetProperty("PreparedRegex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(regex)!;
        var program = Assert.IsType<Utf8SliceProgram>(prepared.ScalarProgram);
        Assert.True(owner.Budget.ReservedBytes >= 128ul);
        switch (type)
        {
            case "Utf8SliceAssertion": Assert.NotEmpty(program.Assertions); break;
            case "Utf8SliceCategoryTable": Assert.True(regex.IsMatch("é"u8)); Assert.False(regex.IsMatch("0"u8)); break;
            case "Utf8SliceNode+CategorySetMember": Assert.True(regex.IsMatch("é"u8)); Assert.False(regex.IsMatch("0"u8)); break;
            case "Utf8SliceClassProgram": Assert.True(regex.IsMatch("é"u8)); Assert.False(regex.IsMatch("A"u8)); break;
            default: Assert.NotEmpty(program.Instructions); break;
        }

    }

    [Fact]
    public void CategoryEndpointConversionIncludesObjectAndArrayLoans()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var table = Utf8SliceCategoryTable.Create(3, [0, 2, ushort.MaxValue]);
        Assert.Equal(new[] { 0, 2, 65535, 65536 }, table.Ranges);
        Assert.Equal(3u, table.Mask); Assert.Null(table.ClassProgram);
        Assert.Equal(Math.Max(128ul, Utf8SliceCategoryTable.ObjectBytes) + Utf8ConstructionBudget.ArrayBytes<int>(4), owner.Budget.ReservedBytes);
        Assert.NotNull(table);
    }

    [Theory]
    [InlineData("category")] [InlineData("predicate")] [InlineData("member")] [InlineData("assertion")] [InlineData("program")]
    public void CompleteMetadataLoansHaveExactByteBoundariesAndRollback(string kind)
    {
        var body = Utf8SliceParser.TryParse("", System.Text.RegularExpressions.RegexOptions.None);
        Assert.NotNull(body);
        var category = Utf8SliceCategoryTable.ForScalarRanges([128, 256]);
        var loan = Math.Max(128ul, kind switch
        {
            "category" or "predicate" => Utf8SliceCategoryTable.ObjectBytes,
            "member" => Utf8ConstructionBudget.ObjectBytes<(Utf8SliceCategoryTable, bool)>(),
            "assertion" => Utf8SliceAssertion.ObjectBytes,
            _ => Utf8SliceProgram.ObjectBytes,
        });
        foreach (var available in new[] { 0ul, loan - 1, loan })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 128 + available, ulong.MaxValue, ulong.MaxValue);
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Acquire(kind, body, category));
                if (available < loan) Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else { Assert.Null(failure); Assert.Equal(128 + loan, owner.Budget.ReservedBytes); }
            }
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
        }
    }

    [Theory]
    [InlineData("category")] [InlineData("predicate")] [InlineData("member")] [InlineData("assertion")] [InlineData("program")]
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
            var checkpoint = owner.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Acquire(kind, body, category));
                if (available < work) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, 2 + available);
        }
    }

    [Fact]
    public void PublishedProgramFieldsRemainImmutable()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        var fields = typeof(Utf8SliceProgram).GetFields(flags);
        Assert.NotEmpty(fields);
        Assert.All(fields, static field => Assert.True(field.IsInitOnly));
        Assert.All(typeof(Utf8SliceProgram).GetProperties(flags), static property => Assert.Null(property.SetMethod));
    }

    private static object Acquire(string kind, Utf8SliceProgram body, Utf8SliceCategoryTable category) => kind switch
    {
        "category" => Utf8SliceCategoryTable.ForScalarRanges(category.Ranges),
        "predicate" => Utf8SliceCategoryTable.ForScalarPredicate(static scalar => scalar == 0x1F600),
        "member" => Utf8SliceNode.CategorySetMember.Create(category, true),
        "assertion" => Utf8SliceAssertion.Create(body, 0, 0, false),
        _ => Utf8SliceProgram.Create(body.Instructions, body.SetMasks, body.CategorySetMembers,
            body.RepeatCount, body.GroupCount, body.RequiresUnicodeWordBoundary, body.RequiresUnicodeShorthand,
            body.RequiresUnicodeCategory, body.Assertions, body.HasNonFixedLookbehind, body.GroupNames, body.HasMixedNamedGroups,
            body.HasBalancing, body.IgnoreCase, body.HasContiguousAnchor, body.HasConditionalTest, body.RightToLeft,
            body.SawLookaround, body.SawCapturingLookbehind, body.CanMatchEmpty, body.CaptureCatalog,
            body.UsesInterpretedLazyLoopSemantics, body.UsesLiteralClassBracketSyntax, body.CaseBehavior,
            body.IsNonBacktracking, body.NonBacktrackingCaptureOpeningCount, body.NonBacktrackingEpsilonOrder, body.BackreferenceLowerings),
    };
}
