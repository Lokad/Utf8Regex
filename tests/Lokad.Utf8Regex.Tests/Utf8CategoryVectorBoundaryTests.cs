using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CategoryVectorBoundaryTests
{
    private delegate int VectorCounter(ReadOnlySpan<byte> input, ref int index);

    [Theory]
    [InlineData(128)]
    [InlineData(256)]
    public void FullVectorBlocksPreserveEntryOffsetAndTail(int width)
    {
        var method = typeof(Utf8UnicodeCategoryExecutor).GetMethod("CountAsciiMathSymbolsVector" + width,
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var count = method.CreateDelegate<VectorCounter>();
        foreach (var offset in new[] { 0, 1, 7, 17, 31 })
        for (var length = 0; length <= 97; length++)
        {
            var bytes = Enumerable.Repeat((byte)'x', offset + length + 11).ToArray();
            for (var i = 0; i < length; i++) bytes[offset + i] = "+<=>|~x"u8[i % 7];
            var input = bytes.AsSpan(0, offset + length);
            var index = offset;
            var consumed = length / (width / 8) * (width / 8);
            var expected = 0;
            for (var i = 0; i < consumed; i++) if ("+<=>|~"u8.Contains(bytes[offset + i])) expected++;
            Assert.Equal(expected, count(input, ref index));
            Assert.Equal(offset + consumed, index);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(17)] [InlineData(31)]
    public void PublicCategoryCountKeepsScalarAndBorrowedBoundarySemantics(int offset)
    {
        var ordinary = new Utf8Regex(@"\p{Sm}");
        var compiled = new Utf8Regex(@"\p{Sm}", RegexOptions.Compiled);
        var finite = new Utf8Regex(@"\p{Sm}", RegexOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 8192, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
        for (var length = 0; length <= 97; length++)
        foreach (var suffix in new[] { "", "é", "😀", "𝛁", "∑" })
        {
            var text = new string(Enumerable.Range(0, length).Select(i => "+<=>|~x"[i % 7]).ToArray()) + suffix + "+";
            var data = Encoding.UTF8.GetBytes(text);
            var storage = new byte[offset + data.Length + 11];
            data.CopyTo(storage.AsSpan(offset));
            var input = storage.AsSpan(offset, data.Length);
            var expected = text.EnumerateRunes().Count(rune => Rune.GetUnicodeCategory(rune) == UnicodeCategory.MathSymbol);
            Assert.Equal(expected, Utf8UnicodeCategoryExecutor.CountCategory(input, true, UnicodeCategory.MathSymbol));
            Assert.Equal(expected, ordinary.Count(input));
            Assert.Equal(expected, compiled.Count(input));
            Assert.Equal(expected, finite.Count(input));
        }
    }
}
