using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteLiteralFamilyCountTests
{
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

    [Fact]
    public void WorkDenialIsPublicCumulativeAndFreshOnReuse()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("cat horse ", 128)));
        Utf8PythonRegex Create(ulong cap) => new("cat|horse", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
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
            try { Assert.Equal(256, Create(middle).Count(input)); high = middle; }
            catch (Utf8ResourceLimitExceededException error)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); low = middle; }
        }
        Denied(high - 1);
        var admitted = Create(high);
        Assert.Equal(256, admitted.Count(input)); Assert.Equal(256, admitted.Count(input));
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
    [InlineData(false)] [InlineData(true)]
    public void NativeSearchKeepsOriginalInterruptionAfterValidation(bool expire)
    {
        var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20), Generous);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("cat horse ", 512)));
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
        Assert.Equal(1024, regex.Count(input));
    }
}
