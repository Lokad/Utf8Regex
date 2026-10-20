using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Benchmarks;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralMatchProbeTests
{
    [Theory]
    [InlineData("ab", "😀ab ab")]
    [InlineData("é", "😀é é")]
    [InlineData("😀", "é😀😀")]
    [InlineData("ab", "missing")]
    [InlineData("cat|dog", "dog cat")]
    [InlineData("cat|dog", "😀dog cat")]
    [InlineData("a|ab", "ab")]
    [InlineData("ab|a", "ab")]
    public void AvailableMatchProbesPreservePublicRangesAndBranchPriority(string pattern, string text)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(pattern, options);
            var bytes = Encoding.UTF8.GetBytes(text);
            foreach (var offset in new[] { 0, 1, 31 })
            {
                var storage = new byte[offset + bytes.Length + 32];
                bytes.CopyTo(storage.AsSpan(offset));
                var input = storage.AsSpan(offset, bytes.Length);
                var expected = regex.Match(input);
                if (regex.Inspection.DebugTryMatchExactLiteral(input, out var match))
                {
                    Assert.Equal(expected.Success, match.Success);
                    if (match.Success)
                    {
                        Assert.Equal(expected.IndexInBytes, match.IndexInBytes);
                        Assert.Equal(expected.LengthInBytes, match.LengthInBytes);
                        Assert.Equal(expected.IndexInUtf16, match.IndexInUtf16);
                        Assert.Equal(expected.LengthInUtf16, match.LengthInUtf16);
                    }
                }
                if (regex.Inspection.DebugTryIsMatchExactLiteral(input, out var exact))
                    Assert.Equal(expected.Success, exact);
                if (regex.Inspection.DebugTryIsMatchLiteralFamily(input, out var family))
                    Assert.Equal(expected.Success, family);
                if (regex.Inspection.DebugTryMatchCompiledAsciiLiteralFamilyRaw(input, out var start, out var length))
                {
                    Assert.True(expected.Success);
                    Assert.Equal(expected.IndexInBytes, start);
                    Assert.Equal(expected.LengthInBytes, length);
                }
            }
        }
    }

    [Fact]
    public void EligibleProbesRunExistingExactAndFamilyKernels()
    {
        var exact = new Utf8Regex("é");
        Assert.True(exact.Inspection.DebugTryIsMatchExactLiteral("😀é"u8, out var found));
        Assert.True(found);
        Assert.True(exact.Inspection.DebugTryMatchExactLiteral("😀é"u8, out var match));
        Assert.Equal(4, match.IndexInBytes);
        Assert.Equal(2, match.IndexInUtf16);
        var family = new Utf8Regex("cat|dog");
        Assert.True(family.Inspection.DebugTryIsMatchLiteralFamily("dog"u8, out found));
        Assert.True(found);
        Assert.True(family.Inspection.DebugTryMatchCompiledAsciiLiteralFamilyRaw("dog"u8, out var start, out var length));
        Assert.Equal((0, 3), (start, length));
    }
}
