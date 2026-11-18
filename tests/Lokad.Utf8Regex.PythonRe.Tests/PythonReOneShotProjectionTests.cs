using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReOneShotProjectionTests
{
    private static ulong ArrayBytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);

    [Theory]
    [InlineData(0, "(?P<x>.)", "é😀z", 2, 2, 6, "😀")]
    [InlineData(1, "(?P<x>.)", "é😀z", 2, 2, 6, "😀")]
    [InlineData(2, "(?P<x>.)", "é😀", 2, 2, 6, "😀")]
    [InlineData(0, "(?<=é).(?=z)", "é😀z", 0, 2, 6, "😀")]
    [InlineData(0, @"(.)\1", "é😀😀z", 0, 2, 10, "😀😀")]
    [InlineData(1, "(?P<x>.)?", "", 0, 0, 0, "")]
    [InlineData(2, "(?P<x>.)?", "", 0, 0, 0, "")]
    [InlineData(0, "😀", "é😀", 0, 2, 6, "😀")]
    [InlineData(1, "ab[0-9]+", "ab12😀", 0, 0, 4, "ab12")]
    [InlineData(2, "(?P<x>.)+", "é😀", 0, 0, 6, "é😀")]
    [InlineData(2, "(?P<x>.)+", "πé😀", 2, 2, 8, "é😀")]
    [InlineData(2, "(?P<x>.*)", "πé😀", 8, 8, 8, "")]
    [InlineData(0, "(?P<x>😀)$", "πé😀", 0, 4, 8, "😀")]
    [InlineData(0, "(?P<x>é)", "πé😀", 0, 2, 4, "é")]
    public void ValueAndCursorModesKeepByteRangesAndValues(int kind, string pattern,
        string subject, int start, int startBytes, int endBytes, string text)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        using var token = new CancellationTokenSource();
        foreach (var cancellation in new[] { CancellationToken.None, token.Token })
        {
            var value = kind switch { 0 => regex.Match(input,start,cancellationToken: cancellation,mode: Utf8PythonMatchMode.Search), 1 => regex.Match(input,start,cancellationToken: cancellation), _ => regex.Match(input,start,cancellationToken: cancellation,mode: Utf8PythonMatchMode.FullMatch)};
            Assert.True(value.Success);
            Assert.Equal((startBytes, endBytes), (value.StartOffsetInBytes, value.EndOffsetInBytes));
            Assert.Equal(Encoding.UTF8.GetBytes(text), value.GetValueBytes().ToArray());
            Utf8PythonRangeMatch context;
            using (var cursor = regex.CreateMatchCursor(input, start, cancellation, mode: (Utf8PythonMatchMode)kind))
                cursor.MoveNext(out context);
            Assert.True(context.Success);
            Assert.Equal((startBytes, endBytes), (context.Value.StartOffsetInBytes, context.Value.EndOffsetInBytes));
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString(context.GetValueMemory().Span));
            if (regex.IsMatch(input, start, cancellation)) Assert.True(regex.Match(input,start,cancellationToken: cancellation,mode: Utf8PythonMatchMode.Search).Success);
        }
    }

    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(8193)]
    public void LongAsciiAndUnicodeTailsKeepValueAndStringWidths(int asciiBytes)
    {
        using var regex = new Utf8PythonRegex("start.*end", PythonReCompileOptions.DotAll);
        foreach (var tail in new[] { "", "é", "中", "😀", "\U0010ffff" })
        {
            var text = "start" + new string('x', asciiBytes) + tail + "end";
            var bytes = Encoding.UTF8.GetBytes(text);
            byte[] storage = [0xff, 0xff, 0xff, .. bytes, 0xff];
            var input = storage.AsSpan(3, bytes.Length);
            var value = regex.Match(input,mode: Utf8PythonMatchMode.FullMatch);
            Assert.True(value.Success);
            Assert.Equal((0, bytes.Length), (value.StartOffsetInBytes, value.EndOffsetInBytes));
            Assert.True(value.GetValueBytes().SequenceEqual(bytes));
            PythonReMatchAssert.Equal(text, regex.Match(input,mode: Utf8PythonMatchMode.FullMatch));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void LongNativeAndScalarValuesKeepCoordinatesAcrossBorrowedBlockCuts(int borrowedOffset)
    {
        using var native = new Utf8PythonRegex("start.*end", PythonReCompileOptions.DotAll);
        using var scalar = new Utf8PythonRegex("(start.*end)", PythonReCompileOptions.DotAll,
            resourcePolicy: new() { MaxWorkSteps = 1000000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        using var token = new CancellationTokenSource();
        foreach (var prefixLength in new[] { 0, 1, 30, 31, 32, 4093, 4094, 4095 })
            foreach (var tail in new[] { 0, 1, 31 })
            {
                var prefix = new string('x', prefixLength) + string.Concat(Enumerable.Repeat("é😀中", 513));
                var text = "start" + string.Concat(Enumerable.Repeat("\0\u007f\u0080\u07ff\u0800\uffff\U00010000\U0010ffff", 257)) + new string('y', tail) + "end";
                var bytes = Encoding.UTF8.GetBytes(prefix + text);
                byte[] storage = [.. Enumerable.Repeat((byte)0xff, borrowedOffset), .. bytes, 0xff];
                var input = storage.AsSpan(borrowedOffset, bytes.Length);
                var start = Encoding.UTF8.GetByteCount(prefix);
                var expected = Encoding.UTF8.GetBytes(text);
                foreach (var regex in new[] { native, scalar })
                {
                    var value = regex.Match(input,start,cancellationToken: ReferenceEquals(regex, native) ? default : token.Token,mode: Utf8PythonMatchMode.FullMatch);
                    Assert.True(value.Success);
                    Assert.Equal((start, bytes.Length), (value.StartOffsetInBytes, value.EndOffsetInBytes));
                    Assert.True(value.GetValueBytes().SequenceEqual(expected));
                    Assert.False(regex.Match("no"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
                    Assert.True(value.GetValueBytes().SequenceEqual(expected));
                }
            }
    }

    [Theory]
    [InlineData("(?<=(?P<left>é))a", "πéa", 4, "left", "é", 2, 4)]
    [InlineData("a(?=(?P<right>😀))", "πa😀", 2, "right", "😀", 3, 7)]
    [InlineData("(?P<outer>(?P<inner>é😀))(?P<absent>x)?", "πé😀", 2, "inner", "é😀", 2, 8)]
    [InlineData("(?P<empty>)", "π", 2, "empty", "", 2, 2)]
    public void CursorModesProjectCapturesWithoutUnrelatedUnicodeSuffix(string pattern, string prefix, int start, string name, string text, int begin, int end)
    {
        var input = Encoding.UTF8.GetBytes(prefix + string.Concat(Enumerable.Repeat("é😀", 65536)));
        using var token = new CancellationTokenSource();
        using var regex = new Utf8PythonRegex(pattern, resourcePolicy: new()
        {
            MaxWorkSteps = 5000,
            MaxDepth = 4096,
            MaxWorkingBytes = 65536,
            MaxResultBytes = 65536
        });
        foreach (var kind in new[] { 0, 1 })
        {
            Utf8PythonRangeMatch owned = default;
            using (var captureCursor1 = kind == 0 ? regex.CreateMatchCursor(input, start, token.Token, mode: Utf8PythonMatchMode.Search) : regex.CreateMatchCursor(input, start, token.Token, mode: Utf8PythonMatchMode.Match))
            {
                captureCursor1.MoveNext(out owned);
            }
            Assert.True(owned.Success);
            Assert.True(owned.TryGetFirstSetGroup(name, out var group));
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString((group.Success ? owned.GetGroupMemory(group.Number).Span : ReadOnlySpan<byte>.Empty)));
            Assert.Equal((begin, end), (group.StartOffsetInBytes, group.EndOffsetInBytes));
            Assert.Equal(1, owned.LastIndex);
            if (name == "inner")
            {
                Assert.Equal("outer", owned.LastGroup);
                Assert.False(owned.Groups[3].Success);
            }
            // Borrowed captured bytes survive another operation's scratch release.
            Utf8PythonRangeMatch snapshot2 = default;
            using (var captureCursor2 = regex.CreateMatchCursor(("!"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
            {
                captureCursor2.MoveNext(out snapshot2);
            }
            _ = snapshot2;
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString((group.Success ? owned.GetGroupMemory(group.Number).Span : ReadOnlySpan<byte>.Empty)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnownEndpointPublicationKeepsOriginalCancellationAndDeadline(bool expire)
    {
        var regex = new Utf8PythonRegex("(?P<x>.)+", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20));
        var input = "é😀"u8.ToArray();
        long ticks = 0;
        using var stable = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        Assert.True(regex.Match(input,mode: Utf8PythonMatchMode.FullMatch).Success); // Prepare before counting warm-call clock reads.
        var total = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => { total++; return 0; }))
            Assert.True(regex.Match(input,mode: Utf8PythonMatchMode.FullMatch).Success);
        using var token = new CancellationTokenSource();
        var reads = 0;
        using var injection = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == total) { if (expire) ticks = 50; else token.Cancel(); }
            return ticks;
        });
        if (expire) Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input,0,cancellationToken: token.Token,mode: Utf8PythonMatchMode.FullMatch));
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Match(input,0,cancellationToken: token.Token,mode: Utf8PythonMatchMode.FullMatch)).CancellationToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MissingCursorSnapshotsHaveNoOwnedChildren(int kind)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀)");
        Utf8PythonRangeMatch context;
        using (var cursor = regex.CreateMatchCursor("é"u8.ToArray(), mode: (Utf8PythonMatchMode)kind))
            cursor.MoveNext(out context);
        Assert.False(context.Success); Assert.Equal(0, context.Groups.Length);
        Assert.Empty(context.NameEntries.ToArray()); var value = kind switch { 0 => regex.Match("é"u8,mode: Utf8PythonMatchMode.Search), 1 => regex.Match("é"u8), _ => regex.Match("é"u8,mode: Utf8PythonMatchMode.FullMatch)};
        Assert.False(value.Success);
        Assert.False(regex.IsMatch("é"u8));
    }

    [Fact]
    public void ScalarValueProjectionRetainsNoCoordinateOrGroupRoot()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(?<=é)(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var cursor = new PythonReScalarMatchCursor("é😀z"u8, program, 0,
            Regex.InfiniteMatchTimeout,
            null, "(?<=é)(.)", new(new(0, 0, 1UL << 20), Regex.InfiniteMatchTimeout, CancellationToken.None));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        PythonReGroupData value;
        try
        {
            Assert.True(cursor.MoveNext());
            var baseline = cursor.ReservedBytes;
            value = cursor.SnapshotValueData();
            Assert.Equal((2, 6), (value.StartOffsetInBytes, value.EndOffsetInBytes));
            Assert.Equal(0UL, cursor.ResultReservation);
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
            Assert.True(regex.Match(input,0,cancellationToken: token.Token,mode: Utf8PythonMatchMode.Search).Success);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var found = regex.IsMatch(input, 0, token.Token);
        var value = regex.Match(input,0,cancellationToken: token.Token,mode: Utf8PythonMatchMode.Search);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(found); Assert.True(value.Success); Assert.Equal(1 << 20, value.StartOffsetInBytes);
        Assert.True(allocated < 65536, $"Unexpected subject-sized projection: {allocated} bytes.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RangeNamesAndGroupsRequireAdmissionAndSurviveScratchRelease(bool allow)
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var names = Enumerable.Repeat(new PythonReNameEntry { Name = "word", Number = 1 }, 5000).ToArray();
        var extent = ArrayBytes<PythonReNameEntry>(names.Length);
        var groupExtent = ArrayBytes<Utf8PythonCaptureRange>(2);
        var input = "😀é"u8.ToArray();
        // Real snapshot roots overlap discovery scratch. Refusal must leave the
        // result unpublished and every reservation refundable by this owner.
        var total = 13 + 2 * ArrayBytes<int>(1) + groupExtent + extent;
        var state = new PythonReScalarMatchState(input, program, 0, Regex.InfiniteMatchTimeout,
            "(.)", new(new(0, 0, total - (allow ? 0UL : 1UL)), Regex.InfiniteMatchTimeout, CancellationToken.None), null);
        Assert.True(state.WorkingInvocation.Ledger.TryReserve(13));
        Utf8PythonRangeMatch result = default;
        try
        {
            Assert.True(state.MoveNext(input));
            Exception? failure = null;
            try { result = state.SnapshotRanges(input, names); }
            catch (InvalidOperationException error) { failure = error; }
            if (allow)
            {
                Assert.Null(failure); Assert.True(result.Success);
                Assert.Equal(names, result.NameEntries.ToArray());
                Assert.Equal(groupExtent + extent, state.ResultReservation);
                Assert.Equal(total, state.ReservedBytes);
                names[0] = new PythonReNameEntry { Name = "changed", Number = 0 };
                Assert.Equal("word", result.NameEntries[0].Name);
                Assert.Equal("😀", Encoding.UTF8.GetString(result.GetValueMemory().Span));
            }
            else
            {
                Assert.NotNull(failure); Assert.False(result.Success);
                Assert.Equal(groupExtent, state.ResultReservation);
                Assert.Equal(total - extent, state.ReservedBytes);
                Assert.Equal(total, state.WorkingInvocation.Budget.PeakWorkingBytes);
                Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, state.WorkingInvocation.Budget.LimitBreach);
            }
        }
        finally { state.Dispose(); }
        Assert.Equal(13UL, state.ReservedBytes); state.WorkingInvocation.Ledger.Release(13);
        if (allow)
        {
            Assert.Equal("😀", Encoding.UTF8.GetString(result.GetValueMemory().Span));
            Assert.Equal("word", result.NameEntries[0].Name);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NameCopyFailureAfterAdmissionIsUnpublishedAndPreservesNeutralFailure(bool expire)
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
        var cursor = new PythonReScalarMatchState("😀"u8, program, 0, timeout,
            "(.)", new(new(0, 0, 1UL << 24), timeout, token.Token), null);
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        try
        {
            Assert.True(cursor.MoveNext("😀"u8));
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
                Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);

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
