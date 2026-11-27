using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralMatchingTests
{
    [Theory]
    [InlineData("ab", "😀ab ab")]
    [InlineData("é", "😀é é")]
    [InlineData("😀", "é😀😀")]
    [InlineData("ab", "missing")]
    [InlineData("cat|dog", "dog cat")]
    [InlineData("cat|dog", "😀dog cat")]
    [InlineData("汉|汉字😀|字", "é汉字😀汉😀字")]
    [InlineData("a|ab", "ab")]
    [InlineData("ab|a", "ab")]
    public void LiteralMatchingPreservesByteRangesAndBranchPriority(string pattern, string text)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft })
        {
            using var regex = new Utf8Regex(pattern, options: options);
            var oracle = new Regex(pattern, options).Match(text);
            var expected = oracle.Success
                ? new Utf8CaptureRange(Encoding.UTF8.GetByteCount(text.AsSpan(0, oracle.Index)),
                    Encoding.UTF8.GetByteCount(text.AsSpan(0, oracle.Index + oracle.Length)))
                : default;
            var runtime = regex.Inspection.CompiledRuntime;
            var bytes = Encoding.UTF8.GetBytes(text);
            foreach (var offset in new[] { 0, 1, 31 })
            {
                var storage = new byte[offset + bytes.Length + 32];
                bytes.CopyTo(storage.AsSpan(offset));
                var input = storage.AsSpan(offset, bytes.Length);
                Assert.Equal(expected, regex.Match(input));
                Assert.Equal(expected.Success, regex.IsMatch(input));
                if (runtime is not null)
                {
                    Assert.Equal(expected, runtime.Match(input, Utf8ExecutionDeadline.Infinite));
                    Assert.Equal(expected.Success, runtime.IsMatch(input, Utf8ExecutionDeadline.Infinite));
                }
            }
        }
    }

    [Fact]
    public void ExactAndFamilyKernelsReturnByteRanges()
    {
        using var exact = new Utf8Regex("é");
        var exactRuntime = Assert.IsType<Utf8LiteralCompiledEngineRuntime>(exact.Inspection.CompiledRuntime);
        Assert.Equal(4, exactRuntime.FindFirstLiteralViaSearch("😀é"u8, Utf8ExecutionDeadline.Infinite));
        Assert.Equal(new Utf8CaptureRange(4, 6),
            exactRuntime.MatchExactUtf8Literal("😀é"u8, Utf8ExecutionDeadline.Infinite, rightToLeft: false));
        using var family = new Utf8Regex("cat|dog");
        var familyRuntime = Assert.IsType<Utf8LiteralCompiledEngineRuntime>(family.Inspection.CompiledRuntime);
        Assert.True(familyRuntime.IsMatchLiteralFamily("dog"u8, Utf8ExecutionDeadline.Infinite, rightToLeft: false));
        Assert.Equal(new Utf8CaptureRange(0, 3), familyRuntime.Match("dog"u8, Utf8ExecutionDeadline.Infinite));
    }

    [Fact]
    public void FoldedMatchReturnsByteRangesForAsciiAndKelvinMatches()
    {
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        using var folded = new Utf8Regex("kilo", options);
        Assert.Equal(new Utf8CaptureRange(4, 8), folded.Match("😀KILOé"u8));
        Assert.True(folded.IsMatch("😀KILOé"u8));
        Assert.Equal(new Utf8CaptureRange(0, 6), folded.Match("KILO KILO"u8));

        using var word = new Utf8Regex(@"\bkilo\b", options);
        Assert.False(word.Match("éKILO"u8).Success);
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
    }
}
