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

    [Theory]
    [InlineData("-3.14159")]
    [InlineData("3.14159")]
    [InlineData("-314159")]
    [InlineData("-")]
    [InlineData(".\n")]
    public void DisjointTerminalOptionalsNeedNoRewindStorage(string text)
    {
        using var source = new CancellationTokenSource();
        using var regex = Create(false, 128, 1, 1, 1);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(text), source.Token));
    }

    [Fact]
    public void DigitDiscoverySpendsTheOriginalWorkAllowance()
    {
        var input = Encoding.ASCII.GetBytes(new string('1', 4096));
        const ulong workLimit = 1;
        var regex = Create(false, workLimit, 65536, 8 << 20, 1);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(workLimit, error.Limit);
        Assert.True(error.Requested > error.Limit);
        Assert.True(Create(false, 1000000, 65536, 8 << 20, 1).IsMatch(input));
    }

    [Theory]
    [InlineData(false, 128)]
    [InlineData(true, 128)]
    [InlineData(false, 80)]
    [InlineData(true, 80)]
    public void ScalarDiscoveryKeepsOriginalCancellationAndDeadline(bool deadline, int blocks)
    {
        // The diagnostic hook fires every 64 modeled steps. A long ASCII
        // digit run now charges one step per checked block.
        var input = Encoding.ASCII.GetBytes(new string('1', blocks * 2048));
        var regex = Create(false, 100000000, 65536, 8 << 20, 1);
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var triggered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (triggered) return;
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
        new(Pattern, options: RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None), policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1),
            ResourcePolicy = new()
            {
                MaxWorkSteps = work,
                MaxDepth = depth,
                MaxWorkingBytes = working,
                MaxResultBytes = result
            }
        });
}
