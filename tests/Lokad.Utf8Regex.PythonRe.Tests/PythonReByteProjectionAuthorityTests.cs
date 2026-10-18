using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReByteProjectionAuthorityTests
{
    private static Utf8SliceProgram Program => new global::Lokad.Utf8Regex.Utf8Regex(".")
        .Inspection.AvailableOwnedSliceProgram!;

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(4095)]
    [InlineData(4096)] [InlineData(4097)] [InlineData(16385)]
    public void ByteCopiesOwnTheirStorageAcrossBlockTails(int length)
    {
        var input = Enumerable.Repeat((byte)'a', length).ToArray();
        var cursor = new PythonReScalarMatchCursor(input, Program, 0, Timeout.InfiniteTimeSpan,
            ".", default, new(1000000, 128, 8 << 20, 8 << 20));
        byte[] result;
        try
        {
            result = cursor.GetBytes(0, input.Length);
            Assert.Equal(input, result);
            if (length != 0) Assert.NotSame(input, result);
        }
        finally { cursor.Dispose(); }
        input.AsSpan().Fill((byte)'z');
        Assert.All(result, value => Assert.Equal((byte)'a', value));
        Assert.Equal(0UL, cursor.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EntryInterruptionPrecedesResultDenialAndAcquisition(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var cursor = new PythonReScalarMatchCursor("é"u8, Program, 0, timeout,
            ".", cancellation.Token, new(1000000, 128, 8 << 20, 1));
        try
        {
            var baseline = cursor.ReservedBytes;
            if (expire) ticks = 50; else cancellation.Cancel();
            Exception? failure = null;
            try { _ = cursor.GetBytes(0, 2); }
            catch (Exception error) { failure = error; }
            if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(baseline, cursor.ReservedBytes);
            Assert.Equal(0UL, cursor.ResultReservation);
        }
        finally { cursor.Dispose(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InFlightCopyInterruptionKeepsOriginalAuthorityAndParentLoan(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var input = Encoding.UTF8.GetBytes(new string('é', 150000));
        var cursor = new PythonReScalarMatchCursor(input, Program, 0, timeout,
            ".", cancellation.Token, new(1000000, 128, 8 << 20, 8 << 20));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        var observer = cursor.WorkingInvocation;
        var baseline = cursor.ReservedBytes;
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = _ =>
        {
            if (observer.Ledger.ReservedBytes <= baseline) return;
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        };
        byte[]? result = null;
        Exception? failure = null;
        try { result = cursor.GetBytes(0, input.Length); }
        catch (Exception error) { failure = error; }
        finally { cursor.Dispose(); }
        Assert.True(entered);
        Assert.Null(result);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(13UL, observer.Ledger.ReservedBytes);
        observer.Ledger.Release(13);
    }
}
