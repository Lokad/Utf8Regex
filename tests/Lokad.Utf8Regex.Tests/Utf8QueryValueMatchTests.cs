using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8QueryValueMatchTests
{
    private const string Digits = @"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\d+)/?\?t=(?<tab>[^ ?]+)(?<rest>.*)$";
    private const string Hex = @"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \n]+)?(?<rest>.*)$";
    private const string Route = "/gateway/BigFiles/Browse/Download?hash=";
    private const RegexOptions Fold = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 20000000,
        MaxDepth = 8192, MaxWorkingBytes = 8 << 20, MaxResultBytes = 8 << 20 };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WholeRangesAndFinalLfNeedNoCaptureWorkspace(bool hex)
    {
        var pattern = hex ? Hex : Digits;
        var tail = hex ? Route + "ABCD&path=a?name=x&name=y" : "/d/12?t=x";
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, Fold | mode, Timeout.InfiniteTimeSpan,
                Funded with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            var control = new Utf8Regex(pattern, Fold | mode, Timeout.InfiniteTimeSpan, Funded);
            var oracle = new Regex(pattern, Fold | mode);
            foreach (var text in new[] { "", "wrong", "~", "~" + tail, "~" + tail + "\n", "~" + tail + " \n",
                "~" + tail + "\nz", "~" + tail + "?tail\n", "https://go.lokad.com/ABC" + tail,
                "HTTPS://GO.TESTING.LOKAD.COM/ABC" + tail, "~bad ~" + tail, "~" + tail + " ~/bad",
                "~" + tail + " ~" + tail + "\n", "~" + tail + "\n~" + tail })
            {
                var input = Encoding.ASCII.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var expected = control.MatchDetailed(input); var bcl = oracle.Match(text);
                Assert.Equal((bcl.Success, bcl.Length), (expected.Success, expected.LengthInUtf16));
                using var token = new CancellationTokenSource();
                var session = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    var actual = regex.Match(storage.AsSpan(1, input.Length), token.Token);
                    Assert.Equal(expected.Success, actual.Success);
                    if (actual.Success) Assert.Equal((expected.IndexInBytes, expected.LengthInBytes),
                        (actual.IndexInBytes, actual.LengthInBytes));
                    Assert.Equal(Utf8ExecutionRoute.NativeAsciiAnchoredValidator, session.ExecutionRoute);
                    Assert.Equal(actual, regex.Match(input));
                }
                finally { session.Complete(); }
            }
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void WholeSubjectBoundDefinesTheWorkingAdmissionEnvelope(int length)
    {
        const string tail = "~/d/1?t=x";
        var input = Encoding.ASCII.GetBytes(new string('x', length - tail.Length) + tail);
        var tight = new Utf8Regex(Digits, Fold, Timeout.InfiniteTimeSpan,
            Funded with { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        if (length <= 2048) Assert.Equal(length, tight.Match(input).LengthInBytes);
        else Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tight.Match(input)).ResourceKind);
    }

    [Fact]
    public void UnicodeOffsetsCulturesAndExcludedModesKeepScalarResults()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "tr-TR" })
            foreach (var options in new[] { RegexOptions.IgnoreCase, Fold, Fold | RegexOptions.RightToLeft, Fold | RegexOptions.NonBacktracking })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var regex = new Utf8Regex(Digits, options, Timeout.InfiniteTimeSpan, Funded);
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                foreach (var text in new[] { "HTTPS://GO.TESTING.LOKAD.COM/d/12?t=x", "😀~/d/١?t=é\n",
                    "~/d/1?t=😀", "~/d/1?t=x\n", new string('x', 2049) + "~/d/1?t=x" })
                {
                    var input = Encoding.UTF8.GetBytes(text); var expected = regex.MatchDetailed(input);
                    var actual = regex.Match(input);
                    Assert.Equal(expected.Success, actual.Success);
                    if (actual.Success) Assert.Equal((expected.IndexInBytes, expected.LengthInBytes),
                        (actual.IndexInBytes, actual.LengthInBytes));
                }
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
        var offset = new Utf8Regex(Digits, Fold); var bytes = "x~/d/1?t=x"u8.ToArray();
        Assert.True(offset.MatchFromUtf16Offset(bytes, 0).Success);
        Assert.False(offset.MatchFromUtf16Offset(bytes, 1).Success);
        var scoped = new Utf8Regex(Digits.Replace("https", "(?-i:https)"), Fold);
        Assert.False(scoped.Match("HTTPS://go.lokad.com/d/1?t=x"u8).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryRetryWorkCutoffKeepsItsLoanAndCannotPublishAHit(bool hex)
    {
        var regex = new Utf8Regex(hex ? Hex : Digits, Fold);
        var plan = regex.Inspection.PreparedRegex.FallbackDirectFamily;
        var tail = hex ? Route + "ABCD?path=a?name=x&name=y" : "/d/1?t=x";
        var input = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("~bad ", 40)) + "~" + tail);
        var baseline = new Utf8SliceInvocation(new(100000, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8AsciiAnchoredUrlExecutor.TryMatchWhole(input, plan,
            ref baseline, out var found, out var length));
        Assert.True(found); Assert.Equal(input.Length, length); Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work <= baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var breach = Utf8AsciiAnchoredUrlExecutor.TryMatchWhole(input, plan, ref invocation, out var hit, out var selectedLength);
            if (work == baseline.Budget.Steps) { Assert.Equal(Utf8ExecutionLimitKind.None, breach); Assert.True(hit); }
            else { Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, breach); Assert.False(hit); Assert.Equal(0, selectedLength); }
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionAppliesDuringRetries(bool expire)
    {
        var input = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("~bad ", 100)) + "~/d/1?t=x");
        var validation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.Validate(input, ref validation);
        long ticks = 0; using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(Digits, Fold, TimeSpan.FromMilliseconds(20), Funded);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || steps <= validation.Budget.Steps) return;
            entered = true; if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(input, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void DetailedCapturesAliasesAndStrictTailsRemainOwned()
    {
        var regex = new Utf8Regex(Digits, Fold); var input = "~/ABC/d/12?t=tab"u8.ToArray();
        var retained = regex.MatchDetailed(input); var tab = retained.GetGroup("tab");
        var value = regex.Match(input); var cursor = regex.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(value, cursor.Current); Assert.Equal(value, alias.Current);
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        Assert.Equal("12", retained.GetGroup("topicId").GetValueString()); Assert.Equal("tab", tab.GetValueString());
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(malformed, token.Token)).CancellationToken);
    }
}
