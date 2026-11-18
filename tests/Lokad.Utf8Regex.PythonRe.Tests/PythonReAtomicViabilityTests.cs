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
    [InlineData("a++a")]
    [InlineData("(?>a+)a")]
    [InlineData("(?>a*)a")]
    [InlineData("é++é")]
    public void ImpossibleAtomicRunsFinishWithoutScalarWorkspace(string pattern)
    {
        using var regex = Utf8PythonRegex.Compile(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Policy,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default);
        Assert.Equal(Utf8SliceInstructionKind.Fail, Program(regex).Instructions[0].Kind);
        using var live = new CancellationTokenSource();
        foreach (var input in new[] { ""u8.ToArray(), "aaaa"u8.ToArray(), "😀éaaé😀"u8.ToArray() })
        {
            Assert.False(regex.Match(input, 0, cancellationToken: live.Token, mode: Utf8PythonMatchMode.Search).Success);
            Assert.False(regex.Match(input, 0, cancellationToken: live.Token).Success);
            Assert.False(regex.Match(input, 0, cancellationToken: live.Token, mode: Utf8PythonMatchMode.FullMatch).Success);
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
        Assert.Equal(success, regex.Match(Encoding.UTF8.GetBytes(text), mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void DeadSequencesKeepCaptureCatalogsAndEnclosingBranchSemantics()
    {
        var impossible = new Utf8PythonRegex("(?P<before>x)?a++a(?P<after>y)");
        var copiedNames30 = new string[impossible.CaptureGroupCount + 1];
        for (var captureSlot30 = 0; captureSlot30 < copiedNames30.Length; captureSlot30++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                impossible.TryCopyGroupNameUtf8(captureSlot30, [], out var nameLength30));
            var nameBytes30 = new byte[nameLength30];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                impossible.TryCopyGroupNameUtf8(captureSlot30, nameBytes30, out var written30));
            Assert.Equal(nameLength30, written30);
            copiedNames30[captureSlot30] = System.Text.Encoding.UTF8.GetString(nameBytes30);
        }
        Assert.Equal(new[] { "0", "before", "after" }, copiedNames30);
        Assert.Equal(2, Program(impossible).GroupCount);
        Assert.Equal(0, impossible.Count("xaaaay"u8));
        var negative = new Utf8PythonRegex("(?!a++a)(?P<value>.)");
        Utf8PythonRangeMatch negativeHit = default;
        using (var captureCursor1 = negative.CreateMatchCursor(("😀"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out negativeHit);
        }
        Assert.True(negativeHit.Success); Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((negativeHit.Groups[1].Success ? negativeHit.GetGroupMemory(negativeHit.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        var conditional = new Utf8PythonRegex("(?:(?P<x>a++a)|b)(?(x)c|d)");
        Assert.True(conditional.Match("bd"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(conditional.Match("bc"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        var allowance = new Allowance(16 << 20);
        using var branches = Utf8PythonRegex.Compile("(?P<dead>a++a)|(?P<live>é+)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = allowance
        }, cancellationToken: default);
        Utf8PythonRangeMatch retained = default;
        using (var captureCursor2 = branches.CreateMatchCursor(("xxéé"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor2.MoveNext(out retained);
        }
        Assert.True(retained.Success); Assert.False(retained.Groups[1].Success);
        Assert.Equal("éé", System.Text.Encoding.UTF8.GetString((retained.Groups[2].Success ? retained.GetGroupMemory(retained.Groups[2].Number).Span : ReadOnlySpan<byte>.Empty)));
        branches.Dispose(); Assert.Equal(0UL, allowance.Used);
        Assert.Equal("éé", System.Text.Encoding.UTF8.GetString((retained.Groups[2].Success ? retained.GetGroupMemory(retained.Groups[2].Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void ScalarOffsetsOriginalTokensAndFundingRemainAuthoritative()
    {
        var allowance = new Allowance(16 << 20);
        using var regex = Utf8PythonRegex.Compile("a++a", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Policy,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = allowance
        }, cancellationToken: default);
        var held = allowance.Used; allowance.Maximum = held;
        Assert.False(regex.Match("😀aaaa"u8, 4, mode: Utf8PythonMatchMode.Search).Success); Assert.Equal(held, allowance.Used);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("😀aaaa"u8, 1, mode: Utf8PythonMatchMode.Search));
        byte[] malformed = [.. "aaaa"u8, 0xf0, 0x9f];

        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(malformed, 0, cancellationToken: canceled.Token, mode: Utf8PythonMatchMode.Search)).CancellationToken);
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public void WorkRefusalCannotPublishProvedFailure()
    {
        var denied = false; var admitted = false;
        for (ulong work = 1; work < 30; work++)
        {
            var regex = new Utf8PythonRegex("a++a", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy with
                {
                    MaxWorkSteps = work
                }
            });
            try { Assert.False(regex.Match("aaaa"u8, mode: Utf8PythonMatchMode.Search).Success); admitted = true; break; }
            catch (Utf8ResourceLimitExceededException error)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); denied = true; }
        }
        Assert.True(denied); Assert.True(admitted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProvedFailureStillChecksFinalPublication(bool expire)
    {
        var regex = new Utf8PythonRegex("a++a", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Policy
        });
        using var token = new CancellationTokenSource();
        var reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => { reads++; return 0; }))
            Assert.False(regex.Match("aaaa"u8, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search).Success);
        var terminal = reads; Assert.True(terminal > 0); reads = 0; long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == terminal) { if (expire) ticks = 50; else token.Cancel(); } return ticks; });
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("aaaa"u8, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("aaaa"u8, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search)).CancellationToken);
    }
}
