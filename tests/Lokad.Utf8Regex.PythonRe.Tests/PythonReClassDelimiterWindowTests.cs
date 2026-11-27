using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReClassDelimiterWindowTests
{
    [Theory]
    [InlineData("Today is 11/18/201A and tomorrow is 11/19/201A.", "")]
    [InlineData("Today is 11/18/2019.", "11/18/2019")]
    [InlineData("😀 11/18/201A then 𝟙𝟚/١٨/٢٠١٩.", "𝟙𝟚/١٨/٢٠١٩")]
    [InlineData("Today is 11/18/201\U0001E5F1!", "11/18/201")]
    [InlineData("Today is 11/18/201é!", "")]
    public void BoundedDateSearchWithAsciiAndPinnedUnicodeBoundaries(string text, string expected)
    {
        const string pattern = @"\b\d{1,2}/\d{1,2}/\d{2,4}\b";
        var input = Encoding.UTF8.GetBytes(text);
        using var cancellation = new CancellationTokenSource();
        using var ordinary = new Utf8PythonRegex(pattern);
        using var funded = new Utf8PythonRegex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxDepth = 32, MaxWorkingBytes = 4096 }
        });

        Assert.Equal(expected.Length != 0, ordinary.IsMatch(input));
        Assert.Equal(expected.Length != 0, funded.IsMatch(input, cancellationToken: cancellation.Token));
        var match = funded.Match(input, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.Search);
        Assert.Equal(expected.Length != 0, match.Success);
        if (match.Success) Assert.Equal(expected, Encoding.UTF8.GetString(match.GetValueBytes()));
    }

    [Theory]
    [InlineData("([a-z]+)-([0-9]+)", "xx-! good-12 tail", 0, 5, "good-12", "good", "12")]
    [InlineData("([a-z]{2,3})-([0-9]+)", "abcde-12 tail", 0, 2, "cde-12", "cde", "12")]
    [InlineData("([a-z]+)-([0-9]+)", "😀xx-! good-12 tail", 4, 9, "good-12", "good", "12")]
    public void FailedInitialCandidatesPreserveLaterStartsAndCaptureValues(string pattern, string text,
        int floor, int expectedStart, string expectedWhole, string first, string second)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var storage = new byte[bytes.Length + 7];
        bytes.CopyTo(storage, 3);
        foreach (var finite in new[] { false, true })
        {
            using var token = new CancellationTokenSource();
            using var owner = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = finite ? new()
                {
                    MaxDepth = 128,
                    MaxWorkingBytes = 1_000_000,
                    MaxResultBytes = 1_000_000
                }

    : default
            });
            Utf8PythonRangeMatch result = default;
            using (var captureCursor1 = owner.CreateMatchCursor(storage.AsMemory(3, bytes.Length), floor, token.Token, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor1.MoveNext(out result);
            }
            Assert.Equal(expectedStart, result.Value.StartOffsetInBytes);
            Assert.Equal(new[] { expectedWhole, first, second }, PythonReCaptureAssert.Values(result));
        }
    }

    [Theory]
    [InlineData(@"[a-z]+@z", "abc \t @z def@z")]
    [InlineData(@"[a-z]+ z", "abc \t  z abc z")]
    [InlineData(@"[a ]+ z", "a\t  z a a z")]
    [InlineData(@"[a ]+@z", "a\t @z a a@z")]
    [InlineData(@"[a\t]+@z", "a \t@z a\t@z")]
    [InlineData(@"[\s\w]+@z", "a\t @z b @bad c@z")]
    [InlineData(@"[a-z]{2,3}@z", "abcde@z a@z @z")]
    [InlineData(@"[a-z]{1,3}?@z", "abcde@z a@z @z")]
    [InlineData(@"[^\s]+@z", "a@bad@z b@z")]
    [InlineData(@"[^ \t]+@z", "a@bad@z b@z")]
    [InlineData(@"[a@]+@z", "aaa@@z aa@z a@bad@z")]
    [InlineData(@".+@z", "abc\n @z def@z")]
    [InlineData(@"[a-z]+\s+@z", "abc \t @z def@z")]
    [InlineData(@"[a-z]+\s*.{0,2}@z", "abc \t ?@z uvw@z")]
    [InlineData(@"([a-z]+)@\1", "ababc@abc def@def")]
    [InlineData(@"[a-z]+@([a-z]+)-\1", "abc \t @x-x abc@x-y uvw@ab-ab")]
    [InlineData(@"[a-z]+@(?<=ab@)[0-9]+", "zzab@12 ab@3 \t @4")]
    [InlineData(@"[\w.+-]+@[\w.-]+\.[\w.-]+", "abc \t @mention abc@bad abc@x.y é@x.y")]
    public void DirectAndExplicitWhitespaceWindowsKeepAllPossibleStarts(string pattern, string suffix)
    {
        foreach (var prefix in new[] { "é😀 ", "é😀" + new string('x', 256) + " " })
        {
            var text = prefix + suffix;
            var input = Encoding.UTF8.GetBytes(text);
            var oracle = new Regex(pattern);
            foreach (var finite in new[] { false, true })
            {
                using var owner = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan,
                    ResourcePolicy = finite ? new()
                    {
                        MaxDepth = 4096,
                        MaxWorkingBytes = 1_000_000,
                        MaxResultBytes = 1_000_000
                    }

    : default
                });
                Assert.Equal(oracle.Count(text), owner.Count(input));
                Assert.Equal(oracle.Count(suffix), owner.Count(input, Encoding.UTF8.GetByteCount(prefix)));
                var collectedRanges1 = new List<Utf8PythonRangeMatch>();
                using (var rangeCursor1 = owner.CreateMatchCursor(input, 0, default))
                {
                    while (rangeCursor1.MoveNext(out var rangeMatch1))
                        collectedRanges1.Add(rangeMatch1);
                }
                var actual = collectedRanges1.ToArray();
                var wanted = oracle.Matches(text).ToArray();
                Assert.Equal(wanted.Length, actual.Length);
                for (var index = 0; index < actual.Length; index++)
                {
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, wanted[index].Index)), actual[index].Value.StartOffsetInBytes);
                    Assert.Equal(wanted[index].Groups.Cast<Group>().Select(group => group.Value),
                        actual[index].Groups.ToArray().Select(group => System.Text.Encoding.UTF8.GetString(actual[index].GetGroupMemory(group.Number).Span)));
                }
            }
        }
    }

    [Fact]
    public void DirectWindowCountKeepsEveryWorkCutoffAndParentLoan()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var program = PythonReScalarCompiler.Compile(new PythonReParser(@"[a-z]+@z").Parse(default));
        var input = Encoding.UTF8.GetBytes(new string('x', 256) + " \t @bad ok@z");
        ulong Run(ulong cap)
        {
            var invocation = new Utf8SliceInvocation(new(256, 1_000_000, 1), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            try
            {
                var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
                if (outcome.Breach != Utf8ExecutionLimitKind.None)
                {
                    throw invocation.Budget.CreateResourceFailure();
                }
                if (invocation.CheckInput() != Utf8ExecutionLimitKind.None)
                {
                    throw invocation.Budget.CreateResourceFailure();
                }
                Assert.Equal(1, outcome.Count);
                return invocation.Budget.DiagnosticCheckpoints;
            }
            catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
                invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
            {
                throw invocation.Budget.CreateResourceFailure();
            }
            finally
            {
                Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            }
        }
        var steps = Run(1_000_000);
        Assert.InRange(steps, 1UL, 1024UL);
        Assert.Equal(steps, Run(steps));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectWindowCountKeepsOriginalInterruption(bool timeout)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        using var owner = new Utf8PythonRegex(@"[a-z]+@z", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = timeout ? TimeSpan.FromTicks(1) : Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaaa \t @bad ", 512)) + "b@z");
        var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            reached = true;
            if (timeout)
            {
                ticks = 1000;
            }
            else
            {
                token.Cancel();
            }
        });
        try
        {
            var error = Record.Exception(() => owner.Count(input, 0, token.Token));
            Assert.True(reached);
            if (timeout)
            {
                Assert.IsType<RegexMatchTimeoutException>(error);
            }
            else
            {
                Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
            }
        }
        finally
        {
            session.Complete();
        }
    }

    [Theory]
    [InlineData(255, false)]
    [InlineData(256, false)]
    [InlineData(256, true)]
    [InlineData(2048, false)]
    [InlineData(2048, true)]
    [InlineData(4097, true)]
    public void PublicOperationsKeepCapturesAndUnicodePrefixContext(int prefix, bool finite)
    {
        const string pattern = "([a-z]+)-([0-9]+)";
        var text = "é😀" + new string('x', prefix) + " item-123 other-45";
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern);
        using var owner = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = finite ? new()
            {
                MaxWorkingBytes = 1_000_000,
                MaxResultBytes = 1_000_000,
                MaxDepth = 128
            }

    : default
        });
        Utf8PythonRangeMatch first = default;
        using (var captureCursor2 = owner.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor2.MoveNext(out first);
        }
        var wanted = oracle.Match(text);
        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, wanted.Index)), first.Value.StartOffsetInBytes);
        Assert.Equal(new[] { "item-123", "item", "123" }, PythonReCaptureAssert.Values(first));
        Assert.Equal(2, owner.Count(input));
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = owner.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(new[] { "item", "123", "other", "45" }, snapshots1.Select(captureSnapshot1 => Enumerable.Range(1, captureSnapshot1.Groups.Length - 1).Select(captureNumber1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureNumber1).Span)).ToArray()).ToArray().SelectMany(row => row));
        Assert.Equal(oracle.Replace(text, "$2:$1"), System.Text.Encoding.UTF8.GetString(owner.Replace(input, @"\2:\1"u8)));
        Assert.Equal(oracle.Split(text), PythonReSplitAssert.Decode(input, owner.SplitRanges(input, 0, 0, default)));
        var floor = Encoding.UTF8.GetByteCount("é😀") + prefix + 1;
        Assert.True(owner.Match(input, floor).Success);
        Assert.False(owner.Match(input).Success);
        Assert.False(owner.Match(input, floor, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal("item", System.Text.Encoding.UTF8.GetString((first.Groups[1].Success ? first.GetGroupMemory(first.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("(?P<x>[a-z]+)-(?P=x)", @"(?<x>[a-z]+)-\k<x>", "abab-ab")]
    [InlineData("([a-z]{2,3})-([0-9]{1,2})", "([a-z]{2,3})-([0-9]{1,2})", "abcde-123 gh-56")]
    [InlineData("([a-z]+)a([0-9]+)", "([a-z]+)a([0-9]+)", "xxaa12 ba3")]
    [InlineData("([a-z]+)-(?<=ab-)([0-9]+)", "([a-z]+)-(?<=ab-)([0-9]+)", "zzab-42")]
    [InlineData("(?<=:)([a-z]+)-([0-9]+)", "(?<=:)([a-z]+)-([0-9]+)", ":item-123")]
    [InlineData("([a-z]+?)-([0-9]+)", "([a-z]+?)-([0-9]+)", "item-123")]
    [InlineData("([a-z]+)?-([0-9]+)", "([a-z]+)?-([0-9]+)", "item-123 -45")]
    [InlineData("(?i:([a-z]+))-([0-9]+)", "(?i:([a-z]+))-([0-9]+)", "ITEM-123")]
    [InlineData("([a-z]+)[-:]([0-9]+)", "([a-z]+)[-:]([0-9]+)", "item:123")]
    public void NecessaryWindowsKeepLaterStartsAndUnprovedShapes(string pattern, string reference, string suffix)
    {
        var text = new string('x', 256) + " " + suffix;
        var input = Encoding.UTF8.GetBytes(text);
        using var owner = new Utf8PythonRegex(pattern);
        var oracle = new Regex(reference);
        var wanted = oracle.Matches(text).ToArray();
        var collectedRanges2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = owner.CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
                collectedRanges2.Add(rangeMatch2);
        }
        var actual = collectedRanges2.ToArray();
        Assert.Equal(wanted.Length, actual.Length);
        for (var i = 0; i < wanted.Length; i++)
        {
            Assert.Equal(wanted[i].Index, actual[i].Value.StartOffsetInBytes);
            Assert.Equal(wanted[i].Groups.Cast<Group>().Select(g => g.Value), actual[i].Groups.ToArray().Select(g => System.Text.Encoding.UTF8.GetString(actual[i].GetGroupMemory(g.Number).Span)));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(246)]
    [InlineData(247)]
    [InlineData(248)]
    [InlineData(512)]
    public void EveryWorkCutoffRetainsTheCallerLoanAndRefusesPartialSuccess(int prefix)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var program = PythonReScalarCompiler.Compile(new PythonReParser("([a-z]+)-([0-9]+)").Parse(default));
        var input = Encoding.UTF8.GetBytes(new string('x', prefix) + " item-123");
        ulong Run(ulong cap)
        {
            var invocation = new Utf8SliceInvocation(new(128, 1_000_000, 1), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            PythonReScalarMatchState.Initialize(out var state, input, program, 0, Timeout.InfiniteTimeSpan, "pattern", invocation, null);
            try
            {
                Assert.True(state.MoveNext(input));
                state.Checkpoint();
                return state.WorkingInvocation.Budget.DiagnosticCheckpoints;
            }
            finally
            {
                state.Dispose();
                Assert.Equal(13UL, state.ReservedBytes);
            }
        }
        var steps = Run(1_000_000);
        Assert.InRange(steps, 1UL, 1024UL);
        Assert.Equal(steps, Run(steps));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WindowSearchKeepsOriginalCancellationAndDeadline(bool timeout)
    {
        var program = PythonReScalarCompiler.Compile(new PythonReParser("([a-z]+)-([0-9]+)").Parse(default));
        var input = Encoding.UTF8.GetBytes(new string('x', 262144) + " item-123");
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var duration = timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan;
        var invocation = new Utf8SliceInvocation(new(128, 1_000_000, 1), duration, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        PythonReScalarMatchState.Initialize(out var state, input, program, 0, duration, "pattern", invocation, null);
        var reached = false;
        state.WorkingInvocation.CheckpointHook = _ =>
        {
            reached = true;
            if (timeout)
            {
                ticks = 1000;
            }
            else
            {
                token.Cancel();
            }
        };
        try
        {
            var error = Record.Exception(() => state.MoveNext(input));
            Assert.True(reached);
            if (timeout)
            {
                Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
            }
            else
            {
                Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
            }
        }
        finally
        {
            state.Dispose();
            Assert.Equal(13UL, state.ReservedBytes);
        }
    }

    [Fact]
    public void ScalarFloorsAndOriginalCancellationPrecedeWindowSearch()
    {
        using var owner = new Utf8PythonRegex("([a-z]+)-([0-9]+)");
        var valid = Encoding.UTF8.GetBytes("😀" + new string('x', 256) + " item-123");
        Assert.Throws<ArgumentOutOfRangeException>(() => owner.Match(valid, 1, mode: Utf8PythonMatchMode.Search));
        Assert.Equal("😀" + new string('x', 256) + " 123:item", System.Text.Encoding.UTF8.GetString(owner.Replace(valid, @"\2:\1"u8)));
        using var token = new CancellationTokenSource();
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => owner.Match(valid, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search)).CancellationToken);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    public void SubjectGateKeepsDetailedCapturesLaterFloorsAndAnchoredOperations(int length)
    {
        var text = new string('x', length - 7) + " item-1";
        var bytes = Encoding.UTF8.GetBytes(text);
        var storage = new byte[bytes.Length + 7];
        bytes.CopyTo(storage, 3);
        var input = storage.AsSpan(3, bytes.Length);
        var at = length - 6;
        for (var mode = 0; mode < 3; mode++)
        {
            using var token = new CancellationTokenSource();
            using var owner = new Utf8PythonRegex("([a-z]+)-([0-9]+)", options: default, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = mode == 2 ? TimeSpan.FromSeconds(10) : Timeout.InfiniteTimeSpan,
                ResourcePolicy = mode == 0 ? default : new()
                {
                    MaxDepth = 128,
                    MaxWorkingBytes = 1_000_000,
                    MaxResultBytes = 1_000_000
                }
            });
            Utf8PythonRangeMatch first = default;
            using (var captureCursor3 = owner.CreateMatchCursor((input).ToArray(), 0, token.Token, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor3.MoveNext(out first);
            }
            Assert.Equal((at, length), (first.Value.StartOffsetInBytes, first.Value.EndOffsetInBytes));
            Assert.Equal(new[] { "item-1", "item", "1" }, PythonReCaptureAssert.Values(first));
            Assert.Equal(at + 1, owner.Match(input, at + 1, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
            Assert.False(owner.Match(input).Success);
            Assert.False(owner.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        }
    }

    [Fact]
    public void ShortWindowAliasesKeepOwnedSnapshotsAfterAdvanceAndDisposal()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = "prefix item-123 other-45"u8.ToArray();
        using var owner = new Utf8PythonRegex("(?P<n>[a-z]+)-([0-9]+)");
        var cursor = owner.CreateMatchCursor(input);
        var alias = cursor;
        try
        {
            Assert.True(cursor.MoveNext(out cursorRange));
            var first = cursorRange;
            Assert.True(alias.MoveNext(out cursorRange));
            var second = cursorRange;
            Assert.False(cursor.MoveNext(out cursorRange));
            alias.Dispose();

            Assert.Equal("item-123", System.Text.Encoding.UTF8.GetString(first.GetValueMemory().Span));
            Assert.Equal("other-45", System.Text.Encoding.UTF8.GetString(second.GetValueMemory().Span));
            Assert.True(second.TryGetGroup(1, out var group));
            Assert.Equal("other", System.Text.Encoding.UTF8.GetString((group.Success ? second.GetGroupMemory(group.Number) : default).Span));
        }
        finally
        {
            cursor.Dispose();
        }
    }
}
