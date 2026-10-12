using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2SliceCallbackAuthorityTests
{
    private static Utf8Pcre2Regex Create(Utf8Pcre2ExecutionLimits limits, TimeSpan? timeout = null)
    {
        var regex = new Utf8Pcre2Regex("(é)(😀)", Pcre2CompileOptions.None, default, limits,
            timeout ?? Timeout.InfiniteTimeSpan);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        return regex;
    }

    private sealed class Observation
    {
        internal Utf8SliceByteOutputScope? Scope;
        internal string Fragment = "!";
        internal int Calls;
        internal readonly List<ulong> Steps = [];
        internal readonly List<Pcre2GroupData> Captures = [];
        internal InvalidOperationException? Failure;
    }

    private static void Observe(in Utf8Pcre2MatchContext match, Observation state)
    {
        var scope = Utf8SliceByteOutputScope.Current;
        Assert.NotNull(scope);
        if (state.Scope is not null) Assert.Same(state.Scope, scope);
        state.Scope = scope;
        state.Calls++;
        state.Steps.Add(scope.Invocation.Budget.Steps);
        state.Captures.Add(match.GetGroup(2)._data);
    }

    private static string Run(Utf8Pcre2Regex regex, byte[] input, Observation state, bool text,
        CancellationToken token, int start = 0)
    {
        try
        {
            return text
                ? regex.ReplaceToString(input, state, static (in Utf8Pcre2MatchContext match, ref Observation state) =>
                {
                    Observe(in match, state);
                    return state.Fragment + match.GetGroup(2).GetValueString();
                }, start, Pcre2MatchOptions.None, token)
                : Encoding.UTF8.GetString(regex.Replace(input, state,
                    static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref Observation state) =>
                    {
                        Observe(in match, state);
                        writer.Append(state.Fragment.AsSpan());
                        writer.Append(match.GetGroup(2).GetValueBytes());
                    }, start, Pcre2MatchOptions.None, token));
        }
        finally
        {
            if (state.Scope is not null) Assert.Equal(0UL, state.Scope.Invocation.Ledger.ReservedBytes);
            Assert.Null(Utf8SliceByteOutputScope.Current);
        }
    }

    public static IEnumerable<object[]> PublicCutoffs()
    {
        foreach (var working in new[] { false, true })
        foreach (var text in new[] { false, true })
        foreach (var hit in new[] { false, true })
        foreach (var liveToken in new[] { false, true })
            yield return [working, text, hit, liveToken];
    }

    [Theory]
    [MemberData(nameof(PublicCutoffs))]
    public void PublicWorkAndHeapCutoffsCoverDiscoveryReplayAndFinalOutput(bool working, bool text, bool hit, bool liveToken)
    {
        using var source = new CancellationTokenSource();
        var token = liveToken ? source.Token : CancellationToken.None;
        var input = Encoding.UTF8.GetBytes(hit ? "é😀é😀" : new string('q', 8192));
        var fragment = new string('a', 4097);
        var expected = hit ? fragment + "😀" + fragment + "😀" : Encoding.UTF8.GetString(input);
        string Invoke(ulong cap)
        {
            var state = new Observation { Fragment = fragment };
            return Run(Create(new() { MatchLimit = working ? 0 : (uint)cap, HeapLimitInBytes = working ? cap : 0 }),
                input, state, text, token);
        }
        var kind = working ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit;
        Assert.Equal(expected, Invoke(working ? ulong.MaxValue : uint.MaxValue));
        Assert.Equal(kind, Assert.Throws<Pcre2MatchException>(() => Invoke(1)).ErrorKind);
        ulong first = 1, last = 1UL << 20;
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try { Assert.Equal(expected, Invoke(cap)); last = cap; }
            catch (Pcre2MatchException failure) { Assert.Equal(kind, failure.ErrorKind); first = cap + 1; }
        }
        Assert.InRange(first, 2UL, (1UL << 20) - 1);
        Assert.Equal(expected, Invoke(first));
        Assert.Equal(kind, Assert.Throws<Pcre2MatchException>(() => Invoke(first - 1)).ErrorKind);
        Assert.Equal(expected, Invoke(first));
    }

    [Theory]
    [InlineData("xé😀xé😀β", 0, "x!😀x!😀β")]
    [InlineData("é😀xé😀β", 7, "é😀x!😀β")]
    [InlineData("nothing😀", 0, "nothing😀")]
    [InlineData("", 0, "")]
    public void ByteAndTextCallbacksPreserveOffsetsAndOwnedResults(string input, int start, string expected)
    {
        foreach (var text in new[] { false, true })
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var actual = Run(Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }),
                bytes, new Observation(), text, CancellationToken.None, start);
            Assert.Equal(expected, actual);
            bytes.AsSpan().Fill((byte)'q');
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicCallbackSeesDiscoveryAndReplayInItsOriginalAuthority(bool liveToken)
    {
        using var source = new CancellationTokenSource();
        var token = liveToken ? source.Token : CancellationToken.None;
        var regex = Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 });
        var direct = (Pcre2SliceDirectProgram)regex.DebugCompiledProgram.Operations.Replace;
        var input = "é😀é😀"u8.ToArray();
        var control = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, Utf8ValidatedInput.Create(input),
            new Utf8BytePosition(0), regex.DebugCompiledProgram.Request, token);
        Assert.True(control.MoveNext());
        _ = control.CaptureGroupsAt(0, true);
        var discoverySteps = control.WorkingInvocation.Budget.Steps;
        var discoveryPeak = control.WorkingInvocation.Budget.PeakWorkingBytes;
        var state = new Observation();
        Assert.Equal("!😀!😀", Run(regex, input, state, false, token));
        Assert.Equal(2, state.Calls);
        Assert.NotNull(state.Scope);
        Assert.Equal(1000000UL, state.Scope.Invocation.Budget.Limits.MaxWorkSteps);
        Assert.Equal(1000000UL, state.Scope.Invocation.Budget.Limits.MaxWorkingBytes);
        Assert.True(state.Steps[0] > discoverySteps);
        Assert.True(state.Steps[1] > state.Steps[0]);
        Assert.True(state.Scope.Invocation.Budget.Steps > state.Steps[1]);
        Assert.True(state.Scope.Invocation.Budget.PeakWorkingBytes >= discoveryPeak + 2 * Utf8SliceByteOutputScope.ControlBytes);
        Assert.Equal((2, 6), (state.Captures[0].StartOffsetInBytes, state.Captures[0].EndOffsetInBytes));
        Assert.Equal((8, 12), (state.Captures[1].StartOffsetInBytes, state.Captures[1].EndOffsetInBytes));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BorrowedCursorFailureIsStickyInOutputAndReturnsEveryLoan(bool working)
    {
        var regex = Create(default);
        var direct = (Pcre2SliceDirectProgram)regex.DebugCompiledProgram.Operations.Replace;
        var invocation = new Utf8SliceInvocation(new(working ? 0UL : 3UL, 0, working ? 512UL : 1000000UL),
            Timeout.InfiniteTimeSpan, CancellationToken.None);
        invocation.ShareStorageForCursor();
        var scope = Utf8SliceByteOutputScope.Enter(invocation);
        try
        {
            var buffer = scope.CreateBuffer();
            var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, scope.Invocation,
                Utf8ValidatedInput.Create("é😀"u8), new Utf8BytePosition(0), regex.DebugCompiledProgram.Request);
            Pcre2MatchException? failure = null;
            try { cursor.MoveNext(); }
            catch (Pcre2MatchException error) { failure = error; }
            Assert.Equal(working ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit,
                Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            Assert.Equal(working ? Utf8ExecutionLimitKind.WorkingBytes : Utf8ExecutionLimitKind.WorkSteps,
                invocation.Budget.LimitBreach);
            Assert.Equal(invocation.Budget.Steps, scope.Invocation.Budget.Steps);
            Assert.Equal(512UL, invocation.Ledger.ReservedBytes);
            failure = null;
            try { cursor.MoveNext(); }
            catch (Pcre2MatchException error) { failure = error; }
            Assert.Equal(working ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit,
                Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            Assert.Throws<InvalidOperationException>(() => buffer.Append("!"u8));
        }
        finally { scope.Release(); scope.Release(); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BorrowingKeepsOriginalDeadlineAndTokenAheadOfHeapDenial(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = Create(default, TimeSpan.FromMilliseconds(20));
        var direct = (Pcre2SliceDirectProgram)regex.DebugCompiledProgram.Operations.Replace;
        var invocation = new Utf8SliceInvocation(new(0, 0, 512), TimeSpan.FromMilliseconds(20), source.Token);
        invocation.ShareStorageForCursor();
        ticks = 15;
        var scope = Utf8SliceByteOutputScope.Enter(invocation);
        try
        {
            scope.CreateBuffer();
            var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, scope.Invocation,
                Utf8ValidatedInput.Create("é😀"u8), new Utf8BytePosition(0), regex.DebugCompiledProgram.Request);
            Assert.Same(invocation.Budget.Deadline, cursor.WorkingInvocation.Budget.Deadline);
            if (cancel) source.Cancel(); else ticks = 25;
            try { cursor.MoveNext(); Assert.Fail("Original authority must fail before slot admission."); }
            catch (OperationCanceledException error) { Assert.True(cancel); Assert.Equal(source.Token, error.CancellationToken); }
            catch (Pcre2MatchException error) { Assert.False(cancel); Assert.Equal(Pcre2ErrorKind.Timeout, error.ErrorKind); }
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.Budget.LimitBreach);
            Assert.Equal(512UL, invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NestedReplacementAliasesAndCallerExceptionsPreserveScopeOwnership(bool fail)
    {
        var state = new Observation { Failure = fail ? new InvalidOperationException("caller failure") : null };
        byte[] Invoke() => Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }).Replace("é😀β"u8, state,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref Observation state) =>
            {
                Observe(in match, state);
                var scope = state.Scope!;
                var steps = scope.Invocation.Budget.Steps;
                var bytes = scope.Invocation.Ledger.ReservedBytes;
                var nested = new Utf8Pcre2Regex("(x)").ReplaceToString("x"u8, 0,
                    static (in Utf8Pcre2MatchContext match, ref int state) => "𐐨");
                Assert.Same(scope, Utf8SliceByteOutputScope.Current);
                Assert.Equal(steps, scope.Invocation.Budget.Steps);
                Assert.Equal(bytes, scope.Invocation.Ledger.ReservedBytes);
                var alias = writer;
                alias.Append("discarded"u8);
                writer = default;
                writer.Append(nested.AsSpan());
                alias.Append("after reset"u8);
                if (state.Failure is not null) throw state.Failure;
            });
        if (fail) Assert.Same(state.Failure, Assert.Throws<InvalidOperationException>(() => Invoke()));
        else Assert.Equal("𐐨β"u8.ToArray(), Invoke());
        Assert.Equal(1, state.Calls);
        Assert.NotNull(state.Scope);
        Assert.Equal(Utf8ExecutionLimitKind.None, state.Scope.Invocation.Budget.LimitBreach);
        Assert.Equal(0UL, state.Scope.Invocation.Ledger.ReservedBytes);
        Assert.Equal((2, 6), (state.Captures[0].StartOffsetInBytes, state.Captures[0].EndOffsetInBytes));
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void PublishedProjectionSurvivesSharedDenialAndScopeCleanup()
    {
        var regex = Create(new() { HeapLimitInBytes = 1000000 });
        var direct = (Pcre2SliceDirectProgram)regex.DebugCompiledProgram.Operations.Replace;
        var input = "é😀é😀"u8.ToArray();
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, CancellationToken.None);
        invocation.ShareStorageForCursor();
        var scope = Utf8SliceByteOutputScope.Enter(invocation);
        Pcre2GroupData[] groups;
        try
        {
            scope.CreateBuffer();
            var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, scope.Invocation,
                Utf8ValidatedInput.Create(input), new Utf8BytePosition(0), regex.DebugCompiledProgram.Request);
            Assert.True(cursor.MoveNext());
            groups = cursor.CaptureGroupsAt(0, true);
            var alias = cursor;
            Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, scope.Invocation.TryReserveWorking(1000000));
            try { alias.MoveNext(); Assert.Fail("Shared denial must survive cursor copy."); }
            catch (Pcre2MatchException error) { Assert.Equal(Pcre2ErrorKind.HeapLimit, error.ErrorKind); }
            Assert.Equal(512UL, invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        var retained = Utf8Pcre2MatchContext.Create(input, groups);
        Assert.Equal("é😀", retained.GetValueString());
        Assert.Equal("😀", retained.GetGroup(2).GetValueString());
        Assert.Equal((2, 6), (retained.GetGroup(2).StartOffsetInBytes, retained.GetGroup(2).EndOffsetInBytes));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalCancellationAndStrictInputRejectBeforeTinyCapacity(bool text)
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var regex = Create(new() { HeapLimitInBytes = 1 });
        var canceled = Assert.Throws<OperationCanceledException>(() => Run(regex, "é😀"u8.ToArray(),
            new Observation(), text, source.Token));
        Assert.Equal(source.Token, canceled.CancellationToken);
        Assert.Throws<ArgumentException>(() => Run(regex, [0xff], new Observation(), text, CancellationToken.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(regex, "é😀"u8.ToArray(),
            new Observation(), text, CancellationToken.None, 3));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FiniteCallbacksKeepTheSamePositionNonemptyRetry(bool text)
    {
        var regex = new Utf8Pcre2Regex("(?<=abc)(|def)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }, Timeout.InfiniteTimeSpan);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        var input = "abcdefabc😀"u8.ToArray();
        var calls = new List<(int, int, string)>();
        string actual;
        if (text)
            actual = regex.ReplaceToString(input, calls,
                static (in Utf8Pcre2MatchContext match, ref List<(int, int, string)> calls) =>
                {
                    calls.Add((match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes, match.GetGroup(1).GetValueString()));
                    return "<" + match.GetGroup(1).GetValueString() + ">";
                });
        else
            actual = Encoding.UTF8.GetString(regex.Replace(input, calls,
                static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref List<(int, int, string)> calls) =>
                {
                    calls.Add((match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes, match.GetGroup(1).GetValueString()));
                    writer.AppendAsciiByte((byte)'<'); writer.Append(match.GetGroup(1).GetValueBytes()); writer.AppendAsciiByte((byte)'>');
                }));
        Assert.Equal("abc<><def>abc<>😀", actual);
        Assert.Equal([(3, 3, ""), (3, 6, "def"), (9, 9, "")], calls);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void LargeCallbackBufferIsDeniedBeforePayloadAllocation()
    {
        var regex = Create(new() { HeapLimitInBytes = 128000 });
        var fragment = new string('a', 1000000);
        var input = "é😀"u8.ToArray();
        try { Run(regex, input, new Observation { Fragment = fragment }, false, CancellationToken.None); }
        catch (Pcre2MatchException) { }
        var state = new Observation { Fragment = fragment };
        var before = GC.GetAllocatedBytesForCurrentThread();
        Pcre2MatchException? failure = null;
        try { Run(regex, input, state, false, CancellationToken.None); }
        catch (Pcre2MatchException error) { failure = error; }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(1, state.Calls);
        Assert.InRange(allocated, 0, 81920);
        Assert.NotNull(state.Scope);
        Assert.True(state.Scope.Invocation.Budget.PeakWorkingBytes > (ulong)fragment.Length);
        Assert.Equal(0UL, state.Scope.Invocation.Ledger.ReservedBytes);
    }
}
