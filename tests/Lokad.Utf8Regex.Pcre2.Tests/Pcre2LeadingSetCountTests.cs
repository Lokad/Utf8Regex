using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LeadingSetCountTests
{
    [Theory]
    [InlineData("(a+)(b)", "😀aaabéab", 2)]
    [InlineData("([ab]+)(c)", "😀aabcébc", 2)]
    [InlineData(@"(a+)\1", "😀aaaaéaa", 2)]
    [InlineData("(a+)(?=b)b", "😀aaabéab", 2)]
    public void AdmittedLeadingSetsPreserveVerificationAndScalarOffsets(string pattern, string text, int expected)
    {
        var regex = Create(pattern);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.Equal(Pcre2CandidateSearchKind.LeadingAsciiSet, regex.DebugCompiledProgram.CandidateSearch.Kind);
        var padded = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = padded.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expected, regex.Count(input, 4, Pcre2MatchOptions.None, token.Token));
        Assert.Equal(0, regex.Count(input, input.Length));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(Encoding.UTF8.GetBytes(text), 1));
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { (byte)'x', 255 }));
    }

    [Theory]
    [InlineData("(a*)")]
    [InlineData("([ab]*)")]
    [InlineData("(a)?")]
    public void NullableLanguagesKeepTheirEmptyMatchesWithoutARequiredLeadingByte(string pattern)
    {
        var regex = Create(pattern);
        Assert.Equal(Pcre2CandidateSearchKind.None, regex.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(3, regex.Count("xx"u8));
        Assert.True(regex.IsMatch("xx"u8));
        Assert.Equal(string.Empty, regex.MatchDetailed("xx"u8).GetGroup(0).GetValueString());
        var matches = regex.EnumerateMatches("xx"u8);
        var count = 0;
        while (matches.MoveNext()) count++;
        Assert.Equal(3, count);
    }

    [Fact]
    public void FlagsAssertionsAndResetKeepTheirExistingExclusions()
    {
        foreach (var pattern in new[] { "(?i)(a+)(b)", "(?<=a)(b+)", @"(a+)\Kb", "(?:(a+)|b+)(?(1)c|d)" })
            Assert.Equal(Pcre2CandidateSearchKind.None, Create(pattern).DebugCompiledProgram.CandidateSearch.Kind);
        var regex = Create("(a+)(b)");
        Assert.Equal(1, regex.Count("xxab"u8));
        Assert.Equal(0, regex.Count("xxab"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(0, regex.Count("ab😀"u8, 2));
        var anchored = new Utf8Pcre2Regex("(a+)(b)", Pcre2CompileOptions.Anchored);
        Assert.Equal(Pcre2CandidateSearchKind.None, anchored.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(0, anchored.Count("xxab"u8));
    }

    [Theory]
    [InlineData("(a+)(b)", 'x', "aaab")]
    [InlineData("[a-z]+@[a-z]+", 'a', "foo@bar")]
    public void ImpossibleSubjectsNeedNoVmScratchWhileRealAttemptsStillAdmitIt(string pattern, char filler, string hit)
    {
        var regex = Create(pattern, working: 1, result: 1);
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        var bytes = Encoding.UTF8.GetBytes(new string(filler, 65536));
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(bytes, ref invocation, out var input));
        var validation = invocation.Budget.Steps;
        Assert.True(Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
            new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
        Assert.Equal(0, count);
        Assert.True(invocation.Budget.Steps > validation);
        Assert.Equal(0UL, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.Count(Encoding.UTF8.GetBytes(hit))).ErrorKind);
    }

    [Theory]
    [InlineData("(a+)(b)", 'x')]
    [InlineData("[a-z]+@[a-z]+", 'a')]
    public void ValidationAndAbsenceSearchSpendOneStickyWorkAllowance(string pattern, char filler)
    {
        var bytes = Encoding.UTF8.GetBytes(new string(filler, 262144));
        var regex = Create(pattern);
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(bytes, ref invocation, out var input));
        var validation = invocation.Budget.Steps;
        Assert.True(Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
            new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
        Assert.Equal(0, count);
        var search = invocation.Budget.Steps - validation;
        Assert.True(validation > 0 && search > 0);
        var limit = checked((uint)Math.Max(validation, search));
        var denied = Create(pattern, work: limit);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => denied.Count(bytes)).ErrorKind);
    }

    [Theory]
    [InlineData(false, "(a+)(b)", "aaab", "aaa", "b")]
    [InlineData(true, "(a+)(b)", "aaab", "aaa", "b")]
    [InlineData(false, "[a-z]+@([a-z]+)", "foo@bar", "bar", null)]
    [InlineData(true, "[a-z]+@([a-z]+)", "foo@bar", "bar", null)]
    public void AbsenceSearchKeepsOriginalInterruptionAndRetainedCaptures(bool cancel, string pattern,
        string hit, string firstCapture, string? secondCapture)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, TimeSpan.FromMilliseconds(20));
        var retained = regex.MatchDetailed(Encoding.UTF8.GetBytes(hit));
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, token.Token);
        var bytes = Encoding.UTF8.GetBytes(new string('x', 262144));
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(bytes, ref invocation, out var input));
        var validation = invocation.Budget.Steps;
        var checkpoints = 0;
        invocation.CheckpointHook = _ => { checkpoints++; if (cancel) token.Cancel(); else ticks = 25; };
        Exception? failure = null;
        try
        {
            Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out _);
        }
        catch (Exception error) { failure = error; }
        Assert.True(checkpoints > 0 && invocation.Budget.Steps > validation);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(0UL, invocation.Budget.PeakWorkingBytes);
        if (cancel) Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        Assert.Equal(firstCapture, retained.GetGroup(1).GetValueString());
        if (secondCapture is not null) Assert.Equal(secondCapture, retained.GetGroup(2).GetValueString());
    }

    [Fact]
    public void RequiredLiteralMustOccurInTheRequestedSuffixAndStillNeedsVerification()
    {
        var regex = Create("[a-z]+@([a-z]+)");
        Assert.Equal(Pcre2CandidateSearchKind.LeadingRunThenLiteral, regex.DebugCompiledProgram.CandidateSearch.Kind);
        var padded = Encoding.UTF8.GetBytes("pad😀@bad foo@bar λaaa@bbbpad");
        var subject = padded.AsSpan(3, padded.Length - 6);
        Assert.Equal(2, regex.Count(subject));
        Assert.Equal(1, regex.Count(subject, Encoding.UTF8.GetByteCount("😀@bad foo@bar λ")));
        Assert.Equal(0, regex.Count("foo@bar"u8, 4));
        Assert.Equal(0, regex.Count("@bad aa@!"u8));
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { (byte)'a', 0xFF }));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀foo@bar"u8, 1));
    }

    private static Utf8Pcre2Regex Create(string pattern, ulong working = 1 << 20,
        ulong result = 1 << 20, uint work = 1000000) =>
        new(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = work, DepthLimit = 256, HeapLimitInBytes = working,
                ResultLimitInBytes = result }, Timeout.InfiniteTimeSpan);
}
