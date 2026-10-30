using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ReplacementPreparationAuthorityTests
{
    [Theory]
    [InlineData(4093, 3)] [InlineData(4094, 3)] [InlineData(4095, 3)] [InlineData(4096, 3)]
    [InlineData(4093, 4)] [InlineData(4094, 4)] [InlineData(4095, 4)] [InlineData(4096, 4)]
    public void InvalidUtf8TemplateKeepsAbsoluteOffsetsBeforeInputAndDestinationWork(int prefix, int route)
    {
        var regex = new Utf8Regex("(a)", RegexOptions.CultureInvariant);
        var bytes = Enumerable.Repeat((byte)'x', prefix).Concat(new byte[] { 0xed, 0xa0, 0x80 }).ToArray();
        var destination = Enumerable.Repeat((byte)0x5a, 8).ToArray();
        using var token = new CancellationTokenSource();
        var failure = Assert.Throws<ArgumentException>(() => Replace(regex, route, [0xff], "", bytes, destination, token.Token));
        Assert.Equal("byteOffset", failure.ParamName);
        Assert.Contains($"byte offset {prefix}.", failure.Message);
        Assert.Equal(0, CacheCount(regex));
        Assert.All(destination, value => Assert.Equal((byte)0x5a, value));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void ForeignPreparationInterruptionDoesNotBecomeTheUncontrolledCallersFailure(bool deadline, bool native)
    {
        var regex = new Utf8Regex("(a)", RegexOptions.CultureInvariant);
        var template = string.Concat(Enumerable.Repeat("$1", 64));
        var templateBytes = Encoding.UTF8.GetBytes(template);
        using var foreign = new CancellationTokenSource();
        foreign.Cancel();
        using var parent = Utf8ConstructionScope.Enter();
        var calls = 0;
        parent.Budget.CheckpointHook = _ =>
        {
            if (++calls != 32) return;
            if (deadline) throw new Utf8ExecutionDeadlineExpiredException();
            throw new OperationCanceledException(foreign.Token);
        };
        Assert.Equal(new string('a', 64), Encoding.UTF8.GetString(native ? regex.Replace("a"u8, templateBytes) : regex.Replace("a"u8, template)));
        Assert.Equal(0, CacheCount(regex)); // The independent retry is not a shared publication.
        parent.Budget.CheckpointHook = null;
        Assert.Equal(new string('a', 64), Encoding.UTF8.GetString(native ? regex.Replace("a"u8, templateBytes) : regex.Replace("a"u8, template)));
        Assert.Equal(1, CacheCount(regex));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void NullTemplatePrecedesAnElapsedClock(int route)
    {
        var regex = new Utf8Regex("(a)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks += 100);
        var failure = Assert.Throws<ArgumentNullException>(() => Replace(regex, route, "a"u8.ToArray(), null!, [], new byte[8], CancellationToken.None));
        Assert.Equal("replacement", failure.ParamName);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void SuccessfulPreparationDoesNotRestartTheRemainingMatchingDeadline(int route)
    {
        var regex = new Utf8Regex("(a)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20),
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000 });
        var template = string.Concat(Enumerable.Repeat("$1", 32));
        var templateBytes = Encoding.UTF8.GetBytes(template);
        var destination = new byte[8192];
        var input = "aaa"u8.ToArray();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var parent = Utf8ConstructionScope.Enter();
        var calls = 0;
        parent.Budget.CheckpointHook = _ => { if (++calls == 32) ticks = 15; };
        var entered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps > 0) { entered = true; ticks = 25; }
        });
        RegexMatchTimeoutException failure;
        try
        {
            failure = Assert.Throws<RegexMatchTimeoutException>(() => Replace(regex, route, input, template, templateBytes,
                destination, CancellationToken.None));
        }
        finally { diagnostics.Complete(); }
        Assert.True(entered);
        Assert.Equal(TimeSpan.FromMilliseconds(20), failure.MatchTimeout);
        Assert.Equal(1, CacheCount(regex)); // Fully prepared code survives later interruption.
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void TimeoutOnlyCallsIncludePreparationWithoutForcingAToken(int route)
    {
        var regex = new Utf8Regex("(a)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
        var template = string.Concat(Enumerable.Repeat("$1", 128));
        var input = "a"u8.ToArray();
        var bytes = Encoding.UTF8.GetBytes(template);
        var destination = new byte[1024];
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var parent = Utf8ConstructionScope.Enter();
        var calls = 0;
        parent.Budget.CheckpointHook = _ => { if (++calls == 32) ticks = 25; };
        Assert.Throws<RegexMatchTimeoutException>(() => Replace(regex, route, input, template, bytes, destination, CancellationToken.None));
        Assert.Equal(0, CacheCount(regex));
    }

    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void Utf8TemplateKeyIsAdmittedBeforeItsMajorAllocation(int route)
    {
        var regex = new Utf8Regex("(a)", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000 });
        _ = regex.Replace("a"u8, "$1"u8); // Warm the codec/cache/exception call paths.
        var template = new string('x', 32768) + "$1";
        var bytes = Encoding.UTF8.GetBytes(template);
        var input = "a"u8.ToArray();
        var destination = new byte[16];
        var keyBytes = Utf8ConstructionBudget.ArrayBytes<byte>(bytes.Length);
        var parentBytes = Utf8ConstructionBudget.ArrayBytes<byte>(1);
        using var parent = new Utf8ConstructionScope(ulong.MaxValue, parentBytes + keyBytes - 1);
        var payload = parent.Budget.AllocateArray<byte>(1, false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Replace(regex, route, input, template, bytes,
            destination, CancellationToken.None));
        var allocated = (ulong)(GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(allocated < keyBytes, $"Denied template acquired {allocated} bytes before refusal.");
        Assert.Equal(parentBytes, parent.Budget.ReservedBytes);
        Assert.Equal(1, CacheCount(regex)); // Only the earlier small template exists.
        GC.KeepAlive(payload);
    }

    private static int CacheCount(Utf8Regex regex)
    {
        var cache = typeof(Utf8Regex).GetField("_replacementCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;
        return (int)cache.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache)!;
    }

    private static void Replace(Utf8Regex regex, int route, byte[] input, string template, byte[] templateBytes,
        byte[] destination, CancellationToken token)
    {
        switch (route)
        {
            case 0: _ = regex.Replace(input, template, token); break;
            case 1: _ = regex.ReplaceToString(input, template, token); break;
            case 2: _ = regex.TryReplace(input, template, destination, out _, token); break;
            case 3: _ = regex.Replace(input, templateBytes, token); break;
            default: _ = regex.TryReplace(input, templateBytes, destination, out _, token); break;
        }
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)] [InlineData(4, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)] [InlineData(4, true)]
    public void ColdPreparationUsesCallerAuthorityAndRollsBackBeforePublication(int route, bool timeout)
    {
        var regex = new Utf8Regex("(a)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20),
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 1024, MaxWorkingBytes = 1UL << 20, MaxResultBytes = 1UL << 20 });
        var template = string.Concat(Enumerable.Repeat("$1", 2048));
        var templateBytes = Encoding.UTF8.GetBytes(template);
        var destination = Enumerable.Repeat((byte)0x5a, 8192).ToArray();
        var input = "a"u8.ToArray();
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var parent = Utf8ConstructionScope.Enter();
        var parentPayload = parent.Budget.AllocateArray<byte>(1, false);
        var held = parent.Budget.ReservedBytes;
        var parentDeadline = parent.Budget.Deadline;
        var calls = 0;
        Action<ulong> hook = _ =>
        {
            if (++calls != 32) return;
            if (timeout) ticks = 25; else token.Cancel();
        };
        parent.Budget.CheckpointHook = hook;

        var failure = Record.Exception(() => Replace(regex, route, input, template, templateBytes, destination, token.Token));
        Assert.True(calls >= 32);
        if (timeout)
        {
            var expired = Assert.IsType<RegexMatchTimeoutException>(failure);
            Assert.Equal(TimeSpan.FromMilliseconds(20), expired.MatchTimeout);
            Assert.Equal(string.Empty, expired.Input);
        }
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(0, CacheCount(regex));
        Assert.Equal(held, parent.Budget.ReservedBytes);
        Assert.Same(parentDeadline, parent.Budget.Deadline);
        Assert.Equal(default, parent.Budget.CancellationToken);
        Assert.Same(hook, parent.Budget.CheckpointHook);
        Assert.All(destination, value => Assert.Equal((byte)0x5a, value));

        parent.Budget.CheckpointHook = null;
        var result = regex.Replace(input, template);
        Assert.Equal(new string('a', 2048), Encoding.UTF8.GetString(result));
        Assert.Equal(1, CacheCount(regex));
        GC.KeepAlive(parentPayload);
    }
}
