using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8BoundaryMapInvocationTests
{
    public static IEnumerable<object[]> Texts()
    {
        foreach (var text in new[] { "", "a", "é中😀\U00010400\0\uffff", new string('a', 8193),
            string.Concat(Enumerable.Repeat("é中😀", 2200)) }) yield return [text];
        foreach (var width in new[] { "é", "中", "😀" })
        foreach (var prefix in new[] { 2045, 2046, 2047, 2048, 2049 })
            yield return [new string('a', prefix) + width + new string('b', 4097) + width];
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void BoundaryMapPreservesEveryScalarBlockBoundary(string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out var map, out var bytes));
        Assert.Equal(MapBytes(validation), bytes);
        var unit = 0;
        for (var offset = 0; offset < input.Length;)
        {
            Assert.True(map.Resolve(unit).IsScalarBoundary);
            Assert.Equal(offset, map.Resolve(unit).ByteOffset);
            Assert.Equal(System.Buffers.OperationStatus.Done, Rune.DecodeFromUtf8(input.AsSpan(offset), out var scalar, out var width));
            if (!scalar.IsBmp) Assert.False(map.Resolve(unit + 1).IsScalarBoundary);
            unit += scalar.Utf16SequenceLength;
            offset += width;
        }
        Assert.True(map.Resolve(unit).IsScalarBoundary);
        Assert.Equal(input.Length, map.Resolve(unit).ByteOffset);
        Assert.Equal(7 + bytes, invocation.Ledger.ReservedBytes);
        Assert.True(invocation.Budget.Steps > 0);
        invocation.Ledger.Release(bytes);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    private static ulong MapBytes(Utf8ValidationResult validation) => validation.IsAscii ? 0UL :
        64UL + ((ulong)(validation.Utf16Length + 1) * sizeof(int) + 15) / 16 * 16
            + ((ulong)(validation.Utf16Length + 32) / 32 * sizeof(uint) + 15) / 16 * 16;

    [Fact]
    public void EveryProjectionWorkAndStorageCutoffReturnsNoPartialMapAndRestoresBorrowedStorage()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("é中😀", 2200)));
        var validation = Utf8Validation.Validate(input);
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BoundaryMap.TryCreate(input, validation, ref baseline, out _, out _));
        for (ulong cap = 1; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out var map, out var bytes));
            Assert.Equal(0, map.ByteLength);
            Assert.Equal(0UL, bytes);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
        input = Encoding.UTF8.GetBytes("é😀中");
        validation = Utf8Validation.Validate(input);
        var required = MapBytes(validation);
        for (ulong cap = 7; cap <= required + 7; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var breach = Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out var map, out var bytes);
            Assert.Equal(cap == required + 7 ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, breach);
            Assert.Equal(cap == required + 7 ? required : 0UL, bytes);
            Assert.Equal(cap == required + 7 ? input.Length : 0, map.ByteLength);
            Assert.Equal(7 + bytes, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(bytes);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectionCancellationAndDeadlineObserveExactAuthorityAndRollback(bool deadline)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var input = Encoding.UTF8.GetBytes(new string('é', 200000));
        var validation = Utf8Validation.Validate(input);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        invocation.CheckpointHook = _ => { if (deadline) clock.Advance(250); else source.Cancel(); };
        Assert.True(invocation.Ledger.TryReserve(7));
        var map = default(Utf8BoundaryMap);
        ulong bytes = 0;
        var interrupted = false;
        try { Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out map, out bytes); }
        catch (Utf8ExecutionDeadlineExpiredException) { Assert.True(deadline); interrupted = true; }
        catch (OperationCanceledException exception) { Assert.False(deadline); Assert.Equal(source.Token, exception.CancellationToken); interrupted = true; }
        Assert.True(interrupted);
        Assert.Equal(0, map.ByteLength);
        Assert.Equal(0UL, bytes);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void FinalProjectionPublicationChecksCancellationAndImpossibleExtentBeforeAllocation()
    {
        var input = Encoding.UTF8.GetBytes("é😀中");
        var validation = Utf8Validation.Validate(input);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Utf8BoundaryMap.Create(input, validation, ref baseline, out _);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        while (invocation.Budget.Steps < 64 - baseline.Budget.Steps) invocation.TryChargeStep();
        invocation.CheckpointHook = _ => source.Cancel();
        Assert.True(invocation.Ledger.TryReserve(7));
        var interrupted = false;
        try { Utf8BoundaryMap.Create(input, validation, ref invocation, out _); }
        catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); interrupted = true; }
        Assert.True(interrupted);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        invocation = new(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, Utf8BoundaryMap.TryCreate([], new(0, int.MaxValue, false, false), ref invocation, out var map, out var bytes));
        Assert.Equal(0, map.ByteLength);
        Assert.Equal(0UL, bytes);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ProjectionExceptionAfterAllocationRestoresItsAdmission()
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var failed = false;
        // Deliberately inconsistent internal validation forces an exception
        // after map allocation. Public preparation obtains real validation.
        try { Utf8BoundaryMap.Create("abc"u8, new(3, 2, false, false), ref invocation, out _); }
        catch (IndexOutOfRangeException) { failed = true; }
        Assert.True(failed);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

}
