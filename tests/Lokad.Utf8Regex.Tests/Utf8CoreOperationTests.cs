using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CoreOperationTests
{
    internal static readonly IReadOnlyList<string> Operations = ["is", "count", "match", "capture", "slot-match",
        "capture-offset", "matches", "matches-offset", "split", "split-count", "replace", "destination"];

    public static IEnumerable<object[]> OperationModes()
    {
        foreach (var operation in Operations)
        foreach (var compiled in new[] { false, true })
        foreach (var live in new[] { false, true }) yield return [operation, compiled, live];
    }

    public static IEnumerable<object[]> InterruptedOperations()
    {
        foreach (var operation in Operations)
        foreach (var deadline in new[] { false, true }) yield return [operation, deadline];
    }

    [Theory]
    [MemberData(nameof(InterruptedOperations))]
    public void SearchChecksOriginalCancellationAndDeadline(string operation, bool deadline)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var triggered = false;
        using var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (triggered) return;
            triggered = true;
            if (deadline) clock.Advance(250); else source.Cancel();
        });
        var regex = new Utf8Regex("ab", policy: new Utf8RegexPolicy
        {
            MatchTimeout = deadline ? TimeSpan.FromMilliseconds(200) : Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes(new string('x', 1 << 20) + "ab");
        if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, operation, input, source.Token));
        else
        {
            var error = Assert.Throws<OperationCanceledException>(() => Run(regex, operation, input, source.Token));
            Assert.Equal(source.Token, error.CancellationToken);
        }
        Assert.True(triggered);
    }

    [Theory]
    [MemberData(nameof(InterruptedOperations))]
    public void PreCancellationPrecedesAnEarlyMatch(string operation, bool compiled)
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var regex = new Utf8Regex("ab", compiled ? RegexOptions.Compiled : RegexOptions.None);
        var error = Assert.Throws<OperationCanceledException>(() => Run(regex, operation, "ab é"u8.ToArray(), source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
    }

    [Fact]
    public void BacktrackingSearchChecksOriginalCancellation()
    {
        // The subject contains the required literal outside every possible match.
        var input = Encoding.UTF8.GetBytes("b" + new string('a', 200));
        using var source = new CancellationTokenSource();
        using var diagnostics = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        var regex = new Utf8Regex("(a+)+b", policy: new Utf8RegexPolicy { MatchTimeout = TimeSpan.FromSeconds(2) });
        var error = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.True(regex.IsMatch("aab"u8));
    }

    [Fact]
    public void LazyAdvancesKeepTheOriginalDeadlineAndCancellationToken()
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var regex = new Utf8Regex("ab", policy: new Utf8RegexPolicy { MatchTimeout = TimeSpan.FromMilliseconds(200) });
        var cursor = regex.EnumerateMatches("ab ab"u8);
        Assert.True(cursor.MoveNext());
        clock.Advance(250);
        try { cursor.MoveNext(); Assert.Fail("Waiting must not renew the invocation deadline."); }
        catch (RegexMatchTimeoutException) { }

        using var source = new CancellationTokenSource();
        var canceled = new Utf8Regex("ab").EnumerateMatches("ab ab"u8, cancellationToken: source.Token);
        Assert.True(canceled.MoveNext());
        source.Cancel();
        try { canceled.MoveNext(); Assert.Fail("The original token remains authoritative."); }
        catch (OperationCanceledException error) { Assert.Equal(source.Token, error.CancellationToken); }
    }

    private static string Range(Utf8CaptureRange value) => value.Success
        ? $"True:{value.StartOffsetInBytes}:{value.EndOffsetInBytes - value.StartOffsetInBytes}"
        : "False";

    private static string Detail(Utf8RangeMatch value)
    {
        var result = new StringBuilder($"{value.Success}:{value.Value.StartOffsetInBytes}:{value.Value.EndOffsetInBytes}");
        foreach (var number in value.GroupNumbers)
        {
            Assert.True(value.TryGetGroup(number, out var group));
            result.Append($"|{number}:{group.Success}:{Convert.ToHexString(value.GetGroupMemory(number).Span)}");
            foreach (var capture in value.GetCaptures(number))
                result.Append($":{capture.StartOffsetInBytes}:{capture.EndOffsetInBytes}");
        }
        return result.ToString();
    }

    internal static string Run(Utf8Regex regex, string operation, byte[] input, CancellationToken token,
        int? startOffsetInBytes = null)
    {
        int Start() => startOffsetInBytes ??
            (Rune.DecodeFromUtf8(input, out _, out var width) == OperationStatus.Done ? width : 0);

        switch (operation)
        {
            case "is": return regex.IsMatch(input, token).ToString();
            case "count": return regex.Count(input, token).ToString();
            case "match": return Range(regex.Match(input, cancellationToken: token));
            case "capture": return Detail(Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token)));
            case "slot-match":
                var slots = new Utf8CaptureRange[regex.CaptureGroupCount + 1];
                var match = regex.Match(input, Start(), slots, token);
                return Range(match) + "|" + string.Join("|", slots.Select(Range));
            case "capture-offset": return Detail(Utf8MatchSequence.First(regex.CreateMatchCursor(input, Start(), token)));
            case "matches":
            case "matches-offset":
                var matches = regex.EnumerateMatches(input, operation == "matches-offset" ? Start() : null, token);
                var ranges = new List<string>();
                while (matches.MoveNext()) ranges.Add(Range(matches.Current));
                return string.Join("|", ranges);
            case "split":
            case "split-count":
                var split = regex.EnumerateSplits(input, operation == "split-count" ? 2 : int.MaxValue, token);
                var pieces = new List<string>();
                while (split.MoveNext()) pieces.Add(Convert.ToHexString(split.Current.GetValueBytes()));
                return string.Join("|", pieces);
            case "replace": return Encoding.UTF8.GetString(regex.Replace(input, "<$&>"u8, token));
            case "destination":
                var destination = new byte[4096];
                var status = regex.TryReplace(input, "<$&>"u8, destination, out var written, token);
                Assert.Equal(OperationStatus.Done, status);
                return Encoding.UTF8.GetString(destination.AsSpan(0, written));
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
