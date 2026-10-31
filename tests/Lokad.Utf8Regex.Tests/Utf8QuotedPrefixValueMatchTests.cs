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
    public void BoundedAsciiValueCallsUseTheExistingCompleteQuotedGrammar()
    {
        using var live = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var options = mode | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex(Pattern, options, Timeout.InfiniteTimeSpan,
                Policy with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            Assert.Equal("AnchoredQuotedStringPrefix", regex.Inspection.DebugFallbackDirectFamilyKind);
            var oracle = new Regex(Pattern, options);
            foreach (var text in new[] { "", "no quote", "\"", "\"\"tail", "\"abc\"tail", "\"a\\\"b\"tail",
                "\"a\\\\b\"tail", "\"a\nb\"tail", "\"a\\\nb\"tail", "\"a\0b\"tail", "\"abc\\", "x\"a\"", "\n\"a\"" })
            {
                var input = Encoding.ASCII.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var session = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    var actual = regex.Match(storage.AsSpan(1, input.Length), live.Token);
                    var expected = oracle.Match(text);
                    Assert.Equal(expected.Success, actual.Success);
                    if (actual.Success) Assert.Equal((0, expected.Length, 0, expected.Length),
                        (actual.IndexInBytes, actual.LengthInBytes, actual.IndexInUtf16, actual.LengthInUtf16));
                    Assert.Equal(Utf8ExecutionRoute.NativeAsciiAnchoredValidator, session.ExecutionRoute);
                    Assert.Equal(actual, regex.Match(input));
                }
                finally { session.Complete(); }
            }
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void WholeSubjectBoundsKeepTheirWorkingAdmission(int length)
    {
        var text = "\"" + new string('a', length - 2) + "\"";
        var input = Encoding.ASCII.GetBytes(text);
        Assert.Equal(length, new Utf8Regex(Pattern).Match(input).LengthInBytes);
        var tight = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            Policy with { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        if (length <= 2048) Assert.Equal(length, tight.Match(input).LengthInBytes);
        else Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tight.Match(input)).ResourceKind);
    }

    [Fact]
    public void UnicodeCapturesAliasesAndDeclinedShapesKeepTheirResults()
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase,
            RegexOptions.Singleline, RegexOptions.RightToLeft })
            Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(Pattern, options), new Regex(Pattern, options),
                ["\"é😀\\😀x\"tail", "\"abc\"😀", "😀\"abc\"", "\"a\\\nb\"", "\"a\nb\""]);
        var regex = new Utf8Regex(Pattern); var bytes = "\"abc\"\"def\""u8.ToArray();
        var retained = regex.MatchDetailed(bytes);
        var cursor = regex.EnumerateMatches(bytes); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(cursor.Current, alias.Current); var first = alias.Current;
        Assert.True(cursor.MoveNext()); Assert.Equal(first, alias.Current);
        _ = regex.Match("\"other\""u8);
        Assert.Equal("c", retained.GetGroup(1).GetValueString());
        var offset = regex.MatchFromUtf16Offset("😀x\"abc\"tail"u8, 3);
        Assert.Equal((5, 5, 3, 5), (offset.IndexInBytes, offset.LengthInBytes, offset.IndexInUtf16, offset.LengthInUtf16));
        var nbPattern = Pattern.Replace(@"\G", @"\A");
        Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(nbPattern, RegexOptions.NonBacktracking),
            new Regex(nbPattern, RegexOptions.NonBacktracking), ["\"abc\"tail", "\"a\\\nb\"", "\"é😀\""]);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(Pattern + "x"), new Regex(Pattern + "x"), ["\"abc\"x", "\"abc\"tail"]);
    }

    [Fact]
    public void WholeValidationAndOriginalEntryCancellationRemainStrict()
    {
        var regex = new Utf8Regex(Pattern);
        foreach (var text in new[] { "\"abc\"", "miss" })
        {
            byte[] invalid = [.. Encoding.UTF8.GetBytes(text), 0xff];
            Assert.Throws<ArgumentException>(() => regex.Match(invalid));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(invalid, canceled.Token)).CancellationToken);
        }
    }

    [Theory]
    [InlineData("\"abc\"tail", true)] [InlineData("\"unterminated", false)]
    public void EveryWorkCutoffRefusesPartialPublication(string text, bool success)
    {
        var input = Encoding.ASCII.GetBytes(text); var denied = false; var admitted = false;
        for (ulong work = 1; work < 100; work++)
        {
            var regex = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
                Policy with { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
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
        var regex = new Utf8Regex(Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50), Policy);
        Assert.True(regex.Match("\"abc\"tail"u8, live.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("\"abc\"tail"u8, live.Token)).MatchTimeout);
        else Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("\"abc\"tail"u8, live.Token)).CancellationToken);
    }
}
