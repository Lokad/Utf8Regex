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
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 20000000, MaxDepth = 8192, MaxWorkingBytes = 8 << 20, MaxResultBytes = 8 << 20 };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WholeRangesAndFinalLfPreserveFundedResults(bool hex)
    {
        var pattern = hex ? Hex : Digits;
        var tail = hex ? Route + "ABCD&path=a?name=x&name=y" : "/d/12?t=x";
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: Fold | mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded with
                {
                    MaxDepth = 8192,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
            var control = new Utf8Regex(pattern, options: Fold | mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            var oracle = new Regex(pattern, Fold | mode);
            foreach (var text in new[] { "", "wrong", "~", "~" + tail, "~" + tail + "\n", "~" + tail + " \n",
                "~" + tail + "\nz", "~" + tail + "?tail\n", "https://go.lokad.com/ABC" + tail,
                "HTTPS://GO.TESTING.LOKAD.COM/ABC" + tail, "~bad ~" + tail, "~" + tail + " ~/bad",
                "~" + tail + " ~" + tail + "\n", "~" + tail + "\n~" + tail })
            {
                var input = Encoding.ASCII.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var expected = Utf8MatchSequence.First(control.CreateMatchCursor(input)); var bcl = oracle.Match(text);
                Utf8MatchSequence.AssertCaptures(bcl, expected, input, text);
                using var token = new CancellationTokenSource();
                var actual = regex.Match(storage.AsSpan(1, input.Length), cancellationToken: token.Token);
                Assert.Equal(expected.Success, actual.Success);
                if (actual.Success) Assert.Equal((expected.Value.StartOffsetInBytes, (expected.Value.EndOffsetInBytes - expected.Value.StartOffsetInBytes)),
                    (actual.StartOffsetInBytes, (actual.EndOffsetInBytes - actual.StartOffsetInBytes)));
                Assert.Equal(actual, regex.Match(input));
            }
        }
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
                var regex = new Utf8Regex(Digits, options: options, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan,
                    ResourcePolicy = Funded
                });
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                foreach (var text in new[] { "HTTPS://GO.TESTING.LOKAD.COM/d/12?t=x", "😀~/d/١?t=é\n",
                    "~/d/1?t=😀", "~/d/1?t=x\n", new string('x', 2049) + "~/d/1?t=x" })
                {
                    var input = Encoding.UTF8.GetBytes(text); var expected = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                    var actual = regex.Match(input);
                    Assert.Equal(expected.Success, actual.Success);
                        if (actual.Success) Assert.Equal((expected.Value.StartOffsetInBytes, (expected.Value.EndOffsetInBytes - expected.Value.StartOffsetInBytes)),
                            (actual.StartOffsetInBytes, (actual.EndOffsetInBytes - actual.StartOffsetInBytes)));
                    }
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
        var offset = new Utf8Regex(Digits, options: Fold); var bytes = "x~/d/1?t=x"u8.ToArray();
        Assert.True(offset.Match(bytes, 0, []).Success);
        Assert.False(offset.Match(bytes, 1, []).Success);
        var scoped = new Utf8Regex(Digits.Replace("https", "(?-i:https)"), options: Fold);
        Assert.False(scoped.Match("HTTPS://go.lokad.com/d/1?t=x"u8).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionAppliesDuringRetries(bool expire)
    {
        // Rewind must visit these false starters before it can reach the match.
        var input = Encoding.ASCII.GetBytes("~/d/1?t=x " + string.Concat(Enumerable.Repeat("~bad ", 100)));
        long ticks = 0; using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(Digits, options: Fold, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Funded
        });
        Assert.True(regex.Match(input).Success);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (entered) return;
            entered = true; if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, cancellationToken: token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(input, cancellationToken: token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void DetailedCapturesAliasesAndStrictTailsRemainOwned()
    {
        var regex = new Utf8Regex(Digits, options: Fold); var input = "~/ABC/d/12?t=tab"u8.ToArray();
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input)); Assert.True(retained.TryGetGroup(regex.GroupNumberFromUtf8Name("tab"u8), out var tab));
        var value = regex.Match(input); var cursor = regex.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(value, cursor.Current); Assert.Equal(value, alias.Current);
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(regex.GroupNumberFromUtf8Name("topicId"u8)).Span)); Assert.Equal("tab", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(regex.GroupNumberFromUtf8Name("tab"u8)).Span));
        byte[] malformed = [.. input, 0xff];
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(malformed, cancellationToken: token.Token)).CancellationToken);
    }
}
