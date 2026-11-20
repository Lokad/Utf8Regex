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
            using var regex = Utf8PythonRegex.Compile("[A-Z]{3}[0-9]{3}", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 10000,
                    MaxDepth = 1,
                    MaxWorkingBytes = maximum,
                    MaxResultBytes = 4096
                },
                ConstructionPolicy = default,
                ConstructionTimeout = Timeout.InfiniteTimeSpan
            }, cancellationToken: default);
            Assert.True(regex.Match("ABC123"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
            Assert.False(regex.Match("ABC12x"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleCaptureSlotsAreAdmittedWithoutUnneededTrail(bool allow)
    {
        using var regex = Utf8PythonRegex.Compile("([A-Z]{3})[0-9]{3}", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 10000,
                MaxDepth = 1,
                MaxWorkingBytes = allow ? 256UL : 1UL,
                MaxResultBytes = 4096
            },
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default);
        if (allow) Assert.True(regex.Match("ABC123"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        else
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match("ABC123"u8, mode: Utf8PythonMatchMode.FullMatch));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        }
    }

    [Fact]
    public void EagerCompileUsesItsConstructionAllowanceBeforeTinyInvocation()
    {
        const ulong workingLimit = 1024;
        var regex = Utf8PythonRegex.Compile(string.Concat(Enumerable.Repeat("(a?)", 256)), options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 128,
                MaxDepth = 16,
                // The capture storage alone exceeds this limit, even if
                // empty optional atoms need no backtracking frames.
                MaxWorkingBytes = workingLimit,
                MaxResultBytes = 1024
            },
            ConstructionPolicy = new()
            {
                MaxWorkSteps = 16000000,
                MaxDepth = 4096,
                MaxInstructionCapacity = 1048576,
                MaxBytes = 8 << 20
            },
            ConstructionTimeout = TimeSpan.FromSeconds(30)
        }, cancellationToken: CancellationToken.None);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match([], mode: Utf8PythonMatchMode.FullMatch));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        Assert.Equal(workingLimit, failure.Limit);
    }


}
