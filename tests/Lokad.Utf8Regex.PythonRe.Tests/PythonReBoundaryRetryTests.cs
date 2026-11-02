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
        var program = PythonReScalarCompiler.Compile(new PythonReParser(@"\b").Parse(options));
        var input = "éA😀B _0"u8.ToArray();
        var expected = options == PythonReCompileOptions.Ascii
            ? new[] { 2, 3, 7, 8, 9, 11 } : new[] { 0, 3, 7, 8, 9, 11 };
        var invocation = new Utf8SliceInvocation(new(1_000_000, 1024, 1UL << 20),
            Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var cursor = new Utf8PythonMatchCursor(input, program, [], 0,
            Regex.InfiniteMatchTimeout, @"\b", invocation, null);
        var alias = cursor;
        var snapshots = new List<Utf8PythonDetailedMatchData>();
        try
        {
            foreach (var offset in expected)
            {
                Assert.True(alias.MoveNext());
                Assert.Equal(offset, cursor.Current.Value.StartOffsetInBytes);
                Assert.Equal(offset, cursor.Current.Value.EndOffsetInBytes);
                snapshots.Add(cursor.Current);
            }
            Assert.False(cursor.MoveNext());
            Assert.False(alias.MoveNext());
        }
        finally { cursor.Dispose(); alias.Dispose(); }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        for (var i = 0; i < snapshots.Count; i++)
        {
            Assert.Equal(expected[i], snapshots[i].Value.StartOffsetInBytes);
            Assert.Equal(string.Empty, snapshots[i].Value.ValueText);
        }
        invocation.Ledger.Release(13);
    }

    [Fact]
    public void EmptyThenConsumingAlternativesStillRetryTheSameScalar()
    {
        var regex = new Utf8PythonRegex(@"(?a:\b)|a");
        Assert.Equal(new[] { "", "a", "a", "" }, regex.FindAllToStrings("aa"u8).ScalarValues);
        Assert.Equal(4, regex.Count("aa"u8));
        Assert.Equal(new string?[] { "", "", "", "", "" }, regex.SplitToStrings("aa"u8));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalEmptyRetryKeepsOriginalInterruptionAndParentLoan(bool expire)
    {
        var program = PythonReScalarCompiler.Compile(new PythonReParser(@"\b").Parse(PythonReCompileOptions.Ascii));
        using var source = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var armed = false;
        var observed = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (!armed) return;
            observed = true;
            if (expire) ticks = 50; else source.Cancel();
        });
        try
        {
            var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
            var invocation = new Utf8SliceInvocation(new(1_000_000, 1024, 1UL << 20), timeout, source.Token);
            Assert.True(invocation.Ledger.TryReserve(13));
            var cursor = new Utf8PythonMatchCursor("a"u8.ToArray(), program, [], 0, timeout, @"\b", invocation, null);
            try
            {
                Assert.True(cursor.MoveNext());
                Assert.True(cursor.MoveNext());
                Assert.Equal(1, cursor.Current.Value.StartOffsetInBytes);
                while ((invocation.Budget.Steps & 63) != 63) invocation.TryChargeStep();
                armed = true;
                if (expire)
                    Assert.Equal(timeout, Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext()).MatchTimeout);
                else
                    Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext()).CancellationToken);
                Assert.True(observed);
            }
            finally { cursor.Dispose(); }
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(13);
        }
        finally { session.Complete(); }
    }
}
