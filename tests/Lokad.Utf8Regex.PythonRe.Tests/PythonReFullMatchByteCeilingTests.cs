using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFullMatchByteCeilingTests
{
    [Fact]
    public void PureClassFusionNeedsNoRepeatVectors()
    {
        foreach (var maximum in new[] { 1UL, 64UL, 256UL })
        {
            using var regex = Utf8PythonRegex.Compile("[A-Z]{3}[0-9]{3}",
                PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
                new() { MaxWorkSteps = 10000, MaxDepth = 1, MaxWorkingBytes = maximum, MaxResultBytes = 4096 },
                default, Timeout.InfiniteTimeSpan, default);
            Assert.True(regex.Match("ABC123"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
            Assert.False(regex.Match("ABC12x"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleCaptureSlotsAreAdmittedWithoutUnneededTrail(bool allow)
    {
        using var regex = Utf8PythonRegex.Compile("([A-Z]{3})[0-9]{3}",
            PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 10000, MaxDepth = 1, MaxWorkingBytes = allow ? 256UL : 1UL, MaxResultBytes = 4096 },
            default, Timeout.InfiniteTimeSpan, default);
        if (allow) Assert.True(regex.Match("ABC123"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        else
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match("ABC123"u8,mode: Utf8PythonMatchMode.FullMatch));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnfundedColdPreparationDoesNotPublishAndRestoresParent(bool liveToken)
    {
        var regex = new Utf8PythonRegex(string.Concat(Enumerable.Repeat("(a?)", 256)),
            PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 128, MaxDepth = 16, MaxWorkingBytes = 4096, MaxResultBytes = 1024 });
        using var token = new CancellationTokenSource();
        using var parent = Utf8ConstructionScope.Enter();
        _ = parent.Budget.AllocateArray<byte>(17, false);
        var reserved = parent.Budget.ReservedBytes;
        var maximum = parent.Budget.MaximumBytes;
        Action<ulong> hook = _ => { };
        parent.Budget.CheckpointHook = hook;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var spent = parent.Budget.WorkSteps;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                regex.Match([],0,cancellationToken: liveToken ? token.Token : default,mode: Utf8PythonMatchMode.FullMatch));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.True(failure.Requested > failure.Limit);
            Assert.False(regex.DebugIsScalarFullProgramValueCreated);
            Assert.Equal(reserved, parent.Budget.ReservedBytes);
            Assert.Equal(maximum, parent.Budget.MaximumBytes);
            Assert.Same(hook, parent.Budget.CheckpointHook);
            Assert.Equal(0ul, parent.Budget.Depth);
            Assert.True(parent.Budget.WorkSteps > spent);
        }
    }

    [Fact]
    public void EagerCompileUsesItsConstructionAllowanceBeforeTinyInvocation()
    {
        var regex = Utf8PythonRegex.Compile(string.Concat(Enumerable.Repeat("(a?)", 256)),
            PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 128, MaxDepth = 16, MaxWorkingBytes = 4096, MaxResultBytes = 1024 },
            new() { MaxWorkSteps = 16000000, MaxDepth = 4096, MaxInstructionCapacity = 1048576, MaxBytes = 8 << 20 },
            TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.True(regex.DebugIsScalarFullProgramValueCreated);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match([],mode: Utf8PythonMatchMode.FullMatch));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        Assert.Equal(4096ul, failure.Limit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundedColdPreparationKeepsInterruptionAndRetry(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+", PythonReCompileOptions.None,
            TimeSpan.FromMilliseconds(20), new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 256,
                MaxWorkingBytes = 16384,
                MaxResultBytes = 16384
            });
        using var token = new CancellationTokenSource();
        using var parent = Utf8ConstructionScope.Enter();
        var maximum = parent.Budget.MaximumBytes;
        var checkpoints = 0;
        Action<ulong> hook = _ =>
        {
            if (++checkpoints != 3) return;
            if (cancel) token.Cancel(); else ticks = 25;
        };
        parent.Budget.CheckpointHook = hook;
        var failure = Record.Exception(() => regex.Match("😀a"u8,0,cancellationToken: token.Token,mode: Utf8PythonMatchMode.FullMatch));
        if (cancel)
            Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
        {
            var timeout = Assert.IsType<RegexMatchTimeoutException>(failure);
            Assert.Equal(TimeSpan.FromMilliseconds(20), timeout.MatchTimeout);
            Assert.Empty(timeout.Input);
        }
        Assert.Equal(3, checkpoints);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        Assert.Equal(0ul, parent.Budget.ReservedBytes);
        Assert.Equal(maximum, parent.Budget.MaximumBytes);
        Assert.Same(hook, parent.Budget.CheckpointHook);
        ticks = 0;
        parent.Budget.CheckpointHook = null;
        Utf8PythonRangeMatch retained = default;
        using (var captureCursor1 = regex.CreateMatchCursor(("😀a"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out retained);
        }
        Assert.True(retained.Success);
        Assert.Equal("x", retained.LastGroup);
        Assert.True(regex.DebugIsScalarFullProgramValueCreated);
        Assert.True(regex.Match("😀a"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal("😀a", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }
}
