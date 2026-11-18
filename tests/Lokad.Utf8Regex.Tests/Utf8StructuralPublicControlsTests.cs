using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8StructuralPublicControlsTests
{
    [Theory]
    [InlineData(false, @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "class Bravo")]
    [InlineData(true, @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "class Bravo")]
    [InlineData(false, @"\b(?:LogError|LogWarning|LogInformation)\s*\(", "LogInformation (")]
    [InlineData(true, @"\b(?:LogError|LogWarning|LogInformation)\s*\(", "LogInformation (")]
    public void StructuralPatternsPreserveScalarResultsAndInvocationControls(bool compiled, string pattern, string value)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 2048,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 };
        foreach (var limits in new Utf8ResourcePolicy?[] { null, policy })
        {
            var regex = limits is { } finite
                ? new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = Regex.InfiniteMatchTimeout,
                    ResourcePolicy = finite
                })                : new Utf8Regex(pattern, options: options);
            var input = Encoding.UTF8.GetBytes("é😀 " + value);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(1, regex.Count(input));
            var first = regex.Match(input);
            Assert.Equal((7, Encoding.UTF8.GetByteCount(value)), (first.StartOffsetInBytes, (first.EndOffsetInBytes - first.StartOffsetInBytes)));
            var count = 0;
            foreach (var match in regex.EnumerateMatches(input))
            {
                Assert.Equal((first.StartOffsetInBytes, (first.EndOffsetInBytes - first.StartOffsetInBytes)), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
                count++;
            }
            Assert.Equal(1, count);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            var error = Assert.Throws<OperationCanceledException>(() => regex.Count(input, canceled.Token));
            Assert.Equal(canceled.Token, error.CancellationToken);
        }
    }

    [Theory]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+")]
    public void SelectingPreparedEngineDoesNotAcquireDiscardedKernelBuffers(string pattern)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 0, ulong.MaxValue, ulong.MaxValue);
        var engine = Utf8CompiledEngineSelector.Select(regex.Inspection.PreparedRegex);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }
}
