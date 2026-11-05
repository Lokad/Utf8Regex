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
    public void PublicCapturesAndDeclinedShapesKeepTheirOutcomes(string pattern, string text)
    {
        var options = RegexOptions.CultureInvariant;
        var oracle = new Regex(pattern, options);
        var owner = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 10000, MaxWorkingBytes = 1 << 24, MaxResultBytes = 1 << 24 });
        var bytes = Encoding.UTF8.GetBytes(text);
        var expected = oracle.Match(text);
        var actual = owner.MatchDetailed(bytes);
        Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected, actual);
        Assert.Equal(oracle.Count(text), owner.Count(bytes));
        Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected, actual);
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
        var owner = new Utf8Regex(pattern, options);
        var actual = owner.MatchDetailed(Encoding.UTF8.GetBytes(text));
        Utf8SliceSparseGroupTests.AssertDetailed(oracle, oracle.Match(text), actual);
    }

    [Fact]
    public void ScalarBoundsAndCaptureSnapshotsSurviveLaterAttempts()
    {
        var owner = new Utf8Regex(@"(?<run>[é😀]{1,2})[0-9]+");
        var bytes = Encoding.UTF8.GetBytes("😀é12 é3");
        var retained = owner.MatchDetailed(bytes);
        Assert.Equal("😀é12", retained.GetValueString());
        Assert.Equal("😀é", retained.GetGroup(owner.GroupNumberFromName("run")).GetValueString());
        Assert.Equal((8, 5), (retained.LengthInBytes, retained.LengthInUtf16));
        Assert.Equal(2, owner.Count(bytes));
        Assert.Equal("😀é", retained.GetGroup(owner.GroupNumberFromName("run")).GetValueString());
    }

    [Fact]
    public void RepeatBuffersAreAdmittedWithoutUnnecessaryRewindStorage()
    {
        const string pattern = @"\Aitem\s+[0-9]+\z";
        var capacity = 2 * Utf8ConstructionBudget.ArrayBytes<int>(2);
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 1,
            MaxWorkingBytes = capacity, MaxResultBytes = 1 };
        var owner = new Utf8Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout, policy);
        Assert.True(owner.Match("item   12345"u8).Success);
        var denied = new Utf8Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout,
            policy with { MaxWorkingBytes = capacity - 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Match("item   12345"u8)).ResourceKind);
        Assert.True(owner.Match("item   12345"u8).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TheBoundedDisjointnessProofObservesOriginalInterruption(bool expire)
    {
        const string pattern = @"[^\x00-\x7F]+[\x00-\x7F]+";
        var input = "éabc"u8.ToArray();
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var owner = new Utf8Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(20),
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 128, MaxWorkingBytes = 1 << 20 });
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
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => owner.Match(input, token.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(interrupted);
        ticks = 0;
        Assert.True(owner.Match(input).Success);
    }
}
