using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReScalarTextAdmissionTests
{
    private static ulong TextBytes(int count) => count == 0 ? 0 : 32UL + ((2UL * ((ulong)count + 1) + 15) & ~15UL);

    [Fact]
    public void DotAllByteValueKeepsTheCompleteSuffix()
    {
        var regex = new Utf8PythonRegex("start.*end", PythonReCompileOptions.DotAll);
        var input = "πstart\n東京😀end"u8.ToArray();
        PythonReMatchAssert.Equal("start\n東京😀end", regex.FullMatch(input, 2));
        Assert.False(regex.FullMatch("startendX"u8).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarTextAdmitsItsCompleteStringWithoutAWholeSubjectMap(bool allow)
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var text = string.Concat(Enumerable.Repeat("😀", 5000));
        var input = Encoding.UTF8.GetBytes(text);
        var extent = TextBytes(text.Length);
        var cursor = new PythonReScalarMatchState(input,program,0,Regex.InfiniteMatchTimeout,"(.)",new(new(0, 0, 13 + 96 + extent - (allow ? 0UL : 1UL)), Regex.InfiniteMatchTimeout, CancellationToken.None),            null);
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        var priorPeak = cursor.WorkingInvocation.Budget.PeakWorkingBytes;
        string? result = null;
        try
        {
            Exception? failure = null;
            try { result = cursor.GetText(input,0, input.Length); }
            catch (InvalidOperationException error) { failure = error; }
            if (allow)
            {
                Assert.Null(failure); Assert.Equal(text, result);
                Assert.Equal(extent, cursor.ResultReservation);
                Assert.Equal(13 + 96 + extent, cursor.ReservedBytes);
                Assert.Equal(13 + 96 + extent, cursor.WorkingInvocation.Budget.PeakWorkingBytes);
            }
            else
            {
                Assert.NotNull(failure); Assert.Null(result);
                Assert.Equal(0UL, cursor.ResultReservation);
                Assert.Equal(109UL, cursor.ReservedBytes);
                Assert.True(cursor.WorkingInvocation.Budget.PeakWorkingBytes >= priorPeak);
                Assert.Equal(13 + 96 + extent, cursor.WorkingInvocation.Budget.PeakWorkingBytes);
                Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, cursor.WorkingInvocation.Budget.LimitBreach);
            }
        }
        finally { cursor.Dispose(); }
        Assert.Equal(13UL, cursor.ReservedBytes);
        cursor.WorkingInvocation.Ledger.Release(13);
        if (allow) Assert.Equal(text, result);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarDecodingObservesFailureAfterTheStringLoanIsAdmitted(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.+)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", 150000)));
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var cursor = new PythonReScalarMatchState(input,program,0,timeout,"(.+)",new(new(0, 0, 1UL << 26), timeout, token.Token),            null);
        try
        {
            Assert.True(cursor.MoveNext(input));
            Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
            var baseline = cursor.ReservedBytes;
            cursor.WorkingInvocation.ShareStorageForCursor();
            var observer = cursor.WorkingInvocation;
            var entered = false;
            cursor.WorkingInvocation.CheckpointHook = _ =>
            {
                if (observer.Ledger.ReservedBytes == baseline + TextBytes(300000))
                {
                    entered = true;
                    if (expire) ticks = 50; else token.Cancel();
                }
            };
            string? result = null;
            Exception? failure = null;
            try { result = cursor.GetText(input,cursor.StartOffsetInBytes, cursor.EndOffsetInBytes); }
            catch (Exception error) { failure = error; }
            Assert.True(entered); Assert.Null(result);
            if (expire)
            {
                Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(baseline, cursor.ReservedBytes); Assert.Equal(0UL, cursor.ResultReservation);
        }
        finally { cursor.Dispose(); }
        Assert.Equal(13UL, cursor.ReservedBytes);
        cursor.WorkingInvocation.Ledger.Release(13);
    }
}
