using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCompletedBackreferenceTests
{
    private const string Pattern = @"(?P<word>[a-z]+)-(?P=word)";
    private static Utf8ResourcePolicy Funded => new()
    {
        MaxWorkSteps = 1_000_000,
        MaxDepth = 4096,
        MaxWorkingBytes = 1 << 20,
        MaxResultBytes = 1 << 20
    };

    [Theory]
    [InlineData(Pattern, "token-token")]
    [InlineData(Pattern, "token-other")]
    [InlineData(@"(a+)a\1", "aaaaaaa")]
    [InlineData(@"(a|aa)+\1", "aaaa")]
    [InlineData(@"(a*)\1", "")]
    [InlineData(@"(x)?b\1", "b")]
    [InlineData(@"(?P<x>é😀)-(?P=x)", "é😀-é😀")]
    [InlineData(@"(a)(?<=\1)", "a")]
    [InlineData(@"(a)?(?(1)\1|b)", "aa")]
    public void FullAndGlobalResultsAgreeWithTheJournalPath(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        // The empty assertion retains the old journal classification without
        // changing the accepted language or capture numbering.
        var reference = new Utf8PythonRegex("(?=)(?:" + pattern + ")");
        using var source = new CancellationTokenSource();
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, Funded })
        {
            var regex = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = policy
            });
            Assert.Equal(reference.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success, regex.Match(input, 0, cancellationToken: source.Token, mode: Utf8PythonMatchMode.FullMatch).Success);
            Utf8PythonRangeMatch snapshot1 = default;
            using (var captureCursor1 = reference.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
            {
                captureCursor1.MoveNext(out snapshot1);
            }
            Utf8PythonRangeMatch snapshot2 = default;
            using (var captureCursor2 = regex.CreateMatchCursor(input, 0, source.Token, mode: Utf8PythonMatchMode.FullMatch))
            {
                captureCursor2.MoveNext(out snapshot2);
            }
            PythonReCaptureAssert.Equal(snapshot1, snapshot2);
            var repeated = Encoding.UTF8.GetBytes("😀 " + text + " " + text);
            var collectedRanges1 = new List<Utf8PythonRangeMatch>();
            using (var rangeCursor1 = reference.CreateMatchCursor(repeated, 0, default))
            {
                while (rangeCursor1.MoveNext(out var rangeMatch1))
                    collectedRanges1.Add(rangeMatch1);
            }
            var collectedRanges2 = new List<Utf8PythonRangeMatch>();
            using (var rangeCursor2 = regex.CreateMatchCursor(repeated, 0, source.Token))
            {
                while (rangeCursor2.MoveNext(out var rangeMatch2))
                    collectedRanges2.Add(rangeMatch2);
            }
            PythonReCaptureAssert.Equal(collectedRanges1.ToArray(), collectedRanges2.ToArray());
            ReadOnlyMemory<byte> snapshots1Input = repeated;
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = reference.CreateMatchCursor(snapshots1Input, 0, default))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            ReadOnlyMemory<byte> snapshots2Input = repeated;
            var snapshots2 = new List<Utf8PythonRangeMatch>();
            using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, source.Token))
            {
                while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                    snapshots2.Add(snapshots2Match);
            }
            Assert.Equal(snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray(),
                            snapshots2.Select(captureSnapshot2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureSnapshot2.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
            Assert.Equal(System.Text.Encoding.UTF8.GetString(reference.Replace(repeated, "!"u8)), System.Text.Encoding.UTF8.GetString(regex.Replace(repeated, "!"u8)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedCaptureAndAliasesSurviveOriginalInterruption(bool expire)
    {
        Utf8PythonRangeMatch cursorRange = default;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8PythonRegex(Pattern, options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan,
            ResourcePolicy = Funded
        });
        using var cursor = regex.CreateMatchCursor("token-token token-token"u8.ToArray(), 0, source.Token);
        var alias = cursor;
        ref var invocation = ref cursor.WorkingInvocation;
        ticks = 15;
        Assert.True(cursor.MoveNext(out cursorRange));
        var retained = cursorRange;
        Assert.Equal("token", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        Assert.Equal(1, retained.LastIndex);
        if (expire)
        {
            ticks = 25;
            Assert.Throws<RegexMatchTimeoutException>(() => alias.MoveNext(out _));
        }
        else
        {
            source.Cancel();
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => alias.MoveNext(out _)).CancellationToken);
        }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal("token", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }

    [Fact]
    public void FiniteAdmissionRemainsBeforePublication()
    {
        var input = "token-token"u8.ToArray();
        foreach (var policy in new[] { new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 }, new Utf8ResourcePolicy { MaxResultBytes = 1 } })
        {
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                using var regex = new Utf8PythonRegex(Pattern, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan,
                    ResourcePolicy = policy
                });
                using var cursor = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch);
                cursor.MoveNext(out _);
            });
        }
    }
}
