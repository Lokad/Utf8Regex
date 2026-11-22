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
            var regex = new Utf8Regex(pattern, options: options);
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
                        Assert.Equal((expected.StartOffsetInBytes, expected.EndOffsetInBytes),
                            (match.StartOffsetInBytes, match.EndOffsetInBytes));
                    }
                }
                if (regex.Inspection.DebugTryIsMatchExactLiteral(input, out var exact))
                    Assert.Equal(expected.Success, exact);
                if (regex.Inspection.DebugTryIsMatchLiteralFamily(input, out var family))
                    Assert.Equal(expected.Success, family);
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
        Assert.Equal(4, match.StartOffsetInBytes);
        var family = new Utf8Regex("cat|dog");
        Assert.True(family.Inspection.DebugTryIsMatchLiteralFamily("dog"u8, out found));
        Assert.True(found);
        Assert.True(family.TryMatchDirectWithoutValidation("dog"u8, out var familyMatch));
        Assert.Equal((0, 3), (familyMatch.StartOffsetInBytes, familyMatch.EndOffsetInBytes));
    }

    [Fact]
    public void DirectFoldedMatchUsesTheExistingScalarSelection()
    {
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        using var folded = new Utf8Regex("kilo", options);
        Assert.True(folded.TryMatchDirectWithoutValidation("😀KILOé"u8, out var direct));
        Assert.Equal(new Utf8CaptureRange(4, 8), direct);
        Assert.Equal(direct, folded.Match("😀KILOé"u8));
        Assert.True(folded.IsMatch("😀KILOé"u8));
        Assert.False(folded.TryMatchDirectWithoutValidation("KILO KILO"u8, out _));
        Assert.Equal(new Utf8CaptureRange(0, 6), folded.Match("KILO KILO"u8));

        using var word = new Utf8Regex(@"\bkilo\b", options);
        Assert.False(word.TryMatchDirectWithoutValidation("éKILO"u8, out _));
        Assert.False(word.IsMatch("éKILO"u8));
    }
}
