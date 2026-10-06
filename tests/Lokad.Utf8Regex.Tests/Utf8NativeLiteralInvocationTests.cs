using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeLiteralInvocationTests
{
    private static Utf8SearchPlan Plan(byte[] literal, bool folded, byte[] trailing)
        => Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(folded ? Utf8SearchKind.AsciiFoldedByteLiteral : Utf8SearchKind.ExactAsciiLiteral,
            new() { LiteralUtf8 = literal, TrailingLiteralUtf8 = trailing }));

    private static Utf8PreparedRegex Prepared(Utf8Regex regex)
        => Assert.IsType<Utf8PreparedRegex>(typeof(Utf8Regex).GetProperty("PreparedRegex", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(regex));

    public static IEnumerable<object[]> SearchCases()
    {
        foreach (var length in new[] { 1, 2, 2047, 2048, 2049, 8193 })
        foreach (var offset in new[] { 2047, 2048, 2049 })
        foreach (var folded in new[] { false, true })
        foreach (var reverse in new[] { false, true }) yield return [length, offset, folded, reverse];
    }
    [Theory]
    [MemberData(nameof(SearchCases))]
    public void LiteralDiscoveryKeepsCrossBlockHitsAndBothOrders(int length, int offset, bool folded, bool reverse)
    {
        var needle = new string('a', length - 1) + "B";
        var hit = folded ? needle.ToLowerInvariant() : needle;
        var text = new string('x', offset) + hit + "xx" + hit;
        var input = Encoding.UTF8.GetBytes(text);
        var plan = Plan(Encoding.UTF8.GetBytes(needle), folded, []);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var expected = reverse ? text.LastIndexOf(hit, StringComparison.Ordinal) : offset;
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteral(plan, input, 0, input.Length,
            reverse, ref invocation, out var index));
        Assert.Equal(expected, index);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteral(plan, input,
            reverse ? 0 : index + length, reverse ? index : input.Length, reverse, ref invocation, out var next));
        Assert.Equal(reverse ? offset : text.LastIndexOf(hit, StringComparison.Ordinal), next);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryWorkCutoffAcrossSuccessiveSearchAndProjectionLeavesNoResultAndKeepsCallerLease(bool reverse)
    {
        var regex = new Utf8Regex("target", RegexOptions.CultureInvariant);
        var plan = Prepared(regex);
        var input = Encoding.UTF8.GetBytes("target" + new string('中', 5000) + "target" + new string('é', 5000) + "target");
        var baseline = new Utf8OperationMatchCursor(input, plan, false, reverse,
            new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None));
        var hits = 0;
        while (baseline.TryMoveNextLiteral(out var found) == Utf8ExecutionLimitKind.None && found) hits++;
        Assert.Equal(3, hits);
        var total = baseline.DebugInvocationSteps;
        for (var cap = 1UL; cap < total; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.True(invocation.Ledger.TryReserve(7));
            var cursor = new Utf8OperationMatchCursor(input, plan, false, reverse, invocation);
            Utf8ExecutionLimitKind breach;
            bool found;
            do { breach = cursor.TryMoveNextLiteral(out found); } while (breach == Utf8ExecutionLimitKind.None && found);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, breach);
            Assert.False(found);
            Assert.False(cursor.CurrentValueMatch.Success);
            Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
        }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EveryLongComparisonCutoffRetainsLimitKindAndUnpublishedIndex(bool reverse, bool trailingPredicate)
    {
        var payload = Encoding.UTF8.GetBytes(new string('a', 8193));
        var needle = trailingPredicate ? "target"u8.ToArray() : payload;
        var plan = Plan(needle, false, trailingPredicate ? payload : []);
        byte[] input = [.. Enumerable.Repeat((byte)'x', 5000), .. needle, .. (trailingPredicate ? payload : [])];
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteral(plan, input, 0, input.Length,
            reverse, ref baseline, out var expected));
        Assert.Equal(5000, expected);
        for (var cap = 1UL; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.True(invocation.Ledger.TryReserve(7));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8SearchExecutor.TryFindLiteral(plan, input, 0, input.Length,
                reverse, ref invocation, out var denied));
            Assert.Equal(-1, denied);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    public static IEnumerable<object[]> InterruptedCases()
    {
        foreach (var reverse in new[] { false, true })
        foreach (var phase in new[] { "scan", "compare", "trailing" })
        foreach (var expire in new[] { false, true }) yield return [reverse, phase, expire];
    }
    [Theory]
    [MemberData(nameof(InterruptedCases))]
    public void LongSearchComparisonAndTrailingPredicatePollOriginalInvocation(bool reverse, string phase, bool expire)
    {
        var literal = Encoding.UTF8.GetBytes(phase == "compare" ? new string('a', 160000) + "b" : "target");
        var trailing = phase == "trailing" ? Encoding.UTF8.GetBytes(new string('z', 160000)) : [];
        var input = Encoding.UTF8.GetBytes(phase switch {
            "compare" => new string('a', 320000), "trailing" => "target" + new string('z', 160000), _ => new string('x', 320000) });
        var plan = Plan(literal, false, trailing);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var entered = false;
        invocation.CheckpointHook = _ => { if (!entered) { entered = true; if (expire) Thread.Sleep(250); else source.Cancel(); } };
        var index = -1;
        void Run() => Utf8SearchExecutor.TryFindLiteral(plan, input, 0, input.Length, reverse, ref invocation, out index);
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(Run);
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
        Assert.True(entered);
        Assert.Equal(-1, index);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        var recovery = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteral(plan, "target"u8, 0, 6, reverse, ref recovery, out _));
    }
    [Theory]
    [InlineData(@"\ba", "aaa a", false)]
    [InlineData(@"a\b", "a aaa", true)]
    [InlineData(@"\Ba", "aaa a", false)]
    [InlineData(@"a\B", "a aaa", true)]
    [InlineData(@"\ba", "éa a", false)]
    [InlineData(@"a\b", "a aé", true)]
    public void CursorBoundariesInspectOriginalNeighborsAcrossMatches(string pattern, string text, bool reverse)
    {
        var options = RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        var cursor = new Utf8OperationMatchCursor(input, Prepared(regex), false, reverse,
            new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None));
        foreach (Match expected in new Regex(pattern, options).Matches(text))
        {
            Assert.True(cursor.MoveNext());
            Assert.Equal(expected.Index, cursor.CurrentValueMatch.IndexInUtf16);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), cursor.CurrentValueMatch.IndexInBytes);
        }
        Assert.False(cursor.MoveNext());
    }
    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { "target", "中", "AbCd", @"\ba", @"a\b" })
        foreach (var compiled in new[] { false, true })
        foreach (var reverse in new[] { false, true }) yield return [pattern, RegexOptions.CultureInvariant |
            (pattern == "AbCd" ? RegexOptions.IgnoreCase : RegexOptions.None) |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (reverse ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    public static IEnumerable<object[]> BoundaryOperationCases()
    {
        foreach (var pattern in new[] { @"\ba", @"a\b", @"\Ba", @"a\B" })
        foreach (var compiled in new[] { false, true })
        foreach (var reverse in new[] { false, true }) yield return [pattern, RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (reverse ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    public static IEnumerable<object[]> NonBacktrackingCases()
    {
        foreach (var pattern in new[] { "", "target", "中", "(target)", "a|ab", @"\ba", @"a\b", "(a|ab)", "(?<x>target)" })
        foreach (var compiled in new[] { false, true }) yield return [pattern, RegexOptions.CultureInvariant |
            RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(NonBacktrackingCases))]
    public void NonBacktrackingLiteralsServeAllOwnedOperations(string pattern, RegexOptions options)
    {
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        var oracle = new Regex(pattern, options, TimeSpan.FromSeconds(10));
        Assert.Equal(Utf8ExecutionBackend.NativeLiteral, Prepared(regex).ExecutionBackend);
        // Construction validation remains a recorded BCL dependency. Replace its
        // already-built engine accessors with fail-fast sentinels so this proves
        // execution ownership without misrepresenting constructor independence.
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
        ForbidFallbackExecution(regex);
        if (pattern.Length == 0)
            Utf8ScalarCountTests.AssertEmptyScalarOperations(regex, false);
        else
            Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, oracle,
                ["", "target target", "a ab aba", "aaa a", "é中\U0001f600 target 中"]);
    }

    private static void ForbidFallbackExecution(Utf8Regex regex)
    {
        var field = typeof(Utf8Regex).GetProperty("_verifierRuntime", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var runtime = Assert.IsType<Utf8VerifierRuntime>(field.GetValue(regex));
        foreach (var name in new[] { "_fallbackRegex", "_anchoredFallbackRegex" })
        {
            var engine = typeof(Utf8FallbackCandidateVerifier).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(engine);
            engine.SetValue(runtime.FallbackCandidateVerifier,
                new Lazy<Regex>(() => throw new InvalidOperationException("Native NonBacktracking execution accessed BCL fallback.")));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedCaptureDeclarationsKeepNonBacktrackingHistoryContract(bool compiled)
    {
        const string pattern = "(?<x>(?<x>a)b)";
        var options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options).Match("ab");
        Assert.Equal(Utf8ExecutionBackend.FallbackRegex, Prepared(regex).ExecutionBackend);
        var actual = regex.MatchDetailed("ab"u8).GetGroup("x");
        Assert.Equal(oracle.Groups["x"].Value, actual.GetValueString());
        Assert.Equal(oracle.Groups["x"].Captures.Count, actual.CaptureCount);
    }

    public static IEnumerable<object[]> SemanticExclusionCases()
    {
        var patterns = new[] { "\ud83d", "(\ud83d)", @"(\ud83d)", "\ude00", "(?i:é)", "(?i:AbCd)", "k" };
        for (var id = 0; id < patterns.Length; id++)
        foreach (var compiled in new[] { false, true })
        foreach (var reverse in new[] { false, true }) yield return [id, patterns[id], RegexOptions.CultureInvariant |
            (patterns[id] == "k" ? RegexOptions.IgnoreCase : RegexOptions.None) |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (reverse ? RegexOptions.RightToLeft : RegexOptions.None)];
        foreach (var compiled in new[] { false, true })
        {
            yield return [7, @"\ba", RegexOptions.CultureInvariant | RegexOptions.ECMAScript | (compiled ? RegexOptions.Compiled : RegexOptions.None)];
            yield return [8, "target", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : RegexOptions.None)];
        }
    }

    [Theory]
    [MemberData(nameof(SemanticExclusionCases))]
    public void NativeSelectionKeepsScalarFoldingAndOptionSemantics(int patternId, string pattern, RegexOptions options)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex(pattern, options);
        if ((options & RegexOptions.NonBacktracking) != 0) ForbidFallbackExecution(regex);
        foreach (var text in new[] { "éÉ", "éa a", "abcd ABCD", "KkK", "\U0001f600\U0001f600", "target target" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var actual = regex.EnumerateMatches(input, source.Token);
            // A lone surrogate denotes no scalar on strict UTF-8 subjects.
            // Keep its syntax/owned-route controls; half matches are superseded.
            if (patternId <= 3)
            {
                Assert.Equal(0, regex.Count(input, source.Token));
                Assert.False(regex.Match(input, source.Token).Success);
            }
            else
                foreach (Match expected in new Regex(pattern, options).Matches(text))
                {
                    Assert.True(actual.MoveNext());
                    Assert.Equal(expected.Index, actual.Current.IndexInUtf16);
                    Assert.Equal(expected.Length, actual.Current.LengthInUtf16);
                }
            Assert.False(actual.MoveNext());
        }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, $"Semantic exclusion {patternId}");
    }

    [Theory]
    [MemberData(nameof(BoundaryOperationCases))]
    public void BoundaryFixPreservesAllOwnedPublicOperations(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "aaa a", "a aaa", "éa a aé", "a\U0001f600a", " a a\n"], options);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EmptyLiteralOperationsPreserveCompleteScalars(bool compiled, bool reverse)
        => Utf8ScalarCountTests.AssertEmptyScalarOperations(compiled, reverse);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void NativeEnumerationRetainsCancellationBetweenMatches(bool compiled, bool reverse)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex("target", RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (reverse ? RegexOptions.RightToLeft : RegexOptions.None));
        var enumerator = regex.EnumerateMatches("target target"u8, source.Token);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(reverse ? 7 : 0, enumerator.Current.IndexInUtf16);
        source.Cancel();
        var cancelled = false;
        try { _ = enumerator.MoveNext(); }
        catch (OperationCanceledException e) { Assert.Equal(source.Token, e.CancellationToken); cancelled = true; }
        Assert.True(cancelled);
        Assert.False(enumerator.Current.Success);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void LiveTokenEnumerationPreservesOrdinaryCompiledAndReverseRanges(string pattern, RegexOptions options)
    {
        using var source = new CancellationTokenSource();
        foreach (var text in new[] { "", "target target", "é中\U0001f600 target 中 abCD", "aaa a aé éa", "miss" })
        {
            var regex = new Utf8Regex(pattern, options);
            var input = Encoding.UTF8.GetBytes(text);
            var actual = regex.EnumerateMatches(input, source.Token);
            foreach (Match expected in new Regex(pattern, options).Matches(text))
            {
                Assert.True(actual.MoveNext());
                Assert.Equal(expected.Index, actual.Current.IndexInUtf16);
                Assert.Equal(expected.Length, actual.Current.LengthInUtf16);
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.Current.IndexInBytes);
            }
            Assert.False(actual.MoveNext());
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PublicCancellationOrTimeoutAfterConstructionOccursInsideNativeMoveNext(bool compiled, bool expire)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex("target", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        var input = Encoding.UTF8.GetBytes(new string('x', 320000));
        var armed = false;
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (armed && !entered) { entered = true; if (expire) Thread.Sleep(250); else source.Cancel(); } });
        try
        {
            var enumerator = regex.EnumerateMatches(input, source.Token);
            armed = true;
            var cancelled = false;
            try { _ = enumerator.MoveNext(); }
            catch (OperationCanceledException e) { Assert.Equal(source.Token, e.CancellationToken); cancelled = true; }
            catch (RegexMatchTimeoutException e) { Assert.Equal("target", e.Pattern); Assert.Equal(regex.MatchTimeout, e.MatchTimeout); Assert.Equal(Encoding.UTF8.GetString(input), e.Input); cancelled = true; }
            Assert.True(cancelled);
            Assert.True(entered);
            Assert.False(enumerator.Current.Success);
            Assert.Equal(0, session.VerifierInvocations);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
        finally { session.Complete(); }
        var recovered = regex.EnumerateMatches("target"u8);
        Assert.True(recovered.MoveNext());
    }
}
