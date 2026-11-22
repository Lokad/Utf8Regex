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

    [Fact]
    public void FoldedStarterScanPreservesKelvinOrderAndScalarBoundaries()
    {
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        using var folded = new Utf8Regex("kilo", options);
        var tail = new string('!', 96) + "KILO";
        foreach (var offset in new[] { 0, 59, 60, 61, 64 })
        {
            var subject = Encoding.UTF8.GetBytes(new string('!', offset) + "KILO😀" + tail);
            Assert.True(folded.IsMatch(subject));
            Assert.Equal(new Utf8CaptureRange(offset, offset + 4), folded.Match(subject));
        }

        using var overlapping = new Utf8Regex("kk", options);
        var earlierAlias = Encoding.UTF8.GetBytes("KKK" + tail);
        Assert.Equal(new Utf8CaptureRange(0, 4), overlapping.Match(earlierAlias));

        using var lateAlias = new Utf8Regex("abcdk", options);
        var aliasAtEnd = Encoding.UTF8.GetBytes(new string('!', 96) + "ABCDK");
        Assert.True(lateAlias.IsMatch(aliasAtEnd));
        Assert.Equal(new Utf8CaptureRange(96, 103), lateAlias.Match(aliasAtEnd));

        var decoys = Encoding.UTF8.GetBytes("K!€" + new string('!', 96) + "KILO");
        Assert.Equal(new Utf8CaptureRange(101, 105), folded.Match(decoys));

        using var word = new Utf8Regex(@"\bkilo\b", options);
        var noBoundary = Encoding.UTF8.GetBytes(new string('!', 60) + "KILOé" + new string('!', 96));
        Assert.False(word.IsMatch(noBoundary));
        Assert.False(word.Match(noBoundary).Success);

        using var limited = new Utf8Regex("kilo", options, new() { ResourcePolicy = new() { MaxWorkSteps = 1 } });
        var early = Encoding.UTF8.GetBytes("KILO" + tail);
        Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Match(early));
        Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.IsMatch(early));
    }
}
