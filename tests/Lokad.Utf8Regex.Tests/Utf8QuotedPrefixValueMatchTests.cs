using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8QuotedPrefixValueMatchTests
{
    private const string Pattern = "\\G\"([^\"\\\\]|\\\\.)*\"";
    private static readonly Utf8ResourcePolicy Policy = new()
    { MaxWorkSteps = 1_000_000, MaxDepth = 256, MaxWorkingBytes = 1_000_000, MaxResultBytes = 1_000_000 };

    [Fact]
    public void AsciiValueCallsUseTheCompleteQuotedGrammar()
    {
        using var live = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var options = mode | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex(Pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy with
                {
                    MaxDepth = 8192,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
            var oracle = new Regex(Pattern, options);
            foreach (var text in new[] { "", "no quote", "\"", "\"\"tail", "\"abc\"tail", "\"a\\\"b\"tail",
                "\"a\\\\b\"tail", "\"a\nb\"tail", "\"a\\\nb\"tail", "\"a\0b\"tail", "\"abc\\", "x\"a\"", "\n\"a\"" })
            {
                var input = Encoding.ASCII.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var session = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    var actual = regex.Match(storage.AsSpan(1, input.Length), cancellationToken: live.Token);
                    var expected = oracle.Match(text);
                    Assert.Equal(expected.Success, actual.Success);
                    if (actual.Success) Assert.Equal((0, System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length))), (actual.StartOffsetInBytes, (actual.EndOffsetInBytes - actual.StartOffsetInBytes)));
                    Assert.Equal(actual, regex.Match(input));
                }
                finally { session.Complete(); }
            }
        }
    }

    [Fact]
    public void UnicodeCapturesAliasesAndDeclinedShapesKeepTheirResults()
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase,
            RegexOptions.Singleline, RegexOptions.RightToLeft })
            Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(Pattern, options: options), new Regex(Pattern, options),
                ["\"é😀\\😀x\"tail", "\"abc\"😀", "😀\"abc\"", "\"a\\\nb\"", "\"a\nb\""]);
        var regex = new Utf8Regex(Pattern); var bytes = "\"abc\"\"def\""u8.ToArray();
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(bytes));
        var cursor = regex.EnumerateMatches(bytes); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(cursor.Current, alias.Current); var first = alias.Current;
        Assert.True(cursor.MoveNext()); Assert.Equal(first, alias.Current);
        _ = regex.Match("\"other\""u8);
        Assert.Equal("c", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        var offset = regex.Match("😀x\"abc\"tail"u8, 5, []);
        Assert.Equal((5, 5), (offset.StartOffsetInBytes, offset.EndOffsetInBytes - offset.StartOffsetInBytes));
        var nbPattern = Pattern.Replace(@"\G", @"\A");
        Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(nbPattern, options: RegexOptions.NonBacktracking),
            new Regex(nbPattern, RegexOptions.NonBacktracking), ["\"abc\"tail", "\"a\\\nb\"", "\"é😀\""]);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(Pattern + "x"), new Regex(Pattern + "x"), ["\"abc\"x", "\"abc\"tail"]);
    }

    [Theory]
    [InlineData(@"\G(?:[^X](?<run>a)|X(?<escaped>b))", RegexOptions.None)]
    [InlineData(@"\G(?:[^Y](?<run>a)|X(?<escaped>b))", RegexOptions.None)]
    [InlineData(@"\G(?:[^X](?<run>a)|X(?<escaped>b))", RegexOptions.IgnoreCase)]
    public void AsciiForkKeepsCapturePriorityAndOverlappingRetries(string pattern, RegexOptions options)
    {
        using var regex = new Utf8Regex(pattern, options);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, options),
            ["Xa", "Xb", "xb", "éa", "😀a", "Ya", "XaXb", "X", ""]);
    }

    [Fact]
    public void PlainRunBlocksKeepEscapesWiderScalarsAndWorkLimits()
    {
        using var regex = new Utf8Regex(Pattern, RegexOptions.Compiled);
        foreach (var length in new[] { 1, 63, 64, 65, 66, 127, 128, 129, 130 })
        {
            var input = Encoding.UTF8.GetBytes("😀\"" + new string('a', length) + "\\\"😀\"tail");
            var result = regex.Match(input, 4, []);
            Assert.Equal(new Utf8CaptureRange(4, length + 12), result);
            var captures = new Utf8CaptureRange[2];
            Assert.Equal(result, regex.Match(input, 4, captures));
            Assert.Equal(result, captures[0]);
            Assert.Equal(new Utf8CaptureRange(length + 7, length + 11), captures[1]);
        }

        var longInput = Encoding.UTF8.GetBytes("\"" + new string('a', 8192) + "\"");
        using var admitted = new Utf8Regex(Pattern, policy: new() { ResourcePolicy = Policy with { MaxWorkSteps = 512 } });
        using var refused = new Utf8Regex(Pattern, policy: new() { ResourcePolicy = Policy with { MaxWorkSteps = 64 } });
        Assert.Equal(new Utf8CaptureRange(0, longInput.Length), admitted.Match(longInput));
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => refused.Match(longInput));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
    }

    [Fact]
    public void MatchObservesAlreadyCanceledTokenOnHitsAndMisses()
    {
        using var regex = new Utf8Regex(Pattern);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        foreach (var text in new[] { "\"abc\"", "miss" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var error = Assert.Throws<OperationCanceledException>(() => regex.Match(input, cancellationToken: canceled.Token));
            Assert.Equal(canceled.Token, error.CancellationToken);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlainRunBlocksCheckInterruptionBeforeContinuing(bool expire)
    {
        var input = Encoding.UTF8.GetBytes("\"" + new string('a', 8192) + "\"");
        using var regex = new Utf8Regex(Pattern, policy: new()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(50),
            ResourcePolicy = Policy
        });
        using var source = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        ulong checkpoint = 0;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            checkpoint = steps;
            if (expire) ticks = 100; else source.Cancel();
        });
        try
        {
            if (expire)
                Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, cancellationToken: source.Token)).MatchTimeout);
            else
                Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(input, cancellationToken: source.Token)).CancellationToken);
            Assert.Equal(64UL, checkpoint);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData("\"abc\"tail", true)]
    [InlineData("\"unterminated", false)]
    public void EveryWorkCutoffRefusesPartialPublication(string text, bool success)
    {
        var input = Encoding.ASCII.GetBytes(text); var denied = false; var admitted = false;
        for (ulong work = 1; work < 512; work++)
        {
            var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy with
                {
                    MaxWorkSteps = work,
                    MaxDepth = 8192,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
            try { Assert.Equal(success, regex.Match(input).Success); admitted = true; break; }
            catch (Utf8ResourceLimitExceededException failure)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind); denied = true; }
        }
        Assert.True(denied); Assert.True(admitted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicationChecksTheOriginalTokenAndDeadline(bool expire)
    {
        var reads = 0; var trigger = int.MaxValue; long ticks = 0;
        using var live = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == trigger) { if (expire) ticks = 100; else live.Cancel(); } return ticks; });
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(50),
            ResourcePolicy = Policy
        });
        Assert.True(regex.Match("\"abc\"tail"u8, cancellationToken: live.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("\"abc\"tail"u8, cancellationToken: live.Token)).MatchTimeout);
        else Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("\"abc\"tail"u8, cancellationToken: live.Token)).CancellationToken);
    }
}
