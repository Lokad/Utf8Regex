using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReReplacementCacheChargeTests
{
    private static ulong ArrayBytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);
    private static readonly IReadOnlyDictionary<string, int> Names = new Dictionary<string, int> { ["word"] = 1 };
    private static ulong PhysicalBytes(PythonReReplacementPlan value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.Equal(2, typeof(PythonReReplacementPlan).GetFields(flags).Length);
        Assert.Equal(3, typeof(PythonReReplacementToken).GetFields(flags).Length);
        var seen = new Dictionary<object, ulong>(ReferenceEqualityComparer.Instance);
        void Array<T>(T[]? array) { if (array is { Length: > 0 }) seen.TryAdd(array, ArrayBytes<T>(array.Length)); }
        Array(value.Tokens); Array(value.LiteralUtf8);
        foreach (var token in value.Tokens) Array(token.Utf8);
        return seen.Values.Aggregate(0UL, static (sum, bytes) => sum + bytes);
    }

    [Theory]
    [InlineData("")] [InlineData("é😀")] [InlineData(@"é\g<word>😀\1")]
    [InlineData(@"\101\1\041")] [InlineData(@"\g<0>\g<1>")]
    public void ClosedChargesCoverCompleteProgramsAndExactCacheBoundaries(string template)
    {
        var value = PythonReReplacementParser.Parse(template, 1, Names); var physical = PhysicalBytes(value);
        using var owner = Utf8ConstructionScope.Enter(); var storage = default(PythonReReplacementRetainedStorage);
        Assert.True(storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out var charge));
        Assert.True(charge >= physical);
        Assert.True(storage.TryGetValueCharge(value, charge, default(Utf8ConstructionTextNoControl), out var exact)); Assert.Equal(charge, exact);
        if (charge > 0) Assert.False(storage.TryGetValueCharge(value, charge - 1, default(Utf8ConstructionTextNoControl), out _));
        var keyCharge = storage.GetKeyCharge(template);
        foreach (var allow in new[] { false, true })
        {
            if (keyCharge + charge == 0) continue;
            var maximum = keyCharge + charge - (allow ? 0UL : 1UL);
            var cache = Utf8BoundedPreparationCache<string, PythonReReplacementPlan>.CreateRetained(2, StringComparer.Ordinal, 65536, maximum);
            var result = cache.GetOrAdd(template, value, static (_, value) => value, default(Utf8ConstructionTextNoControl), storage);
            Assert.Same(value.Tokens, result.Tokens); Assert.Equal(allow ? 1 : 0, cache.Count);
            Assert.Equal(allow ? maximum : 0UL, cache.PayloadBytes);
            cache.Clear(); Assert.Same(value.Tokens, result.Tokens);
        }
    }

    [Fact]
    public void SharedLiteralArraysCountPerSlotWithoutAccountingAllocations()
    {
        var bytes = "é😀"u8.ToArray();
        var value = new PythonReReplacementPlan([PythonReReplacementToken.ForLiteral(bytes), PythonReReplacementToken.ForLiteral(bytes)], bytes);
        var expected = ArrayBytes<PythonReReplacementToken>(2) + 3 * ArrayBytes<byte>(bytes.Length);
        Assert.True(expected > PhysicalBytes(value));
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        var storage = default(PythonReReplacementRetainedStorage);
        for (var index = 0; index < 3; index++) Assert.True(storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out _));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var accepted = storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out var charge);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(accepted); Assert.Equal(expected, charge); Assert.Equal(0, allocated);
        Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(7UL, owner.Budget.PeakBytes);
        Assert.False(storage.TryGetValueCharge(value, expected - 1, default(Utf8ConstructionTextNoControl), out _));
    }

    [Fact]
    public void WorkCutoffsAndTokenPrecedencePreserveTheOriginalOwner()
    {
        var value = PythonReReplacementParser.Parse(@"é\g<word>😀\1", 1, Names);
        var storage = default(PythonReReplacementRetainedStorage); ulong steps;
        using (var probe = Utf8ConstructionScope.Enter())
        { Assert.True(storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out _)); steps = probe.Budget.WorkSteps; }
        for (ulong cap = 0; cap <= steps; cap++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cap, ulong.MaxValue); owner.Budget.ReserveBytes(7);
            var failure = Record.Exception(() => storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out _));
            if (cap < steps) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
            else Assert.Null(failure);
            Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.InRange(owner.Budget.WorkSteps, 0UL, cap);
        }
        using var source = new CancellationTokenSource(); source.Cancel();
        using var canceled = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        var error = Assert.Throws<OperationCanceledException>(() => storage.TryGetValueCharge(value, 0, new TokenControl(source.Token), out _));
        Assert.Equal(source.Token, error.CancellationToken); Assert.Equal(0UL, canceled.Budget.WorkSteps);
    }

    private readonly struct TokenControl(CancellationToken token) : IUtf8ConstructionTextControl
    { public void Check() => token.ThrowIfCancellationRequested(); }

    [Fact]
    public void TheParentDeadlinePrecedesPayloadDenialWithoutBeingRestarted()
    {
        var value = PythonReReplacementParser.Parse(@"é\g<word>😀", 1, Names); var storage = default(PythonReReplacementRetainedStorage);
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
        owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        ticks = 15; Assert.True(storage.TryGetValueCharge(value, ulong.MaxValue, default(Utf8ConstructionTextNoControl), out _));
        var spent = owner.Budget.WorkSteps;
        ticks = 25; Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => storage.TryGetValueCharge(value, 0, default(Utf8ConstructionTextNoControl), out _));
        Assert.Equal(spent, owner.Budget.WorkSteps); Assert.Equal(7UL, owner.Budget.ReservedBytes);
    }
}
