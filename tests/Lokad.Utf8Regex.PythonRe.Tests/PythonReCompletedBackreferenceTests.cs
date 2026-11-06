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
            Assert.Equal(JsonSerializer.Serialize(reference.FullMatchDetailedData(input)),
                JsonSerializer.Serialize(regex.FullMatchDetailedData(input, 0, source.Token)));
            var repeated = Encoding.UTF8.GetBytes("😀 " + text + " " + text);
            Assert.Equal(JsonSerializer.Serialize(reference.FindIterDetailed(repeated)),
                JsonSerializer.Serialize(regex.FindIterDetailed(repeated, 0, source.Token)));
            Assert.Equal(reference.FindAllToStrings(repeated).ScalarValues,
                regex.FindAllToStrings(repeated, 0, source.Token).ScalarValues);
            Assert.Equal(reference.ReplaceToString(repeated, "!"), regex.ReplaceToString(repeated, "!"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedCaptureAndAliasesSurviveOriginalInterruption(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8PythonRegex(Pattern, default, expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan, Funded);
        using var cursor = regex.CreateMatchCursor("token-token token-token"u8.ToArray(), 0, source.Token);
        var alias = cursor;
        ref var invocation = ref cursor.WorkingInvocation;
        ticks = 15; Assert.True(cursor.MoveNext()); var retained = alias.Current;
        Assert.Equal("token", retained.Groups[1].ValueText);
        Assert.Equal(1, retained.LastIndex); Assert.Equal("word", retained.LastGroup);
        if (expire) { ticks = 25; Assert.Throws<RegexMatchTimeoutException>(() => alias.MoveNext()); }
        else { source.Cancel(); Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => alias.MoveNext()).CancellationToken); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal("token", retained.Groups[1].ValueText);
    }

    [Fact]
    public void StrictValidationAndFiniteAdmissionRemainBeforePublication()
    {
        var malformed = "token-token"u8.ToArray().Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() => new Utf8PythonRegex(Pattern).FullMatch(malformed));
        foreach (var policy in new[] { new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 }, new Utf8ResourcePolicy { MaxResultBytes = 1 } })
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new Utf8PythonRegex(Pattern, default, Timeout.InfiniteTimeSpan, policy).FullMatchDetailedData("token-token"u8.ToArray()));
    }
}
