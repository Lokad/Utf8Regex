using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ControlledDecimalBooleanTests
{
    private const string Pattern = @"^[-+]?\d*\.?\d*$";

    [Theory]
    [InlineData("", true)]
    [InlineData("+", true)]
    [InlineData("-", true)]
    [InlineData(".", true)]
    [InlineData("-.", true)]
    [InlineData("+.1", true)]
    [InlineData("12.", true)]
    [InlineData("-12.34\n", true)]
    [InlineData("\n", true)]
    [InlineData("12\n\n", false)]
    [InlineData("1.2.3", false)]
    [InlineData("x12", false)]
    [InlineData("+١٢.٣", true)]
    [InlineData("𝟙.٢", true)]
    [InlineData("1é", false)]
    public void ControlledBooleanKeepsTheScalarGrammar(string text, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        foreach (var compiled in new[] { false, true })
        {
            var regex = Create(compiled, 100000, 256, 1 << 20, 1 << 20);
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
        }
    }

    [Fact]
    public void DigitDiscoverySpendsTheOriginalWorkAllowance()
    {
        var input = Encoding.ASCII.GetBytes(new string('1', 4096));
        var invocation = new Utf8SliceInvocation(default, Regex.InfiniteMatchTimeout, default);
        _ = Utf8InputAnalyzer.Describe(input, ref invocation);
        var validationWork = invocation.Budget.Steps;
        var regex = Create(false, validationWork, 65536, 8 << 20, 1);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(validationWork, error.Limit);
        Assert.True(error.Requested > error.Limit);
        Assert.True(Create(false, 1000000, 65536, 8 << 20, 1).IsMatch(input));
    }

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 8)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public void ScalarDiscoveryKeepsOriginalCancellationAndDeadline(bool deadline, int blocks)
    {
        // 62 full blocks place a diagnostic checkpoint at final publication;
        // 512 place it while more digit blocks remain.
        var input = Encoding.ASCII.GetBytes(new string('1', blocks * 2048));
        var probe = new Utf8SliceInvocation(default, Regex.InfiniteMatchTimeout, default);
        _ = Utf8InputAnalyzer.Describe(input, ref probe);
        var validationWork = probe.Budget.Steps;
        var regex = Create(false, 100000000, 65536, 8 << 20, 1);
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var triggered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(work =>
        {
            if (work <= validationWork || triggered) return;
            triggered = true;
            if (deadline) clock.Advance(2000); else source.Cancel();
        });
        try
        {
            if (deadline)
            {
                var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input, source.Token));
                Assert.Empty(error.Input);
            }
            else
            {
                var error = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, source.Token));
                Assert.Equal(source.Token, error.CancellationToken);
            }
            Assert.True(triggered);
        }
        finally { diagnostics.Complete(); }
        Assert.True(regex.IsMatch(input));
        source.Cancel();
        var canceled = Assert.Throws<OperationCanceledException>(() => regex.IsMatch([0xff], source.Token));
        Assert.Equal(source.Token, canceled.CancellationToken);
    }

    private static Utf8Regex Create(bool compiled, ulong work, uint depth, ulong working, ulong result) =>
        new(Pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None),
            TimeSpan.FromSeconds(1), new() { MaxWorkSteps = work, MaxDepth = depth, MaxWorkingBytes = working, MaxResultBytes = result });
}
