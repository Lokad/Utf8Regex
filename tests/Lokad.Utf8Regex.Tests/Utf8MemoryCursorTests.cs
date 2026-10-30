using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MemoryCursorTests
{
    [Theory]
    [InlineData("(?<letter>a)+(?<absent>b)?", "aaa aaa", RegexOptions.None)]
    [InlineData("(?<10>a)+(?<named>b)?", "aa ab", RegexOptions.None)]
    [InlineData("(?<x>a)(?<x>b)+", "abb ab", RegexOptions.None)]
    [InlineData("(?<open>a)+(?<-open>b)+(?(open)(?!))", "aaabbb aabb", RegexOptions.None)]
    [InlineData("(?<=foo)([a-z]+)-([0-9]+)", "fooabc-123 foofoo-45", RegexOptions.None)]
    [InlineData("(a|ab)+", "ababa aaba", RegexOptions.RightToLeft)]
    [InlineData("(a|ab)+", "ababa aaba", RegexOptions.NonBacktracking)]
    [InlineData("(?<x>a)+", "aaa aaa", RegexOptions.RightToLeft)]
    [InlineData("(?<x>a)+", "aaa aaa", RegexOptions.NonBacktracking)]
    [InlineData(@"\G(a?)", "aa ba", RegexOptions.None)]
    [InlineData(@"(?m)^([a-z]*)$", "ab\n\ncd", RegexOptions.None)]
    [InlineData("(?<x>é)+", "éé é", RegexOptions.None)]
    [InlineData("ab|a", "xxab a", RegexOptions.Compiled)]
    public void PublicRangesPreserveGroupsAndHistories(string pattern, string subject, RegexOptions options)
    {
        var bcl = new Regex(pattern, options);
        var expression = new Utf8Regex(pattern, options);
        using var cursor = expression.CreateMatchCursor(Encoding.UTF8.GetBytes(subject));
        foreach (Match expected in bcl.Matches(subject))
        {
            Assert.True(cursor.MoveNext(out var actual));
            Assert.Equal(bcl.GetGroupNumbers(), actual.GroupNumbers.ToArray());
            Assert.Equal(bcl.GetGroupNames(), actual.GroupNames.ToArray());
            Assert.Equal(expected.Value, Encoding.UTF8.GetString(actual.GetValueMemory().Span));
            foreach (var number in bcl.GetGroupNumbers())
            {
                Assert.True(actual.TryGetGroup(number, out var group));
                Assert.Equal(expected.Groups[number].Success, group.Success);
                Assert.Equal(expected.Groups[number].Value, Encoding.UTF8.GetString(actual.GetGroupMemory(number).Span));
                var captures = actual.GetCaptures(number);
                Assert.Equal(expected.Groups[number].Captures.Count, captures.Length);
                for (var index = 0; index < captures.Length; index++)
                {
                    var capture = expected.Groups[number].Captures[index];
                    Assert.True(captures[index].Success);
                    Assert.Equal(Encoding.UTF8.GetByteCount(subject.AsSpan(0, capture.Index)), captures[index].StartOffsetInBytes);
                    Assert.Equal(Encoding.UTF8.GetByteCount(subject.AsSpan(0, capture.Index + capture.Length)), captures[index].EndOffsetInBytes);
                }
            }
            Assert.False(actual.TryGetGroup(-1, out _));
        }
        Assert.False(cursor.MoveNext(out _)); Assert.False(cursor.MoveNext(out _));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void EmptyMatchesAdvanceOneScalarInEitherDirection(bool rtl)
    {
        using var cursor = new Utf8Regex("", rtl ? RegexOptions.RightToLeft : RegexOptions.None).CreateMatchCursor("😀é"u8.ToArray());
        var positions = new List<int>();
        while (cursor.MoveNext(out var match))
        {
            Assert.True(match.Value.Success); Assert.Equal(match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes);
            positions.Add(match.Value.StartOffsetInBytes);
        }
        Assert.Equal(rtl ? [6, 4, 0] : new[] { 0, 4, 6 }, positions);
    }

    [Fact]
    public void ScalarsStartsAndFullSubjectContextAreByteNative()
    {
        var bytes = "😀fooé é"u8.ToArray();
        using var cursor = new Utf8Regex("(?<=foo)(?<value>.)").CreateMatchCursor(bytes, 4);
        Assert.True(cursor.MoveNext(out var match)); Assert.Equal(7, match.Value.StartOffsetInBytes); Assert.Equal(9, match.Value.EndOffsetInBytes);
        Assert.True(match.TryGetGroup("value", out var group)); Assert.Equal(7, group.StartOffsetInBytes);
        Assert.Equal("é"u8.ToArray(), match.GetGroupMemory(1).ToArray()); Assert.False(cursor.MoveNext(out _));
        using var dots = new Utf8Regex(".").CreateMatchCursor("😀é"u8.ToArray());
        Assert.True(dots.MoveNext(out var first)); Assert.Equal(4, first.Value.EndOffsetInBytes);
        Assert.True(dots.MoveNext(out var second)); Assert.Equal(4, second.Value.StartOffsetInBytes); Assert.Equal(6, second.Value.EndOffsetInBytes);
        foreach (var start in new[] { -1, 1, 2, 3, 8, bytes.Length + 1 })
            Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex(".").CreateMatchCursor(bytes, start));
        Assert.Throws<ArgumentException>(() => new Utf8Regex("x").CreateMatchCursor(new byte[] { (byte)'x', 0xff }));
    }

    [Fact]
    public async Task SeparateOwnersKeepProgramAndResultFundedAcrossCloseAndAwaits()
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root); var results = new Account(root);
        var owner = Compile("(?<x>😀)+", programs); var programCharge = programs.Used;
        var cursor = owner.CreateMatchCursor("😀😀 😀"u8.ToArray(), workingMemoryAllowance: working);
        var alias = cursor;
        Assert.True(cursor.MoveNextRetained(out var retained, resultMemoryAllowance: results));
        Assert.True(results.Used > 0); var copied = retained.Match;
        Assert.Equal(2, copied.GetCaptures(1).Length); Assert.True(copied.TryGetGroup("x", out _));
        owner.Dispose(); Assert.Equal(programCharge, programs.Used);
        Assert.Throws<ObjectDisposedException>(() => owner.CreateMatchCursor("😀"u8.ToArray()));
        await Task.Yield();
        Assert.True(alias.MoveNext(out _)); Assert.False(cursor.MoveNext(out _));
        Assert.Equal(0UL, programs.Used); Assert.Equal(0UL, working.Used); Assert.Equal(retained.ChargedBytes, root.Used);
        cursor.Dispose(); Assert.Throws<ObjectDisposedException>(() => alias.MoveNext(out _));
        retained.Dispose(); retained.Dispose(); Assert.Equal(0UL, root.Used);
        Assert.Throws<ObjectDisposedException>(() => retained.Match);
        Assert.Equal("😀😀"u8.ToArray(), copied.GetValueMemory().ToArray()); Assert.Equal(2, copied.GetCaptures(1).Length);
    }

    [Fact]
    public void ResultTransferAdmitsOverlapAndRefusalIsTerminal()
    {
        var root = new Root(); var working = new Account(root); var results = new Account(root);
        using var cursor = new Utf8Regex("(?<x>a)+").CreateMatchCursor("aaa aaa"u8.ToArray(), workingMemoryAllowance: working);
        var overlap = false; results.AfterGrant = () => overlap = working.Used > 0;
        Assert.True(cursor.MoveNextRetained(out var first, resultMemoryAllowance: results)); Assert.True(overlap);
        var held = results.Used; var attempts = results.Attempts; results.Maximum = held;
        Assert.Throws<Utf8MemoryAdmissionException>(() => cursor.MoveNextRetained(out _, resultMemoryAllowance: results));
        Assert.Equal(attempts + 1, results.Attempts); Assert.Equal(held, results.Used); Assert.Equal(0UL, working.Used);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
        Assert.Equal("aaa"u8.ToArray(), first.Match.GetValueMemory().ToArray()); first.Dispose(); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void SameOwnerReceiptTransfersWithoutAnotherAcquisition()
    {
        var root = new Root(); var working = new Account(root);
        using var cursor = new Utf8Regex("x").CreateMatchCursor("x"u8.ToArray(), workingMemoryAllowance: working);
        Assert.True(cursor.MoveNextRetained(out var result)); Assert.True(working.Used >= result.ChargedBytes);
        Assert.False(cursor.MoveNext(out _)); Assert.Equal(result.ChargedBytes, working.Used);
        var copy = result.Match; result.Dispose(); Assert.Equal(0UL, root.Used); Assert.True(copy.Success);
    }

    [Fact]
    public void AdvanceTokensDoNotEscapeCompletedCallsAndCancellationFaultsAllAliases()
    {
        var root = new Root(); var working = new Account(root);
        using var original = new CancellationTokenSource(); using var advance = new CancellationTokenSource();
        using var cursor = new Utf8Regex(".").CreateMatchCursor("😀éx"u8.ToArray(), cancellationToken: original.Token, workingMemoryAllowance: working);
        Assert.True(cursor.MoveNext(out var first, advance.Token)); advance.Cancel();
        Assert.True(cursor.MoveNext(out _, default));
        using var current = new CancellationTokenSource(); current.Cancel();
        Assert.Equal(current.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _, current.Token)).CancellationToken);
        Assert.Equal(0UL, working.Used); Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
        Assert.Equal("😀"u8.ToArray(), first.GetValueMemory().ToArray());
        using var second = new Utf8Regex(".").CreateMatchCursor("x"u8.ToArray(), cancellationToken: original.Token, workingMemoryAllowance: working);
        original.Cancel();
        Assert.Equal(original.Token, Assert.Throws<OperationCanceledException>(() => second.MoveNext(out _, current.Token)).CancellationToken);
        Assert.Equal(0UL, root.Used);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void LiveAdvanceCancellationPreservesOriginalTokenIdentity(bool cancelOriginal)
    {
        var root = new Root(); var working = new Account(root);
        using var original = new CancellationTokenSource(); using var current = new CancellationTokenSource();
        var armed = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (armed) (cancelOriginal ? original : current).Cancel(); });
        try
        {
            using var cursor = new Utf8Regex("needle").CreateMatchCursor(Encoding.UTF8.GetBytes(new string('x', 262144)),
                cancellationToken: original.Token, workingMemoryAllowance: working);
            armed = true;
            Assert.Equal((cancelOriginal ? original : current).Token,
                Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _, current.Token)).CancellationToken);
            Assert.Equal(0UL, root.Used);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public async Task DeadlineIncludesHostWaitAndRefundsScratch()
    {
        var root = new Root(); var working = new Account(root);
        using var cursor = new Utf8Regex("(?<x>a)+", RegexOptions.None, TimeSpan.FromMilliseconds(250))
            .CreateMatchCursor("aaa aaa"u8.ToArray(), workingMemoryAllowance: working);
        Assert.True(cursor.MoveNext(out var first)); await Task.Delay(350);
        Assert.Empty(Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _)).Input);
        Assert.Equal(0UL, root.Used); Assert.Equal("aaa"u8.ToArray(), first.GetValueMemory().ToArray());
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void FinitePolicyDenialRefundsAndNeverPublishes(int kind)
    {
        var root = new Root(); var working = new Account(root);
        var policy = kind switch
        {
            0 => new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            1 => new Utf8ResourcePolicy { MaxWorkingBytes = 1 },
            2 => new Utf8ResourcePolicy { MaxResultBytes = 1 },
            _ => new Utf8ResourcePolicy { MaxDepth = 1 }
        };
        var regex = new Utf8Regex("(?=(?=(a)))a", RegexOptions.None, Timeout.InfiniteTimeSpan, policy);
        Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            using var cursor = regex.CreateMatchCursor("a"u8.ToArray(), workingMemoryAllowance: working);
            cursor.MoveNext(out _);
        });
        Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void ConstructorAndWorkingDenialsRefundOnlyTheirOwnLoans()
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root) { Maximum = 1 };
        using var owner = Compile("(a+)", programs); var held = programs.Used;
        Assert.Throws<Utf8MemoryAdmissionException>(() => owner.CreateMatchCursor("aaa"u8.ToArray(), workingMemoryAllowance: working));
        Assert.Equal(held, root.Used); Assert.Equal(1, working.Attempts);
        working.Maximum = 1 << 20;
        Assert.Throws<ArgumentException>(() => owner.CreateMatchCursor(new byte[] { 0xff }, workingMemoryAllowance: working));
        Assert.Equal(held, root.Used);
    }

    [Fact]
    public void LongIterationKeepsLiveFundingBoundedAndDoesNotRevalidateTheSubject()
    {
        var root = new Root(); var working = new Account(root);
        var input = Encoding.UTF8.GetBytes(new string('x', 500000));
        using (var cursor = new Utf8Regex("x").CreateMatchCursor(input, workingMemoryAllowance: working))
        {
            var count = 0;
            while (cursor.MoveNext(out var result)) { Assert.Equal(count++, result.Value.StartOffsetInBytes); Assert.True(working.Used < 1024); }
            Assert.Equal(input.Length, count);
        }
        Assert.Equal(0UL, root.Used); Assert.True(working.Peak < 1024);
        using var bounded = new Utf8Regex("x", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 50000 }).CreateMatchCursor(input);
        for (var index = 0; index < 8; index++) Assert.True(bounded.MoveNext(out _));
    }

    [Fact]
    public void ReentrantAccessIsRejectedWithoutBreakingTheOriginalCall()
    {
        var root = new Root(); var working = new Account(root); Utf8RegexMatchCursor? cursor = null;
        var rejected = false;
        working.AfterGrant = () => { if (cursor is not null) { Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _)); rejected = true; } };
        using (cursor = new Utf8Regex("(a+)").CreateMatchCursor("aaa"u8.ToArray(), workingMemoryAllowance: working))
            Assert.True(cursor.MoveNext(out _));
        Assert.True(rejected); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void ResultTrafficIsCumulativeEvenWhenEachPublishedLoanRefunds()
    {
        var root = new Root(); var working = new Account(root);
        using var cursor = new Utf8Regex("x", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxResultBytes = 150 }).CreateMatchCursor("xxxxx"u8.ToArray(), workingMemoryAllowance: working);
        Assert.True(cursor.MoveNext(out var first));
        var published = 1;
        Assert.Throws<Utf8ResourceLimitExceededException>(() => { while (cursor.MoveNext(out _)) published++; });
        Assert.InRange(published, 1, 4); Assert.Equal(0UL, root.Used); Assert.True(first.Success);
    }

    [Fact]
    public void CancellationAfterSeparateResultAdmissionRefundsBothChildrenBeforePublication()
    {
        var root = new Root(); var working = new Account(root); var results = new Account(root);
        using var current = new CancellationTokenSource();
        using var cursor = new Utf8Regex("(a+)").CreateMatchCursor("aaa"u8.ToArray(), workingMemoryAllowance: working);
        results.AfterGrant = current.Cancel;
        Assert.Equal(current.Token, Assert.Throws<OperationCanceledException>(() =>
            cursor.MoveNextRetained(out _, current.Token, results)).CancellationToken);
        Assert.Equal(1, results.Attempts); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public async Task ConcurrentAccessAndDisposeAreRejectedWhileAnAdvanceOwnsTheCursor()
    {
        using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        var armed = false; var paused = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (!armed || paused) return;
            paused = true; entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test advance was not resumed.");
        });
        Utf8RegexMatchCursor cursor;
        try { cursor = new Utf8Regex("needle").CreateMatchCursor(Encoding.UTF8.GetBytes(new string('x', 262144) + "needle")); }
        finally { session.Complete(); }
        using (cursor)
        {
            armed = true;
            var advance = Task.Run(() => cursor.MoveNext(out _));
            try
            {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
                Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
                Assert.Throws<InvalidOperationException>(cursor.Dispose);
            }
            finally { resume.Set(); }
            Assert.True(await advance);
        }
    }

    private static Utf8RegexProgramOwner Compile(string pattern, Account account) => Utf8Regex.Compile(pattern,
        RegexOptions.None, Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, account);

    private sealed class Root { internal ulong Used; }
    private sealed class Account(Root root) : IUtf8MemoryAllowance
    {
        internal ulong Used, Peak;
        internal ulong Maximum = 64 << 20;
        internal int Attempts;
        internal Action? AfterGrant;
        public bool TryReserve(ulong bytes)
        {
            Attempts++;
            if (bytes > Maximum - Math.Min(Maximum, Used)) return false;
            Used += bytes; root.Used += bytes; Peak = Math.Max(Peak, Used);
            AfterGrant?.Invoke(); return true;
        }
        public void Release(ulong bytes)
        {
            if (bytes > Used) throw new InvalidOperationException("Unfunded release.");
            Used -= bytes; root.Used -= bytes;
        }
    }
}
