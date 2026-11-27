using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSubstitutionOutputTests
{
    private static ulong Bytes(int count) => count == 0 ? 0 : 32UL + (((ulong)count + 15) & ~15UL);

    [Fact]
    public void TemplateExpansionBorrowsByteCapturesWithoutMappingOrProjectionArrays()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var plan = PythonReReplacementParser.Parse(@"<\1>"u8, 1, new Dictionary<string, int>());
        var input = "😀"u8.ToArray();
        var cursor = new PythonReScalarMatchCursor(input, program, 0,
            Regex.InfiniteMatchTimeout,
            null, "(.)", new(new(0, 1UL << 20), Regex.InfiniteMatchTimeout, CancellationToken.None));
        var writer = cursor.CreateOutputWriter();
        try
        {
            Assert.True(cursor.MoveNext());
            var baseline = cursor.ReservedBytes;
            plan.AppendToUtf8(input, ref cursor, ref writer);
            Assert.Equal("<😀>"u8.ToArray(), writer.WrittenBytes.ToArray());
            Assert.Equal(baseline, cursor.ReservedBytes);
            Assert.Equal(0UL, cursor.ResultReservation);
        }
        finally { cursor.ReleaseOutput(ref writer); cursor.Dispose(); }
        Assert.Equal(0UL, cursor.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarFinalByteRootAdmitsTheWriterOverlapBeforeAllocation(bool allow)
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var slotBytes = Utf8ConstructionBudget.ArrayBytes<int>(2 * program.GroupCount);
        var cursor = new PythonReScalarMatchCursor([], program, 0, Regex.InfiniteMatchTimeout,
            null, "(.)", new(new(0, 13 + slotBytes + 2 * Bytes(8193) - (allow ? 0UL : 1UL)), Regex.InfiniteMatchTimeout, CancellationToken.None));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        var writer = cursor.CreateOutputWriter();
        var payload = Enumerable.Repeat((byte)'x', 8193).ToArray();
        try
        {
            cursor.AppendBytes(ref writer, payload);
            byte[] result = [];
            Exception? failure = null;
            try { result = cursor.CompleteOutputBytes(ref writer); }
            catch (InvalidOperationException error) { failure = error; }
            if (allow)
            {
                Assert.Null(failure); Assert.Equal(payload, result);
                Assert.Equal(0, writer.Length);
                Assert.Equal(Bytes(8193), cursor.ResultReservation);
                Assert.Equal(13 + slotBytes + 2 * Bytes(8193), cursor.WorkingInvocation.Budget.PeakWorkingBytes);
            }
            else
            {
                Assert.NotNull(failure); Assert.Empty(result);
                Assert.Equal(payload, writer.WrittenBytes.ToArray());
                Assert.Equal(0UL, cursor.ResultReservation);
            }
            Assert.Equal(13 + slotBytes + Bytes(8193), cursor.ReservedBytes);
        }
        finally { cursor.ReleaseOutput(ref writer); cursor.Dispose(); }
        Assert.Equal(13UL, cursor.ReservedBytes);
        cursor.WorkingInvocation.Ledger.Release(13);
    }

    [Fact]
    public void NativeLiteralByteOutputKeepsItsLoanUntilPublication()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("foo");
        var invocation = new Utf8SliceInvocation(new(0, 13 + 48 + 64), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(invocation.Ledger.TryReserve(13));
        Utf8InvocationResultLedger results = default;
        try
        {
            var bytes = core.ReplaceExactLiteralForResult("foo fooé"u8, "😀"u8, int.MaxValue, 0, ref invocation, ref results, out var count);
            Assert.NotNull(bytes);
            Assert.Equal(2, count); Assert.Equal("😀 😀é"u8.ToArray(), bytes);
            Assert.Equal(48UL, results.ReservedBytes); Assert.Equal(61UL, invocation.Ledger.ReservedBytes);

        }
        finally { results.Release(ref invocation); }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void NativeLiteralMissDefersOutputAcquisitionToItsPublicShape()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("cat");
        var input = "caté😀 dog"u8;
        var invocation = new Utf8SliceInvocation(new(0, 128), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(invocation.Ledger.TryReserve(13));
        Utf8InvocationResultLedger results = default;
        try
        {
            var bytes = core.ReplaceExactLiteralForResult(input, "😀é"u8, int.MaxValue, "caté😀"u8.Length,
                ref invocation, ref results, out var count);
            Assert.Null(bytes);
            Assert.Equal(0, count);
            Assert.Equal(0UL, results.ReservedBytes);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        finally { results.Release(ref invocation); }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Theory]
    [InlineData("dog fox", "tiger", 0, "dog fox", 0)]
    [InlineData("é😀 dog", "😀é", 0, "é😀 dog", 0)]
    [InlineData("é😀 cat 中 cat", "😀é", 0, "é😀 😀é 中 😀é", 2)]
    [InlineData("catcaté", "", 0, "é", 2)]
    [InlineData("catcaté", "😀", 1, "😀caté", 1)]
    public void LiteralByteOutputsKeepUnicodeCountsAndOwnedCopies(
        string text, string replacement, int limit, string expected, int replacements)
    {
        var regex = new Utf8PythonRegex("cat");
        var input = Encoding.UTF8.GetBytes(text);
        var bytes = regex.Subn(input, Encoding.UTF8.GetBytes(replacement), limit);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), bytes.ResultBytes);
        Assert.Equal(replacements, bytes.ReplacementCount);
        input.AsSpan().Fill((byte)'x');
        Assert.Equal(Encoding.UTF8.GetBytes(expected), bytes.ResultBytes);
    }

    [Theory]
    [InlineData(2045)]
    [InlineData(2046)]
    public void FundedLiteralOutputReplaysPastEightOffsetsAcrossSearchBlocks(int gapLength)
    {
        using var regex = new Utf8PythonRegex("cat", policy: new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 4096, MaxResultBytes = 4096 }
        });
        using var source = new CancellationTokenSource();
        var gap = new string('x', gapLength);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("cat", 8)) + gap + "cat😀cat");
        var expected = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("tiger", 8)) + gap + "tiger😀cat");

        var result = regex.Subn(input, "tiger"u8, 9, cancellationToken: source.Token);
        Assert.Equal(9, result.ReplacementCount);
        Assert.Equal(expected, result.ResultBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralMissChecksInterruptionBeforeAdmittingTheOutput(bool expires)
    {
        var input = Encoding.ASCII.GetBytes(new string('x', 192 * 2048));
        using var source = new CancellationTokenSource();
        var ticks = 0L;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var regex = new Utf8PythonRegex("cat", policy: new()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new() { MaxResultBytes = 1 }
        });
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (expires) ticks = 25;
            else source.Cancel();
        });
        try
        {
            if (expires)
                Assert.Throws<RegexMatchTimeoutException>(() => regex.Subn(input, "tiger"u8, 1, cancellationToken: source.Token));
            else
                Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
                    regex.Subn(input, "tiger"u8, 1, cancellationToken: source.Token)).CancellationToken);
        }
        finally { diagnostics.Complete(); }
    }
}
