using System.Reflection;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaptureCatalogAdmissionTests
{
    public static TheoryData<int> Cases => new(Enumerable.Range(0, 8));

    public static IEnumerable<object[]> Limits()
    {
        foreach (var index in Enumerable.Range(0, 8))
            foreach (var kind in new[] { Utf8ResourceLimitKind.ConstructionBytes,
                Utf8ResourceLimitKind.ConstructionWorkSteps, Utf8ResourceLimitKind.ConstructionDepth })
                yield return [index, kind];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void NumberingNamesAndReferencesSurviveClosedOwnersAndCallerArrayMutation(int index)
    {
        var seed = Seed(index);
        var catalog = Utf8CaptureCatalog.Create(seed.Unnamed, seed.Definitions, seed.References);
        Assert.Equal(seed.Numbers, catalog.GroupNumbers);
        Assert.Equal(seed.Names, catalog.GroupNames);
        Assert.Equal(seed.Assigned, catalog.DefinitionNumbers);
        Assert.Equal(seed.Shared, catalog.HasSharedCaptureDeclarations);
        Assert.Equal(seed.Explicit, catalog.HasExplicitCaptureNumbers);
        for (var ordinal = 0; ordinal < seed.Numbers.Length; ordinal++)
        {
            var number = seed.Numbers[ordinal];
            Assert.Equal(ordinal, catalog.OrdinalFromNumber(number));
            Assert.Equal(number, catalog.ReferenceNumber(number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.Equal(number, catalog.GroupNumberFromName(seed.Names[ordinal]));
            Assert.Equal(seed.Names[ordinal], catalog.GroupNameFromNumber(number));
        }
        Assert.Equal(-1, catalog.OrdinalFromNumber(-1));
        Assert.Equal(-1, catalog.ReferenceNumber("missing"));
        Assert.Equal(-1, catalog.GroupNumberFromName("missing"));
        Assert.Equal(string.Empty, catalog.GroupNameFromNumber(-1));
        Array.Fill(seed.Definitions, new("changed", 8, false, 0));
        Assert.Equal(seed.Assigned, catalog.DefinitionNumbers);
        Assert.Equal(seed.Names, catalog.GroupNames);
    }

    [Theory]
    [MemberData(nameof(Limits))]
    public void EveryCutoffEitherAdmitsIdenticalMetadataOrRestoresTheFundedParent(int index, Utf8ResourceLimitKind kind)
    {
        var seed = Seed(index);
        ulong bytes, work;
        using (var funded = Utf8ConstructionScope.Enter())
        {
            _ = funded.Budget.AllocateObject(17, static value => new Parent(value));
            _ = Utf8CaptureCatalog.Create(seed.Unnamed, seed.Definitions, seed.References);
            bytes = funded.Budget.PeakBytes - 112;
            work = funded.Budget.WorkSteps - 2;
            Assert.Equal(0ul, funded.Budget.Depth);
        }
        var maximum = kind == Utf8ResourceLimitKind.ConstructionBytes ? bytes
            : kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? work : 1;
        var rejected = 0;
        for (ulong cutoff = 0; cutoff <= maximum; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity,
                kind == Utf8ResourceLimitKind.ConstructionBytes ? 112 + cutoff : Utf8ConstructionScope.DefaultBufferBytes,
                kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? 2 + cutoff : Utf8ConstructionScope.DefaultWorkSteps,
                kind == Utf8ResourceLimitKind.ConstructionDepth ? cutoff : Utf8ConstructionScope.DefaultDepth);
            var budget = owner.Budget;
            var parent = budget.AllocateObject(17, static value => new Parent(value));
            var checkpoint = budget.ObjectCheckpoint;
            Utf8CaptureCatalog? result = null;
            var failure = Record.Exception(() => result = Utf8CaptureCatalog.Create(seed.Unnamed, seed.Definitions, seed.References));
            Assert.Equal(0ul, budget.Depth);
            Assert.Equal(0ul, budget.Reserved);
            Assert.True(budget.WorkSteps <= budget.MaximumWorkSteps);
            Assert.True(budget.PeakBytes <= budget.MaximumBytes);
            if (failure is null)
            {
                Assert.NotNull(result);
                Assert.Equal(seed.Numbers, result.GroupNumbers);
                Assert.Equal(seed.Names, result.GroupNames);
                Assert.True(budget.ReservedBytes > 112);
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
            if (cutoff == maximum) Assert.Null(failure);
            if (kind == Utf8ResourceLimitKind.ConstructionDepth && cutoff == 0)
            {
                Assert.Equal(2ul, budget.WorkSteps);
                Assert.Equal(0ul, budget.PeakDepth);
            }
        }
        Assert.True(rejected > 0);
    }

    [Fact]
    public void TheOnlyConstructorAdmitsAllSevenFieldsBeforeAcquiringTheCatalog()
    {
        var type = typeof(Utf8CaptureCatalog);
        var constructor = Assert.Single(type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var state = Assert.Single(constructor.GetParameters()).ParameterType;
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        var stateFields = state.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(7, fields.Length);
        Assert.Equal(fields.Select(field => field.FieldType), stateFields.Select(field => field.FieldType));
        using var owner = Utf8ConstructionScope.Enter();
        var catalog = Utf8CaptureCatalog.Create(0, [], false);
        var acquisition = owner.Budget.ObjectCheckpoint!;
        Assert.Same(catalog, acquisition.Value);
        Assert.Equal(144ul, acquisition.Bytes);
        Assert.Equal(240ul, owner.Budget.ReservedBytes);
    }

    [Fact]
    public void RetainedLookupsSpendTheCurrentOwnerInsteadOfTheClosedProducer()
    {
        Utf8CaptureCatalog catalog;
        Utf8ConstructionBudget producer;
        using (var original = Utf8ConstructionScope.Enter())
        {
            producer = original.Budget;
            var seed = Seed(3);
            catalog = Utf8CaptureCatalog.Create(seed.Unnamed, seed.Definitions, false);
            original.Complete();
        }
        var spent = producer.WorkSteps;
        Assert.Equal(9, catalog.GroupNumberFromName("9"));
        Assert.Equal(spent, producer.WorkSteps);
        using var current = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => catalog.GroupNumberFromName("9")).ResourceKind);
        Assert.Equal(spent, producer.WorkSteps);
    }

    private sealed class Parent(int value) { internal readonly int Value = value; }

    private sealed record CatalogSeed(int Unnamed, Utf8CaptureDefinition[] Definitions, bool References,
        int[] Assigned, int[] Numbers, string[] Names, bool Shared = false, bool Explicit = false);

    private static CatalogSeed Seed(int index) => index switch
    {
        0 => new(0, [], false, [], [0], ["0"]),
        1 => new(3, [], false, [], [0, 1, 2, 3], ["0", "1", "2", "3"]),
        2 => new(1, [new("named", 0, true, 9)], false, [2], [0, 1, 2], ["0", "1", "named"]),
        3 => new(1, [new("9", 9, false, 1)], false, [9], [0, 1, 9], ["0", "1", "9"], Explicit: true),
        4 => new(1, [new("x", 0, true, 30), new("x", 0, true, 10), new("y", 0, true, 20), new("2", 2, false, 5)],
            false, [3, 3, 4, 2], [0, 1, 2, 3, 4], ["0", "1", "2", "x", "y"], true, true),
        5 => new(0, [new("2147483647", int.MaxValue, false, 1)], false, [int.MaxValue], [0, int.MaxValue], ["0", "2147483647"], Explicit: true),
        6 => new(0, [new("0", 0, false, 1)], false, [0], [0], ["0"], Shared: true),
        7 => new(0, [], true, [], [0], ["0"], Shared: true),
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
}
