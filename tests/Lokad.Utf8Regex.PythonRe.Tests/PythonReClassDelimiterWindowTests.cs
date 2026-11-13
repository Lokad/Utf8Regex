using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReClassDelimiterWindowTests
{
    [Theory]
    [InlineData("([a-z]+)-([0-9]+)", "xx-! good-12 tail", 0, 5, "good-12", "good", "12")]
    [InlineData("([a-z]{2,3})-([0-9]+)", "abcde-12 tail", 0, 2, "cde-12", "cde", "12")]
    [InlineData("([a-z]+)-([0-9]+)", "😀xx-! good-12 tail", 4, 9, "good-12", "good", "12")]
    public void FailedInitialCandidatesPreserveLaterStartsAndCaptureValues(string pattern, string text,
        int floor, int expectedStart, string expectedWhole, string first, string second)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var storage = new byte[bytes.Length + 7]; bytes.CopyTo(storage, 3);
        foreach (var finite in new[] { false, true })
        {
            using var token = new CancellationTokenSource();
            using var owner = new Utf8PythonRegex(pattern, default, Timeout.InfiniteTimeSpan,
                finite ? new() { MaxWorkSteps = 1_000_000, MaxDepth = 128,
                    MaxWorkingBytes = 1_000_000, MaxResultBytes = 1_000_000 } : default);
            var result = owner.SearchDetailedData(storage.AsSpan(3, bytes.Length), floor, token.Token);
            Assert.Equal(expectedStart, result.Value.StartOffsetInBytes);
            Assert.Equal(new[] { expectedWhole, first, second }, result.Groups.Select(group => group.ValueText));
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
                using var owner = new Utf8PythonRegex(pattern, default, Timeout.InfiniteTimeSpan,
                    finite ? new() { MaxWorkSteps = 1_000_000, MaxDepth = 4096,
                        MaxWorkingBytes = 1_000_000, MaxResultBytes = 1_000_000 } : default);
                Assert.Equal(oracle.Count(text), owner.Count(input));
                Assert.Equal(oracle.Count(suffix), owner.Count(input, Encoding.UTF8.GetByteCount(prefix)));
                var actual = owner.FindIterDetailed(input);
                var wanted = oracle.Matches(text).ToArray();
                Assert.Equal(wanted.Length, actual.Length);
                for (var index = 0; index < actual.Length; index++)
                {
                    Assert.Equal(wanted[index].Index, actual[index].Value.StartOffsetInUtf16);
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, wanted[index].Index)), actual[index].Value.StartOffsetInBytes);
                    Assert.Equal(wanted[index].Groups.Cast<Group>().Select(group => group.Value),
                        actual[index].Groups.Select(group => group.ValueText));
                }
            }
        }
    }

    [Fact]
    public void DirectWindowCountKeepsEveryWorkCutoffAndParentLoan()
    {
        var program = PythonReScalarCompiler.Compile(new PythonReParser(@"[a-z]+@z").Parse(default));
        var input = Encoding.UTF8.GetBytes(new string('x', 256) + " \t @bad ok@z");
        ulong Run(ulong cap)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 256, 1_000_000, 1), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            try
            {
                Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
                var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
                if (outcome.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                if (invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                Assert.Equal(1, outcome.Count);
                return invocation.Budget.Steps;
            }
            catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
                invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
            { throw invocation.Budget.CreateResourceFailure(); }
            finally { Assert.Equal(13UL, invocation.Ledger.ReservedBytes); }
        }
        var steps = Run(1_000_000);
        Assert.InRange(steps, 1UL, 1024UL);
        for (ulong cap = 1; cap < steps; cap++)
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(cap)).ResourceKind);
        Assert.Equal(steps, Run(steps));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void DirectWindowCountKeepsOriginalInterruption(bool timeout)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        using var owner = new Utf8PythonRegex(@"[a-z]+@z", default,
            timeout ? TimeSpan.FromTicks(1) : Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaaa \t @bad ", 512)) + "b@z");
        var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => { reached = true; if (timeout) ticks = 1000; else token.Cancel(); });
        try
        {
            var error = Record.Exception(() => owner.Count(input, 0, token.Token));
            Assert.True(reached);
            if (timeout) Assert.IsType<RegexMatchTimeoutException>(error);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(255, false)] [InlineData(256, false)] [InlineData(256, true)]
    [InlineData(2048, false)] [InlineData(2048, true)] [InlineData(4097, true)]
    public void PublicOperationsKeepCapturesAndUnicodePrefixContext(int prefix, bool finite)
    {
        const string pattern = "([a-z]+)-([0-9]+)";
        var text = "é😀" + new string('x', prefix) + " item-123 other-45";
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern);
        using var owner = new Utf8PythonRegex(pattern, default, Timeout.InfiniteTimeSpan,
            finite ? new() { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1_000_000,
                MaxResultBytes = 1_000_000, MaxDepth = 128 } : default);
        var first = owner.SearchDetailedData(input);
        var wanted = oracle.Match(text);
        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, wanted.Index)), first.Value.StartOffsetInBytes);
        Assert.Equal(wanted.Index, first.Value.StartOffsetInUtf16);
        Assert.Equal(new[] { "item-123", "item", "123" }, first.Groups.Select(g => g.ValueText));
        Assert.Equal(2, owner.Count(input));
        Assert.Equal(new[] { "item", "123", "other", "45" }, owner.FindAllToStrings(input).TupleValues.SelectMany(row => row));
        Assert.Equal(oracle.Replace(text, "$2:$1"), owner.ReplaceToString(input, @"\2:\1"));
        Assert.Equal(oracle.Split(text), owner.SplitToStrings(input));
        var floor = Encoding.UTF8.GetByteCount("é😀") + prefix + 1;
        Assert.True(owner.Match(input, floor).Success);
        Assert.False(owner.Match(input).Success);
        Assert.False(owner.FullMatch(input, floor).Success);
        Assert.Equal("item", first.Groups[1].ValueText);
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
        var actual = owner.FindIterDetailed(input);
        Assert.Equal(wanted.Length, actual.Length);
        for (var i = 0; i < wanted.Length; i++)
        {
            Assert.Equal(wanted[i].Index, actual[i].Value.StartOffsetInBytes);
            Assert.Equal(wanted[i].Groups.Cast<Group>().Select(g => g.Value), actual[i].Groups.Select(g => g.ValueText));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    [InlineData(246)] [InlineData(247)] [InlineData(248)] [InlineData(512)]
    public void EveryWorkCutoffRetainsTheCallerLoanAndRefusesPartialSuccess(int prefix)
    {
        var program = PythonReScalarCompiler.Compile(new PythonReParser("([a-z]+)-([0-9]+)").Parse(default));
        var input = Encoding.UTF8.GetBytes(new string('x', prefix) + " item-123");
        ulong Run(ulong cap)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 128, 1_000_000, 1), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var state = new PythonReScalarMatchState(input, program, 0, Timeout.InfiniteTimeSpan, "pattern", invocation, null);
            try
            {
                Assert.True(state.MoveNext(input));
                state.Checkpoint();
                return state.WorkingInvocation.Budget.Steps;
            }
            finally { state.Dispose(); Assert.Equal(13UL, state.ReservedBytes); }
        }
        var steps = Run(1_000_000);
        Assert.InRange(steps, 1UL, 1024UL);
        for (ulong cap = 1; cap < steps; cap++)
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(cap)).ResourceKind);
        Assert.Equal(steps, Run(steps));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void WindowSearchKeepsOriginalCancellationAndDeadline(bool timeout)
    {
        var program = PythonReScalarCompiler.Compile(new PythonReParser("([a-z]+)-([0-9]+)").Parse(default));
        var input = Encoding.UTF8.GetBytes(new string('x', 262144) + " item-123");
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var duration = timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan;
        var invocation = new Utf8SliceInvocation(new(1_000_000, 128, 1_000_000, 1), duration, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var state = new PythonReScalarMatchState(input, program, 0, duration, "pattern", invocation, null);
        var reached = false;
        state.WorkingInvocation.CheckpointHook = _ => { reached = true; if (timeout) ticks = 1000; else token.Cancel(); };
        try
        {
            var error = Record.Exception(() => state.MoveNext(input));
            Assert.True(reached);
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        }
        finally { state.Dispose(); Assert.Equal(13UL, state.ReservedBytes); }
    }

    [Fact]
    public void ScalarFloorsAndOriginalCancellationPrecedeWindowSearch()
    {
        using var owner = new Utf8PythonRegex("([a-z]+)-([0-9]+)");
        var valid = Encoding.UTF8.GetBytes("😀" + new string('x', 256) + " item-123");
        Assert.Throws<ArgumentOutOfRangeException>(() => owner.Search(valid, 1));
        Assert.Equal("😀" + new string('x', 256) + " 123:item", owner.ReplaceToString(valid, @"\2:\1"));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => owner.Search(valid, 0, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(15)] [InlineData(16)] [InlineData(17)]
    [InlineData(255)] [InlineData(256)] [InlineData(257)]
    public void SubjectGateKeepsDetailedCapturesLaterFloorsAndAnchoredOperations(int length)
    {
        var text = new string('x', length - 7) + " item-1";
        var bytes = Encoding.UTF8.GetBytes(text); var storage = new byte[bytes.Length + 7]; bytes.CopyTo(storage, 3);
        var input = storage.AsSpan(3, bytes.Length); var at = length - 6;
        for (var mode = 0; mode < 3; mode++)
        {
            using var token = new CancellationTokenSource();
            using var owner = new Utf8PythonRegex("([a-z]+)-([0-9]+)", default,
                mode == 2 ? TimeSpan.FromSeconds(10) : Timeout.InfiniteTimeSpan,
                mode == 0 ? default : new() { MaxWorkSteps = 1_000_000, MaxDepth = 128,
                    MaxWorkingBytes = 1_000_000, MaxResultBytes = 1_000_000 });
            var first = owner.SearchDetailedData(input, 0, token.Token);
            Assert.Equal((at, length), (first.Value.StartOffsetInBytes, first.Value.EndOffsetInBytes));
            Assert.Equal(new[] { "item-1", "item", "1" }, first.Groups.Select(group => group.ValueText));
            Assert.Equal(at + 1, owner.Search(input, at + 1, token.Token).StartOffsetInBytes);
            Assert.False(owner.Match(input).Success);
            Assert.False(owner.FullMatch(input).Success);
        }
    }

    [Fact]
    public void ShortWindowAliasesKeepOwnedSnapshotsAfterAdvanceAndDisposal()
    {
        var input = "prefix item-123 other-45"u8.ToArray();
        using var owner = new Utf8PythonRegex("(?P<n>[a-z]+)-([0-9]+)");
        var cursor = owner.CreateMatchCursor(input); var alias = cursor;
        try
        {
            Assert.True(cursor.MoveNext()); var first = alias.Current;
            Assert.True(alias.MoveNext()); var second = cursor.Current;
            Assert.False(cursor.MoveNext()); alias.Dispose();
            input.AsSpan().Fill((byte)'x');
            Assert.Equal("item-123", first.Value.ValueText);
            Assert.Equal("other-45", second.Value.ValueText);
            first.Groups[1] = default; first.NameEntries[0] = default;
            Assert.True(second.TryGetFirstSetGroup("n", out var group));
            Assert.Equal("other", group.ValueText);
        }
        finally { cursor.Dispose(); }
    }
}
