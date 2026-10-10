using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarCandidateVerificationTests
{
    private static Utf8VerifierRuntime Runtime(string pattern, RegexOptions options,
        bool endCoverage, bool trailingCoverage)
    {
        var prepared = Utf8VerifierTestPlans.WithScalarVerifier(Utf8FrontEnd.Compile(pattern, options),
            new(endCoverage, trailingCoverage));
        return Utf8VerifierRuntime.Create(prepared);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CandidateCoveragePlansUseTheCompleteScalarSubjectAndCachedProgram(int coverage)
    {
        var runtime = Runtime(@"(?<=é)(?<x>.)\k<x>(?=z)", RegexOptions.CultureInvariant, coverage != 0, coverage == 2);
        var input = "é😀😀z"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        using var construction = Utf8ConstructionScope.Enter();
        var checkpoint = construction.Budget.ObjectCheckpoint;
        var before = construction.Budget.ReservedBytes;
        var candidate = coverage == 2
            ? Utf8StructuralCandidate.Window(2, 10, 8, 0, 6, 4, 0)
            : Utf8StructuralCandidate.Start(2, 10, 8, 0);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Equal(Utf8ExecutionLimitKind.None, runtime.TryVerifyCandidate(input,
                candidate, validation, ref invocation, out var result));
            Assert.True(result.Success);
            Assert.Equal((2, 8), (result.IndexInBytes, result.LengthInBytes));
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8ProjectionExecutor.TryProjectScalarVerification(input,
                validation, result, ref invocation, out var projected));
            Assert.True(projected.IsByteAligned);
            Assert.Equal((1, 4), (projected.IndexInUtf16, projected.LengthInUtf16));
            Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
        }
        Assert.Equal(Utf8ExecutionLimitKind.None, runtime.TryVerifyCandidate(input,
            Utf8StructuralCandidate.Start(3, 10, 7, 0), validation, ref invocation, out var splitStart));
        Assert.False(splitStart.Success);
        Assert.Equal(before, construction.Budget.ReservedBytes);
        Assert.Equal(0ul, construction.Budget.WorkSteps);
        Assert.Same(checkpoint, construction.Budget.ObjectCheckpoint);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(runtime);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(runtime);
    }

    [Theory]
    [InlineData(".", "😀z", 0, 1, 4, 2, false, false, true)]
    [InlineData(".{2}", "😀z", 0, 5, 5, 3, false, false, true)]
    [InlineData("[^\\n]+", "😀é\n", 0, 6, 6, 3, false, false, true)]
    [InlineData("(?=(?<x>😀))\\k<x>", "😀z", 0, 4, 4, 2, false, false, true)]
    [InlineData("(?<x>.)\\k<x>", "é😀😀z", 2, 10, 8, 4, true, false, false)]
    [InlineData("(?<x>.){2}", "😀𐐀", 0, 8, 8, 4, false, true, true)]
    [InlineData("\\k<x>(?<x>.)", "é😀😀z", 2, 10, 8, 4, true, false, true)]
    public void ScalarClassesReferencesRtlAndNonBacktrackingKeepByteExtents(string pattern, string text,
        int start, int end, int bytes, int units, bool rtl, bool nonBacktracking, bool expected)
    {
        var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0) |
            (nonBacktracking ? RegexOptions.NonBacktracking : 0);
        var runtime = Runtime(pattern, options, true, false);
        var input = Encoding.UTF8.GetBytes(text);
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, runtime.TryVerifyCandidate(input,
            Utf8StructuralCandidate.Start(start, end, bytes, 0), validation, ref invocation, out var result));
        Assert.Equal(expected, result.Success);
        if (expected)
        {
            Assert.Equal((start, bytes), (result.IndexInBytes, result.LengthInBytes));
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8ProjectionExecutor.TryProjectScalarVerification(input,
                validation, result, ref invocation, out var projected));
            Assert.True(projected.IsByteAligned); Assert.Equal(units, projected.LengthInUtf16);
        }
        Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(runtime);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CandidateCoverageDoesNotReplaceTheScalarMatchEnd(bool requireEnd)
    {
        var runtime = Runtime(".", RegexOptions.CultureInvariant, requireEnd, false);
        var input = "😀😀"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, runtime.TryVerifyCandidate(input,
            Utf8StructuralCandidate.Start(0, 8, 8, 0), validation, ref invocation, out var result));
        Assert.Equal(!requireEnd, result.Success);
        if (result.Success) Assert.Equal(4, result.LengthInBytes);
        Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(4, 1, true)] [InlineData(1, 1, false)] [InlineData(4, 2, false)]
    public void TrailingCoverageUsesWholeScalarByteBounds(int trailingStart, int trailingLength, bool expected)
    {
        var runtime = Runtime("😀z?", RegexOptions.CultureInvariant, true, true);
        var input = "😀z"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, runtime.TryVerifyCandidate(input,
            Utf8StructuralCandidate.Window(0, 4, 4, 0, trailingStart, trailingLength, 0), validation, ref invocation, out var result));
        Assert.Equal(expected, result.Success);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(4, true)]
    public void ContiguousAnchorsBorrowTheCallerOrigin(int origin, bool expected)
    {
        var runtime = Runtime(@"\G.", RegexOptions.CultureInvariant, false, false);
        var input = "😀z"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        invocation.AnchorOriginByte = origin;
        Assert.Equal(Utf8ExecutionLimitKind.None, runtime.TryVerifyCandidate(input,
            Utf8StructuralCandidate.Start(4, -1, 1, 0), validation, ref invocation, out var result));
        Assert.Equal(expected, result.Success); Assert.Equal(origin, invocation.AnchorOriginByte);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DenialRestoresTheParentLedgerAndKeepsSpentWork(bool bytes)
    {
        var runtime = Runtime("(?<x>a)+", RegexOptions.CultureInvariant, true, false);
        var input = "aaaa"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(new(bytes ? 0ul : 1ul, 0, bytes ? 8ul : 0ul), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var kind = bytes ? Utf8ExecutionLimitKind.WorkingBytes : Utf8ExecutionLimitKind.WorkSteps;
        Assert.Equal(kind, runtime.TryVerifyCandidate(input, Utf8StructuralCandidate.Start(0, 4, 4, 0),
            validation, ref invocation, out var result));
        Assert.False(result.Success); Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
        Assert.True(invocation.Budget.Steps > 0);
        var recovery = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, runtime.TryVerifyCandidate(input, Utf8StructuralCandidate.Start(0, 4, 4, 0),
            validation, ref recovery, out result));
        Assert.True(result.Success); Assert.Equal(0ul, recovery.Ledger.ReservedBytes);
    }

    [Fact]
    public void CancellationDuringVmWorkReleasesScratchAndKeepsTheOriginalToken()
    {
        var runtime = Runtime("(?<x>a+)b", RegexOptions.CultureInvariant, true, false);
        var input = Encoding.UTF8.GetBytes(new string('a', 2048) + "b");
        var validation = Utf8Validation.Validate(input);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var entered = false;
        invocation.CheckpointHook = steps => { if (steps >= 64) { entered = true; source.Cancel(); } };
        OperationCanceledException? failure = null;
        try { _ = runtime.TryVerifyCandidate(input, Utf8StructuralCandidate.Start(0, input.Length, input.Length, 0), validation, ref invocation, out _); }
        catch (OperationCanceledException cancelled) { failure = cancelled; }
        Assert.True(entered); Assert.NotNull(failure); Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void LaterCandidatesCannotResetTheSharedWorkOwner()
    {
        var runtime = Runtime("a", RegexOptions.CultureInvariant, true, false);
        var validation = Utf8Validation.Validate("a"u8);
        var funded = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        var candidate = Utf8StructuralCandidate.Start(0, 1, 1, 0);
        Assert.Equal(Utf8ExecutionLimitKind.None, runtime.TryVerifyCandidate("a"u8, candidate, validation, ref funded, out var result));
        Assert.True(result.Success);
        var limited = new Utf8SliceInvocation(new(funded.Budget.Steps, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, runtime.TryVerifyCandidate("a"u8, candidate, validation, ref limited, out result));
        Assert.True(result.Success);
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, runtime.TryVerifyCandidate("a"u8, candidate, validation, ref limited, out result));
        Assert.False(result.Success); Assert.True(limited.Budget.Steps > funded.Budget.Steps);
        Assert.Equal(0ul, limited.Ledger.ReservedBytes);
    }

    [Fact]
    public void VerificationChecksTheOriginalEntryDeadlineAfterADeferredAttempt()
    {
        var runtime = Runtime("a", RegexOptions.CultureInvariant, true, false);
        var validation = Utf8Validation.Validate("a"u8);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, TimeSpan.FromMilliseconds(1), default);
        Thread.Sleep(20);
        Utf8ExecutionDeadlineExpiredException? failure = null;
        try { _ = runtime.TryVerifyCandidate("a"u8, Utf8StructuralCandidate.Start(0, 1, 1, 0), validation, ref invocation, out _); }
        catch (Utf8ExecutionDeadlineExpiredException expired) { failure = expired; }
        Assert.NotNull(failure); Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("foo(?<x>.)\\k<x>", false)]
    [InlineData("foo(?<x>.)\\k<x>", true)]
    [InlineData("foo(?<x>.)(?(x)\\k<x>|bar)", false)]
    [InlineData("foo(?<x>.)(?(x)\\k<x>|bar)", true)]
    [InlineData("[A-Z](?<x>.)\\k<x>", false)]
    [InlineData("[A-Z](?<x>.)\\k<x>", true)]
    public void CandidateRuntimeCountStaysScalarBeyondTheFormerDemotionThreshold(string pattern, bool emitted)
    {
        const RegexOptions options = RegexOptions.CultureInvariant;
        var prepared = Utf8FrontEnd.Compile(pattern, options);
        var verifier = Utf8VerifierRuntime.Create(prepared);
        var token = pattern.StartsWith("[A-Z]", StringComparison.Ordinal) ? "A😀😀 " : "foo😀😀 ";
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(token, 40)));
        var validation = Utf8Validation.Validate(input);
        Assert.False(prepared.SearchPlan.FirstMatchOperation.Projection.HasValue);
        if (pattern.StartsWith("[A-Z]", StringComparison.Ordinal))
        {
            Assert.False(prepared.SearchPlan.FallbackSearch.HasCandidates);
            var regex = new Utf8Regex(pattern, options | (emitted ? RegexOptions.Compiled : 0));
            Assert.Equal(40, regex.Count(input)); Assert.True(regex.IsMatch(input));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
        else
        {
            Assert.True(prepared.SearchPlan.FallbackSearch.HasCandidates);
            var runtime = Utf8CompiledRuntimeFactory.Create(
                new(Utf8CompiledEngineKind.SearchGuidedFallback, emitted ? Utf8CompiledExecutionBackend.EmittedInstruction : Utf8CompiledExecutionBackend.InterpretedInstruction),
                prepared, verifier, options);
            Assert.Equal(40, runtime.Count(input, validation, Utf8ExecutionDeadline.Infinite));
            Assert.True(runtime.IsMatch(input, validation, Utf8ExecutionDeadline.Infinite));
        }
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
    }

    [Fact]
    public void CompiledCandidateRuntimeProjectsScalarsWithoutItsFormerBclMatchPath()
    {
        const string pattern = @"foo(?<x>.)\k<x>";
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var verifier = Utf8VerifierRuntime.Create(prepared);
        var runtime = Utf8CompiledFallbackCompiledEngineRuntime.Create(prepared, verifier);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("foo😀😀 ", 40)));
        var validation = Utf8Validation.Validate(input);
        Assert.True(runtime.IsMatch(input, validation, Utf8ExecutionDeadline.Infinite));
        Assert.Equal(40, runtime.Count(input, validation, Utf8ExecutionDeadline.Infinite));
        var match = runtime.Match(input, validation, Utf8ExecutionDeadline.Infinite);
        Assert.True(match.Success); Assert.True(match.IsByteAligned);
        Assert.Equal((0, 11, 0, 7), (match.IndexInBytes, match.LengthInBytes, match.IndexInUtf16, match.LengthInUtf16));
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
    }

    [Fact]
    public void EmittedWindowPortfolioUsesOneScalarInvocationWithoutDecodedSubjects()
    {
        const string pattern = @"foo(?<x>.)\k<x>bar";
        var search = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.None, new Utf8SearchFactData
        {
            RequiredWindowPrefilters = [Utf8WindowSearchFacts.WithinLines(
                Utf8PreparedSearcherInfo.LiteralFamily(["foo"u8.ToArray()]),
                Utf8PreparedSearcherInfo.LiteralFamily(["bar"u8.ToArray()]), 1)],
            MinRequiredLength = 8,
        }));
        var prepared = Utf8VerifierTestPlans.WithSearchPlan(Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant), search);
        prepared = Utf8VerifierTestPlans.WithScalarVerifier(prepared, new(true, true));
        Assert.True(prepared.SearchPlan.FallbackSearch.HasCandidates);
        Assert.True(Utf8CompiledBackendCapability.CanUseEmittedSearchGuidedFallback(prepared));
        var verifier = Utf8VerifierRuntime.Create(prepared);
        Assert.True(Utf8EmittedSearchGuidedFallback.TryCreate(prepared, verifier, out var backend));
        Assert.NotNull(backend);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("foo😀😀bar\n", 40)));
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.Equal(40, backend.Count(input, validation, ref invocation));
        var spent = invocation.Budget.Steps;
        invocation.AnchorOriginByte = 0;
        Assert.True(backend.IsMatch(input, validation, ref invocation));
        Assert.True(invocation.Budget.Steps > spent); Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
    }
}
