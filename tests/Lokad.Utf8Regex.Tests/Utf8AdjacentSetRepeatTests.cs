using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AdjacentSetRepeatTests
{
    [Theory]
    [InlineData(@"\Aitem\s+[0-9]+\z", "item   12345")]
    [InlineData(@"\Aitem(\s+)[0-9]+\z", "item\u2003\u3000 12345")]
    [InlineData(@"(\s+)[0-9]", "π😀  7 xx \u20032")]
    [InlineData(@"([a-z]+)[0-9]+", "abc123 de45")]
    [InlineData(@"([a-z]{0,3})[0-9]{1,2}", "1 abc12 abcd34")]
    [InlineData(@"\A([a-zA-Z]{1,4})([a-zA-Z]{2})!\z", "abcd!")]
    [InlineData(@"\A([a-zA-Z]{1,4}?)([a-zA-Z]{2})!\z", "abcd!")]
    [InlineData(@"\A(\d{1,4})(\d{2})!\z", "1234!")]
    [InlineData(@"\A(\d{1,4}?)(\d{2})!\z", "١234!")]
    [InlineData(@"([a-zA-Z]{1,4})é", "abcdé abé")]
    [InlineData(@"\A([a-zA-Z]{2,4})!\z", "aé!")]
    [InlineData(@"(([^0-9]+))[0-9]+", "é😀123 4")]
    [InlineData(@"([0-9 ]+)[0-9]+", "   12345")]
    [InlineData(@"([a-z]*)(?:[a-z]+)", "abc")]
    [InlineData(@"([a-z]+)[0-9]*[a-z]+", "abc")]
    [InlineData(@"(?i:([a-z]+))[K0-9]+", "abkK12")]
    [InlineData(@"([a-z]+)(?i:[K0-9]+)", "abkK12")]
    [InlineData(@"([a-z]+?)[0-9]+", "abc123 de45")]
    [InlineData(@"(?<x>[a-z]+)[0-9]+\k<x>", "abc12abc abc1ab")]
    [InlineData(@"(?:(?<x>[a-z]+)[0-9]+)*Z", "abc1de2Z")]
    [InlineData(@"(?=([a-z]+)[0-9]+)\1[0-9]+", "abc12")]
    [InlineData(@"(?<=([a-z]+)[0-9]+)!", "abc12!")]
    [InlineData(@"(?<x>[a-z]+)[0-9]+(?(x)X|Y)", "abc12X")]
    [InlineData(@"([a-z]+)[^a-z0-9]+", "abc!!!")]
    [InlineData(@"([a-z]+)[é0-9]+", "abcé12")]
    [InlineData(@"\A([\s!]+)[A-Z]{2}\z", " \u2003!AB")]
    [InlineData(@"\A([\sA]+)[A-Z]{2}\z", " \u2003AB")]
    [InlineData(@"\A([^\sX]+)[A-Z]{2}\z", "éabcAB")]
    [InlineData(@"(\w+)\s+\w+\s+Holmes", "π😀 é\u0085名\u2028Holmes Ω\u3000薛\tHolmes")]
    [InlineData(@"(\p{L}+)\p{Zs}+K", "é\u00a0K 東\u3000K")]
    [InlineData(@"(\p{L}+)\p{Ll}+Z", "abcZ éλZ")]
    [InlineData(@"([\p{L}\u0085]+)\s+Z", "é\u0085Z")]
    [InlineData(@"([\p{L}\t]+)\s+Z", "é\tZ")]
    [InlineData(@"([^\p{L}]+)\s+Z", "!\u2029Z")]
    [InlineData(@"\A(?<run>[\sA]+[^\sX]+)\z", "A")]
    [InlineData(@"\A(?<run>[\sA]+[^\sX]+)\z", "AX")]
    [InlineData(@"\A(?<run>[\sA]+[^\sX]+)\z", "A Z")]
    [InlineData(@"\A(?<run>[\sA]+[^\sX]+)\z", "AZ")]
    [InlineData(@"\A(?<run>[\sA]+[^\sX]+)\z", "AA")]
    [InlineData(@"\A(?<run>[\sA]+[^\sX]+)\z", "Aé📍")]
    [InlineData(@"(?<run>[^/\s]+[^\s]+)(?:x|/x)\z", "abc/x")]
    [InlineData(@"(?<run>[^/\s]+[^\s]+)(?:x|/x)\z", "a📍/x")]
    [InlineData(@"\A(?<run>[^/\s]+[^\s]+)\z", "/abc")]
    [InlineData(@"\A(?<run>([^/\s]+)[^\s]+)\z", "abc/x")]
    [InlineData(@"\A(?<run>[^/\s]+[^\s]+?)", "abc/x")]
    public void PublicCapturesAndDeclinedShapesKeepTheirOutcomes(string pattern, string text)
    {
        var options = RegexOptions.CultureInvariant;
        var oracle = new Regex(pattern, options);
        var owner = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1_000_000,
                MaxDepth = 10000,
                MaxWorkingBytes = 1 << 24,
                MaxResultBytes = 1 << 24
            }
        });
        var bytes = Encoding.UTF8.GetBytes(text);
        var expected = oracle.Match(text);
        var actual = Utf8MatchSequence.First(owner.CreateMatchCursor(bytes));
        Utf8MatchSequence.AssertCaptures(expected, actual, bytes, text);
        Assert.Equal(oracle.Count(text), owner.Count(bytes));
        Utf8MatchSequence.AssertCaptures(expected, actual, bytes, text);
    }

    [Theory]
    [InlineData(RegexOptions.None)] [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)] [InlineData(RegexOptions.NonBacktracking)]
    public void ExecutionOptionsKeepTheirTraversalAndCaptures(RegexOptions options)
    {
        const string pattern = @"([a-z]+)[0-9]+";
        const string text = "abc12 def345";
        options |= RegexOptions.CultureInvariant;
        var oracle = new Regex(pattern, options);
        var owner = new Utf8Regex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(text);
        var actual = Utf8MatchSequence.First(owner.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(oracle.Match(text), actual, input, text);
    }

    [Fact]
    public void SubsumedFirstClassStillRequiresTwoScalars()
    {
        using var regex = new Utf8Regex(@"\A(?<path>[^/\s]+[^\s]+)\z");
        Assert.False(regex.IsMatch("😀"u8));
        Assert.False(regex.IsMatch("/😀"u8));
        var slots = new Utf8CaptureRange[2];
        Assert.Equal(new Utf8CaptureRange(0, 5), regex.Match("😀/"u8, captures: slots));
        Assert.Equal(slots[0], slots[1]);
    }

    [Fact]
    public void ScalarBoundsAndCaptureSnapshotsSurviveLaterAttempts()
    {
        var owner = new Utf8Regex(@"(?<run>[é😀]{1,2})[0-9]+");
        var bytes = Encoding.UTF8.GetBytes("😀é12 é3");
        var retained = Utf8MatchSequence.First(owner.CreateMatchCursor(bytes));
        Assert.Equal("😀é12"u8.ToArray(), retained.GetValueMemory().ToArray());
        Assert.Equal("😀é"u8.ToArray(), retained.GetGroupMemory(owner.GroupNumberFromUtf8Name("run"u8)).ToArray());
        Assert.Equal((0, 8), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.Equal(2, owner.Count(bytes));
        Assert.Equal("😀é"u8.ToArray(), retained.GetGroupMemory(owner.GroupNumberFromUtf8Name("run"u8)).ToArray());
    }

    [Theory]
    [InlineData(@"\Aitem\s+[0-9]+\z", "item   12345")]
    [InlineData(@"\A\w+\s+\w+\s+Holmes\z", "é\u0085東\u3000Holmes")]
    [InlineData(@"\A\p{L}+\p{Zs}+K\z", "é\u00a0K")]
    public void FusedRunsNeedNoRepeatOrRewindStorage(string pattern, string text)
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 1,
            MaxWorkingBytes = 1, MaxResultBytes = 1 };
        var owner = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        });
        Assert.True(owner.Match(Encoding.UTF8.GetBytes(text), 0, []).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TheBoundedDisjointnessProofObservesOriginalInterruption(bool expire)
    {
        // A wider class retains the per-member proof and reaches its bounded
        // diagnostic checkpoint. Two plain masks now need only two charges;
        // their completed proof cannot reach the hook's 64-step cadence.
        const string pattern = @"[^\x00-\x7Fé]+[\x00-\x7F]+";
        var input = "😀abc"u8.ToArray();
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var owner = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1_000_000,
                MaxDepth = 128,
                MaxWorkingBytes = 1 << 20
            }
        });
        var interrupted = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 64) return;
            interrupted = true;
            if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(pattern, Assert.Throws<RegexMatchTimeoutException>(() => owner.Match(input)).Pattern);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => owner.Match(input, cancellationToken: token.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(interrupted);
        ticks = 0;
        Assert.True(owner.Match(input).Success);
    }
}
