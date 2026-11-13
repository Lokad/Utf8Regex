using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReAtomicViabilityTests
{
    private static readonly Utf8ResourcePolicy Policy = new()
    { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };

    private static Utf8SliceProgram Program(Utf8PythonRegex regex) =>
        (Utf8SliceProgram)typeof(Utf8PythonRegex).GetField("_scalarProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;

    [Theory]
    [InlineData("a++a")] [InlineData("(?>a+)a")] [InlineData("(?>a*)a")]
    [InlineData("é++é")]
    public void ImpossibleAtomicRunsFinishWithoutScalarWorkspace(string pattern)
    {
        using var regex = Utf8PythonRegex.Compile(pattern, PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, Policy, default, Timeout.InfiniteTimeSpan, default);
        Assert.Equal(Utf8SliceInstructionKind.Fail, Program(regex).Instructions[0].Kind);
        using var live = new CancellationTokenSource();
        foreach (var input in new[] { ""u8.ToArray(), "aaaa"u8.ToArray(), "😀éaaé😀"u8.ToArray() })
        {
            Assert.False(regex.Search(input, 0, live.Token).Success);
            Assert.False(regex.Match(input, 0, live.Token).Success);
            Assert.False(regex.FullMatch(input, 0, live.Token).Success);
            Assert.False(regex.IsMatch(input, 0, live.Token));
            Assert.Equal(0, regex.Count(input, 0, live.Token));
        }
    }

    [Theory]
    [InlineData("a+a", "aa", true)]
    [InlineData("a{1,2}+a", "aaa", true)]
    [InlineData("(?>a+?)a", "aa", true)]
    [InlineData("(?-i:a)++A", "aaA", true)]
    [InlineData("a++(?:a|b)", "aab", true)]
    [InlineData("(?i:a)++a", "Aa", false)]
    [InlineData("(a)++a", "aa", false)]
    [InlineData("😀++😀", "😀😀", false)]
    [InlineData("(?>ab+)c", "xxabbbdzz", false)]
    [InlineData("(?>ab+)c", "xxabbbczz", true)]
    public void LazyFiniteFoldedCapturedAndCompoundAtomsRetainVerification(string pattern, string text, bool success)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.NotEqual(Utf8SliceInstructionKind.Fail, Program(regex).Instructions[0].Kind);
        Assert.Equal(success, regex.Search(Encoding.UTF8.GetBytes(text)).Success);
    }

    [Fact]
    public void DeadSequencesKeepCaptureCatalogsAndEnclosingBranchSemantics()
    {
        var impossible = new Utf8PythonRegex("(?P<before>x)?a++a(?P<after>y)");
        Assert.Equal(new[] { "0", "before", "after" }, impossible.GetGroupNames());
        Assert.Equal(2, Program(impossible).GroupCount);
        Assert.Equal(0, impossible.Count("xaaaay"u8));
        var negative = new Utf8PythonRegex("(?!a++a)(?P<value>.)");
        var negativeHit = negative.SearchDetailedData("😀"u8);
        Assert.True(negativeHit.Success); Assert.Equal("😀", negativeHit.Groups[1].ValueText);
        var conditional = new Utf8PythonRegex("(?:(?P<x>a++a)|b)(?(x)c|d)");
        Assert.True(conditional.FullMatch("bd"u8).Success);
        Assert.False(conditional.FullMatch("bc"u8).Success);
        var allowance = new Allowance(16 << 20);
        using var branches = Utf8PythonRegex.Compile("(?P<dead>a++a)|(?P<live>é+)", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, allowance);
        var retained = branches.SearchDetailedData("xxéé"u8);
        Assert.True(retained.Success); Assert.False(retained.Groups[1].Success);
        Assert.Equal("éé", retained.Groups[2].ValueText);
        branches.Dispose(); Assert.Equal(0UL, allowance.Used);
        Assert.Equal("éé", retained.Groups[2].ValueText);
    }

    [Fact]
    public void ScalarOffsetsOriginalTokensAndFundingRemainAuthoritative()
    {
        var allowance = new Allowance(16 << 20);
        using var regex = Utf8PythonRegex.Compile("a++a", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, Policy, default, Timeout.InfiniteTimeSpan, default, allowance);
        var held = allowance.Used; allowance.Maximum = held;
        Assert.False(regex.Search("😀aaaa"u8, 4).Success); Assert.Equal(held, allowance.Used);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Search("😀aaaa"u8, 1));
        byte[] malformed = [.. "aaaa"u8, 0xf0, 0x9f];

        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.Search(malformed, 0, canceled.Token)).CancellationToken);
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public void WorkRefusalCannotPublishProvedFailure()
    {
        var denied = false; var admitted = false;
        for (ulong work = 1; work < 30; work++)
        {
            var regex = new Utf8PythonRegex("a++a", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
                Policy with { MaxWorkSteps = work });
            try { Assert.False(regex.Search("aaaa"u8).Success); admitted = true; break; }
            catch (Utf8ResourceLimitExceededException error)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); denied = true; }
        }
        Assert.True(denied); Assert.True(admitted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ProvedFailureStillChecksFinalPublication(bool expire)
    {
        var regex = new Utf8PythonRegex("a++a", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20), Policy);
        using var token = new CancellationTokenSource();
        var reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => { reads++; return 0; }))
            Assert.False(regex.Search("aaaa"u8, 0, token.Token).Success);
        var terminal = reads; Assert.True(terminal > 0); reads = 0; long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == terminal) { if (expire) ticks = 50; else token.Cancel(); } return ticks; });
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Search("aaaa"u8, 0, token.Token)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Search("aaaa"u8, 0, token.Token)).CancellationToken);
    }
}
