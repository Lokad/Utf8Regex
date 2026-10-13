using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralCensusBoundaryTests
{
    private delegate Utf8SelectedCountKernelMetrics Census(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(17)] [InlineData(31)]
    public void CorrelatedCensusPreservesOffsetsTailsCandidatesAndNonoverlap(int offset)
    {
        var method = typeof(Utf8LiteralCompiledEngineRuntime).GetMethod("InspectCorrelatedTwoBytePrefixMetrics",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var census = method.CreateDelegate<Census>();
        foreach (var literalText in new[] { "éé", "аб", "éabc" })
        for (var length = 0; length <= 97; length++)
        {
            var literal = Encoding.UTF8.GetBytes(literalText);
            var bytes = Enumerable.Repeat((byte)'x', offset + length + 11).ToArray();
            var input = bytes.AsSpan(offset, length);
            for (var start = 0; start <= length - literal.Length; start += 7) literal.CopyTo(input[start..]);
            var expected = 0;
            for (var start = 0; start <= length - literal.Length;)
            {
                if (input.Slice(start, literal.Length).SequenceEqual(literal)) { expected++; start += literal.Length; }
                else start++;
            }
            var metrics = census(input, literal);
            Assert.Equal(expected, metrics.Matches);
            Assert.Equal(metrics.Candidates, metrics.FullVerifications);
            Assert.True(metrics.Candidates >= metrics.Matches);
            Assert.Equal("CorrelatedTwoBytePrefix", metrics.Route);
            Assert.False(metrics.IncludesUtf8Validation);
        }
        var repeated = Encoding.UTF8.GetBytes(new string('é', 97));
        Assert.Equal(48, census(repeated, "éé"u8).Matches);
    }

    [Fact]
    public void SelectedCensusAgreesWithScalarCountAndFinitePublicControls()
    {
        var regex = new Utf8Regex("éé", RegexOptions.Compiled);
        var finite = new Utf8Regex("éé", RegexOptions.Compiled, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 8192, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
        var text = string.Concat(Enumerable.Repeat("xéééx😀éé ", 20000));
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex("éé").Count(text);
        Assert.True(regex.Inspection.DebugTryGetSelectedCountKernelMetrics(input, out var metrics));
        Assert.Equal(expected, metrics.Matches);
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expected, finite.Count(input));
        Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 0xff }).ToArray()));
    }
}
