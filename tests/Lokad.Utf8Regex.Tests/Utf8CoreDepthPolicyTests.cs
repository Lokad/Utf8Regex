using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CoreDepthPolicyTests
{
    private static readonly string[] Operations = ["is", "count", "match", "detail", "is-offset", "count-offset",
        "match-offset", "detail-offset", "matches", "matches-offset", "split", "split-count", "replace",
        "replace-bytes", "replace-text", "destination", "destination-bytes", "callback-bytes", "callback-text"];
    private static Utf8Regex Create(string pattern, uint cap, RegexOptions options)
        => new(pattern, options, Regex.InfiniteMatchTimeout, new Utf8ResourcePolicy { MaxDepth = cap });
    public static IEnumerable<object[]> Modes()
    {
        foreach (var operation in Operations)
            foreach (var compiled in new[] { false, true })
                foreach (var live in new[] { false, true }) yield return [operation, compiled, live];
    }
    private static void Denied(Utf8ResourceLimitExceededException error, uint cap)
    {
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth, error.ResourceKind);
        Assert.Equal((ulong)cap, error.Limit); Assert.Equal((ulong)cap + 1, error.Requested);
    }
    [Theory]
    [MemberData(nameof(Modes))]
    public void AllPublicOperationsHaveExactDepthSuccessAndOneUnderDenial(string operation, bool compiled, bool live)
    {
        const string pattern = "(?<x>😀+)(?<y>ab)";
        var input = "😀😀ab"u8.ToArray();
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        using var source = new CancellationTokenSource(); var token = live ? source.Token : default;
        var baseline = Utf8CoreWorkPolicyTests.Run(new Utf8Regex(pattern, options), operation, input, token, 0);
        Assert.Equal(baseline, Utf8CoreWorkPolicyTests.Run(Create(pattern, uint.MaxValue, options), operation, input, token, 0));
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8CoreWorkPolicyTests.Run(Create(pattern, 1, options), operation, input, token, 0)), 1);
        uint low = 1, high = 256;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            try { Assert.Equal(baseline, Utf8CoreWorkPolicyTests.Run(Create(pattern, middle, options), operation, input, token, 0)); high = middle; }
            catch (Utf8ResourceLimitExceededException error) { Denied(error, middle); low = middle; }
        }
        Assert.Equal(baseline, Utf8CoreWorkPolicyTests.Run(Create(pattern, high, options), operation, input, token, 0));
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8CoreWorkPolicyTests.Run(Create(pattern, high - 1, options), operation, input, token, 0)), high - 1);
    }
    [Theory]
    [InlineData("abc", "xxabc", RegexOptions.None)]
    [InlineData("😀", "é😀", RegexOptions.Compiled)]
    [InlineData("[a-z]+", "abc", RegexOptions.RightToLeft)]
    [InlineData("(?<=😀)(?<n>é+)\\k<n>", "😀éé", RegexOptions.Compiled)]
    [InlineData("(a|aa)+", "aaaa", RegexOptions.NonBacktracking)]
    [InlineData("(😀|😀😀)+", "😀😀😀", RegexOptions.NonBacktracking | RegexOptions.Compiled)]
    public void NativeShapesAssertionsReferencesRtlAndLinearRoutesKeepScalarOutcomes(string pattern, string text, RegexOptions mode)
    {
        var input = Encoding.UTF8.GetBytes(text); var options = mode | RegexOptions.CultureInvariant;
        foreach (var operation in Operations)
            Assert.Equal(Utf8CoreWorkPolicyTests.Run(new Utf8Regex(pattern, options), operation, input, default, 0),
                Utf8CoreWorkPolicyTests.Run(Create(pattern, 4096, options), operation, input, default, 0));
        Assert.False(Create("absent", 1, options).IsMatch(input));
    }
    [Fact]
    public void CopiedCursorDenialIsStickyAndRetainedRangesStayValid()
    {
        var input = "ab 😀😀ab"u8.ToArray(); var regex = Create("(?<x>ab)|😀+ab", 1, RegexOptions.CultureInvariant);
        var cursor = regex.EnumerateMatches(input); Assert.True(cursor.MoveNext()); var retained = cursor.Current; var alias = cursor;
        try { alias.MoveNext(); Assert.Fail("Depth denial must remain an exception."); } catch (Utf8ResourceLimitExceededException error) { Denied(error, 1); }
        try { cursor.MoveNext(); Assert.Fail("Aliases must share the first denial."); } catch (Utf8ResourceLimitExceededException error) { Denied(error, 1); }
        Assert.Equal(0, retained.IndexInBytes); Assert.Equal(2, retained.LengthInBytes);
        Assert.True(regex.IsMatch("ab"u8)); // A separate invocation keeps its own authority.
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInterruptionWinsBeforeNextDepthDenial(bool deadline)
    {
        using var clock = Utf8DeadlineTestClock.Start(); using var token = new CancellationTokenSource();
        var triggered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ => { if (triggered) return; triggered = true; if (deadline) clock.Advance(250); else token.Cancel(); });
        try
        {
            var regex = new Utf8Regex("(a|aa)+", RegexOptions.CultureInvariant, deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, new Utf8ResourcePolicy { MaxDepth = 1 });
            var input = Encoding.UTF8.GetBytes(new string('é', 10000) + "aaaa");
            if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, token.Token));
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
            Assert.True(triggered);
        }
        finally { diagnostics.Complete(); }
    }
    [Fact]
    public void FirstWorkResultOrDepthDenialKeepsItsOriginalKind()
    {
        var budget = new Utf8ExecutionBudget(new(1, 1, 0, 1), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryChargeStep());
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, budget.TryChargeStep());
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, budget.TryNoteDepth(2));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, budget.CreateResourceFailure().ResourceKind);
        var result = new Utf8ExecutionBudget(new(0, 1, 0, 1), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, result.TryChargeResultBytes(2));
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, result.TryNoteDepth(2));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, result.CreateResourceFailure().ResourceKind);
        var depth = new Utf8ExecutionBudget(new(1, 1, 0, 1), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.Depth, depth.TryNoteDepth(2));
        Assert.Equal(Utf8ExecutionLimitKind.Depth, depth.TryChargeStep());
        Assert.Equal(Utf8ExecutionLimitKind.Depth, depth.TryChargeStep());
        Assert.Equal(Utf8ExecutionLimitKind.Depth, depth.TryChargeResultBytes(2));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth, depth.CreateResourceFailure().ResourceKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinearDepthDenialSkipsTheDeniedRegisterCopy(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant
            | (compiled ? RegexOptions.Compiled : 0);
        const string prefix = "(?:((?:😀|é))|a)";
        var small = new Utf8Regex(prefix, options).Inspection.AvailableOwnedSliceProgram!;
        var large = new Utf8Regex(prefix + string.Concat(Enumerable.Repeat("()", 1024)), options)
            .Inspection.AvailableOwnedSliceProgram!;
        var smallInvocation = new Utf8SliceInvocation(new(0, 1, 0), Regex.InfiniteMatchTimeout, default);
        var largeInvocation = new Utf8SliceInvocation(new(0, 1, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(smallInvocation.Ledger.TryReserve(7));
        Assert.True(largeInvocation.Ledger.TryReserve(7));
        foreach (var program in new[] { small, large })
        {
            ref var invocation = ref (program == small ? ref smallInvocation : ref largeInvocation);
            var result = Utf8SliceRunner.TryMatchAt(program, "😀"u8, 0,
                new int[program.GroupCount], new int[program.GroupCount], true, ref invocation);
            Assert.False(result.IsMatch);
            Assert.Equal(Utf8ExecutionLimitKind.Depth, result.Breach);
            Assert.Equal(2U, invocation.Budget.PeakDepth);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
        static ulong Pages(Utf8SliceProgram program)
            => ((ulong)program.GroupCount * 2 + (ulong)program.NonBacktrackingCaptureOpeningCount + 511) / 512;
        // Only the required seed fill, initial copy and admitted first-fork copy
        // may scale with capture width. The denied second branch must copy nothing.
        Assert.Equal(3 * (Pages(large) - Pages(small)), largeInvocation.Budget.Steps - smallInvocation.Budget.Steps);
        Assert.True(Create(prefix, 2, options).IsMatch("😀"u8));
        // The viability-table cursor replays one chosen branch without a pending
        // stack; table width is workspace, not execution depth.
        var cursor = Create(prefix, 1, options).EnumerateMatches("😀"u8);
        Assert.True(cursor.MoveNext());
        Assert.Equal(4, cursor.Current.LengthInBytes);
        Assert.False(cursor.MoveNext());
        smallInvocation.Ledger.Release(7);
        largeInvocation.Ledger.Release(7);
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkSteps)]
    [InlineData(Utf8ResourceLimitKind.ExecutionResultBytes)]
    [InlineData(Utf8ResourceLimitKind.ExecutionDepth)]
    public void CombinedPoliciesKeepEachPublicFailureKind(Utf8ResourceLimitKind kind)
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = kind == Utf8ResourceLimitKind.ExecutionWorkSteps ? 1 : ulong.MaxValue,
            MaxResultBytes = kind == Utf8ResourceLimitKind.ExecutionResultBytes ? 1 : ulong.MaxValue,
            MaxDepth = kind == Utf8ResourceLimitKind.ExecutionDepth ? 1 : uint.MaxValue,
        };
        var regex = new Utf8Regex("(?<x>😀+)(?<y>ab)", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, policy);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.MatchDetailed("😀😀ab"u8));
        Assert.Equal(kind, error.ResourceKind);
        Assert.Equal(1UL, error.Limit);
        Assert.True(error.Requested > 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DepthPolicyStillValidatesTheEntireSubjectAndScalarOffsets(bool compiled)
    {
        var regex = Create("ab", 1, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        Assert.Throws<ArgumentException>(() => regex.IsMatch([97, 98, 255]));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchFromUtf16Offset("😀ab"u8, 1));
        var match = regex.MatchFromUtf16Offset("😀ab"u8, 2);
        Assert.Equal((4, 2), (match.IndexInBytes, match.LengthInBytes));
    }
}
