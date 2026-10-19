using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralCensusBoundaryTests
{
    private delegate int ProductionCount(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(17)] [InlineData(31)]
    public void ProductionCorrelatedCountRespectsBorrowedOffsetsAndVectorTails(int offset)
    {
        var method = typeof(Utf8LiteralCompiledEngineRuntime).GetMethod("CountExactUtf8LiteralCorrelatedTwoBytePrefix",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var count = method.CreateDelegate<ProductionCount>();
        foreach (var text in new[] { "éé", "аб", "éabc", "é" + new string('a', 39), new string('é', 20) })
        for (var prefix = 0; prefix < 64; prefix++)
        for (var tail = 0; tail < 32; tail++)
        {
            var literal = Encoding.UTF8.GetBytes(text);
            var length = prefix + literal.Length + tail;
            var storage = Enumerable.Repeat((byte)0xff, offset + length + 1).ToArray();
            var input = storage.AsSpan(offset, length);
            input.Fill((byte)'x');
            literal.CopyTo(input[prefix..]);
            Assert.Equal(1, count(input, literal));
        }
        var overlapping = Encoding.UTF8.GetBytes(new string('é', 97));
        Assert.Equal(48, count(overlapping, "éé"u8));
        Assert.Equal(4, count(overlapping, Encoding.UTF8.GetBytes(new string('é', 20))));
    }


    [Fact]
    public void ScalarCountAgreesWithFinitePublicControls()
    {
        var regex = new Utf8Regex("éé", RegexOptions.Compiled);
        var finite = new Utf8Regex("éé", RegexOptions.Compiled, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 8192, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
        var text = string.Concat(Enumerable.Repeat("xéééx😀éé ", 20000));
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex("éé").Count(text);
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expected, finite.Count(input));
        Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 0xff }).ToArray()));
    }
}
