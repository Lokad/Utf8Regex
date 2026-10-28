using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8StructuralInvocationTests
{
    private static Utf8PreparedRegex Prepared(Utf8Regex regex) =>
        (Utf8PreparedRegex)typeof(Utf8Regex).GetProperty("PreparedRegex", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(regex)!;

    private static PreparedSearcher Searcher(int kind) => kind switch {
        0 => new(new PreparedSubstringSearch("target"u8.ToArray(), false), false),
        1 => new(new PreparedSubstringSearch("TARGET"u8.ToArray(), true), true),
        2 => new(new PreparedMultiLiteralSearch(["target"u8.ToArray(), "needle"u8.ToArray(), "absent"u8.ToArray()], false)),
        3 => new(new PreparedMultiLiteralSearch(["TARGET"u8.ToArray(), "NEEDLE"u8.ToArray()], true)),
        4 => new(PreparedByteSearch.Create((byte)'t', (byte)'n')),
        5 => new(new PreparedQuotedAsciiRunSearch(Utf8SearchAsciiSet.FromBytes("ab"u8), 8193)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)) };

    public static IEnumerable<object[]> SearchCases()
    {
        foreach (var kind in Enumerable.Range(0, 6))
        foreach (var offset in new[] { 2047, 2048, 2049 })
        foreach (var reverse in new[] { false, true }) yield return [kind, offset, reverse];
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public void BoundedPreparedSearchKeepsCrossBlockCandidatesAndLegacyReverseStartOrder(int kind, int offset, bool reverse)
    {
        var searcher = Searcher(kind);
        var hit = kind == 5 ? "\"" + new string('a', 8193) + "\"" : "target needle TARGET";
        var input = Encoding.UTF8.GetBytes(new string('x', offset) + hit + new string('x', 2200) + hit);
        var expectedFound = reverse ? searcher.TryFindLastMatch(input, out var expected) : searcher.TryFindFirstMatch(input, out expected);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.None, searcher.TryFindMatch(input, 0, input.Length,
            reverse, ref invocation, out var actual, out var found));
        Assert.Equal(expectedFound, found);
        Assert.Equal(expected, actual);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawReverseCandidatesUseLatestStartWhileRegexFamilyExecutionUsesLatestEnd(bool folded)
    {
        var literals = new[] { "abcde"u8.ToArray(), "d"u8.ToArray() };
        var searcher = new PreparedSearcher(new PreparedMultiLiteralSearch(literals, folded));
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, searcher.TryFindMatch("abcde"u8, 0, 5, true,
            ref invocation, out var raw, out var found));
        Assert.True(found);
        Assert.Equal(3, raw.Index);
        var regex = new Utf8Regex("abcde|d", RegexOptions.CultureInvariant | (folded ? RegexOptions.IgnoreCase : RegexOptions.None));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindLiteralFamily(Prepared(regex).SearchPlan,
            "abcde"u8, 0, 5, true, ref invocation, out var execution, out found));
        Assert.True(found);
        Assert.Equal(0, execution.Index);
        Assert.Equal(5, execution.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    public void EveryPreparedSearchCutoffLeavesCoordinatesAndStateUnpublished(int kind)
    {
        var searcher = Searcher(kind);
        var hit = kind == 5 ? "\"" + new string('a', 8193) + "\"" : "target";
        var input = Encoding.UTF8.GetBytes(new string('x', 10000) + hit);
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        var initial = new PreparedSearchScanState(1, default);
        var state = initial;
        Assert.Equal(Utf8ExecutionLimitKind.None, searcher.TryFindNextOverlappingMatch(input, ref state,
            ref baseline, out _, out var found));
        Assert.True(found);
        for (var cap = 1UL; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            state = initial;
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, searcher.TryFindNextOverlappingMatch(input, ref state,
                ref invocation, out var denied, out found));
            Assert.False(found);
            Assert.Equal(default, denied);
            Assert.Equal(initial, state);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    private static Utf8StructuralSearchPlan WindowPlan(int maxLines = int.MaxValue) =>
        new(Utf8StructuralSearchYieldKind.Window, [
            Utf8StructuralSearchStage.FindWindow(new PreparedWindowSearch(Searcher(0),
                new PreparedSearcher(new PreparedSubstringSearch("end"u8.ToArray(), false), false))),
            Utf8StructuralSearchStage.RequireWithinLineSpan(maxLines), Utf8StructuralSearchStage.YieldWindow()]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PairedWindowsReuseTrailingProgressAndKeepCandidateFacts(bool bounded)
    {
        var plan = WindowPlan(3);
        var input = "target x target\r\nend target\n\n\nend target end"u8.ToArray();
        var expectedState = new Utf8StructuralSearchState(default, PreparedWindowScanState.Create(0));
        var actualState = expectedState;
        var invocation = new Utf8SliceInvocation(bounded ? new(ulong.MaxValue, 0, 7) : Utf8ExecutionLimits.Unlimited,
            Regex.InfiniteMatchTimeout, default);
        while (plan.TryFindNextCandidate(input, ref expectedState, out var expected))
        {
            Assert.Equal(Utf8ExecutionLimitKind.None, plan.TryFindNextCandidate(input, ref actualState,
                ref invocation, out var actual, out var found));
            Assert.True(found);
            Assert.Equal(expected, actual);
        }
        Assert.Equal(Utf8ExecutionLimitKind.None, plan.TryFindNextCandidate(input, ref actualState,
            ref invocation, out _, out var terminal));
        Assert.False(terminal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryWindowAndLineScanCutoffPreservesBorrowedLeaseAndOriginalState(bool crlf)
    {
        var plan = WindowPlan();
        var gap = crlf ? string.Concat(Enumerable.Repeat(new string('x', 2047) + "\r\n", 12)) : new string('x', 25000);
        var input = Encoding.UTF8.GetBytes("target" + gap + "end");
        var initial = new Utf8StructuralSearchState(default, PreparedWindowScanState.Create(0));
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        var state = initial;
        Assert.Equal(Utf8ExecutionLimitKind.None, plan.TryFindNextCandidate(input, ref state, ref baseline, out _, out var found));
        Assert.True(found);
        for (var cap = 1UL; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            state = initial;
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, plan.TryFindNextCandidate(input, ref state, ref invocation,
                out var denied, out found));
            Assert.False(found);
            Assert.Equal(default, denied);
            Assert.Equal(initial, state);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { @"(?:get|set)[A-Z][A-Za-z0-9_]*", @"\b(?:get|set)[A-Z]\w*\b",
            @"\b(?:LogError|LogWarning|LogInformation)\s*\(", @"(?:foo|bar)\s+End\b",
            @"(?:foo|foobar)[A-Z][A-Za-z0-9_]*", @"(?<n>get|set)(?<v>[A-Z]\w*)" })
        foreach (var compiled in new[] { false, true }) yield return [pattern, compiled];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void CompleteOwnedOperationsAndLiveTokenValuesRetainUnicodeAndCaptureSemantics(string pattern, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        var oracle = new Regex(pattern, options);
        string[] texts = ["", "getAlpha setBeta LogError ( LogWarning( foo End bar End foobarAlpha",
            "é😀 getAlphaé setBeta中 LogInformation\u2003( foo\u2003End bar End", "xgetA getA_ getAé\nsetB\r\nLogError ( "];
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, oracle, texts);
        using var source = new CancellationTokenSource();
        foreach (var text in texts)
        {
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(oracle.Count(text), regex.Count(input, source.Token));
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, source.Token));
            var expected = oracle.Match(text);
            var actual = regex.Match(input, source.Token);
            Assert.Equal(expected.Success, actual.Success);
            if (!expected.Success) continue;
            Assert.Equal(expected.Index, actual.IndexInUtf16);
            Assert.Equal(expected.Length, actual.LengthInUtf16);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.IndexInBytes);
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static IEnumerable<object[]> InterruptedOperations()
    {
        foreach (var method in new[] { false, true })
        foreach (var compiled in new[] { false, true })
        foreach (var operation in new[] { "ismatch", "count", "match" })
        foreach (var phase in new[] { "validation", "search", "verifier" })
        foreach (var expire in new[] { false, true }) yield return [method, compiled, operation, phase, expire];
    }

    public static IEnumerable<object[]> OverlapCases()
    {
        foreach (var compiled in new[] { false, true })
        foreach (var finite in new[] { false, true })
        foreach (var liveToken in new[] { false, true }) yield return [compiled, finite, liveToken];
    }

    [Theory]
    [MemberData(nameof(OverlapCases))]
    public void MixedStructuralSubjectsRetainUnicodeWhitespaceAndUtf16CoordinatesInEveryValueLane(bool compiled, bool finite, bool liveToken)
    {
        foreach (var pattern in new[] { @"(?:get|set)[A-Z][A-Za-z0-9_]*", @"\b(?:LogError|LogWarning|LogInformation)\s*\(" })
        foreach (var text in new[] { "é😀 getAlphaé setBeta中 LogInformation\u2003( ", "xgetA getA_ getAé\nsetB\r\nLogError ( " })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options, finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout);
            var oracle = new Regex(pattern, options);
            var input = Encoding.UTF8.GetBytes(text);
            using var source = new CancellationTokenSource();
            var token = liveToken ? source.Token : default;
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, token));
            Assert.Equal(oracle.Count(text), regex.Count(input, token));
            var expectedMatch = oracle.Match(text);
            var actualMatch = regex.Match(input, token);
            Assert.True(actualMatch.Success);
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
            Assert.Equal(expectedMatch.Value, Encoding.UTF8.GetString(input.AsSpan(actualMatch.IndexInBytes, actualMatch.LengthInBytes)));
            var expected = oracle.EnumerateMatches(text);
            var actual = regex.EnumerateMatches(input, token);
            while (expected.MoveNext())
            {
                Assert.True(actual.MoveNext());
                Assert.Equal(expected.Current.Index, actual.Current.IndexInUtf16);
                Assert.Equal(expected.Current.Length, actual.Current.LengthInUtf16);
            }
            Assert.False(actual.MoveNext());
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    [Theory]
    [MemberData(nameof(OverlapCases))]
    public void ShorterRejectedStructuralPrefixRetriesLongerBranchInEveryValueLane(bool compiled, bool finite, bool liveToken)
    {
        const string pattern = @"(?:foo|foobar)[A-Z][A-Za-z0-9_]*";
        const string text = "foobarAlpha fooBeta foobarGamma";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout);
        var oracle = new Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(Prepared(regex).StructuralIdentifierFamilyPlan.HasUnresolvedPrefixOverlap);
        using var source = new CancellationTokenSource();
        var token = liveToken ? source.Token : default;
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, token));
        Assert.Equal(oracle.Count(text), regex.Count(input, token));
        var match = regex.Match(input, token);
        Assert.True(match.Success);
        Assert.Equal(oracle.Match(text).Value, Encoding.UTF8.GetString(input.AsSpan(match.IndexInBytes, match.LengthInBytes)));
        var expected = oracle.EnumerateMatches(text);
        var actual = regex.EnumerateMatches(input, token);
        while (expected.MoveNext())
        {
            Assert.True(actual.MoveNext());
            Assert.Equal(expected.Current.Index, actual.Current.IndexInUtf16);
            Assert.Equal(expected.Current.Length, actual.Current.LengthInUtf16);
        }
        Assert.False(actual.MoveNext());
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [MemberData(nameof(InterruptedOperations))]
    public void PublicStructuralPhasesRetainEntryTokenDeadlineAndMappedFailure(bool method, bool compiled, string operation, string phase, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var pattern = method ? @"\b(?:LogError|LogWarning|LogInformation)\s*\(" : @"(?:get|set)[A-Z][A-Za-z0-9_]*";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, Prepared(regex).ExecutionKind);
        var text = phase == "verifier" ? (method ? "LogError" + new string(' ', 320000) + "(" : "getA" + new string('a', 320000))
            : new string('x', 320000);
        var input = Encoding.UTF8.GetBytes(text);
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var validationSteps = validation.Budget.Steps;
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (entered || phase == "search" && steps <= validationSteps ||
                phase == "verifier" && Utf8SearchDiagnosticsSession.Current!.VerifierInvocations == 0) return;
            entered = true;
            if (expire) clock.Advance(250);
            else source.Cancel();
        });
        try
        {
            void Run()
            {
                if (operation == "ismatch") _ = regex.IsMatch(input, source.Token);
                else if (operation == "count") _ = regex.Count(input, source.Token);
                else _ = regex.Match(input, source.Token);
            }
            if (expire)
            {
                var timeout = Assert.Throws<RegexMatchTimeoutException>(Run);
                Assert.Equal(pattern, timeout.Pattern);
                Assert.Equal(regex.MatchTimeout, timeout.MatchTimeout);
                Assert.Equal(text, timeout.Input);
            }
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
            Assert.True(entered);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
        finally { session.Complete(); }
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(method ? "LogError(" : "getAlpha")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedStructuralCountNeverPublishesPartialTotalAndIncludesValidationWork(bool method)
    {
        var pattern = method ? @"\b(?:LogError|LogWarning|LogInformation)\s*\(" : @"(?:get|set)[A-Z][A-Za-z0-9_]*";
        var prepared = Prepared(new Utf8Regex(pattern, RegexOptions.CultureInvariant));
        var input = Encoding.UTF8.GetBytes(method ? "LogError ( LogWarning( LogInformation (" : "getAlpha setBeta getGamma");
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref baseline);
        var validationSteps = baseline.Budget.Steps;
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8AsciiStructuralIdentifierFamilyExecutor.TryCount(prepared.StructuralSearchPlan,
            prepared.StructuralVerifier.AsciiProgram, input, ref baseline, out var count));
        Assert.Equal(3, count);
        for (var cap = validationSteps; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            _ = Utf8Validation.Validate(input, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8AsciiStructuralIdentifierFamilyExecutor.TryCount(prepared.StructuralSearchPlan,
                prepared.StructuralVerifier.AsciiProgram, input, ref invocation, out var denied));
            Assert.Equal(0, denied);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartTransformsAndFullSubjectBoundaryRequirementsAreBoundedInBothDirections(bool reverse)
    {
        var plan = new Utf8StructuralSearchPlan(Utf8StructuralSearchYieldKind.Start, [
            Utf8StructuralSearchStage.FindLiteralFamily(Searcher(0)),
            Utf8StructuralSearchStage.TransformCandidateStart(new(0, Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace)),
            Utf8StructuralSearchStage.RequireLeadingBoundary(Utf8BoundaryRequirement.Boundary),
            Utf8StructuralSearchStage.RequireTrailingBoundary(Utf8BoundaryRequirement.Boundary),
            Utf8StructuralSearchStage.RequireTrailingLiteral("!"u8.ToArray()), Utf8StructuralSearchStage.YieldStart()]);
        var input = Encoding.UTF8.GetBytes("xtarget! " + new string(' ', 140000) + "target! targetx");
        var expectedState = default(Utf8StructuralSearchState);
        var expectedFound = reverse ? plan.TryFindLastCandidate(input, input.Length, out var expected)
            : plan.TryFindNextCandidate(input, ref expectedState, out expected);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        var state = default(Utf8StructuralSearchState);
        var breach = reverse ? plan.TryFindLastCandidate(input, input.Length, ref invocation, out var actual, out var found)
            : plan.TryFindNextCandidate(input, ref state, ref invocation, out actual, out found);
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal(expectedFound, found);
        Assert.Equal(expected, actual);
        Assert.True(invocation.Budget.Steps > 64);
    }

    public static IEnumerable<object[]> FixedCases()
    {
        foreach (var set in new[] { false, true })
        foreach (var reverse in new[] { false, true })
        foreach (var offset in new[] { 2047, 2048, 2049 }) yield return [set, reverse, offset];
    }

    [Theory]
    [MemberData(nameof(FixedCases))]
    public void FixedDistanceSearchesKeepOffsetsAndAllSetGuards(bool set, bool reverse, int offset)
    {
        var ascii = set ? PreparedAsciiFindPlan.CreateFixedDistanceSet([Utf8FixedDistanceSet.FromBytes(1, [(byte)'a'], false), Utf8FixedDistanceSet.FromBytes(3, [(byte)'c'], false)])
            : PreparedAsciiFindPlan.CreateFixedDistanceLiteral("target"u8.ToArray(), 2);
        var plan = new Utf8StructuralSearchPlan(Utf8StructuralSearchYieldKind.Start,
            [Utf8StructuralSearchStage.FindAscii(ascii), Utf8StructuralSearchStage.YieldStart()]);
        var hit = set ? "zaQc" : "zztarget";
        var input = Encoding.UTF8.GetBytes(new string('x', offset) + hit + "xxxxx" + hit);
        var original = default(Utf8StructuralSearchState);
        var expectedFound = reverse ? plan.TryFindLastCandidate(input, input.Length, out var expected)
            : plan.TryFindNextCandidate(input, ref original, out expected);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        var state = default(Utf8StructuralSearchState);
        var breach = reverse ? plan.TryFindLastCandidate(input, input.Length, ref invocation, out var actual, out var found)
            : plan.TryFindNextCandidate(input, ref state, ref invocation, out actual, out found);
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.True(found);
        Assert.Equal(expectedFound, found);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixedDistanceWorkDenialNeverPublishesItsPendingAnchor(bool set)
    {
        var plan = set ? PreparedAsciiFindPlan.CreateFixedDistanceSet([Utf8FixedDistanceSet.FromBytes(1, [(byte)'a'], false), Utf8FixedDistanceSet.FromBytes(3, [(byte)'c'], false)])
            : PreparedAsciiFindPlan.CreateFixedDistanceLiteral("target"u8.ToArray(), 2);
        var input = Encoding.UTF8.GetBytes(new string('x', 10000) + (set ? "zaQc" : "zztarget"));
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8AsciiFindExecutor.TryFindNextFixedDistanceCandidate(input, plan, 0,
            ref baseline, out _, out _, out var found));
        Assert.True(found);
        for (var cap = 1UL; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8AsciiFindExecutor.TryFindNextFixedDistanceCandidate(input, plan, 0,
                ref invocation, out var index, out var length, out found));
            Assert.Equal(-1, index);
            Assert.Equal(0, length);
            Assert.False(found);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicCountCancelsAfterAcceptedMatchesWithoutReturningPartialTotal(bool compiled)
    {
        var regex = new Utf8Regex(@"(?:get|set)[A-Z][A-Za-z0-9_]*",
            RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("getAlpha setBeta ", 2000)));
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => {
            if (entered || Utf8SearchDiagnosticsSession.Current!.VerifierMatches < 8) return;
            entered = true;
            source.Cancel();
        });
        try
        {
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
            Assert.True(entered);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
        finally { session.Complete(); }
        Assert.Equal(4000, regex.Count(input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PairedWindowTrailingSearchWorkAndTerminalProgressStayLinear(bool trailingHit)
    {
        var search = new PreparedWindowSearch(Searcher(0),
            new PreparedSearcher(new PreparedSubstringSearch("end"u8.ToArray(), false), false));
        var measured = new List<ulong>();
        foreach (var repeats in new[] { 128, 256 })
        {
            var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("target x ", repeats)) +
                new string('x', 4096) + (trailingHit ? "end" : ""));
            var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var state = PreparedWindowScanState.Create(0);
            var count = 0;
            while (true)
            {
                Assert.Equal(Utf8ExecutionLimitKind.None, search.TryFindNextWindow(input, ref state, ref invocation, out _, out var found));
                if (!found) break;
                count++;
            }
            Assert.Equal(trailingHit ? repeats : 0, count);
            measured.Add(invocation.Budget.Steps);
            var terminalSteps = invocation.Budget.Steps;
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(Utf8ExecutionLimitKind.None, search.TryFindNextWindow(input, ref state, ref invocation, out var window, out var found));
                Assert.False(found);
                Assert.Equal(default, window);
            }
            Assert.InRange(invocation.Budget.Steps - terminalSteps, 0UL, 30UL);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
        Assert.True(measured[1] <= measured[0] * 3, $"Work grew from {measured[0]} to {measured[1]}.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrLfCrossingEveryBlockBoundaryCannotSkipLineScanTokenOrDeadlineCheckpoints(bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var gap = string.Concat(Enumerable.Repeat(new string('x', 2047) + "\r\n", 200));
        var input = Encoding.UTF8.GetBytes("target" + gap + "end");
        var window = new PreparedWindowMatch(new(0, 6, 0), new(input.Length - 3, 3, 0));
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(0, 0, 7), expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var entered = false;
        invocation.CheckpointHook = _ => { if (!entered) { entered = true; if (expire) clock.Advance(250); else source.Cancel(); } };
        var accepted = false;
        void Run() => Utf8StructuralSearchExecutor.TrySatisfyWindowRequirements(input,
            [Utf8StructuralSearchStage.RequireWithinLineSpan(int.MaxValue)], window, ref invocation, out accepted);
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(Run);
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
        Assert.True(entered);
        Assert.False(accepted);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }
}
