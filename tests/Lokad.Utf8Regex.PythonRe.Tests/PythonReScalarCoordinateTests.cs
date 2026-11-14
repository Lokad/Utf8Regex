using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReScalarCoordinateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(253)]
    [InlineData(254)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(511)]
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
            ordinal++;
        }
        Assert.Equal(edges.Count - 1, ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void ScalarCountsKeepAllWidthsAcrossVectorAndIndexBlockCuts(int borrowedOffset)
    {
        using var regex = new Utf8PythonRegex("(.*)");
        for (var prefix = 0; prefix < 64; prefix++)
        foreach (var tail in new[] { 0, 1, 31, 32, 33 })
        {
            var text = new string('x', prefix) + string.Concat(Enumerable.Repeat("aé中😀\u0800\uD7FF\U0010ffff", 32)) + new string('y', tail);
            var bytes = Encoding.UTF8.GetBytes(text);
            byte[] storage = [.. Enumerable.Repeat((byte)0xff, borrowedOffset), .. bytes, 0xff];
            using var cursor = regex.CreateMatchCursor(storage.AsMemory(borrowedOffset, bytes.Length));
            Assert.True(cursor.MoveNext());
            var value = cursor.Current.Value;
            Assert.Equal((0, text.Length), (value.StartOffsetInUtf16, value.EndOffsetInUtf16));
            var expected = text.EnumerateRunes().Count();
            Assert.True(cursor.TryGetScalarRange(0, out var start, out var end));
            Assert.Equal((0, expected), (start, end));
            Assert.True(cursor.TryGetScalarRange(1, out start, out end));
            Assert.Equal((0, expected), (start, end));
        }
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
    [Theory]
    [InlineData("(?<=(é))😀", 1, 0, 1)]
    [InlineData("(?=(é))", 1, 0, 1)]
    [InlineData("(?:(a)|(é))()", 1, -1, -1)]
    [InlineData("(?:(a)|(é))()", 3, 1, 1)]
    public void LookaroundUnmatchedAndParticipatingEmptyRangesRemainDistinct(string pattern, int group, int expectedStart, int expectedEnd)
    {
        using var cursor = new Utf8PythonRegex(pattern).CreateMatchCursor("é😀"u8.ToArray());
        Assert.True(cursor.MoveNext(out _));
        Assert.Equal(expectedStart >= 0, cursor.TryGetScalarRange(group, out var start, out var end));
        Assert.Equal(expectedStart, start); Assert.Equal(expectedEnd, end);
        Assert.False(cursor.TryGetScalarRange(-1, out start, out end)); Assert.Equal(-1, start); Assert.Equal(-1, end);
        Assert.False(cursor.TryGetScalarRange(999, out _, out _));
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

    [Theory]
    [InlineData("a")]
    [InlineData("é")]
    public void EarlyQueriesDoNotPrepareAnUnusedLargeSuffix(string capture)
    {
        foreach (var suffix in new[] { "", new string('x', 1 << 20), string.Concat(Enumerable.Repeat("😀", 1 << 18)) })
        {
            var account = new Allowance(1 << 20); Assert.True(account.TryReserve(17));
            var input = Encoding.UTF8.GetBytes(new string('x', 300) + capture + suffix);
            using var cursor = new Utf8PythonRegex("(" + capture + ")", resourcePolicy: new()
                { MaxWorkSteps = 2000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 })
                .CreateMatchCursor(input, 0, default, account);
            Assert.True(cursor.MoveNext(out var retained));
            var reservations = account.Reservations;
            Assert.True(cursor.TryGetScalarRange(1, out var start, out var end));
            Assert.Equal((300, 301), (start, end));
            if (capture == "a") Assert.Equal(reservations, account.Reservations);
            cursor.Dispose();
            Assert.Equal(17UL, account.Used); account.Release(17);
            Assert.Equal(capture, Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        }
    }

    [Fact]
    public void PrefixGrowthAndLaterDetailedOutputKeepDistinctCoordinateFacts()
    {
        var text = new string('x', 300) + "a" + new string('x', 700) + "é" +
            new string('x', 1100) + "😀" + new string('x', 5000) + "é😀";
        var account = new Allowance(1 << 20);
        using var cursor = new Utf8PythonRegex("(a)|(é)|(😀)").CreateMatchCursor(Encoding.UTF8.GetBytes(text), 0, default, account);
        foreach (var expected in new[] { (300, 301), (1001, 1002), (2102, 2103) })
        {
            Assert.True(cursor.MoveNext(out _));
            Assert.True(cursor.TryGetScalarRange(0, out var start, out var end));
            Assert.Equal(expected, (start, end));
            var reservations = account.Reservations;
            Assert.True(cursor.TryGetScalarRange(0, out start, out end));
            Assert.Equal(expected, (start, end)); Assert.Equal(reservations, account.Reservations);
        }
        // A proved ASCII prefix must not become a whole-subject description.
        Assert.True(cursor.MoveNext()); var detailed = cursor.Current;
        Assert.Equal("é", detailed.Groups[0].ValueText);
        Assert.Equal((7107, 7109), (detailed.Groups[0].StartOffsetInBytes, detailed.Groups[0].EndOffsetInBytes));
        Assert.Equal((7104, 7105), (detailed.Groups[0].StartOffsetInUtf16, detailed.Groups[0].EndOffsetInUtf16));
        Assert.True(cursor.TryGetScalarRange(0, out var finalStart, out var finalEnd));
        Assert.Equal((7103, 7104), (finalStart, finalEnd));
        cursor.Dispose(); Assert.Equal(0UL, account.Used); Assert.Equal("é", detailed.Groups[0].ValueText);
    }

    [Fact]
    public void BackwardLookbehindQueriesReuseTheInitializedPrefix()
    {
        var account = new Allowance(1 << 20);
        using var cursor = new Utf8PythonRegex("(?<=(é))(😀)")
            .CreateMatchCursor(Encoding.UTF8.GetBytes(new string('x', 1000) + "é😀"), 0, default, account);
        Assert.True(cursor.MoveNext(out _));
        Assert.True(cursor.TryGetScalarRange(2, out var start, out var end));
        Assert.Equal((1001, 1002), (start, end)); var reservations = account.Reservations;
        Assert.True(cursor.TryGetScalarRange(1, out start, out end));
        Assert.Equal((1000, 1001), (start, end)); Assert.Equal(reservations, account.Reservations);
        cursor.Dispose(); Assert.Equal(0UL, account.Used);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GrowthRefusalOrCancellationRefundsBothCapacities(bool cancel)
    {
        var account = new Allowance(1 << 20); Assert.True(account.TryReserve(17));
        using var token = new CancellationTokenSource();
        using var cursor = new Utf8PythonRegex("(é)")
            .CreateMatchCursor(Encoding.UTF8.GetBytes(new string('x', 300) + "é" + new string('x', 2000) + "é"), 0, token.Token, account);
        Assert.True(cursor.MoveNext(out var first)); Assert.True(cursor.TryGetScalarRange(0, out _, out _));
        Assert.True(cursor.MoveNext(out var second));
        if (cancel) account.AfterGrant = token.Cancel;
        else account.Maximum = account.Used;
        if (cancel)
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.TryGetScalarRange(0, out _, out _)).CancellationToken);
        else
            Assert.Throws<Utf8MemoryAdmissionException>(() => cursor.TryGetScalarRange(0, out _, out _));
        Assert.Equal(17UL, account.Used);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
        Assert.Equal("é", Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.Equal("é", Encoding.UTF8.GetString(second.GetGroupMemory(1).Span));
        cursor.Dispose(); Assert.Equal(17UL, account.Used); account.Release(17);
    }
    [Fact]
    public void NoIndexIsAcquiredUntilARealMixedInputCaptureIsQueried()
    {
        var account = new Allowance(1 << 20); var input = Encoding.UTF8.GetBytes(new string('é', 8192) + "a");
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
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(new string('x', 1024) + "é" + new string('x', 1024)));
        Assert.True(cursor.MoveNextRetained(out var retained)); using var receipt = retained!;
        regex.Dispose(); Assert.True(cursor.TryGetScalarRange(1, out var start, out var end));
        Assert.Equal(1024, start); Assert.Equal(1025, end); var held = account.Used; var reservations = account.Reservations;
        for (var i = 0; i < 10000; i++) Assert.True(cursor.TryGetScalarRange(1, out _, out _));
        Assert.Equal(held, account.Used); Assert.Equal(reservations, account.Reservations);
        Assert.False(cursor.MoveNext(out _)); Assert.Equal(17UL + receipt.ChargedBytes, account.Used);
        Assert.Equal("é", Encoding.UTF8.GetString(receipt.Match.GetValueMemory().Span));
        receipt.Dispose(); Assert.Equal(17UL, account.Used); account.Release(17);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HalfMillionDiscardedScalarRangesHaveBoundedLiveStorage(bool unicode)
    {
        // The shared index stores both scalar and UTF-16 counts, admitting old/new
        // growth overlap. Keep a bounded cap that covers that pair of counts.
        var account = new Allowance(65536); var input = unicode ? Encoding.UTF8.GetBytes(new string('é', 500000)) : new byte[500000];
        if (!unicode) input.AsSpan().Fill((byte)'a');
        using var cursor = new Utf8PythonRegex(".", default, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 65536 }).CreateMatchCursor(input, 0, default, account);
        var count = 0;
        while (cursor.MoveNext(out _))
        {
            Assert.True(cursor.TryGetScalarRange(0, out var start, out var end));
            Assert.Equal(count, start); Assert.Equal(++count, end);
        }
        Assert.Equal(500000, count); Assert.Equal(0UL, account.Used); Assert.True(account.Peak <= 65536);
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringIndexConstructionPreservesOriginalTokenAndRefunds(bool cancelCreation)
    {
        using var creation = new CancellationTokenSource(); using var query = new CancellationTokenSource();
        var account = new Allowance(1 << 20); using var cursor = new Utf8PythonRegex("(a)")
            .CreateMatchCursor(Encoding.UTF8.GetBytes(new string('é', 32768) + "a"), 0, creation.Token, account);
        Assert.True(cursor.MoveNext(out var snapshot)); var held = account.Used; var steps = cursor.WorkingInvocation.Budget.Steps;
        cursor.WorkingInvocation.CheckpointHook = current => { if (current > steps + 128) { if (cancelCreation) creation.Cancel(); query.Cancel(); } };
        var error = Assert.Throws<OperationCanceledException>(() => cursor.TryGetScalarRange(0, out _, out _, query.Token));
        Assert.Equal(cancelCreation ? creation.Token : query.Token, error.CancellationToken);
        Assert.True(account.Peak >= held); Assert.Equal(0UL, account.Used);
        Assert.Equal("a", Encoding.UTF8.GetString(snapshot.GetValueMemory().Span));
    }
    [Fact]
    public void DeadlineDuringIndexConstructionOmitsUnadmittedSubjectText()
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var account = new Allowance(1 << 20); using var cursor = new Utf8PythonRegex("(a)", default, TimeSpan.FromMilliseconds(100))
            .CreateMatchCursor(Encoding.UTF8.GetBytes(new string('é', 32768) + "a"), 0, default, account);
        Assert.True(cursor.MoveNext(out _)); var steps = cursor.WorkingInvocation.Budget.Steps;
        cursor.WorkingInvocation.CheckpointHook = current => { if (current > steps + 128) ticks = 100; };
        var error = Assert.Throws<RegexMatchTimeoutException>(() => cursor.TryGetScalarRange(0, out _, out _));
        Assert.Empty(error.Input); Assert.Equal("(a)", error.Pattern); Assert.Equal(0UL, account.Used);
    }
    [Fact]
    public void ScalarIndexUsesWorkingAdmissionWithoutSpendingResultTraffic()
    {
        var input = Encoding.UTF8.GetBytes(new string('é', 4096) + "a");
        using var cursor = new Utf8PythonRegex("(a)", default, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxResultBytes = 4096 }).CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out _)); var traffic = cursor.WorkingInvocation.Budget.ResultBytes;
        Assert.True(cursor.TryGetScalarRange(1, out _, out _)); Assert.Equal(traffic, cursor.WorkingInvocation.Budget.ResultBytes);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndexConstructionPreservesFiniteWorkAndWorkingDenials(bool work)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 65536) + "é");
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
        using var cursor = new Utf8PythonRegex("(a)").CreateMatchCursor(Encoding.UTF8.GetBytes(new string('é', 8192) + "a"));
        Assert.True(cursor.MoveNext(out _)); var rejected = false;
        cursor.WorkingInvocation.CheckpointHook = currentStep =>
        {
            Assert.Throws<InvalidOperationException>(() => cursor.TryGetScalarRange(0, out _, out _));
            rejected = true;
        };
        Assert.True(cursor.TryGetScalarRange(1, out var start, out var end));
        Assert.True(rejected); Assert.Equal(8192, start); Assert.Equal(8193, end);
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
