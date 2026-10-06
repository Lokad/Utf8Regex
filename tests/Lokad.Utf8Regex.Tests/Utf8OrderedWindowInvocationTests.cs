using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OrderedWindowInvocationTests
{
    private static Utf8PreparedRegex Prepared(Utf8Regex regex) =>
        (Utf8PreparedRegex)typeof(Utf8Regex).GetProperty("PreparedRegex", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(regex)!;

    private static readonly string[] Patterns = [@"a\s+b", @"a\s{2,}b", @"a\s+?b", @"a.{0,4}b", @"river.{10,25}Tom",
        @"Holmes.{0,25}Watson|Watson.{0,25}Holmes", @"\b(?:public|private|internal)\b\s+.{0,80}\bclass\b",
        @"HttpClient(?=\s+client)", @"(?:a|ab).{0,4}z", @"a\s+ b", @"a\s+ ", @"a.{0,4}\nb", @"(?:foo|bar).{0,8}z",
        @"a[ \t]+b", @"a[ \t]+?.{0,4}b", @"a\s*.{0,4}b", @"a\s+?.{0,4}b",
        @"a[ ]+.{0,4}z|b[\t]+?.{0,4}z", @"a[ \t]+bb", @"a\s+ bb", @"a \s+bb", @"(?:a|long )\s+.{0,4}bb"];

    private static readonly string[] Texts = ["", "xa  ba\t\nb a1b2b a3b a\nb a  b a\n\nb abz a1z2z foo1z2z barz foobaz",
        "Holmes and Watson Watson met Holmes Holmes and Watson", "public sealed class One private class Two internal partial class Three",
        "HttpClient client HttpClient\tclient HttpClient other", "river123456789Tom river1234567890Tom",
        "a\t\t b a     a\r\nb a\n\nb a\tb a\v\fb a   z b\t\tz a\nbb a\tbb a  bb long  bb",
        "é😀 a\u2003b Holmes and Watson public\u2003class One HttpClient\u2003client"];

    public static IEnumerable<object[]> ValueCases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var finite in new[] { false, true })
        foreach (var liveToken in new[] { false, true }) yield return [pattern, compiled, finite, liveToken];
    }

    [Theory]
    [MemberData(nameof(ValueCases))]
    public void ValueAndEnumerationModesPreserveGreedyRangesSeparatorsAndBranchPriority(string pattern, bool compiled, bool finite, bool liveToken)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout);
        Assert.Equal(NativeExecutionKind.AsciiOrderedLiteralWindow, regex.Inspection.ExecutionKind);
        var oracle = new Regex(pattern, options);
        using var source = new CancellationTokenSource();
        var token = liveToken ? source.Token : default;
        foreach (var text in Texts)
        {
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, token));
            Assert.Equal(oracle.Count(text), regex.Count(input, token));
            var expectedMatch = oracle.Match(text);
            var actualMatch = regex.Match(input, token);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (actualMatch.Success)
            {
                Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
                Assert.Equal(expectedMatch.Value, Encoding.UTF8.GetString(input.AsSpan(actualMatch.IndexInBytes, actualMatch.LengthInBytes)));
            }
            var expected = oracle.EnumerateMatches(text);
            var actual = regex.EnumerateMatches(input, token);
            while (expected.MoveNext())
            {
                Assert.True(actual.MoveNext());
                Assert.Equal(expected.Current.Index, actual.Current.IndexInUtf16);
                Assert.Equal(expected.Current.Length, actual.Current.LengthInUtf16);
            }
            Assert.False(actual.MoveNext());
            Assert.False(actual.MoveNext());
        }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false, 17)]
    [InlineData(true, 17)]
    [InlineData(false, 83)]
    [InlineData(true, 83)]
    public void SeededAsciiWindowsAgreeWithIndependentRegexOnEveryAcceptedRange(bool compiled, int seed)
    {
        var random = new Random(seed);
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        foreach (var pattern in Patterns)
        {
            var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
            var oracle = new Regex(pattern, options);
            for (var trial = 0; trial < 40; trial++)
            {
                const string alphabet = "aabbzzfoo bar\t\r\n";
                var text = new string(Enumerable.Range(0, 72).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
                var input = Encoding.UTF8.GetBytes(text);
                var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
                var prepared = Prepared(regex);
                var floor = 0;
                foreach (Match expected in oracle.Matches(text))
                {
                    Assert.Equal(Utf8ExecutionLimitKind.None, AsciiOrderedLiteralWindowExecutor.TryFindNext(input,
                        prepared.StructuralLinearProgram.OrderedLiteralWindowPlan, prepared.SearchPlan, floor,
                        ref invocation, out var actual, out var found));
                    Assert.True(found, $"{pattern}: {text}");
                    Assert.Equal((expected.Index, expected.Length), (actual.Index, actual.Length));
                    floor = actual.Index + actual.Length;
                }
                Assert.Equal(Utf8ExecutionLimitKind.None, AsciiOrderedLiteralWindowExecutor.TryFindNext(input,
                    prepared.StructuralLinearProgram.OrderedLiteralWindowPlan, prepared.SearchPlan, floor,
                    ref invocation, out _, out var terminal));
                Assert.False(terminal, $"{pattern}: {text}");
            }
        }
    }

    [Theory]
    [InlineData(@"a\s+b")]
    [InlineData(@"a.{0,4}b")]
    [InlineData(@"(?:foo|bar).{0,8}z")]
    [InlineData(@"Holmes.{0,25}Watson|Watson.{0,25}Holmes")]
    public void EveryCountCutoffIncludesValidationAndNeverPublishesPartialTotalOrConsumesBorrowedLease(string pattern)
    {
        var prepared = Prepared(new Utf8Regex(pattern, RegexOptions.CultureInvariant));
        var input = Encoding.UTF8.GetBytes(new string('x', 4096) + " a  b a1b2b foo1z2z barz Holmes and Watson Watson and Holmes");
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, AsciiOrderedLiteralWindowExecutor.TryCount(input,
            prepared.StructuralLinearProgram.OrderedLiteralWindowPlan, prepared.SearchPlan, ref baseline, out var total));
        Assert.Equal(new Regex(pattern).Count(Encoding.UTF8.GetString(input)), total);
        for (var cap = 1UL; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var validation = Utf8Validation.TryValidate(input, ref invocation, out _);
            if (validation == Utf8ExecutionLimitKind.None)
            {
                Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, AsciiOrderedLiteralWindowExecutor.TryCount(input,
                    prepared.StructuralLinearProgram.OrderedLiteralWindowPlan, prepared.SearchPlan, ref invocation, out var denied));
                Assert.Equal(0, denied);
            }
            else Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, validation);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeparatorBacktrackingDoesNotReplayOverlappingSearchWindows(bool lazy)
    {
        var pattern = lazy ? @"a\s+?.{0,8192}b" : @"a\s+.{0,8192}b";
        var prepared = Prepared(new Utf8Regex(pattern, RegexOptions.CultureInvariant));
        var work = new List<ulong>();
        foreach (var size in new[] { 4096, 8192 })
        {
            var input = Encoding.ASCII.GetBytes("a" + new string(' ', size));
            var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.Equal(Utf8ExecutionLimitKind.None, AsciiOrderedLiteralWindowExecutor.TryFindNext(input,
                prepared.StructuralLinearProgram.OrderedLiteralWindowPlan, prepared.SearchPlan, 0, ref invocation, out _, out var found));
            Assert.False(found);
            work.Add(invocation.Budget.Steps);
        }
        Assert.True(work[1] <= 3 * work[0], $"Work grew from {work[0]} to {work[1]}.");
    }

    [Theory]
    [InlineData(@"a.{0,4}b")]
    [InlineData(@"(?:foo|bar).{0,8}z")]
    public void CursorCutoffsRetainPreviousResultAndProgressAndBorrowedReservation(string pattern)
    {
        var prepared = Prepared(new Utf8Regex(pattern, RegexOptions.CultureInvariant));
        var input = "a1b2b\na3b\nfoo1z2z\nbarz"u8.ToArray();
        var baselineInvocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        var baseline = new Utf8OperationMatchCursor(input, prepared, baselineInvocation);
        Assert.True(baseline.MoveNext());
        var firstSteps = baseline.DebugInvocationSteps;
        Assert.True(baseline.MoveNext());
        for (var cap = firstSteps; cap < baseline.DebugInvocationSteps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var cursor = new Utf8OperationMatchCursor(input, prepared, invocation);
            Assert.True(cursor.MoveNext());
            var previous = cursor.CurrentValueMatch;
            Exception? observed = null;
            try { _ = cursor.MoveNext(); } catch (Exception exception) { observed = exception; }
            Assert.IsType<InvalidOperationException>(observed);
            Assert.Equal(previous, cursor.CurrentValueMatch);
            Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
        }
    }

    public static IEnumerable<object[]> InterruptedCases()
    {
        foreach (var separator in new[] { false, true })
        foreach (var compiled in new[] { false, true })
        foreach (var operation in new[] { "ismatch", "count", "match", "enum" })
        foreach (var phase in new[] { "validation", "confirmation" })
        foreach (var expire in new[] { false, true }) yield return [separator, compiled, operation, phase, expire];
    }

    [Theory]
    [MemberData(nameof(InterruptedCases))]
    public void PublicWindowPhasesCarryEntryInvocationAndMapCancellationOrExpiry(bool separator, bool compiled, string operation, string phase, bool expire)
    {
        var pattern = separator ? @"a\s+b" : @"a.{0,200000}b";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        Assert.Equal(NativeExecutionKind.AsciiOrderedLiteralWindow, regex.Inspection.ExecutionKind);
        Assert.NotNull(typeof(Utf8Regex).GetField("_unitSliceProgram", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(regex));
        var text = phase == "confirmation" ? "a" + new string(separator ? ' ' : 'x', 160000) + "b" : new string('x', 320000);
        var input = Encoding.UTF8.GetBytes(text);
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var validationSteps = validation.Budget.Steps;
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (entered || phase == "confirmation" && Utf8SearchDiagnosticsSession.Current!.VerifierInvocations == 0) return;
            if (phase == "validation") Assert.True(steps <= validationSteps);
            entered = true;
            if (expire) Thread.Sleep(250); else source.Cancel();
        });
        try
        {
            void Run()
            {
                if (operation == "ismatch") _ = regex.IsMatch(input, source.Token);
                else if (operation == "count") _ = regex.Count(input, source.Token);
                else if (operation == "match") _ = regex.Match(input, source.Token);
                else { var cursor = regex.EnumerateMatches(input, source.Token); _ = cursor.MoveNext(); }
            }
            if (expire)
            {
                var exception = Assert.Throws<RegexMatchTimeoutException>(Run);
                Assert.Equal(pattern, exception.Pattern);
                Assert.Equal(text, exception.Input);
                Assert.Equal(regex.MatchTimeout, exception.MatchTimeout);
            }
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
            Assert.True(entered);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
        finally { session.Complete(); }
        Assert.True(regex.IsMatch("a b"u8));
    }

    public static IEnumerable<object[]> CompleteCases()
    {
        foreach (var pattern in new[] { @"a\s+b", @"a.{0,4}\nb", @"Holmes.{0,25}Watson|Watson.{0,25}Holmes", @"(?<x>a)\s+(?<y>b)" })
        foreach (var compiled in new[] { false, true }) yield return [pattern, compiled];
    }

    [Theory]
    [MemberData(nameof(CompleteCases))]
    public void CompleteOperationsKeepCapturesOffsetsSplitsAndReplacement(string pattern, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, options), Texts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CountCancellationAfterAcceptedWindowsCannotReturnPartialCount(bool compiled)
    {
        var regex = new Utf8Regex(@"a\s+b", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("a b ", 4000)));
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
        }
        finally { session.Complete(); }
        Assert.Equal(4000, regex.Count(input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalWindowCursorKeepsConstantWorkAndChecksTokenOrOriginalDeadline(bool expire)
    {
        var prepared = Prepared(new Utf8Regex(@"a\s+b", RegexOptions.CultureInvariant));
        using var source = new CancellationTokenSource();
        var deadline = expire ? Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(200)) : Utf8ExecutionDeadline.Infinite;
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), deadline, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var cursor = new Utf8OperationMatchCursor(new byte[32000], prepared, invocation);
        Assert.False(cursor.MoveNext());
        var terminalSteps = cursor.DebugInvocationSteps;
        for (var i = 0; i < 3; i++) Assert.False(cursor.MoveNext());
        Assert.InRange(cursor.DebugInvocationSteps - terminalSteps, 3UL, 6UL);
        if (expire) Thread.Sleep(250); else source.Cancel();
        Exception? observed = null;
        try { _ = cursor.MoveNext(); } catch (Exception exception) { observed = exception; }
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(observed);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
    }
}
