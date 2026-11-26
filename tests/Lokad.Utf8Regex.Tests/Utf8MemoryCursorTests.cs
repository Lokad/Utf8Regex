using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

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
    [InlineData(@"[\w.+-]+@[\w-]+\.[\w.-]+", "é@x.y 😀a-b@z.com", RegexOptions.None)]
    [InlineData(@"[\w@]+@[\w-]+\.[\w.-]+", "😀a@b@c.de é@x.y", RegexOptions.None)]
    [InlineData(@"\p{L}+", "éa-β C", RegexOptions.NonBacktracking)]
    [InlineData(@"\p{L}+", "éa-β C", RegexOptions.RightToLeft)]
    [InlineData(@"\b", "éa-β C", RegexOptions.None)]
    [InlineData(@"(\w+)\s+(ab|bc|longer)", "😀é abc beta bc X longer", RegexOptions.None)]
    [InlineData(@"(\w+)\s+(X?Holmes|Watson)", "é XHolmes beta Watson", RegexOptions.None)]
    [InlineData(@"\B[a-zA-Z]{0,12}ing\B", "éwalkingish a starting point", RegexOptions.None)]
    [InlineData(@"\b([a-zA-Z]{0,12})ing\b", "β walking abcdefghijklmnopqring éworking", RegexOptions.None)]
    [InlineData("(?<10>é)(?<2>a)?()", "éa é", RegexOptions.None)]
    [InlineData("(?:(?<first>ab)c|(?<second>a)b)d", "abd abcd", RegexOptions.None)]
    [InlineData("(é){2}", "éé éé", RegexOptions.None)]
    public void PublicRangesPreserveGroupsAndHistories(string pattern, string subject, RegexOptions options)
    {
        var bcl = new Regex(pattern, options);
        var expression = new Utf8Regex(pattern, options: options);
        using var cursor = expression.CreateMatchCursor(Encoding.UTF8.GetBytes(subject));
        foreach (Match expected in bcl.Matches(subject))
        {
            Assert.True(cursor.MoveNext(out var actual));
            Assert.Equal(bcl.GetGroupNumbers(), actual.GroupNumbers.ToArray());
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
        using var cursor = new Utf8Regex("", options: rtl ? RegexOptions.RightToLeft : RegexOptions.None).CreateMatchCursor("😀é"u8.ToArray());
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
        Assert.True(match.TryGetGroup(1, out var group)); Assert.Equal(7, group.StartOffsetInBytes);
        Assert.Equal("é"u8.ToArray(), match.GetGroupMemory(1).ToArray()); Assert.False(cursor.MoveNext(out _));
        using var dots = new Utf8Regex(".").CreateMatchCursor("😀é"u8.ToArray());
        Assert.True(dots.MoveNext(out var first)); Assert.Equal(4, first.Value.EndOffsetInBytes);
        Assert.True(dots.MoveNext(out var second)); Assert.Equal(4, second.Value.StartOffsetInBytes); Assert.Equal(6, second.Value.EndOffsetInBytes);
        foreach (var start in new[] { -1, 1, 2, 3, 8, bytes.Length + 1 })
            Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex(".").CreateMatchCursor(bytes, start));
    }

    [Fact]
    public void BoundaryFamilyCursorAdmitsOnlyItsRangeSnapshot()
    {
        var root = new Root();
        var working = new Account(root) { Maximum = 128 };
        using var regex = new Utf8Regex(@"\b(?:the|and|that|have|for)\b");
        using var cursor = regex.CreateMatchCursor("éthe xand forth that\u200d 😀for! and!"u8.ToArray(),
            startOffsetInBytes: 2, workingMemoryAllowance: working);

        Assert.True(cursor.MoveNext(out var first));
        Assert.True(cursor.MoveNext(out var second));
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(0UL, root.Used);
        Assert.Equal("for"u8.ToArray(), first.GetValueMemory().ToArray());
        Assert.Equal("and"u8.ToArray(), second.GetValueMemory().ToArray());
    }

    [Fact]
    public void ShortCursorTailUsesTheRequiredDelimiterWithinTheWorkBudget()
    {
        var subject = "earlier@x.y 😀é Ω 中 Sherlock Holmes and Watson, key: value 12.5 a@b.cd end"u8.ToArray();
        var working = new Account(new Root());
        using var regex = new Utf8Regex(@"[\w.+-]+@[\w-]+\.[\w.-]+", policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 64 }
        });
        using var cursor = regex.CreateMatchCursor(subject, startOffsetInBytes: "earlier@x.y 😀"u8.Length,
            workingMemoryAllowance: working);

        Assert.True(cursor.MoveNext(out var result));
        Assert.Equal("a@b.cd"u8.ToArray(), result.GetValueMemory().ToArray());
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(0UL, working.Used);
    }

    [Fact]
    public void ShortSuffixWindowKeepsTheOriginalBoundariesAndByteFloor()
    {
        var input = "😀walking Sherlock Holmes and Watson were quietly outside 😀starting! kingpin"u8.ToArray();
        using var regex = new Utf8Regex(@"\b[a-zA-Z]{0,12}ing\b", policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 128 }
        });
        using var cursor = regex.CreateMatchCursor(input, startOffsetInBytes: "😀wa"u8.Length);

        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal("starting"u8.ToArray(), match.GetValueMemory().ToArray());
        Assert.False(cursor.MoveNext(out _));
    }

    [Fact]
    public void CapturedDelimiterAlternativesUseTheExistingWindowWithinTheWorkBudget()
    {
        var input = "😀é Ω 中 Sherlock Holmes and Watson were quietly walking, key: value 12.5"u8.ToArray();
        var working = new Account(new Root());
        using var regex = new Utf8Regex(@"(\w+)\s+(Holmes|Watson)", policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 256 }
        });
        using var cursor = regex.CreateMatchCursor(input, startOffsetInBytes: "😀"u8.Length,
            workingMemoryAllowance: working);

        Assert.True(cursor.MoveNext(out var first));
        Assert.True(cursor.MoveNext(out var second));
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal("Sherlock"u8.ToArray(), first.GetGroupMemory(1).ToArray());
        Assert.Equal("Holmes"u8.ToArray(), first.GetGroupMemory(2).ToArray());
        Assert.Equal("and"u8.ToArray(), second.GetGroupMemory(1).ToArray());
        Assert.Equal("Watson"u8.ToArray(), second.GetGroupMemory(2).ToArray());
        Assert.Equal(0UL, working.Used);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FundedAssertionScratchRefundsOnEndOrCancellation(bool cancel)
    {
        using var regex = new Utf8Regex(@"(?<=(?<sep>\s))(?<word>[A-Z][a-z]+)(?=\s)");
        var root = new Root();
        var account = new Account(root);
        using var source = new CancellationTokenSource();
        using var cursor = regex.CreateMatchCursor("😀 Alpha Beta "u8.ToArray(),
            cancellationToken: source.Token, workingMemoryAllowance: account);
        Assert.True(cursor.MoveNext(out var first));
        Assert.True(account.Used > 0);
        Assert.True(cursor.MoveNext(out var second));
        Assert.Equal("Beta", Encoding.UTF8.GetString(second.GetValueMemory().Span));
        if (cancel)
        {
            source.Cancel();
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _)).CancellationToken);
        }
        else Assert.False(cursor.MoveNext(out _));
        Assert.Equal(0ul, root.Used);
        Assert.Equal("Alpha", Encoding.UTF8.GetString(first.GetValueMemory().Span));
        Assert.Equal(" ", Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
    }

    [Fact]
    public async Task SeparateOwnersKeepProgramAndResultFundedAcrossCloseAndAwaits()
    {
        var root = new Root(); var programs = new Account(root); var working = new Account(root); var results = new Account(root);
        var owner = Compile("(?<12>😀)+", programs); var programCharge = programs.Used;
        var number = owner.GroupNumberFromUtf8Name("12"u8);
        Assert.Equal(12, number);
        var cursor = owner.CreateMatchCursor("😀😀 😀"u8.ToArray(), workingMemoryAllowance: working);
        var alias = cursor;
        Assert.True(cursor.MoveNextRetained(out var retained, resultMemoryAllowance: results));
        Assert.True(results.Used > 0); var copied = retained.Match;
        Assert.Equal(2, copied.GetCaptures(number).Length);
        owner.Dispose(); Assert.Equal(programCharge, programs.Used);
        Assert.Throws<ObjectDisposedException>(() => owner.CreateMatchCursor("😀"u8.ToArray()));
        await Task.Yield();
        Assert.True(alias.MoveNext(out _)); Assert.False(cursor.MoveNext(out _));
        Assert.Equal(0UL, programs.Used); Assert.Equal(0UL, working.Used); Assert.Equal(retained.ChargedBytes, root.Used);
        cursor.Dispose(); Assert.Throws<ObjectDisposedException>(() => alias.MoveNext(out _));
        retained.Dispose(); retained.Dispose(); Assert.Equal(0UL, root.Used);
        Assert.Throws<ObjectDisposedException>(() => retained.Match);
        Assert.Equal("😀😀"u8.ToArray(), copied.GetValueMemory().ToArray());
        Assert.Equal([0, 12], copied.GroupNumbers.ToArray());
        Assert.Equal(2, copied.GetCaptures(number).Length);
        Assert.Equal("😀"u8.ToArray(), copied.GetGroupMemory(number).ToArray());
    }

    [Fact]
    public void RetainedAdmissionIgnoresCaptureNameLength()
    {
        var root = new Root();
        var working = new Account(root);
        var results = new Account(root);
        foreach (var name in new[] { "x", new string('é', 100) })
        {
            using var regex = new Utf8Regex($"(?<{name}>a)+");
            using var cursor = regex.CreateMatchCursor("aaa"u8.ToArray(), workingMemoryAllowance: working);
            Assert.True(cursor.MoveNextRetained(out var retained, resultMemoryAllowance: results));
            using (retained)
            {
                Assert.Equal(3, retained.Match.GetCaptures(1).Length);
                Assert.Equal(results.Used, retained.ChargedBytes);
                results.Maximum = retained.ChargedBytes;
            }
        }
        Assert.Equal(0UL, root.Used);
    }

    [Theory]
    [InlineData("(?<x>a)+", 3)]
    [InlineData("(?<x>a+)", 1)]
    public void ResultTransferAdmitsOverlapAndRefusalIsTerminal(string pattern, int captureCount)
    {
        var root = new Root(); var working = new Account(root); var results = new Account(root);
        using var cursor = new Utf8Regex(pattern).CreateMatchCursor("aaa aaa"u8.ToArray(), workingMemoryAllowance: working);
        var overlap = false; results.AfterGrant = () => overlap = working.Used > 0;
        Assert.True(cursor.MoveNextRetained(out var first, resultMemoryAllowance: results)); Assert.True(overlap);
        var held = results.Used; var attempts = results.Attempts; results.Maximum = held;
        Assert.Throws<Utf8MemoryAdmissionException>(() => cursor.MoveNextRetained(out _, resultMemoryAllowance: results));
        Assert.Equal(attempts + 1, results.Attempts); Assert.Equal(held, results.Used); Assert.Equal(0UL, working.Used);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
        Assert.Equal(captureCount, first.Match.GetCaptures(1).Length);
        Assert.Equal("aaa"u8.ToArray(), first.Match.GetValueMemory().ToArray()); first.Dispose(); Assert.Equal(0UL, root.Used);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CyclicRegularFirstCaptureNeedsNoBatchLoan(bool late)
    {
        var root = new Root(); var working = new Account(root) { Maximum = 8192 }; var results = new Account(root);
        using var regex = new Utf8Regex("(?<x>😀|a)+", RegexOptions.NonBacktracking);
        var input = Encoding.UTF8.GetBytes(late ? new string('z', 2048) + "😀a" : "😀a" + new string('z', 2048) + "a");
        var start = late ? 2048 : 0;
        using var cursor = regex.CreateMatchCursor(input, workingMemoryAllowance: working);
        var alias = cursor;

        Assert.True(cursor.MoveNextRetained(out var first, resultMemoryAllowance: results));
        using (first)
        {
            Assert.Equal(new Utf8CaptureRange(start, start + 5), first.Match.Value);
            Assert.Equal(new Utf8CaptureRange(start + 4, start + 5), Assert.Single(first.Match.GetCaptures(1).ToArray()));
            if (late) Assert.False(alias.MoveNext(out _));
            else
            {
                Assert.Throws<Utf8MemoryAdmissionException>(() => alias.MoveNext(out _));
                Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
            }
            Assert.Equal(0UL, working.Used);
            Assert.Equal("😀a"u8.ToArray(), first.Match.GetValueMemory().ToArray());
        }
        Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void CompactRegularCursorAdvancesAnEmptyMatchByOneScalar()
    {
        var root = new Root(); var working = new Account(root) { Maximum = 2048 };
        using var regex = new Utf8Regex("(?<x>a?)", RegexOptions.NonBacktracking);
        var input = Encoding.UTF8.GetBytes("é😀a" + new string('z', 256));
        using var cursor = regex.CreateMatchCursor(input, startOffsetInBytes: 2, workingMemoryAllowance: working);

        Assert.True(cursor.MoveNext(out var first));
        Assert.Equal(new Utf8CaptureRange(2, 2), first.Value);
        Assert.Equal(first.Value, Assert.Single(first.GetCaptures(1).ToArray()));
        Assert.True(cursor.MoveNext(out var second));
        Assert.Equal(new Utf8CaptureRange(6, 7), second.Value);
        Assert.Equal(second.Value, Assert.Single(second.GetCaptures(1).ToArray()));
        cursor.Dispose(); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void ForwardOnlyRegularCursorReusesBoundedScratchAndPreservesRetainedCaptures()
    {
        var root = new Root(); var working = new Account(root) { Maximum = 2048 }; var results = new Account(root);
        using var regex = new Utf8Regex("(?<x>😀ab|a){1,2}", RegexOptions.NonBacktracking);
        // The crossing result needs lookahead. Later windows must keep the
        // trailing 'b' and reject a different scalar with the same lead byte.
        var input = Encoding.UTF8.GetBytes("aa" + "ab" + new string('z', 61) + "😀aba" + new string('z', 8192)
            + "😀ab" + new string('z', 96) + "😁" + new string('z', 96) + "a");
        using var cursor = regex.CreateMatchCursor(input, workingMemoryAllowance: working);
        var alias = cursor;

        Assert.True(cursor.MoveNextRetained(out var first, resultMemoryAllowance: results));
        using (first)
        {
            Assert.Equal(new Utf8CaptureRange(0, 2), first.Match.Value);
            Assert.True(alias.MoveNext(out var seed));
            Assert.Equal(new Utf8CaptureRange(2, 3), seed.Value);
            Assert.True(cursor.MoveNext(out var crossing));
            Assert.Equal(new Utf8CaptureRange(65, 72), crossing.Value);
            Assert.Equal(new Utf8CaptureRange(71, 72), crossing.GetCaptures(1)[0]);
            Assert.True(alias.MoveNext(out var late));
            Assert.Equal(new Utf8CaptureRange(72 + 8192, 78 + 8192), late.Value);
            Assert.Equal(late.Value, late.GetCaptures(1)[0]);
            Assert.True(alias.MoveNext(out var last));
            Assert.Equal(new Utf8CaptureRange(input.Length - 1, input.Length), last.Value);
            Assert.False(cursor.MoveNext(out _));
            Assert.Equal(0UL, working.Used);
            Assert.Equal("aa"u8.ToArray(), first.Match.GetValueMemory().ToArray());
            Assert.Equal(new Utf8CaptureRange(1, 2), first.Match.GetCaptures(1)[0]);
        }
        Assert.Equal(0UL, root.Used);
    }

    [Theory]
    [InlineData("(?<x>😀|a){1,2}", 3)]
    [InlineData("(?<x>😀|a){0,2}", 3076)]
    public void ForwardOnlyRegularCountFitsBoundedWorkingAllowance(string pattern, int expected)
    {
        var root = new Root(); var working = new Account(root) { Maximum = 2048 };
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀a" + new string('z', 1024), 3)));
        using var regex = new Utf8Regex(pattern, RegexOptions.NonBacktracking);

        Assert.Equal(expected, regex.Count(input, workingMemoryAllowance: working));
        Assert.Equal(0UL, root.Used);
        working.Maximum = 128;
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.Count(input, workingMemoryAllowance: working));
        Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void SameOwnerReceiptTransfersWithoutAnotherAcquisition()
    {
        var root = new Root(); var working = new Account(root);
        using var cursor = new Utf8Regex("x").CreateMatchCursor("x"u8.ToArray(), workingMemoryAllowance: working);
        Assert.True(cursor.MoveNextRetained(out var result)); Assert.True(working.Used >= result.ChargedBytes);
        Assert.False(cursor.MoveNext(out _)); Assert.Equal(result.ChargedBytes, working.Used);
        var copy = result.Match; result.Dispose(); Assert.Equal(0UL, root.Used); Assert.True(copy.Success);
        Assert.True(copy.TryGetGroup(0, out var group));
        Assert.Equal((0, 1), (group.StartOffsetInBytes, group.EndOffsetInBytes));
        Assert.Equal(group, copy.GetCaptures(0)[0]);
        Assert.False(copy.TryGetGroup(1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = copy.GetCaptures(1); });
        Assert.False(default(Utf8RangeMatch).TryGetGroup(0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = default(Utf8RangeMatch).GetCaptures(0); });
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
        using var cursor = new Utf8Regex("(?<x>a)+", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(250)
        })            .CreateMatchCursor("aaa aaa"u8.ToArray(), workingMemoryAllowance: working);
        Assert.True(cursor.MoveNext(out var first)); await Task.Delay(350);
        var failure = Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _));
        Assert.Empty(failure.Input); Assert.Equal("(?<x>a)+", failure.Pattern);
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
        var regex = new Utf8Regex("(?=(?=(a)))a", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
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
        Assert.Equal(held, root.Used);
    }

    [Fact]
    public void LongIterationKeepsLiveFundingBoundedAndWorkIncremental()
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
        using var bounded = new Utf8Regex("x", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 50000
            }
        }).CreateMatchCursor(input);
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

    [Theory]
    [InlineData("x", RegexOptions.None, 1, 0)]
    [InlineData("(x)", RegexOptions.None, 2, 0)]
    [InlineData("(x)", RegexOptions.NonBacktracking, 2, 0)]
    [InlineData("(x)(y)?", RegexOptions.None, 2, 4)]
    public void ResultTrafficIsCumulativeEvenWhenEachPublishedLoanRefunds(
        string pattern, RegexOptions options, int ranges, int offsets)
    {
        var root = new Root(); var working = new Account(root);
        var resultBytes = Utf8ConstructionBudget.ArrayBytes<int>(offsets) + Utf8ConstructionBudget.ArrayBytes<Utf8CaptureRange>(ranges);
        using var cursor = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxResultBytes = 2 * resultBytes
            }
        }).CreateMatchCursor(Encoding.UTF8.GetBytes(new string('x', 80)), workingMemoryAllowance: working);
        Assert.True(cursor.MoveNext(out var first));
        Assert.True(cursor.MoveNext(out var second));
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext(out _));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
        Assert.Equal(0UL, root.Used);
        Assert.Equal(0, first.GetCaptures(0)[0].StartOffsetInBytes);
        Assert.Equal(1, second.GetCaptures(0)[0].StartOffsetInBytes);
        if (ranges > 1)
        {
            Assert.Equal(first.Value, Assert.Single(first.GetCaptures(1).ToArray()));
            Assert.Equal(second.Value, Assert.Single(second.GetCaptures(1).ToArray()));
        }
        if (offsets > 0)
        {
            Assert.True(first.TryGetGroup(2, out var unmatched));
            Assert.False(unmatched.Success);
            Assert.Empty(first.GetCaptures(2).ToArray());
        }
    }

    [Fact]
    public void SparseGroupNumbersReadCompleteAndPartialSnapshotsAfterDisposal()
    {
        var root = new Root();
        var working = new Account(root);
        using var regex = new Utf8Regex("(?<2>a)(?<7>b)?");
        using var cursor = regex.CreateMatchCursor("ab a"u8.ToArray(), workingMemoryAllowance: working);
        Assert.True(cursor.MoveNextRetained(out var receipt));
        var complete = receipt.Match;
        Assert.True(cursor.MoveNext(out var partial));
        cursor.Dispose();
        receipt.Dispose();

        Assert.Equal(0UL, root.Used);
        Assert.Equal(new[] { 0, 2, 7 }, complete.GroupNumbers.ToArray());
        Assert.Equal(new[] { 0, 2, 7 }, partial.GroupNumbers.ToArray());
        Assert.Equal("b"u8.ToArray(), complete.GetGroupMemory(7).ToArray());
        Assert.Equal(1, complete.GetCaptures(7)[0].StartOffsetInBytes);
        Assert.Equal(3, partial.GetCaptures(2)[0].StartOffsetInBytes);
        Assert.True(partial.TryGetGroup(7, out var missing));
        Assert.False(missing.Success);
        Assert.Empty(partial.GetCaptures(7).ToArray());
        Assert.False(complete.TryGetGroup(1, out _));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancellationAfterSeparateResultAdmissionRefundsBothChildrenBeforePublication(bool cancelOriginal)
    {
        var root = new Root(); var working = new Account(root); var results = new Account(root);
        using var original = new CancellationTokenSource(); using var current = new CancellationTokenSource();
        using var cursor = new Utf8Regex("(a+)").CreateMatchCursor("aaa"u8.ToArray(),
            cancellationToken: original.Token, workingMemoryAllowance: working);
        results.AfterGrant = (cancelOriginal ? original : current).Cancel;
        Assert.Equal((cancelOriginal ? original : current).Token, Assert.Throws<OperationCanceledException>(() =>
            cursor.MoveNextRetained(out _, current.Token, results)).CancellationToken);
        Assert.Equal(1, results.Attempts); Assert.Equal(0UL, root.Used);
    }

    [Fact]
    public void DeadlineAfterSeparateResultAdmissionRefundsBothChildrenBeforePublication()
    {
        var root = new Root(); var working = new Account(root); var results = new Account(root);
        using var clock = Utf8DeadlineTestClock.Start();
        using var cursor = new Utf8Regex("(a+)", policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(200)
        })            .CreateMatchCursor("aaa"u8.ToArray(), workingMemoryAllowance: working);
        results.AfterGrant = () => clock.Advance(250);
        Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNextRetained(out _, resultMemoryAllowance: results));
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
            // The test thread blocks on this handoff. A dedicated worker avoids
            // thread-pool starvation when the full suite runs concurrently.
            var advance = Task.Factory.StartNew(() => cursor.MoveNext(out _),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var matched = false;
            try
            {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
                Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
                Assert.Throws<InvalidOperationException>(cursor.Dispose);
            }
            finally { resume.Set(); matched = await advance; }
            Assert.True(matched);
        }
    }

    private static Utf8Regex Compile(string pattern, Account account) => Utf8Regex.Compile(pattern, options:         RegexOptions.None, policy: new Utf8RegexPolicy
    {
        MatchTimeout = Timeout.InfiniteTimeSpan,
        ResourcePolicy = default,
        ConstructionPolicy = default,
        ConstructionTimeout = Timeout.InfiniteTimeSpan,
        ProgramMemoryAllowance = account
    }, cancellationToken: default);

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
