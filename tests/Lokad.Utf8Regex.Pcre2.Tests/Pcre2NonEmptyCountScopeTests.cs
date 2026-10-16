using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2NonEmptyCountScopeTests
{
    [Theory]
    [InlineData("(a+)(b)", "😀aaabéab", 2)]
    [InlineData(@"(a+)\1", "aaaa aaa aa", 3)]
    [InlineData("(a+?)(b)", "aaab ab", 2)]
    [InlineData("(a+)(?=b)b", "aaab ab", 2)]
    [InlineData("(?<=(a))(b+)", "abbb abb", 2)]
    [InlineData(@"\G(a+)(b)", "aabab ab", 2)]
    [InlineData("(?>a+)(b)", "aaab ab", 2)]
    [InlineData("(?:(a+)|b+)(?(1)c|d)", "aac bbd ac bd", 4)]
    [InlineData("(😀+)(é+)", "😀😀ééx😀é", 2)]
    public void SharedCountPreservesNativeCaptureDependentGlobalCounts(string pattern, string text, int expected)
    {
        var regex = Create(pattern, result: 1);
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.False(direct.SliceProgram.CanMatchEmpty);
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expected, regex.Count(input, 0, Pcre2MatchOptions.None, token.Token));
        Assert.Equal(Pcre2ErrorKind.ResultLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed(Encoding.UTF8.GetBytes(text))).ErrorKind);
    }

    [Fact]
    public void CoreGrammarDelegationUsesItsAlreadyAdmittedScalarProgram()
    {
        var regex = Create(@"\u0041+", result: 1);
        Assert.IsType<Pcre2Utf8DirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        using var token = new CancellationTokenSource();
        Assert.Equal(2, regex.Count("😀AAA A"u8, 4));
        Assert.Equal(2, regex.Count("😀AAA A"u8, 4, Pcre2MatchOptions.None, token.Token));
        Assert.Equal(3, Create(@"\u0041*").Count("😀AA"u8));
    }

    [Fact]
    public void ScalarOffsetsAndExplicitOptionsKeepTheirProgression()
    {
        var regex = Create("(a+)(b)");
        Assert.Equal(2, regex.Count("😀aaab ab"u8, 4));
        Assert.Equal(1, regex.Count("😀aaab ab"u8, 9));
        Assert.Equal(1, regex.Count("😀aaab ab"u8, 4, Pcre2MatchOptions.Anchored));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀aaab ab"u8, 1));
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { (byte)'a', (byte)'b', 255 }));
        Assert.Equal(3, Create("(a*)").Count("😀a"u8));
        Assert.Equal(2, Create("(a)?").Count("😀"u8));
    }

    [Theory]
    [InlineData(1UL)] [InlineData(1048576UL)]
    public void ActualScratchIsAdmittedAndReleasedOnSuccessOrStickyDenial(ulong working)
    {
        var regex = Create("(a+)(b)", working: working);
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate("aaab ab"u8,
            ref invocation, out var input));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (working == 1)
            {
                Pcre2MatchException? failure = null;
                try
                {
                    Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                        new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out _);
                }
                catch (Pcre2MatchException error) { failure = error; }
                Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            }
            else
            {
                Assert.True(Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                    new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
                Assert.Equal(2, count);
            }
            Assert.True(invocation.Budget.PeakWorkingBytes > 0);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void ValidationAndMatchingKeepOneCumulativeWorkAllowance()
    {
        var regex = Create("(a+)(b)");
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaab ", 100)));
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(bytes,
            ref invocation, out var input));
        var validation = invocation.Budget.Steps;
        Assert.True(Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
            new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
        Assert.Equal(100, count);
        var matching = invocation.Budget.Steps - validation;
        Assert.True(validation > 0 && matching > 0);
        var cap = checked((uint)Math.Max(validation, matching));
        var denied = Create("(a+)(b)", work: cap);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => denied.Count(bytes)).ErrorKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterruptionAfterValidationReleasesScratchAndKeepsRetainedCaptures(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(a+)(b)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, TimeSpan.FromMilliseconds(20));
        var retained = regex.MatchDetailed("aaab"u8);
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, token.Token);
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaab ", 3000)));
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(bytes,
            ref invocation, out var input));
        var validated = invocation.Budget.Steps;
        int checkpoints = 0;
        invocation.CheckpointHook = _ =>
        {
            checkpoints++;
            if (cancel) token.Cancel(); else ticks = 25;
        };
        Exception? failure = null;
        try
        {
            Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out _);
        }
        catch (Exception error) { failure = error; }
        Assert.True(checkpoints > 0 && invocation.Budget.Steps > validated);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        if (cancel)
            Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
            Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        Assert.Equal("aaa", retained.GetGroup(1).GetValueString());
        Assert.Equal("b", retained.GetGroup(2).GetValueString());
        Assert.Equal(1, regex.Count("ab"u8));
    }

    private static Utf8Pcre2Regex Create(string pattern, ulong working = 1 << 20,
        ulong result = 1 << 20, uint work = 1000000) =>
        new(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = work, DepthLimit = 256, HeapLimitInBytes = working,
                ResultLimitInBytes = result }, Timeout.InfiniteTimeSpan);
}
