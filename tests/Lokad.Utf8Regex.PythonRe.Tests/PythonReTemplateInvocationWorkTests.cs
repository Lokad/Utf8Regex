using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTemplateInvocationWorkTests
{
    private delegate PythonReReplacementPlan Prepare(ReadOnlySpan<byte> input, string replacement,
        Utf8SliceInvocation invocation, CancellationToken cancellationToken);

    private static Prepare Preparation(Utf8PythonRegex regex) => typeof(Utf8PythonRegex)
        .GetMethod("GetReplacementPlan", BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(ReadOnlySpan<byte>), typeof(string), typeof(Utf8SliceInvocation), typeof(CancellationToken)], null)!
        .CreateDelegate<Prepare>(regex);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongTemplateIsDeniedBeforeColdPreparationEvenWhenSubjectIsEmpty(bool text)
    {
        var template = new string('z', 1 << 20);
        var regex = new Utf8PythonRegex("a", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 64, MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 });
        using var parent = Utf8ConstructionScope.Enter();
        var retained = parent.Budget.AllocateArray<byte>(13, false);
        retained[0] = 42;
        var held = parent.Budget.ReservedBytes;
        var spent = parent.Budget.WorkSteps;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (text) _ = regex.ReplaceToString([], template);
            else _ = regex.Replace([], template);
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
        Assert.Equal(64UL, failure.Limit);
        Assert.Equal(65UL, failure.Requested);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal(spent, parent.Budget.WorkSteps);
        Assert.Equal(held, parent.Budget.ReservedBytes);
        Assert.Equal(42, retained[0]);
        Assert.Empty(regex.Replace([], "z"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2048)]
    [InlineData(2049)]
    public void WarmTemplateLookupSpendsBoundedLengthWorkWithoutReparsing(int length)
    {
        var regex = new Utf8PythonRegex("a");
        var prepare = Preparation(regex);
        var template = new string('z', length);
        var warm = new Utf8SliceInvocation(new(100000, 128, 16 << 20), Timeout.InfiniteTimeSpan, default);
        var plan = prepare([], template, warm, default);
        using var parent = Utf8ConstructionScope.Enter();
        var invocation = new Utf8SliceInvocation(new(100000, 128, 16 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.Equal(plan, prepare([], template, invocation, default));
        Assert.Equal((ulong)((length + 2047) / 2048), invocation.Budget.Steps);
        Assert.Equal(0UL, parent.Budget.WorkSteps);
        Assert.Equal(0UL, parent.Budget.ReservedBytes);
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarmLengthDenialKeepsThePreviouslyPublishedPlan(bool text)
    {
        var regex = new Utf8PythonRegex("a", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        var prepare = Preparation(regex);
        var template = new string('z', 4096);
        var funded = new Utf8SliceInvocation(new(100000, 128, 16 << 20), Timeout.InfiniteTimeSpan, default);
        var retained = prepare([], template, funded, default);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (text) _ = regex.ReplaceToString([], template);
            else _ = regex.Replace([], template);
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
        Assert.Equal(1UL, failure.Limit);
        Assert.Equal(2UL, failure.Requested);
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
        var recovery = new Utf8SliceInvocation(new(100000, 128, 16 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.Equal(retained, prepare([], template, recovery, default));
        Assert.Equal(template.Length, retained.LiteralUtf8!.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LengthAdmissionUsesOriginalInterruptionBeforeCachePublication(bool timeout)
    {
        var regex = new Utf8PythonRegex("a");
        var prepare = Preparation(regex);
        using var token = new CancellationTokenSource();
        long now = 1000;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        var invocation = new Utf8SliceInvocation(new(100000, 128, 16 << 20),
            timeout ? TimeSpan.FromMilliseconds(1) : Timeout.InfiniteTimeSpan, token.Token);
        invocation.CheckpointHook = step => { if (step == 64) { if (timeout) now++; else token.Cancel(); } };
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        var failure = Record.Exception(() => prepare([], new string('z', 1 << 20), invocation, token.Token));
        if (timeout) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        invocation.Ledger.Release(13);
    }
}
