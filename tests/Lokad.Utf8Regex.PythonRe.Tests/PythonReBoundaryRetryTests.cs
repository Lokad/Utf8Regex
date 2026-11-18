using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReBoundaryRetryTests
{
    [Theory]
    [InlineData(PythonReCompileOptions.Ascii)]
    [InlineData(PythonReCompileOptions.None)]
    public void BoundarySnapshotsAndAliasesKeepScalarPositionsAfterExhaustion(PythonReCompileOptions options)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var program = PythonReScalarCompiler.Compile(new PythonReParser(@"\b").Parse(options));
        var input = "éA😀B _0"u8.ToArray();
        var expected = options == PythonReCompileOptions.Ascii
            ? new[] { 2, 3, 7, 8, 9, 11 } : new[] { 0, 3, 7, 8, 9, 11 };
        var invocation = new Utf8SliceInvocation(new(1_000_000, 1024, 1UL << 20),
            Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var cursor = new Utf8PythonMatchCursor(input, program, 0,
            Regex.InfiniteMatchTimeout, @"\b", invocation, null);
        var alias = cursor;
        var snapshots = new List<Utf8PythonRangeMatch>();
        try
        {
            foreach (var offset in expected)
            {
                Assert.True(alias.MoveNext(out cursorRange));
                Assert.Equal(offset, cursorRange.Value.StartOffsetInBytes);
                Assert.Equal(offset, cursorRange.Value.EndOffsetInBytes);
                snapshots.Add(cursorRange);
            }
            Assert.False(cursor.MoveNext(out cursorRange));
            Assert.False(alias.MoveNext(out cursorRange));
        }
        finally
        {
            cursor.Dispose();
            alias.Dispose();
        }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        for (var i = 0; i < snapshots.Count; i++)
        {
            Assert.Equal(expected[i], snapshots[i].Value.StartOffsetInBytes);
            Assert.Equal(string.Empty, System.Text.Encoding.UTF8.GetString(snapshots[i].GetValueMemory().Span));
        }
        invocation.Ledger.Release(13);
    }

    [Fact]
    public void EmptyThenConsumingAlternativesStillRetryTheSameScalar()
    {
        var regex = new Utf8PythonRegex(@"(?a:\b)|a");
        ReadOnlyMemory<byte> snapshots1Input = ("aa"u8).ToArray();
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(new[] { "", "a", "a", "" }, snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(4, regex.Count("aa"u8));
        Assert.Equal(new string?[] { "", "", "", "", "" }, PythonReSplitAssert.Decode("aa"u8, regex.SplitRanges("aa"u8, 0, 0, default)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalEmptyRetryKeepsOriginalInterruptionAndParentLoan(bool expire)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var program = PythonReScalarCompiler.Compile(new PythonReParser(@"\b").Parse(PythonReCompileOptions.Ascii));
        using var source = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var armed = false;
        var observed = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (!armed)
            {
                return;
            }
            observed = true;
            if (expire)
            {
                ticks = 50;
            }
            else
            {
                source.Cancel();
            }
        });
        try
        {
            var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
            var invocation = new Utf8SliceInvocation(new(1_000_000, 1024, 1UL << 20), timeout, source.Token);
            Assert.True(invocation.Ledger.TryReserve(13));
            var cursor = new Utf8PythonMatchCursor("a"u8.ToArray(), program, 0, timeout, @"\b", invocation, null);
            try
            {
                Assert.True(cursor.MoveNext(out cursorRange));
                Assert.True(cursor.MoveNext(out cursorRange));
                Assert.Equal(1, cursorRange.Value.StartOffsetInBytes);
                while ((invocation.Budget.Steps & 63) != 63)
                    invocation.TryChargeStep();
                armed = true;
                if (expire)
                {
                    Assert.Equal(timeout, Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _)).MatchTimeout);
                }
                else
                {
                    Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _)).CancellationToken);
                }
                Assert.True(observed);
            }
            finally
            {
                cursor.Dispose();
            }
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(13);
        }
        finally
        {
            session.Complete();
        }
    }
}
