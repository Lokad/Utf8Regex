using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCompletedBackreferenceTests
{
    private const string Pattern = @"(?P<word>[a-z]+)-(?P=word)";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1_000_000,
        MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

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
            var regex = new Utf8PythonRegex(pattern, default, Timeout.InfiniteTimeSpan, policy);
            Assert.Equal(reference.FullMatch(input).Success, regex.FullMatch(input, 0, source.Token).Success);
            PythonReCaptureAssert.Equal(reference.FullMatchDetailed(input), regex.FullMatchDetailed(input, 0, source.Token));
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
            Assert.Equal(PythonReFindAllAssert.Decode(reference.FindAllToUtf8(repeated)).ScalarValues,
                PythonReFindAllAssert.Decode(regex.FindAllToUtf8(repeated, 0, source.Token)).ScalarValues);
            Assert.Equal(System.Text.Encoding.UTF8.GetString(reference.Replace(repeated, "!")), System.Text.Encoding.UTF8.GetString(regex.Replace(repeated, "!")));
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
        var regex = new Utf8PythonRegex(Pattern, default, expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan, Funded);
        using var cursor = regex.CreateMatchCursor("token-token token-token"u8.ToArray(), 0, source.Token);
        var alias = cursor;
        ref var invocation = ref cursor.WorkingInvocation;
        ticks = 15; Assert.True(cursor.MoveNext(out cursorRange)); var retained = cursorRange;
        Assert.Equal("token", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        Assert.Equal(1, retained.LastIndex); Assert.Equal("word", retained.LastGroup);
        if (expire) { ticks = 25; Assert.Throws<RegexMatchTimeoutException>(() => alias.MoveNext(out _)); }
        else { source.Cancel(); Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => alias.MoveNext(out _)).CancellationToken); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal("token", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }

    [Fact]
    public void OriginalCancellationAndFiniteAdmissionRemainBeforePublication()
    {
        var malformed = "token-token"u8.ToArray().Concat(new byte[] { 0xff }).ToArray();

        foreach (var policy in new[] { new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 }, new Utf8ResourcePolicy { MaxResultBytes = 1 } })
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new Utf8PythonRegex(Pattern, default, Timeout.InfiniteTimeSpan, policy).FullMatchDetailed("token-token"u8.ToArray()));
    }
}
