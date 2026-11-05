using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteLiteralFamilyCountTests
{
    private static readonly string[] Literals = ["cat", "horse", "dog", "pig", "cow", "duck", "eagle", "fox"];

    private static Utf8ResourcePolicy Generous => new()
    {
        MaxWorkSteps = 1_000_000, MaxDepth = 128,
        MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
    };

    [Theory]
    [InlineData("cat|horse", "π😀cat horse cat", 6, 3)]
    [InlineData("cat|horse|dog", "π😀cat horse dog cat", 10, 3)]
    [InlineData("a|aa", "π😀aaaa", 6, 4)]
    [InlineData("aa|a", "π😀aaaa", 6, 2)]
    [InlineData("é|😀", "πé😀é", 2, 3)]
    [InlineData("cat|horse", "π😀nothing", 6, 0)]
    [InlineData("cat|horse", "", 0, 0)]
    public void CountKeepsIndependentScalarAndOrderedOutcomes(string pattern, string text, int start, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, Generous })
        {
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
            using var cancellation = new CancellationTokenSource();
            Assert.Equal(expected, regex.Count(input, start));
            Assert.Equal(expected, regex.Count(input, start, cancellation.Token));
            Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 0xFF }).ToArray(), start));
            Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(input, input.Length + 1));
            if (input.Length > 1) Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(input, 1));
        }
    }

    [Theory]
    [InlineData("(cat)|(horse)", "cat horse", 2)]
    [InlineData("a|", "aa", 3)]
    [InlineData("(?:a?)+b", "aaab", 1)]
    [InlineData(@"\b|\w+", "é skip y", 9)]
    [InlineData(@"(?P<x>é)-(?P=x)", "é-é é-É", 1)]
    [InlineData(@"(?i:cat)|horse", "CAT horse", 2)]
    [InlineData("cat|horse|dog|pig", "cat horse dog pig", 4)]
    public void OtherProfileRoutesRetainTheirCountSemantics(string pattern, string text, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, Generous);
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text), 0, cancellation.Token));
    }

    [Fact]
    public void PureCountNeedsNeitherResultPayloadNorVmStorageOrDepth()
    {
        var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 10_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes("π😀" + new string('x', 4096) + " cat horse cat");
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(3, regex.Count(input, 6, cancellation.Token));
        Assert.Equal(0, regex.Count(input, input.Length));
    }

    [Theory]
    [InlineData(4, false)] [InlineData(5, false)] [InlineData(6, false)]
    [InlineData(7, false)] [InlineData(8, false)]
    [InlineData(4, true)] [InlineData(5, true)] [InlineData(6, true)]
    [InlineData(7, true)] [InlineData(8, true)]
    public void FourToEightLiteralCountsDiscardCaptureStorageAndKeepWholeSubjectValidation(int width, bool captured)
    {
        var pattern = string.Join('|', Literals.Take(width));
        if (captured) pattern = "(" + pattern + ")";
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes("π😀" + string.Join(' ', Literals.Take(width)) + " cat");
        using var token = new CancellationTokenSource();
        Assert.Equal(width + 1, regex.Count(input, 6));
        Assert.Equal(width + 1, regex.Count(input, 6, token.Token));
        Assert.Equal(0, regex.Count(input, input.Length, token.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(input, 1));
        Assert.Contains($"byte offset {input.Length}.", Assert.Throws<ArgumentException>(() =>
            regex.Count([.. input, 0xFF], input.Length, token.Token)).Message);
        input[0] = 0xFF;
        Assert.Contains("byte offset 0.", Assert.Throws<ArgumentException>(() => regex.Count(input, 6)).Message);
    }

    [Fact]
    public void BorrowedEightLiteralCountRetainsParentLoanAndDiscoveryEligibility()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex(string.Join('|', Literals));
        Assert.True(core.CanCountNativeLiteralForResult);
        Assert.False(core.CanFindNativeLiteralForResult);
        var invocation = new Utf8SliceInvocation(new(1_000_000, 1, 8, 1), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        try
        {
            Assert.Equal(8, core.CountForResult(Encoding.UTF8.GetBytes(string.Join(' ', Literals)), 0, ref invocation));
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
        finally { invocation.Ledger.Release(7); }
    }

    [Fact]
    public void LongLiteralWindowsAndLaterCapturedResultsKeepTheirSemantics()
    {
        var longLiterals = Enumerable.Range(0, 5).Select(i => (char)('a' + i) + new string('x', 1025) + "z").ToArray();
        var longRegex = new Utf8PythonRegex(string.Join('|', longLiterals), PythonReCompileOptions.None,
            Regex.InfiniteMatchTimeout, new() { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(2, longRegex.Count(Encoding.UTF8.GetBytes("😀" + longLiterals[4] + " " + longLiterals[0]), 4));
        var captured = new Utf8PythonRegex("(cat|horse|dog|pig|cow)", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, Generous);
        var input = "π😀cat cow"u8.ToArray();
        var retained = captured.SearchDetailedData(input);
        Assert.Equal(2, captured.Count(input));
        var matches = captured.FindIterDetailed(input);
        Assert.Equal(new[] { "cat", "cow" }, matches.Select(match => match.Groups[1].ValueText));
        Assert.Equal("cat", retained.Groups[1].ValueText);
        Assert.Equal((6, 3), (retained.Value.StartOffsetInBytes, retained.Value.StartOffsetInUtf16));
    }

    [Fact]
    public void NinthAlternativeStillUsesAdmittedScalarExecution()
    {
        var pattern = string.Join('|', Literals) + "|wolf";
        var input = Encoding.UTF8.GetBytes(string.Join(' ', Literals) + " wolf");
        var bounded = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => bounded.Count(input)).ResourceKind);
        Assert.Equal(9, new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, Generous).Count(input));
    }

    [Theory]
    [InlineData(2)] [InlineData(5)] [InlineData(8)]
    public void WorkDenialIsPublicCumulativeAndFreshOnReuse(int width)
    {
        var pattern = string.Join('|', Literals.Take(width));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(string.Join(' ', Literals.Take(width)) + " ", 128)));
        var expected = width * 128;
        Utf8PythonRegex Create(ulong cap) => new(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            Generous with { MaxWorkSteps = cap });
        void Denied(ulong cap)
        {
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(cap).Count(input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
            Assert.Equal(cap, error.Limit); Assert.True(error.Requested > cap);
        }
        Denied(1);
        ulong low = 1, high = 100_000;
        while (high - low > 1)
        {
            var middle = low + (high - low) / 2;
            try { Assert.Equal(expected, Create(middle).Count(input)); high = middle; }
            catch (Utf8ResourceLimitExceededException error)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); low = middle; }
        }
        Denied(high - 1);
        var admitted = Create(high);
        Assert.Equal(expected, admitted.Count(input)); Assert.Equal(expected, admitted.Count(input));
    }

    [Fact]
    public void DeadlinePrecedesWholeValidationAndUsesPythonDiagnosticLimits()
    {
        var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1), Generous);
        var invalid = new byte[] { 0xFF, 99, 97, 116 };
        long reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => reads++ == 0 ? 0 : 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(invalid, 1));
        Assert.Empty(error.Input); Assert.Equal(regex.Pattern, error.Pattern);
        Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count(invalid, 1, cancellation.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 5)] [InlineData(true, 5)]
    [InlineData(false, 8)] [InlineData(true, 8)]
    public void NativeSearchKeepsOriginalInterruptionAfterValidation(bool expire, int width)
    {
        var regex = new Utf8PythonRegex(string.Join('|', Literals.Take(width)), PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20), Generous);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(string.Join(' ', Literals.Take(width)) + " ", 512)));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var validation = new Utf8SliceInvocation(new Utf8ExecutionLimits(1_000_000, 128, 1 << 20, 1 << 20),
            TimeSpan.FromMilliseconds(20), default);
        _ = Utf8Validation.Validate(input, ref validation);
        var validationSteps = validation.Budget.Steps;
        using var cancellation = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= validationSteps) return;
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        try
        {
            if (expire) Assert.Empty(Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, 0)).Input);
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Count(input, 0, cancellation.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered); ticks = 0;
        Assert.Equal(width * 512, regex.Count(input));
    }
}
