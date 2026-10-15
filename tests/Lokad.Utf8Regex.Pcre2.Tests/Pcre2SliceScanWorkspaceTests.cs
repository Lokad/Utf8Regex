using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2SliceScanWorkspaceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(17)]
    public void FailedCandidatesResetCapturesAndEachAdvanceRestoresParent(int offset)
    {
        var regex = Create(new() { MatchLimit = 100000, DepthLimit = 256,
            HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 });
        var compiled = regex.DebugCompiledProgram;
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(compiled.Operations.Count);
        var bytes = Encoding.UTF8.GetBytes("😀aa!😀aaabb 😀aab");
        var storage = new byte[offset + bytes.Length + 1];
        storage.AsSpan().Fill(0xff);
        bytes.CopyTo(storage, offset);
        var input = Utf8ValidatedInput.Create(storage.AsSpan(offset, bytes.Length));
        Assert.Equal(2, regex.Count(input.Bytes));
        var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, input,
            new Utf8BytePosition(0), compiled.Request, default);
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(7));
        try
        {
            Assert.True(cursor.MoveNext());
            Assert.Equal((11, 16), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
            Assert.Equal(7UL, cursor.WorkingInvocation.Ledger.ReservedBytes);
            var retained = cursor.CaptureGroupsAt(11, true);
            Assert.Equal((11, 14), (retained[1].StartOffsetInBytes, retained[1].EndOffsetInBytes));
            Assert.Equal((14, 16), (retained[2].StartOffsetInBytes, retained[2].EndOffsetInBytes));
            Assert.Equal(7UL, cursor.WorkingInvocation.Ledger.ReservedBytes);
            var alias = cursor;
            Assert.True(cursor.MoveNext());
            Assert.Equal((21, 24), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
            Assert.Equal(7UL, cursor.WorkingInvocation.Ledger.ReservedBytes);
            Assert.False(cursor.MoveNext());
            Assert.Equal(7UL, cursor.WorkingInvocation.Ledger.ReservedBytes);
            Assert.True(alias.MoveNext());
            Assert.Equal((21, 24), (alias.Current.StartOffsetInBytes, alias.Current.EndOffsetInBytes));
            Assert.Equal(7UL, alias.WorkingInvocation.Ledger.ReservedBytes);
            Assert.Equal((11, 16), (retained[0].StartOffsetInBytes, retained[0].EndOffsetInBytes));
        }
        finally { cursor.WorkingInvocation.Ledger.Release(7); }
    }

    [Theory]
    [InlineData(0)] // Work denial after candidate scanning has started.
    [InlineData(1)] // Heap denial during scratch acquisition.
    [InlineData(2)] // Cancellation from an in-scan checkpoint.
    [InlineData(3)] // Original deadline expires at an in-scan checkpoint.
    public void FailedScansRestoreParentWithoutReplacingInvocationAuthority(int failure)
    {
        using var cancellation = new CancellationTokenSource();
        var expired = false;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(
            () => expired ? long.MaxValue / 2 : 0);
        var regex = Create(new() { MatchLimit = failure == 0 ? 64U : 100000U,
            DepthLimit = 256, HeapLimitInBytes = failure == 1 ? 128UL : 1UL << 20 },
            failure == 3 ? TimeSpan.FromMilliseconds(1) : Timeout.InfiniteTimeSpan);
        var compiled = regex.DebugCompiledProgram;
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(compiled.Operations.Count);
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aa!😀", 128)) + "aaabb");
        var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram,
            Utf8ValidatedInput.Create(bytes), new Utf8BytePosition(0), compiled.Request, cancellation.Token);
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(7));
        cursor.WorkingInvocation.CheckpointHook = _ =>
        {
            if (failure == 2) cancellation.Cancel();
            if (failure == 3) expired = true;
        };
        Exception? error = null;
        try { _ = cursor.MoveNext(); }
        catch (Exception caught) { error = caught; }
        Assert.Equal(7UL, cursor.WorkingInvocation.Ledger.ReservedBytes);
        cursor.WorkingInvocation.Ledger.Release(7);
        if (failure == 2)
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        else
            Assert.Equal(failure == 0 ? Pcre2ErrorKind.MatchLimit :
                failure == 1 ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.Timeout,
                Assert.IsType<Pcre2MatchException>(error).ErrorKind);
        expired = false;
        Assert.Equal(1, new Utf8Pcre2Regex("(a+)(b+)").Count(bytes));
    }

    [Fact]
    public void EmptyRetryKeepsWholeScalarAdvancementWithFiniteControls()
    {
        var regex = new Utf8Pcre2Regex("x*|😀+", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256,
                HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 }, Timeout.InfiniteTimeSpan);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        var bytes = "é😀😀"u8.ToArray();
        Assert.Equal(4, regex.Count(bytes));
        var cursor = regex.EnumerateMatches(bytes);
        var ranges = new List<(int, int)>();
        while (cursor.MoveNext())
            ranges.Add((cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.Equal(new[] { (0, 0), (2, 2), (2, 10), (10, 10) }, ranges);
    }

    private static Utf8Pcre2Regex Create(Utf8Pcre2ExecutionLimits limits, TimeSpan? timeout = null)
        => new("(a+)(b+)", Pcre2CompileOptions.None, default, limits,
            timeout ?? Timeout.InfiniteTimeSpan);
}
