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
        Assert.True(regex.IsMatch(input));
        Assert.True(regex.IsMatch(input, 4, Pcre2MatchOptions.None, token.Token));
        Assert.False(regex.IsMatch(input, input.Length));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(Encoding.UTF8.GetBytes(text), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(Encoding.UTF8.GetBytes(text), 1));
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
        ReadOnlySpan<byte> result1Input = "xx"u8;
        var result1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var result1Result = regex.Match(result1Input, captures: result1Captures);
        Assert.Equal(string.Empty, System.Text.Encoding.UTF8.GetString((result1Captures[0].Success ? result1Input.Slice(result1Captures[0].StartOffsetInBytes, result1Captures[0].EndOffsetInBytes - result1Captures[0].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
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
        Assert.True(regex.IsMatch("xxab"u8));
        Assert.False(regex.IsMatch("xxab"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.False(regex.IsMatch("ab😀"u8, 2));
        var anchored = new Utf8Pcre2Regex("(a+)(b)", options: Pcre2CompileOptions.Anchored);
        Assert.Equal(Pcre2CandidateSearchKind.None, anchored.DebugCompiledProgram.CandidateSearch.Kind);
        Assert.Equal(0, anchored.Count("xxab"u8));
    }

    [Theory]
    [InlineData("(a+)(b)", 'x', "aaab", true, false)]
    [InlineData("[a-z]+@[a-z]+", 'a', "foo@bar", false, false)]
    [InlineData(@"[^a]+\.[^z]+", 'b', "ébc.😀tail", true, true)]
    public void ImpossibleSubjectsNeedNoVmScratchWhileRealAttemptsStillAdmitIt(
        string pattern, char filler, string hit, bool countNeedsStorage, bool matchNeedsStorage)
    {
        var regex = Create(pattern, working: 1, result: 1);
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        var bytes = Encoding.UTF8.GetBytes(new string(filler, 65536));
        var input = Pcre2Runner.PrepareInput(bytes, 0, ref invocation);
        var validation = invocation.Budget.Steps;
        Assert.True(Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
            new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
        Assert.Equal(0, count);
        Assert.True(invocation.Budget.Steps > validation);
        Assert.Equal(0UL, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        if (countNeedsStorage)
            Assert.Equal(Pcre2ErrorKind.HeapLimit,
                Assert.Throws<Pcre2MatchException>(() => regex.Count(Encoding.UTF8.GetBytes(hit))).ErrorKind);
        else Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes(hit)));

        var budget = Pcre2ResourceBudget.CreateExclusive(regex.DebugCompiledProgram.Request, default);
        ref var matchInvocation = ref budget.WorkingInvocation;
        input = Pcre2Runner.PrepareInput(bytes, 0, ref matchInvocation);
        validation = matchInvocation.Budget.Steps;
        Assert.True(Pcre2Runner.TryIsMatch(regex.DebugCompiledProgram, ref input,
            new Utf8BytePosition(0), Pcre2MatchOptions.None, ref budget, out var found));
        Assert.False(found);
        Assert.True(matchInvocation.Budget.Steps > validation);
        Assert.Equal(0UL, matchInvocation.Budget.PeakWorkingBytes);
        Assert.Equal(0UL, matchInvocation.Ledger.ReservedBytes);
        Assert.False(regex.IsMatch(bytes));
        if (matchNeedsStorage)
            Assert.Equal(Pcre2ErrorKind.HeapLimit,
                Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(Encoding.UTF8.GetBytes(hit))).ErrorKind);
        else Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(hit)));
    }

    [Theory]
    [InlineData("(a+)(b)", 'x')]
    [InlineData("[a-z]+@[a-z]+", 'a')]
    [InlineData(@"[^a]+\.[^z]+", 'b')]
    public void ValidationAndAbsenceSearchSpendOneStickyWorkAllowance(string pattern, char filler)
    {
        var bytes = Encoding.UTF8.GetBytes(new string(filler, 262144));
        var regex = Create(pattern);
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        var input = Pcre2Runner.PrepareInput(bytes, 0, ref invocation);
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
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => denied.IsMatch(bytes)).ErrorKind);
    }

    [Theory]
    [InlineData(false, false, "(a+)(b)", "aaab", "aaa", "b")]
    [InlineData(true, false, "(a+)(b)", "aaab", "aaa", "b")]
    [InlineData(false, false, "[a-z]+@([a-z]+)", "foo@bar", "bar", null)]
    [InlineData(true, false, "[a-z]+@([a-z]+)", "foo@bar", "bar", null)]
    [InlineData(false, true, "(a+)(b)", "aaab", "aaa", "b")]
    [InlineData(true, true, "(a+)(b)", "aaab", "aaa", "b")]
    [InlineData(false, true, "[a-z]+@([a-z]+)", "foo@bar", "bar", null)]
    [InlineData(true, true, "[a-z]+@([a-z]+)", "foo@bar", "bar", null)]
    public void AbsenceSearchKeepsOriginalInterruptionAndRetainedCaptures(bool cancel, bool isMatch, string pattern,
        string hit, string firstCapture, string? secondCapture)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        ReadOnlySpan<byte> retained2Input = Encoding.UTF8.GetBytes(hit);
        var retained2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained2Input, captures: retained2Captures);
        var request = regex.DebugCompiledProgram.Request;
        var budget = isMatch ? Pcre2ResourceBudget.CreateExclusive(request, token.Token)
            : new Pcre2ResourceBudget(request.DefaultLimits, Pcre2SliceDriver.CreateInvocation(request, token.Token), false);
        ref var invocation = ref budget.WorkingInvocation;
        var bytes = Encoding.UTF8.GetBytes(new string('x', 262144));
        var input = Pcre2Runner.PrepareInput(bytes, 0, ref invocation);
        var validation = invocation.Budget.Steps;
        var checkpoints = 0;
        invocation.CheckpointHook = _ => { checkpoints++; if (cancel) token.Cancel(); else ticks = 25; };
        Exception? failure = null;
        try
        {
            if (isMatch)
                Pcre2Runner.TryIsMatch(regex.DebugCompiledProgram, ref input,
                    new Utf8BytePosition(0), Pcre2MatchOptions.None, ref budget, out _);
            else
                Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                    new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out _);
        }
        catch (Exception error) { failure = error; }
        Assert.True(checkpoints > 0 && invocation.Budget.Steps > validation);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(0UL, invocation.Budget.PeakWorkingBytes);
        if (cancel) Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        Assert.Equal(firstCapture, System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        if (secondCapture is not null) Assert.Equal(secondCapture, System.Text.Encoding.UTF8.GetString((retained2Captures[2].Success ? retained2Input.Slice(retained2Captures[2].StartOffsetInBytes, retained2Captures[2].EndOffsetInBytes - retained2Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
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
        Assert.True(regex.IsMatch(subject));
        Assert.False(regex.IsMatch("foo@bar"u8, 4));
        Assert.False(regex.IsMatch("@bad aa@!"u8));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀foo@bar"u8, 1));
    }

    private static Utf8Pcre2Regex Create(string pattern, ulong working = 1 << 20,
        ulong result = 1 << 20, uint work = 1000000) =>
        new(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = (work) == 0 ? null : (work),
                MaxDepth = 256,
                MaxWorkingBytes = (working) == 0 ? null : (working),
                MaxResultBytes = (result) == 0 ? null : (result)
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
}
