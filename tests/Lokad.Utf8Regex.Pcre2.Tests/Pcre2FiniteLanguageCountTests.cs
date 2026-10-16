using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2FiniteLanguageCountTests
{
    [Theory]
    [InlineData("(a)(b)", "ab ab", 2)]
    [InlineData("(a|aa)a", "aaaaaa", 3)]
    [InlineData("(ab|a)b", "abbab", 2)]
    [InlineData(@"(?<x>a|b)\k<x>", "aa bb ab ba", 2)]
    [InlineData(@"(a)\1", "aa aaa", 2)]
    [InlineData(@"(?<x>😀)(é)", "😀éx😀é", 2)]
    [InlineData(@"(?<x>a)\Kb", "ab ab", 2)]
    [InlineData("(a)?b", "b ab b", 3)]
    [InlineData("(a{2,3}?)(a)", "aaaaaa", 2)]
    public void ControlledCountPreservesTheProvedLanguageAndOrder(string pattern, string text, int expected)
    {
        var regex = Create(pattern, new() { MatchLimit = 1000000, DepthLimit = 256,
            HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 });
        Assert.IsAssignableFrom<Pcre2LiteralFamilyDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expected, regex.Count(input, 0, Pcre2MatchOptions.None, token.Token));
        // Count publishes no owned result payload, while detailed results still do.
        Assert.Equal(Pcre2ErrorKind.ResultLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed(input)).ErrorKind);
    }

    [Theory]
    [InlineData("(a)?", "😀", 2)]
    [InlineData("(a*)", "aaa", 2)]
    [InlineData("(?<=a)b", "ab abb", 2)]
    [InlineData("(a+)(b)", "ab aaab", 2)]
    [InlineData("(?i)(a)(b)", "AB ab", 2)]
    public void EmptyAndUnprovedPatternsKeepTheirExistingSemantics(string pattern, string text, int expected)
    {
        var regex = Create(pattern, new() { MatchLimit = 1000000, DepthLimit = 256,
            HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 });
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void ValidationAndCountingSpendOneCumulativeAllowance()
    {
        const string pattern = "(a)(b)";
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 100)));
        var measured = Create(pattern, new() { MatchLimit = 1000000 });
        var invocation = Pcre2SliceDriver.CreateInvocation(measured.DebugCompiledProgram.Request, default);
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(input, ref invocation, out var validated));
        var validationSteps = invocation.Budget.Steps;
        Assert.True(Pcre2GlobalOperationDriver.TryCount(measured.DebugCompiledProgram,
            ref validated, new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
        Assert.Equal(100, count);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        var matchingSteps = invocation.Budget.Steps - validationSteps;
        Assert.True(validationSteps > 0 && matchingSteps > 0);
        // Either phase fits separately. Their combined work must still deny.
        var limit = checked((uint)Math.Max(validationSteps, matchingSteps));
        var denied = Create(pattern, new() { MatchLimit = limit });
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => denied.Count(input)).ErrorKind);
    }

    [Fact]
    public void ActualAlternativeFramesReleaseWorkingCapacityOnSuccessAndDenial()
    {
        foreach (var allowance in new[] { 1UL << 20, 1UL })
        {
            var regex = Create("((ab)|(a))", new() { HeapLimitInBytes = allowance });
            var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate("ab a"u8,
                ref invocation, out var input));
            if (allowance == 1)
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
    public void StartOptionsMalformedTailsAndHeapAdmissionStayAuthoritative()
    {
        var regex = Create("(a)(b)", new() { MatchLimit = 1000000, HeapLimitInBytes = 1 << 20 });
        Assert.Equal(2, regex.Count("😀ab ab"u8, 4));
        Assert.Equal(1, regex.Count("😀ab ab"u8, 5));
        Assert.Equal(0, regex.Count("😀ab ab"u8, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀ab ab"u8, 1));
        Assert.Equal(1, regex.Count("ab ab"u8, 0, Pcre2MatchOptions.Anchored));
        var malformed = "ab ab"u8.ToArray(); malformed[^1] = 0xff;
        var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(malformed));
        Assert.Equal(expected.Message, Assert.Throws<ArgumentException>(() => regex.Count(malformed)).Message);
        var tinyLiteral = Create("(a)(b)", new() { HeapLimitInBytes = 1 });
        Assert.Equal(1, tinyLiteral.Count("ab"u8)); // No major scratch is acquired.
        var tiny = Create("((ab)|(a))", new() { HeapLimitInBytes = 1 });
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() => tiny.Count("ab a"u8)).ErrorKind);
        var depth = Create("(?:(a|aa)+)b", new() { DepthLimit = 1 });
        Assert.Equal(Pcre2ErrorKind.DepthLimit,
            Assert.Throws<Pcre2MatchException>(() => depth.Count("aaaab"u8)).ErrorKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CountKeepsOriginalInterruptionAndRetainedDetailedCaptures(bool cancel)
    {
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(a)(b)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, TimeSpan.FromMilliseconds(20));
        var retained = regex.MatchDetailed("ab"u8);
        int reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == 5 && cancel) token.Cancel();
            return reads >= 5 && !cancel ? 25 : 0;
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 3000)));
        if (cancel)
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(
                () => regex.Count(input, 0, Pcre2MatchOptions.None, token.Token)).CancellationToken);
        else
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(
                () => regex.Count(input)).ErrorKind);
        Assert.Equal("a", retained.GetGroup(1).GetValueString());
        Assert.Equal("b", retained.GetGroup(2).GetValueString());
        Assert.Equal(1, regex.Count("ab"u8));
    }

    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits limits) =>
        new(pattern, Pcre2CompileOptions.None, default, limits, Timeout.InfiniteTimeSpan);
}
