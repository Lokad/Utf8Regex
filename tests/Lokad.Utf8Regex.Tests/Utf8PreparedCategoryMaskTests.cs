using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PreparedCategoryMaskTests
{
    [Theory]
    [MemberData(nameof(Utf8SingleCategoryCountTests.Categories), MemberType = typeof(Utf8SingleCategoryCountTests))]
    public void CategoryRunsAgreeWithScalarOracleForEveryAsciiByte(string name, UnicodeCategory category)
    {
        var text = string.Concat(Enumerable.Range(0, 128).Select(value => (char)value)) +
            "éΩ中😀𐐀𐐨𝟙𝛁𝒜𝓪∑¥\u0378\u0300+";
        var expected = 0; var previous = false;
        foreach (var scalar in text.EnumerateRunes())
        {
            var hit = scalar.Value == '+' || Rune.GetUnicodeCategory(scalar) == category;
            if (hit && !previous) expected++;
            previous = hit;
        }
        var bytes = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(@"[+\p{" + name + "}]+", options: options);
            Assert.Equal(expected, regex.Count(bytes));
        }
    }

    [Fact]
    public void WiderCategoryCapturesKeepByteCoordinatesUnderFiniteLimits()
    {
        {
            var regex = new Utf8Regex(@"([+\p{L}]+)-[+\p{Nd}]+", policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 10000,
                    MaxWorkingBytes = 65536,
                    MaxResultBytes = 65536
                }
            });
            var match = Utf8MatchSequence.First(regex.CreateMatchCursor("😀 é+𝒜-𝟙+ !"u8.ToArray()));
            Assert.True(match.Success);
            Assert.Equal(5, match.Value.StartOffsetInBytes);
            Assert.Equal(13, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes));
            Assert.Equal("é+𝒜", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
            Assert.Equal(5, (match.TryGetGroup(1, out var range1) ? range1.StartOffsetInBytes : default));
            Assert.Equal(7, ((match.TryGetGroup(1, out var range2) ? range2.EndOffsetInBytes : default) - (match.TryGetGroup(1, out var range3) ? range3.StartOffsetInBytes : default)));
        }
    }

    [Fact]
    public void IgnoreCaseDecimalRunKeepsWideScalarsAndCaptureHistory()
    {
        using var regex = new Utf8Regex(@"^(?<row>(?<digit>\d)+)[a-z]$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            new() { ResourcePolicy = new() { MaxWorkSteps = 128, MaxWorkingBytes = 4096 } });
        using var source = new CancellationTokenSource();
        var input = "١𝟙K\n"u8.ToArray();
        var range = new Utf8CaptureRange(0, 9);
        Assert.Equal(range, regex.Match(input, cancellationToken: source.Token));
        Assert.Equal(1, regex.Count(input, source.Token));
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: source.Token));
        Assert.Equal(range, match.Value);
        Assert.Equal(new Utf8CaptureRange(0, 6), Assert.Single(match.GetCaptures(1).ToArray()));
        Assert.Equal(new[] { new Utf8CaptureRange(0, 2), new Utf8CaptureRange(2, 6) }, match.GetCaptures(2).ToArray());
    }

    [Fact]
    public void DisjointDecimalRunsWithOmittedCapturesNeedNoRewindStorage()
    {
        using var regex = new Utf8Regex(@"^[a-z]((\d)+):?[a-z]((\d)+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase,
            new() { ResourcePolicy = new() { MaxWorkSteps = 128, MaxWorkingBytes = 1 } });
        using var source = new CancellationTokenSource();
        Assert.Equal(new Utf8CaptureRange(0, 12), regex.Match("K١𝟙A٢"u8, cancellationToken: source.Token));
    }

    [Fact]
    public void LoweringDoesNotMutateMasksBorrowedByOtherTrees()
    {
        var mask = new byte[16]; mask['+' >> 3] = (byte)(1 << ('+' & 7));
        var original = mask.ToArray();
        var letters = Utf8SliceNode.CategorySetMember.Create(
            Utf8SliceCategoryTable.Create(1u << (int)UnicodeCategory.UppercaseLetter, []), false);
        var source = new Utf8SliceNode.CategorySetNode(mask, false, [], [letters], false);
        Assert.NotNull(Utf8SliceLowerer.Lower(source, null, false, false, false));
        Assert.Equal(original, mask);
        var digits = Utf8SliceNode.CategorySetMember.Create(
            Utf8SliceCategoryTable.Create(1u << (int)UnicodeCategory.DecimalDigitNumber, []), false);
        var reused = source with { Categories = [digits] };
        var program = Utf8SliceLowerer.Lower(reused, null, false, false, false)!;
        var instruction = program.Instructions[0];
        Assert.Equal(0, program.SetMasks[instruction.A]['A' >> 3] & (1 << ('A' & 7)));
        Assert.Equal(original, mask);
    }
}
