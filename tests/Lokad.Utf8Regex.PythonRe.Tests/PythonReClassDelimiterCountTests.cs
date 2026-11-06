using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReClassDelimiterCountTests
{
    [Theory]
    [InlineData(@"[\w.+-]+@[\w.-]+\.[\w.-]+")]
    [InlineData(@"\w+@")]
    [InlineData(@"[\w@]+@")]
    [InlineData(@"[^!]{1,4}@")]
    [InlineData(@"(?P<w>\w+)@(?P=w)")]
    [InlineData(@"[\w.]*@")]
    [InlineData(@"(?i)[a-z]+@")]
    [InlineData(@"\w+@(?<=a@)")]
    [InlineData(@"(?:\w+@|!)")]
    public void CountAgreesWithOriginalScalarDiscoveryAtEveryScalarFloor(string pattern)
    {
        var regex = Create(pattern);
        var input = Encoding.UTF8.GetBytes("😀é²𐐀._+-@dömain.exämple ab@b a@@a _\u0301@ !" + new string('!', 260));
        byte[] storage = [0xff, .. input, 0xff];
        using var token = new CancellationTokenSource();
        for (var floor = 0; floor <= input.Length;)
        {
            var expected = regex.FindIterDetailed(input, floor).Length;
            Assert.Equal(expected, regex.Count(storage.AsSpan(1, input.Length), floor));
            Assert.Equal(expected, regex.Count(input, floor, token.Token));
            if (floor == input.Length) break;
            Rune.DecodeFromUtf8(input.AsSpan(floor), out _, out var width);
            floor += width;
        }
        Assert.Equal(3, new Utf8PythonRegex("x*|y").Count("y"u8));
    }

    [Fact]
    public void StrictValidationAndWorkAdmissionRemainRequired()
    {
        var regex = Create(@"\w+@");
        var input = Encoding.UTF8.GetBytes(new string('!', 100000) + "é@");
        Assert.Equal(1, regex.Count(input));
        Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff]));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀é@"u8, 1));
        var working = new Utf8PythonRegex(@"\w+@", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            Policy with { MaxWorkingBytes = 1 });
        Assert.Equal(1, working.Count(input));
        Assert.Equal(0, working.Count(Encoding.UTF8.GetBytes(new string('!', 100000))));
        var work = new Utf8PythonRegex(@"\w+@", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            Policy with { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => work.Count([.. input, 0xff])).ResourceKind);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public void SearchRewindAndVerificationKeepOriginalInterruption(bool expire, int phase)
    {
        var pattern = phase == 2 ? @"\w+@(?:x|xy)z" : @"\w+@";
        var text = phase switch
        {
            0 => new string('!', 200000),
            1 => new string('é', 100000) + "@",
            _ => string.Concat(Enumerable.Repeat("word@xq ", 2000)),
        };
        var input = Encoding.UTF8.GetBytes(text);
        var calibration = new Utf8SliceInvocation(new(4_000_000, 4096, 16UL << 20), Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref calibration);
        var threshold = ((calibration.Budget.Steps / 64) + 1) * 64;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            var regex = Create(pattern, expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan);
            if (expire) Assert.IsType<RegexMatchTimeoutException>(Record.Exception(() => regex.Count(input, 0, token.Token)));
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, 0, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }

    private static Utf8ResourcePolicy Policy => new() { MaxWorkSteps = 4_000_000, MaxDepth = 4096,
        MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 };

    [Fact]
    public void FundedCountRefundsSuccessfulDeniedAndCanceledLoans()
    {
        var account = new PythonReSharedWorkingAllowanceTests.Allowance(16 << 20);
        Assert.True(account.TryReserve(13));
        using var regex = Utf8PythonRegex.Compile(@"[\w.+-]+@[\w.-]+\.[\w.-]+",
            PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, Policy, default,
            Timeout.InfiniteTimeSpan, default, account);
        var held = account.Used;
        var input = Encoding.UTF8.GetBytes(new string('!', 100000) + "é@d.e");
        Assert.Equal(1, regex.Count(input));
        Assert.Equal(held, account.Used);
        account.Maximum = held;
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.Count(input));
        Assert.Equal(held, account.Used);
        account.Maximum = 16 << 20;
        using var token = new CancellationTokenSource();
        account.AfterGrant = token.Cancel;
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, 0, token.Token)).CancellationToken);
        account.AfterGrant = null;
        Assert.Equal(held, account.Used);
        regex.Dispose();
        Assert.Equal(13UL, account.Used);
        account.Release(13);
    }

    [Fact]
    public void FinalDeadlineReadCannotPublishAfterCancelingTheOriginalToken()
    {
        var reads = 0;
        var cancelAt = int.MaxValue;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == cancelAt) token.Cancel();
            return 0;
        });
        var regex = Create(@"\w+@", TimeSpan.FromSeconds(1));
        var input = Encoding.UTF8.GetBytes("word@" + new string('!', 131072));
        Assert.Equal(1, regex.Count(input, 0, token.Token));
        Assert.True(reads > 1);
        cancelAt = reads;
        reads = 0;
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, 0, token.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }

    private static Utf8PythonRegex Create(string pattern, TimeSpan? timeout = null)
        => new(pattern, PythonReCompileOptions.None, timeout ?? Timeout.InfiniteTimeSpan, Policy);
}
