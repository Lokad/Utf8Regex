using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2MemoryCursorTests
{
    [Theory]
    [InlineData("needle", "😀needle needle", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("a|ab", "ababa", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("one|two|three|four", "one two four", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData(".", "😀éx", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("a+", "aa aaa", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("a*|b", "ba", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("(?<x>a)+(?<absent>b)?", "aaa aaa", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("(?<=foo)(?<word>[a-z]+)-([0-9]+)", "😀fooabc-123 foofoo-45", Pcre2CompileOptions.None, 4, Pcre2MatchOptions.None)]
    [InlineData(@"\w+", "café tea", Pcre2CompileOptions.Ucp, 0, Pcre2MatchOptions.None)]
    [InlineData("abc", "aBc ABC", Pcre2CompileOptions.Caseless, 0, Pcre2MatchOptions.None)]
    [InlineData("(?|(a)|(b))", "ab a", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("(*MARK:tag)(a+)", "aaa aa", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData("^a+", "aaa\naaa", Pcre2CompileOptions.Multiline, 0, Pcre2MatchOptions.NotBol)]
    [InlineData("a*", "ba", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.NotEmpty)]
    [InlineData("a|aa", "xaa", Pcre2CompileOptions.None, 1, Pcre2MatchOptions.Anchored)]
    [InlineData(@"([a-z]+)-\K([0-9]+)", "foo-123 bar-45", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    [InlineData(@"a\K(?=b)", "ab ab", Pcre2CompileOptions.None, 0, Pcre2MatchOptions.None)]
    public void PublicRangesPreserveProfileProgressionAndFirstCaptureSlots(string pattern, string text,
        Pcre2CompileOptions options, int start, Pcre2MatchOptions matchOptions)
    {
        var regex = new Utf8Pcre2Regex(pattern, options); var input = Encoding.UTF8.GetBytes(text);
        using var cursor = regex.CreateMatchCursor(input, start, matchOptions);
        var expected = regex.MatchDetailed(input, start, matchOptions);
        var moved = cursor.MoveNext(out var first); Assert.Equal(expected.Success, moved);
        if (moved)
        {
            Assert.Equal(expected.CaptureSlotCount, first.Groups.Length); Assert.Equal(expected.Mark, first.Mark);
            for (var slot = 0; slot < expected.CaptureSlotCount; slot++)
            {
                var old = expected.GetGroup(slot); Assert.True(first.TryGetGroup(slot, out var group));
                Assert.Equal(old.Success, group.Success); Assert.Equal(old.StartOffsetInBytes, group.StartOffsetInBytes);
                Assert.Equal(old.EndOffsetInBytes, group.EndOffsetInBytes); Assert.Equal(old.IsUtf8SliceWellFormed, group.IsUtf8SliceWellFormed);
                Assert.Equal(old.GetValueBytes().ToArray(), first.GetGroupMemory(slot).ToArray());
            }
            var names = new Pcre2NameEntry[expected.NameEntryCount]; expected.CopyNameEntries(names, out var more);
            Assert.False(more); Assert.Equal(names, first.NameEntries.ToArray());
        }
        var enumerator = regex.EnumerateMatches(input, start, matchOptions);
        if (moved) { Assert.True(enumerator.MoveNext()); Assert.Equal(enumerator.Current.StartOffsetInBytes, first.Value.StartOffsetInBytes); }
        while (cursor.MoveNext(out var match))
        {
            Assert.True(enumerator.MoveNext());
            Assert.Equal((enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes),
                (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        }
        Assert.False(enumerator.MoveNext()); Assert.False(cursor.MoveNext(out _));
    }

    [Fact]
    public void DuplicateNamesLastCapturesAndUnmatchedSlotsStayDistinct()
    {
        var regex = new Utf8Pcre2Regex("(?J)(?:(?<x>a)+|(?<x>b)+)(?<empty>)(?<missing>c)?");
        using var cursor = regex.CreateMatchCursor("aaa bb"u8.ToArray());
        Assert.True(cursor.MoveNext(out var first)); Assert.True(first.TryGetFirstSetGroup("x", out var a));
        Assert.Equal(1, a.Number); Assert.Equal(2, a.StartOffsetInBytes); Assert.Equal(3, a.EndOffsetInBytes);
        Assert.True(first.TryGetGroup(3, out var empty)); Assert.True(empty.Success); Assert.Equal(empty.StartOffsetInBytes, empty.EndOffsetInBytes);
        Assert.True(first.TryGetGroup(4, out var missing)); Assert.False(missing.Success);
        Assert.True(cursor.MoveNext(out var second)); Assert.True(second.TryGetFirstSetGroup("x", out var b)); Assert.Equal(2, b.Number);
        Assert.Equal("b"u8.ToArray(), second.GetGroupMemory(2).ToArray()); Assert.Equal(4, first.NameEntries.Length);
        Assert.False(first.TryGetFirstSetGroup("missing", out _)); Assert.False(first.TryGetGroup(99, out _));
    }

    [Theory] [InlineData(Pcre2NewlineConvention.Lf)] [InlineData(Pcre2NewlineConvention.Crlf)]
    public void EmptyMatchesPreserveProfileCrlfAndSamePositionRetry(Pcre2NewlineConvention newline)
    {
        var regex = new Utf8Pcre2Regex("(?:|.)", Pcre2CompileOptions.None, new() { Newline = newline }, default, Timeout.InfiniteTimeSpan);
        var input = "😀\r\né"u8.ToArray(); using var cursor = regex.CreateMatchCursor(input);
        var expected = regex.EnumerateMatches(input);
        while (cursor.MoveNext(out var actual))
        {
            Assert.True(expected.MoveNext());
            Assert.Equal((expected.Current.StartOffsetInBytes, expected.Current.EndOffsetInBytes),
                (actual.Value.StartOffsetInBytes, actual.Value.EndOffsetInBytes));
        }
        Assert.False(expected.MoveNext());
    }

    [Fact]
    public void OptedInCodeUnitsReturnExplicitPartialByteRanges()
    {
        var regex = new Utf8Pcre2Regex(@"(\C)", Pcre2CompileOptions.None,
            new() { BackslashC = Pcre2BackslashCPolicy.Allow }, default, Timeout.InfiniteTimeSpan);
        using var cursor = regex.CreateMatchCursor("😀x"u8.ToArray());
        for (var index = 0; index < 5; index++)
        {
            Assert.True(cursor.MoveNext(out var match)); Assert.Equal(index, match.Value.StartOffsetInBytes);
            Assert.Equal(index + 1, match.Value.EndOffsetInBytes); Assert.Equal(index == 4, match.Value.IsUtf8SliceWellFormed);
            Assert.Single(match.GetValueMemory().ToArray()); Assert.Equal(match.Value.IsUtf8SliceWellFormed, match.Groups[1].IsUtf8SliceWellFormed);
        }
        Assert.False(cursor.MoveNext(out _));
    }

    [Fact]
    public void StrictValidationBoundaryAndNonMonotoneRejectionsPrecedePublication()
    {
        var regex = new Utf8Pcre2Regex(".");
        Assert.Throws<ArgumentException>(() => regex.CreateMatchCursor(new byte[] { (byte)'a', 0xff }));
        foreach (var offset in new[] { -1, 1, 2, 3, 6 })
            Assert.Throws<ArgumentOutOfRangeException>(() => regex.CreateMatchCursor("😀x"u8.ToArray(), offset));
        var nonMonotone = new Utf8Pcre2Regex(@"(?=ab\K)", Pcre2CompileOptions.None,
            new() { AllowLookaroundBackslashK = true }, default, Timeout.InfiniteTimeSpan);
        Assert.Throws<NotSupportedException>(() => nonMonotone.CreateMatchCursor("ab"u8.ToArray()));
    }

    [Fact]
    public async Task ProgramAndResultOwnersSurviveCloseAndSuspensionIndependently()
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root); var results = new Account(root);
        var owner = Compile("(?<x>😀)+", programs); var held = programs.Used;
        var cursor = owner.CreateMatchCursor("😀😀 😀"u8.ToArray(), workingMemoryAllowance: working); var alias = cursor;
        Assert.True(cursor.MoveNextRetained(out var receipt, resultMemoryAllowance: results)); var copied = receipt.Match;
        Assert.Equal(4, copied.Groups[1].StartOffsetInBytes); Assert.Equal(0UL, working.Used); Assert.True(results.Used > 0);
        owner.Dispose(); Assert.Equal(held, programs.Used); await Task.Yield();
        Assert.Throws<ObjectDisposedException>(() => owner.CreateMatchCursor("😀"u8.ToArray()));
        Assert.True(alias.MoveNext(out _)); Assert.False(cursor.MoveNext(out _)); Assert.Equal(0UL, programs.Used);
        Assert.Equal(receipt.ChargedBytes, root.Used); cursor.Dispose(); Assert.Throws<ObjectDisposedException>(() => alias.MoveNext(out _));
        receipt.Dispose(); receipt.Dispose(); Assert.Equal(0UL, root.Used);
        Assert.Equal("😀😀"u8.ToArray(), copied.GetValueMemory().ToArray()); Assert.Throws<ObjectDisposedException>(() => receipt.Match);
    }

    [Fact]
    public void ResultTransferAdmitsOverlapAndRefusalNeverRetriesOrInvalidatesPriorResults()
    {
        var root = new Root(); var working = new Account(root); var results = new Account(root);
        using var cursor = new Utf8Pcre2Regex("(*MARK:tag)(?<x>a)+").CreateMatchCursor("aaa aaa"u8.ToArray(), workingMemoryAllowance: working);
        var overlap = false; results.AfterGrant = () => overlap = working.Used > 0;
        Assert.True(cursor.MoveNextRetained(out var receipt, resultMemoryAllowance: results)); Assert.True(overlap);
        var held = results.Used; var attempts = results.Attempts; results.Maximum = held;
        Assert.Throws<Utf8MemoryAdmissionException>(() => cursor.MoveNextRetained(out _, resultMemoryAllowance: results));
        Assert.Equal(attempts + 1, results.Attempts); Assert.Equal(held, root.Used); Assert.Equal("tag", receipt.Match.Mark);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _)); receipt.Dispose(); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void SameAccountReceiptAndCurrentTokensKeepTheirExplicitLifetimes()
    {
        var root = new Root(); var account = new Account(root);
        using var original = new CancellationTokenSource(); using var previous = new CancellationTokenSource();
        using var cursor = new Utf8Pcre2Regex(".").CreateMatchCursor("😀éx"u8.ToArray(), cancellationToken: original.Token, workingMemoryAllowance: account);
        Assert.True(cursor.MoveNextRetained(out var first, previous.Token)); var held = first.ChargedBytes; previous.Cancel();
        Assert.True(cursor.MoveNext(out _)); Assert.Equal(held, root.Used);
        using var current = new CancellationTokenSource(); current.Cancel(); original.Cancel();
        Assert.Equal(original.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _, current.Token)).CancellationToken);
        Assert.Equal(held, root.Used); first.Dispose(); Assert.Equal(0UL, root.Used);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ActualInterruptionAfterAcquisitionRefundsBeforeThrowing(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var root = new Root(); var working = new Account(root); using var current = new CancellationTokenSource();
        using var cursor = new Utf8Pcre2Regex("(?<x>a)+", Pcre2CompileOptions.None, default, default,
            timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan).CreateMatchCursor("aaa"u8.ToArray(), workingMemoryAllowance: working);
        working.AfterGrant = () => { if (timeout) ticks = 100; else current.Cancel(); };
        var error = Record.Exception(() => cursor.MoveNext(out _, current.Token));
        if (timeout) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(error).ErrorKind);
        else Assert.Equal(current.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        Assert.True(working.Attempts > 0); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void CancellationAfterSeparateResultReserveRefundsBothChildren()
    {
        var root = new Root(); var working = new Account(root); var results = new Account(root); using var current = new CancellationTokenSource();
        using var cursor = new Utf8Pcre2Regex("(a+)").CreateMatchCursor("aaa"u8.ToArray(), workingMemoryAllowance: working);
        results.AfterGrant = current.Cancel;
        Assert.Equal(current.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNextRetained(out _, current.Token, results)).CancellationToken);
        Assert.Equal(1, results.Attempts); Assert.Equal(0UL, root.Used);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void AllNumericLimitsRemainEnforceable(int kind)
    {
        var policy = kind switch
        {
            0 => new Utf8ResourcePolicy { MaxWorkSteps = 1 }, 1 => new Utf8ResourcePolicy { MaxDepth = 1 },
            2 => new Utf8ResourcePolicy { MaxWorkingBytes = 1 }, _ => new Utf8ResourcePolicy { MaxResultBytes = 1 }
        };
        var root = new Root(); var working = new Account(root);
        var regex = Utf8Pcre2Regex.Create("(?=(?=(a+)))a+", default, default, policy, Timeout.InfiniteTimeSpan);
        Assert.Throws<Pcre2MatchException>(() => { using var cursor = regex.CreateMatchCursor("aaaa"u8.ToArray(), workingMemoryAllowance: working); cursor.MoveNext(out _); });
        Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void RepeatedPublicationDoesNotRefundResultTraffic()
    {
        var root = new Root(); var working = new Account(root);
        using var cursor = Utf8Pcre2Regex.Create("x", default, default, new() { MaxResultBytes = 100 }, Timeout.InfiniteTimeSpan)
            .CreateMatchCursor("xxxx"u8.ToArray(), workingMemoryAllowance: working);
        Assert.True(cursor.MoveNext(out var first)); var count = 1;
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() => { while (cursor.MoveNext(out _)) count++; }).ErrorKind);
        Assert.InRange(count, 1, 3); Assert.True(first.Success); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void LongIterationIsBoundedAndValidationIsNotRepeated()
    {
        var root = new Root(); var working = new Account(root); var input = Encoding.UTF8.GetBytes(new string('x', 500000));
        using (var cursor = new Utf8Pcre2Regex("x").CreateMatchCursor(input, workingMemoryAllowance: working))
        {
            var count = 0; while (cursor.MoveNext(out var match)) { Assert.Equal(count++, match.Value.StartOffsetInBytes); Assert.Equal(0UL, working.Used); }
            Assert.Equal(input.Length, count);
        }
        Assert.Equal(0UL, root.Used); Assert.True(working.Peak < 1024);
        using var bounded = Utf8Pcre2Regex.Create("x", default, default, new() { MaxWorkSteps = 50000 }, Timeout.InfiniteTimeSpan).CreateMatchCursor(input);
        for (var index = 0; index < 8; index++) Assert.True(bounded.MoveNext(out _));
    }

    [Fact]
    public void ReentrantAccessIsRejectedWithoutBreakingTheOwningAdvance()
    {
        var root = new Root(); var working = new Account(root); Utf8Pcre2MatchCursor? cursor = null; var rejected = false;
        working.AfterGrant = () => { if (cursor is not null) { Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _)); rejected = true; } };
        using (cursor = new Utf8Pcre2Regex("(a+)").CreateMatchCursor("aaa"u8.ToArray(), workingMemoryAllowance: working)) Assert.True(cursor.MoveNext(out _));
        Assert.True(rejected); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public async Task ConcurrentAdvanceAndDisposeAreRejectedUntilTheOwningCallFinishes()
    {
        var root = new Root(); var working = new Account(root);
        using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        using var cursor = new Utf8Pcre2Regex("(a+)").CreateMatchCursor("aaa"u8.ToArray(), workingMemoryAllowance: working);
        var paused = false;
        working.AfterGrant = () =>
        {
            if (paused) return; paused = true; entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test advance was not resumed.");
        };
        var advance = Task.Run(() => cursor.MoveNext(out _));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
            Assert.Throws<InvalidOperationException>(cursor.Dispose);
        }
        finally { resume.Set(); }
        Assert.True(await advance); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public async Task DeadlineIncludesHostWaitAndEarlierRangesRemainValid()
    {
        var root = new Root(); var working = new Account(root);
        using var cursor = new Utf8Pcre2Regex("(a+)", default, default, default, TimeSpan.FromMilliseconds(250))
            .CreateMatchCursor("aaa aaa"u8.ToArray(), workingMemoryAllowance: working);
        Assert.True(cursor.MoveNext(out var first)); await Task.Delay(350);
        Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() => cursor.MoveNext(out _)).ErrorKind);
        Assert.Equal(0UL, root.Used); Assert.Equal("aaa"u8.ToArray(), first.GetValueMemory().ToArray());
    }

    [Fact]
    public void RangeAdmissionDenialIsTerminalAndPreCancellationPrecedesInvalidInput()
    {
        var root = new Root(); var working = new Account(root) { Maximum = 0 };
        using var cursor = new Utf8Pcre2Regex("x").CreateMatchCursor("x"u8.ToArray(), workingMemoryAllowance: working);
        Assert.Equal(0, working.Attempts);
        Assert.Throws<Utf8MemoryAdmissionException>(() => cursor.MoveNext(out _));
        Assert.Equal(1, working.Attempts); Assert.Equal(0UL, root.Used);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            new Utf8Pcre2Regex(".").CreateMatchCursor(new byte[] { 0xff }, cancellationToken: canceled.Token)).CancellationToken);
    }

    private static Utf8Pcre2RegexProgramOwner Compile(string pattern, Account account)
        => Utf8Pcre2Regex.Compile(pattern, default, default, Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, account);
    private sealed class Root { internal readonly object Gate = new(); internal ulong Used; }
    private sealed class Account(Root root) : IUtf8MemoryAllowance
    {
        internal ulong Used, Peak; internal ulong Maximum = 64 << 20; internal int Attempts; internal Action? AfterGrant;
        public bool TryReserve(ulong bytes)
        {
            lock (root.Gate)
            {
                Attempts++; if (bytes > Maximum - Math.Min(Maximum, Used)) return false;
                Used += bytes; root.Used += bytes; Peak = Math.Max(Peak, Used);
            }
            AfterGrant?.Invoke(); return true;
        }
        public void Release(ulong bytes)
        {
            lock (root.Gate) { if (bytes > Used) throw new InvalidOperationException("Unfunded release."); Used -= bytes; root.Used -= bytes; }
        }
    }
}
