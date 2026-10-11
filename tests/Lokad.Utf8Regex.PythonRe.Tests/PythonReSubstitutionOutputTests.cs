using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSubstitutionOutputTests
{
    private static ulong Bytes(int count) => count == 0 ? 0 : 32UL + (((ulong)count + 15) & ~15UL);
    private static ulong TextBytes(int count) => count == 0 ? 0 : 32UL + ((2UL * ((ulong)count + 1) + 15) & ~15UL);

    private static bool HasMap(ref PythonReScalarMatchCursor cursor) => cursor.HasCoordinateMap;

    [Fact]
    public void TemplateExpansionBorrowsByteCapturesWithoutMappingOrProjectionArrays()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var plan = PythonReReplacementParser.Parse(@"<\1>", 1, new Dictionary<string, int>());
        var input = "😀"u8.ToArray();
        var cursor = new PythonReScalarMatchCursor(input, program, [0, 1], 0,
            Regex.InfiniteMatchTimeout, "(.)", CancellationToken.None, new(0, 0, 1UL << 20));
        var writer = cursor.CreateOutputWriter();
        try
        {
            Assert.True(cursor.MoveNext());
            Assert.False(HasMap(ref cursor));
            var baseline = cursor.ReservedBytes;
            plan.AppendToUtf8(input, ref cursor, ref writer);
            Assert.Equal("<😀>"u8.ToArray(), writer.WrittenBytes.ToArray());
            Assert.False(HasMap(ref cursor));
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
        var cursor = new PythonReScalarMatchCursor([], program, [0, 1], 0, Regex.InfiniteMatchTimeout,
            "(.)", CancellationToken.None, new(0, 0, 13 + 96 + 2 * Bytes(8193) - (allow ? 0UL : 1UL)));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        var writer = cursor.CreateOutputWriter();
        var payload = Enumerable.Repeat((byte)'x', 8193).ToArray();
        try
        {
            cursor.AppendBytes(ref writer, payload);
            byte[] result = [];
            Exception? failure = null;
            try { result = cursor.CompleteOutputBytes(ref writer, false); }
            catch (InvalidOperationException error) { failure = error; }
            if (allow)
            {
                Assert.Null(failure); Assert.Equal(payload, result);
                Assert.Equal(0, writer.Length);
                Assert.Equal(Bytes(8193), cursor.ResultReservation);
                Assert.Equal(13 + 96 + 2 * Bytes(8193), cursor.WorkingInvocation.Budget.PeakWorkingBytes);
            }
            else
            {
                Assert.NotNull(failure); Assert.Empty(result);
                Assert.Equal(payload, writer.WrittenBytes.ToArray());
                Assert.Equal(0UL, cursor.ResultReservation);
            }
            Assert.Equal(13 + 96 + Bytes(8193), cursor.ReservedBytes);
        }
        finally { cursor.ReleaseOutput(ref writer); cursor.Dispose(); }
        Assert.Equal(13UL, cursor.ReservedBytes);
        cursor.WorkingInvocation.Ledger.Release(13);
    }

    [Fact]
    public void EveryFinalByteWorkCutoffPreservesUnpublishedStorageAndParentCapacity()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var payload = new byte[8193];
        ulong seed;
        {
            var measured = new PythonReScalarMatchCursor([], program, [0, 1], 0,
                Regex.InfiniteMatchTimeout, "(.)", CancellationToken.None, new(100000, 0, 1UL << 20));
            var writer = measured.CreateOutputWriter();
            measured.AppendBytes(ref writer, payload);
            seed = measured.WorkingInvocation.Budget.Steps;
            measured.ReleaseOutput(ref writer); measured.Dispose();
        }
        for (ulong cutoff = 0; cutoff <= 9; cutoff++)
        {
            var cursor = new PythonReScalarMatchCursor([], program, [0, 1], 0,
                Regex.InfiniteMatchTimeout, "(.)", CancellationToken.None, new(seed + cutoff, 0, 1UL << 20));
            Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
            var writer = cursor.CreateOutputWriter();
            try
            {
                cursor.AppendBytes(ref writer, payload);
                Assert.Equal(seed, cursor.WorkingInvocation.Budget.Steps);
                byte[] result = [];
                Exception? failure = null;
                try { result = cursor.CompleteOutputBytes(ref writer, false); }
                catch (InvalidOperationException error) { failure = error; }
                if (cutoff == 9) { Assert.Null(failure); Assert.Equal(payload, result); Assert.Equal(0, writer.Length); }
                else
                {
                    Assert.NotNull(failure); Assert.Empty(result);
                    Assert.Equal(8193, writer.Length); Assert.Equal(payload, writer.WrittenBytes.ToArray());
                }
                Assert.Equal(cutoff >= 3 ? Bytes(8193) : 0, cursor.ResultReservation);
                Assert.Equal(13 + 96 + Bytes(8193) + (cutoff >= 3 && cutoff < 9 ? Bytes(8193) : 0), cursor.ReservedBytes);
            }
            finally { cursor.ReleaseOutput(ref writer); cursor.Dispose(); }
            Assert.Equal(13UL, cursor.ReservedBytes);
            cursor.WorkingInvocation.Ledger.Release(13);
        }
    }

    [Fact]
    public void FinalTextOverlapsTheWriterAndTransfersOnlyItsStringLoan()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var text = string.Concat(Enumerable.Repeat("😀", 5000));
        var cursor = new PythonReScalarMatchCursor([], program, [0, 1], 0,
            Regex.InfiniteMatchTimeout, "(.)", CancellationToken.None, new(0, 0, 1UL << 20));
        var writer = cursor.CreateOutputWriter();
        string result;
        try
        {
            cursor.AppendText(ref writer, text);
            result = cursor.CompleteOutputText(ref writer);
            Assert.Equal(text, result);
            Assert.Equal(0, writer.Length);
            Assert.Equal(TextBytes(10000), cursor.ResultReservation);
            Assert.Equal(96 + TextBytes(10000), cursor.ReservedBytes);
            Assert.Equal(96 + Bytes(20000) + TextBytes(10000), cursor.WorkingInvocation.Budget.PeakWorkingBytes);
        }
        finally { cursor.ReleaseOutput(ref writer); cursor.Dispose(); }
        Assert.Equal(0UL, cursor.ReservedBytes);
        Assert.Equal(text, result);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalTextFailureRestoresTheWriterAndPreservesProfileIdentity(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var cursor = new PythonReScalarMatchCursor([], program, [0, 1], 0,
            timeout, "(.)", token.Token, new(0, 0, 1UL << 20));
        var writer = cursor.CreateOutputWriter();
        try
        {
            cursor.AppendText(ref writer, new string('é', 5000));
            var baseline = cursor.ReservedBytes;
            ref var invocation = ref cursor.WorkingInvocation;
            while ((invocation.Budget.Steps & 63) != 60) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
            var entered = false;
            invocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else token.Cancel(); };
            string? result = null;
            Exception? failure = null;
            try { result = cursor.CompleteOutputText(ref writer); }
            catch (Exception error) { failure = error; }
            Assert.True(entered); Assert.Null(result);
            if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(10000, writer.Length);
            Assert.Equal(baseline, cursor.ReservedBytes);
            Assert.Equal(0UL, cursor.ResultReservation);
        }
        finally { cursor.ReleaseOutput(ref writer); cursor.Dispose(); }
        Assert.Equal(0UL, cursor.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StringCallbackFragmentsStayIndependentAndPairsCrossEncodingBlocks(bool textOutput)
    {
        var regex = new Utf8PythonRegex("(.)");
        var input = "xy"u8.ToArray();
        var values = new[] { new string('a', 2047) + "😀" + new string((char)0xd83d, 1), new string((char)0xde00, 1) };
        Utf8PythonMatchEvaluator<string[]> callback = static (state, match) => state[match.Value.StartOffsetInBytes];
        var expected = new string('a', 2047) + "😀��";
        if (textOutput) Assert.Equal(expected, regex.SubnToString(input, values, callback).ResultText);
        else Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Subn(input, values, callback).ResultBytes);
    }

    [Fact]
    public void CompletedByteCallbackOutputRejectsMalformedBytesAndCanJoinFragments()
    {
        var regex = new Utf8PythonRegex("(.)");
        var input = "xy"u8.ToArray();
        Utf8PythonUtf8MatchEvaluator<int> malformed = static (_, match) => match.Value.StartOffsetInBytes == 0 ? "a"u8.ToArray() : [0xff];
        var error = Assert.Throws<ArgumentException>(() => regex.Subn(input, 0, malformed));
        Assert.Contains("byte offset 1", error.Message);
        Utf8PythonUtf8MatchEvaluator<int> fragments = static (_, match) => match.Value.StartOffsetInBytes == 0 ? [0xe2] : [0x82, 0xac];
        var result = regex.Subn(input, 0, fragments);
        Assert.Equal("€"u8.ToArray(), result.ResultBytes);
        Assert.Equal(2, result.ReplacementCount);
        Assert.Equal("xy", regex.SubnToString(input, @"\1").ResultText);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeLiteralOutputKeepsItsLoanThroughStringProjection(bool textOutput)
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("foo");
        var invocation = new Utf8SliceInvocation(new(0, 0, 13 + 96), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(invocation.Ledger.TryReserve(13));
        Utf8InvocationResultLedger results = default;
        var control = new Utf8InvocationBufferControl(ref invocation);
        try
        {
            var bytes = core.ReplaceExactLiteralForResult("foo fooé"u8, "😀"u8, int.MaxValue, ref invocation, ref results, out var count);
            Assert.Equal(2, count); Assert.Equal("😀 😀é"u8.ToArray(), bytes);
            Assert.Equal(48UL, results.ReservedBytes); Assert.Equal(61UL, invocation.Ledger.ReservedBytes);
            if (textOutput)
            {
                var text = results.DecodeUtf8(bytes, 6, ref invocation);
                Assert.Equal("😀 😀é", text);
                Assert.Equal(109UL, invocation.Ledger.ReservedBytes);
                results.ReleaseArray(bytes, ref invocation);
                Assert.Equal(61UL, invocation.Ledger.ReservedBytes);
            }
        }
        finally { results.Release(ref invocation); }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void CallbackCancellationPrecedesEncodingItsLargeReturnedString()
    {
        var regex = new Utf8PythonRegex("(.)");
        var input = "x"u8.ToArray();
        var text = new string('é', 1 << 20);
        Utf8PythonMatchEvaluator<(CancellationTokenSource Source, string Text)> evaluator = static (state, _) =>
        {
            state.Source.Cancel();
            return state.Text;
        };
        using (var warm = new CancellationTokenSource())
            Assert.Throws<OperationCanceledException>(() => regex.Subn(input, (warm, text), evaluator, 0, 0, warm.Token));
        using var source = new CancellationTokenSource();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Assert.Throws<OperationCanceledException>(() => regex.Subn(input, (source, text), evaluator, 0, 0, source.Token));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.True(allocated < 65536, $"Canceled output allocated {allocated} bytes before observing the callback token.");
    }
}
