using System.Reflection;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionArenaNumericTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ArenaReleasesNumericScratchAndKeepsOnlyCompletedRetainedCharges(bool complete)
    {
        var budget = new Utf8ConstructionBudget(1000, 1000000);
        budget.ReserveBytes(13);
        var arena = new Utf8ConstructionArena(budget);
        var scratch = arena.Allocate<byte>(17, false);
        var retained = arena.Allocate<int>(3, true);
        scratch[0] = 42; retained[0] = 99;
        var retainedExtent = Utf8ConstructionBudget.ArrayBytes<int>(3);
        var scratchExtent = Utf8ConstructionBudget.ArrayBytes<byte>(17);
        arena.Retain(scratch);
        arena.Retain(scratch); // Shared publication is intentionally counted twice.
        Assert.Equal(13UL + retainedExtent + 3 * scratchExtent, budget.ReservedBytes);
        var work = budget.WorkSteps;
        if (complete) arena.Complete();
        arena.Dispose(); arena.Dispose();
        Assert.Equal(complete ? 13UL + retainedExtent + 2 * scratchExtent : 13UL, budget.ReservedBytes);
        Assert.Equal(work, budget.WorkSteps);
        Assert.Equal((byte)42, scratch[0]); Assert.Equal(99, retained[0]);
    }

    [Fact]
    public void ConservativePublicationDeniesBeforeChangingItsAggregate()
    {
        var extent = Utf8ConstructionBudget.ArrayBytes<byte>(17);
        var budget = new Utf8ConstructionBudget(1000, 13 + 2 * extent - 1);
        budget.ReserveBytes(13);
        using var arena = new Utf8ConstructionArena(budget);
        var array = arena.Allocate<byte>(17, false);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => arena.Retain(array));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, error.ResourceKind);
        Assert.Equal(13 + extent, budget.ReservedBytes);
        arena.Dispose();
        Assert.Equal(13UL, budget.ReservedBytes);
    }

    [Fact]
    public void TakenAndAdoptedLoansKeepTheirOriginalAdmissionWithoutDescriptors()
    {
        var budget = new Utf8ConstructionBudget(1000, 1000000);
        using var arena = new Utf8ConstructionArena(budget);
        var adopted = budget.AllocateArray<int>(5, false);
        arena.Adopt(adopted, false);
        using var buffer = new Utf8ConstructionBuffer<byte>(budget);
        buffer.AddRange("value"u8);
        var taken = arena.Take(buffer, true);
        arena.Complete(); arena.Dispose(); buffer.Dispose();
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(taken.Length), budget.ReservedBytes);
        Assert.Equal("value"u8.ToArray(), taken);
        Assert.Equal(5, adopted.Length);
        var fields = typeof(Utf8ConstructionArena).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.All(fields, field => Assert.True(field.FieldType == typeof(Utf8ConstructionBudget) || field.FieldType.IsValueType));
    }

    [Fact]
    public void AggregatePublicationCannotWrapTheSharedLedger()
    {
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, ulong.MaxValue);
        budget.ReserveBytes(ulong.MaxValue - 31);
        using var arena = new Utf8ConstructionArena(budget);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => arena.Retain(new byte[1]));
        Assert.Equal(ulong.MaxValue, error.Requested);
        arena.Dispose();
        Assert.Equal(ulong.MaxValue - 31, budget.ReservedBytes);
    }

    [Fact]
    public void CanceledOrDeniedAcquisitionKeepsCallerLoanAndRetainedAliases()
    {
        var budget = new Utf8ConstructionBudget(1000, 1000000);
        budget.ReserveBytes(13);
        using var token = new CancellationTokenSource();
        budget.CancellationToken = token.Token;
        var arena = new Utf8ConstructionArena(budget);
        var array = arena.Allocate<int>(1, true);
        array[0] = 42;
        var held = budget.ReservedBytes;
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => arena.Allocate<byte>(100, false)).CancellationToken);
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => arena.Retain(array)).CancellationToken);
        Assert.Equal(held, budget.ReservedBytes);
        arena.Dispose();
        Assert.Equal(13UL, budget.ReservedBytes);
        Assert.Equal(42, array[0]);
    }
}
