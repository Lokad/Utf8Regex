using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2ValueCallbackAuthorityTests
{
    private sealed class Observation
    {
        internal Utf8SliceByteOutputScope? Scope;
        internal string Fragment = "R";
        internal readonly List<ulong> Steps = [];
        internal readonly List<(int, int, string)> Captures = [];
    }

    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits limits, TimeSpan? timeout = null)
        => new(pattern, Pcre2CompileOptions.None, default, limits, timeout ?? Timeout.InfiniteTimeSpan);

    private static void Observe(in Utf8Pcre2MatchContext match, Observation state)
    {
        var scope = Utf8SliceByteOutputScope.Current;
        Assert.NotNull(scope);
        if (state.Scope is not null) Assert.Same(state.Scope, scope);
        state.Scope = scope;
        state.Steps.Add(scope.Invocation.Budget.Steps);
        var group = match.GetGroup(0);
        state.Captures.Add((group.StartOffsetInBytes, group.EndOffsetInBytes, group.GetValueString()));
        Assert.True(scope.Invocation.Ledger.ReservedBytes >=
            2 * Utf8SliceByteOutputScope.ControlBytes + Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1));
    }

    private static string Run(Utf8Pcre2Regex regex, byte[] input, Observation state, bool text,
        Pcre2MatchOptions options = Pcre2MatchOptions.None, CancellationToken token = default, int start = 0)
    {
        try
        {
            return text ? regex.ReplaceToString(input, state,
                static (in Utf8Pcre2MatchContext match, ref Observation state) =>
                { Observe(in match, state); return state.Fragment; }, start, options, token)
                : Encoding.UTF8.GetString(regex.Replace(input, state,
                    static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref Observation state) =>
                    { Observe(in match, state); writer.Append(state.Fragment.AsSpan()); }, start, options, token));
        }
        finally
        {
            if (state.Scope is not null) Assert.Equal(0UL, state.Scope.Invocation.Ledger.ReservedBytes);
            Assert.Null(Utf8SliceByteOutputScope.Current);
        }
    }

    [Theory]
    [InlineData("x", "αx😀xβ", "αR😀Rβ", "Pcre2Literal")]
    [InlineData("[x]", "αx😀xβ", "αR😀Rβ", "Pcre2Character")]
    [InlineData("a+", "éaaa😀aaβ", "éR😀Rβ", "Pcre2SingleTokenRepeat")]
    [InlineData("a|ab", "éab😀aβ", "éRb😀Rβ", "Pcre2LiteralFamily")]
    [InlineData("", "😀", "R😀R", "Pcre2Literal")]
    [InlineData(".", "é😀", "RR", "Pcre2Character")]
    public void ActualValueSelectorsAndScalarRangesSurviveOutputBorrowing(string pattern, string input, string expected,
        string kind)
    {
        var regex = Create(pattern, default);
        Assert.Equal(kind, regex.DebugCompiledProgram.Operations.Enumerate.Kind.ToString());
        foreach (var text in new[] { false, true })
        {
            var state = new Observation();
            Assert.Equal(expected, Run(regex, Encoding.UTF8.GetBytes(input), state, text));
            Assert.NotEmpty(state.Captures);
            if (pattern == ".") Assert.Equal([(0, 2, "é"), (2, 6, "😀")], state.Captures);
            if (pattern.Length == 0) Assert.Equal([(0, 0, ""), (4, 4, "")], state.Captures);
        }
    }

    public static IEnumerable<object[]> Cutoffs()
    {
        foreach (var pattern in new[] { "x", "[x]", "a+", "a|ab" })
        foreach (var working in new[] { false, true })
        foreach (var text in new[] { false, true })
        foreach (var hit in new[] { false, true })
            yield return [pattern, working, text, hit];
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void ExactCutoffsCoverValueDiscoverySlotAndPublication(string pattern, bool working, bool text, bool hit)
    {
        var subject = hit ? pattern is "x" or "[x]" ? "éx😀xβ" : "éa😀aβ" : new string('q', 4096);
        var fragment = new string('a', 4097);
        var expected = hit ? "é" + fragment + "😀" + fragment + "β" : subject;
        var input = Encoding.UTF8.GetBytes(subject);
        using var source = new CancellationTokenSource();
        string Invoke(ulong cap) => Run(Create(pattern, new() { MatchLimit = working ? 0 : (uint)cap,
            HeapLimitInBytes = working ? cap : 0 }), input, new() { Fragment = fragment }, text,
            Pcre2MatchOptions.NotBol, source.Token);
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
    [InlineData("x")] [InlineData("[x]")] [InlineData("a+")] [InlineData("a|ab")]
    public void ValueCallbacksSeeCumulativeDiscoveryAndReturnTheSlotLoan(string pattern)
    {
        var input = Encoding.UTF8.GetBytes(pattern is "x" or "[x]" ? "éx😀xβ" : "éa😀aβ");
        var state = new Observation();
        Assert.Equal("éR😀Rβ", Run(Create(pattern, new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }),
            input, state, false));
        Assert.NotNull(state.Scope);
        Assert.Equal(1000000UL, state.Scope.Invocation.Budget.Limits.MaxWorkSteps);
        Assert.Equal(1000000UL, state.Scope.Invocation.Budget.Limits.MaxWorkingBytes);
        Assert.Equal(2, state.Steps.Count);
        Assert.True(state.Steps[0] > 2);
        Assert.True(state.Steps[1] > state.Steps[0]);
        Assert.True(state.Scope.Invocation.Budget.Steps > state.Steps[1]);
        Assert.Equal(0UL, state.Scope.Invocation.Ledger.ReservedBytes);
        Assert.Equal(2, state.Captures[0].Item1);
        Assert.Equal(7, state.Captures[1].Item1);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NonAsciiCoordinateProjectionUsesBoundedWorkAndOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var input = Utf8ValidatedInput.Create(Encoding.UTF8.GetBytes(new string('é', 10000) + "x"));
        var projection = input.CreateProjectionCursor();
        var invocation = new Utf8SliceInvocation(new(2, 0, 0), Timeout.InfiniteTimeSpan, source.Token);
        var budget = new Pcre2ResourceBudget(new() { MatchLimit = 2 }, invocation, false);
        if (cancel) source.Cancel();
        Exception? failure = null;
        try { Pcre2GlobalCursorProjection.Project(20000, 20001, ref input, ref projection, ref budget); }
        catch (Exception error) { failure = error; }
        if (cancel)
        {
            Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(0, projection.BytesConsumed);
        }
        else
        {
            Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            Assert.Equal(4096, projection.BytesConsumed);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, invocation.Budget.LimitBreach);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AliasesNestedCallsAndCallerExceptionsKeepScalarResultsAndReleaseLoans(bool fail)
    {
        var state = new Observation();
        var callerFailure = new InvalidOperationException("caller failure");
        byte[] Invoke() => Create("[x]", new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }).Replace("αxβ"u8, state,
            (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref Observation current) =>
            {
                Observe(in match, current);
                var scope = current.Scope!;
                var steps = scope.Invocation.Budget.Steps;
                var held = scope.Invocation.Ledger.ReservedBytes;
                var nested = Create("x", new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }).ReplaceToString("x"u8,
                    0, static (in Utf8Pcre2MatchContext match, ref int state) => "𐐨");
                Assert.Same(scope, Utf8SliceByteOutputScope.Current);
                Assert.Equal(steps, scope.Invocation.Budget.Steps);
                Assert.Equal(held, scope.Invocation.Ledger.ReservedBytes);
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
        Assert.Equal([(2, 3, "x")], state.Captures);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalDeadlineAndTokenAbortTheLastCallbackWithoutPublication(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var state = new Observation();
        var regex = Create("x", new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }, TimeSpan.FromMilliseconds(20));
        Exception? failure = null;
        try
        {
            regex.Replace("x"u8, state,
                (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref Observation current) =>
                {
                    Observe(in match, current);
                    writer.Append("R"u8);
                    if (cancel) source.Cancel(); else ticks = 25;
                }, 0, Pcre2MatchOptions.NotBol, source.Token);
        }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.NotNull(state.Scope);
        Assert.Equal(0UL, state.Scope.Invocation.Ledger.ReservedBytes);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData("x")] [InlineData("[x]")] [InlineData("a+")] [InlineData("a|ab")]
    public void LargeCallbackOutputIsRefusedBeforePayloadAllocation(string pattern)
    {
        var regex = Create(pattern, new() { HeapLimitInBytes = 128000 });
        var input = Encoding.UTF8.GetBytes(pattern is "x" or "[x]" ? "x" : "a");
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
