using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReOneShotProjectionTests
{
    private static ulong ArrayBytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);

    private static bool HasMap(ref PythonReScalarMatchCursor cursor) => cursor.HasCoordinateMap;

    [Theory]
    [InlineData(0, "(?P<x>.)", "é😀z", 2, 2, 6, 1, 3, "😀")]
    [InlineData(1, "(?P<x>.)", "é😀z", 2, 2, 6, 1, 3, "😀")]
    [InlineData(2, "(?P<x>.)", "é😀", 2, 2, 6, 1, 3, "😀")]
    [InlineData(0, "(?<=é).(?=z)", "é😀z", 0, 2, 6, 1, 3, "😀")]
    [InlineData(0, @"(.)\1", "é😀😀z", 0, 2, 10, 1, 5, "😀😀")]
    [InlineData(1, "(?P<x>.)?", "", 0, 0, 0, 0, 0, "")]
    [InlineData(2, "(?P<x>.)?", "", 0, 0, 0, 0, 0, "")]
    [InlineData(0, "😀", "é😀", 0, 2, 6, 1, 3, "😀")]
    [InlineData(1, "ab[0-9]+", "ab12😀", 0, 0, 4, 0, 4, "ab12")]
    public void ValueAndContextEntryPointsKeepScalarByteRangesAndDerivedCoordinates(int kind, string pattern,
        string subject, int start, int startBytes, int endBytes, int startUtf16, int endUtf16, string text)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        using var token = new CancellationTokenSource();
        foreach (var cancellation in new[] { CancellationToken.None, token.Token })
        {
            var value = Value(regex, input, start, kind, cancellation);
            Assert.True(value.Success);
            Assert.Equal((startBytes, endBytes), (value.StartOffsetInBytes, value.EndOffsetInBytes));
            Assert.Equal((startUtf16, endUtf16), (value.StartOffsetInUtf16, value.EndOffsetInUtf16));
            Assert.Equal(Encoding.UTF8.GetBytes(text), value.GetValueBytes().ToArray());
            var context = Context(regex, input, start, kind, cancellation);
            Assert.True(context.Success);
            Assert.Equal((startBytes, endBytes), (context.Value.StartOffsetInBytes, context.Value.EndOffsetInBytes));
            Assert.Equal((startUtf16, endUtf16), (context.Value.StartOffsetInUtf16, context.Value.EndOffsetInUtf16));
            Assert.Equal(text, context.GetValueString());
            if (regex.IsMatch(input, start, cancellation)) Assert.True(regex.Search(input, start, cancellation).Success);
        }
    }

    private static Utf8PythonValueMatch Value(Utf8PythonRegex regex, ReadOnlySpan<byte> input, int start, int kind, CancellationToken token)
        => kind switch { 0 => regex.Search(input, start, token), 1 => regex.Match(input, start, token), _ => regex.FullMatch(input, start, token) };

    private static Utf8PythonMatchContext Context(Utf8PythonRegex regex, ReadOnlySpan<byte> input, int start, int kind, CancellationToken token)
        => kind switch { 0 => regex.SearchDetailed(input, start, token), 1 => regex.MatchDetailed(input, start, token), _ => regex.FullMatchDetailed(input, start, token) };

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void MissingContextsHaveNoOwnedChildren(int kind)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀)");
        var context = Context(regex, "é"u8, 0, kind, CancellationToken.None);
        Assert.False(context.Success); Assert.Equal(0, context.CaptureSlotCount);
        Assert.Empty(context.GetNameEntries()); Assert.False(Value(regex, "é"u8, 0, kind, CancellationToken.None).Success);
        Assert.False(regex.IsMatch("é"u8));
    }

    [Fact]
    public void ScalarValueProjectionRetainsNoCoordinateOrGroupRoot()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(?<=é)(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var cursor = new PythonReScalarMatchCursor("é😀z"u8, program, [0, 1], 0,
            Regex.InfiniteMatchTimeout, "(?<=é)(.)", CancellationToken.None, new(0, 0, 1UL << 20));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        PythonReGroupData value;
        try
        {
            Assert.True(cursor.MoveNext());
            var baseline = cursor.ReservedBytes;
            value = cursor.SnapshotValueData();
            Assert.Equal((2, 6), (value.StartOffsetInBytes, value.EndOffsetInBytes));
            Assert.Equal((1, 3), (value.StartOffsetInUtf16, value.EndOffsetInUtf16));
            Assert.False(HasMap(ref cursor)); Assert.Equal(0UL, cursor.ResultReservation);
            Assert.Equal(baseline, cursor.ReservedBytes);
        }
        finally { cursor.Dispose(); }
        Assert.Equal(13UL, cursor.ReservedBytes); cursor.WorkingInvocation.Ledger.Release(13);
        Assert.True(value.Success); Assert.Equal(6, value.EndOffsetInBytes);
    }

    [Fact]
    public void BooleanAndValueCallsAvoidSubjectSizedProjectionAllocations()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀)");
        var input = Encoding.UTF8.GetBytes(new string('a', 1 << 20) + "😀");
        using var token = new CancellationTokenSource();
        for (var index = 0; index < 3; index++)
        {
            Assert.True(regex.IsMatch(input, 0, token.Token));
            Assert.True(regex.Search(input, 0, token.Token).Success);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var found = regex.IsMatch(input, 0, token.Token);
        var value = regex.Search(input, 0, token.Token);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(found); Assert.True(value.Success); Assert.Equal(1 << 20, value.StartOffsetInBytes);
        Assert.True(allocated < 65536, $"Unexpected subject-sized projection: {allocated} bytes.");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ContextNamesAreAdmittedWithTheGroupRootAndTransferredWithoutAnotherClone(bool allow)
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var names = Enumerable.Repeat(new PythonReNameEntry { Name = "word", Number = 1 }, 5000).ToArray();
        var extent = ArrayBytes<PythonReNameEntry>(names.Length);
        var groupExtent = ArrayBytes<PythonReGroupData>(2);
        var scratch = ArrayBytes<int>(program.GroupCount) + (ulong)Utf8SliceRunner.TrailReserveBytes +
            ArrayBytes<Utf8SliceTrailEntry[]>(Utf8SliceRunner.ChunkTableCapacity);
        var total = 13 + 96 + 96 + scratch + groupExtent + extent;
        var cursor = new PythonReScalarMatchCursor("😀é"u8, program, [0, 1], 0, Regex.InfiniteMatchTimeout,
            "(.)", CancellationToken.None, new(0, 0, total - (allow ? 0UL : 1UL)));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        Utf8PythonMatchContext result = default;
        try
        {
            Assert.True(cursor.MoveNext());
            var groups = cursor.SnapshotGroups();
            Assert.Equal(groupExtent, cursor.ResultReservation);
            var baseline = cursor.ReservedBytes;
            var priorPeak = cursor.WorkingInvocation.Budget.PeakWorkingBytes;
            PythonReNameEntry[]? copied = null;
            Exception? failure = null;
            try { copied = cursor.SnapshotNameEntries(names); }
            catch (InvalidOperationException error) { failure = error; }
            if (allow)
            {
                Assert.Null(failure); Assert.NotNull(copied);
                result = new Utf8PythonMatchContext("😀é"u8, groups, copied, cursor.LastIndex);
                cursor.Checkpoint();
                Assert.Same(copied, result.GetNameEntries()); Assert.NotSame(names, copied);
                Assert.Equal(groupExtent + extent, cursor.ResultReservation);
                Assert.Equal(total, cursor.ReservedBytes); Assert.Equal(total, cursor.WorkingInvocation.Budget.PeakWorkingBytes);
                result.GetNameEntries()[0] = new PythonReNameEntry { Name = "changed", Number = 0 };
                Assert.Equal("word", names[0].Name);
                Assert.Equal("😀", result.GetValueString());
            }
            else
            {
                Assert.NotNull(failure); Assert.Null(copied); Assert.False(result.Success);
                Assert.Equal(groupExtent, cursor.ResultReservation); Assert.Equal(baseline, cursor.ReservedBytes);
                Assert.True(cursor.WorkingInvocation.Budget.PeakWorkingBytes >= priorPeak);
                Assert.Equal(baseline + extent, cursor.WorkingInvocation.Budget.PeakWorkingBytes);
                Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, cursor.WorkingInvocation.Budget.LimitBreach);
            }
        }
        finally { cursor.Dispose(); }
        Assert.Equal(13UL, cursor.ReservedBytes); cursor.WorkingInvocation.Ledger.Release(13);
        if (allow) { Assert.Equal("😀", result.GetValueString()); Assert.Equal("changed", result.GetNameEntries()[0].Name); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NameCopyFailureAfterAdmissionIsUnpublishedAndPreservesProfileIdentity(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var names = Enumerable.Repeat(new PythonReNameEntry { Name = "word", Number = 1 }, 16385).ToArray();
        var extent = ArrayBytes<PythonReNameEntry>(names.Length);
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var cursor = new PythonReScalarMatchCursor("😀"u8, program, [0, 1], 0, timeout,
            "(.)", token.Token, new(0, 0, 1UL << 24));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        try
        {
            Assert.True(cursor.MoveNext()); _ = cursor.SnapshotGroups();
            var baseline = cursor.ReservedBytes;
            var priorResults = cursor.ResultReservation;
            cursor.WorkingInvocation.ShareStorageForCursor();
            var observer = cursor.WorkingInvocation;
            while ((cursor.WorkingInvocation.Budget.Steps & 63) != 0)
                Assert.Equal(Utf8ExecutionLimitKind.None, cursor.WorkingInvocation.TryChargeInputStep());
            var entered = false;
            cursor.WorkingInvocation.CheckpointHook = _ =>
            {
                if (observer.Ledger.ReservedBytes == baseline + extent)
                {
                    entered = true;
                    if (expire) ticks = 50; else token.Cancel();
                }
            };
            PythonReNameEntry[]? result = null;
            Exception? failure = null;
            try { result = cursor.SnapshotNameEntries(names); }
            catch (Exception error) { failure = error; }
            Assert.True(entered); Assert.Null(result);
            if (expire)
            {
                var expired = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal("(.)", expired.Pattern); Assert.Equal(timeout, expired.MatchTimeout);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(baseline + extent, cursor.ReservedBytes);
            Assert.Equal(priorResults + extent, cursor.ResultReservation);
            Assert.All(names, entry => Assert.Equal("word", entry.Name));
        }
        finally { cursor.Dispose(); }
        Assert.Equal(13UL, cursor.ReservedBytes); cursor.WorkingInvocation.Ledger.Release(13);
    }
}
