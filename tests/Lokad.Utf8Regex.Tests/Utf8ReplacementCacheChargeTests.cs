using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ReplacementCacheChargeTests
{
    private static ulong ArrayBytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);
    private static ulong TextBytes(string? text) => string.IsNullOrEmpty(text) ? 0 : 32UL + ((2 * ((ulong)text.Length + 1) + 15) & ~15UL);
    private static Utf8AnalyzedReplacement Prepare(string template)
    {
        return Utf8FrontEndReplacementAnalyzer.Analyze(template, [0, 1], ["0", "word"]);
    }

    private static ulong PhysicalBytes(Utf8AnalyzedReplacement value)
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Assert.Equal(5, typeof(Utf8ReplacementPattern).GetFields(fields).Length);
        Assert.Single(typeof(Utf8ReplacementPlan).GetFields(fields));
        Assert.Equal(5, typeof(Utf8ReplacementToken).GetFields(fields).Length);
        Assert.Equal(4, typeof(Utf8ReplacementInstruction).GetFields(fields).Length);
        var seen = new Dictionary<object, ulong>(ReferenceEqualityComparer.Instance)
        { [value.Pattern] = 48, [value.Plan] = 48 };
        void Text(string? text) { if (text is { Length: > 0 }) seen.TryAdd(text, TextBytes(text)); }
        void Array<T>(T[]? array) { if (array is { Length: > 0 }) seen.TryAdd(array, ArrayBytes<T>(array.Length)); }
        Text(value.OriginalText); Array(value.LiteralUtf8); Array(value.Pattern.Tokens);
        Array((Utf8ReplacementInstruction[])value.Plan.Instructions);
        foreach (var token in value.Pattern.Tokens) { Text(token.Literal); Text(token.ReferenceText); }
        foreach (var instruction in value.Plan.Instructions) { Text(instruction.LiteralText); Array(instruction.LiteralUtf8); }
        return seen.Values.Aggregate(0UL, static (total, bytes) => total + bytes);
    }

    [Theory]
    [InlineData("")] [InlineData("é😀")] [InlineData("${word}")] [InlineData("x$1y$01z")]
    [InlineData("$&$`$'$+$$_")] [InlineData("é${missing}😀$999")]
    public void ClosedChargesCoverPhysicalPayloadsAndExactCacheBoundaries(string template)
    {
        var value = Prepare(template); var physical = PhysicalBytes(value);
        var storage = default(Utf8ReplacementRetainedStorage);
        using var owner = Utf8ConstructionScope.Enter();
        Assert.True(storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out var charge));
        Assert.True(charge >= physical);
        Assert.True(storage.TryGetValueCharge(value, charge, default(Utf8ConstructionTextNoControl), out var exact)); Assert.Equal(charge, exact);
        Assert.False(storage.TryGetValueCharge(value, charge - 1, default(Utf8ConstructionTextNoControl), out _));
        var keyCharge = storage.GetKeyCharge(template);
        foreach (var allow in new[] { false, true })
        {
            var maximum = keyCharge + charge - (allow ? 0UL : 1UL);
            var cache = Utf8BoundedPreparationCache<string, Utf8AnalyzedReplacement>.CreateRetained(2, StringComparer.Ordinal, 65536, maximum);
            var result = cache.GetOrAdd(template, value, static (_, value) => value, default(Utf8ConstructionTextNoControl), storage);
            Assert.Same(value.Plan, result.Plan); Assert.Equal(allow ? 1 : 0, cache.Count);
            Assert.Equal(allow ? maximum : 0UL, cache.PayloadBytes);
            cache.Clear(); Assert.Equal(value.Plan.Instructions.Count, result.Plan.Instructions.Count);
        }
    }

    [Fact]
    public void SharedLiteralSlotsAreChargedAgainWithoutAllocatingAccountingStorage()
    {
        var value = Prepare("é😀"); var physical = PhysicalBytes(value);
        Assert.Same(value.LiteralUtf8, value.Plan.Instructions[0].LiteralUtf8);
        Assert.Same(value.Pattern.Tokens[0].Literal, value.Plan.Instructions[0].LiteralText);
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        var storage = default(Utf8ReplacementRetainedStorage);
        for (var index = 0; index < 3; index++) Assert.True(storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out _));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var accepted = storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out var charge);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(accepted); Assert.True(charge > physical); Assert.Equal(0, allocated);
        Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(7UL, owner.Budget.PeakBytes);
    }

    [Fact]
    public void EveryWorkCutoffPreservesParentCapacityAndOriginalCancellation()
    {
        var value = Prepare("é$1😀$1é"); var storage = default(Utf8ReplacementRetainedStorage);
        ulong steps;
        using (var probe = Utf8ConstructionScope.Enter())
        { Assert.True(storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out _)); steps = probe.Budget.WorkSteps; }
        for (ulong cap = 0; cap <= steps; cap++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cap, ulong.MaxValue); owner.Budget.ReserveBytes(7);
            var error = Record.Exception(() => storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out _));
            if (cap < steps) { var failure = Assert.IsType<Utf8ResourceLimitExceededException>(error); Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind); }
            else Assert.Null(error);
            Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.InRange(owner.Budget.WorkSteps, 0UL, cap);
        }
        using var source = new CancellationTokenSource(); source.Cancel();
        using var canceled = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        var cancellation = Assert.Throws<OperationCanceledException>(() => storage.TryGetValueCharge(value, 0, new TokenControl(source.Token), out _));
        Assert.Equal(source.Token, cancellation.CancellationToken); Assert.Equal(0UL, canceled.Budget.WorkSteps);
    }

    private readonly struct TokenControl(CancellationToken token) : IUtf8ConstructionTextControl
    { public void Check() => token.ThrowIfCancellationRequested(); }

    [Fact]
    public void TheParentDeadlineIsNotRestartedForFlatChargeComputation()
    {
        var value = Prepare("é$1😀"); var storage = default(Utf8ReplacementRetainedStorage);
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        ticks = 15; Assert.True(storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out _));
        var spent = owner.Budget.WorkSteps;
        ticks = 25; Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => storage.TryGetValueCharge(value, 0, default(Utf8ConstructionTextNoControl), out _));
        Assert.Equal(spent, owner.Budget.WorkSteps); Assert.Equal(7UL, owner.Budget.ReservedBytes);
    }
}
