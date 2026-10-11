using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CoreWorkPolicyTests
{
    private static readonly string[] Operations = ["is", "count", "match", "detail", "is-offset", "count-offset",
        "match-offset", "detail-offset", "matches", "matches-offset", "split", "split-count", "replace",
        "replace-bytes", "replace-text", "destination", "destination-bytes", "callback-bytes", "callback-text"];

    private static Utf8Regex Create(string pattern, ulong cap, RegexOptions options = RegexOptions.CultureInvariant,
        TimeSpan? timeout = null) => new(pattern, options, timeout ?? Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = cap });

    public static IEnumerable<object[]> OperationModes()
    {
        foreach (var operation in Operations)
        foreach (var compiled in new[] { false, true })
        foreach (var live in new[] { false, true }) yield return [operation, compiled, live];
    }

    [Theory]
    [MemberData(nameof(OperationModes))]
    public void EveryPublicOperationHasFiniteSuccessAndStructuredExhaustion(string operation, bool compiled, bool live)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        using var source = new CancellationTokenSource();
        var token = live ? source.Token : default;
        var input = Encoding.UTF8.GetBytes("é😀 ab😀 ab");
        const string pattern = "😀|(?<x>ab)";
        var baseline = Run(new Utf8Regex(pattern, options), operation, input, token);
        Assert.Equal(baseline, Run(Create(pattern, 100000, options), operation, input, token));
        AssertWorkFailure(() => Run(Create(pattern, 1, options), operation, input, token), 1);
        // The largest finite value remains a real finite policy; arithmetic must not wrap.
        Assert.Equal(baseline, Run(Create(pattern, ulong.MaxValue, options), operation, input, token));
    }

    [Theory]
    [MemberData(nameof(OperationModes))]
    public void ExactAdmissionAndSmallCutoffsNeverPublishTruncatedSuccess(string operation, bool compiled, bool live)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        using var source = new CancellationTokenSource();
        var token = live ? source.Token : default;
        var input = "ab ab"u8.ToArray();
        var expected = Run(Create("ab", 100000, options), operation, input, token);
        ulong first = 1, last = 100000;
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try
            {
                Assert.Equal(expected, Run(Create("ab", cap, options), operation, input, token));
                last = cap;
            }
            catch (Utf8ResourceLimitExceededException error)
            {
                CheckFailure(error, cap);
                first = cap + 1;
            }
        }
        Assert.Equal(expected, Run(Create("ab", first, options), operation, input, token));
        Assert.True(first > 1);
        AssertWorkFailure(() => Run(Create("ab", first - 1, options), operation, input, token), first - 1);
        for (ulong cap = 1; cap <= Math.Min(64UL, first); cap++)
        {
            if (cap == first) Assert.Equal(expected, Run(Create("ab", cap, options), operation, input, token));
            else AssertWorkFailure(() => Run(Create("ab", cap, options), operation, input, token), cap);
        }
    }

    [Theory]
    [InlineData("abc", "xxabcxx")]
    [InlineData("^[-+]?[0-9]+$", "+123")]
    [InlineData("[0-9]{4}-[0-9]{2}-[0-9]{2}", "2026-10-11")]
    [InlineData("^(?:[A-Za-z]+)?$", "field")]
    [InlineData("(?:get|set)[A-Z][A-Za-z0-9_]*", "getAlpha setBeta")]
    [InlineData("ab[0-9]{2}", "ab12 ab34")]
    [InlineData("^abc", "abc")]
    [InlineData("abc$", "xxabc")]
    [InlineData("(?:ab|xy)", "ab xy")]
    public void NativeEligiblePatternsCannotBypassFiniteEntry(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        foreach (var operation in Operations)
        {
            var expected = Run(new Utf8Regex(pattern, options), operation, input, default);
            Assert.Equal(expected, Run(Create(pattern, 100000, options), operation, input, default));
            AssertWorkFailure(() => Run(Create(pattern, 1, options), operation, input, default), 1);
        }
    }

    [Theory]
    [InlineData(".", false)]
    [InlineData(".", true)]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData("(?=(.))", false)]
    [InlineData("(?<x>😀)+\\k<x>", false)]
    [InlineData("(?<x>😀)+\\k<x>", true)]
    [InlineData("(?<=😀)ab", false)]
    public void ScalarAndEmptyProgressionCaptureAndReferenceSemanticsRemainOwned(string pattern, bool rtl)
    {
        var input = Encoding.UTF8.GetBytes("é😀😀 ab😀");
        var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0);
        foreach (var operation in Operations)
            Assert.Equal(Run(new Utf8Regex(pattern, options), operation, input, default),
                Run(Create(pattern, 100000, options), operation, input, default));
        var dot = Create(".", 100000, options);
        Assert.Equal(7, dot.Count(input));
        var cursor = dot.EnumerateMatches("😀"u8);
        Assert.True(cursor.MoveNext());
        Assert.Equal((0, 4), (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
        Assert.False(cursor.MoveNext());
    }

    [Fact]
    public void NonBacktrackingFinitePolicyUsesTheSameCumulativeAuthority()
    {
        var regex = Create("(?<x>.)+", 100000, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        var input = Encoding.UTF8.GetBytes("é😀中");
        Assert.Equal(1, regex.Count(input));
        var group = regex.MatchDetailed(input).GetGroup("x");
        Assert.Equal(1, group.CaptureCount);
        Assert.Equal("中", group.GetValueString());
        foreach (var operation in Operations)
            AssertWorkFailure(() => Run(Create(".+", 1, RegexOptions.NonBacktracking), operation, input, default), 1);
    }

    [Theory]
    [MemberData(nameof(OperationModes))]
    public void FullValidationAndCanceledEntryCannotBeSkippedByEarlyHits(string operation, bool compiled, bool live)
    {
        using var source = new CancellationTokenSource();
        var token = live ? source.Token : default;
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var input = "ab x"u8.ToArray().Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() => Run(Create("ab", 100000, options), operation, input, token));
        AssertWorkFailure(() => Run(Create("ab", 1, options), operation, input, token), 1);
        source.Cancel();
        var canceled = Assert.Throws<OperationCanceledException>(() => Run(Create("ab", 1, options), operation, input, source.Token));
        Assert.Equal(source.Token, canceled.CancellationToken);
    }

    [Fact]
    public void DerivedStartsRejectSurrogateInteriorsAndUseOneValidationBudget()
    {
        var input = Encoding.UTF8.GetBytes("é😀ab");
        foreach (var operation in Operations.Where(x => x.Contains("offset")))
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Run(Create("ab", 100000), operation, input, default, 2));
            Assert.Equal(Run(new Utf8Regex("ab"), operation, input, default, 3),
                Run(Create("ab", 100000), operation, input, default, 3));
        }
        var large = Encoding.UTF8.GetBytes("ab" + new string('é', 10000));
        foreach (var operation in Operations)
            AssertWorkFailure(() => Run(Create("ab", 2), operation, large, default), 2);
    }

    public static IEnumerable<object[]> InterruptedOperations()
    {
        foreach (var operation in Operations)
        foreach (var deadline in new[] { false, true }) yield return [operation, deadline];
    }

    [Theory]
    [MemberData(nameof(InterruptedOperations))]
    public void InterruptionDuringFullValidationWinsBeforeTheNextWorkDenial(string operation, bool deadline)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var triggered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (triggered) return;
            triggered = true;
            if (deadline) clock.Advance(250); else source.Cancel();
        });
        try
        {
            var regex = Create("ab", 64, timeout: deadline ? TimeSpan.FromMilliseconds(200) : null);
            var input = Encoding.UTF8.GetBytes("ab" + new string('é', 200000));
            if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, operation, input, source.Token));
            else
            {
                var error = Assert.Throws<OperationCanceledException>(() => Run(regex, operation, input, source.Token));
                Assert.Equal(source.Token, error.CancellationToken);
            }
            Assert.True(triggered);
        }
        finally { diagnostics.Complete(); }
    }

    [Fact]
    public void EmptySubjectsAndResultDestinationAdmissionKeepTheirDefinedOutcomes()
    {
        foreach (var pattern in new[] { "", "ab", "(?=(.))" })
        foreach (var operation in Operations)
            Assert.Equal(Run(new Utf8Regex(pattern), operation, [], default, 0),
                Run(Create(pattern, 100000), operation, [], default, 0));
        var regex = Create("ab", 100000);
        var destination = new byte[] { 0x7f };
        Assert.Equal(OperationStatus.DestinationTooSmall, regex.TryReplace("ab"u8, "longer", destination, out var written));
        Assert.Equal(0, written);
        Assert.Equal(new byte[] { 0x7f }, destination);
        Assert.ThrowsAny<ArgumentException>(() => Create("[invalid", 100));
    }

    [Fact]
    public void CopiedMatchCursorsShareSpentWorkAndKeepPublishedRangesValid()
    {
        var input = Encoding.UTF8.GetBytes("é😀 ab ab ab ab ab");
        var probe = Create("ab", 100000).EnumerateMatches(input);
        Assert.True(probe.MoveNext());
        var cap = probe.DebugInvocationSteps + 1;
        var cursor = Create("ab", cap).EnumerateMatches(input);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var alias = cursor;
        var before = cursor.DebugInvocationSteps;
        try { alias.MoveNext(); Assert.Fail("A copied cursor must not receive a fresh allowance."); }
        catch (Utf8ResourceLimitExceededException error) { CheckFailure(error, cap); }
        Assert.True(cursor.DebugInvocationSteps > before);
        Assert.Equal(alias.DebugInvocationSteps, cursor.DebugInvocationSteps);
        try { cursor.MoveNext(); Assert.Fail("Denial remains sticky across aliases."); }
        catch (Utf8ResourceLimitExceededException error) { CheckFailure(error, cap); }
        Assert.Equal("ab", Encoding.UTF8.GetString(input.AsSpan(retained.IndexInBytes, retained.LengthInBytes)));
    }

    [Fact]
    public void CopiedSplitCursorsShareWorkWithoutInvalidatingRetainedViews()
    {
        var input = Encoding.UTF8.GetBytes("é😀 ab ab ab ab");
        var probe = Create("ab", 100000).EnumerateSplits(input);
        Assert.True(probe.MoveNext());
        var cap = probe.DebugInvocationSteps + 1;
        var cursor = Create("ab", cap).EnumerateSplits(input);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var alias = cursor;
        try { alias.MoveNext(); Assert.Fail("A split copy must share cumulative work."); }
        catch (Utf8ResourceLimitExceededException error) { CheckFailure(error, cap); }
        Assert.Equal(alias.DebugInvocationSteps, cursor.DebugInvocationSteps);
        Assert.Equal("é😀 ", retained.GetValueString());
        Assert.Equal("é😀 "u8.ToArray(), retained.GetValueBytes().ToArray());
    }

    [Fact]
    public void LazyAdvancesKeepTheOriginalDeadlineAndCancellationToken()
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var regex = Create("ab", 100000, timeout: TimeSpan.FromMilliseconds(200));
        var cursor = regex.EnumerateMatches("ab ab"u8);
        Assert.True(cursor.MoveNext());
        clock.Advance(250);
        try { cursor.MoveNext(); Assert.Fail("Waiting must not renew the invocation deadline."); }
        catch (RegexMatchTimeoutException) { }
        using var source = new CancellationTokenSource();
        var canceled = Create("ab", 100000).EnumerateMatches("ab ab"u8, source.Token);
        Assert.True(canceled.MoveNext());
        source.Cancel();
        try { canceled.MoveNext(); Assert.Fail("The original token remains authoritative."); }
        catch (OperationCanceledException error) { Assert.Equal(source.Token, error.CancellationToken); }
    }

    [Fact]
    public void BacktrackingMissIsBoundedAndIndependentCallsGetFreshBudgets()
    {
        var regex = Create("(a+)+b", 1000);
        var input = Encoding.UTF8.GetBytes(new string('a', 200));
        AssertWorkFailure(() => regex.IsMatch(input), 1000);
        AssertWorkFailure(() => regex.IsMatch(input), 1000);
        var ordinary = Create("ab", 100000);
        Assert.True(ordinary.IsMatch("ab"u8));
        Assert.True(ordinary.IsMatch("ab"u8));
    }

    [Fact]
    public void CallbackFailuresAreNotRelabeledAndNestedScopesRestoreTheOuterMeter()
    {
        var regex = Create("ab", 100000);
        var sentinel = new InvalidOperationException("caller failure");
        Assert.Same(sentinel, Assert.Throws<InvalidOperationException>(() => regex.ReplaceToString("ab"u8, 0,
            (in Utf8MatchContext match, ref int state) => throw sentinel)));
        Assert.Equal("ZZ", regex.ReplaceToString("abab"u8, regex,
            static (in Utf8MatchContext match, ref Utf8Regex nested) =>
            {
                Assert.True(nested.IsMatch("ab"u8));
                return "Z";
            }));
        Assert.Equal("<ab>", regex.ReplaceToString("ab"u8, "<$&>"));
        var expanded = Create("ab", 200);
        AssertWorkFailure(() => expanded.ReplaceToString("ab"u8, 0,
            static (in Utf8MatchContext match, ref int state) => new string('é', 1000000)), 200);
        Assert.Equal("ab", regex.MatchDetailed("ab"u8).GetValueString());
    }

    private static void AssertWorkFailure(Action action, ulong cap) => CheckFailure(Assert.Throws<Utf8ResourceLimitExceededException>(action), cap);
    private static void CheckFailure(Utf8ResourceLimitExceededException error, ulong cap)
    {
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(cap, error.Limit);
        Assert.True(error.Requested > cap || cap == ulong.MaxValue && error.Requested == cap);
    }

    private static string Range(Utf8ValueMatch value) => value.Success
        ? $"True:{value.IndexInBytes}:{value.LengthInBytes}:{value.IndexInUtf16}:{value.LengthInUtf16}"
        : "False";
    private static string Detail(Utf8MatchContext value)
    {
        var result = new StringBuilder($"{value.Success}:{value.IndexInBytes}:{value.LengthInBytes}:{value.GroupCount}");
        for (var n = 0; n < value.GroupCount; n++)
        {
            var group = value.GetGroup(n);
            result.Append($"|{group.Success}:{group.GetValueString()}:{group.CaptureCount}");
            for (var c = 0; c < group.CaptureCount; c++) result.Append($":{group.GetCapture(c).GetValueString()}");
        }
        return result.ToString();
    }

    private static string Run(Utf8Regex regex, string operation, byte[] input, CancellationToken token, int offset = 1)
    {
        var live = token.CanBeCanceled;
        switch (operation)
        {
            case "is": return (live ? regex.IsMatch(input, token) : regex.IsMatch(input)).ToString();
            case "count": return (live ? regex.Count(input, token) : regex.Count(input)).ToString();
            case "match": return Range(live ? regex.Match(input, token) : regex.Match(input));
            case "detail": return Detail(live ? regex.MatchDetailed(input, token) : regex.MatchDetailed(input));
            case "is-offset": return (live ? regex.IsMatchFromUtf16Offset(input, offset, token) : regex.IsMatchFromUtf16Offset(input, offset)).ToString();
            case "count-offset": return (live ? regex.CountFromUtf16Offset(input, offset, token) : regex.CountFromUtf16Offset(input, offset)).ToString();
            case "match-offset": return Range(live ? regex.MatchFromUtf16Offset(input, offset, token) : regex.MatchFromUtf16Offset(input, offset));
            case "detail-offset": return Detail(live ? regex.MatchDetailedFromUtf16Offset(input, offset, token) : regex.MatchDetailedFromUtf16Offset(input, offset));
            case "matches":
            case "matches-offset":
                var matches = operation == "matches-offset"
                    ? (live ? regex.EnumerateMatchesFromUtf16Offset(input, offset, token) : regex.EnumerateMatchesFromUtf16Offset(input, offset))
                    : (live ? regex.EnumerateMatches(input, token) : regex.EnumerateMatches(input));
                var ranges = new List<string>();
                while (matches.MoveNext()) ranges.Add(Range(matches.Current));
                return string.Join("|", ranges);
            case "split":
            case "split-count":
                var split = operation == "split-count"
                    ? (live ? regex.EnumerateSplits(input, 2, token) : regex.EnumerateSplits(input, 2))
                    : (live ? regex.EnumerateSplits(input, token) : regex.EnumerateSplits(input));
                var pieces = new List<string>();
                while (split.MoveNext()) pieces.Add(Convert.ToHexString(split.Current.GetValueBytes()));
                return string.Join("|", pieces);
            case "replace": return Encoding.UTF8.GetString(live ? regex.Replace(input, "<$&>", token) : regex.Replace(input, "<$&>"));
            case "replace-bytes": return Encoding.UTF8.GetString(live ? regex.Replace(input, "<$&>"u8, token) : regex.Replace(input, "<$&>"u8));
            case "replace-text": return live ? regex.ReplaceToString(input, "<$&>", token) : regex.ReplaceToString(input, "<$&>");
            case "destination":
            case "destination-bytes":
                var destination = new byte[4096];
                OperationStatus status;
                int written;
                if (operation == "destination") status = live ? regex.TryReplace(input, "<$&>", destination, out written, token) : regex.TryReplace(input, "<$&>", destination, out written);
                else status = live ? regex.TryReplace(input, "<$&>"u8, destination, out written, token) : regex.TryReplace(input, "<$&>"u8, destination, out written);
                Assert.Equal(OperationStatus.Done, status);
                return Encoding.UTF8.GetString(destination.AsSpan(0, written));
            case "callback-bytes":
                return Encoding.UTF8.GetString(live ? regex.Replace(input, 0, AppendMatch, token) : regex.Replace(input, 0, AppendMatch));
            case "callback-text": return live ? regex.ReplaceToString(input, 0, ExpandMatch, token) : regex.ReplaceToString(input, 0, ExpandMatch);
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
    private static void AppendMatch(in Utf8MatchContext match, ref Utf8ReplacementWriter writer, ref int state)
    {
        writer.Append("<"u8);
        Assert.True(match.TryGetValueBytes(out var bytes));
        writer.Append(bytes);
        writer.Append(">"u8);
    }
    private static string ExpandMatch(in Utf8MatchContext match, ref int state) => "<" + match.GetValueString() + ">";
}
