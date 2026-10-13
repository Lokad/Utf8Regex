using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLazyFullMatchAuthorityTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ColdFullMatchDoesNotRestartDeadlineAfterPreparation(int result)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(200));
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var construction = Utf8ConstructionScope.Enter();
        var checkpoints = 0;
        construction.Budget.CheckpointHook = _ => { if (++checkpoints == 8) ticks = 250; };
        var error = Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, result, CancellationToken.None));
        Assert.Equal(TimeSpan.FromMilliseconds(200), error.MatchTimeout);
        Assert.Equal("😀a", error.Input);
        Assert.True(checkpoints >= 8);
        Assert.True(regex.DebugIsScalarFullProgramValueCreated);
        // A fresh invocation can reuse the successfully prepared immutable program.
        ticks = 0;
        Assert.Equal("x", regex.FullMatchDetailedData("😀a"u8).LastGroup);
        Assert.Equal(1, regex.FullMatchDetailedData("😀a"u8).LastIndex);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ColdPreparationCancellationUsesOriginalTokenAndAllowsFreshCall(int result)
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+");
        using var cancellation = new CancellationTokenSource();
        using var construction = Utf8ConstructionScope.Enter();
        var checkpoints = 0;
        construction.Budget.CheckpointHook = _ => { if (++checkpoints == 8) cancellation.Cancel(); };
        var error = Assert.Throws<OperationCanceledException>(() => Run(regex, result, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.True(checkpoints >= 8);
        Assert.True(regex.DebugIsScalarFullProgramValueCreated);
        Assert.True(regex.FullMatch("😀a"u8).Success);
    }

    [Fact]
    public void PreparationKeepsSeparateBackstopAndFiniteExecutionOwner()
    {
        var regex = new Utf8PythonRegex("(?P<x>😀|a)+", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FullMatch("😀a"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(1ul, error.Limit);
        Assert.Equal(2ul, error.Requested);
        Assert.True(regex.DebugIsScalarFullProgramValueCreated);
        var again = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FullMatch("😀a"u8));
        Assert.Equal(error.ResourceKind, again.ResourceKind);
        Assert.Equal(error.Requested, again.Requested);
    }

    private static void Run(Utf8PythonRegex regex, int result, CancellationToken token)
    {
        if (result == 0) _ = regex.FullMatch("😀a"u8, 0, token);
        else if (result == 1) _ = regex.FullMatchDetailed("😀a"u8, 0, token);
        else _ = regex.FullMatchDetailedData("😀a"u8, 0, token);
    }
}
