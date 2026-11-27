using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTemplateInvocationTests
{
    private delegate PythonReReplacementPlan Prepare(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacement,
        ref Utf8SliceInvocation invocation, CancellationToken cancellationToken, out Utf8FundedStorage.Use? memoryUse);

    private static Prepare Preparation(Utf8PythonRegex regex) => typeof(Utf8PythonRegex)
        .GetMethod("GetReplacementPlan", BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(ReadOnlySpan<byte>), typeof(ReadOnlySpan<byte>), typeof(Utf8SliceInvocation).MakeByRefType(),
                typeof(CancellationToken), typeof(Utf8FundedStorage.Use).MakeByRefType()], null)!
        .CreateDelegate<Prepare>(regex);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2048)]
    [InlineData(2049)]
    public void WarmTemplateLookupBorrowsTheCachedPlanWithoutConstructionStorage(int length)
    {
        var regex = new Utf8PythonRegex("a");
        var prepare = Preparation(regex);
        var template = Encoding.UTF8.GetBytes(new string('z', length));
        var warm = Utf8SliceInvocation.CreateExclusive(new(128, 16 << 20), Timeout.InfiniteTimeSpan, default);
        var plan = prepare([], template, ref warm, default, out _);
        using var parent = Utf8ConstructionScope.Enter();
        var invocation = Utf8SliceInvocation.CreateExclusive(new(128, 16 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.Equal(plan, prepare([], template, ref invocation, default, out _));
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ColdPreparationUsesOriginalInterruptionBeforeCachePublication(bool timeout)
    {
        var regex = new Utf8PythonRegex("a");
        var prepare = Preparation(regex);
        using var token = new CancellationTokenSource();
        long now = 1000;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        var invocation = Utf8SliceInvocation.CreateExclusive(new(128, 16 << 20),
            timeout ? TimeSpan.FromMilliseconds(1) : Timeout.InfiniteTimeSpan, token.Token);
        invocation.CheckpointHook = step => { if (step == 64) { if (timeout) now++; else token.Cancel(); } };
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        var template = Encoding.UTF8.GetBytes(new string('z', 1 << 20));
        var failure = Record.Exception(() => prepare([], template, ref invocation, token.Token, out _));
        if (timeout) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        invocation.Ledger.Release(13);
    }
}
