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
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(@"[+\p{" + name + "}]+", options, inputValidation: validation);
            Assert.Equal(expected, regex.Count(bytes));
            if (validation == Utf8InputValidation.Validate)
                Assert.Throws<ArgumentException>(() => regex.Count([.. bytes, 0xff]));
        }
    }

    [Fact]
    public void WiderCategoryCapturesKeepByteCoordinatesUnderFiniteLimits()
    {
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8Regex(@"([+\p{L}]+)-[+\p{Nd}]+", resourcePolicy: new()
                { MaxWorkSteps = 10000, MaxWorkingBytes = 65536, MaxResultBytes = 65536 },
                inputValidation: validation);
            var match = regex.MatchDetailed("😀 é+𝒜-𝟙+ !"u8);
            Assert.True(match.Success);
            Assert.Equal(5, match.IndexInBytes);
            Assert.Equal(13, match.LengthInBytes);
            Assert.Equal("é+𝒜", match.GetGroup(1).GetValueString());
            Assert.Equal(5, match.GetGroup(1).IndexInBytes);
            Assert.Equal(7, match.GetGroup(1).LengthInBytes);
        }
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
