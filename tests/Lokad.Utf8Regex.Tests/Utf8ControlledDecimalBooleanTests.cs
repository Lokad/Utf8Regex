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
    [InlineData("", true)] [InlineData("+", true)] [InlineData("-", true)]
    [InlineData(".", true)] [InlineData("-.", true)] [InlineData("+.1", true)]
    [InlineData("12.", true)] [InlineData("-12.34\n", true)] [InlineData("\n", true)]
    [InlineData("12\n\n", false)] [InlineData("1.2.3", false)] [InlineData("x12", false)]
    [InlineData("+١٢.٣", true)] [InlineData("𝟙.٢", true)] [InlineData("1é", false)]
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
    [InlineData(31)] [InlineData(32)] [InlineData(33)]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    [InlineData(4095)] [InlineData(4096)] [InlineData(4097)]
    public void BorrowedOffsetsAndDigitTailsNeedNoVmScratch(int length)
    {
        var input = Encoding.ASCII.GetBytes("-" + new string('1', length) + "." + new string('2', length) + "\n");
        byte[] storage = [0xff, 0xff, 0xff, .. input, 0xff];
        using var source = new CancellationTokenSource();
        foreach (var compiled in new[] { false, true })
        {
            var regex = Create(compiled, 100000, 1, 1, 1);
            Assert.True(regex.IsMatch(storage.AsSpan(3, input.Length), source.Token));
            var error = Assert.Throws<ArgumentException>(() => regex.IsMatch(storage.AsSpan(3), source.Token));
            Assert.Contains($"byte offset {input.Length}.", error.Message);
            Assert.True(regex.IsMatch(input));
        }
    }

    [Fact]
    public void ValidationAndDigitDiscoveryShareOneWorkAllowance()
    {
        var input = Encoding.ASCII.GetBytes(new string('1', 16384));
        var invocation = new Utf8SliceInvocation(default, Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref invocation);
        var validationWork = invocation.Budget.Steps;
        var regex = Create(false, validationWork, 1, 1, 1);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(validationWork, error.Limit);
        Assert.True(error.Requested > error.Limit);
        Assert.True(Create(false, 1000, 1, 1, 1).IsMatch(input));
    }

    [Fact]
    public void FinalPublicationRequiresItsOwnAdmittedWorkAndOriginalAuthority()
    {
        var input = "12.34"u8.ToArray();
        var complete = new Utf8SliceInvocation(new(1000, 1, 1, 1), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None,
            Utf8AsciiPrefixTokenExecutor.TryMatchSignedDecimalWhole(input, ref complete, out var matched));
        Assert.True(matched);
        var denied = new Utf8SliceInvocation(new(complete.Budget.Steps - 1, 1, 1, 1),
            Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps,
            Utf8AsciiPrefixTokenExecutor.TryMatchSignedDecimalWhole(input, ref denied, out matched));
        Assert.False(matched);
    }

    [Theory]
    [InlineData(false, 512)] [InlineData(true, 512)]
    [InlineData(false, 62)] [InlineData(true, 62)]
    public void DiscoveryKeepsCancellationAndDeadlineAfterWholeInputValidation(bool deadline, int blocks)
    {
        // 62 full blocks place a diagnostic checkpoint at final publication;
        // 512 place it while more digit blocks remain.
        var input = Encoding.ASCII.GetBytes(new string('1', blocks * 2048));
        var probe = new Utf8SliceInvocation(default, Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref probe);
        var validationWork = probe.Budget.Steps;
        var regex = Create(false, 100000, 1, 1, 1);
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
            TimeSpan.FromSeconds(1), new() { MaxWorkSteps = work, MaxDepth = depth,
                MaxWorkingBytes = working, MaxResultBytes = result });
}
