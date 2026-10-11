using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementCacheChargeTests
{
    private delegate bool Charge(Pcre2ReplacementPreparation value, ulong maximum, Utf8ConstructionTextNoControl control, out ulong charge);
    private static readonly Charge Compute = CreateCharge();
    private static Charge CreateCharge()
    {
        var type = typeof(Utf8Pcre2Regex).GetNestedType("Pcre2ReplacementRetainedStorage", BindingFlags.NonPublic)!;
        var method = type.GetMethod("TryGetValueCharge")!.MakeGenericMethod(typeof(Utf8ConstructionTextNoControl));
        return (Charge)method.CreateDelegate(typeof(Charge), Activator.CreateInstance(type));
    }
    private static ulong ArrayBytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);
    private static ulong PhysicalBytes(Pcre2ReplacementPreparation value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.Equal(2, typeof(Pcre2ReplacementPreparation).GetFields(flags).Length);
        Assert.Equal(1, typeof(Pcre2SimpleReplacementPlan).GetFields(flags).Length);
        Assert.Equal(2, typeof(Pcre2SimpleReplacementSegment).GetFields(flags).Length);
        Assert.Equal(2, typeof(Pcre2PreparedReplacementPlan).GetFields(flags).Length);
        Assert.Equal(2, typeof(Pcre2ReplacementFragment).GetFields(flags).Length);
        Assert.Equal(8, typeof(Pcre2ReplacementInstruction).GetFields(flags).Length);
        var seen = new Dictionary<object, ulong>(ReferenceEqualityComparer.Instance);
        void Array<T>(T[]? array) { if (array is { Length: > 0 }) seen.TryAdd(array, ArrayBytes<T>(array.Length)); }
        if (value.Simple is { } simple) { Array(simple.Segments); foreach (var segment in simple.Segments) Array(segment.LiteralUtf8); }
        if (value.Prepared is { } prepared) { Array(prepared.Instructions); Array(prepared.Fragments); foreach (var instruction in prepared.Instructions) Array(instruction.Payload); }
        return seen.Values.Aggregate(0UL, static (sum, bytes) => sum + bytes);
    }

    [Theory]
    [InlineData("")] [InlineData("é😀")] [InlineData("a$0é")]
    [InlineData("${name}")] [InlineData("${name:+yes:no}")] [InlineData(@"\Ué$1\E")]
    public void BothActualProgramShapesFitConservativeExactCharges(string template)
    {
        Pcre2ReplacementPreparation value;
        if (Pcre2SimpleReplacementParser.TryParse(template, Pcre2SubstitutionOptions.None, out var simple)) value = new(simple, null);
        else value = new(null, Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended));
        var physical = PhysicalBytes(value);
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        Assert.True(Compute(value, ulong.MaxValue, default, out var charge)); Assert.True(charge >= physical);
        Assert.True(Compute(value, charge, default, out var exact)); Assert.Equal(charge, exact);
        if (charge > 0) Assert.False(Compute(value, charge - 1, default, out _));
        Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(7UL, owner.Budget.PeakBytes);
    }

    [Fact]
    public void SharedPayloadSlotsAndNullInstructionsNeedNoAccountingAllocations()
    {
        var bytes = "é😀"u8.ToArray();
        var value = new Pcre2ReplacementPreparation(
            new([new(bytes, false), new(bytes, false)]),
            new([new(Pcre2ReplacementOpcode.Literal, default, 0, bytes, 0, 0, -1, -1), default], [new(0, 0)]));
        var expected = ArrayBytes<Pcre2SimpleReplacementSegment>(2)
            + ArrayBytes<Pcre2ReplacementInstruction>(2) + ArrayBytes<Pcre2ReplacementFragment>(1) + 3 * ArrayBytes<byte>(bytes.Length);
        Assert.True(expected > PhysicalBytes(value));
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        for (var index = 0; index < 3; index++) Assert.True(Compute(value, ulong.MaxValue, default, out _));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var accepted = Compute(value, ulong.MaxValue, default, out var charge);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(accepted); Assert.Equal(expected, charge); Assert.Equal(0, allocated);
        Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(7UL, owner.Budget.PeakBytes);
        Assert.False(Compute(value, expected - 1, default, out _));
    }

    [Fact]
    public void EveryWorkCutoffAndEmptyValueChecksUseTheParentBudget()
    {
        var value = new Pcre2ReplacementPreparation(null, Pcre2PreparedReplacementParser.Parse("é$1😀", Pcre2SubstitutionOptions.Extended));
        ulong steps;
        using (var probe = Utf8ConstructionScope.Enter()) { Assert.True(Compute(value, ulong.MaxValue, default, out _)); steps = probe.Budget.WorkSteps; }
        for (ulong cap = 0; cap <= steps; cap++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cap, ulong.MaxValue); owner.Budget.ReserveBytes(7);
            var failure = Record.Exception(() => Compute(value, ulong.MaxValue, default, out _));
            if (cap < steps) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
            else Assert.Null(failure);
            Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.InRange(owner.Budget.WorkSteps, 0UL, cap);
        }
        using var source = new CancellationTokenSource(); source.Cancel();
        using var canceled = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        canceled.Budget.CancellationToken = source.Token;
        var error = Assert.Throws<OperationCanceledException>(() => Compute(default, 0, default, out _));
        Assert.Equal(source.Token, error.CancellationToken); Assert.Equal(0UL, canceled.Budget.WorkSteps);
    }

    [Fact]
    public void EmptyPreparationStillChecksTheOriginalDeadline()
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        ticks = 15; Assert.True(Compute(default, 0, default, out var charge)); Assert.Equal(0UL, charge);
        var spent = owner.Budget.WorkSteps;
        ticks = 25; Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => Compute(default, 0, default, out _));
        Assert.Equal(spent, owner.Budget.WorkSteps); Assert.Equal(7UL, owner.Budget.ReservedBytes);
    }
}
