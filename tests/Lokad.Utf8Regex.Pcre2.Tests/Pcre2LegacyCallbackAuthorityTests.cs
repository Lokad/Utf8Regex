using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LegacyCallbackAuthorityTests
{
    private static Utf8Pcre2Regex Create(Utf8Pcre2ExecutionLimits limits, TimeSpan? timeout = null)
    {
        var regex = new Utf8Pcre2Regex("(*MARK:tag)(é)(😀)", Pcre2CompileOptions.None, default,
            limits, timeout ?? Timeout.InfiniteTimeSpan);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        return regex;
    }

    private sealed class Observation
    {
        internal Utf8SliceByteOutputScope? Scope;
        internal string Fragment = "!";
        internal readonly List<ulong> Steps = [];
        internal readonly List<Pcre2GroupData> Captures = [];
    }

    private static void Observe(in Utf8Pcre2MatchContext match, Observation state)
    {
        var scope = Utf8SliceByteOutputScope.Current;
        Assert.NotNull(scope);
        if (state.Scope is not null) Assert.Same(state.Scope, scope);
        state.Scope = scope;
        state.Steps.Add(scope.Invocation.Budget.Steps);
        state.Captures.Add(match.GetGroup(2)._data);
        Assert.Equal("tag", match.Mark);
    }

    private static string Run(Utf8Pcre2Regex regex, byte[] input, Observation state, bool text,
        Pcre2MatchOptions options = Pcre2MatchOptions.None, CancellationToken token = default, int start = 0)
    {
        try
        {
            return text
                ? regex.ReplaceToString(input, state, static (in Utf8Pcre2MatchContext match, ref Observation state) =>
                {
                    Observe(in match, state);
                    return state.Fragment + match.GetGroup(2).GetValueString();
                }, start, options, token)
                : Encoding.UTF8.GetString(regex.Replace(input, state,
                    static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref Observation state) =>
                    {
                        Observe(in match, state);
                        writer.Append(state.Fragment.AsSpan());
                        writer.Append(match.GetGroup(2).GetValueBytes());
                    }, start, options, token));
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
        foreach (var options in new[] { Pcre2MatchOptions.None, Pcre2MatchOptions.NotBol })
            yield return [working, text, hit, options];
    }

    [Theory]
    [MemberData(nameof(PublicCutoffs))]
    public void ExactPublicCutoffsCoverMatchingAndFinalOutput(bool working, bool text, bool hit, Pcre2MatchOptions options)
    {
        using var source = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(hit ? "é😀é😀" : new string('q', 8192));
        var fragment = new string('a', 4097);
        var expected = hit ? fragment + "😀" + fragment + "😀" : Encoding.UTF8.GetString(input);
        string Invoke(ulong cap) => Run(Create(new() { MatchLimit = working ? 0 : (uint)cap,
            HeapLimitInBytes = working ? cap : 0 }), input, new() { Fragment = fragment }, text, options, source.Token);
        var kind = working ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit;
        Assert.Equal(expected, Invoke(working ? ulong.MaxValue : uint.MaxValue));
        Assert.Equal(kind, Assert.Throws<Pcre2MatchException>(() => Invoke(1)).ErrorKind);
        ulong first = 1, last = 1UL << 20;
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try { Assert.Equal(expected, Invoke(cap)); last = cap; }
            catch (Pcre2MatchException error) { Assert.Equal(kind, error.ErrorKind); first = cap + 1; }
        }
        Assert.InRange(first, 2UL, (1UL << 20) - 1);
        Assert.Equal(expected, Invoke(first));
        Assert.Equal(kind, Assert.Throws<Pcre2MatchException>(() => Invoke(first - 1)).ErrorKind);
        Assert.Equal(expected, Invoke(first));
    }

    [Theory]
    [InlineData("foo(?<Bar>BAR)?", "αfooBAR😀fooβ", "α[2:6]😀[12:3]β")]
    [InlineData("(a)(?1)", "aaaa", "[0:2][2:2]")]
    [InlineData("(*MARK:tag)(?<=abc)(|def)", "abcdefabc😀", "abc[3:0][3:3]abc[9:0]😀")]
    [InlineData(@"(a\Kb)*", "ab xx ab yy ab", "a[1:1][2:0] [3:0]x[4:0]x[5:0] a[7:1][8:0] [9:0]y[10:0]y[11:0] a[13:1][14:0]")]
    [InlineData(@"(?:a\Kb)*", "ab xx ab", "a[1:1][2:0] [3:0]x[4:0]x[5:0] a[7:1][8:0]")]
    public void ExistingDetailedLoopPreservesLegacyGrammarAndRetry(string pattern, string input, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }, Timeout.InfiniteTimeSpan);
        var direct = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        Assert.True(direct.Program.CaptureSlotCount > 1 || direct.Program.SuppressesUnresetEmptyMatches);
        foreach (var options in new[] { Pcre2MatchOptions.None, Pcre2MatchOptions.NotBol })
        {
            var calls = new List<(int, int, string)>();
            var bytes = Encoding.UTF8.GetBytes(input);
            var actual = regex.ReplaceToString(bytes, calls,
                static (in Utf8Pcre2MatchContext match, ref List<(int, int, string)> calls) =>
                {
                    calls.Add((match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes, match.Mark ?? ""));
                    return $"[{match.Value.StartOffsetInBytes}:{match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes}]";
                }, 0, options);
            Assert.Equal(expected, actual);
            if (pattern.StartsWith("(*MARK"))
            {
                Assert.Equal([(3, 3, "tag"), (3, 6, "tag"), (9, 9, "tag")], calls);
            }
            Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(bytes, 0,
                static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
                    writer.Append($"[{match.Value.StartOffsetInBytes}:{match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes}]".AsSpan()),
                0, options));
        }
    }

    [Fact]
    public void ExplicitByteCapturesRemainValidWithoutScalarProjection()
    {
        var regex = new Utf8Pcre2Regex(@"(\C)", Pcre2CompileOptions.None,
            new() { BackslashC = Pcre2BackslashCPolicy.Allow },
            new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }, Timeout.InfiniteTimeSpan);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        foreach (var options in new[] { Pcre2MatchOptions.None, Pcre2MatchOptions.NotBol })
        {
            Assert.Equal("😀"u8.ToArray(), regex.Replace("😀"u8, 0,
                static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
                    writer.Append(match.GetGroup(1).GetValueBytes()), 0, options));
            Assert.Equal("xxxx", regex.ReplaceToString("😀"u8, 0,
                static (in Utf8Pcre2MatchContext match, ref int state) => "x", 0, options));
        }
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MatchingProjectionAndOutputShareActualCountersAndOverlap(bool liveToken)
    {
        using var source = new CancellationTokenSource();
        var token = liveToken ? source.Token : CancellationToken.None;
        var regex = Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 });
        var compiled = regex.DebugCompiledProgram;
        var direct = (Pcre2BacktrackingDirectProgram)compiled.Operations.Replace;
        var input = "é😀é😀"u8.ToArray();
        var invocation = Pcre2SliceDriver.CreateInvocation(compiled.Request, token);
        var budget = new Pcre2ResourceBudget(compiled.Request.DefaultLimits, invocation, false);
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(direct.Program, compiled.CandidateSearchPlan,
            Utf8ValidatedInput.Create(input), new(0), Pcre2MatchOptions.None, compiled.Request, budget);
        Assert.True(cursor.MoveNext());
        var matchingSteps = invocation.Budget.Steps;
        var matchingPeak = invocation.Budget.PeakWorkingBytes;
        var state = new Observation();
        Assert.Equal("!😀!😀", Run(regex, input, state, false, token: token));
        Assert.Equal(2, state.Steps.Count);
        Assert.NotNull(state.Scope);
        Assert.Equal(1000000UL, state.Scope.Invocation.Budget.Limits.MaxWorkSteps);
        Assert.Equal(1000000UL, state.Scope.Invocation.Budget.Limits.MaxWorkingBytes);
        Assert.True(state.Steps[0] > matchingSteps);
        Assert.True(state.Steps[1] > state.Steps[0]);
        Assert.True(state.Scope.Invocation.Budget.Steps > state.Steps[1]);
        Assert.True(state.Scope.Invocation.Budget.PeakWorkingBytes >= matchingPeak + 2 * Utf8SliceByteOutputScope.ControlBytes);
        Assert.Equal((2, 6), (state.Captures[0].StartOffsetInBytes, state.Captures[0].EndOffsetInBytes));
        Assert.Equal((8, 12), (state.Captures[1].StartOffsetInBytes, state.Captures[1].EndOffsetInBytes));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BorrowedFailureIsStickyAndScratchReleasesBeforeScopeExit(bool working)
    {
        var regex = Create(default);
        var compiled = regex.DebugCompiledProgram;
        var direct = (Pcre2BacktrackingDirectProgram)compiled.Operations.Replace;
        var invocation = new Utf8SliceInvocation(new(working ? 0UL : 3UL, 0, working ? 512UL : 1000000UL),
            Timeout.InfiniteTimeSpan, CancellationToken.None);
        invocation.ShareStorageForCursor();
        var scope = Utf8SliceByteOutputScope.Enter(invocation);
        try
        {
            var buffer = scope.CreateBuffer();
            var budget = new Pcre2ResourceBudget(compiled.Request.DefaultLimits, scope.Invocation, false);
            var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(direct.Program, compiled.CandidateSearchPlan,
                Utf8ValidatedInput.Create("é😀"u8), new(0), Pcre2MatchOptions.None, compiled.Request, budget);
            for (var i = 0; i < 2; i++)
            {
                try { cursor.MoveNext(); Assert.Fail("Borrowed matching must exhaust the same allowance."); }
                catch (Pcre2MatchException error) { Assert.Equal(working ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit, error.ErrorKind); }
                Assert.Equal(working ? Utf8ExecutionLimitKind.WorkingBytes : Utf8ExecutionLimitKind.WorkSteps, invocation.Budget.LimitBreach);
                Assert.Equal(512UL, invocation.Ledger.ReservedBytes);
            }
            Assert.Throws<InvalidOperationException>(() => buffer.Append("!"u8));
        }
        finally { scope.Release(); scope.Release(); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BorrowingPreservesOriginalDeadlineAndTokenBeforeHeapAdmission(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(0, 0, 1), TimeSpan.FromMilliseconds(20), source.Token);
        ticks = 15;
        var budget = new Pcre2ResourceBudget(default, invocation, false);
        if (cancel) source.Cancel(); else ticks = 25;
        if (cancel) Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => budget.ReserveWorking(1000)).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() => budget.ReserveWorking(1000)).ErrorKind);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.Budget.LimitBreach);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NestedCallsWriterAliasesAndCallerExceptionsKeepRetainedCaptures(bool fail)
    {
        var state = new Observation();
        var callerFailure = new InvalidOperationException("caller failure");
        byte[] Invoke() => Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }).Replace("é😀β"u8, state,
            (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref Observation current) =>
            {
                Observe(in match, current);
                var scope = current.Scope!;
                var steps = scope.Invocation.Budget.Steps;
                var bytes = scope.Invocation.Ledger.ReservedBytes;
                var nested = Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }).ReplaceToString("é😀"u8,
                    0, static (in Utf8Pcre2MatchContext match, ref int state) => "𐐨");
                Assert.Same(scope, Utf8SliceByteOutputScope.Current);
                Assert.Equal(steps, scope.Invocation.Budget.Steps);
                Assert.Equal(bytes, scope.Invocation.Ledger.ReservedBytes);
                var alias = writer;
                alias.Append("discarded"u8);
                writer = default;
                writer.Append(nested.AsSpan());
                alias.Append("after reset"u8);
                if (fail) throw callerFailure;
            });
        if (fail) Assert.Same(callerFailure, Assert.Throws<InvalidOperationException>(() => Invoke()));
        else Assert.Equal("𐐨β"u8.ToArray(), Invoke());
        Assert.NotNull(state.Scope);
        Assert.Equal(Utf8ExecutionLimitKind.None, state.Scope.Invocation.Budget.LimitBreach);
        Assert.Equal(0UL, state.Scope.Invocation.Ledger.ReservedBytes);
        Assert.Equal((2, 6), (state.Captures[0].StartOffsetInBytes, state.Captures[0].EndOffsetInBytes));
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OffsetValidationAndInterruptionKeepEntryAndPublicationContracts(bool text)
    {
        var regex = Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 });
        Assert.Equal("é😀x!😀β", Run(regex, "é😀xé😀β"u8.ToArray(), new(), text, start: 7));
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => Run(regex, [0xff], new(), text,
            token: source.Token)).CancellationToken);
        Assert.Throws<ArgumentException>(() => Run(regex, [0xff], new(), text));
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(regex, "é😀"u8.ToArray(), new(), text, start: 3));
        using var active = new CancellationTokenSource();
        Assert.Equal(active.Token, Assert.Throws<OperationCanceledException>(() => regex.Replace("é😀"u8, active,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref CancellationTokenSource state) =>
            { writer.Append("!"u8); state.Cancel(); }, 0, Pcre2MatchOptions.NotBol, active.Token)).CancellationToken);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var timed = Create(default, TimeSpan.FromMilliseconds(20));
        Action expire = () => ticks = 25;
        Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() => timed.ReplaceToString("é😀"u8, expire,
            static (in Utf8Pcre2MatchContext match, ref Action expire) => { expire(); return "!"; })).ErrorKind);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void LargeCallbackOutputIsDeniedBeforeItsPayloadAllocation()
    {
        var regex = Create(new() { HeapLimitInBytes = 128000 });
        var input = "é😀"u8.ToArray();
        var fragment = new string('a', 1000000);
        try { Run(regex, input, new() { Fragment = fragment }, false); }
        catch (Pcre2MatchException) { }
        var state = new Observation { Fragment = fragment };
        var before = GC.GetAllocatedBytesForCurrentThread();
        Pcre2MatchException? failure = null;
        try { Run(regex, input, state, false); }
        catch (Pcre2MatchException error) { failure = error; }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Single(state.Steps);
        Assert.InRange(allocated, 0, 81920);
        Assert.NotNull(state.Scope);
        Assert.True(state.Scope.Invocation.Budget.PeakWorkingBytes > (ulong)fragment.Length);
        Assert.Equal(0UL, state.Scope.Invocation.Ledger.ReservedBytes);
    }
}
