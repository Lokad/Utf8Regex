using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReScalarCoordinateTests
{
    [Theory] [InlineData(0)] [InlineData(253)] [InlineData(254)] [InlineData(255)]
    [InlineData(256)] [InlineData(257)] [InlineData(511)]
    public void ScalarPositionsCrossByteBlocksAndSupplementaryTails(int asciiPrefix)
    {
        var text = new string('x', asciiPrefix) + "é😀a\u0301" + new string('y', 260) + "😀é";
        var input = Encoding.UTF8.GetBytes(text); var edges = new List<int> { 0 };
        foreach (var rune in text.EnumerateRunes()) edges.Add(edges[^1] + rune.Utf8SequenceLength);
        using var cursor = new Utf8PythonRegex("(?P<x>.)").CreateMatchCursor(input);
        var ordinal = 0;
        while (cursor.MoveNext(out var range))
        {
            Assert.Equal(edges[ordinal], range.Value.StartOffsetInBytes);
            Assert.Equal(edges[ordinal + 1], range.Value.EndOffsetInBytes);
            for (var number = 0; number < 2; number++)
            {
                Assert.True(cursor.TryGetScalarRange(number, out var start, out var end));
                Assert.Equal(ordinal, start); Assert.Equal(ordinal + 1, end);
            }
            Assert.False(cursor.HasCoordinateMap); ordinal++;
        }
        Assert.Equal(edges.Count - 1, ordinal);
    }

    [Fact]
    public void DetailedUtf16CoordinatesAndScalarCoordinatesKeepDistinctUnits()
    {
        using var cursor = new Utf8PythonRegex("(?P<x>.)").CreateMatchCursor("é😀a"u8.ToArray(), 2);
        Assert.True(cursor.MoveNext()); var snapshot = cursor.Current;
        Assert.True(cursor.TryGetScalarRange(1, out var start, out var end));
        Assert.Equal(1, start); Assert.Equal(2, end);
        Assert.Equal(2, snapshot.Groups[1].StartOffsetInBytes); Assert.Equal(6, snapshot.Groups[1].EndOffsetInBytes);
        Assert.Equal(1, snapshot.Groups[1].StartOffsetInUtf16); Assert.Equal(3, snapshot.Groups[1].EndOffsetInUtf16);
        cursor.Dispose(); Assert.Equal("😀", snapshot.Groups[1].ValueText); Assert.Equal(1, start);
    }

    [Theory] [InlineData("(?<=(é))😀", 1, 0, 1)] [InlineData("(?=(é))", 1, 0, 1)]
    [InlineData("(?:(a)|(é))()", 1, -1, -1)] [InlineData("(?:(a)|(é))()", 3, 1, 1)]
    public void LookaroundUnmatchedAndParticipatingEmptyRangesRemainDistinct(string pattern, int group, int expectedStart, int expectedEnd)
    {
        using var cursor = new Utf8PythonRegex(pattern).CreateMatchCursor("é😀"u8.ToArray());
        Assert.True(cursor.MoveNext(out _));
        Assert.Equal(expectedStart >= 0, cursor.TryGetScalarRange(group, out var start, out var end));
        Assert.Equal(expectedStart, start); Assert.Equal(expectedEnd, end);
        Assert.False(cursor.TryGetScalarRange(-1, out start, out end)); Assert.Equal(-1, start); Assert.Equal(-1, end);
        Assert.False(cursor.TryGetScalarRange(999, out _, out _)); Assert.False(cursor.HasCoordinateMap);
    }

    [Fact]
    public void EmptySubjectAndCursorPositionGuardsAreExplicit()
    {
        using var cursor = new Utf8PythonRegex("").CreateMatchCursor(ReadOnlyMemory<byte>.Empty);
        Assert.Throws<InvalidOperationException>(() => cursor.TryGetScalarRange(0, out _, out _));
        Assert.True(cursor.MoveNext(out _)); Assert.True(cursor.TryGetScalarRange(0, out var start, out var end));
        Assert.Equal(0, start); Assert.Equal(0, end); Assert.False(cursor.MoveNext(out _));
        Assert.Throws<InvalidOperationException>(() => cursor.TryGetScalarRange(0, out _, out _));
        cursor.Dispose(); Assert.Throws<ObjectDisposedException>(() => cursor.TryGetScalarRange(0, out _, out _));
    }

    [Fact]
    public void NoIndexIsAcquiredUntilARealMixedInputCaptureIsQueried()
    {
        var account = new Allowance(1 << 20); var input = Encoding.UTF8.GetBytes("a" + new string('é', 8192));
        using var cursor = new Utf8PythonRegex("(a)").CreateMatchCursor(input, 0, default, account);
        Assert.True(cursor.MoveNext(out var snapshot)); var reservations = account.Reservations;
        Assert.False(cursor.TryGetScalarRange(99, out _, out _)); Assert.Equal(reservations, account.Reservations);
        account.Maximum = account.Used;
        Assert.Throws<Utf8MemoryAdmissionException>(() => cursor.TryGetScalarRange(1, out _, out _));
        Assert.Equal(0UL, account.Used); Assert.Equal("a", Encoding.UTF8.GetString(snapshot.GetValueMemory().Span));
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
    }

    [Fact]
    public void RepeatedQueriesReuseOneLoanAndRetainedResultsSurviveClosure()
    {
        var account = new Allowance(4 << 20); Assert.True(account.TryReserve(17));
        using var regex = Utf8PythonRegex.Compile("(é)", default, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, account);
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("é" + new string('x', 2048)));
        Assert.True(cursor.MoveNextRetained(out var retained)); using var receipt = retained!;
        regex.Dispose(); Assert.True(cursor.TryGetScalarRange(1, out var start, out var end));
        Assert.Equal(0, start); Assert.Equal(1, end); var held = account.Used; var reservations = account.Reservations;
        for (var i = 0; i < 10000; i++) Assert.True(cursor.TryGetScalarRange(1, out _, out _));
        Assert.Equal(held, account.Used); Assert.Equal(reservations, account.Reservations);
        Assert.False(cursor.MoveNext(out _)); Assert.Equal(17UL + receipt.ChargedBytes, account.Used);
        Assert.Equal("é", Encoding.UTF8.GetString(receipt.Match.GetValueMemory().Span));
        receipt.Dispose(); Assert.Equal(17UL, account.Used); account.Release(17);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void HalfMillionDiscardedScalarRangesHaveBoundedLiveStorage(bool unicode)
    {
        var account = new Allowance(32768); var input = unicode ? Encoding.UTF8.GetBytes(new string('é', 500000)) : new byte[500000];
        if (!unicode) input.AsSpan().Fill((byte)'a');
        using var cursor = new Utf8PythonRegex(".", default, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 32768 }).CreateMatchCursor(input, 0, default, account);
        var count = 0;
        while (cursor.MoveNext(out _))
        {
            Assert.True(cursor.TryGetScalarRange(0, out var start, out var end));
            Assert.Equal(count, start); Assert.Equal(++count, end);
        }
        Assert.Equal(500000, count); Assert.Equal(0UL, account.Used); Assert.True(account.Peak <= 32768);
    }

    [Fact]
    public void CompletedQueryAndAdvanceTokensHaveNoAuthorityOverLaterCalls()
    {
        using var creation = new CancellationTokenSource(); using var advance = new CancellationTokenSource();
        using var first = new CancellationTokenSource(); using var second = new CancellationTokenSource();
        using var cursor = new Utf8PythonRegex(".").CreateMatchCursor("é😀a"u8.ToArray(), 0, creation.Token);
        Assert.True(cursor.MoveNext(out _, advance.Token)); advance.Cancel();
        Assert.True(cursor.TryGetScalarRange(0, out _, out _, first.Token)); first.Cancel();
        Assert.True(cursor.TryGetScalarRange(0, out var start, out var end, second.Token)); second.Cancel();
        Assert.Equal(0, start); Assert.Equal(1, end); Assert.True(cursor.MoveNext(out _));
        Assert.True(cursor.TryGetScalarRange(0, out start, out end)); Assert.Equal(1, start); Assert.Equal(2, end);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void CancellationDuringIndexConstructionPreservesOriginalTokenAndRefunds(bool cancelCreation)
    {
        using var creation = new CancellationTokenSource(); using var query = new CancellationTokenSource();
        var account = new Allowance(1 << 20); using var cursor = new Utf8PythonRegex("(a)")
            .CreateMatchCursor(Encoding.UTF8.GetBytes("a" + new string('é', 32768)), 0, creation.Token, account);
        Assert.True(cursor.MoveNext(out var snapshot)); var held = account.Used; var steps = cursor.WorkingInvocation.Budget.Steps;
        cursor.WorkingInvocation.CheckpointHook = current => { if (current > steps + 128) { if (cancelCreation) creation.Cancel(); query.Cancel(); } };
        var error = Assert.Throws<OperationCanceledException>(() => cursor.TryGetScalarRange(0, out _, out _, query.Token));
        Assert.Equal(cancelCreation ? creation.Token : query.Token, error.CancellationToken);
        Assert.True(account.Peak > held); Assert.Equal(0UL, account.Used);
        Assert.Equal("a", Encoding.UTF8.GetString(snapshot.GetValueMemory().Span));
    }

    [Fact]
    public void DeadlineDuringIndexConstructionOmitsUnadmittedSubjectText()
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var account = new Allowance(1 << 20); using var cursor = new Utf8PythonRegex("(a)", default, TimeSpan.FromMilliseconds(100))
            .CreateMatchCursor(Encoding.UTF8.GetBytes("a" + new string('é', 32768)), 0, default, account);
        Assert.True(cursor.MoveNext(out _)); var steps = cursor.WorkingInvocation.Budget.Steps;
        cursor.WorkingInvocation.CheckpointHook = current => { if (current > steps + 128) ticks = 100; };
        var error = Assert.Throws<RegexMatchTimeoutException>(() => cursor.TryGetScalarRange(0, out _, out _));
        Assert.Empty(error.Input); Assert.Equal("(a)", error.Pattern); Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void ScalarIndexUsesWorkingAdmissionWithoutSpendingResultTraffic()
    {
        var input = Encoding.UTF8.GetBytes("a" + new string('é', 4096));
        using var cursor = new Utf8PythonRegex("(a)", default, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxResultBytes = 4096 }).CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out _)); var traffic = cursor.WorkingInvocation.Budget.ResultBytes;
        Assert.True(cursor.TryGetScalarRange(1, out _, out _)); Assert.Equal(traffic, cursor.WorkingInvocation.Budget.ResultBytes);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void IndexConstructionPreservesFiniteWorkAndWorkingDenials(bool work)
    {
        var input = Encoding.UTF8.GetBytes("é" + new string('x', 65536));
        var account = new Allowance(1 << 20); ulong steps, peak;
        using (var reference = new Utf8PythonRegex("(é)", default, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1 << 20 }).CreateMatchCursor(input, 0, default, account))
        {
            Assert.True(reference.MoveNext(out _)); steps = reference.WorkingInvocation.Budget.Steps;
            peak = reference.WorkingInvocation.Budget.PeakWorkingBytes;
            var depth = reference.WorkingInvocation.Budget.PeakDepth;
            Assert.True(reference.TryGetScalarRange(0, out _, out _));
            Assert.Equal(depth, reference.WorkingInvocation.Budget.PeakDepth);
        }
        Assert.Equal(0UL, account.Used);
        using var cursor = new Utf8PythonRegex("(é)", default, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = work ? steps + 3 : 1_000_000, MaxWorkingBytes = work ? 1UL << 20 : peak })
            .CreateMatchCursor(input, 0, default, account);
        Assert.True(cursor.MoveNext(out var retained));
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.TryGetScalarRange(1, out _, out _));
        Assert.Equal(work ? Utf8ResourceLimitKind.ExecutionWorkSteps : Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind);
        Assert.Equal(0UL, account.Used); Assert.Equal("é", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Fact]
    public void ReentrantProjectionIsRejectedWithoutCorruptingTheActiveQuery()
    {
        using var cursor = new Utf8PythonRegex("(a)").CreateMatchCursor(Encoding.UTF8.GetBytes("a" + new string('é', 8192)));
        Assert.True(cursor.MoveNext(out _)); var rejected = false;
        cursor.WorkingInvocation.CheckpointHook = currentStep =>
        {
            Assert.Throws<InvalidOperationException>(() => cursor.TryGetScalarRange(0, out _, out _));
            rejected = true;
        };
        Assert.True(cursor.TryGetScalarRange(1, out var start, out var end));
        Assert.True(rejected); Assert.Equal(0, start); Assert.Equal(1, end);
    }

    [Fact]
    public async Task SharedHostLifetimesComposeAcrossWaitsAndProgramClosure()
    {
        var account = new Allowance(8 << 20); Assert.True(account.TryReserve(17));
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 10_000_000, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        Utf8PythonRegex Compile(string pattern) => Utf8PythonRegex.Compile(pattern, default, Timeout.InfiniteTimeSpan,
            policy, default, Timeout.InfiniteTimeSpan, default, account);
        using var first = Compile("(?i)(?P<x>é+|a+)"); using var second = Compile("(?P<d>[0-9]+)");
        Assert.Equal(PythonReCompileOptions.IgnoreCase | PythonReCompileOptions.Unicode, first.EffectiveOptions);
        Assert.Equal("éé!", first.SubnToString("éé"u8, @"\g<x>!").ResultText);
        var held = account.Used;
        Assert.Equal(PythonReErrorSource.Pattern, Assert.Throws<PythonRePatternException>(() => Compile("😀[")).ErrorSource);
        Assert.Equal(PythonReErrorSource.Replacement, Assert.Throws<PythonRePatternException>(() => first.Subn(""u8, @"bad\q")).ErrorSource);
        Assert.Equal(held, account.Used); Assert.Equal("éé!", first.SubnToString("éé"u8, @"\g<x>!").ResultText);
        using var advance = new CancellationTokenSource(); using var query = new CancellationTokenSource();
        using var a = first.CreateMatchCursor(Encoding.UTF8.GetBytes(new string('x', 300) + "😀 éé AA"));
        using var b = second.CreateMatchCursor("😀 12"u8.ToArray());
        Assert.True(a.MoveNextRetained(out var retained, advance.Token)); using var rangeReceipt = retained!;
        Assert.True(b.MoveNext(out _)); advance.Cancel(); await Task.Yield();
        first.Dispose(); Assert.True(a.TryGetScalarRange(1, out var start, out var end, query.Token));
        Assert.Equal(302, start); Assert.Equal(304, end); Assert.Equal("x", rangeReceipt.Match.LastGroup);
        using var output = second.SubnRetained("12"u8, @"[\g<d>]"); var escaped = output.Result;
        second.Dispose(); Assert.True(b.TryGetScalarRange(1, out var otherStart, out var otherEnd));
        Assert.Equal(2, otherStart); Assert.Equal(4, otherEnd); query.Cancel(); await Task.Yield();
        Assert.True(a.MoveNext(out _)); Assert.True(a.TryGetScalarRange(1, out var nextStart, out var nextEnd));
        Assert.Equal(305, nextStart); Assert.Equal(307, nextEnd);
        Assert.False(a.MoveNext(out _)); Assert.False(b.MoveNext(out _));
        Assert.Equal(17UL + rangeReceipt.ChargedBytes + output.ChargedBytes, account.Used);
        Assert.Equal("éé", Encoding.UTF8.GetString(rangeReceipt.Match.GetGroupMemory(1).Span));
        output.Dispose(); rangeReceipt.Dispose(); Assert.Equal(17UL, account.Used);
        Assert.Equal("[12]", Encoding.UTF8.GetString(escaped.ResultBytes)); Assert.Equal(302, start);
        account.Release(17);
    }
}
