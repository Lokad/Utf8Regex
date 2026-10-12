using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReOneShotTextTests
{
    private static ulong TextBytes(int count) => count == 0 ? 0 : 32UL + ((2UL * ((ulong)count + 1) + 15) & ~15UL);

    private static bool HasMap(ref PythonReScalarMatchCursor cursor) => cursor.HasCoordinateMap;

    [Theory]
    [InlineData(0, "😀", "é😀z", 0, "😀")]
    [InlineData(1, "😀", "é😀z", 0, null)]
    [InlineData(1, "😀", "😀z", 0, "😀")]
    [InlineData(0, ".", "é😀z", 2, "😀")]
    [InlineData(1, ".", "é😀z", 2, "😀")]
    [InlineData(2, ".", "é😀", 2, "😀")]
    [InlineData(2, ".", "é😀z", 2, null)]
    [InlineData(0, "", "😀", 0, "")]
    [InlineData(1, "", "😀", 0, "")]
    [InlineData(2, "", "😀", 0, null)]
    [InlineData(1, "ab[0-9]+", "ab123😀", 0, "ab123")]
    [InlineData(1, "ab[0-9]+", "ab😀", 0, null)]
    public void StringEntryPointsPreserveNativeAndScalarResults(int kind, string pattern, string subject,
        int start, string? expected)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(expected, Run(regex, input, start, kind, CancellationToken.None));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, Run(regex, input, start, kind, token.Token));
    }

    private static string? Run(Utf8PythonRegex regex, byte[] input, int start, int kind, CancellationToken token)
        => kind switch
        {
            0 => regex.SearchToString(input, start, token),
            1 => regex.MatchToString(input, start, token),
            _ => regex.FullMatchToString(input, start, token),
        };

    [Fact]
    public void NativeDotAllStringKeepsTheCompleteSuffixAndDoesNotPrepareAFullVm()
    {
        var regex = new Utf8PythonRegex("start.*end", PythonReCompileOptions.DotAll);
        var input = "πstart\n東京😀end"u8.ToArray();
        Assert.True(regex.DebugUsesAsciiDotAllFullMatchFastPath);
        Assert.Equal("start\n東京😀end", regex.FullMatchToString(input, 2));
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        Assert.Null(regex.FullMatchToString("startendX"u8));
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
        var cursor = new PythonReScalarMatchCursor(input, program, [0, 1], 0, Regex.InfiniteMatchTimeout,
            "(.)", CancellationToken.None, new(0, 0, 13 + 96 + extent - (allow ? 0UL : 1UL)));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        var priorPeak = cursor.WorkingInvocation.Budget.PeakWorkingBytes;
        string? result = null;
        try
        {
            Exception? failure = null;
            try { result = cursor.GetText(0, input.Length); }
            catch (InvalidOperationException error) { failure = error; }
            Assert.False(HasMap(ref cursor));
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
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void NativeStringLoanOverlapsItsParentUntilPublication(int kind, bool allow)
    {
        var regex = kind switch
        {
            1 => new Utf8PythonRegex("ab[0-9]+"),
            2 => new Utf8PythonRegex("start.*end", PythonReCompileOptions.DotAll),
            _ => new Utf8PythonRegex("😀"),
        };
        var expected = kind switch { 1 => "ab" + new string('7', 5000), 2 => "start😀end", _ => "😀" };
        var input = Encoding.UTF8.GetBytes(kind == 2 ? "π" + expected : expected + "x");
        var extent = TextBytes(expected.Length);
        var invocation = new Utf8SliceInvocation(new(0, 0, 13 + extent - (allow ? 0UL : 1UL)), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        Utf8InvocationResultLedger results = default;
        string? result = null;
        try
        {
            Exception? failure = null;
            try { result = regex.MaterializeNativeMatchText(input, kind == 2 ? 2 : 0, kind, ref invocation, ref results); }
            catch (InvalidOperationException error) { failure = error; }
            if (allow)
            {
                Assert.Null(failure); Assert.Equal(expected, result);
                Assert.Equal(extent, results.ReservedBytes);
                Assert.Equal(13 + extent, invocation.Ledger.ReservedBytes);
                Assert.Equal(13 + extent, invocation.Budget.PeakWorkingBytes);
            }
            else
            {
                Assert.NotNull(failure); Assert.Null(result);
                Assert.Equal(0UL, results.ReservedBytes);
                Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
                Assert.Equal(13 + extent, invocation.Budget.PeakWorkingBytes);
                Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.Budget.LimitBreach);
            }
        }
        finally { results.Release(ref invocation); }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
        if (allow) Assert.Equal(expected, result);
    }

    [Fact]
    public void EveryNativeStringWorkCutoffLeavesPublicationAtomicAndReturnsOnlyOwnedLoans()
    {
        var regex = new Utf8PythonRegex("é😀");
        var input = "xé😀z"u8.ToArray();
        const ulong seed = 7;
        var measured = new Utf8SliceInvocation(new(100000, 0, 1UL << 20), Regex.InfiniteMatchTimeout, default);
        for (var index = 0; index < (int)seed; index++) Assert.Equal(Utf8ExecutionLimitKind.None, measured.TryChargeInputStep());
        Utf8InvocationResultLedger measurement = default;
        Assert.Equal("é😀", regex.MaterializeNativeMatchText(input, 0, 0, ref measured, ref measurement));
        var required = measured.Budget.Steps - seed;
        measurement.Release(ref measured);
        for (ulong cutoff = 0; cutoff <= required; cutoff++)
        {
            var invocation = new Utf8SliceInvocation(new(seed + cutoff, 0, 1UL << 20), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            for (var index = 0; index < (int)seed; index++) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
            Utf8InvocationResultLedger results = default;
            string? result = null;
            try
            {
                Exception? failure = null;
                try { result = regex.MaterializeNativeMatchText(input, 0, 0, ref invocation, ref results); }
                catch (InvalidOperationException error) { failure = error; }
                if (cutoff == required) { Assert.Null(failure); Assert.Equal("é😀", result); Assert.Equal(48UL, results.ReservedBytes); }
                else { Assert.NotNull(failure); Assert.Null(result); }
                Assert.Equal(13 + results.ReservedBytes, invocation.Ledger.ReservedBytes);
                Assert.True(results.ReservedBytes is 0 or 48);
            }
            finally { results.Release(ref invocation); }
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(13);
        }
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
        var cursor = new PythonReScalarMatchCursor(input, program, [0, 1], 0, timeout,
            "(.+)", token.Token, new(0, 0, 1UL << 26));
        try
        {
            Assert.True(cursor.MoveNext());
            Assert.False(HasMap(ref cursor));
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
            try { result = cursor.GetText(cursor.StartOffsetInBytes, cursor.EndOffsetInBytes); }
            catch (Exception error) { failure = error; }
            Assert.True(entered); Assert.Null(result); Assert.False(HasMap(ref cursor));
            if (expire)
            {
                var expired = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal("(.+)", expired.Pattern); Assert.Equal(timeout, expired.MatchTimeout);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(baseline, cursor.ReservedBytes); Assert.Equal(0UL, cursor.ResultReservation);
        }
        finally { cursor.Dispose(); }
        Assert.Equal(13UL, cursor.ReservedBytes);
        cursor.WorkingInvocation.Ledger.Release(13);
    }
}
