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
        _ => throw new ArgumentOutOfRangeException(nameof(kind)) };

    public static IEnumerable<object[]> SearchCases()
    {
        foreach (var kind in Enumerable.Range(0, 5))
        foreach (var offset in new[] { 2047, 2048, 2049 })
        foreach (var reverse in new[] { false, true }) yield return [kind, offset, reverse];
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public void BoundedPreparedSearchKeepsCrossBlockCandidatesAndLegacyReverseStartOrder(int kind, int offset, bool reverse)
    {
        var searcher = Searcher(kind);
        var hit = "target needle TARGET";
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
        var regex = new Utf8Regex("abcde|d", options: RegexOptions.CultureInvariant | (folded ? RegexOptions.IgnoreCase : RegexOptions.None));
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
    public void BoundedPreparedSearchDenialLeavesCoordinatesUnpublished(int kind)
    {
        var searcher = Searcher(kind);
        var hit = "target";
        var input = Encoding.UTF8.GetBytes(new string('x', 10000) + hit);
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, searcher.TryFindMatch(input, 1, input.Length,
            false, ref baseline, out _, out var found));
        Assert.True(found);
        for (var cap = 1UL; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, searcher.TryFindMatch(input, 1, input.Length,
                false, ref invocation, out var denied, out found));
            Assert.False(found);
            Assert.Equal(default, denied);
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
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(10)
        });
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
            var actual = regex.Match(input, cancellationToken: source.Token);
            Assert.Equal(expected.Success, actual.Success);
            if (!expected.Success) continue;
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length)), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.StartOffsetInBytes);
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static IEnumerable<object[]> InterruptedOperations()
    {
        foreach (var method in new[] { false, true })
        foreach (var compiled in new[] { false, true })
        foreach (var operation in new[] { "ismatch", "count", "match" })
        foreach (var phase in new[] { "entry", "execution" })
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
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout
            });
            var oracle = new Regex(pattern, options);
            var input = Encoding.UTF8.GetBytes(text);
            using var source = new CancellationTokenSource();
            var token = liveToken ? source.Token : default;
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, token));
            Assert.Equal(oracle.Count(text), regex.Count(input, token));
            var expectedMatch = oracle.Match(text);
            var actualMatch = regex.Match(input, cancellationToken: token);
            Assert.True(actualMatch.Success);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
                Assert.Equal(expectedMatch.Value, Encoding.UTF8.GetString(input.AsSpan(actualMatch.StartOffsetInBytes, (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes))));
                var expected = oracle.EnumerateMatches(text);
            var actual = regex.EnumerateMatches(input, cancellationToken: token);
            while (expected.MoveNext())
            {
                Assert.True(actual.MoveNext());
                    Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Current.Index)), actual.Current.StartOffsetInBytes);
                    Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Current.Index, expected.Current.Length)), (actual.Current.EndOffsetInBytes - actual.Current.StartOffsetInBytes));
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
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout
        });
        var oracle = new Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        var token = liveToken ? source.Token : default;
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, token));
        Assert.Equal(oracle.Count(text), regex.Count(input, token));
        var match = regex.Match(input, cancellationToken: token);
        Assert.True(match.Success);
        Assert.Equal(oracle.Match(text).Value, Encoding.UTF8.GetString(input.AsSpan(match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))));
        var expected = oracle.EnumerateMatches(text);
        var actual = regex.EnumerateMatches(input, cancellationToken: token);
        while (expected.MoveNext())
        {
            Assert.True(actual.MoveNext());
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Current.Index)), actual.Current.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Current.Index, expected.Current.Length)), (actual.Current.EndOffsetInBytes - actual.Current.StartOffsetInBytes));
        }
        Assert.False(actual.MoveNext());
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [MemberData(nameof(InterruptedOperations))]
    public void PublicStructuralPhasesRetainEntryTokenDeadlineAndMappedFailure(bool method, bool compiled, string operation, string phase, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        // A Boolean call can finish before scanning an optional suffix. Give
        // its identifier branch enough required work to reach the checkpoint.
        const int requiredIdentifierTail = 128;
        var pattern = method ? @"\b(?:LogError|LogWarning|LogInformation)\s*\(" :
            "(?:get|set)[A-Z][A-Za-z0-9_]" + (operation == "ismatch" ? $"{{{requiredIdentifierTail},}}" : "*");
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout
        });
        var text = phase == "execution" ? (method ? "LogError" + new string(' ', 320000) + "(" : "getA" + new string('a', 320000))
            : new string('x', 320000);
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (entered || phase == "execution" && steps < 8) return;
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
                else _ = regex.Match(input, cancellationToken: source.Token);
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
        var positive = method ? "LogError(" : "getA" + new string('a', requiredIdentifierTail);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(positive)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicCountCancelsDuringRepeatedMatchesWithoutReturningPartialTotal(bool compiled)
    {
        var regex = new Utf8Regex(@"(?:get|set)[A-Z][A-Za-z0-9_]*", options:             RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("getAlpha setBeta ", 2000)));
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (entered || steps < 256) return;
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
}
