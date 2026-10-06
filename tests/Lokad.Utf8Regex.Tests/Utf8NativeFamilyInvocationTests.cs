using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeFamilyInvocationTests
{
    private static Utf8PreparedRegex Prepared(Utf8Regex regex) =>
        (Utf8PreparedRegex)typeof(Utf8Regex).GetProperty("PreparedRegex", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(regex)!;

    private static Utf8SearchPlan Plan(byte[][] literals, bool folded = false, byte[]? trailing = null) =>
        Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(folded ? Utf8SearchKind.AsciiFoldedByteLiterals : Utf8SearchKind.ExactAsciiLiterals,
            new Utf8SearchFactData { AlternateLiteralsUtf8 = literals, TrailingLiteralUtf8 = trailing ?? [] }));

    public static IEnumerable<object[]> OrderCases()
    {
        foreach (var pattern in new[] { "a|ab", "ab|a", "abcde|d", "d|abcde", "ab|cab", "cab|ab", @"\b(?:a-b|b|niño)\b", @"\b(?:café|caféteria|niño)\b", @"(?:café|café noir|niño)(?=!)" })
        foreach (var reverse in new[] { false, true })
        foreach (var folded in new[] { false, true })
        {
            if (folded && pattern.Any(static c => c > 127)) continue;
            yield return [pattern, reverse, folded];
        }
    }

    [Theory]
    [MemberData(nameof(OrderCases))]
    public void BoundedFamilySearchPreservesBranchOrderEndPriorityAndPredicates(string pattern, bool reverse, bool folded)
    {
        var options = RegexOptions.CultureInvariant | (folded ? RegexOptions.IgnoreCase : RegexOptions.None) |
            (reverse ? RegexOptions.RightToLeft : RegexOptions.None);
        // Exercise reverse discovery with the same immutable family facts. Some
        // public RTL front ends deliberately retain their unit semantic route.
        var regex = new Utf8Regex(pattern, options & ~RegexOptions.RightToLeft);
        var plan = Prepared(regex).SearchPlan;
        Assert.NotNull(plan.AlternateLiteralsUtf8);
        const string text = "abcde cab AB caféteria café noir! xa-b a-b b niño";
        var input = Encoding.UTF8.GetBytes(text);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var low = 0;
        var high = input.Length;
        foreach (Match expected in new Regex(pattern, options).Matches(text))
        {
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteralFamily(plan, input, low, high,
                reverse, ref invocation, out var match, out var found));
            Assert.True(found);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), match.Index);
            Assert.Equal(Encoding.UTF8.GetByteCount(expected.Value), match.Length);
            Assert.True(string.Equals(Encoding.UTF8.GetString(input.AsSpan(match.Index, match.Length)),
                Encoding.UTF8.GetString(plan.AlternateLiteralsUtf8![match.LiteralId]),
                folded ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (reverse) high = match.Index;
            else low = match.Index + match.Length;
        }
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteralFamily(plan, input, low, high,
            reverse, ref invocation, out _, out var terminal));
        Assert.False(terminal);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryCutoffAcrossMatchesSearchAndProjectionPreservesCallerLeaseAndUnpublishedResult(bool folded)
    {
        var regex = new Utf8Regex("target|needle", RegexOptions.CultureInvariant | (folded ? RegexOptions.IgnoreCase : RegexOptions.None));
        var input = Encoding.UTF8.GetBytes("é target 中 needle 😀 target");
        var plan = Prepared(regex);
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(7));
        var cursor = new Utf8OperationMatchCursor(input, plan, false, baseline);
        var count = 0;
        while (cursor.MoveNext()) count++;
        Assert.Equal(3, count);
        for (var cap = 1UL; cap < cursor.DebugInvocationSteps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var limited = new Utf8OperationMatchCursor(input, plan, false, invocation);
            Utf8ExecutionLimitKind breach;
            bool found;
            do { breach = limited.TryMoveNextLiteralFamily(out found); } while (breach == Utf8ExecutionLimitKind.None && found);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, breach);
            Assert.False(found);
            Assert.False(limited.CurrentValueMatch.Success);
            Assert.Equal(7UL, limited.DebugInvocationReservedBytes);
        }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static IEnumerable<object[]> CrossBlockCases()
    {
        foreach (var length in new[] { 1, 2047, 2048, 2049, 8193 })
        foreach (var offset in new[] { 2047, 2048, 2049 })
        foreach (var folded in new[] { false, true }) yield return [length, offset, folded];
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CountNeverPublishesPartialSuccessAndKeepsOneBudgetAcrossMatches(bool reverse, bool folded)
    {
        var regex = new Utf8Regex("target|needle", RegexOptions.CultureInvariant | (folded ? RegexOptions.IgnoreCase : RegexOptions.None));
        var plan = Prepared(regex).SearchPlan;
        var input = Encoding.UTF8.GetBytes("é target 中 needle 😀 target");
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BackendInstructionExecutor.TryCountLiteralFamily(plan, input,
            reverse, ref baseline, out var total));
        Assert.Equal(3, total);
        for (var cap = 1UL; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8BackendInstructionExecutor.TryCountLiteralFamily(plan, input,
                reverse, ref invocation, out var partial));
            Assert.Equal(0, partial);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [MemberData(nameof(CrossBlockCases))]
    public void FamilyBranchesCrossEveryScanAndComparisonBoundary(int length, int offset, bool folded)
    {
        var literal = Encoding.UTF8.GetBytes(new string('a', length));
        var plan = Plan(["needle"u8.ToArray(), literal], folded);
        byte[] input = [.. Enumerable.Repeat((byte)'x', offset), .. (folded ? Encoding.UTF8.GetBytes(new string('A', length)) : literal)];
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteralFamily(plan, input, 0, input.Length,
            false, ref invocation, out var match, out var found));
        Assert.True(found);
        Assert.Equal(offset, match.Index);
        Assert.Equal(length, match.Length);
        Assert.Equal(1, match.LiteralId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryLongPredicateCutoffKeepsFailureKindAndUnpublishedMatch(bool reverse)
    {
        var trailing = Encoding.UTF8.GetBytes(new string('a', 8193));
        var plan = Plan(["target"u8.ToArray(), "needle"u8.ToArray()], trailing: trailing);
        byte[] input = [.. Enumerable.Repeat((byte)'x', 5000), .. "target"u8, .. trailing];
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteralFamily(plan, input, 0, input.Length,
            reverse, ref baseline, out _, out var expected));
        Assert.True(expected);
        for (var cap = 1UL; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8SearchExecutor.TryFindLiteralFamily(plan, input, 0, input.Length,
                reverse, ref invocation, out var rejected, out var found));
            Assert.False(found);
            Assert.Equal(default, rejected);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OffsetFloorKeepsOriginalBoundaryContextAndFamilyProgression(bool metered)
    {
        var regex = new Utf8Regex(@"\b(?:target|needle)\b", RegexOptions.CultureInvariant);
        const string text = "é target xneedle needle target";
        var input = Encoding.UTF8.GetBytes(text);
        var invocation = new Utf8SliceInvocation(metered ? new(ulong.MaxValue, 0, 0) : Utf8ExecutionLimits.Unlimited,
            Regex.InfiniteMatchTimeout, default);
        var cursor = new Utf8OperationMatchCursor(input, Prepared(regex), false, invocation).WithStartFloor(10, 9);
        foreach (Match expected in new Regex(regex.Pattern, RegexOptions.CultureInvariant).Matches(text, 9))
        {
            Assert.True(cursor.MoveNext());
            Assert.Equal(expected.Index, cursor.CurrentValueMatch.IndexInUtf16);
            Assert.Equal(expected.Length, cursor.CurrentValueMatch.LengthInUtf16);
        }
        Assert.False(cursor.MoveNext());
    }

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { "target|needle", "café|niño", @"\b(?:foo|foobar|baz)\b", @"\b(?:a-b|b|niño)\b", @"(?:café|café noir|niño)(?=!)" })
        foreach (var compiled in new[] { false, true }) yield return [pattern, compiled];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyDeadlineFinderExpiresWithoutChangingScanState(bool hit)
    {
        var regex = new Utf8Regex("target|needle", RegexOptions.CultureInvariant);
        var plan = Prepared(regex).SearchPlan;
        var input = Encoding.UTF8.GetBytes(new string('x', 320000) + (hit ? "target" : ""));
        var state = default(PreparedMultiLiteralScanState);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50));
        Thread.Sleep(75);
        var failed = false;
        try { _ = Utf8BackendInstructionExecutor.TryFindNextLiteralFamilyMatch(plan, plan.EnumerationOperation,
            input, ref state, deadline, out _); }
        catch (Utf8ExecutionDeadlineExpiredException) { failed = true; }
        Assert.True(failed);
        Assert.Equal(default, state);
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void NativeFamilyKeepsCompleteOwnedOperationsAndLiveTokenRanges(string pattern, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "é target needle 中 target", "foo foobar baz xfoo", "xa-b a-b b niño café noir!"], options);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex(pattern, options);
        const string text = "é target needle foo foobar xa-b a-b b niño café noir!";
        var enumerator = regex.EnumerateMatches(Encoding.UTF8.GetBytes(text), source.Token);
        foreach (Match expected in new Regex(pattern, options).Matches(text))
        {
            Assert.True(enumerator.MoveNext());
            Assert.Equal(expected.Index, enumerator.Current.IndexInUtf16);
            Assert.Equal(expected.Length, enumerator.Current.LengthInUtf16);
        }
        Assert.False(enumerator.MoveNext());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PublicMoveNextPollsInsideNativeFamilyAfterConstruction(bool compiled, bool expire)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex("target|needle", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        var input = Encoding.UTF8.GetBytes(new string('x', 320000));
        var armed = false;
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (!armed || entered) return;
            entered = true;
            if (expire) Thread.Sleep(250);
            else source.Cancel();
        });
        try
        {
            var enumerator = regex.EnumerateMatches(input, source.Token);
            armed = true;
            var failed = false;
            try { _ = enumerator.MoveNext(); }
            catch (OperationCanceledException e) { Assert.Equal(source.Token, e.CancellationToken); failed = true; }
            catch (RegexMatchTimeoutException e)
            {
                Assert.Equal(regex.Pattern, e.Pattern);
                Assert.Equal(regex.MatchTimeout, e.MatchTimeout);
                Assert.Equal(Encoding.UTF8.GetString(input), e.Input);
                failed = true;
            }
            Assert.True(failed);
            Assert.True(entered);
            Assert.False(enumerator.Current.Success);
            Assert.Equal(0, session.VerifierInvocations);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
        finally { session.Complete(); }
        Assert.True(regex.EnumerateMatches("target"u8).MoveNext());
    }

    public static IEnumerable<object[]> InterruptedCases()
    {
        foreach (var reverse in new[] { false, true })
        foreach (var phase in new[] { "scan", "compare", "trailing" })
        foreach (var expire in new[] { false, true }) yield return [reverse, phase, expire];
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PublicFamilyCancellationBetweenMatchesKeepsTokenAndUnpublishedCurrent(bool compiled, bool folded)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex("target|needle", RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (folded ? RegexOptions.IgnoreCase : RegexOptions.None));
        var enumerator = regex.EnumerateMatches("é target needle"u8, source.Token);
        Assert.True(enumerator.MoveNext());
        source.Cancel();
        var failed = false;
        try { _ = enumerator.MoveNext(); }
        catch (OperationCanceledException e) { Assert.Equal(source.Token, e.CancellationToken); failed = true; }
        Assert.True(failed);
        Assert.False(enumerator.Current.Success);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(InterruptedCases))]
    public void LongFamilyPhasesRetainOriginalTokenDeadlineAndCallerLease(bool reverse, string phase, bool expire)
    {
        using var source = new CancellationTokenSource();
        var payload = Encoding.UTF8.GetBytes(new string('a', 200000));
        var plan = phase == "compare" ? Plan([payload, "needle"u8.ToArray()]) :
            Plan(["target"u8.ToArray(), "needle"u8.ToArray()], trailing: phase == "trailing" ? payload : []);
        var input = phase == "scan" ? Encoding.UTF8.GetBytes(new string('x', 320000)) :
            phase == "compare" ? payload : [.. "target"u8, .. payload];
        var entered = false;
        var invocation = new Utf8SliceInvocation(new(0, 0, 7),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        invocation.CheckpointHook = _ =>
        {
            if (entered) return;
            entered = true;
            if (expire) Thread.Sleep(250);
            else source.Cancel();
        };
        var failed = false;
        // Restrict candidate discovery for the trailing-predicate lane while
        // retaining the full original subject for the lookahead comparison.
        var end = phase == "trailing" ? 6 : input.Length;
        try { _ = Utf8SearchExecutor.TryFindLiteralFamily(plan, input, 0, end, reverse, ref invocation, out _, out _); }
        catch (OperationCanceledException e) { Assert.Equal(source.Token, e.CancellationToken); failed = true; }
        catch (Utf8ExecutionDeadlineExpiredException) { Assert.True(expire); failed = true; }
        Assert.True(entered);
        Assert.True(failed);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    public static IEnumerable<object[]> DeadlineOperations()
    {
        foreach (var pattern in new[] { "target|needle", "café|niño", @"\b(?:foo|foobar|baz)\b" })
        foreach (var compiled in new[] { false, true })
        foreach (var operation in new[] { "ismatch", "count", "match" }) yield return [pattern, compiled, operation];
    }

    [Theory]
    [MemberData(nameof(DeadlineOperations))]
    public void DeadlineAwarePublicFamilyOperationsExpireInsideLongDiscovery(string pattern, bool compiled, string operation)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None),
            TimeSpan.FromMilliseconds(200));
        var input = Encoding.UTF8.GetBytes(new string('x', 320000));
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var validationSteps = validation.Budget.Steps;
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (!entered && steps > validationSteps) { entered = true; Thread.Sleep(250); }
        });
        try
        {
            var timeout = Assert.Throws<RegexMatchTimeoutException>(() =>
            {
                if (operation == "ismatch") _ = regex.IsMatch(input);
                else if (operation == "count") _ = regex.Count(input);
                else _ = regex.Match(input);
            });
            Assert.True(entered);
            Assert.Equal(pattern, timeout.Pattern);
            Assert.Equal(regex.MatchTimeout, timeout.MatchTimeout);
            Assert.Equal(Encoding.UTF8.GetString(input), timeout.Input);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(false, "ismatch")]
    [InlineData(true, "ismatch")]
    [InlineData(false, "count")]
    [InlineData(true, "count")]
    [InlineData(false, "match")]
    [InlineData(true, "match")]
    public void ValidationRetainsOriginalFamilyDeadlineUntilValueOperationFinishes(bool compiled, string operation)
    {
        var regex = new Utf8Regex("target|needle", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None),
            TimeSpan.FromMilliseconds(200));
        var input = Encoding.UTF8.GetBytes(new string('x', 320000));
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (!entered) { entered = true; Thread.Sleep(250); } });
        try
        {
            var timeout = Assert.Throws<RegexMatchTimeoutException>(() =>
            {
                if (operation == "ismatch") _ = regex.IsMatch(input);
                else if (operation == "count") _ = regex.Count(input);
                else _ = regex.Match(input);
            });
            Assert.True(entered);
            Assert.Equal(regex.Pattern, timeout.Pattern);
            Assert.Equal(regex.MatchTimeout, timeout.MatchTimeout);
            Assert.Equal(Encoding.UTF8.GetString(input), timeout.Input);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptureSensitiveFamilyKeepsOwnedUnitSemanticsWithLiveTokens(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        const string pattern = "(?<x-00>a)|b";
        const string text = "a a b b";
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        var oracle = new Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        Assert.Equal(oracle.Count(text), regex.Count(input, source.Token));
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, source.Token));
        Assert.Equal(oracle.Match(text).Index, regex.Match(input, source.Token).IndexInUtf16);
        var enumerator = regex.EnumerateMatches(input, source.Token);
        foreach (Match expected in oracle.Matches(text))
        {
            Assert.True(enumerator.MoveNext());
            Assert.Equal(expected.Index, enumerator.Current.IndexInUtf16);
            Assert.Equal(expected.Length, enumerator.Current.LengthInUtf16);
        }
        Assert.False(enumerator.MoveNext());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExhaustedFamilyCursorDoesNotRescanItsTerminalGap(bool folded)
    {
        var regex = new Utf8Regex("target|needle", RegexOptions.CultureInvariant | (folded ? RegexOptions.IgnoreCase : RegexOptions.None));
        var input = Encoding.UTF8.GetBytes(new string('x', 320000));
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        var cursor = new Utf8OperationMatchCursor(input, Prepared(regex), true, invocation);
        Assert.False(cursor.MoveNext());
        var first = cursor.DebugInvocationSteps;
        Assert.False(cursor.MoveNext());
        Assert.InRange(cursor.DebugInvocationSteps - first, 1UL, 4UL);
        Assert.True(first > 100);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DenseLaterBranchCountWorkGrowsLinearlyWithSubject(bool reverse)
    {
        var regex = new Utf8Regex("absent|a", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        var plan = Prepared(regex).SearchPlan;
        var small = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        var large = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        var smallInput = Encoding.UTF8.GetBytes(new string('a', 16384));
        var largeInput = Encoding.UTF8.GetBytes(new string('a', 32768));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BackendInstructionExecutor.TryCountLiteralFamily(plan, smallInput,
            reverse, ref small, out var smallCount));
        Assert.Equal(smallInput.Length, smallCount);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BackendInstructionExecutor.TryCountLiteralFamily(plan, largeInput,
            reverse, ref large, out var largeCount));
        Assert.Equal(largeInput.Length, largeCount);
        Assert.True(large.Budget.Steps <= small.Budget.Steps * 9 / 4);
    }
}
