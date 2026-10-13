using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2OptionCallbackAuthorityTests
{
    private static Utf8Pcre2Regex Create(Utf8Pcre2ExecutionLimits limits, TimeSpan? timeout = null)
    {
        var regex = new Utf8Pcre2Regex("(é)(😀)", Pcre2CompileOptions.None, default,
            limits, timeout ?? Timeout.InfiniteTimeSpan);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        return regex;
    }

    private sealed class Observation
    {
        internal Utf8SliceByteOutputScope? Scope;
        internal string Fragment = "!";
        internal readonly List<ulong> Steps = [];
        internal readonly List<(int Start, int End, string Value)> Captures = [];
    }

    private static void Observe(in Utf8Pcre2MatchContext match, Observation state)
    {
        var scope = Utf8SliceByteOutputScope.Current;
        Assert.NotNull(scope);
        if (state.Scope is not null) Assert.Same(state.Scope, scope);
        state.Scope = scope;
        state.Steps.Add(scope.Invocation.Budget.Steps);
        var group = match.GetGroup(2);
        state.Captures.Add((group.StartOffsetInBytes, group.EndOffsetInBytes, group.GetValueString()));
    }

    private static string Run(Utf8Pcre2Regex regex, byte[] input, Observation state, bool text,
        Pcre2MatchOptions options, CancellationToken token = default, int start = 0)
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

    public static IEnumerable<object[]> AllOptions()
    {
        for (var flags = 0; flags < 64; flags++) yield return [(Pcre2MatchOptions)flags];
    }

    [Theory]
    [MemberData(nameof(AllOptions))]
    public void EveryOptionCombinationPreservesScalarCapturesAndOffsets(Pcre2MatchOptions options)
    {
        var regex = Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 });
        var input = "qé😀é😀"u8.ToArray();
        var endAnchored = (options & Pcre2MatchOptions.EndAnchored) != 0;
        var anchored = (options & Pcre2MatchOptions.Anchored) != 0;
        var expected = endAnchored ? anchored ? "qé😀é😀" : "qé😀!😀" : "q!😀!😀";
        foreach (var text in new[] { false, true })
        {
            var state = new Observation();
            Assert.Equal(expected, Run(regex, input, state, text, options, start: 1));
            if (endAnchored && anchored) Assert.Empty(state.Captures);
            else if (endAnchored) Assert.Equal([(9, 13, "😀")], state.Captures);
            else Assert.Equal([(3, 7, "😀"), (9, 13, "😀")], state.Captures);
            if (state.Scope is not null)
            {
                Assert.Equal(1000000UL, state.Scope.Invocation.Budget.Limits.MaxWorkSteps);
                Assert.Equal(1000000UL, state.Scope.Invocation.Budget.Limits.MaxWorkingBytes);
                Assert.True(state.Scope.Invocation.Budget.Steps > state.Steps[^1]);
            }
        }
    }

    [Theory]
    [InlineData(Pcre2MatchOptions.None)]
    [InlineData(Pcre2MatchOptions.NotBol)]
    [InlineData(Pcre2MatchOptions.NotEol)]
    [InlineData(Pcre2MatchOptions.NotEmptyAtStart)]
    [InlineData(Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol)]
    [InlineData(Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEmptyAtStart)]
    public void CapturesBelongToTheActualEmptyAndNonemptyGlobalYields(Pcre2MatchOptions options)
    {
        var regex = new Utf8Pcre2Regex("(?<=abc)(|def)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }, Timeout.InfiniteTimeSpan);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        foreach (var text in new[] { false, true })
        {
            var captures = new List<(int, int, int, int, string)>();
            string result;
            if (text) result = regex.ReplaceToString("abcdefabc😀"u8, captures,
                static (in Utf8Pcre2MatchContext match, ref List<(int, int, int, int, string)> captures) =>
                {
                    var group = match.GetGroup(1);
                    captures.Add((match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes,
                        group.StartOffsetInBytes, group.EndOffsetInBytes, group.GetValueString()));
                    return "<" + group.GetValueString() + ">";
                }, 0, options);
            else result = Encoding.UTF8.GetString(regex.Replace("abcdefabc😀"u8, captures,
                static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref List<(int, int, int, int, string)> captures) =>
                {
                    var group = match.GetGroup(1);
                    captures.Add((match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes,
                        group.StartOffsetInBytes, group.EndOffsetInBytes, group.GetValueString()));
                    writer.AppendAsciiByte((byte)'<'); writer.Append(group.GetValueBytes()); writer.AppendAsciiByte((byte)'>');
                }, 0, options));
            Assert.Equal("abc<><def>abc<>😀", result);
            Assert.Equal([(3, 3, 3, 3, ""), (3, 6, 3, 6, "def"), (9, 9, 9, 9, "")], captures);
        }
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData(Pcre2MatchOptions.Anchored, 0, "abcdefabc😀", "abcdefabc😀")]
    [InlineData(Pcre2MatchOptions.Anchored, 3, "abcdefabc😀", "abc<><def>abc😀")]
    [InlineData(Pcre2MatchOptions.NotEmpty, 0, "abcdefabc😀", "abc<def>abc😀")]
    [InlineData(Pcre2MatchOptions.NotEmptyAtStart, 3, "abcdefabc😀", "abc<def>abc<>😀")]
    [InlineData(Pcre2MatchOptions.EndAnchored, 0, "abcdef", "abc<def>")]
    [InlineData(Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmpty, 3, "abcdef", "abc<def>")]
    public void AnchoredAndEmptyRestrictionsKeepTheirOriginalStartDisposition(Pcre2MatchOptions options, int start, string input, string expected)
    {
        var regex = new Utf8Pcre2Regex("(?<=abc)(|def)");
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Equal(expected, regex.ReplaceToString(bytes, 0,
            static (in Utf8Pcre2MatchContext match, ref int state) => "<" + match.GetGroup(1).GetValueString() + ">", start, options));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(bytes, 0,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
            { writer.AppendAsciiByte((byte)'<'); writer.Append(match.GetGroup(1).GetValueBytes()); writer.AppendAsciiByte((byte)'>'); }, start, options));
    }

    [Theory]
    [InlineData(Pcre2MatchOptions.NotBol, "!😀")]
    [InlineData(Pcre2MatchOptions.NotEol, "!😀")]
    [InlineData(Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol, "!😀")]
    public void LineAnchorOptionsAreEnforcedByTheExistingNativeProgram(Pcre2MatchOptions options, string expected)
    {
        var regex = new Utf8Pcre2Regex("(^é)(😀)$");
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        Assert.Equal("é😀", regex.ReplaceToString("é😀"u8, 0,
            static (in Utf8Pcre2MatchContext match, ref int state) => "!", 0, options));
        Assert.Equal(expected, Run(Create(default), "é😀"u8.ToArray(), new(), true, options));
    }

    public static IEnumerable<object[]> PublicCutoffs()
    {
        foreach (var working in new[] { false, true })
        foreach (var text in new[] { false, true })
        foreach (var hit in new[] { false, true })
        foreach (var options in new[] { Pcre2MatchOptions.NotBol, Pcre2MatchOptions.NotEmptyAtStart })
            yield return [working, text, hit, options];
    }

    [Theory]
    [MemberData(nameof(PublicCutoffs))]
    public void ExactFiniteCutoffsCoverDiscoveryAndByteOrTextPublication(bool working, bool text, bool hit, Pcre2MatchOptions options)
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
            catch (Pcre2MatchException failure) { Assert.Equal(kind, failure.ErrorKind); first = cap + 1; }
        }
        Assert.InRange(first, 2UL, (1UL << 20) - 1);
        Assert.Equal(expected, Invoke(first));
        Assert.Equal(kind, Assert.Throws<Pcre2MatchException>(() => Invoke(first - 1)).ErrorKind);
        Assert.Equal(expected, Invoke(first));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalTokenAndDeadlineAbortAfterCallbackWithoutPublishing(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }, TimeSpan.FromMilliseconds(20));
        var state = new Observation();
        Action expire = () => { if (cancel) source.Cancel(); else ticks = 25; };
        Exception? failure = null;
        try
        {
            regex.Replace("é😀"u8, state,
                (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref Observation state) =>
                {
                    Observe(in match, state);
                    writer.Append("!"u8);
                    expire();
                }, 0, Pcre2MatchOptions.NotBol, source.Token);
        }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.NotNull(state.Scope);
        Assert.Equal(0UL, state.Scope.Invocation.Ledger.ReservedBytes);
        Assert.Equal([(2, 6, "😀")], state.Captures);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WriterAliasesNestedCallsAndCallerFailuresPreserveScopeAndCaptures(bool fail)
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
                    0, static (in Utf8Pcre2MatchContext match, ref int state) => "!😀", 0, Pcre2MatchOptions.NotBol);
                Assert.Same(scope, Utf8SliceByteOutputScope.Current);
                Assert.Equal(steps, scope.Invocation.Budget.Steps);
                Assert.Equal(bytes, scope.Invocation.Ledger.ReservedBytes);
                writer.Append(nested.AsSpan());
                var alias = writer;
                alias.Append("discarded"u8);
                writer = default;
                writer.Append("𐐨"u8);
                alias.Append("after reset"u8);
                if (fail) throw callerFailure;
            }, 0, Pcre2MatchOptions.NotEmptyAtStart);
        if (fail) Assert.Same(callerFailure, Assert.Throws<InvalidOperationException>(() => Invoke()));
        else Assert.Equal("𐐨β"u8.ToArray(), Invoke());
        Assert.NotNull(state.Scope);
        Assert.Equal(Utf8ExecutionLimitKind.None, state.Scope.Invocation.Budget.LimitBreach);
        Assert.Equal(0UL, state.Scope.Invocation.Ledger.ReservedBytes);
        Assert.Equal([(2, 6, "😀")], state.Captures);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void EntryValidationPrecedesMatchingAndRejectsMalformedTails()
    {
        var regex = Create(new() { MatchLimit = 100000 });
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => Run(regex, [0xff], new(), false,
            Pcre2MatchOptions.NotBol, source.Token)).CancellationToken);
        Assert.Throws<ArgumentException>(() => Run(regex, [0xff], new(), false, Pcre2MatchOptions.NotBol));
        Assert.Throws<ArgumentException>(() => Run(regex, [.. "é😀"u8, 0xff], new(), true, Pcre2MatchOptions.NotBol));
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(regex, "é😀"u8.ToArray(), new(), false,
            Pcre2MatchOptions.NotBol, start: 3));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
            Run(Create(new() { MatchLimit = 1 }), [.. "é😀"u8, 0xff], new(), true,
                Pcre2MatchOptions.NotBol)).ErrorKind);
    }

    [Fact]
    public void LargeExplicitOptionCallbackOutputIsDeniedBeforePayloadAllocation()
    {
        var regex = Create(new() { HeapLimitInBytes = 128000 });
        var input = "é😀"u8.ToArray();
        var fragment = new string('a', 1000000);
        try { Run(regex, input, new() { Fragment = fragment }, false, Pcre2MatchOptions.NotBol); }
        catch (Pcre2MatchException) { }
        var state = new Observation { Fragment = fragment };
        var before = GC.GetAllocatedBytesForCurrentThread();
        Pcre2MatchException? failure = null;
        try { Run(regex, input, state, false, Pcre2MatchOptions.NotBol); }
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
